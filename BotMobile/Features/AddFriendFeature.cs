using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Add friend per UID dari pool targets (GraphQL, port add_friend target_file).</summary>
public class AddFriendFeature : IBotFeature
{
    public string Id => "add_friend";
    public string Name => "Add Friend";
    public string Description => "Kirim permintaan teman ke UID dari pool target (GraphQL).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("MaxPerRun", "Maks UID per akun", "10"),
        ("DelayMinMs", "Jeda min (ms)", "2000"),
        ("DelayMaxMs", "Jeda max (ms)", "5000"),
    };
    public bool DefaultEnabled => false;
    public string[] Modes => new[] { FeatureModes.GraphQl, FeatureModes.Selector };

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var mode = cfg.Get("Metode", FeatureModes.GraphQl);
        if (mode == FeatureModes.Selector)
        {
            await RunSelectorAsync(page, acc, cfg, log, flags);
            return;
        }
        var max = Math.Clamp(cfg.GetInt("MaxPerRun", 10), 1, 200);
        var rnd = new Random();
        using var pool = new PoolDb();
        var uids = pool.TakeTargets(max, $"{acc.Uid}#add_friend");
        if (uids.Count == 0) { log("pool target kosong"); return; }
        log($"klaim {uids.Count} UID dari pool");

        var ok = 0; var fail = 0;
        var claimed = uids.ToList();
        foreach (var (uid, idx) in claimed.Select((u, i) => (u, i)))
        {
            if (flags.SessionExpired)
            {
                var sisa = claimed.Skip(idx + 1).ToList();
                if (sisa.Count > 0) pool.RollbackTargets(sisa);
                break;
            }
            var (_, outcome, _) = await FbHelper.AddFriendAsync(page, uid);
            switch (outcome)
            {
                case "request_sent":
                    pool.MarkTargetResult(uid, true);
                    ok++;
                    log($"{uid}: terkirim");
                    break;
                case "already_friend":
                    pool.MarkTargetResult(uid, true, "already_friend");
                    log($"{uid}: sudah teman");
                    break;
                case "blocked_or_cannot_request":
                    pool.MarkTargetResult(uid, false, outcome);
                    fail++;
                    log($"{uid}: tidak bisa request");
                    break;
                case "rate_limit":
                    pool.RollbackTargets(new[] { uid });
                    log($"{uid}: rate limit — rollback, stop");
                    flags.SessionExpired = true; // hentikan sisa
                    break;
                case "session_expired":
                    pool.RollbackTargets(new[] { uid });
                    flags.SessionExpired = true;
                    log($"{uid}: session mati — rollback");
                    break;
                default:
                    pool.MarkTargetResult(uid, false, outcome);
                    fail++;
                    log($"{uid}: {outcome}");
                    break;
            }
            var delay = rnd.Next(cfg.GetInt("DelayMinMs", 2000), cfg.GetInt("DelayMaxMs", 5000));
            await Task.Delay(delay);
        }
        log($"selesai: {ok} sukses, {fail} gagal");
    }

    // jalur SELECTOR: buka profil m.facebook per UID, klik tombol Add Friend (multi-bahasa)
    private async Task RunSelectorAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var max = Math.Clamp(cfg.GetInt("MaxPerRun", 10), 1, 200);
        using var pool = new PoolDb();
        var uids = pool.TakeTargets(max, $"{acc.Uid}#add_friend_sel");
        if (uids.Count == 0) { log("pool target kosong"); return; }
        log($"[selector] klaim {uids.Count} UID");

        var ok = 0; var skip = 0;
        foreach (var uid in uids)
        {
            if (flags.SessionExpired) break;
            await Selector.UiSelector.GoToAsync(page, $"https://m.facebook.com/{uid}");
            if (page.Url.Contains("/login") || page.Url.Contains("checkpoint"))
            {
                flags.SessionExpired = true;
                pool.RollbackTargets(uids.SkipWhile(u => u != uid));
                log("session mati — rollback sisa");
                break;
            }
            if (await Selector.UiSelector.HasLabelAsync(page, Selector.UiSelector.CancelRequestLabels))
            {
                pool.MarkTargetResult(uid, true, "already_requested");
                log($"{uid}: sudah pernah request");
                continue;
            }
            if (await Selector.UiSelector.ClickButtonByLabelsAsync(page, Selector.UiSelector.AddFriendLabels))
            {
                await Task.Delay(2500);
                var verified = await Selector.UiSelector.HasLabelAsync(page, Selector.UiSelector.CancelRequestLabels);
                pool.MarkTargetResult(uid, verified, verified ? null : "unverified");
                if (verified) { ok++; log($"{uid}: terkirim (verify OK)"); }
                else { ok++; log($"{uid}: klik OK (belum terverifikasi)"); }
            }
            else
            {
                pool.MarkTargetResult(uid, false, "button_not_found");
                skip++;
                log($"{uid}: tombol tidak ditemukan");
            }
            await Task.Delay(3000);
        }
        log($"[selector] selesai: {ok} add, {skip} gagal");
    }
}
