using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Probe GraphQL helper (window.__mfb): login via cookies → buka www.facebook.com →
/// getTokens → fetchPendingRequests/suggestions. Validasi port Bot_Ngekeng.
/// Jalankan: dotnet run -- --probe-graphql uid
/// </summary>
public static class GraphQLProbe
{
    public static async Task Run(string uid)
    {
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");
        List<Account> candidates;
        using (var db = new AccountDb(dbPath))
        {
            var all = db.GetAll();
            candidates = string.IsNullOrEmpty(uid)
                ? all.Where(a => !string.IsNullOrWhiteSpace(a.Cookies)).ToList()
                : new List<Account> { all.FirstOrDefault(a => a.Uid == uid) ?? throw new InvalidOperationException($"uid {uid} tidak ada di DB") };
        }
        foreach (var acc in candidates)
        {
            Console.WriteLine($"=== coba {acc.Uid} ===");
            try
            {
                if (await ProbeOne(acc)) { Console.WriteLine("=== GraphQL OK ==="); return; }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"probe gagal: {ex.Message.Split('\n')[0]}");
                try
                {
                    Console.WriteLine($"  url saat gagal: {acc.Uid} → (di halaman berikut)");
                    // state halaman utk diagnosis
                }
                catch { }
            }
        }
        Console.WriteLine("=== TIDAK ADA AKUN COOKIES VALID ===");
    }

    static async Task<bool> ProbeOne(Account acc)
    {
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

            // login via cookies (mobile) → fallback password
            var ok = await FacebookLogin.TryCookieLoginAsync(page, acc, m => Console.WriteLine($"[login] {m}"));
            string outcome = ok ? "ok" : "cookie_gagal";
            if (!ok)
            {
                Console.WriteLine("[login] cookies gagal → password");
                var (pok, poutcome) = await FacebookLogin.TryPasswordLoginAsync(page, acc, m => Console.WriteLine($"[login] {m}"));
                ok = pok;
                outcome = poutcome;
            }
            Console.WriteLine($"login: {ok} ({outcome})");
            if (!ok)
            {
                Console.WriteLine($"login gagal ({outcome}) — lanjut akun berikutnya");
                return false;
            }

            // FASE 2: konteks desktop www (GraphQL Comet ditolak dari m.facebook — 1357004)
            Console.WriteLine("[fase2] ganti UA desktop + buka www.facebook.com");
            try
            {
                await page.SetUserAgentAsync(
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 850 });
                await page.GoToAsync("https://www.facebook.com/", new NavigationOptions
                {
                    Timeout = 30000,
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[nav] exception (lanjut): {ex.Message.Split('\n')[0]}");
            }
            await Task.Delay(5000);
            Console.WriteLine($"url: {page.Url}");

            // install helper + tunggu token
            var installed = await FbHelper.InstallAsync(page);
            Console.WriteLine($"__mfb installed: {installed}");
            if (!installed) return false;

            var tokens = await FbHelper.GetTokensAsync(page);
            string T(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) ? v : "";
            Console.WriteLine($"tokens: userId={T(tokens, "userId")} fb_dtsg={(T(tokens, "fb_dtsg").Length > 0 ? $"ADA({T(tokens, "fb_dtsg").Length})" : "KOSONG")} lsd={(T(tokens, "lsd").Length > 0 ? "ada" : "kosong")}");
            Console.WriteLine($"userId match uid: {T(tokens, "userId") == acc.Uid}");

            // verifikasi fingerprint mobile nyata: UA string + UA-CH + viewport
            var fp = await page.EvaluateExpressionAsync<string>(
                "JSON.stringify({ua: navigator.userAgent, mobile: navigator.userAgentData ? navigator.userAgentData.mobile : null, platform: navigator.userAgentData ? navigator.userAgentData.platform : null, w: innerWidth, touch: 'ontouchstart' in window})");
            Console.WriteLine($"fingerprint: {fp}");

            // fetch pending friend requests (readonly query — aman)
            Console.WriteLine("--- fetchPendingRequests ---");
            var raw = await page.EvaluateExpressionAsync<string>(
                "window.__mfb.graphql('4499082396829105','FriendingCometRootQuery',{requests_initial:1000,scale:1}).then(r => r)");
            Console.WriteLine($"raw graphql: {(raw.Length > 500 ? raw.Substring(0, 500) : raw)}");
            var reqs = await FbHelper.FetchPendingRequestsAsync(page);
            Console.WriteLine($"pending: {reqs.Count} {(reqs.Count > 0 && reqs[0].StartsWith("ERR:") ? reqs[0] : "")}");
            if (reqs.Count > 0 && !reqs[0].StartsWith("ERR:"))
                Console.WriteLine($"  contoh: {string.Join(",", reqs.Take(3))}");

            Console.WriteLine("--- fetchSuggestions(10) ---");
            var sug = await FbHelper.FetchSuggestionsAsync(page, 10);
            Console.WriteLine($"suggestions: {sug.Count} {(sug.Count > 0 && sug[0].StartsWith("ERR:") ? sug[0] : "")}");
            if (sug.Count > 0 && !sug[0].StartsWith("ERR:"))
                Console.WriteLine($"  contoh: {string.Join(",", sug.Take(3))}");

            Console.WriteLine("--- setBioText raw (diagnosa) ---");
            var rawBio = await page.EvaluateExpressionAsync<string>(
                "window.__mfb.setBioText('Test bio probe').then(r => r)");
            Console.WriteLine($"raw bio: [{(rawBio == null ? "NULL" : rawBio.Length.ToString())}] {(string.IsNullOrEmpty(rawBio) ? "" : rawBio.Substring(0, Math.Min(200, rawBio.Length)))}");
            return T(tokens, "fb_dtsg").Length > 0;
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
