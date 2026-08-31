using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Post status teks (draft dari config). Selector belum diprobe — default disabled.</summary>
public class PostStatusFeature : IBotFeature
{
    public string Id => "post_status";
    public string Name => "Post Status";
    public string Description => "Buat post status teks (perlu probe ulang selector composer).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("Text", "Isi status", "Halo dunia"),
    };

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log)
    {
        log("post_status: selector composer belum tervalidasi probe — skip. (aktifkan setelah probe composer)");
        await Task.CompletedTask;
    }
}
