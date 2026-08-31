using Avalonia.Controls;
using BotMobile.Models;
using System.Linq;

namespace BotMobile.Views;

public partial class EditAccountWindow : Window
{
    readonly Account _acc;
    public bool Confirmed { get; private set; }

    public EditAccountWindow(Account acc)
    {
        InitializeComponent();
        _acc = acc;
        TxtUid.Text = acc.Uid;
        TxtPassword.Text = acc.Password;
        TxtEmail.Text = acc.Email;
        Txt2Fa.Text = acc.Secret2Fa;
        TxtCookies.Text = acc.Cookies;
        UpdateCookieInfo();
        Loaded += (_, _) => TxtUid.Focus();
    }

    void OnCookiesChanged(object? sender, TextChangedEventArgs e) => UpdateCookieInfo();

    void UpdateCookieInfo()
    {
        var raw = TxtCookies.Text ?? "";
        var names = raw.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=').First().Trim())
            .Where(n => n.Length > 0)
            .ToList();
        var hasLogin = names.Contains("c_user") && names.Contains("xs");
        TxtCookieInfo.Text = names.Count == 0
            ? "kosong — login akan pakai UID + password"
            : $"{names.Count} cookie {(hasLogin ? "• c_user+xs ada (login session)" : "• c_user/xs belum ada")}";
    }

    void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var uid = TxtUid.Text?.Trim() ?? "";
        if (uid.Length == 0) return;
        _acc.Uid = uid;
        _acc.Password = TxtPassword.Text?.Trim() ?? "";
        _acc.Email = TxtEmail.Text?.Trim() ?? "";
        _acc.Secret2Fa = Txt2Fa.Text?.Trim() ?? "";
        _acc.Cookies = TxtCookies.Text?.Trim() ?? "";
        Confirmed = true;
        Close();
    }

    void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
