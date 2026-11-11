using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;
using GerberPanelizer.Core.Panelization;
using GerberPanelizer.Core.Parsing;

namespace GerberPanelizer.App;

public sealed class PanelPreviewControl : FrameworkElement
{
    public static readonly DependencyProperty PlacementsProperty = DependencyProperty.Register(nameof(Placements), typeof(IEnumerable<BoardPlacement>), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPlacementsChanged));
    public static readonly DependencyProperty LayersProperty = DependencyProperty.Register(nameof(Layers), typeof(IEnumerable<LayerItemViewModel>), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(null, OnLayersChanged));
    public static readonly DependencyProperty PanelWidthMmProperty = RegisterStaticVisualProperty(nameof(PanelWidthMm), 200d);
    public static readonly DependencyProperty PanelHeightMmProperty = RegisterStaticVisualProperty(nameof(PanelHeightMm), 130d);
    public static readonly DependencyProperty WorkspaceWidthMmProperty = RegisterStaticVisualProperty(nameof(WorkspaceWidthMm), 200d);
    public static readonly DependencyProperty WorkspaceHeightMmProperty = RegisterStaticVisualProperty(nameof(WorkspaceHeightMm), 130d);
    public static readonly DependencyProperty StagingAreaLabelProperty = RegisterStaticVisualProperty(nameof(StagingAreaLabel), string.Empty);
    public static readonly DependencyProperty MachinePositionXMillimetresProperty = DependencyProperty.Register(nameof(MachinePositionXMillimetres), typeof(double), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MachinePositionYMillimetresProperty = DependencyProperty.Register(nameof(MachinePositionYMillimetres), typeof(double), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowMachinePositionProperty = DependencyProperty.Register(nameof(ShowMachinePosition), typeof(bool), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MachinePositionLabelProperty = DependencyProperty.Register(nameof(MachinePositionLabel), typeof(string), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MachinePositionColorProperty = DependencyProperty.Register(nameof(MachinePositionColor), typeof(string), typeof(PanelPreviewControl), new FrameworkPropertyMetadata("#B42318", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MachinePathProperty = DependencyProperty.Register(nameof(MachinePath), typeof(IEnumerable<PcbPoint>), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnMachinePathChanged));
    public static readonly DependencyProperty MovePlacementCommandProperty = DependencyProperty.Register(nameof(MovePlacementCommand), typeof(ICommand), typeof(PanelPreviewControl));
    public static readonly DependencyProperty DeletePlacementCommandProperty = DependencyProperty.Register(nameof(DeletePlacementCommand), typeof(ICommand), typeof(PanelPreviewControl));
    public static readonly DependencyProperty SelectedPlacementProperty = DependencyProperty.Register(nameof(SelectedPlacement), typeof(BoardPlacement), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnStaticVisualPropertyChanged));
    public static readonly DependencyProperty BoardContextNameLabelProperty = DependencyProperty.Register(nameof(BoardContextNameLabel), typeof(string), typeof(PanelPreviewControl), new PropertyMetadata("Board"));
    public static readonly DependencyProperty BoardContextLayersLabelProperty = DependencyProperty.Register(nameof(BoardContextLayersLabel), typeof(string), typeof(PanelPreviewControl), new PropertyMetadata("Layers"));

    private readonly Dictionary<BoardPlacement, PcbPoint> transientOffsets = new();
    private readonly Dictionary<ManufacturingFile, Drawing> fileDrawings = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? observableLayers;
    private INotifyCollectionChanged? observableMachinePath;
    private Drawing? staticDrawing;
    private StreamGeometry? machinePathGeometry;
    private bool staticDrawingDirty = true;
    private bool machinePathDirty = true;
    private double zoom = 1;
    private Vector pan;
    private bool isPanning;
    private bool isDraggingPlacement;
    private Point pointerOrigin;
    private PcbPoint dragOriginOffset;
    private BoardPlacement? draggedPlacement;

    public PanelPreviewControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public IEnumerable<BoardPlacement>? Placements { get => (IEnumerable<BoardPlacement>?)GetValue(PlacementsProperty); set => SetValue(PlacementsProperty, value); }
    public IEnumerable<LayerItemViewModel>? Layers { get => (IEnumerable<LayerItemViewModel>?)GetValue(LayersProperty); set => SetValue(LayersProperty, value); }
    public double PanelWidthMm { get => (double)GetValue(PanelWidthMmProperty); set => SetValue(PanelWidthMmProperty, value); }
    public double PanelHeightMm { get => (double)GetValue(PanelHeightMmProperty); set => SetValue(PanelHeightMmProperty, value); }
    public double WorkspaceWidthMm { get => (double)GetValue(WorkspaceWidthMmProperty); set => SetValue(WorkspaceWidthMmProperty, value); }
    public double WorkspaceHeightMm { get => (double)GetValue(WorkspaceHeightMmProperty); set => SetValue(WorkspaceHeightMmProperty, value); }
    public string StagingAreaLabel { get => (string)GetValue(StagingAreaLabelProperty); set => SetValue(StagingAreaLabelProperty, value); }
    public double MachinePositionXMillimetres { get => (double)GetValue(MachinePositionXMillimetresProperty); set => SetValue(MachinePositionXMillimetresProperty, value); }
    public double MachinePositionYMillimetres { get => (double)GetValue(MachinePositionYMillimetresProperty); set => SetValue(MachinePositionYMillimetresProperty, value); }
    public bool ShowMachinePosition { get => (bool)GetValue(ShowMachinePositionProperty); set => SetValue(ShowMachinePositionProperty, value); }
    public string MachinePositionLabel { get => (string)GetValue(MachinePositionLabelProperty); set => SetValue(MachinePositionLabelProperty, value); }
    public string MachinePositionColor { get => (string)GetValue(MachinePositionColorProperty); set => SetValue(MachinePositionColorProperty, value); }
    public IEnumerable<PcbPoint>? MachinePath { get => (IEnumerable<PcbPoint>?)GetValue(MachinePathProperty); set => SetValue(MachinePathProperty, value); }
    public ICommand? MovePlacementCommand { get => (ICommand?)GetValue(MovePlacementCommandProperty); set => SetValue(MovePlacementCommandProperty, value); }
    public ICommand? DeletePlacementCommand { get => (ICommand?)GetValue(DeletePlacementCommandProperty); set => SetValue(DeletePlacementCommandProperty, value); }
    public BoardPlacement? SelectedPlacement { get => (BoardPlacement?)GetValue(SelectedPlacementProperty); set => SetValue(SelectedPlacementProperty, value); }
    public string BoardContextNameLabel { get => (string)GetValue(BoardContextNameLabelProperty); set => SetValue(BoardContextNameLabelProperty, value); }
    public string BoardContextLayersLabel { get => (string)GetValue(BoardContextLayersLabelProperty); set => SetValue(BoardContextLayersLabelProperty, value); }

    private static DependencyProperty RegisterStaticVisualProperty<T>(string name, T defaultValue) => DependencyProperty.Register(name, typeof(T), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender, OnStaticVisualPropertyChanged));

    private static void OnStaticVisualPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (PanelPreviewControl)dependencyObject;
        control.staticDrawingDirty = true;
        control.machinePathDirty = true;
        control.InvalidateVisual();
    }

    /// <summary>Deletes the selected placement through <see cref="DeletePlacementCommand"/>.</summary>
    public void RequestDeleteSelection()
    {
        if (SelectedPlacement is { } placement && DeletePlacementCommand?.CanExecute(placement) == true) DeletePlacementCommand.Execute(placement);
    }

    private static void OnPlacementsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (PanelPreviewControl)dependencyObject;
        control.transientOffsets.Clear();
        if (control.SelectedPlacement is { } selected && !(eventArgs.NewValue as IEnumerable<BoardPlacement> ?? Enumerable.Empty<BoardPlacement>()).Contains(selected)) control.SelectedPlacement = null;
        control.staticDrawingDirty = true;
        control.InvalidateVisual();
    }

    private static void OnLayersChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (PanelPreviewControl)dependencyObject;
        control.DetachLayerEvents(eventArgs.OldValue as IEnumerable<LayerItemViewModel>);
        control.AttachLayerEvents(eventArgs.NewValue as IEnumerable<LayerItemViewModel>);
        control.fileDrawings.Clear();
        control.staticDrawingDirty = true;
        control.InvalidateVisual();
    }

    private static void OnMachinePathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (PanelPreviewControl)dependencyObject;
        if (control.observableMachinePath is not null) control.observableMachinePath.CollectionChanged -= control.OnMachinePathChanged;
        control.observableMachinePath = eventArgs.NewValue as INotifyCollectionChanged;
        if (control.observableMachinePath is not null) control.observableMachinePath.CollectionChanged += control.OnMachinePathChanged;
        control.machinePathDirty = true;
        control.InvalidateVisual();
    }

    private void OnMachinePathChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        machinePathDirty = true;
        InvalidateVisual();
    }

    private void AttachLayerEvents(IEnumerable<LayerItemViewModel>? layers)
    {
        if (layers is INotifyCollectionChanged changed)
        {
            observableLayers = changed;
            changed.CollectionChanged += OnLayerCollectionChanged;
        }
        foreach (var layer in layers ?? Enumerable.Empty<LayerItemViewModel>()) layer.PropertyChanged += OnLayerPropertyChanged;
    }

    private void DetachLayerEvents(IEnumerable<LayerItemViewModel>? layers)
    {
        if (observableLayers is not null) observableLayers.CollectionChanged -= OnLayerCollectionChanged;
        observableLayers = null;
        foreach (var layer in layers ?? Enumerable.Empty<LayerItemViewModel>()) layer.PropertyChanged -= OnLayerPropertyChanged;
    }

    private void OnLayerCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is IEnumerable oldItems) foreach (var layer in oldItems.OfType<LayerItemViewModel>()) layer.PropertyChanged -= OnLayerPropertyChanged;
        if (eventArgs.NewItems is IEnumerable newItems) foreach (var layer in newItems.OfType<LayerItemViewModel>()) layer.PropertyChanged += OnLayerPropertyChanged;
        fileDrawings.Clear();
        staticDrawingDirty = true;
        InvalidateVisual();
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        fileDrawings.Clear();
        staticDrawingDirty = true;
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        staticDrawingDirty = true;
        machinePathDirty = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0 || PanelWidthMm <= 0 || PanelHeightMm <= 0 || WorkspaceWidthMm <= 0 || WorkspaceHeightMm <= 0) return;
        // An explicit transparent surface makes wheel/pan input available over every point of the work area,
        // including empty grid cells where no Gerber geometry is drawn.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        if (staticDrawingDirty || staticDrawing is null) RebuildStaticDrawing();
        drawingContext.DrawDrawing(staticDrawing);
        DrawMachinePath(drawingContext);
        if (ShowMachinePosition) DrawMachinePosition(drawingContext);
        drawingContext.Pop();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs eventArgs)
    {
        base.OnMouseWheel(eventArgs);
        if (PanelWidthMm <= 0 || PanelHeightMm <= 0) return;
        var pointer = eventArgs.GetPosition(this);
        var pointBefore = ScreenToPanel(pointer);
        zoom = Math.Clamp(zoom * (eventArgs.Delta > 0 ? 1.18 : 1 / 1.18), 0.35, 12);
        var unzoomed = PanelToUnzoomedScreen(pointBefore);
        var center = GetViewportCenter();
        pan = pointer - center - (unzoomed - center) * zoom;
        Cursor = zoom > 1.001 ? Cursors.SizeAll : Cursors.Arrow;
        staticDrawingDirty = true;
        machinePathDirty = true;
        InvalidateVisual();
        eventArgs.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        Focus();
        var pointer = eventArgs.GetPosition(this);
        pointerOrigin = pointer;
        if (eventArgs.ChangedButton == MouseButton.Middle || (eventArgs.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        {
            isPanning = true;
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton == MouseButton.Right)
        {
            var contextPlacement = HitTestPlacement(ScreenToPanel(pointer));
            SelectedPlacement = contextPlacement;
            if (contextPlacement is not null) OpenBoardContextMenu(contextPlacement);
            eventArgs.Handled = contextPlacement is not null;
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Left) return;
        var hit = HitTestPlacement(ScreenToPanel(pointer));
        SelectedPlacement = hit;
        if (hit is { } placement)
        {
            draggedPlacement = placement;
            dragOriginOffset = GetEffectivePlacement(placement).Offset;
            isDraggingPlacement = true;
            Cursor = Cursors.SizeAll;
            CaptureMouse();
        }
        InvalidateVisual();
        eventArgs.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        var pointer = eventArgs.GetPosition(this);
        if (isPanning)
        {
            pan += pointer - pointerOrigin;
            pointerOrigin = pointer;
            staticDrawingDirty = true;
            machinePathDirty = true;
            InvalidateVisual();
            eventArgs.Handled = true;
            return;
        }

        if (!isDraggingPlacement || draggedPlacement is not { } placement) return;
        var from = ScreenToPanel(pointerOrigin);
        var to = ScreenToPanel(pointer);
        var requested = new PcbPoint(dragOriginOffset.X + to.X - from.X, dragOriginOffset.Y + to.Y - from.Y);
        transientOffsets[placement] = ClampOffsetToWorkspace(placement, requested);
        staticDrawingDirty = true;
        InvalidateVisual();
        eventArgs.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        if (isPanning)
        {
            EndPointerOperation();
            eventArgs.Handled = true;
            return;
        }

        if (isDraggingPlacement && eventArgs.ChangedButton == MouseButton.Left)
        {
            var placement = draggedPlacement;
            var finalOffset = placement is not null && transientOffsets.TryGetValue(placement, out var offset) ? offset : dragOriginOffset;
            EndPointerOperation();
            if (placement is not null && !finalOffset.Equals(placement.Offset))
            {
                var request = new PlacementMoveRequest(placement, finalOffset);
                if (MovePlacementCommand?.CanExecute(request) == true) MovePlacementCommand.Execute(request);
            }
            InvalidateVisual();
            eventArgs.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs eventArgs)
    {
        base.OnLostMouseCapture(eventArgs);
        isPanning = false;
        isDraggingPlacement = false;
        draggedPlacement = null;
        Cursor = zoom > 1.001 ? Cursors.SizeAll : Cursors.Arrow;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Key == Key.Delete)
        {
            RequestDeleteSelection();
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Home)
        {
            zoom = 1;
            pan = default;
            Cursor = Cursors.Arrow;
            staticDrawingDirty = true;
            machinePathDirty = true;
            InvalidateVisual();
            eventArgs.Handled = true;
        }
    }

    private void EndPointerOperation()
    {
        isPanning = false;
        isDraggingPlacement = false;
        draggedPlacement = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Cursor = zoom > 1.001 ? Cursors.SizeAll : Cursors.Arrow;
    }

    private BoardPlacement? HitTestPlacement(PcbPoint point)
    {
        foreach (var placement in (Placements ?? Enumerable.Empty<BoardPlacement>()).Reverse())
        {
            var bounds = GetEffectivePlacement(placement).Bounds;
            if (point.X >= bounds.MinX && point.X <= bounds.MaxX && point.Y >= bounds.MinY && point.Y <= bounds.MaxY) return placement;
        }
        return null;
    }

    private BoardPlacement GetEffectivePlacement(BoardPlacement placement) => transientOffsets.TryGetValue(placement, out var offset) ? placement with { Offset = offset } : placement;

    private void RebuildStaticDrawing()
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            DrawStagingArea(context);
            DrawPanelSubstrate(context);
            foreach (var placement in Placements ?? Enumerable.Empty<BoardPlacement>()) DrawBoard(context, GetEffectivePlacement(placement));
            if (SelectedPlacement is { } selected) DrawSelection(context, GetEffectivePlacement(selected).Bounds);
        }
        if (group.CanFreeze) group.Freeze();
        staticDrawing = group;
        staticDrawingDirty = false;
    }

    private void OpenBoardContextMenu(BoardPlacement placement)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = $"{BoardContextNameLabel}: {placement.Board.DisplayName}", FontWeight = FontWeights.SemiBold, IsEnabled = false });
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = $"{BoardContextLayersLabel}:", FontWeight = FontWeights.SemiBold, IsEnabled = false });
        foreach (var file in placement.Board.Files.OrderBy(file => file.Layer).ThenBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = $"{GetLayerDisplayName(file.Layer)} — {file.Name}", IsEnabled = false });
        }
        ContextMenu = menu;
        menu.IsOpen = true;
    }

    private string GetLayerDisplayName(ManufacturingLayerKind kind)
    {
        var identifier = kind switch
        {
            ManufacturingLayerKind.Drill => "Drill",
            ManufacturingLayerKind.CopperTop => "Top",
            ManufacturingLayerKind.CopperBottom => "Bottom",
            ManufacturingLayerKind.Profile => "Outline",
            ManufacturingLayerKind.SilkScreenTop or ManufacturingLayerKind.SilkScreenBottom => "Silkscreen",
            _ => string.Empty,
        };
        return Layers?.FirstOrDefault(layer => layer.Identifier == identifier)?.Name ?? kind.ToString();
    }

    private PcbPoint ClampOffsetToWorkspace(BoardPlacement placement, PcbPoint requested)
    {
        var bounds = (placement with { Offset = new PcbPoint(0, 0) }).Bounds;
        if (bounds.IsEmpty) return requested;
        var minimumX = -bounds.MinX;
        var maximumX = WorkspaceWidthMm - bounds.MaxX;
        var minimumY = -bounds.MinY;
        var maximumY = WorkspaceHeightMm - bounds.MaxY;
        return new PcbPoint(Clamp(requested.X, minimumX, maximumX), Clamp(requested.Y, minimumY, maximumY));
    }

    private static double Clamp(double value, double minimum, double maximum) => minimum > maximum ? minimum : Math.Clamp(value, minimum, maximum);

    private void DrawSelection(DrawingContext context, PcbBounds bounds)
    {
        var topLeft = ToScreen(new PcbPoint(bounds.MinX, bounds.MaxY));
        var bottomRight = ToScreen(new PcbPoint(bounds.MaxX, bounds.MinY));
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(11, 118, 110)), 2) { DashStyle = DashStyles.Dash }, new Rect(topLeft, bottomRight));
    }

    private void DrawBoard(DrawingContext context, BoardPlacement placement)
    {
        foreach (var file in placement.Board.Files)
        {
            var layer = file.Format == FileFormat.Excellon ? ManufacturingLayerKind.Drill : file.Layer;
            if (!TryGetLayer(layer, out var brush)) continue;
            if (!fileDrawings.TryGetValue(file, out var drawing))
            {
                drawing = BuildFileDrawing(file, brush);
                fileDrawings[file] = drawing;
            }
            context.PushTransform(CreatePlacementTransform(placement));
            context.DrawDrawing(drawing);
            context.Pop();
        }
    }

    private bool TryGetLayer(ManufacturingLayerKind kind, out Brush brush)
    {
        var layerName = kind switch
        {
            ManufacturingLayerKind.Drill => "Drill",
            ManufacturingLayerKind.CopperTop => "Top",
            ManufacturingLayerKind.CopperBottom => "Bottom",
            ManufacturingLayerKind.Profile => "Outline",
            ManufacturingLayerKind.SilkScreenTop or ManufacturingLayerKind.SilkScreenBottom => "Silkscreen",
            _ => string.Empty,
        };
        var layer = Layers?.FirstOrDefault(candidate => candidate.Identifier == layerName && candidate.IsVisible);
        brush = layer?.Brush ?? Brushes.Transparent;
        return layer is not null;
    }

    private Drawing BuildFileDrawing(ManufacturingFile file, Brush brush)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            try
            {
                if (file.Format == FileFormat.Gerber) DrawGerberFile(context, file.Content, brush);
                else if (file.Format == FileFormat.Excellon) DrawExcellonFile(context, file.Content, brush);
            }
            catch (ArgumentException) { }
        }
        if (group.CanFreeze) group.Freeze();
        return group;
    }

    private static void DrawGerberFile(DrawingContext context, string content, Brush brush)
    {
        var document = new GerberParser().Parse(content);
        foreach (var operation in document.Operations)
        {
            if (operation.Type == GerberOperationType.Move) continue;
            var start = ToDrawingPoint(operation.Start);
            var end = ToDrawingPoint(operation.End);
            var diameter = Math.Max(.01, operation.ApertureDiameterMillimetres);
            if (operation.Type == GerberOperationType.Draw) context.DrawLine(CreateTracePen(brush, diameter), start, end);
            if (operation.Type == GerberOperationType.Flash) DrawGerberFlash(context, brush, end, operation.Aperture);
            if (operation.Type == GerberOperationType.Region) DrawGerberRegion(context, brush, operation.RegionPoints);
        }
    }

    private static void DrawGerberRegion(DrawingContext context, Brush brush, IReadOnlyList<PcbPoint>? points)
    {
        if (points is not { Count: >= 3 }) return;
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(ToDrawingPoint(points[0]), isFilled: true, isClosed: true);
            geometryContext.PolyLineTo(points.Skip(1).Select(ToDrawingPoint).ToArray(), isStroked: false, isSmoothJoin: false);
        }
        if (geometry.CanFreeze) geometry.Freeze();
        context.DrawGeometry(brush, null, geometry);
    }

    private static void DrawGerberFlash(DrawingContext context, Brush brush, Point center, GerberAperture aperture)
    {
        var width = Math.Max(.01, aperture.WidthMillimetres);
        var height = Math.Max(.01, aperture.HeightMillimetres);
        var bounds = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
        switch (aperture.Shape)
        {
            case GerberApertureShape.Rectangle:
                context.DrawRectangle(brush, null, bounds);
                break;
            case GerberApertureShape.Obround:
                context.DrawRoundedRectangle(brush, null, bounds, Math.Min(width, height) / 2, Math.Min(width, height) / 2);
                break;
            case GerberApertureShape.Circle:
            case GerberApertureShape.Polygon:
                context.DrawEllipse(brush, null, center, width / 2, height / 2);
                break;
        }
    }

    private static Pen CreateTracePen(Brush brush, double thickness) => new(brush, thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    };

    private void DrawPanelSubstrate(DrawingContext context)
    {
        var panelBounds = new Rect(ToScreen(new PcbPoint(0, PanelHeightMm)), ToScreen(new PcbPoint(PanelWidthMm, 0)));
        context.DrawRectangle(Brushes.White, null, panelBounds);

        var gridSpacingMm = 5d;
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(233, 240, 239)), Math.Max(.35, PixelsPerMillimetre * .025));
        var majorGridPen = new Pen(new SolidColorBrush(Color.FromRgb(215, 228, 225)), Math.Max(.5, PixelsPerMillimetre * .04));
        for (var x = 0d; x <= PanelWidthMm + .001; x += gridSpacingMm)
        {
            context.DrawLine(IsMajorGridLine(x) ? majorGridPen : gridPen, ToScreen(new PcbPoint(x, 0)), ToScreen(new PcbPoint(x, PanelHeightMm)));
        }
        for (var y = 0d; y <= PanelHeightMm + .001; y += gridSpacingMm)
        {
            context.DrawLine(IsMajorGridLine(y) ? majorGridPen : gridPen, ToScreen(new PcbPoint(0, y)), ToScreen(new PcbPoint(PanelWidthMm, y)));
        }
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(11, 118, 110)), Math.Max(1, PixelsPerMillimetre * .08)), panelBounds);
    }

    private void DrawStagingArea(DrawingContext context)
    {
        if (WorkspaceWidthMm <= PanelWidthMm + .001) return;
        var stagingBounds = new Rect(ToScreen(new PcbPoint(PanelWidthMm, WorkspaceHeightMm)), ToScreen(new PcbPoint(WorkspaceWidthMm, 0)));
        var border = new Pen(new SolidColorBrush(Color.FromRgb(160, 181, 178)), Math.Max(.75, PixelsPerMillimetre * .04)) { DashStyle = DashStyles.Dash };
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(231, 238, 237)), border, stagingBounds);
        if (string.IsNullOrWhiteSpace(StagingAreaLabel)) return;
        var text = new FormattedText(StagingAreaLabel, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, new SolidColorBrush(Color.FromRgb(72, 103, 99)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        context.DrawText(text, new Point(stagingBounds.Left + 8, stagingBounds.Top + 6));
    }

    private void DrawMachinePosition(DrawingContext context)
    {
        var point = ToScreen(new PcbPoint(MachinePositionXMillimetres, MachinePositionYMillimetres));
        var radius = Math.Max(7, PixelsPerMillimetre * 1.5);
        var accent = CreateMachinePositionBrush();
        var halo = new SolidColorBrush(Color.FromArgb(72, accent.Color.R, accent.Color.G, accent.Color.B));
        context.DrawEllipse(halo, null, point, radius * 1.8, radius * 1.8);
        context.DrawEllipse(Brushes.White, new Pen(accent, 2), point, radius, radius);
        context.DrawLine(new Pen(accent, 2), new Point(point.X - radius * 1.6, point.Y), new Point(point.X + radius * 1.6, point.Y));
        context.DrawLine(new Pen(accent, 2), new Point(point.X, point.Y - radius * 1.6), new Point(point.X, point.Y + radius * 1.6));
        if (string.IsNullOrWhiteSpace(MachinePositionLabel)) return;
        var text = new FormattedText(MachinePositionLabel, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var origin = new Point(Math.Clamp(point.X + radius + 5, 4, Math.Max(4, ActualWidth - text.Width - 4)), Math.Clamp(point.Y - radius - text.Height - 4, 4, Math.Max(4, ActualHeight - text.Height - 4)));
        context.DrawRoundedRectangle(Brushes.White, new Pen(accent, 1), new Rect(origin.X - 4, origin.Y - 2, text.Width + 8, text.Height + 4), 3, 3);
        context.DrawText(text, origin);
    }

    private void DrawMachinePath(DrawingContext context)
    {
        if (MachinePath is null) return;
        if (machinePathDirty || machinePathGeometry is null) RebuildMachinePathGeometry();
        if (machinePathGeometry is null) return;
        var accent = CreateMachinePositionBrush();
        var trace = new Pen(new SolidColorBrush(Color.FromArgb(185, accent.Color.R, accent.Color.G, accent.Color.B)), Math.Max(1.5, PixelsPerMillimetre * .24)) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        context.DrawGeometry(null, trace, machinePathGeometry);
    }

    private void RebuildMachinePathGeometry()
    {
        var points = MachinePath?.Select(ToScreen).ToList() ?? [];
        if (points.Count < 2)
        {
            machinePathGeometry = null;
            machinePathDirty = false;
            return;
        }
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo(points.Skip(1).ToList(), true, false);
        }
        if (geometry.CanFreeze) geometry.Freeze();
        machinePathGeometry = geometry;
        machinePathDirty = false;
    }

    private static bool IsMajorGridLine(double value) => Math.Abs(value % 25) < .001 || Math.Abs(value % 25 - 25) < .001;

    private SolidColorBrush CreateMachinePositionBrush()
    {
        try
        {
            if (ColorConverter.ConvertFromString(MachinePositionColor) is Color color) return new SolidColorBrush(color);
        }
        catch (FormatException) { }
        return new SolidColorBrush(Color.FromRgb(180, 35, 24));
    }

    private static void DrawExcellonFile(DrawingContext context, string content, Brush brush)
    {
        var document = new ExcellonParser().Parse(content);
        foreach (var hit in document.Hits)
        {
            var diameterMm = document.Tools.TryGetValue(hit.ToolNumber, out var diameter) ? diameter : 0.5;
            var radius = Math.Max(.01, diameterMm) / 2;
            context.DrawEllipse(brush, null, ToDrawingPoint(hit.Position), radius, radius);
        }
    }

    private Transform CreatePlacementTransform(BoardPlacement placement)
    {
        var origin = ToScreen(placement.TransformPoint(new PcbPoint(0, 0)));
        var xAxis = ToScreen(placement.TransformPoint(new PcbPoint(1, 0)));
        var yAxis = ToScreen(placement.TransformPoint(new PcbPoint(0, -1)));
        return new MatrixTransform(new Matrix(xAxis.X - origin.X, xAxis.Y - origin.Y, yAxis.X - origin.X, yAxis.Y - origin.Y, origin.X, origin.Y));
    }

    private static Point ToDrawingPoint(PcbPoint point) => new(point.X, -point.Y);

    private double PixelsPerMillimetre => Math.Min(ActualWidth / WorkspaceWidthMm, ActualHeight / WorkspaceHeightMm) * zoom;

    private Point ToScreen(PcbPoint point)
    {
        var unzoomed = PanelToUnzoomedScreen(point);
        var center = GetViewportCenter();
        return center + (unzoomed - center) * zoom + pan;
    }

    private Point PanelToUnzoomedScreen(PcbPoint point)
    {
        var scale = Math.Min(ActualWidth / WorkspaceWidthMm, ActualHeight / WorkspaceHeightMm);
        var width = WorkspaceWidthMm * scale;
        var height = WorkspaceHeightMm * scale;
        return new Point((ActualWidth - width) / 2 + point.X * scale, (ActualHeight - height) / 2 + (WorkspaceHeightMm - point.Y) * scale);
    }

    private PcbPoint ScreenToPanel(Point point)
    {
        var center = GetViewportCenter();
        var unzoomed = center + (point - center - pan) / zoom;
        var scale = Math.Min(ActualWidth / WorkspaceWidthMm, ActualHeight / WorkspaceHeightMm);
        var width = WorkspaceWidthMm * scale;
        var height = WorkspaceHeightMm * scale;
        return new PcbPoint((unzoomed.X - (ActualWidth - width) / 2) / scale, WorkspaceHeightMm - (unzoomed.Y - (ActualHeight - height) / 2) / scale);
    }

    private Point GetViewportCenter() => new(ActualWidth / 2, ActualHeight / 2);
}

/// <summary>Sent when a board is released after a manual drag.</summary>
public sealed record PlacementMoveRequest(BoardPlacement Placement, PcbPoint RequestedOffset);
