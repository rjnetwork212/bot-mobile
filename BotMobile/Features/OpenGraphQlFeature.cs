using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>
/// Buka www.facebook.com (origin GraphQL Comet) + install helper __mfb.
/// Semua fitur GraphQL jalan lewat ini. Gagal session → set flags.SessionExpired.
/// </summary>
public class OpenGraphQlFeature : IBotFeature
{
    public string Id => "open_graphql";
    public string Name => "Sambung GraphQL";
    public string Description => "Buka www.facebook.com + pasang helper (wajib pertama, urutan bebas di atas).";
    public (string, string, string)[] ParamDefs => Array.Empty<(string, string, string)>();
    public bool DefaultEnabled => true;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        // www dengan UA desktop (GraphQL Comet butuh konteks desktop — hasil probe;
        // m.facebook menolak doc_id Comet dengan 1357004). Tab sama, UA diganti sementara.
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
        catch (Exception)
        {
            // abort/redirect — halaman tetap dicek
        }
        await Task.Delay(5000);

        if (page.Url.Contains("/login") || page.Url.Contains("checkpoint"))
        {
            flags.SessionExpired = true;
            log($"session mati ({(page.Url.Contains("checkpoint") ? "checkpoint" : "login page")})");
            return;
        }

        var installed = await FbHelper.InstallAsync(page);
        if (!installed)
        {
            flags.SessionExpired = true;
            log("helper __mfb gagal terpasang");
            return;
        }
        var loaded = await FbHelper.WaitUserLoadedAsync(page, acc.Uid, 15);
        var tokens = await FbHelper.GetTokensAsync(page);
        tokens.TryGetValue("userId", out var uid);
        tokens.TryGetValue("lsd", out var lsd);
        // mobile web tak punya fb_dtsg — lsd cukup (hasil probe token)
        if (uid != acc.Uid || string.IsNullOrEmpty(lsd))
        {
            flags.SessionExpired = true;
            log($"token tak valid (userId={uid}, lsd={(lsd.Length > 0 ? "ada" : "kosong")})");
            return;
        }
        log($"GraphQL siap (userId={uid}, lsd {lsd.Length} char)");
    }
}
