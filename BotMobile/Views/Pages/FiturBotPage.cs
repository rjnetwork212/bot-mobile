using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using BotMobile.Features;
using System;
using System.Linq;

namespace BotMobile.Views.Pages;

/// <summary>Tab Fitur Bot: card list fitur (toggle, metode, config, reorder).</summary>
public class FiturBotPage : UserControl
{
    private readonly MainWindow _main;
    private StackPanel _list = new();

    public FiturBotPage(MainWindow main)
    {
        _main = main;
        Build();
    }

    private void Build()
    {
        var root = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _list,
        };
        Rebuild();
        Content = root;
    }

    private void Rebuild()
    {
        _list = new StackPanel { Spacing = 10 };
        foreach (var row in _main.FeatureRows)
        {
            _list.Children.Add(BuildCard(row));
        }
    }

    private Border BuildCard(MainWindow.FeatureRow row)
    {
        var enabled = row.Enabled;

        // kiri: nama + deskripsi + badge metode
        var modeColor = row.Mode == FeatureModes.Selector ? "#FFB020" : "#00E5FF";
        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = row.OrderNo, FontFamily = "Consolas,monospace", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#849396")), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = row.DisplayName, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse(row.Enabled ? "#DFE2EF" : "#5A6668")), VerticalAlignment = VerticalAlignment.Center },
            },
        });
        left.Children.Add(new TextBlock { Text = row.Description, FontSize = 11.5, Foreground = new SolidColorBrush(Color.Parse("#849396") ), TextWrapping = TextWrapping.Wrap, MaxWidth = 560 });
        left.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse(modeColor + "1A")),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(7, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = row.ModeBadge, FontSize = 9.5, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse(modeColor)) },
        });

        // kanan: metode combo (jika >1), toggle, gear, up, down
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (row.Feature.Modes.Length > 1)
        {
            var combo = new ComboBox
            {
                ItemsSource = row.Feature.Modes,
                SelectedItem = row.Mode,
                Width = 110,
                FontSize = 11.5,
                Height = 28,
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string m && m != row.Mode)
                {
                    row.Config.Params["Metode"] = m;
                    _main.SaveFeatures();
                    Rebuild();
                }
            };
            right.Children.Add(combo);
        }
        else
        {
            right.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#162238")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(7, 2),
                Child = new TextBlock { Text = row.ModeBadge, FontSize = 9.5, Foreground = new SolidColorBrush(Color.Parse("#849396")) },
            });
        }

        var toggle = new ToggleSwitch
        {
            IsChecked = enabled,
            OnContent = "",
            OffContent = "",
            MinWidth = 44,
            Height = 22,
            Padding = new Thickness(0),
        };
        toggle.IsCheckedChanged += (_, _) =>
        {
            row.Config.Enabled = toggle.IsChecked == true;
            _main.SaveFeatures();
            Rebuild();
        };
        right.Children.Add(toggle);

        var gear = new Button { Content = "⚙", Width = 30, Height = 28, FontSize = 13 };
        gear.Click += async (_, _) => await ConfigFeature(row);
        right.Children.Add(gear);

        var up = new Button { Content = "▲", Width = 30, Height = 28, FontSize = 11 };
        up.Click += (_, _) => Move(row, -1);
        right.Children.Add(up);

        var down = new Button { Content = "▼", Width = 30, Height = 28, FontSize = 11 };
        down.Click += (_, _) => Move(row, 1);
        right.Children.Add(down);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#101827")),
            BorderBrush = new SolidColorBrush(Color.Parse(enabled ? "#00E5FF40" : "#1A2333")),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new Border { Child = left },
                    right,
                },
            },
        };
    }

    private void Move(MainWindow.FeatureRow row, int delta)
    {
        var rows = _main.FeatureRows;
        int i = rows.IndexOf(row);
        int j = i + delta;
        if (j < 0 || j >= rows.Count) return;
        rows.Move(i, j);
        _main.RefreshFeatureList();
        _main.SaveFeatures();
        Rebuild();
    }

    private async System.Threading.Tasks.Task ConfigFeature(MainWindow.FeatureRow row)
    {
        var win = new FeatureConfigWindow(row.Feature, row.Config);
        await win.ShowDialog(_main);
        if (win.Confirmed)
        {
            _main.SaveFeatures();
            Rebuild();
        }
    }
}
