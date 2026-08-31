using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Post status teks ke timeline (GraphQL ComposerStoryCreateMutation).</summary>
public class PostTimelineFeature : IBotFeature
{
    public string Id => "post_timeline";
    public string Name => "Post Status";
    public string Description => "Post status teks ke timeline (GraphQL).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("Message", "Isi status", "Halo dunia"),
        ("Privacy", "Privasi (EVERYONE/FRIENDS/ONLY_ME)", "EVERYONE"),
        ("Count", "Jumlah post", "1"),
        ("LinkPerPost", "Ambil link dari pool (true/false)", "false"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var count = Math.Clamp(cfg.GetInt("Count", 1), 1, 20);
        var message = cfg.Get("Message", "Halo dunia");
        var privacy = cfg.Get("Privacy", "EVERYONE");
        var useLink = cfg.Get("LinkPerPost", "false") == "true";
        using var pool = useLink ? new PoolDb() : null;

        var ok = 0;
        for (int i = 0; i < count; i++)
        {
            if (flags.SessionExpired) break;
            var text = message;
            if (useLink && pool != null)
            {
                var link = pool.TakeOneLink($"{acc.Uid}#post_timeline");
                if (link == null) { log("pool link habis"); break; }
                text = $"{message}\n{link}";
            }
            var (_, outcome, postId) = await FbHelper.PostStatusAsync(page, text, privacy);
            if (outcome == "posted") { ok++; log($"post OK (id {postId})"); }
            else if (outcome == "restricted") { log("akun restricted (1357031) — stop"); break; }
            else if (outcome == "session_expired" || outcome == "no_tokens") { flags.SessionExpired = true; log("session mati"); break; }
            else log($"post gagal: {outcome}");
            await Task.Delay(4000);
        }
        log($"selesai: {ok}/{count} post");
    }
}
