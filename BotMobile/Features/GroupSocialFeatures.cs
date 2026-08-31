using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Tambah member dari pool UID ke group Messenger (port add_group_member).</summary>
public class AddGroupMemberFeature : IBotFeature
{
    public string Id => "add_group_member";
    public string Name => "Add Member Group";
    public string Description => "Tambah UID (pool) ke group Messenger (fetch group otomatis).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("MaxPerRun", "Maks add per akun", "10"),
        ("DelayMinMs", "Jeda min (ms)", "2000"),
        ("DelayMaxMs", "Jeda max (ms)", "5000"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var max = Math.Clamp(cfg.GetInt("MaxPerRun", 10), 1, 100);
        using var pool = new PoolDb();
        var uids = pool.TakeTargets(max, $"{acc.Uid}#add_group_member");
        if (uids.Count == 0) { log("pool target kosong"); return; }

        var groups = BotData.Context.GroupThreads.ToList();
        var dbg = "runtime-context";
        if (groups.Count == 0)
            (groups, dbg) = await FbHelperExt.FetchGroupsAsync(page);
        if (groups.Count == 0) { log($"tidak ada group ({dbg})"); pool.RollbackTargets(uids); return; }
        log($"{groups.Count} group ditemukan ({dbg}), tambah {uids.Count} UID");

        var rnd = new Random();
        var ok = 0; var fail = 0;
        for (int i = 0; i < uids.Count; i++)
        {
            if (flags.SessionExpired) { pool.RollbackTargets(uids.Skip(i)); break; }
            var threadId = groups[i % groups.Count];
            var (addOk, outcome, method) = await FbHelperExt.AddGroupMemberAsync(page, threadId, uids[i]);
            if (addOk) { pool.MarkTargetResult(uids[i], true); ok++; log($"{uids[i]} → group {threadId} ({method})"); }
            else if (outcome.Contains("session") || outcome == "err:no_tokens")
            { pool.RollbackTargets(uids.Skip(i)); flags.SessionExpired = true; break; }
            else { pool.MarkTargetResult(uids[i], false, outcome); fail++; log($"{uids[i]}: {outcome}"); }
            await Task.Delay(rnd.Next(cfg.GetInt("DelayMinMs", 2000), cfg.GetInt("DelayMaxMs", 5000)));
        }
        log($"selesai: {ok} ditambah, {fail} gagal");
    }
}

/// <summary>Kirim pesan/link ke group Messenger (port send_group_message).</summary>
public class SendGroupMessageFeature : IBotFeature
{
    public string Id => "send_group_message";
    public string Name => "Kirim Pesan Group";
    public string Description => "Kirim teks/link ke semua group (link dari pool Link).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("Message", "Isi pesan (kosong = pakai link pool)", ""),
        ("MaxGroups", "Maks group", "10"),
        ("DelayMinMs", "Jeda min (ms)", "3000"),
        ("DelayMaxMs", "Jeda max (ms)", "8000"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var maxGroups = Math.Clamp(cfg.GetInt("MaxGroups", 10), 1, 50);
        var message = cfg.Get("Message", "");
        using var pool = new PoolDb();
        var groups = BotData.Context.GroupThreads.ToList();
        var dbg = "runtime-context";
        if (groups.Count == 0)
            (groups, dbg) = await FbHelperExt.FetchGroupsAsync(page);
        if (groups.Count == 0) { log($"tidak ada group ({dbg}) — jalankan Buat Group dulu di atasnya"); return; }
        groups = groups.Take(maxGroups).ToList();
        log($"{groups.Count} group dikirim pesan ({dbg})");

        var rnd = new Random();
        var ok = 0; var fail = 0;
        foreach (var threadId in groups)
        {
            if (flags.SessionExpired) break;
            var text = message;
            if (text.Length == 0)
            {
                var link = pool.TakeOneLink($"{acc.Uid}#send_group_message");
                if (link == null) { log("pesan kosong & pool link habis — stop"); break; }
                text = link;
            }
            var (sendOk, outcome) = await FbHelperExt.SendGroupMessageAsync(page, threadId, text);
            if (!sendOk)
            {
                (sendOk, outcome) = await FbHelperExt.SendGroupMessageLightspeedAsync(page, threadId, text);
            }
            if (sendOk) { ok++; log($"group {threadId}: terkirim"); }
            else if (outcome.Contains("session") || outcome == "err:no_tokens") { flags.SessionExpired = true; break; }
            else { fail++; pool.RollbackLink(text); log($"group {threadId}: {outcome}"); }
            await Task.Delay(rnd.Next(cfg.GetInt("DelayMinMs", 3000), cfg.GetInt("DelayMaxMs", 8000)));
        }
        log($"selesai: {ok} terkirim, {fail} gagal");
    }
}

