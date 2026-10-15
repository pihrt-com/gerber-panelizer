using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
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
    public static readonly DependencyProperty PanelWidthMmProperty = DependencyProperty.Register(nameof(PanelWidthMm), typeof(double), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PanelHeightMmProperty = DependencyProperty.Register(nameof(PanelHeightMm), typeof(double), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(130d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MovePlacementCommandProperty = DependencyProperty.Register(nameof(MovePlacementCommand), typeof(ICommand), typeof(PanelPreviewControl));
    public static readonly DependencyProperty DeletePlacementCommandProperty = DependencyProperty.Register(nameof(DeletePlacementCommand), typeof(ICommand), typeof(PanelPreviewControl));
    public static readonly DependencyProperty SelectedPlacementProperty = DependencyProperty.Register(nameof(SelectedPlacement), typeof(BoardPlacement), typeof(PanelPreviewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly Dictionary<BoardPlacement, PcbPoint> transientOffsets = new();
    private INotifyCollectionChanged? observableLayers;
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
    public ICommand? MovePlacementCommand { get => (ICommand?)GetValue(MovePlacementCommandProperty); set => SetValue(MovePlacementCommandProperty, value); }
    public ICommand? DeletePlacementCommand { get => (ICommand?)GetValue(DeletePlacementCommandProperty); set => SetValue(DeletePlacementCommandProperty, value); }
    public BoardPlacement? SelectedPlacement { get => (BoardPlacement?)GetValue(SelectedPlacementProperty); set => SetValue(SelectedPlacementProperty, value); }

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
        control.InvalidateVisual();
    }

    private static void OnLayersChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (PanelPreviewControl)dependencyObject;
        control.DetachLayerEvents(eventArgs.OldValue as IEnumerable<LayerItemViewModel>);
        control.AttachLayerEvents(eventArgs.NewValue as IEnumerable<LayerItemViewModel>);
        control.InvalidateVisual();
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
        InvalidateVisual();
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0 || PanelWidthMm <= 0 || PanelHeightMm <= 0) return;
        // An explicit transparent surface makes wheel/pan input available over every point of the work area,
        // including empty grid cells where no Gerber geometry is drawn.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        DrawPanelSubstrate(drawingContext);
        foreach (var placement in Placements ?? Enumerable.Empty<BoardPlacement>()) DrawBoard(drawingContext, GetEffectivePlacement(placement));
        if (SelectedPlacement is { } selected) DrawSelection(drawingContext, GetEffectivePlacement(selected).Bounds);
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
            InvalidateVisual();
            eventArgs.Handled = true;
            return;
        }

        if (!isDraggingPlacement || draggedPlacement is not { } placement) return;
        var from = ScreenToPanel(pointerOrigin);
        var to = ScreenToPanel(pointer);
        var requested = new PcbPoint(dragOriginOffset.X + to.X - from.X, dragOriginOffset.Y + to.Y - from.Y);
        transientOffsets[placement] = ClampOffsetToPanel(placement.Board, requested);
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

    private PcbPoint ClampOffsetToPanel(ImportedBoard board, PcbPoint requested)
    {
        var bounds = board.Bounds;
        if (bounds.IsEmpty) return requested;
        var minimumX = -bounds.MinX;
        var maximumX = PanelWidthMm - bounds.MaxX;
        var minimumY = -bounds.MinY;
        var maximumY = PanelHeightMm - bounds.MaxY;
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
            if (file.Format == FileFormat.Gerber && TryGetLayer(file.Layer, out var brush)) DrawGerber(context, file.Content, placement, brush);
            if (file.Format == FileFormat.Excellon && TryGetLayer(ManufacturingLayerKind.Drill, out var drillBrush)) DrawExcellon(context, file.Content, placement, drillBrush);
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

    private void DrawGerber(DrawingContext context, string content, BoardPlacement placement, Brush brush)
    {
        GerberDocument document;
        try { document = new GerberParser().Parse(content); }
        catch (ArgumentException) { return; }
        foreach (var operation in document.Operations)
        {
            if (operation.Type == GerberOperationType.Move) continue;
            var start = ToScreen(operation.Start.Translate(placement.Offset.X, placement.Offset.Y));
            var end = ToScreen(operation.End.Translate(placement.Offset.X, placement.Offset.Y));
            var diameter = Math.Max(1, operation.ApertureDiameterMillimetres * PixelsPerMillimetre);
            if (operation.Type == GerberOperationType.Draw) context.DrawLine(CreateTracePen(brush, diameter), start, end);
            if (operation.Type == GerberOperationType.Flash) DrawGerberFlash(context, brush, end, operation.Aperture);
        }
    }

    private void DrawGerberFlash(DrawingContext context, Brush brush, Point center, GerberAperture aperture)
    {
        var width = Math.Max(1, aperture.WidthMillimetres * PixelsPerMillimetre);
        var height = Math.Max(1, aperture.HeightMillimetres * PixelsPerMillimetre);
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

    private static bool IsMajorGridLine(double value) => Math.Abs(value % 25) < .001 || Math.Abs(value % 25 - 25) < .001;

    private void DrawExcellon(DrawingContext context, string content, BoardPlacement placement, Brush brush)
    {
        ExcellonDocument document;
        try { document = new ExcellonParser().Parse(content); }
        catch (ArgumentException) { return; }
        foreach (var hit in document.Hits)
        {
            var diameterMm = document.Tools.TryGetValue(hit.ToolNumber, out var diameter) ? diameter : 0.5;
            var radius = Math.Max(1, diameterMm * PixelsPerMillimetre) / 2;
            context.DrawEllipse(brush, null, ToScreen(hit.Position.Translate(placement.Offset.X, placement.Offset.Y)), radius, radius);
        }
    }

    private double PixelsPerMillimetre => Math.Min(ActualWidth / PanelWidthMm, ActualHeight / PanelHeightMm) * zoom;

    private Point ToScreen(PcbPoint point)
    {
        var unzoomed = PanelToUnzoomedScreen(point);
        var center = GetViewportCenter();
        return center + (unzoomed - center) * zoom + pan;
    }

    private Point PanelToUnzoomedScreen(PcbPoint point)
    {
        var scale = Math.Min(ActualWidth / PanelWidthMm, ActualHeight / PanelHeightMm);
        var width = PanelWidthMm * scale;
        var height = PanelHeightMm * scale;
        return new Point((ActualWidth - width) / 2 + point.X * scale, (ActualHeight - height) / 2 + (PanelHeightMm - point.Y) * scale);
    }

    private PcbPoint ScreenToPanel(Point point)
    {
        var center = GetViewportCenter();
        var unzoomed = center + (point - center - pan) / zoom;
        var scale = Math.Min(ActualWidth / PanelWidthMm, ActualHeight / PanelHeightMm);
        var width = PanelWidthMm * scale;
        var height = PanelHeightMm * scale;
        return new PcbPoint((unzoomed.X - (ActualWidth - width) / 2) / scale, PanelHeightMm - (unzoomed.Y - (ActualHeight - height) / 2) / scale);
    }

    private Point GetViewportCenter() => new(ActualWidth / 2, ActualHeight / 2);
}

/// <summary>Sent when a board is released after a manual drag.</summary>
public sealed record PlacementMoveRequest(BoardPlacement Placement, PcbPoint RequestedOffset);
