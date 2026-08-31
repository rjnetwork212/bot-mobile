using BotMobile.Models;
using BotMobile.Features.Selector;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// Alur login Facebook (cookies → password). Fingerprint mobile via Fingerprint.
/// SEMUA selector di file ini divalidasi lewat ProbeRunner (--probe), jangan ubah tanpa probe ulang.
/// </summary>
public static class FacebookLogin
{
    public static List<(string Name, string Value)> ParseCookies(string raw)
    {
        var list = new List<(string, string)>();
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0) continue;
            list.Add((part[..i].Trim(), part[(i + 1)..].Trim()));
        }
        return list;
    }

    public static async Task SetupMobileAsync(IPage page, string uid)
    {
        var dev = Fingerprint.DeviceFor(uid);
        // UA + UA-CH metadata (mobile=true/Android) — tanpa metadata FB serve desktop
        await page.SetUserAgentAsync(Fingerprint.BuildUa(dev), Fingerprint.BuildUaMetadata(dev));
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width = dev.W, Height = dev.H, DeviceScaleFactor = dev.Dpr,
            IsMobile = true, HasTouch = true,
        });
        await page.EvaluateExpressionOnNewDocumentAsync(StealthLoader.Load());
        page.DefaultNavigationTimeout = 60000;
    }

    public static async Task<bool> TryCookieLoginAsync(IPage page, Account acc, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(acc.Cookies)) return false;
        // WAJIB: goto dulu ke origin FB (page-level SetCookieAsync sebelum navigasi
        // pertama nyaris selalu gagal masuk jar — hasil CookieProbe: set OK tapi jar 0)
        try
        {
            await page.GoToAsync("https://m.facebook.com/", new NavigationOptions
            {
                Timeout = 30000,
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
            });
        }
        catch (NavigationException) { /* abort saat redirect — jar tetap bisa di-set */ }

        var pairs = ParseCookies(acc.Cookies);
        log($"set {pairs.Count} cookie via CDP");
        foreach (var (name, value) in pairs)
        {
            try
            {
                await page.Client.SendAsync("Network.setCookie", new
                {
                    name,
                    value,
                    domain = ".facebook.com",
                    path = "/",
                    secure = true,
                    httpOnly = false,
                });
            }
            catch (Exception ex)
            {
                log($"set {name} gagal: {ex.Message.Split('\n')[0]}");
            }
        }
        log($"cookies ({pairs.Count} item)");

        try
        {
            await page.GoToAsync("https://www.facebook.com/", WaitUntilNavigation.DOMContentLoaded);
        }
        catch (NavigationException)
        {
            // FB kadang abort navigation saat redirect — url final tetap dicek
        }
        await Task.Delay(4000);
        return await IsLoggedInAsync(page) && !page.Url.Contains("/login");
    }

    /// <summary>Login password + outcome detail (ok/checkpoint/wrongpass/blocked/unknown).</summary>
    public static async Task<(bool Ok, string Outcome)> TryPasswordLoginAsync(IPage page, Account acc, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(acc.Password)) return (false, "nopassword");
        // cookie mati yang masih nempel bikin /login/ render varian "saved login" tanpa form
        try { await page.Client.SendAsync("Network.clearBrowserCookies"); } catch { }
        try
        {
            await page.GoToAsync("https://m.facebook.com/", WaitUntilNavigation.DOMContentLoaded);
        }
        catch (NavigationException) { }
        await Task.Delay(2500);

        // state machine relogin (port relogin.py) — max 2 attempt
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var state = await FacebookRelogin.DetectAsync(page);
            log($"state: {state} (attempt {attempt})");
            switch (state)
            {
                case "logged_in":
                    return (await IsLoggedInAsync(page) && !IsLoginPageUrl(page.Url), "ok");

                case "saved_profile_resume":
                    var saved = await FacebookRelogin.HandleSavedProfileAsync(page, acc, log);
                    if (saved) return (true, "ok_saved_profile");
                    break; // retry → detect ulang

                case "needs_2fa":
                    var (twoFa, twoFaOutcome) = await FacebookRelogin.Handle2FaAsync(page, acc, log);
                    if (twoFa) return (true, "ok_2fa");
                    return (false, twoFaOutcome == "no_totp_secret" ? "checkpoint" : "checkpoint");

                case "consent":
                    await FacebookRelogin.DismissConsentAsync(page, log);
                    break;

                case "captcha":
                    return (false, "captcha");

                case "checkpoint_disabled":
                    return (false, "blocked");

                case "checkpoint":
                    return (false, "checkpoint");

                case "login_failed_identify":
                    return (false, "checkpoint");

                case "suspicious":
                    if (!await UiSelector.ClickButtonByLabelsAsync(page, FbLang.IdentityButtons))
                        return (false, "checkpoint");
                    await Task.Delay(4000);
                    break;

                case "login_form":
                {
                    await page.TypeAsync(LoginSelectors.Email, acc.Uid, new TypeOptions { Delay = 50 });
                    await page.TypeAsync(LoginSelectors.Pass, acc.Password, new TypeOptions { Delay = 50 });
                    await Task.Delay(400);
                    await page.ClickAsync(LoginSelectors.Submit);
                    log("submit login");
                    var (ok, outcome) = await WaitLoginResultAsync(page);
                    if (ok) return (true, "ok");
                    if (outcome == "blocked") return (false, "blocked");
                    if (outcome == "wrongpass") return (false, "wrongpass");
                    if (outcome == "checkpoint") return (false, "checkpoint");
                    break; // unknown → detect ulang
                }

                default:
                {
                    // form tidak ada & tidak dikenal: mungkin session masih valid
                    if (await IsLoggedInAsync(page)) return (true, "ok");
                    // coba buka form eksplisit
                    try
                    {
                        await page.GoToAsync("https://m.facebook.com/login/", WaitUntilNavigation.DOMContentLoaded);
                    }
                    catch (NavigationException) { }
                    await Task.Delay(2000);
                    if (!await HasSelector(page, LoginSelectors.Email))
                    {
                        var st2 = await FacebookRelogin.DetectAsync(page);
                        if (st2 == "saved_profile_resume")
                        {
                            var saved2 = await FacebookRelogin.HandleSavedProfileAsync(page, acc, log);
                            if (saved2) return (true, "ok_saved_profile");
                        }
                        return (false, "form_not_found");
                    }
                    break;
                }
            }
        }
        var finalState = await FacebookRelogin.DetectAsync(page);
        return (false, finalState.Contains("checkpoint") || finalState == "needs_2fa" ? "checkpoint"
            : finalState == "captcha" ? "captcha" : "unknown");
    }

    static async Task<bool> HasSelector(IPage page, string selector)
    {
        try { return await page.QuerySelectorAsync(selector) != null; }
        catch { return false; }
    }

    /// <summary>
    /// Tunggu hasil login sampai 30 dtk. Outcome (hasil probe nyata):
    ///   ok / checkpoint / wrongpass / blocked (rate-limit FB) / unknown
    /// Catatan: c_user bisa nempel walau session mati (FB redirect ke /login/),
    /// jadi login dianggap sukses HANYA jika c_user+xs ada DAN url bukan halaman login.
    /// </summary>
    public static async Task<(bool Ok, string Outcome)> WaitLoginResultAsync(IPage page)
    {
        string url = "", text = "";
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(1000);
            try
            {
                url = page.Url ?? "";
                if (url.Contains("checkpoint") || url.Contains("two_step")) break;
                if (await IsLoggedInAsync(page)) break;
                text = await page.EvaluateExpressionAsync<string>(
                    "document.body ? document.body.innerText.slice(0, 3000) : ''");
                if (LoginText.IsBlocked(text) || LoginText.IsWrongPassword(text)) break;
            }
            catch (Exception)
            {
                // halaman navigasi di tengah poll (context destroyed) — login kemungkinan sukses,
                // lanjut poll sampai halaman stabil lalu cek cookie
            }
        }
        await Task.Delay(2000); // stabilkan setelah navigasi
        if (url.Contains("checkpoint") || url.Contains("two_step") ||
            await HasSelector(page, LoginSelectors.TfaCode))
            return (false, "checkpoint");
        if (await IsLoggedInAsync(page) && !IsLoginPageUrl(page.Url)) return (true, "ok");
        try
        {
            text = await page.EvaluateExpressionAsync<string>(
                "document.body ? document.body.innerText.slice(0, 3000) : ''");
        }
        catch { text = ""; }
        if (LoginText.IsBlocked(text)) return (false, "blocked");
        if (LoginText.IsWrongPassword(text)) return (false, "wrongpass");
        return (false, "unknown");
    }

    static bool IsLoginPageUrl(string? url) =>
        url != null && (url.Contains("/login") || url.Contains("login.php"));

    public static async Task<bool> IsLoggedInAsync(IPage page)
    {
        try
        {
            var cookies = await page.GetCookiesAsync();
            return cookies.Any(c => c.Name == "c_user" && !string.IsNullOrEmpty(c.Value))
                && cookies.Any(c => c.Name == "xs" && !string.IsNullOrEmpty(c.Value));
        }
        catch { return false; }
    }

    public static async Task<string> DumpCookiesAsync(IPage page)
    {
        var cookies = await page.GetCookiesAsync();
        return string.Join("; ", cookies
            .Where(c => c.Domain != null && c.Domain.Contains("facebook"))
            .Select(c => $"{c.Name}={c.Value}"));
    }
}

public static class LoginText
{
    public static bool IsWrongPassword(string text) =>
        ContainsAny(text, "incorrect", "Incorrect", "salah", "Salah", "wrong password", "sandi");

    // hasil probe nyata: "You've tried to log in too many times..." (rate-limit FB)
    public static bool IsBlocked(string text) =>
        ContainsAny(text, "too many times", "temporary block", "try again later");

    static bool ContainsAny(string text, params string[] keys) => keys.Any(text.Contains);
}
