using BotMobile.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotMobile.Services;

/// <summary>
/// Parser baris akun multi-format. Deteksi kolom by POLA, bukan posisi:
///   cookie → ada "=" dan ";" dan nama cookie dikenal (datr/c_user/xs/fr/sb/mid/csrftoken)
///   email  → ada "@" + domain berisi "."
///   2fa    → base32 murni 16-64 char (A-Z2-7) — dicek TERAKHIR
///   uid    → kolom pertama, password → kolom kedua
/// Support: uid|pass, uid|pass|cookies, uid|pass|cookies|secret2fa,
///          uid|pass|email|cookies, uid|pass|cookies|email (dan kombinasi lain)
/// </summary>
public static class AccountParser
{
    static readonly string[] CookieNames =
    {
        "datr", "c_user", "xs", "fr", "sb", "mid", "csrftoken", "wd", "dpr", "ps_l", "ps_n",
    };

    static readonly char[] Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public static Account? ParseLine(string line)
    {
        var t = line.Trim();
        if (t.Length == 0 || t.StartsWith("#") || t.StartsWith("//")) return null;
        var parts = t.Split('|');
        if (parts.Length < 2 || parts[0].Trim().Length == 0) return null;

        var acc = new Account { Uid = parts[0].Trim(), Password = parts[1].Trim() };

        var cookieParts = new List<string>();
        foreach (var raw in parts.Skip(2))
        {
            var col = raw.Trim();
            if (col.Length == 0) continue;
            switch (Classify(col))
            {
                case ColKind.Cookie:
                    cookieParts.Add(col);
                    break;
                case ColKind.Email: acc.Email = col; break;
                case ColKind.Tfa: acc.Secret2Fa = col; break;
            }
        }
        acc.Cookies = string.Join("; ", cookieParts);
        return acc;
    }

    enum ColKind { Cookie, Email, Tfa, Unknown }

    static ColKind Classify(string col)
    {
        if (IsEmail(col)) return ColKind.Email;
        if (IsCookie(col)) return ColKind.Cookie;
        if (IsBase32(col)) return ColKind.Tfa;
        return ColKind.Unknown;
    }

    static bool IsEmail(string s) =>
        s.Contains('@') && s.Contains('.') && s.IndexOf('@') < s.LastIndexOf('.');

    static bool IsCookie(string s)
    {
        if (!s.Contains('=')) return false;
        var first = s.Split(';')[0].Trim();
        var i = first.IndexOf('=');
        if (i <= 0) return false;
        return CookieNames.Contains(first[..i].Trim().ToLowerInvariant());
    }

    static bool IsBase32(string s) =>
        s.Length >= 16 && s.Length <= 64 && s.All(Base32.Contains);

    // export kanonik: uid|pass|cookies|secret2fa|email (kolom kosong dilewati)
    public static string ToLine(Account a)
    {
        var cols = new List<string> { a.Uid, a.Password };
        if (!string.IsNullOrEmpty(a.Cookies)) cols.Add(a.Cookies);
        if (!string.IsNullOrEmpty(a.Secret2Fa)) cols.Add(a.Secret2Fa);
        if (!string.IsNullOrEmpty(a.Email)) cols.Add(a.Email);
        return string.Join("|", cols);
    }

    public static IEnumerable<Account> ParseLines(IEnumerable<string> lines) =>
        lines.Select(ParseLine).Where(a => a != null).Cast<Account>();
}