/// <summary>Ganti bahasa Facebook akun (port change_locale).</summary>
public class ChangeLocaleFeature : IBotFeature
{
    public string Id => "change_locale";
    public string Name => "Ganti Bahasa";
    public string Description => "Ganti bahasa UI Facebook (mis. id_ID → en_US).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("Locale", "Kode bahasa (en_US, id_ID, ja_JP...)", "en_US"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var locale = cfg.Get("Locale", "en_US");
        var (_, outcome) = await FbHelperExt.ChangeLocaleAsync(page, locale);
        if (outcome == "locale_changed") log($"bahasa → {locale} OK");
        else if (outcome.Contains("session") || outcome == "err:no_tokens") { flags.SessionExpired = true; log("session mati"); }
        else log($"ganti bahasa gagal: {outcome}");
    }
}

/// <summary>Post + link (pool khusus tag_friend) + tag teman (port tag_friend + createTagPost).</summary>
public class TagFriendFeature : IBotFeature
{
    public string Id => "tag_friend";
    public string Name => "Tag Teman";
    public string Description => "Post dengan link + tag teman (link pool khusus, caption otomatis 16 bahasa).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("TagsPerPost", "Tag per post", "10"),
        ("Count", "Jumlah post", "1"),
        ("CaptionLang", "Bahasa caption (random/en/id/...)", "random"),
        ("Hashtags", "Hashtag pool (spasi/koma)", ""),
        ("UsePreset", "Pakai preset caption (risiko moderasi)", "false"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var count = Math.Clamp(cfg.GetInt("Count", 1), 1, 20);
        var tagsPerPost = Math.Clamp(cfg.GetInt("TagsPerPost", 10), 1, 50);
        var lang = cfg.Get("CaptionLang", "random");
        var hashtags = cfg.Get("Hashtags", "");
        var usePreset = cfg.Get("UsePreset", "false") == "true";

        using var pool = new PoolDb("pool_tagfriend.db");
        var link = pool.TakeOneLink($"{acc.Uid}#tag_friend");
        if (link == null) { log("pool link tag_friend kosong (isi tab Link lalu Import ke Pool)"); return; }
        log($"link: {link[..Math.Min(50, link.Length)]}...");

        var friends = await FbHelper.FetchFriendsAsync(page);
        if (friends.Count > 0 && friends[0].StartsWith("ERR:")) { log($"fetch friends gagal: {friends[0]}"); pool.RollbackLink(link); return; }
        if (friends.Count == 0) { log("tidak ada teman"); pool.RollbackLink(link); return; }
        log($"{friends.Count} teman tersedia");

        var rnd = new Random();
        var ok = 0; var fail = 0;
        for (int i = 0; i < count; i++)
        {
            if (flags.SessionExpired) { pool.RollbackLink(link); break; }
            var tags = friends.OrderBy(_ => rnd.Next()).Take(tagsPerPost).ToList();
            var caption = CaptionBuilder.Build(link, lang, null, null, hashtags);
            if (usePreset && rnd.Next(100) < 35)
                caption = CaptionBuilder.Build(link, "en", null, null, hashtags); // preset single EN
            var (postOk, outcome, postId) = await FbHelperExt.CreateTagPostAsync(page, caption, "EVERYONE", tags, link);
            if (outcome == "posted")
            {
                ok++;
                log($"post {i + 1} OK ({tags.Count} tag, id {postId})");
            }
            else if (outcome == "restricted" || outcome == "rate_limited")
            {
                pool.RollbackLink(link);
                log($"{outcome} — link di-rollback, stop");
                break;
            }
            else if (outcome.Contains("session") || outcome == "err:no_tokens")
            {
                pool.RollbackLink(link);
                flags.SessionExpired = true;
                break;
            }
            else
            {
                // rollback link hanya stage aman — post sudah terkirim? tidak diketahui → rollback (aman utk duplikasi rendah)
                fail++;
                pool.RollbackLink(link);
                log($"post gagal: {outcome}");
            }
            await Task.Delay(8000);
        }
        log($"selesai: {ok} post OK, {fail} gagal");
    }
}

/// <summary>Setting privacy profil via bookmarklet (port setting_profile, JS verbatim Bot_Ngekeng).</summary>
public class SettingProfileFeature : IBotFeature
{
    public string Id => "setting_profile";
    public string Name => "Setting Profil";
    public string Description => "Set privacy settings via bookmarklet (port Bot_Ngekeng).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("WaitSeconds", "Tunggu setelah inject (detik)", "15"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var (_, outcome) = await FbHelperExt.SettingProfileAsync(page);
        if (outcome == "bookmarklet_injected")
        {
            var wait = Math.Clamp(cfg.GetInt("WaitSeconds", 15), 1, 120);
            log($"bookmarklet jalan, tunggu {wait}s (mutations fire-and-forget)");
            await Task.Delay(wait * 1000);
        }
        else log($"gagal: {outcome}");
    }
}
