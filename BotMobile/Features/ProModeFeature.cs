using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Aktifkan Professional Mode (GraphQL CometProModeActivationDialogTransitionMutation).</summary>
public class ProModeFeature : IBotFeature
{
    public string Id => "pro_mode";
    public string Name => "Mode Profesional";
    public string Description => "Aktifkan professional mode (GraphQL).";
    public (string, string, string)[] ParamDefs => Array.Empty<(string, string, string)>();
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var (_, outcome) = await FbHelper.ActivateProModeAsync(page);
        if (outcome == "activated") log("professional mode OK");
        else if (outcome == "session_expired" || outcome == "no_tokens") { flags.SessionExpired = true; log("session mati"); }
        else log($"hasil: {outcome}");
    }
}
