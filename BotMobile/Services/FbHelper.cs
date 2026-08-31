using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Jembatan C# ke window.__mfb (fbhelper.js embedded). Port pola Bot_Ngekeng fb_client:
/// inject helper sekali, panggil fungsi JS, parse JSON balik.
/// </summary>
public static class FbHelper
{
    static string? _js;

    public static string Js()
    {
        if (_js == null)
        {
            var asm = typeof(FbHelper).Assembly;
            using var s = asm.GetManifestResourceStream("BotMobile.Resources.fbhelper.js")
                ?? throw new InvalidOperationException("embedded fbhelper.js hilang");
            using var r = new StreamReader(s);
            _js = r.ReadToEnd();
        }
        return _js;
    }

    /// <summary>Inject helper (idempotent di JS side) + tunggu terpasang.</summary>
    public static async Task<bool> InstallAsync(IPage page)
    {
        await page.EvaluateFunctionOnNewDocumentAsync(Js());
        // dipanggil juga langsung (halaman mungkin sudah ke-load sebelum inject)
        try
        {
            await page.EvaluateExpressionAsync("(function(){ " + Js() + " })(); window.__mfb ? '1' : '0'");
        }
        catch { /* halaman navigasi — akan terpasang di load berikutnya */ }
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var ok = await page.EvaluateExpressionAsync<bool>("!!(window.__mfb && window.__mfb._installed)");
                if (ok) return true;
                await Task.Delay(500);
            }
        }
        catch { }
        return false;
    }

    /// <summary>Tokens: fb_dtsg/lsd/jazoest/userId. userId "0" = belum login.</summary>
    public static async Task<Dictionary<string, string>> GetTokensAsync(IPage page)
    {
        var json = await page.EvaluateExpressionAsync<string>("JSON.stringify(window.__mfb.getTokens())");
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
    }

    /// <summary>Tunggu token user match (port wait_user_loaded).</summary>
    public static async Task<bool> WaitUserLoadedAsync(IPage page, string uid, int maxWaitSec = 15)
    {
        for (int i = 0; i < maxWaitSec; i++)
        {
            try
            {
                var t = await GetTokensAsync(page);
                if (t.TryGetValue("userId", out var cur) && cur == uid) return true;
            }
            catch { }
            await Task.Delay(1000);
        }
        return false;
    }

    static async Task<string> CallAsync(IPage page, string expr)
    {
        try
        {
            return await page.EvaluateExpressionAsync<string>(expr);
        }
        catch (Exception ex)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                new { __error = "eval_failed", message = ex.Message.Split('\n')[0] });
        }
    }

    public static async Task<(bool Ok, string Outcome, string Raw)> AddFriendAsync(IPage page, string targetUid) =>
        Parse(await CallAsync(page, $"window.__mfb.addFriend('{targetUid}').then(r => r)"));

    public static async Task<List<string>> FetchPendingRequestsAsync(IPage page) =>
        UidList(await CallAsync(page, "window.__mfb.fetchPendingRequests().then(r => r)"));

    public static async Task<(bool Ok, string Outcome)> ConfirmFriendAsync(IPage page, string uid) =>
        Parse2(await CallAsync(page, $"window.__mfb.confirmFriendRequest('{uid}').then(r => r)"));

    public static async Task<List<string>> FetchFriendsAsync(IPage page) =>
        UidList(await CallAsync(page, "window.__mfb.fetchFriends().then(r => r)"));

    public static async Task<List<string>> FetchSuggestionsAsync(IPage page, int count) =>
        UidList(await CallAsync(page, $"window.__mfb.fetchSuggestions({count}).then(r => r)"));

    public static async Task<(bool Ok, string Outcome, string ThreadId)> CreateGroupAsync(IPage page, string name)
    {
        var json = await CallAsync(page, $"window.__mfb.createGroup({System.Text.Json.JsonSerializer.Serialize(name)}).then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d), d.TryGetValue("thread_id", out var t) ? t : "");
    }

    public static async Task<(bool Ok, string Outcome, string PostId)> PostStatusAsync(IPage page, string message, string privacy)
    {
        var json = await CallAsync(page,
            $"window.__mfb.postStatus({System.Text.Json.JsonSerializer.Serialize(message)}, {System.Text.Json.JsonSerializer.Serialize(privacy)}).then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d), d.TryGetValue("post_id", out var p) ? p : "");
    }

    public static async Task<(bool Ok, string Outcome)> SetBioTextAsync(IPage page, string bio)
    {
        var json = await CallAsync(page, $"window.__mfb.setBioText({System.Text.Json.JsonSerializer.Serialize(bio)}).then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    public static async Task<(bool Ok, string Outcome)> ActivateProModeAsync(IPage page)
    {
        var json = await CallAsync(page, "window.__mfb.activateProMode().then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    public static async Task<(bool Ok, string PhotoId)> UploadPhotoAsync(IPage page, string filePath, string purpose)
    {
        var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(filePath));
        var mime = MimeOf(Path.GetExtension(filePath));
        var json = await CallAsync(page,
            $"window.__mfb.uploadPhotoGeneric('{b64}', '{mime}', {System.Text.Json.JsonSerializer.Serialize(Path.GetFileName(filePath))}, '{purpose}').then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", d.TryGetValue("photo_id", out var p) ? p : "");
    }

    public static async Task<(bool Ok, string Outcome, string StoryId)> CreateStoryAsync(IPage page, string photoId)
    {
        var json = await CallAsync(page, $"window.__mfb.createStory('{photoId}').then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d), d.TryGetValue("story_id", out var sid) ? sid : "");
    }

    public static async Task<(bool Ok, string Outcome)> SetStoryPrivacyAsync(IPage page, string mode)
    {
        var json = await CallAsync(page, $"window.__mfb.setStoryPrivacy({System.Text.Json.JsonSerializer.Serialize(mode)}).then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    public static async Task<(bool Ok, string Outcome)> SetProfilePicAsync(IPage page, string photoId, string caption)
    {
        var json = await CallAsync(page,
            $"window.__mfb.setProfilePic('{photoId}', {System.Text.Json.JsonSerializer.Serialize(caption ?? "")}).then(r => r)");
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    // ---------- utils ----------

    // JSON helper: {"ok":true} bool — buang kalau pakai Dictionary<string,string> (lempar exception)
    static Dictionary<string, string> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return new();
            var dict = new Dictionary<string, string>();
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.True => "True",
                    System.Text.Json.JsonValueKind.False => "False",
                    _ => p.Value.ToString(),
                };
            return dict;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    static (bool, string, string) Parse(string json)
    {
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True",
                OutcomeOf(d),
                d.TryGetValue("previews", out var p) ? p : "");
    }

    static (bool, string) Parse2(string json)
    {
        var d = Deserialize(json);
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    static string OutcomeOf(Dictionary<string, string> d) =>
        d.TryGetValue("outcome", out var o) ? o
        : d.TryGetValue("__error", out var e) ? "err:" + e
        : d.Count == 0 ? "empty_response" : "unknown_keys";

    static List<string> UidList(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("uids", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                return arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
            if (doc.RootElement.TryGetProperty("__error", out var err))
                return new List<string> { "ERR:" + err.GetString() };
        }
        catch { }
        return new List<string>();
    }

    static string MimeOf(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".png" => "image/png",
        _ => "image/png",
    };
}
