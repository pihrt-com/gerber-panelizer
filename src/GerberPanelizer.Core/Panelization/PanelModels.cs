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

public sealed record BoardPlacement(ImportedBoard Board, PcbPoint Offset, double RotationDegrees = 0)
{
    public PcbBounds Bounds => Board.Bounds.Translate(Offset.X, Offset.Y);
}

public sealed record PanelizationResult(IReadOnlyList<BoardPlacement> Placements, IReadOnlyList<ImportedBoard> UnplacedBoards, PcbBounds UsedBounds);
