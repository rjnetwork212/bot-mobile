using BotMobile.Models;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotMobile.Services;

public class BotService
{
    public event Action<string>? Log;

    readonly SemaphoreSlim _slots = new(3); // max 3 login paralel
    IBrowser? _browser;
    string _chromePath = "";

    // ---------- chrome ----------

    public static string FindChrome(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        string[] candidates = OperatingSystem.IsWindows()
            ? new[]
              {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
              }
            : new[] { "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/usr/bin/chromium", "/usr/bin/chromium-browser", "/snap/bin/chromium" };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Chrome tidak ditemukan. Isi path Chrome manual lalu Simpan.");
    }

    async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsClosed: false }) return _browser;
        if (string.IsNullOrWhiteSpace(_chromePath) || !File.Exists(_chromePath))
            _chromePath = FindChrome(_chromePath);
        Log?.Invoke($"launch Chrome: {_chromePath}");
        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = _chromePath,
            Headless = false,
            DefaultViewport = null,
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
                "--no-first-run",
                "--disable-infobars",
                "--lang=id-ID",
                "--disable-notifications",
            },
        });
        return _browser;
    }

    // ---------- fingerprint mobile (FB IAB / seperti APK) ----------

    record Device(string Model, int W, int H, double Dpr);

    static readonly Device[] Devices =
    {
        new("SM-G991B", 384, 854, 2.75),
        new("Pixel 7", 412, 915, 2.625),
        new("22101316C", 393, 873, 2.75),
        new("SM-A536E", 360, 800, 3.0),
    };

    // hash deterministik (string.GetHashCode beda antar proses)
    static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (var c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    static Device DeviceFor(string uid) => Devices[StableHash(uid) % Devices.Length];

    static string BuildUa(Device d) =>
        $"Mozilla/5.0 (Linux; Android 13; {d.Model} Build/TP1A.220624.014; wv) " +
        $"AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/124.0.0.0 Mobile Safari/537.36 " +
        $"[FB_IAB/FB4A;FBAV/460.0.0.60.120;]";

    static string StealthJs()
    {
        var asm = typeof(BotService).Assembly;
        using var s = asm.GetManifestResourceStream("BotMobile.Resources.stealth.js")
            ?? throw new InvalidOperationException("embedded stealth.js hilang");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    // ---------- login ----------

    public async Task LoginAccountsAsync(System.Collections.Generic.IEnumerable<Account> accounts, Func<Account, Task> onSave)
    {
        var tasks = accounts.Select(acc => Task.Run(() => LoginOneAsync(acc, onSave)));
        await Task.WhenAll(tasks);
    }

    async Task LoginOneAsync(Account acc, Func<Account, Task> onSave)
    {
        await _slots.WaitAsync();
        try
        {
            await LoginCoreAsync(acc, onSave);
        }
        catch (Exception ex)
        {
            SetStatus(acc, "Failed", $"err: {ex.Message}");
            Log?.Invoke($"[{acc.Uid}] ERROR: {ex.Message}");
        }
        finally
        {
            _slots.Release();
        }
    }

    async Task LoginCoreAsync(Account acc, Func<Account, Task> onSave)
    {
        var browser = await GetBrowserAsync();
        var page = await browser.NewPageAsync();
        try
        {
            var dev = DeviceFor(acc.Uid);
            await page.SetUserAgentAsync(BuildUa(dev));
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = dev.W, Height = dev.H,
                DeviceScaleFactor = dev.Dpr,
                IsMobile = true, HasTouch = true,
            });
            await page.EvaluateExpressionOnNewDocumentAsync(StealthJs());
            page.DefaultNavigationTimeout = 60000;
            Log?.Invoke($"[{acc.Uid}] device={dev.Model} {dev.W}x{dev.H}@{dev.Dpr}x (FB_IAB)");

            // 1) login via cookies
            if (!string.IsNullOrWhiteSpace(acc.Cookies))
            {
                foreach (var (name, value) in ParseCookies(acc.Cookies))
                {
                    await page.SetCookieAsync(new CookieParam
                    {
                        Name = name, Value = value,
                        Domain = ".facebook.com", Path = "/",
                    });
                }
                Log?.Invoke($"[{acc.Uid}] coba login via cookies ({ParseCookies(acc.Cookies).Count} cookie)");
                await page.GoToAsync("https://www.facebook.com/", WaitUntilNavigation.DOMContentLoaded);
                await Task.Delay(4000);

                if (await IsLoggedInAsync(page, acc.Uid))
                {
                    acc.Cookies = await DumpCookiesAsync(page);
                    SetStatus(acc, "CookieOk");
                    Log?.Invoke($"[{acc.Uid}] login via cookies OK");
                    await onSave(acc);
                    return;
                }
                Log?.Invoke($"[{acc.Uid}] cookies expired/tidak valid, lanjut password");
            }

            // 2) login via uid + password (mobile web, tampilan seperti di APK)
            await page.GoToAsync("https://m.facebook.com/login/", WaitUntilNavigation.DOMContentLoaded);
            await page.WaitForSelectorAsync("input[name='email']", new WaitForSelectorOptions { Timeout = 20000 });
            await Task.Delay(800);
            await page.TypeAsync("input[name='email']", acc.Uid, new TypeOptions { Delay = 50 });
            await page.TypeAsync("input[name='pass']", acc.Password, new TypeOptions { Delay = 50 });
            await Task.Delay(500);
            await page.Keyboard.PressAsync("Enter");
            Log?.Invoke($"[{acc.Uid}] submit login...");

            // tunggu hasil: poll url/status sampai 30 dtk (v20 tak punya WaitForNavigation)
            string url = "", text = "";
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                url = page.Url ?? "";
                if (url.Contains("checkpoint") || url.Contains("two_step")) break;
                if (await IsLoggedInAsync(page, acc.Uid)) break;
                text = await page.EvaluateExpressionAsync<string>(
                    "document.body ? document.body.innerText.slice(0, 3000) : ''");
                if (ContainsAny(text, "incorrect", "Incorrect", "salah", "Salah", "sandi")) break;
            }

            url = page.Url ?? "";
            Log?.Invoke($"[{acc.Uid}] url: {Truncate(url, 90)}");

            if (url.Contains("checkpoint") || url.Contains("two_step") || HasSelector(page, "input[name='approvals_code']").GetAwaiter().GetResult())
            {
                SetStatus(acc, "Checkpoint");
                Log?.Invoke($"[{acc.Uid}] CHECKPOINT/2FA — selesaikan manual");
                return;
            }

            if (await IsLoggedInAsync(page, acc.Uid))
            {
                acc.Cookies = await DumpCookiesAsync(page);
                SetStatus(acc, "PasswordOk");
                Log?.Invoke($"[{acc.Uid}] login via password OK, cookies disimpan");
                await onSave(acc);
                return;
            }

            if (ContainsAny(text, "incorrect", "Incorrect", "salah", "Salah", "wrong password", "sandi"))
            {
                SetStatus(acc, "WrongPassword");
                Log?.Invoke($"[{acc.Uid}] password salah");
            }
            else
            {
                SetStatus(acc, "Failed", "hasil tak dikenal");
                Log?.Invoke($"[{acc.Uid}] hasil tak dikenal: {Truncate(text.Replace('\n', ' '), 120)}");
            }
        }
        finally
        {
            try { await page.CloseAsync(); } catch { }
        }
    }

    // ---------- helpers ----------

    void SetStatus(Account acc, string status, string? info = null)
    {
        // set langsung; INPC di Account yang urus marshal ke UI thread (aman juga saat tanpa UI)
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        acc.Status = status;
        acc.LastLogin = info is null ? stamp : $"{stamp} ({info})";
    }

    static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    static bool ContainsAny(string text, params string[] keys) =>
        keys.Any(text.Contains);

    static System.Collections.Generic.List<(string Name, string Value)> ParseCookies(string raw)
    {
        var list = new System.Collections.Generic.List<(string, string)>();
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0) continue;
            list.Add((part[..i].Trim(), part[(i + 1)..].Trim()));
        }
        return list;
    }

    static async Task<bool> IsLoggedInAsync(IPage page, string uid)
    {
        try
        {
            var cookies = await page.GetCookiesAsync();
            var cUser = cookies.FirstOrDefault(c => c.Name == "c_user");
            var xs = cookies.FirstOrDefault(c => c.Name == "xs");
            return cUser != null && !string.IsNullOrEmpty(cUser.Value)
                && xs != null && !string.IsNullOrEmpty(xs.Value);
        }
        catch { return false; }
    }

    static async Task<bool> HasSelector(IPage page, string selector)
    {
        try { return await page.QuerySelectorAsync(selector) != null; }
        catch { return false; }
    }

    static async Task<string> DumpCookiesAsync(IPage page)
    {
        var cookies = await page.GetCookiesAsync();
        return string.Join("; ", cookies
            .Where(c => c.Domain != null && c.Domain.Contains("facebook"))
            .Select(c => $"{c.Name}={c.Value}"));
    }

    public void Shutdown()
    {
        try { _browser?.CloseAsync().GetAwaiter().GetResult(); } catch { }
    }
}
