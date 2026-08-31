using BotMobile.Models;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Probe elemen FB nyata: buka Chrome headless, dump selector login page & feed.
/// Dipakai untuk memvalidasi selector sebelum dipakai FacebookLogin (jangan asumsi).
/// Jalankan: dotnet run -- --probe [uid]
/// </summary>
public static class ProbeRunner
{
    record InputInfo(string Name, string Id, string Type, string Placeholder, string Aria);
    record ButtonInfo(string Text, string Type, string Name);

    public static async Task Run(string? uid) => await RunProbe(uid, loginPassword: false);

    /// <summary>Probe alur password nyata: isi form login, submit, dump hasil.</summary>
    public static async Task RunPasswordProbe(string uid)
    {
        var acc = LoadAccount(uid) ?? throw new InvalidOperationException($"uid {uid} tidak ada di DB");
        var chrome = BotService.FindChrome(null);
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = chrome, Headless = true, DefaultViewport = null,
            Args = new[] { "--disable-blink-features=AutomationControlled", "--no-first-run", "--lang=id-ID" },
        });
        try
        {
            var page = await browser.NewPageAsync();
            await FacebookLogin.SetupMobileAsync(page, acc.Uid);
            Console.WriteLine("========== PASSWORD LOGIN PROBE ==========");
            await page.GoToAsync("https://m.facebook.com/login/", WaitUntilNavigation.DOMContentLoaded);
            await page.WaitForSelectorAsync(LoginSelectors.Email, new WaitForSelectorOptions { Timeout = 20000 });
            await page.TypeAsync(LoginSelectors.Email, acc.Uid, new TypeOptions { Delay = 40 });
            await page.TypeAsync(LoginSelectors.Pass, acc.Password, new TypeOptions { Delay = 40 });
            Console.WriteLine("typed ok → click button[name='login']");
            await page.ClickAsync(LoginSelectors.Submit);
            var (ok, outcome) = await FacebookLogin.WaitLoginResultAsync(page);
            Console.WriteLine($"result: ok={ok} outcome={outcome}");
            Console.WriteLine($"final url: {page.Url}");
            var text = await page.EvaluateExpressionAsync<string>(
                "document.body ? document.body.innerText.slice(0, 700).replace(/\\n+/g, ' | ') : ''");
            Console.WriteLine($"text: {text}");
            if (ok)
            {
                var ck = await FacebookLogin.DumpCookiesAsync(page);
                Console.WriteLine($"cookies_len: {ck.Length}");
            }
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    static async Task RunProbe(string? uid, bool loginPassword)
    {
        var chrome = BotService.FindChrome(null);
        var acc = uid == null ? null : LoadAccount(uid);

        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = chrome,
            Headless = true, // probe tidak perlu window
            DefaultViewport = null,
            Args = new[] { "--disable-blink-features=AutomationControlled", "--no-first-run", "--lang=id-ID" },
        });
        try
        {
            var page = await browser.NewPageAsync();
            page.DefaultNavigationTimeout = 60000;
            var dev = Fingerprint.DeviceFor("probe");
            await page.SetUserAgentAsync(Fingerprint.BuildUa(dev));
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = dev.W, Height = dev.H, DeviceScaleFactor = dev.Dpr,
                IsMobile = true, HasTouch = true,
            });
            await page.EvaluateExpressionOnNewDocumentAsync(StealthLoader.Load());

            // === 1) LOGIN PAGE (tanpa cookie) ===
            Console.WriteLine("========== LOGIN PAGE ==========");
            await page.GoToAsync("https://m.facebook.com/login/", WaitUntilNavigation.DOMContentLoaded);
            await Task.Delay(4000);
            Console.WriteLine($"url: {page.Url}");
            Console.WriteLine($"title: {await page.GetTitleAsync()}");
            await DumpForms(page);
            await DumpButtons(page);
            Console.WriteLine($"has_link_masuk: {await PageHas(page, "a[href*='recover'], a[href*='reset']")}");
            Console.WriteLine($"body_text_head: {Trunc(await BodyText(page), 400)}");

            // === 2) LOGIN VIA COOKIES → FEED ===
            if (acc != null && !string.IsNullOrWhiteSpace(acc.Cookies))
            {
                Console.WriteLine("========== FEED (cookies) ==========");
                foreach (var (name, value) in FacebookLogin.ParseCookies(acc.Cookies))
                    await page.SetCookieAsync(new CookieParam { Name = name, Value = value, Domain = ".facebook.com", Path = "/" });

                foreach (var target in new[] { "https://m.facebook.com/", "https://www.facebook.com/" })
                {
                    try
                    {
                        await page.GoToAsync(target, WaitUntilNavigation.Networkidle2);
                    }
                    catch (NavigationException ex)
                    {
                        Console.WriteLine($"--- goto {target} (nav exception: {ex.Message.Split('\n')[0]})");
                    }
                    await Task.Delay(3000);
                    var cookies = await page.GetCookiesAsync();
                    var html = await page.EvaluateExpressionAsync<string>(
                        "document.documentElement ? document.documentElement.outerHTML.length.toString() : '0'");
                    var text = await page.EvaluateExpressionAsync<string>(
                        "document.body ? document.body.innerText.slice(0, 300).replace(/\\n+/g, ' | ') : ''");
                    Console.WriteLine($"--- goto {target}");
                    Console.WriteLine($"final url: {page.Url}");
                    Console.WriteLine($"cookie count: {cookies.Length} | c_user: {cookies.FirstOrDefault(c => c.Name == "c_user")?.Value ?? "TIDAK ADA"}");
                    Console.WriteLine($"html_len: {html}");
                    Console.WriteLine($"text: {text}");
                    if (cookies.Any(c => c.Name == "c_user" && !string.IsNullOrEmpty(c.Value))) break;
                }
                await DumpNav(page);
            }
            else
            {
                Console.WriteLine("(skip feed probe: uid tidak ada/tanpa cookies)");
            }
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    static Account? LoadAccount(string uid)
    {
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");
        using var db = new AccountDb(dbPath);
        return db.GetAll().FirstOrDefault(a => a.Uid == uid);
    }

    static async Task DumpForms(IPage page)
    {
        var json = await page.EvaluateFunctionAsync<string[]>(@"() =>
            [...document.querySelectorAll('input')].map(el =>
                JSON.stringify({
                    name: el.name, id: el.id, type: el.type,
                    ph: (el.placeholder||'').slice(0,30),
                    aria: (el.getAttribute('aria-label')||'').slice(0,30)
                }))");
        foreach (var s in json ?? Array.Empty<string>()) Console.WriteLine($"input: {s}");
        var forms = await page.EvaluateFunctionAsync<string[]>(@"() =>
            [...document.querySelectorAll('form')].map(f =>
                JSON.stringify({ action: f.action, method: f.method, id: f.id }))");
        foreach (var s in forms ?? Array.Empty<string>()) Console.WriteLine($"form: {s}");
    }

    static async Task DumpButtons(IPage page)
    {
        var btns = await page.EvaluateFunctionAsync<string[]>(@"() =>
            [...document.querySelectorAll('button, input[type=submit], [role=button]')]
            .filter(el => el.offsetParent !== null)
            .slice(0, 15)
            .map(el => JSON.stringify({
                text: (el.innerText || el.value || '').trim().slice(0,30),
                type: el.type || '', name: el.name || ''
            }))");
        foreach (var s in btns ?? Array.Empty<string>()) Console.WriteLine($"button: {s}");
    }

    static async Task DumpNav(IPage page)
    {
        var links = await page.EvaluateFunctionAsync<string[]>(@"() =>
            [...document.querySelectorAll('a[href]')]
            .map(a => ({ t: (a.innerText||'').trim().slice(0,25), h: a.getAttribute('href')||'' }))
            .filter(x => x.t.length > 0 && x.h.length > 0 && !x.h.startsWith('javascript'))
            .slice(0, 40)
            .map(x => JSON.stringify(x))");
        foreach (var s in links ?? Array.Empty<string>()) Console.WriteLine($"link: {s}");
    }

    static async Task<bool> PageHas(IPage page, string selector)
    {
        try { return await page.QuerySelectorAsync(selector) != null; }
        catch { return false; }
    }

    static async Task<string> BodyText(IPage page) =>
        await page.EvaluateExpressionAsync<string>(
            "document.body ? document.body.innerText.slice(0, 600).replace(/\\n+/g, ' | ') : ''");

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
