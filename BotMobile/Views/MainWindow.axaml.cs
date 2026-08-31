using Avalonia.Controls;
using Avalonia.Interactivity;
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

    readonly AccountDb _db;
    readonly BotEngine _engine = new();
    readonly ObservableCollection<Account> _accounts = new();
    readonly ObservableCollection<FeatureRow> _featureRows = new();
    bool _busy;

    /// <summary>Row VM untuk list fitur.</summary>
    public class FeatureRow
    {
        public IBotFeature Feature { get; init; }
        public FeatureConfig Config { get; init; }
        public bool Enabled { get => Config.Enabled; set => Config.Enabled = value; }
        public string DisplayName => $"{Config.Order + 1}. {Feature.Name}";
        public string ParamSummary => string.Join(", ",
            Feature.ParamDefs.Select(p => $"{p.Key}={cfgGet(Config, p.Key, p.Def)}"));

        static string cfgGet(FeatureConfig c, string k, string d) =>
            c.Params.TryGetValue(k, out var v) && v.Length > 0 ? v : d;
    }

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(DataDir);
        _db = new AccountDb(Path.Combine(DataDir, "accounts.db"));
        GridAccounts.ItemsSource = _accounts;
        foreach (var a in _db.GetAll()) _accounts.Add(a);

        // fitur: load state, merge registry (fitur baru otomatis masuk; disabled default utk fitur mutasi)
        var saved = FeatureStateStore.Load();
        var savedIds = saved.Select(s => s.FeatureId).ToHashSet();
        int order = 0;
        foreach (var f in FeatureRegistry.All)
        {
            var cfg = saved.FirstOrDefault(s => s.FeatureId == f.Id);
            if (cfg == null)
            {
                cfg = new FeatureConfig { FeatureId = f.Id, Enabled = f.DefaultEnabled };
                // fitur baru (tidak ada di state lama): sisipkan sebelum fitur legacy post_status
                cfg.Order = f.Id == "post_status" ? int.MaxValue - 1 : order;
            }
            else cfg.Order = order;
            cfg.Params ??= new();
            _featureRows.Add(new FeatureRow { Feature = f, Config = cfg });
            order++;
        }
        // normalisasi order
        for (int k = 0; k < _featureRows.Count; k++) _featureRows[k].Config.Order = k;
        LstFeatures.ItemsSource = _featureRows;

        _engine.Log += line => Dispatcher.UIThread.Post(() => AppendLog(line));
        TxtChrome.Text = LoadChromePath();
        ResolveChromeInfo();
        UpdateInfo();
        LoadUidsLinks();
        Closed += (_, _) => _engine.Shutdown();
    }

    // ============ FITUR BOT tab ============

    FeatureRow? SelectedFeature() => LstFeatures.SelectedItem as FeatureRow;

    void OnFeatureToggle(object? sender, RoutedEventArgs e) => SaveFeatures();

    void OnFeatureUp(object? sender, RoutedEventArgs e) => MoveFeature(-1);
    void OnFeatureDown(object? sender, RoutedEventArgs e) => MoveFeature(1);

    void MoveFeature(int delta)
    {
        var row = SelectedFeature();
        if (row == null) { AppendLog("pilih fitur dulu"); return; }
        int i = _featureRows.IndexOf(row);
        int j = i + delta;
        if (j < 0 || j >= _featureRows.Count) return;
        _featureRows.Move(i, j);
        for (int k = 0; k < _featureRows.Count; k++) _featureRows[k].Config.Order = k;
        RefreshFeatureRows();
        SaveFeatures();
    }

    void RefreshFeatureRows()
    {
        // paksa re-render row (nomor urut berubah) dengan reload list
        var sel = LstFeatures.SelectedIndex;
        LstFeatures.ItemsSource = null;
        LstFeatures.ItemsSource = _featureRows;
        LstFeatures.SelectedIndex = sel;
    }

    void SaveFeatures()
    {
        FeatureStateStore.Save(_featureRows.Select(r => r.Config));
        AppendLog("urutan/status fitur disimpan");
    }

    async void OnFeatureConfig(object? sender, RoutedEventArgs e)
    {
        var row = SelectedFeature();
        if (row == null) { AppendLog("pilih fitur dulu"); return; }
        var win = new FeatureConfigWindow(row.Feature, row.Config);
        await win.ShowDialog(this);
        if (!win.Confirmed) return;
        SaveFeatures();
        RefreshFeatureRows();
    }

    // ============ DATA AKUN tab ============

    void UpdateInfo() =>
        TxtInfo.Text = $"{_accounts.Count} akun | {BotData.Context.Uids.Count} UID | {BotData.Context.Links.Count} link | DB: {Path.Combine(DataDir, "accounts.db")}";

    void OnAdd(object? sender, RoutedEventArgs e) => EditAccount(new Account());

    void OnEdit(object? sender, RoutedEventArgs e)
    {
        var acc = GridAccounts.SelectedItem as Account;
        if (acc == null) { AppendLog("pilih akun dulu"); return; }
        EditAccount(acc);
    }

    async void EditAccount(Account acc)
    {
        var isNew = _accounts.All(x => x.Uid != acc.Uid) && !_db.GetAll().Any(x => x.Uid == acc.Uid);
        var win = new EditAccountWindow(acc);
        await win.ShowDialog(this);
        if (!win.Confirmed) return;
        if (isNew && _accounts.Any(x => x.Uid == acc.Uid))
        {
            AppendLog($"uid {acc.Uid} sudah ada di tabel");
            return;
        }
        _db.Upsert(acc);
        if (isNew) _accounts.Add(acc);
        UpdateInfo();
    }

    void OnDelete(object? sender, RoutedEventArgs e)
    {
        var sel = GridAccounts.SelectedItems?.Cast<Account>().ToList() ?? new();
        if (sel.Count == 0) { AppendLog("pilih akun dulu"); return; }
        foreach (var a in sel)
        {
            _db.Delete(a.Uid);
            _accounts.Remove(a);
            AppendLog($"hapus {a.Uid}");
        }
        UpdateInfo();
    }

    async void OnBulkImport(object? sender, RoutedEventArgs e)
    {
        var text = TxtBulk.Text ?? "";
        if (text.Trim().Length == 0) { AppendLog("bulk kosong"); return; }
        var lines = text.Split('\n');
        var n = _db.ImportLines(lines);
        _accounts.Clear();
        foreach (var a in _db.GetAll()) _accounts.Add(a);
        TxtBulk.Text = "";
        AppendLog($"import bulk {n}/{lines.Length} baris OK (duplikat di-update)");
        UpdateInfo();
    }

    // ============ login / run ============

    async void OnLoginSelected(object? sender, RoutedEventArgs e) =>
        await RunBotAsync(GridAccounts.SelectedItems?.Cast<Account>().ToList() ?? new());

    async void OnLoginAll(object? sender, RoutedEventArgs e) => await RunBotAsync(_accounts.ToList());

    async void OnRunFeatureSelected(object? sender, RoutedEventArgs e)
    {
        var sel = GridAccounts.SelectedItems?.Cast<Account>().ToList() ?? new();
        if (sel.Count == 0) { AppendLog("pilih akun dulu"); return; }
        // run fitur = sama dengan login (login dulu, lalu fitur enabled)
        await RunBotAsync(sel);
    }

    async Task RunBotAsync(List<Account> targets)
    {
        if (_busy) { AppendLog("masih jalan, tunggu"); return; }
        if (targets.Count == 0) { AppendLog("tidak ada akun"); return; }

        string chrome;
        try { chrome = BotService.FindChrome(TxtChrome.Text); }
        catch (Exception ex) { AppendLog(ex.Message); return; }

        var order = _featureRows.Select(r => r.Config).ToList();
        var enabled = order.Where(f => f.Enabled).ToList();
        _busy = true;
        SetButtons(false);
        AppendLog($"=== run {targets.Count} akun, {enabled.Count} fitur aktif: {string.Join(" → ", enabled.Select(f => f.FeatureId))} ===");
        try
        {
            await _engine.RunAsync(targets, order, acc => { _db.Upsert(acc); return Task.CompletedTask; });
        }
        catch (Exception ex)
        {
            AppendLog($"fatal: {ex.Message}");
        }
        finally
        {
            _busy = false;
            SetButtons(true);
            AppendLog("=== selesai ===");
        }
    }

    void SetButtons(bool enabled)
    {
        foreach (var b in new Control?[] { BtnAdd, BtnEdit, BtnDelete, BtnLoginSel, BtnLoginAll, BtnRunFeat, BtnBulkImport })
            if (b != null) b.IsEnabled = enabled;
    }

    // ============ UID & LINK tabs ============

    static string UidsPath => Path.Combine(DataDir, "uids.txt");
    static string LinksPath => Path.Combine(DataDir, "links.txt");

    void LoadUidsLinks()
    {
        TxtUids.Text = File.Exists(UidsPath) ? File.ReadAllText(UidsPath) : "";
        TxtLinks.Text = File.Exists(LinksPath) ? File.ReadAllText(LinksPath) : "";
        SyncUidLinkCounts();
    }

    void SyncUidLinkCounts()
    {
        BotData.Context.Uids = LinesOf(TxtUids.Text);
        BotData.Context.Links = LinesOf(TxtLinks.Text);
        TxtUidCount.Text = $"{BotData.Context.Uids.Count} UID";
        TxtLinkCount.Text = $"{BotData.Context.Links.Count} link";
    }

    static List<string> LinesOf(string text) => text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(l => l.Length > 0).ToList();

    void OnSaveUids(object? sender, RoutedEventArgs e)
    {
        File.WriteAllText(UidsPath, TxtUids.Text ?? "");
        SyncUidLinkCounts();
        AppendLog($"UID disimpan ({BotData.Context.Uids.Count})");
        UpdateInfo();
    }

    void OnSaveLinks(object? sender, RoutedEventArgs e)
    {
        File.WriteAllText(LinksPath, TxtLinks.Text ?? "");
        SyncUidLinkCounts();
        AppendLog($"link disimpan ({BotData.Context.Links.Count})");
        UpdateInfo();
    }

    void OnUidsFromAccounts(object? sender, RoutedEventArgs e)
    {
        TxtUids.Text = string.Join("\n", _db.GetAll().Select(a => a.Uid));
        SyncUidLinkCounts();
        AppendLog("UID terisi dari data akun (belum disimpan — klik Simpan)");
    }

    async void OnOpenSelectedLink(object? sender, RoutedEventArgs e)
    {
        var link = BotData.Context.Links.FirstOrDefault();
        if (link == null) { AppendLog("tidak ada link"); return; }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link) { UseShellExecute = true });
            AppendLog($"buka link: {link}");
        }
        catch (Exception ex)
        {
            AppendLog($"buka link gagal: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    // ============ chrome path ============

    static string? LoadChromePath() =>
        File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath).Trim() : null;

    void ResolveChromeInfo()
    {
        try { TxtChromeInfo.Text = $"→ {BotService.FindChrome(TxtChrome.Text)}"; }
        catch (Exception ex) { TxtChromeInfo.Text = ex.Message; }
    }

    void OnSaveChrome(object? sender, RoutedEventArgs e)
    {
        File.WriteAllText(ConfigPath, TxtChrome.Text?.Trim() ?? "");
        ResolveChromeInfo();
        AppendLog("path chrome disimpan");
    }

    // ============ log ============

    void AppendLog(string line)
    {
        TxtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}";
        TxtLog.CaretIndex = TxtLog.Text?.Length ?? 0;
    }
}
