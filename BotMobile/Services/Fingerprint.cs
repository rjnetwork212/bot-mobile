using BotMobile.Models;

namespace BotMobile.Services;

/// <summary>Fingerprint mobile "seperti APK FB": UA FB IAB + viewport Android.</summary>
public static class Fingerprint
{
    public record Device(string Model, int W, int H, double Dpr);

    static readonly Device[] Devices =
    {
        new("SM-G991B", 384, 854, 2.75),
        new("Pixel 7", 412, 915, 2.625),
        new("22101316C", 393, 873, 2.75),
        new("SM-A536E", 360, 800, 3.0),
    };

    // hash deterministik (string.GetHashCode beda antar proses)
    public static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (var c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    public static Device DeviceFor(string uid) => Devices[StableHash(uid) % Devices.Length];

    public static string BuildUa(Device d) =>
        $"Mozilla/5.0 (Linux; Android 13; {d.Model} Build/TP1A.220624.014; wv) " +
        $"AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/124.0.0.0 Mobile Safari/537.36 " +
        $"[FB_IAB/FB4A;FBAV/460.0.0.60.120;]";
}

/// <summary>Loader embedded stealth.js.</summary>
public static class StealthLoader
{
    public static string Load()
    {
        var asm = typeof(StealthLoader).Assembly;
        using var s = asm.GetManifestResourceStream("BotMobile.Resources.stealth.js")
            ?? throw new System.InvalidOperationException("embedded stealth.js hilang");
        using var r = new System.IO.StreamReader(s);
        return r.ReadToEnd();
    }
}
