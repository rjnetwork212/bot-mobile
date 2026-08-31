using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Buka profil UID dari daftar UID (menu UID) — prefix m.facebook.com/&lt;uid&gt;.</summary>
public class OpenProfileFeature : IBotFeature
{
    public string Id => "open_profile";
    public string Name => "Buka Profil";
    public string Description => "Kunjungi profil UID (dari menu UID), berurutan.";
    public bool DefaultEnabled => false;
    public string[] Modes => new[] { FeatureModes.Selector };

    public (string, string, string)[] ParamDefs => new[]
    {
        ("MaxUids", "Maks profil per akun", "3"),
        ("WaitSec", "Tunggu per profil (detik)", "4"),
    };

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var ctx = BotData.Context;
        var max = Math.Clamp(cfg.GetInt("MaxUids", 3), 1, 50);
        var wait = Math.Max(1, cfg.GetInt("WaitSec", 4));
        foreach (var uid in ctx.Uids.GetRange(0, Math.Min(max, ctx.Uids.Count)))
        {
            try
            {
                await page.GoToAsync($"https://m.facebook.com/{uid}", new NavigationOptions
                {
                    Timeout = 30000,
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                });
                await Task.Delay(wait * 1000);
                log($"profil {uid} → {page.Url}");
            }
            catch (Exception ex)
            {
                log($"profil {uid} gagal: {ex.Message.Split('\n')[0]}");
            }
        }
    }
}
