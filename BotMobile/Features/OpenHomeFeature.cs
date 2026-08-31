using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Buka beranda/feed dulu biar session "hangat" sebelum aksi lain.</summary>
public class OpenHomeFeature : IBotFeature
{
    public string Id => "open_home";
    public string Name => "Buka Beranda";
    public string Description => "Buka m.facebook.com (home feed).";
    public (string, string, string)[] ParamDefs => new[] { ("WaitSec", "Tunggu setelah load (detik)", "3") };

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log)
    {
        foreach (var url in new[] { "https://m.facebook.com/home.php", "https://www.facebook.com/" })
        {
            try
            {
                await page.GoToAsync(url, new NavigationOptions
                {
                    Timeout = 30000,
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                });
                break;
            }
            catch (Exception)
            {
                // timeout/abort: lanjut fallback atau pakai state halaman sekarang
            }
        }
        var wait = cfg.GetInt("WaitSec", 3);
        await Task.Delay(wait * 1000);
        log($"home buka, url={page.Url}");
    }
}
