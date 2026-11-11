using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using GerberPanelizer.Core.Models;

namespace GerberPanelizer.App;

public sealed class LayerAssignmentWindow : Window
{
    public LayerAssignmentWindow(IEnumerable<ManufacturingFile> files)
    {
        Title = "Assign manufacturing layers";
        Width = 720;
        Height = 460;
        MinWidth = 520;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Items = new ObservableCollection<LayerAssignmentItem>(files.Select(file => new LayerAssignmentItem(file)));

        var root = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var confirm = new Button { Content = "Import", IsDefault = true, MinWidth = 90 };
        confirm.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var header = new TextBlock { Text = "The imported Gerber files have no recognised layer names. Select the manufacturing layer for each file.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn { Header = "File", Width = 440, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(LayerAssignmentItem.Name)) });
        var layerTemplate = new DataTemplate();
        var combo = new FrameworkElementFactory(typeof(ComboBox));
        combo.SetValue(ComboBox.ItemsSourceProperty, LayerAssignmentItem.AvailableLayers);
        combo.SetBinding(ComboBox.SelectedItemProperty, new System.Windows.Data.Binding(nameof(LayerAssignmentItem.Layer)) { Mode = System.Windows.Data.BindingMode.TwoWay });
        layerTemplate.VisualTree = combo;
        grid.Columns.Add(new GridViewColumn { Header = "Layer", Width = 205, CellTemplate = layerTemplate });
        root.Children.Add(new ListView { ItemsSource = Items, View = grid });
        Content = root;
    }

    public ObservableCollection<LayerAssignmentItem> Items { get; }
}

public sealed class LayerAssignmentItem : ObservableObject
{
    private ManufacturingLayerKind layer = ManufacturingLayerKind.CopperTop;
    public LayerAssignmentItem(ManufacturingFile file) => File = file;
    public static IReadOnlyList<ManufacturingLayerKind> AvailableLayers { get; } = [ManufacturingLayerKind.CopperTop, ManufacturingLayerKind.CopperBottom, ManufacturingLayerKind.Profile, ManufacturingLayerKind.Drill, ManufacturingLayerKind.SilkScreenTop, ManufacturingLayerKind.SilkScreenBottom, ManufacturingLayerKind.SolderMaskTop, ManufacturingLayerKind.SolderMaskBottom, ManufacturingLayerKind.PasteTop, ManufacturingLayerKind.PasteBottom, ManufacturingLayerKind.Unknown];
    public ManufacturingFile File { get; }
    public string Name => File.Name;
    public ManufacturingLayerKind Layer { get => layer; set => SetProperty(ref layer, value); }
}
