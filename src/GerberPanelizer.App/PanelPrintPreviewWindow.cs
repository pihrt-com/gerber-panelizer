using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GerberPanelizer.Core.Panelization;

namespace GerberPanelizer.App;

/// <summary>
/// A print-only preview. It never prints the main application window: the printer receives a new
/// visual containing just the panel and its manufacturing layers.
/// </summary>
public sealed class PanelPrintPreviewWindow : Window
{
    private const double DipPerMillimetre = 96d / 25.4d;
    private readonly IReadOnlyList<BoardPlacement> placements;
    private readonly IReadOnlyList<LayerItemViewModel> layers;
    private readonly double panelWidthMm;
    private readonly double panelHeightMm;
    private readonly ComboBox scaleSelector;
    private readonly Border previewSurface;

    public PanelPrintPreviewWindow(
        Window owner,
        IEnumerable<BoardPlacement> placements,
        IEnumerable<LayerItemViewModel> layers,
        double panelWidthMm,
        double panelHeightMm)
    {
        Owner = owner;
        this.placements = placements.ToList();
        this.layers = layers.ToList();
        this.panelWidthMm = panelWidthMm;
        this.panelHeightMm = panelHeightMm;

        Title = T("PrintPreview");
        Width = 1040;
        Height = 760;
        MinWidth = 720;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(246, 249, 248));

        scaleSelector = new ComboBox
        {
            Width = 160,
            Margin = new Thickness(12, 0, 0, 0),
            ItemsSource = new[] { T("FitToPage"), T("ActualSize"), "75 %", "50 %" },
            SelectedIndex = 0,
        };

        previewSurface = CreatePanelSurface(ScaleForPreview());
        var previewHost = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            Margin = new Thickness(30),
            Child = previewSurface,
        };

        var printButton = new Button
        {
            Content = T("PrintNow"),
            Background = new SolidColorBrush(Color.FromRgb(11, 118, 110)),
            Foreground = Brushes.White,
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(0, 0, 8, 0),
        };
        printButton.Click += (_, _) => Print();
        var closeButton = new Button { Content = T("Close"), Padding = new Thickness(18, 8, 18, 8) };
        closeButton.Click += (_, _) => Close();
        scaleSelector.SelectionChanged += (_, _) => ReplacePreviewSurface(previewHost);

        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(24, 18, 24, 12) };
        var title = new StackPanel { Orientation = Orientation.Vertical };
        title.Children.Add(new TextBlock { Text = T("PanelPrintPreview"), FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(36, 69, 66)) });
        title.Children.Add(new TextBlock { Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, T("PrintPreviewDescription"), panelWidthMm, panelHeightMm, this.placements.Count), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(90, 107, 112)), Margin = new Thickness(0, 4, 0, 0) });
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        var scalePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        scalePanel.Children.Add(new TextBlock { Text = T("Scale"), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(36, 69, 66)) });
        scalePanel.Children.Add(scaleSelector);
        DockPanel.SetDock(scalePanel, Dock.Right);
        header.Children.Add(scalePanel);

        var actionBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 12, 24, 18) };
        actionBar.Children.Add(printButton);
        actionBar.Children.Add(closeButton);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(header, 0);
        Grid.SetRow(previewHost, 1);
        Grid.SetRow(actionBar, 2);
        layout.Children.Add(header);
        layout.Children.Add(previewHost);
        layout.Children.Add(actionBar);
        Content = layout;
    }

    private static string T(string key) => LocalizationService.Instance[key];

    private void ReplacePreviewSurface(Viewbox previewHost)
    {
        previewHost.Child = CreatePanelSurface(ScaleForPreview());
    }

    private double ScaleForPreview() => scaleSelector.SelectedIndex switch
    {
        1 => 1,
        2 => .75,
        3 => .5,
        _ => 1,
    };

    private Border CreatePanelSurface(double scale)
    {
        var panelControl = new PanelPreviewControl
        {
            Width = panelWidthMm * DipPerMillimetre * scale,
            Height = panelHeightMm * DipPerMillimetre * scale,
            Placements = placements,
            Layers = layers,
            PanelWidthMm = panelWidthMm,
            PanelHeightMm = panelHeightMm,
            IsHitTestVisible = false,
        };

        return new Border
        {
            Width = panelControl.Width + 4,
            Height = panelControl.Height + 4,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(11, 118, 110)),
            BorderThickness = new Thickness(2),
            Child = panelControl,
        };
    }

    private void Print()
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var requestedScale = ScaleForPreview();
        var naturalWidth = panelWidthMm * DipPerMillimetre;
        var naturalHeight = panelHeightMm * DipPerMillimetre;
        var scale = requestedScale;
        if (scaleSelector.SelectedIndex == 0)
        {
            scale = Math.Min((dialog.PrintableAreaWidth - 36) / naturalWidth, (dialog.PrintableAreaHeight - 36) / naturalHeight);
        }
        if (scale <= 0) return;

        var panel = CreatePanelSurface(scale);
        panel.Measure(new Size(panel.Width, panel.Height));
        panel.Arrange(new Rect(new Point(), new Size(panel.Width, panel.Height)));
        panel.UpdateLayout();

        var page = new FixedPage { Width = dialog.PrintableAreaWidth, Height = dialog.PrintableAreaHeight, Background = Brushes.White };
        FixedPage.SetLeft(panel, Math.Max(18, (dialog.PrintableAreaWidth - panel.Width) / 2));
        FixedPage.SetTop(panel, Math.Max(18, (dialog.PrintableAreaHeight - panel.Height) / 2));
        page.Children.Add(panel);
        page.Measure(new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight));
        page.Arrange(new Rect(new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight)));
        dialog.PrintVisual(page, T("PanelPrintJob"));
    }
}
