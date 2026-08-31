using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia;
using Avalonia.Threading;
using BotMobile.Features;
using BotMobile.Models;
using BotMobile.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotMobile.Views;

public partial class MainWindow : Window
{
    static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");
    static string ConfigPath => Path.Combine(DataDir, "config.txt");

    // shared services (dipakai pages)
    public AccountDb Db { get; }
    public PoolDb Pool { get; }
    public BotEngine Engine { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<FeatureRow> FeatureRows { get; } = new();

    private readonly Dictionary<string, Control> _pages = new();
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(DataDir);
        Db = new AccountDb(Path.Combine(DataDir, "accounts.db"));
        Pool = new PoolDb(Path.Combine(DataDir, "accounts.db"));
        foreach (var a in Db.GetAll()) Accounts.Add(a);

        LoadFeatures();
        Engine.Log += line => Dispatcher.UIThread.Post(() => AppendLog(line));

        TxtChrome.Text = LoadChromePath();
        ResolveChromeInfo();

        _pages["fitur"] = new Views.Pages.FiturBotPage(this);
        _pages["akun"] = new Views.Pages.DataAkunPage(this);
        _pages["uid"] = new Views.Pages.UidPage(this);
        _pages["link"] = new Views.Pages.LinksPage(this);
        Navigate("fitur");

        UpdateInfo();
        Closed += (_, _) => Engine.Shutdown();
    }

    // ============ fitur ============

    public class FeatureRow
    {
        public IBotFeature Feature { get; init; }
        public FeatureConfig Config { get; init; }
        public bool Enabled { get => Config.Enabled; set => Config.Enabled = value; }
        public string DisplayName => Feature.Name;
        public string Description => Feature.Description;
        public string OrderNo => (Config.Order + 1).ToString("00");
        public string Mode => Config.Params.TryGetValue("Metode", out var m) && m.Length > 0
            ? m : Feature.DefaultMode;
        public string ModeBadge => Mode.ToUpperInvariant();
        public string ParamSummary => string.Join("  ·  ",
            Feature.ParamDefs.Select(p => $"{p.Key}={cfgGet(Config, p.Key, p.Def)}"));

        static string cfgGet(FeatureConfig c, string k, string d) =>
            c.Params.TryGetValue(k, out var v) && v.Length > 0 ? v : d;
    }

    private void LoadFeatures()
    {
        var saved = FeatureStateStore.Load();
        int order = 0;
        foreach (var f in FeatureRegistry.All)
        {
            var cfg = saved.FirstOrDefault(s => s.FeatureId == f.Id);
            if (cfg == null)
            {
                cfg = new FeatureConfig { FeatureId = f.Id, Enabled = f.DefaultEnabled };
                cfg.Order = f.Id == "post_status" ? int.MaxValue - 1 : order;
            }
            else cfg.Order = order;
            cfg.Params ??= new();
            if (!cfg.Params.ContainsKey("Metode") && f.Modes.Length > 1)
                cfg.Params["Metode"] = f.DefaultMode;
            FeatureRows.Add(new FeatureRow { Feature = f, Config = cfg });
            order++;
        }
        for (int k = 0; k < FeatureRows.Count; k++) FeatureRows[k].Config.Order = k;
    }

    public void SaveFeatures()
    {
        FeatureStateStore.Save(FeatureRows.Select(r => r.Config));
        AppendLog("urutan/metode/fitur disimpan");
    }

    public void RefreshFeatureList()
    {
        foreach (var row in FeatureRows) row.Config.Order = FeatureRows.IndexOf(row);
    }

    // ============ navigasi ============

    private void Navigate(string key)
    {
        if (!_pages.TryGetValue(key, out var page)) return;
        PageHost.Content = page;
        (TxtPageTitle.Text, TxtPageSub.Text) = key switch
        {
            "fitur" => ("Fitur Bot", "Urutan & metode eksekusi per akun"),
            "akun" => ("Data Akun", "Import bulk & status akun"),
            "uid" => ("UID Target", "UID yang ditambah teman (dipakai fitur Add Friend)"),
            "link" => ("Link", "URL target (dipakai fitur Post Status)"),
            _ => ("", ""),
        };
        foreach (var b in new[] { NavFitur, NavAkun, NavUid, NavLink })
        {
            var active = (string)b!.Tag! == key;
            b.Background = active ? new SolidColorBrush(Color.Parse("#162238")) : Brushes.Transparent;
            b.BorderThickness = new Thickness(3, 0, 0, 0);
            b.BorderBrush = active ? (IBrush)Resources["Cyan"]! : Brushes.Transparent;
            b.Foreground = active ? (IBrush)Resources["Cyan"]! : (IBrush)Resources["TextDim"]!;
        }
    }

    private void OnNav(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string key) Navigate(key);
    }

    // ============ run bot ============

    public async Task RunBotAsync(System.Collections.Generic.List<Account> targets)
    {
        if (_busy) { AppendLog("masih jalan, tunggu"); return; }
        if (targets.Count == 0) { AppendLog("tidak ada akun target"); return; }

        string chrome;
        try { chrome = BotService.FindChrome(TxtChrome.Text); }
        catch (Exception ex) { AppendLog(ex.Message); return; }

        var order = FeatureRows.Select(r => r.Config).ToList();
        _busy = true;
        BtnRun.IsEnabled = false;
        AppendLog($"=== run {targets.Count} akun, fitur aktif: {string.Join(" → ", order.Where(f => f.Enabled).Select(f => f.FeatureId))} ===");
        try
        {
            await Engine.RunAsync(targets, order, acc => { Db.Upsert(acc); return Task.CompletedTask; });
        }
        catch (Exception ex)
        {
            AppendLog($"fatal: {ex.Message}");
        }
        finally
        {
            _busy = false;
            BtnRun.IsEnabled = true;
            AppendLog("=== selesai ===");
        }
    }

    private async void OnRunBot(object? sender, RoutedEventArgs e)
    {
        if (Views.Pages.DataAkunPage.LastSelection.Count > 0)
            await RunBotAsync(Views.Pages.DataAkunPage.LastSelection.ToList());
        else
            await RunBotAsync(Accounts.ToList());
    }

    public void SetButtonsEnabled(bool enabled) => BtnRun.IsEnabled = enabled && !_busy;

    // ============ chrome ============

    static string? LoadChromePath() =>
        File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath).Trim() : null;

    private void ResolveChromeInfo()
    {
        try
        {
            var p = BotService.FindChrome(TxtChrome.Text);
            TxtChromeChip.Text = $"Chrome: {Path.GetFileName(p)}";
            TxtChromeChip.Foreground = (IBrush)Resources["Green"]!;
        }
        catch (Exception ex)
        {
            TxtChromeChip.Text = ex.Message.Split('\n')[0];
            TxtChromeChip.Foreground = (IBrush)Resources["Red"]!;
        }
    }

    private void OnSaveChrome(object? sender, RoutedEventArgs e)
    {
        File.WriteAllText(ConfigPath, TxtChrome.Text?.Trim() ?? "");
        ResolveChromeInfo();
        AppendLog("path chrome disimpan");
    }

    // ============ log ============

    public void AppendLog(string line)
    {
        TxtLog.Text += $"{DateTime.Now:HH:mm:ss}  {line}{Environment.NewLine}";
        TxtLog.CaretIndex = TxtLog.Text?.Length ?? 0;
        TxtInfo.Text = $"{Accounts.Count} akun · {Pool.Stats().TargetsFree} UID bebas · {Pool.Stats().LinksFree} link bebas";
    }

    private void UpdateInfo() => AppendLog("Bot Mobile siap.");
}
