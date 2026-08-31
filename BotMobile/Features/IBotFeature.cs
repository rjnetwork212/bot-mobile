using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>
/// Konfigurasi satu fitur bot. Parameter disimpan sebagai dict string
/// (key=value) biar popup config generik tanpa class per fitur.
/// </summary>
public class FeatureConfig
{
    public string FeatureId { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
    public Dictionary<string, string> Params { get; set; } = new();
}

/// <summary>
/// Kontrak fitur bot. Satu file per fitur (mis. OpenHomeFeature, ScrollFeedFeature).
/// ParamDef mendefinisikan field popup config (label, key, default).
/// </summary>
public interface IBotFeature
{
    string Id { get; }              // id stabil (simpan di DB)
    string Name { get; }            // nama tampilan
    string Description { get; }     // 1 kalimat
    (string Key, string Label, string Def)[] ParamDefs { get; }  // field popup config
    bool DefaultEnabled { get; }    // default aktif? fitur mutasi = false (aman)

    Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags);
}

/// <summary>State run antar fitur (dibuat engine per akun).</summary>
public class RunFlags
{
    public bool SessionExpired { get; set; }
}

/// <summary>Helper ambil param dengan fallback default.</summary>
public static class FeatureParams
{
    public static string Get(this FeatureConfig cfg, string key, string def) =>
        cfg.Params.TryGetValue(key, out var v) && v.Length > 0 ? v : def;

    public static int GetInt(this FeatureConfig cfg, string key, int def) =>
        int.TryParse(cfg.Get(key, def.ToString()), out var n) ? n : def;
}/// <summary>
/// Registri fitur: sumber kebenaran daftar fitur. Tambah fitur = tambah 1 file + 1 baris di sini.
/// Urutan default = urutan array; user bisa geser di GUI.
/// </summary>
public static class FeatureRegistry
{
    public static readonly IBotFeature[] All =
    {
        new OpenHomeFeature(),
        new OpenGraphQlFeature(),   // wajib sebelum fitur GraphQL
        new ScrollFeedFeature(),
        new OpenProfileFeature(),
        new AddFriendFeature(),
        new AddSuggestionsFeature(),
        new ConfirmFriendFeature(),
        new CreateGroupFeature(),
        new PostTimelineFeature(),
        new StoryFeature(),
        new ProfilePicFeature(),
        new SetBioFeature(),
        new ProModeFeature(),
        new PostStatusFeature(),    // legacy composer-based (belum tervalidasi)
    };

    public static IBotFeature? Find(string id) => All.FirstOrDefault(f => f.Id == id);
}
