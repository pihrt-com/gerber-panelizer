using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;

namespace GerberPanelizer.Core.Panelization;

public sealed record PanelDefinition(PcbSize Size, double MarginMillimetres, double SpacingMillimetres)
{
    public PcbBounds UsableBounds => new(MarginMillimetres, MarginMillimetres, Size.Width - MarginMillimetres, Size.Height - MarginMillimetres);

    public void Validate()
    {
        if (!Size.IsPositive) throw new ArgumentOutOfRangeException(nameof(Size), "Panel dimensions must be positive.");
        if (MarginMillimetres < 0 || SpacingMillimetres < 0) throw new ArgumentOutOfRangeException(nameof(MarginMillimetres), "Margins and spacing cannot be negative.");
        if (UsableBounds.Width <= 0 || UsableBounds.Height <= 0) throw new ArgumentException("Margins leave no usable panel area.");
    }
}

public enum BoardMirrorMode
{
    None,
    MirrorX,
    MirrorY,
}

public sealed record BoardPlacement(
    ImportedBoard Board,
    PcbPoint Offset,
    double RotationDegrees = 0,
    BoardMirrorMode MirrorMode = BoardMirrorMode.None)
{
    public PcbBounds Bounds
    {
        get
        {
            if (Board.Bounds.IsEmpty) return Board.Bounds;
            return new[]
            {
                new PcbPoint(Board.Bounds.MinX, Board.Bounds.MinY),
                new PcbPoint(Board.Bounds.MinX, Board.Bounds.MaxY),
                new PcbPoint(Board.Bounds.MaxX, Board.Bounds.MinY),
                new PcbPoint(Board.Bounds.MaxX, Board.Bounds.MaxY),
            }.Aggregate(PcbBounds.Empty, (bounds, point) => bounds.Include(TransformPoint(point)));
        }
    }

    public PcbPoint TransformPoint(PcbPoint point)
    {
        if (Board.Bounds.IsEmpty) return point.Translate(Offset.X, Offset.Y);
        var center = Board.Bounds.Center;
        var transformed = new PcbPoint(point.X - center.X, point.Y - center.Y);
        transformed = ApplyMirror(transformed);
        transformed = ApplyRotation(transformed);
        return new PcbPoint(transformed.X + center.X + Offset.X, transformed.Y + center.Y + Offset.Y);
    }

    public PcbPoint TransformVector(PcbPoint vector) => ApplyRotation(ApplyMirror(vector));

    private PcbPoint ApplyMirror(PcbPoint point) => MirrorMode switch
    {
        BoardMirrorMode.MirrorX => point with { X = -point.X },
        BoardMirrorMode.MirrorY => point with { Y = -point.Y },
        _ => point,
    };

    private PcbPoint ApplyRotation(PcbPoint point)
    {
        var radians = RotationDegrees * Math.PI / 180d;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new PcbPoint(point.X * cosine - point.Y * sine, point.X * sine + point.Y * cosine);
    }
}

public sealed record PanelizationResult(IReadOnlyList<BoardPlacement> Placements, IReadOnlyList<ImportedBoard> UnplacedBoards, PcbBounds UsedBounds);
