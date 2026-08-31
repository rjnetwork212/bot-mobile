using Avalonia;
using Avalonia.Controls;
using System.Linq;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.IO;

namespace BotMobile.Views.Pages;

/// <summary>Tab UID target (pool targets).</summary>
public class UidPage : UserControl
{
    private readonly MainWindow _main;
    private readonly TextBox _txt = new();
    private readonly TextBlock _count = new();

    static string UidsPath => Path.Combine(MainWindowDataDir, "uids.txt");
    internal static string MainWindowDataDir => System.IO.Path.Combine(AppContext.BaseDirectory, "data");

    public UidPage(MainWindow main)
    {
        _main = main;
        StyleDark(_txt);
        _txt.AcceptsReturn = true;
        _txt.Watermark = "61591234567890";
        _txt.FontFamily = "Consolas,monospace";
        _txt.FontSize = 12;
        _txt.Height = 420;
        _txt.Text = File.Exists(UidsPath) ? File.ReadAllText(UidsPath) : "";

        _count.FontSize = 11;
        _count.Foreground = new SolidColorBrush(Color.Parse("#849396"));
        SyncCount();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 10) };
        var save = MkBtn("Simpan", (_, _) => Save());
        save.Background = new SolidColorBrush(Color.Parse("#00E5FF"));
        save.Foreground = new SolidColorBrush(Color.Parse("#00363A"));
        save.FontWeight = FontWeight.SemiBold;
        toolbar.Children.Add(save);
        toolbar.Children.Add(MkBtn("Isi dari Data Akun", (_, _) =>
        {
            _txt.Text = string.Join("\n", _main.Accounts.ToList().Select(a => a.Uid));
            SyncCount();
            _main.AppendLog("UID terisi dari data akun (klik Simpan)");
        }));
        toolbar.Children.Add(MkBtn("Import ke Pool", (_, _) =>
        {
            _main.Pool.AddTargets(Lines(_txt.Text));
            _main.AppendLog($"pool targets diisi ({_main.Pool.Stats().TargetsFree} UID bebas)");
        }));
        toolbar.Children.Add(_count);

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(toolbar);
        stack.Children.Add(_txt);
        Content = stack;
    }

    private void Save()
    {
        File.WriteAllText(UidsPath, _txt.Text ?? "");
        _main.Pool.AddTargets(Lines(_txt.Text));
        SyncCount();
        _main.AppendLog($"UID disimpan ({Lines(_txt.Text).Count} baris, pool {_main.Pool.Stats().TargetsFree} bebas)");
    }

    private void SyncCount() => _count.Text = $"{Lines(_txt.Text).Count} UID";

    private static System.Collections.Generic.List<string> Lines(string? text) => (text ?? "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(l => l.Length > 0).ToList();

    internal static void StyleDark(TextBox box)
    {
        box.Background = new SolidColorBrush(Color.Parse("#0D1420"));
        box.Foreground = new SolidColorBrush(Color.Parse("#DFE2EF"));
        box.BorderBrush = new SolidColorBrush(Color.Parse("#1A2333"));
        box.CaretBrush = new SolidColorBrush(Color.Parse("#00E5FF"));
    }

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
