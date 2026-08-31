using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Buat group Messenger (GraphQL, port create_group).</summary>
public class CreateGroupFeature : IBotFeature
{
    public string Id => "create_group";
    public string Name => "Buat Group";
    public string Description => "Buat group Messenger (nama otomatis/nomor urut).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("Count", "Jumlah group", "3"),
        ("NamePrefix", "Prefix nama group", "Grup VIP"),
        ("DelayMinMs", "Jeda min (ms)", "3000"),
        ("DelayMaxMs", "Jeda max (ms)", "8000"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var count = Math.Clamp(cfg.GetInt("Count", 3), 1, 50);
        var prefix = cfg.Get("NamePrefix", "Grup VIP");
        var rnd = new Random();
        var ok = 0;
        for (int i = 1; i <= count; i++)
        {
            if (flags.SessionExpired) break;
            var name = $"{prefix} {DateTime.Now:ddMM} {i}{rnd.Next(10, 99)}";
            var (_, outcome, threadId) = await FbHelper.CreateGroupAsync(page, name);
            if (outcome == "created") { ok++; log($"'{name}' OK (thread {threadId})"); }
            else if (outcome == "session_expired" || outcome == "no_tokens") { flags.SessionExpired = true; log($"session mati saat '{name}'"); break; }
            else log($"'{name}': {outcome}");
            await Task.Delay(rnd.Next(cfg.GetInt("DelayMinMs", 3000), cfg.GetInt("DelayMaxMs", 8000)));
        }
        log($"selesai: {ok}/{count} group dibuat");
    }
}
