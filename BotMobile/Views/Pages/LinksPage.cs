using Avalonia;
using Avalonia.Controls;
using System.Linq;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Views.Pages;

/// <summary>Tab Link target (pool links).</summary>
public class LinksPage : UserControl
{
    private readonly MainWindow _main;
    private readonly TextBox _txt = new();
    private readonly TextBlock _count = new();

    static string LinksPath => Path.Combine(UidPage.MainWindowDataDir, "links.txt");

    public LinksPage(MainWindow main)
    {
        _main = main;
        UidPage.StyleDark(_txt);
        _txt.AcceptsReturn = true;
        _txt.Watermark = "https://m.facebook.com/...";
        _txt.FontFamily = "Consolas,monospace";
        _txt.FontSize = 12;
        _txt.Height = 420;
        _txt.Text = File.Exists(LinksPath) ? File.ReadAllText(LinksPath) : "";

        _count.FontSize = 11;
        _count.Foreground = new SolidColorBrush(Color.Parse("#849396"));
        SyncCount();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 10) };
        var save = MkBtn("Simpan", (_, _) => Save());
        save.Background = new SolidColorBrush(Color.Parse("#00E5FF"));
        save.Foreground = new SolidColorBrush(Color.Parse("#00363A"));
        save.FontWeight = FontWeight.SemiBold;
        toolbar.Children.Add(save);
        toolbar.Children.Add(MkBtn("Import ke Pool", (_, _) =>
        {
            _main.Pool.AddLinks(Lines(_txt.Text));
            _main.AppendLog($"pool links diisi ({_main.Pool.Stats().LinksFree} link bebas)");
        }));
        toolbar.Children.Add(MkBtn("Buka Link Pertama (test)", (_, _) =>
        {
            var link = Lines(_txt.Text).FirstOrDefault();
            if (link == null) { _main.AppendLog("tidak ada link"); return; }
            try
            {
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                _main.AppendLog($"buka: {link}");
            }
            catch (Exception ex) { _main.AppendLog($"buka gagal: {ex.Message}"); }
        }));
        toolbar.Children.Add(_count);

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(toolbar);
        stack.Children.Add(_txt);
        Content = stack;
    }

    private void Save()
    {
        File.WriteAllText(LinksPath, _txt.Text ?? "");
        _main.Pool.AddLinks(Lines(_txt.Text));
        SyncCount();
        _main.AppendLog($"link disimpan ({Lines(_txt.Text).Count} baris, pool {_main.Pool.Stats().LinksFree} bebas)");
    }

    private void SyncCount() => _count.Text = $"{Lines(_txt.Text).Count} link";

    private static System.Collections.Generic.List<string> Lines(string? text) => (text ?? "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(l => l.Length > 0).ToList();

    private static Button MkBtn(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 6),
            FontSize = 12.5,
            Background = new SolidColorBrush(Color.Parse("#162238")),
            Foreground = new SolidColorBrush(Color.Parse("#DFE2EF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#26334D")),
            Height = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        b.Click += onClick;
        return b;
    }
}
