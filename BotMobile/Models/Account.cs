using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BotMobile.Models;

public class Account : INotifyPropertyChanged
{
    string _uid = "", _password = "", _cookies = "", _email = "", _secret2Fa = "", _status = "NotLogged", _lastLogin = "";

    public string Uid { get => _uid; set { _uid = value; Raise(); } }
    public string Password { get => _password; set { _password = value; Raise(); Raise(nameof(MaskedPassword)); } }
    public string Cookies { get => _cookies; set { _cookies = value; Raise(); Raise(nameof(CookieInfo)); } }
    public string Email { get => _email; set { _email = value; Raise(); Raise(nameof(EmailInfo)); } }
    public string Secret2Fa { get => _secret2Fa; set { _secret2Fa = value; Raise(); Raise(nameof(TfaInfo)); } }
    public string Status { get => _status; set { _status = value; Raise(); } }
    public string LastLogin { get => _lastLogin; set { _lastLogin = value; Raise(); } }

    public string MaskedPassword => string.IsNullOrEmpty(_password) ? "" : "••••••";
    public string CookieInfo => string.IsNullOrWhiteSpace(_cookies) ? "" : "ada";
    public string EmailInfo => string.IsNullOrWhiteSpace(_email) ? "" : "ada";
    public string TfaInfo => string.IsNullOrWhiteSpace(_secret2Fa) ? "" : "ada";

    // ponytail: model referensi Avalonia biar INPC dari worker thread aman; upgrade = MVVM+marshal terpisah
    void Raise([CallerMemberName] string? name = null)
    {
        if (PropertyChanged == null) return;
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
