using PuppeteerSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Features.Selector;

/// <summary>
/// Helper metode Selector: klik elemen UI by label (multi-bahasa).
/// Port pola add_friend button-mode Bot_Ngekeng (reliable click chain).
/// </summary>
public static class UiSelector
{
    public static readonly string[] AddFriendLabels =
    {
        "tambahkan teman", "tambah teman", "add friend", "i-add", "agregar amigo",
        "ajouter comme ami", "als freund hinzufügen", "aggiungi amico", "친구 추가", "フレンド追加",
    };

    public static readonly string[] CancelRequestLabels =
    {
        "batalkan permintaan", "batalkan", "cancel request", "cancel", "pending",
        "menunggu", "requested", "permintaan terkirim",
    };

    public static readonly string[] ConfirmLabels =
    {
        "konfirmasi", "confirm", "terima", "accept",
    };

    public static readonly string[] DeclineLabels =
    {
        "hapus", "delete", "tolak", "decline",
    };

    // klik tombol pertama yang teksnya match labels (visible, bukan disabled)
    private const string ClickJs = """
        (labels) => {
          const norm = s => (s || '').trim().toLowerCase();
          const sel = '[role="button"], button, a[role="button"], div[role="button"], span[role="button"], a[role="link"], a';
          for (const el of document.querySelectorAll(sel)) {
            if (!el.offsetParent && getComputedStyle(el).position !== 'fixed') continue;
            const txt = norm(el.innerText || el.getAttribute('aria-label') || '');
            if (!txt) continue;
            if (labels.some(l => txt === l || txt.startsWith(l))) {
              const r = el.getBoundingClientRect();
              const opts = { bubbles: true, cancelable: true, view: window,
                             clientX: r.left + r.width / 2, clientY: r.top + r.height / 2 };
              el.scrollIntoView({ block: 'center' });
              el.dispatchEvent(new PointerEvent('pointerdown', opts));
              el.dispatchEvent(new MouseEvent('mousedown', opts));
              el.dispatchEvent(new PointerEvent('pointerup', opts));
              el.dispatchEvent(new MouseEvent('mouseup', opts));
              el.dispatchEvent(new MouseEvent('click', opts));
              return true;
            }
          }
          return false;
        }
        """;

    public static async Task<bool> ClickButtonByLabelsAsync(IPage page, string[] labels)
    {
        try
        {
            return await page.EvaluateFunctionAsync<bool>(ClickJs,
                labels.Select(l => l.ToLowerInvariant()).ToArray());
        }
        catch { return false; }
    }

    private const string HasJs = """
        (labels) => {
          const norm = s => (s || '').trim().toLowerCase();
          const sel = '[role="button"], button, a[role="button"], div[role="button"], span[role="button"], a[role="link"], a';
          for (const el of document.querySelectorAll(sel)) {
            if (!el.offsetParent) continue;
            const txt = norm(el.innerText || el.getAttribute('aria-label') || '');
            if (txt && labels.some(l => txt === l || txt.startsWith(l))) return true;
          }
          return false;
        }
        """;

    public static async Task<bool> HasLabelAsync(IPage page, string[] labels)
    {
        try
        {
            return await page.EvaluateFunctionAsync<bool>(HasJs,
                labels.Select(l => l.ToLowerInvariant()).ToArray());
        }
        catch { return false; }
    }

    public static async Task<bool> GoToAsync(IPage page, string url, int timeoutMs = 30000)
    {
        try
        {
            await page.GoToAsync(url, new NavigationOptions
            {
                Timeout = timeoutMs,
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
            });
        }
        catch (NavigationException) { /* redirect abort — lanjut */ }
        await Task.Delay(3000);
        return true;
    }
}
