using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Set bio text profil (GraphQL ProfileCometSetBioMutation).</summary>
public class SetBioFeature : IBotFeature
{
    public string Id => "set_bio";
    public string Name => "Set Bio";
    public string Description => "Ganti bio text profil (GraphQL).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("BioText", "Isi bio", "Halo, ini bio saya"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var bio = cfg.Get("BioText", "Halo, ini bio saya");
        var (_, outcome) = await FbHelper.SetBioTextAsync(page, bio);
        if (outcome == "bio_set") log("bio OK");
        else if (outcome == "session_expired" || outcome == "no_tokens") { flags.SessionExpired = true; log("session mati"); }
        else log($"bio gagal: {outcome}");
    }
}
