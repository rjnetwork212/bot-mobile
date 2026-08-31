using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using BotMobile.Features;
using System;
using System.Linq;

namespace BotMobile.Views;

/// <summary>
/// Popup config generik: render field dari IBotFeature.ParamDefs,
/// simpan balik ke FeatureConfig.Params. Tambah fitur = popup otomatis.
/// </summary>
public partial class FeatureConfigWindow : Window
{
    readonly IBotFeature _feature;
    readonly FeatureConfig _cfg;
    public bool Confirmed { get; private set; }

    public FeatureConfigWindow()
    {
        InitializeComponent();
        _feature = null!;
        _cfg = new FeatureConfig();
    }

    public FeatureConfigWindow(IBotFeature feature, FeatureConfig cfg)
    {
        InitializeComponent();
        _feature = feature;
        _cfg = cfg;
        TxtTitle.Text = feature.Name;
        TxtDesc.Text = feature.Description;

        foreach (var (key, label, def) in feature.ParamDefs)
        {
            var value = cfg.Params.TryGetValue(key, out var v) && v.Length > 0 ? v : def;
            PanelParams.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 2) });
            var box = new TextBox { Text = value, Watermark = def, Tag = key, FontSize = 13 };
            PanelParams.Children.Add(box);
        }
    }

    void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (var box in PanelParams.Children.OfType<TextBox>())
        {
            var key = box.Tag?.ToString();
            if (key == null) continue;
            _cfg.Params[key] = box.Text?.Trim() ?? "";
        }
        Confirmed = true;
        Close();
    }

    void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
