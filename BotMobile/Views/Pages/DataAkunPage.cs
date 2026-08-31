using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BotMobile.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotMobile.Views.Pages;

/// <summary>Tab Data Akun: tabel akun + bulk import + aksi per seleksi.</summary>
public class DataAkunPage : UserControl
{
    private readonly MainWindow _main;
    private DataGrid _grid = new();
    private TextBox _bulk = new();

    public static IReadOnlyList<Account> LastSelection { get; private set; } = Array.Empty<Account>();

    public DataAkunPage(MainWindow main)
    {
        _main = main;

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            SelectionMode = DataGridSelectionMode.Extended,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = main.Accounts,
            Background = new SolidColorBrush(Color.Parse("#101827")),
            RowBackground = new SolidColorBrush(Color.Parse("#101827")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1A2333")),
            Foreground = new SolidColorBrush(Color.Parse("#DFE2EF")),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.Parse("#1A2333")),
            MinHeight = 240,
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "UID", Binding = new Avalonia.Data.Binding("Uid"), Width = new DataGridLength(140), IsReadOnly = true });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Pass", Binding = new Avalonia.Data.Binding("MaskedPassword"), Width = new DataGridLength(70), IsReadOnly = true });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cookie", Binding = new Avalonia.Data.Binding("CookieInfo"), Width = new DataGridLength(60), IsReadOnly = true });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Email", Binding = new Avalonia.Data.Binding("EmailInfo"), Width = new DataGridLength(55), IsReadOnly = true });
        _grid.Columns.Add(new DataGridTextColumn { Header = "2FA", Binding = new Avalonia.Data.Binding("TfaInfo"), Width = new DataGridLength(45), IsReadOnly = true });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Avalonia.Data.Binding("Status"), Width = new DataGridLength(110), IsReadOnly = true });
        _grid.Columns.Add(new DataGridTextColumn { Header = "LastLogin", Binding = new Avalonia.Data.Binding("LastLogin"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true });
        _grid.SelectionChanged += (_, _) =>
        { LastSelection = _grid.SelectedItems?.Cast<Account>().ToList() ?? (IReadOnlyList<Account>)Array.Empty<Account>(); };

        _bulk = new TextBox
        {
            AcceptsReturn = true,
            Watermark = "uid|pass | uid|pass|cookies | uid|pass|cookies|secret2fa | uid|pass|email|cookies  (1 akun per baris)",
            Height = 130,
            FontFamily = "Consolas,monospace",
            FontSize = 11.5,
            Background = new SolidColorBrush(Color.Parse("#0D1420")),
            Foreground = new SolidColorBrush(Color.Parse("#DFE2EF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1A2333")),
        };

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 10) };
        toolbar.Children.Add(MkBtn("＋ Tambah", (_, _) => EditAccount(new Account())));
        toolbar.Children.Add(MkBtn("✎ Edit", (_, _) => EditSelected()));
        toolbar.Children.Add(MkBtn("✕ Hapus", (_, _) => DeleteSelected(), "#FF4D67"));
        toolbar.Children.Add(new Separator { Width = 10, Margin = new Thickness(4, 0) });
        toolbar.Children.Add(MkBtn("▶ Login Selected", (_, _) => RunSelected()));
        toolbar.Children.Add(MkBtn("▶ Run Semua Akun", (_, _) => RunAll()));

        var importBtn = MkBtn("⇪ Import Bulk", (_, _) => DoImport());
        importBtn.Background = new SolidColorBrush(Color.Parse("#00E5FF"));
        importBtn.Foreground = new SolidColorBrush(Color.Parse("#00363A"));
        importBtn.FontWeight = FontWeight.SemiBold;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
        root.Children.Add(toolbar);
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        var hint = new TextBlock
        {
            Text = "Import akun (bulk, 1 akun per baris)",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#849396")),
            Margin = new Thickness(0, 12, 0, 4),
        };
        Grid.SetRow(hint, 2);
        root.Children.Add(hint);

        Grid.SetRow(_bulk, 3);
        root.Children.Add(_bulk);

        var importRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        importRow.Children.Add(importBtn);
        Grid.SetRow(importRow, 4);
        root.Children.Add(importRow);

        Content = root;
    }

    private static Button MkBtn(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick, string? color = null)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 6),
            FontSize = 12.5,
            Background = new SolidColorBrush(Color.Parse(color ?? "#162238")),
            Foreground = new SolidColorBrush(Color.Parse(color != null ? "#0A0E17" : "#DFE2EF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#26334D")),
            Height = 32,
        };
        b.Click += onClick;
        return b;
    }

    private void EditSelected()
    {
        var acc = _grid.SelectedItem as Account;
        if (acc == null) { _main.AppendLog("pilih akun dulu"); return; }
        EditAccount(acc);
    }

    private async void EditAccount(Account acc)
    {
        var isNew = _main.Accounts.All(x => x.Uid != acc.Uid);
        var win = new EditAccountWindow(acc);
        await win.ShowDialog(_main);
        if (!win.Confirmed) return;
        if (isNew && _main.Accounts.Any(x => x.Uid == acc.Uid))
        {
            _main.AppendLog($"uid {acc.Uid} sudah ada");
            return;
        }
        _main.Db.Upsert(acc);
        if (isNew) _main.Accounts.Add(acc);
    }

    private void DeleteSelected()
    {
        var sel = _grid.SelectedItems?.Cast<Account>().ToList() ?? new List<Account>();
        if (sel.Count == 0) { _main.AppendLog("pilih akun dulu"); return; }
        foreach (var a in sel)
        {
            _main.Db.Delete(a.Uid);
            _main.Accounts.Remove(a);
            _main.AppendLog($"hapus {a.Uid}");
        }
    }

    private void DoImport()
    {
        var text = _bulk.Text ?? "";
        if (text.Trim().Length == 0) { _main.AppendLog("bulk kosong"); return; }
        var lines = text.Split('\n');
        var n = _main.Db.ImportLines(lines);
        _main.Accounts.Clear();
        foreach (var a in _main.Db.GetAll()) _main.Accounts.Add(a);
        _bulk.Text = "";
        _main.AppendLog($"import bulk {n}/{lines.Length} baris OK (duplikat di-update)");
    }

    private void RunSelected() =>
        _ = _main.RunBotAsync(_grid.SelectedItems?.Cast<Account>().ToList() ?? new List<Account>());

    private void RunAll() => _ = _main.RunBotAsync(_main.Accounts.ToList());
}
