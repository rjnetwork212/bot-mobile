using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Eksperimen kontrol (tanpa asumsi): cookie fresh + UA desktop vs UA mobile.
/// Menjawab: apakah FB menolak session karena device class mismatch?
/// </summary>
public static class CookieProbe
{
    record Result(string Scenario, string Url, bool LoggedIn, bool CUserAlive, int CookieCount);

    public static async Task Run(string uid)
    {
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");
        Account acc;
        using (var db = new AccountDb(dbPath))
            acc = db.GetAll().FirstOrDefault(a => a.Uid == uid)
                ?? throw new InvalidOperationException("uid tidak ada");

        var chrome = BotService.FindChrome(null);

        foreach (var scenario in new[] { "desktop_ua", "mobile_ua" })
        {
            Console.WriteLine($"========== SCENARIO: {scenario} ==========");
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = chrome, Headless = true, DefaultViewport = null,
                Args = new[] { "--no-first-run" },
            });
            try
            {
                var page = await browser.NewPageAsync();
                if (scenario == "desktop_ua")
                {
                    // UA desktop polos (tanpa IAB), viewport desktop
                    await page.SetUserAgentAsync(
                        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                }
                else
                {
                    await FacebookLogin.SetupMobileAsync(page, acc.Uid);
                }

                // goto dulu origin FB supaya setCookie pada konteks domain valid
                try
                {
                    await page.GoToAsync("https://www.facebook.com/", new NavigationOptions
                    {
                        Timeout = 30000,
                        WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    });
                }
                catch (NavigationException) { }

                foreach (var (name, value) in FacebookLogin.ParseCookies(acc.Cookies))
                {
                    try { await page.SetCookieAsync(new CookieParam { Name = name, Value = value, Domain = ".facebook.com", Path = "/", Secure = true }); }
                    catch (Exception ex) { Console.WriteLine($"  set {name} FAIL: {ex.Message.Split('\n')[0]}"); }
                }

                // buka ulang homepage
                try
                {
                    await page.GoToAsync("https://www.facebook.com/", new NavigationOptions
                    {
                        Timeout = 30000,
                        WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    });
                }
                catch (NavigationException) { }
                await Task.Delay(5000);

                var cookies = await page.GetCookiesAsync();
                var cUser = cookies.FirstOrDefault(c => c.Name == "c_user")?.Value;
                var loggedIn = cookies.Any(c => c.Name == "c_user" && !string.IsNullOrEmpty(c.Value))
                    && cookies.Any(c => c.Name == "xs" && !string.IsNullOrEmpty(c.Value))
                    && !page.Url.Contains("/login");
                Console.WriteLine($"url: {page.Url}");
                Console.WriteLine($"c_user alive: {(cUser != null ? cUser : "TIDAK ADA")} | xs: {cookies.Any(c => c.Name == "xs")} | cookies: {cookies.Length}");
                Console.WriteLine($"LOGIN OK: {loggedIn}");
            }
            finally
            {
                await browser.CloseAsync();
            }
        }
    }
}
