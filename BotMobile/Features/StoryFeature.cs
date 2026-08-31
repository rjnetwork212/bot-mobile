using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Upload story foto (GraphQL: upload → StoriesCreateMutation). Port upload_story.</summary>
public class StoryFeature : IBotFeature
{
    static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp" };
    static readonly Random Rnd = new();

    public string Id => "story";
    public string Name => "Upload Story";
    public string Description => "Upload foto jadi story (GraphQL, port Bot_Ngekeng).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("FotoDir", "Folder foto", "Data/Foto"),
        ("Count", "Jumlah story", "1"),
        ("PrivacyMode", "Privacy (PUBLIC/FRIENDS/SELF)", "PUBLIC"),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var dir = cfg.Get("FotoDir", "Data/Foto");
        var count = Math.Clamp(cfg.GetInt("Count", 1), 1, 10);
        var mode = cfg.Get("PrivacyMode", "PUBLIC");

        var photos = GetPhotos(dir, acc.Uid);
        if (photos.Count == 0) { log($"tidak ada foto di {Path.GetFullPath(dir)}"); return; }
        log($"{photos.Count} foto tersedia, upload {count} story ({mode})");

        var (_, privOutcome) = await FbHelper.SetStoryPrivacyAsync(page, mode);
        log($"privacy {mode}: {privOutcome}");

        var ok = 0;
        for (int i = 0; i < count; i++)
        {
            if (flags.SessionExpired) break;
            var photo = photos[i % photos.Count];
            var (upOk, photoId) = await FbHelper.UploadPhotoAsync(page, photo, "story");
            if (!upOk) { log($"{Path.GetFileName(photo)}: upload gagal"); continue; }
            var (stOk, outcome, storyId) = await FbHelper.CreateStoryAsync(page, photoId);
            if (stOk) { ok++; log($"story OK (id {storyId})"); }
            else if (outcome == "session_expired" || outcome == "no_tokens") { flags.SessionExpired = true; break; }
            else log($"story gagal: {outcome}");
            await Task.Delay(5000);
        }
        log($"selesai: {ok}/{count} story");
    }

    // shuffle deterministik per uid (port pola Bot_Ngekeng: seed = hash uid)
    internal static System.Collections.Generic.List<string> GetPhotos(string dir, string uid)
    {
        var path = Path.IsPathRooted(dir) ? dir : Path.Combine(AppContext.BaseDirectory, dir);
        if (!Directory.Exists(path)) return new();
        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(f => ImageExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => Fingerprint.StableHash(uid + f))
            .ToList();
        return files;
    }
}
