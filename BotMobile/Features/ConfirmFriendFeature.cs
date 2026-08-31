using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Confirm permintaan teman masuk (GraphQL, port confirm_friend_request).</summary>
public class ConfirmFriendFeature : IBotFeature
{
    public string Id => "confirm_friend";
    public string Name => "Confirm Friend";
    public string Description => "Terima semua permintaan pertemanan masuk (GraphQL).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("MaxConfirm", "Maks konfirmasi", "50"),
        ("DelayMinMs", "Jeda min (ms)", "1500"),
        ("DelayMaxMs", "Jeda max (ms)", "4000"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var max = Math.Clamp(cfg.GetInt("MaxConfirm", 50), 1, 500);
        var rnd = new Random();
        var uids = await FbHelper.FetchPendingRequestsAsync(page);
        if (uids.Count > 0 && uids[0].StartsWith("ERR:"))
        {
            log($"fetch gagal: {uids[0]}");
            return;
        }
        if (uids.Count == 0) { log("tidak ada permintaan masuk"); return; }
        log($"{uids.Count} permintaan masuk, proses max {max}");

        var ok = 0; var fail = 0;
        foreach (var uid in uids.GetRange(0, Math.Min(max, uids.Count)))
        {
            if (flags.SessionExpired) break;
            var (_, outcome) = await FbHelper.ConfirmFriendAsync(page, uid);
            if (outcome == "confirmed") { ok++; log($"{uid}: confirmed"); }
            else if (outcome == "session_expired") { flags.SessionExpired = true; log($"{uid}: session mati"); break; }
            else { fail++; log($"{uid}: {outcome}"); }
            await Task.Delay(rnd.Next(cfg.GetInt("DelayMinMs", 1500), cfg.GetInt("DelayMaxMs", 4000)));
        }
        log($"selesai: {ok} confirmed, {fail} gagal");
    }
}
