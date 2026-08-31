using BotMobile.Models;
using BotMobile.Features.Selector;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>
/// State machine relogin (port relogin.py — subset inti, hasil probe m.facebook).
/// State: login_form / saved_profile_resume / needs_2fa / consent / captcha /
/// checkpoint_disabled / checkpoint / suspicious / logged_in / login_failed.
/// </summary>
public static class FacebookRelogin
{
    public static async Task<string> DetectAsync(IPage page)
    {
        var url = page.Url ?? "";
        if (url.Contains("/login/identify") || url.Contains("/login/device-based"))
            return "login_failed_identify";
        if (url.Contains("two_step") || url.Contains("/checkpoint"))
        {
            if (url.Contains("two_step")) return "needs_2fa";
            if (await HasAny(page, LoginSelectors.TfaCode)) return "needs_2fa";
            // checkpoint numerik tanpa tombol actionable = disabled (terminal)
            return url.Contains("/checkpoint") && System.Text.RegularExpressions.Regex.IsMatch(url, @"/checkpoint/\d+/?$")
                ? "checkpoint_disabled"
                : "checkpoint";
        }
        var text = await BodyText(page);
        var stateText = FbLang.DetectStateText(text);
        if (stateText == "captcha_blocked") return "captcha";
        if (stateText == "checkpoint_disabled") return "checkpoint_disabled";
        if (stateText == "suspicious") return "suspicious";
        if (stateText == "needs_2fa") return "needs_2fa";
        if (stateText == "needs_email_verify") return "needs_email_verify";
        if (url.Contains("/login") || url.Contains("login.php"))
        {
            // saved-profile resume: tombol Continue (multi-bahasa) tanpa form pass
            var hasPass = await HasAny(page, "input[name='pass']");
            if (!hasPass && await HasAny(page, FbLang.ContinueLabels)) return "saved_profile_resume";
            if (await HasAny(page, "input[name='email']") && hasPass) return "login_form";
            return "unknown_login";
        }
        if (url.Contains("facebook.com")) return "logged_in";
        return "unknown";
    }

    /// <summary>Port _handle_saved_profile_flow: klik Continue → modal password → submit.</summary>
    public static async Task<bool> HandleSavedProfileAsync(IPage page, Account acc, Action<string> log)
    {
        log("state: saved_profile_resume → klik Continue");
        await UiSelector.ClickButtonByLabelsAsync(page, FbLang.ContinueLabels);
        await Task.Delay(4000);

        // modal password
        var pass = await WaitForAsync(page, "input[type='password']", 10000);
        if (pass == null)
        {
            log("modal password tidak muncul");
            return false;
        }
        await page.TypeAsync("input[type='password']", acc.Password, new TypeOptions { Delay = 40 });
        await Task.Delay(400);
        if (!await UiSelector.ClickButtonByLabelsAsync(page, FbLang.LoginButtonLabels))
        {
            try { await page.Keyboard.PressAsync("Enter"); } catch { }
        }
        await Task.Delay(5000);
        return await FacebookLogin.IsLoggedInAsync(page) && !page.Url.Contains("/login");
    }

    /// <summary>Port _handle_2fa_flow: TOTP dari secret 2FA akun.</summary>
    public static async Task<(bool Ok, string Outcome)> Handle2FaAsync(IPage page, Account acc, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(acc.Secret2Fa))
        {
            log("2FA tanpa secret di DB — selesaikan manual");
            return (false, "no_totp_secret");
        }
        string? code;
        try { code = CaptionBuilder.Totp(acc.Secret2Fa); }
        catch { log("secret 2FA invalid (base32)"); return (false, "no_totp_secret"); }

        if (await WaitForAsync(page, LoginSelectors.TfaCode, 20000) == null)
            return (false, "2fa_form_not_found");
        await page.TypeAsync(LoginSelectors.TfaCode, code, new TypeOptions { Delay = 40 });
        await Task.Delay(400);
        if (!await UiSelector.ClickButtonByLabelsAsync(page, FbLang.ContinueLabels))
        {
            try
            {
                var submit = await page.QuerySelectorAsync("button[type='submit']");
                if (submit != null) await submit.ClickAsync();
                else await page.Keyboard.PressAsync("Enter");
            }
            catch { }
        }
        log("TOTP dikirim");
        await Task.Delay(6000);
        var ok = await FacebookLogin.IsLoggedInAsync(page) && !page.Url.Contains("/checkpoint");
        if (ok) return (true, "ok_totp");
        // setelah 2FA kadang ada halaman lanjutan — masih di checkpoint = gagal
        return (false, "2fa_submit_not_finished");
    }

    /// <summary>Port _try_dismiss_consent_flow (ringkas): radio "gratis dengan iklan" → Continue.</summary>
    public static async Task DismissConsentAsync(IPage page, Action<string> log)
    {
        for (int i = 0; i < 6; i++)
        {
            var clicked = false;
            if (await UiSelector.ClickButtonByLabelsAsync(page, FbLang.AdsFreeRadioLabels)) clicked = true;
            if (await UiSelector.ClickButtonByLabelsAsync(page, FbLang.ContinueLabels)) clicked = true;
            if (!clicked) break;
            log("dismiss consent...");
            await Task.Delay(3000);
        }
    }

    // ---------- helpers ----------

    static async Task<string> BodyText(IPage page)
    {
        try
        {
            return await page.EvaluateExpressionAsync<string>(
                "document.body ? document.body.innerText.slice(0, 3000) : ''");
        }
        catch { return ""; }
    }

    static async Task<bool> HasAny(IPage page, string selector)
    {
        try { return await page.QuerySelectorAsync(selector) != null; }
        catch { return false; }
    }

    static async Task<bool> HasAny(IPage page, string[] labels)
    {
        try { return await UiSelector.HasLabelAsync(page, labels); }
        catch { return false; }
    }

    static async Task<IElementHandle?> WaitForAsync(IPage page, string selector, int timeoutMs)
    {
        try
        {
            return await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Timeout = timeoutMs });
        }
        catch { return null; }
    }
}
