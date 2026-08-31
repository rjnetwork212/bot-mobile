using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Research: dengarkan trafik network asli m.facebook (mobile web) — endpoint apa
/// yang dipanggil app, body-nya seperti apa. Sumber kebenaran untuk port fitur.
/// </summary>
public static class TrafficProbe
{
    public static async Task Run(string uid)
    {
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");
        Account acc;
        using (var db = new AccountDb(dbPath))
            acc = db.GetAll().FirstOrDefault(a => a.Uid == uid)
                ?? throw new InvalidOperationException("uid tidak ada");

        var chrome = BotService.FindChrome(null);
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = chrome, Headless = true, DefaultViewport = null,
            Args = new[] { "--no-first-run" },
        });
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        try
        {
            var page = await browser.NewPageAsync();
            page.Response += (sender, e) =>
            {
                var url = e.Response.Url ?? "";
                if (url.Contains("graphql") || url.Contains("api") || url.Contains("ajax"))
                {
                    var key = url.Split('?')[0];
                    if (seen.TryAdd(key, 0) && seen.Count < 40)
                        Console.WriteLine($"[net] {e.Response.Status} {url.Split('?')[0]}");
                }
            };
            page.Request += (sender, e) =>
            {
                var url = e.Request.Url ?? "";
                if (url.Contains("graphql") && e.Request.Method.ToString() == "POST" && !url.Contains("?"))
                {
                    try
                    {
                        var body = e.Request.PostData ?? "";
                        if (body.Length > 0 && !seen.ContainsKey("BODY_" + url.Split('?')[0]))
                        {
                            seen.TryAdd("BODY_" + url.Split('?')[0], 0);
                            Console.WriteLine($"[req-body] {url.Split('?')[0]}");
                            Console.WriteLine(body.Length > 1200 ? body[..1200] : body);
                        }
                    }
                    catch { }
                }
            };

            await FacebookLogin.SetupMobileAsync(page, acc.Uid);
            await FacebookLogin.TryCookieLoginAsync(page, acc, m => Console.WriteLine($"[login] {m}"));
            try
            {
                await page.GoToAsync("https://m.facebook.com/home.php", new NavigationOptions
                {
                    Timeout = 30000,
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                });
            }
            catch (NavigationException) { }
            await Task.Delay(8000);

            // scroll untuk memicu load feed
            await page.Keyboard.PressAsync("PageDown");
            await Task.Delay(3000);
            await page.Keyboard.PressAsync("PageDown");
            await Task.Delay(5000);
            Console.WriteLine("=== selesai listening ===");
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
