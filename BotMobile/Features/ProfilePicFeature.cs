using BotMobile.Models;
using BotMobile.Services;
using PuppeteerSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features;

/// <summary>Upload foto profil (GraphQL: upload → ProfileCometProfilePictureSetMutation).</summary>
public class ProfilePicFeature : IBotFeature
{
    public string Id => "profile_pic";
    public string Name => "Foto Profil";
    public string Description => "Ganti foto profil dari folder (GraphQL, port Bot_Ngekeng).";
    public (string, string, string)[] ParamDefs => new[]
    {
        ("FotoDir", "Folder foto", "Data/Foto"),
        ("Caption", "Caption foto profil", ""),
    };
    public bool DefaultEnabled => false;

    public async Task RunAsync(IPage page, Account acc, FeatureConfig cfg, Action<string> log, RunFlags flags)
    {
        var dir = cfg.Get("FotoDir", "Data/Foto");
        var caption = cfg.Get("Caption", "");
        var photos = StoryFeature.GetPhotos(dir, acc.Uid);
        if (photos.Count == 0) { log($"tidak ada foto di {Path.GetFullPath(dir)}"); return; }

        var photo = photos[0]; // urut deterministik per uid → foto pertama
        var (upOk, photoId) = await FbHelper.UploadPhotoAsync(page, photo, "profile_pic");
        if (!upOk) { log($"{Path.GetFileName(photo)}: upload gagal"); return; }
        log($"upload OK (photo {photoId})");

        var (_, outcome) = await FbHelper.SetProfilePicAsync(page, photoId, caption);
        if (outcome == "profile_pic_set") log("foto profil OK");
        else if (outcome == "session_expired" || outcome == "no_tokens") { flags.SessionExpired = true; log("session mati"); }
        else log($"set gagal: {outcome}");
    }
}
