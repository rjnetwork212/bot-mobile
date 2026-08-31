using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Add friend dari PYMK suggestions (GraphQL, port add_suggestions mode graphql).</summary>
public class AddSuggestionsFeature : IBotFeature
{
    public string Id => "add_suggestions";
    public string Name => "Add Suggestions";
    public string Description => "Add friend dari daftar saran (PYMK) via GraphQL.";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("MaxPerRun", "Maks add per akun", "15"),
        ("FetchCount", "Jumlah suggestion di-fetch", "50"),
        ("DelayMinMs", "Jeda min (ms)", "2000"),
        ("DelayMaxMs", "Jeda max (ms)", "6000"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var max = Math.Clamp(cfg.GetInt("MaxPerRun", 15), 1, 100);
        var rnd = new Random();
        var uids = await FbHelper.FetchSuggestionsAsync(page, cfg.GetInt("FetchCount", 50));
        if (uids.Count > 0 && uids[0].StartsWith("ERR:")) { log($"fetch gagal: {uids[0]}"); return; }
        if (uids.Count == 0) { log("tidak ada suggestions"); return; }
        log($"{uids.Count} suggestions, proses max {max}");

        var ok = 0; var fail = 0;
        foreach (var uid in uids.GetRange(0, Math.Min(max, uids.Count)))
        {
            if (flags.SessionExpired) break;
            var (addOk, outcome, _) = await FbHelper.AddFriendAsync(page, uid);
            if (outcome == "request_sent") ok++;
            else if (outcome == "already_friend") { /* skip, dihitung netral */ }
            else if (outcome == "rate_limit")
            {
                log("rate limit — stop");
                break;
            }
            else if (outcome == "session_expired") { flags.SessionExpired = true; break; }
            else fail++;
            log($"progress: {ok} sukses, {fail} gagal");
            await Task.Delay(rnd.Next(cfg.GetInt("DelayMinMs", 2000), cfg.GetInt("DelayMaxMs", 6000)));
        }
        log($"selesai: {ok} sukses, {fail} gagal");
    }
}
