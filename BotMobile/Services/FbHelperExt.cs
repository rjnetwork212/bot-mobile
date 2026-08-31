using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>Wrapper fungsi tambahan window.__mfb (v0.6: group/message/locale/setting).</summary>
public static class FbHelperExt
{
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
        catch { return new(); }
    }

    static string OutcomeOf(Dictionary<string, string> d) =>
        d.TryGetValue("outcome", out var o) ? o
        : d.TryGetValue("__error", out var e) ? "err:" + e
        : d.Count == 0 ? "empty_response" : "unknown_keys";

    static async Task<string> CallAsync(IPage page, string expr)
    {
        try { return await page.EvaluateExpressionAsync<string>(expr); }
        catch (Exception ex)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { __error = "eval_failed", message = ex.Message.Split('\n')[0] });
        }
    }

    public static async Task<(bool Ok, string Outcome)> ChangeLocaleAsync(IPage page, string locale)
    {
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.changeLocale({System.Text.Json.JsonSerializer.Serialize(locale)}).then(r => r)"));
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    public static async Task<(List<string> Groups, string Debug)> FetchGroupsAsync(IPage page)
    {
        var json = await CallAsync(page, "window.__mfb.fetchGroups().then(r => r)");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("groups", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                return (arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList(),
                    root.TryGetProperty("source", out var src) ? src.GetString() ?? "" : "");
            if (root.TryGetProperty("__error", out var err))
                return (new List<string>(), "err:" + err.GetString() +
                    (root.TryGetProperty("body", out var body) ? " | " + body.GetString() : ""));
        }
        catch (Exception ex)
        {
            return (new List<string>(), "parse_fail: " + ex.Message + " | " + json[..Math.Min(200, json.Length)]);
        }
        return (new List<string>(), "unknown");
    }

    public static async Task<(bool Ok, string Outcome, string Method)> AddGroupMemberAsync(IPage page, string threadId, string uid)
    {
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.addGroupMember('{threadId}', '{uid}').then(r => r)"));
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d),
            d.TryGetValue("method", out var m) ? m : "");
    }

    public static async Task<(bool Ok, string Outcome)> SendGroupMessageAsync(IPage page, string threadId, string text)
    {
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.sendGroupMessage('{threadId}', {System.Text.Json.JsonSerializer.Serialize(text)}).then(r => r)"));
        var outcome = OutcomeOf(d);
        if (d.TryGetValue("body", out var body)) outcome += " | " + body;
        return (d.TryGetValue("ok", out var ok) && ok == "True", outcome);
    }

    public static async Task<(bool Ok, string Outcome)> PostStatusTaggedAsync(IPage page, string message, string privacy, List<string> tagIds)
    {
        var ids = System.Text.Json.JsonSerializer.Serialize(tagIds);
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.postStatusTagged({System.Text.Json.JsonSerializer.Serialize(message)}, {System.Text.Json.JsonSerializer.Serialize(privacy)}, {ids}).then(r => r)"));
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    public static async Task<(bool Ok, string Outcome, string PostId)> CreateTagPostAsync(IPage page, string message, string privacy, List<string> tagIds, string linkUrl)
    {
        var ids = System.Text.Json.JsonSerializer.Serialize(tagIds);
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.createTagPost({System.Text.Json.JsonSerializer.Serialize(message)}, {System.Text.Json.JsonSerializer.Serialize(privacy)}, {ids}, {System.Text.Json.JsonSerializer.Serialize(linkUrl)}).then(r => r)"));
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d),
            d.TryGetValue("post_id", out var p) ? p : "");
    }

    public static async Task<(bool Ok, string Outcome)> SendGroupMessageLightspeedAsync(IPage page, string threadId, string text)
    {
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.sendGroupMessageLightspeed('{threadId}', {System.Text.Json.JsonSerializer.Serialize(text)}).then(r => r)"));
        var outcome = OutcomeOf(d);
        if (d.TryGetValue("body", out var body)) outcome += " | " + body;
        return (d.TryGetValue("ok", out var ok) && ok == "True", outcome);
    }

    public static async Task<(bool Ok, string Outcome)> SettingProfileAsync(IPage page)
    {
        var js = System.IO.File.Exists("Resources/setting_profile_js.txt")
            ? System.IO.File.ReadAllText("Resources/setting_profile_js.txt")
            : ReadEmbedded();
        var d = Deserialize(await CallAsync(page,
            $"window.__mfb.settingProfile({System.Text.Json.JsonSerializer.Serialize(js)}).then(r => r)"));
        return (d.TryGetValue("ok", out var ok) && ok == "True", OutcomeOf(d));
    }

    static string ReadEmbedded()
    {
        var asm = typeof(FbHelperExt).Assembly;
        using var s = asm.GetManifestResourceStream("BotMobile.Resources.setting_profile_js.txt")
            ?? throw new InvalidOperationException("setting_profile_js.txt hilang");
        using var r = new System.IO.StreamReader(s);
        return r.ReadToEnd();
    }
}
