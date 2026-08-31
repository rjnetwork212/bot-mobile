using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Scroll feed seperti manusia (jeda acak) — warm-up aktivitas.</summary>
public class ScrollFeedFeature : IBotFeature
{
    private static readonly Random Rnd = new();

    public string Id => "scroll_feed";
    public string Name => "Scroll Feed";
    public string Description => "Scroll feed beberapa kali dengan jeda acak.";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("Count", "Jumlah scroll", "5"),
        ("MinDelayMs", "Jeda min (ms)", "800"),
        ("MaxDelayMs", "Jeda max (ms)", "2500"),
    };

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log)
    {
        var count = Math.Clamp(cfg.GetInt("Count", 5), 1, 50);
        var min = Math.Max(200, cfg.GetInt("MinDelayMs", 800));
        var max = Math.Max(min, cfg.GetInt("MaxDelayMs", 2500));
        for (int i = 0; i < count; i++)
        {
            await page.Keyboard.PressAsync("PageDown");
            await Task.Delay(Rnd.Next(min, max));
        }
        log($"scroll {count}x selesai");
    }
}
