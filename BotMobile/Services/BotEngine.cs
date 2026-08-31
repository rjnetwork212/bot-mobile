using BotMobile.Features;
using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Engine run: login akun → eksekusi fitur bot sesuai urutan (enabled saja).
/// Semua path/elemen selector di file fitur terpisah.
/// </summary>
public class BotEngine
{
    public event Action<string>? Log;

    readonly SemaphoreSlim _slots = new(3);
    IBrowser? _browser;
    string _chromePath = "";

    public async Task RunAsync(IEnumerable<Account> accounts, List<FeatureConfig> featureOrder, Func<Account, Task> onSave)
    {
        var tasks = accounts.Select(acc => Task.Run(() => RunOneAsync(acc, featureOrder, onSave)));
        await Task.WhenAll(tasks);
    }

    async Task RunOneAsync(Account acc, List<FeatureConfig> featureOrder, Func<Account, Task> onSave)
    {
        await _slots.WaitAsync();
        try
        {
            var browser = await GetBrowserAsync();
            var page = await browser.NewPageAsync();
            try
            {
                await FacebookLogin.SetupMobileAsync(page, acc.Uid);
                Log?.Invoke($"[{acc.Uid}] device={Fingerprint.DeviceFor(acc.Uid).Model} (FB_IAB)");

                // 1) login: cookies → password
                bool loggedIn = await FacebookLogin.TryCookieLoginAsync(page, acc, m => Log?.Invoke($"[{acc.Uid}] {m}"));
                if (loggedIn)
                {
                    acc.Status = "CookieOk";
                }
                else
                {
                    Log?.Invoke($"[{acc.Uid}] cookies gagal/expired → password");
                    if (string.IsNullOrWhiteSpace(acc.Password))
                    {
                        acc.Status = "NoCookies";
                    }
                    else
                    {
                        var (ok, outcome) = await FacebookLogin.TryPasswordLoginAsync(page, acc, m => Log?.Invoke($"[{acc.Uid}] {m}"));
                        acc.Status = outcome switch
                        {
                            "ok" => "PasswordOk",
                            "checkpoint" => "Checkpoint",
                            "wrongpass" => "WrongPassword",
                            "blocked" => "Blocked",
                            _ => "Failed",
                        };
                        loggedIn = ok;
                    }
                }

                acc.LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                if (loggedIn)
                {
                    acc.Cookies = await FacebookLogin.DumpCookiesAsync(page);
                    Log?.Invoke($"[{acc.Uid}] login OK ({acc.Status}) → run fitur");
                    // 2) fitur berurutan
                    foreach (var cfg in featureOrder.Where(f => f.Enabled))
                    {
                        var feat = FeatureRegistry.Find(cfg.FeatureId);
                        if (feat == null) continue;
                        try
                        {
                            Log?.Invoke($"[{acc.Uid}] ▶ {feat.Name}");
                            await feat.RunAsync(page, acc, cfg, m => Log?.Invoke($"[{acc.Uid}]   {m}"));
                        }
                        catch (Exception ex)
                        {
                            Log?.Invoke($"[{acc.Uid}] ✗ {feat.Name}: {ex.Message.Split('\n')[0]}");
                        }
                    }
                }
                await onSave(acc);
            }
            finally
            {
                try { await page.CloseAsync(); } catch { }
            }
        }
        catch (Exception ex)
        {
            acc.Status = "Failed";
            acc.LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            Log?.Invoke($"[{acc.Uid}] ERROR: {ex.Message.Split('\n')[0]}");
        }
        finally
        {
            _slots.Release();
        }
    }

    async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsClosed: false }) return _browser;
        if (string.IsNullOrWhiteSpace(_chromePath) || !File.Exists(_chromePath))
            _chromePath = BotService.FindChrome(_chromePath);
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

    public void Shutdown()
    {
        try { _browser?.CloseAsync().GetAwaiter().GetResult(); } catch { }
    }
}
