using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace GerberPanelizer.App;

/// <summary>A compact click-to-select palette; arbitrary colours remain available through the HEX field.</summary>
public sealed class ColorPickerWindow : Window
{
    private static readonly string[] Palette =
    [
        "#1F2937", "#374151", "#6B7280", "#9CA3AF", "#D1D5DB", "#F3F4F6", "#FFFFFF", "#000000",
        "#7F1D1D", "#B91C1C", "#DC2626", "#EF4444", "#F87171", "#FCA5A5", "#FEE2E2", "#FB7185",
        "#7C2D12", "#C2410C", "#EA580C", "#F97316", "#FB923C", "#FDBA74", "#FFEDD5", "#F59E0B",
        "#713F12", "#A16207", "#CA8A04", "#EAB308", "#FACC15", "#FDE047", "#FEF9C3", "#84CC16",
        "#14532D", "#15803D", "#16A34A", "#22C55E", "#4ADE80", "#86EFAC", "#DCFCE7", "#10B981",
        "#064E3B", "#0F766E", "#0D9488", "#14B8A6", "#2DD4BF", "#5EEAD4", "#CCFBF1", "#06B6D4",
        "#0C4A6E", "#0369A1", "#0284C7", "#0EA5E9", "#38BDF8", "#7DD3FC", "#E0F2FE", "#3B82F6",
        "#312E81", "#4338CA", "#4F46E5", "#6366F1", "#818CF8", "#A5B4FC", "#E0E7FF", "#8B5CF6",
        "#581C87", "#7E22CE", "#9333EA", "#A855F7", "#C084FC", "#D8B4FE", "#F3E8FF", "#D946EF",
    ];

    public ColorPickerWindow(string currentHex)
    {
        SelectedHex = currentHex;
        Title = LocalizationService.Instance["ColorPickerHint"];
        Width = 440;
        Height = 440;
        MinWidth = 440;
        MinHeight = 440;
        MaxWidth = 440;
        MaxHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(246, 249, 248));

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance["ColorPickerHint"],
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(25, 62, 58)),
        });

        var palette = new UniformGrid { Columns = 8, Rows = 9 };
        foreach (var value in Palette)
        {
            var colour = (Color)ColorConverter.ConvertFromString(value)!;
            var button = new Button
            {
                Background = new SolidColorBrush(colour),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(2),
                ToolTip = value,
                Tag = value,
            };
            button.Click += SelectColor;
            palette.Children.Add(button);
        }
        Grid.SetRow(palette, 2);
        root.Children.Add(palette);

        var cancel = new Button { Content = LocalizationService.Instance["Cancel"], IsCancel = true, Padding = new Thickness(16, 7, 16, 7) };
        Grid.SetRow(cancel, 3);
        root.Children.Add(cancel);
        Content = root;
    }

    public string SelectedHex { get; private set; }

    private void SelectColor(object sender, RoutedEventArgs eventArgs)
    {
        SelectedHex = (string)((Button)sender).Tag;
        DialogResult = true;
    }
}
