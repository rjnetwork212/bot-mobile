using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>Listen trafik asli saat kirim pesan group manual di www.facebook.com — dapatkan endpoint + payload beneran.</summary>
public static class MessagingProbe
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
            ExecutablePath = chrome, Headless = false, DefaultViewport = null,
            Args = new[] { "--no-first-run", "--lang=id-ID" },
        });
        try
        {
            var page = await browser.NewPageAsync();
            page.Request += (_, e) =>
            {
                var url = e.Request.Url ?? "";
                if (url.Contains("send") || url.Contains("message") || url.Contains("mercury") ||
                    url.Contains("LSPlatform") || url.Contains("graphql"))
                {
                    if (e.Request.Method.ToString() == "POST")
                    {
                        Console.WriteLine($"[req] {url.Split('?')[0]}");
                        var body = e.Request.PostData ?? "";
                        if (body.Length > 0)
                            Console.WriteLine(body.Length > 1500 ? body[..1500] : body);
                        Console.WriteLine("---");
                    }
                }
            };

            await FacebookLogin.SetupMobileAsync(page, acc.Uid);
            await FacebookLogin.TryCookieLoginAsync(page, acc, m => Console.WriteLine($"[login] {m}"));

            // fase desktop
            try
            {
                await page.SetUserAgentAsync(
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 850 });
                await page.GoToAsync("https://www.facebook.com/messages/t/", new NavigationOptions
                {
                    Timeout = 30000,
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                });
            }
            catch (NavigationException) { }
            await Task.Delay(8000);
            Console.WriteLine($"url: {page.Url}");
            Console.WriteLine(">>> BUKA group di Messenger lalu kirim pesan manual. Saya rekam request-nya (90 detik)...");
            await Task.Delay(90000);
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
