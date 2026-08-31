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

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
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
}
