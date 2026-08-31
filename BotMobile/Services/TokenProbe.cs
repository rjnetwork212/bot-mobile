using BotMobile.Models;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Services;

/// <summary>Research: dimana fb_dtsg/lsd/jazoest tersimpan di m.facebook.com (mobile web)?</summary>
public static class TokenProbe
{
    public static async Task Run(string uid)
    {
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");
        Account acc;
        using (var db = new AccountDb(dbPath))
            acc = db.GetAll().FirstOrDefault(a => a.Uid == uid)
                ?? throw new InvalidOperationException("uid tidak ada");

        var chrome = BotService.FindChrome(null);
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = chrome, Headless = true, DefaultViewport = null,
            Args = new[] { "--no-first-run" },
        });
        try
        {
            var page = await browser.NewPageAsync();
            await FacebookLogin.SetupMobileAsync(page, acc.Uid);
            await FacebookLogin.TryCookieLoginAsync(page, acc, m => Console.WriteLine($"[login] {m}"));

            foreach (var target in new[] { "https://m.facebook.com/", "https://www.facebook.com/", "https://mbasic.facebook.com/" })
            {
                Console.WriteLine($"===== {target} =====");
                try
                {
                    await page.GoToAsync(target, new NavigationOptions
                    {
                        Timeout = 30000,
                        WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    });
                }
                catch (NavigationException) { }
                await Task.Delay(4000);
                Console.WriteLine($"url: {page.Url}");
                var report = await page.EvaluateFunctionAsync<string[]>(@"() => {
                    const out = [];
                    const html = document.documentElement.outerHTML;
                    out.push('html_len=' + html.length);
                    out.push('input_fb_dtsg=' + (document.querySelector('input[name=""fb_dtsg""]') ? 'ADA' : 'tidak'));
                    out.push('input_lsd=' + (document.querySelector('input[name=""lsd""]') ? 'ADA' : 'tidak'));
                    out.push('input_jazoest=' + (document.querySelector('input[name=""jazoest""]') ? 'ADA' : 'tidak'));
                    out.push('DTSGInitialData=' + (window.DTSGInitialData ? 'ADA' : 'tidak'));
                    try { const r = window.require('DTSGInitData'); out.push('require_DTSG=' + (r && r.token ? 'ADA len=' + r.token.length : 'tidak')); } catch (e) { out.push('require_DTSG=throw'); }
                    out.push('LSD=' + (window.LSD ? 'ADA' : 'tidak'));
                    out.push('CurrentUserInitialData=' + (window.CurrentUserInitialData ? 'ADA' : 'tidak'));
                    const m1 = html.match(/""DTSGInitData""[^}]*?""token"":""([^""]+)""/);
                    out.push('regex_DTSGInitData=' + (m1 ? 'ADA len=' + m1[1].length : 'tidak'));
                    const m2 = html.match(/""DTSGInitialData""[^}]*?""token"":""([^""]+)""/);
                    out.push('regex_DTSGInitialData=' + (m2 ? 'ADA len=' + m2[1].length : 'tidak'));
                    const m3 = html.match(/name=""fb_dtsg"" value=""([^""]+)""/);
                    out.push('regex_input_dtsg=' + (m3 ? 'ADA len=' + m3[1].length : 'tidak'));
                    const m4 = html.match(/""LSD"",\[\],\{""token"":""([^""]+)""/);
                    out.push('regex_LSD=' + (m4 ? 'ADA len=' + m4[1].length : 'tidak'));
                    const m5 = html.match(/fb_dtsg(?:\""|\s*:\s*)""(Az:[^""]{20,60})""/);
                    out.push('regex_Az_dtsg=' + (m5 ? 'ADA len=' + m5[1].length : 'tidak'));
                    // pola umum mobile web: ""token"":""Az:...""  di dekat DTSG
                    const m6 = html.match(/""(Az:[A-Za-z0-9_-]{30,120})""/);
                    out.push('regex_Az_any=' + (m6 ? 'ADA len=' + m6[1].length + ' head=' + m6[1].slice(0, 12) : 'tidak'));
                    const Q = String.fromCharCode(34);
                    for (const k of ['dyn', 'csr', 'rev', 'hs', 'hsi', 'ccg', 'comet_req', 'spin_r', 'spin_t']) {
                        const re = new RegExp(Q + '__' + k + Q + ':""([A-Za-z0-9_.,%-]{1,300})');
                        const mm = html.match(re);
                        out.push('param___' + k + '=' + (mm ? 'ADA len=' + mm[1].length + ' head=' + mm[1].slice(0, 10) : 'tidak'));
                    }
                    const dre = new RegExp(Q + 'dpr' + Q + ':""([0-9.]+)');
                    out.push('dpr=' + (html.match(dre) ? 'ADA' : 'tidak'));
                    return out;
                }");
                foreach (var line in report ?? Array.Empty<string>()) Console.WriteLine("  " + line);
                if (page.Url.Contains("mbasic")) break;
            }
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
