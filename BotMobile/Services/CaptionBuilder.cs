using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

namespace BotMobile.Services;

/// <summary>Caption builder — port caption_builder.py (16 bahasa, hook/cta/emoji/hashtag).</summary>
public static class CaptionBuilder
{
    record Templates(string[] Hook, string[] Cta);

    static readonly string[] OpeningEmoji = { "💕", "💖", "🥰", "✨", "🌸", "🔥", "💞", "🌹", "😘", "💋" };
    static readonly string[] CtaEmoji = { "👇", "👉", "💋", "😘", "🔗", "✨", "🎀", "💌" };

    static readonly Dictionary<string, Templates> _templates;
    static readonly List<string> _langs;

    static CaptionBuilder()
    {
        var asm = typeof(CaptionBuilder).Assembly;
        using var s = asm.GetManifestResourceStream("BotMobile.Resources.caption_templates.json")
            ?? throw new InvalidOperationException("caption_templates.json hilang");
        var raw = JsonDocument.Parse(s);
        var dict = new Dictionary<string, Templates>();
        foreach (var lang in raw.RootElement.EnumerateObject())
        {
            var hook = new List<string>();
            var cta = new List<string>();
            if (lang.Value.TryGetProperty("hook", out var h))
                foreach (var x in h.EnumerateArray()) hook.Add(x.GetString() ?? "");
            if (lang.Value.TryGetProperty("cta", out var c))
                foreach (var x in c.EnumerateArray()) cta.Add(x.GetString() ?? "");
            dict[lang.Name] = new Templates(hook.ToArray(), cta.ToArray());
        }
        _templates = dict;
        _langs = new List<string>(dict.Keys);
    }

    /// <summary>
    /// Build caption. port build_caption:
    /// {opening} {hook}~ {cta} {ctaEmoji} {link} + hashtag.
    /// lang: "id"/"en"/... atau "random"; customPool = template user dengan {link}.
    /// </summary>
    public static string Build(string? link, string lang = "random", int? seed = null,
        List<string>? customPool = null, string? hashtags = null, int hashtagPick = 5)
    {
        var rnd = seed.HasValue ? new Random(seed.Value) : new Random();
        var linkClean = (link ?? "").Trim();

        string hook, cta;
        if (customPool is { Count: > 0 })
        {
            var t = customPool[rnd.Next(customPool.Count)];
            return linkClean.Length > 0
                ? (t.Contains("{link}") ? t.Replace("{link}", linkClean) : t + " " + linkClean)
                : t;
        }
        if (lang == "mixed" && _templates.Count > 0)
        {
            var l1 = _langs[rnd.Next(_langs.Count)];
            var l2 = _langs[rnd.Next(_langs.Count)];
            hook = Pick(_templates[l1].Hook, rnd);
            cta = Pick(_templates[l2].Cta, rnd);
        }
        else
        {
            if (lang == "random" || !_templates.ContainsKey(lang))
                lang = _langs[rnd.Next(_langs.Count)];
            hook = Pick(_templates[lang].Hook, rnd);
            cta = Pick(_templates[lang].Cta, rnd);
        }

        var opening = OpeningEmoji[rnd.Next(OpeningEmoji.Length)];
        var ctaEm = CtaEmoji[rnd.Next(CtaEmoji.Length)];
        var text = $"{opening} {hook}~ {cta} {ctaEm}";
        if (linkClean.Length > 0) text += " " + linkClean;
        if (!string.IsNullOrWhiteSpace(hashtags))
            text += "\n\n" + PickHashtags(hashtags, rnd, hashtagPick);
        return text;
    }

    static string Pick(string[] arr, Random rnd) => arr.Length == 0 ? "" : arr[rnd.Next(arr.Length)];

    static string PickHashtags(string pool, Random rnd, int n)
    {
        var tags = pool.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var norm = new List<string>();
        foreach (var t in tags)
        {
            var v = t.StartsWith("#") ? t : "#" + t;
            if (!norm.Contains(v)) norm.Add(v);
        }
        // shuffle
        for (int i = norm.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (norm[i], norm[j]) = (norm[j], norm[i]);
        }
        return string.Join(" ", norm.GetRange(0, Math.Min(n, norm.Count)));
    }

    /// <summary>TOTP 6 digit (port pyotp; HMAC-SHA1 30s). secret: strip spasi + upper.</summary>
    public static string Totp(string secretBase32)
    {
        var secret = secretBase32.Replace(" ", "").ToUpperInvariant();
        var key = Base32Decode(secret);
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var code = ((hash[offset] & 0x7F) << 24)
                 | ((hash[offset + 1] & 0xFF) << 16)
                 | ((hash[offset + 2] & 0xFF) << 8)
                 | (hash[offset + 3] & 0xFF);
        return (code % 1_000_000).ToString("D6");
    }

    static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = new List<bool>();
        foreach (var c in input)
        {
            if (c == '=') continue;
            var idx = alphabet.IndexOf(c);
            if (idx < 0) throw new FormatException($"base32 invalid char: {c}");
            for (int b = 4; b >= 0; b--) bits.Add((idx >> b & 1) == 1);
        }
        var bytes = new byte[bits.Count / 8];
        for (int i = 0; i < bytes.Length; i++)
            for (int b = 0; b < 8; b++)
                if (bits[i * 8 + b]) bytes[i] |= (byte)(1 << (7 - b));
        return bytes;
    }
}
