using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;

namespace GerberPanelizer.Core.Panelization;

public enum PanelPlacementState
{
    Outside,
    Inside,
    CrossingBoundary,
}

public sealed record BoardStagingResult(IReadOnlyList<BoardPlacement> Placements, PcbSize WorkspaceSize);

/// <summary>Places imported boards in a visible staging area to the right of the machine work area.</summary>
public sealed class BoardStagingLayout
{
    private const double HeaderClearanceMillimetres = 10;

    public BoardStagingResult Arrange(
        IEnumerable<ImportedBoard> boards,
        PcbSize panelSize,
        double spacingMillimetres,
        BoardMirrorMode mirrorMode = BoardMirrorMode.None,
        double? firstColumnX = null)
    {
        ArgumentNullException.ThrowIfNull(boards);
        if (!panelSize.IsPositive) throw new ArgumentOutOfRangeException(nameof(panelSize), "Panel dimensions must be positive.");
        if (spacingMillimetres < 0) throw new ArgumentOutOfRangeException(nameof(spacingMillimetres), "Spacing cannot be negative.");

        var gap = Math.Max(5, spacingMillimetres);
        var prepared = boards.Select(board => new BoardPlacement(board, new PcbPoint(0, 0), 0, mirrorMode)).ToList();
        var tallestBoard = prepared.Where(placement => !placement.Bounds.IsEmpty).Select(placement => placement.Bounds.Height).DefaultIfEmpty(0).Max();
        var workspaceHeight = Math.Max(panelSize.Height, tallestBoard + gap * 2 + HeaderClearanceMillimetres);
        var columnX = Math.Max(panelSize.Width + gap, firstColumnX ?? panelSize.Width + gap);
        var columnTop = workspaceHeight - gap - HeaderClearanceMillimetres;
        var columnWidth = 0d;
        var columnHasBoard = false;
        var placements = new List<BoardPlacement>();

        foreach (var placement in prepared)
        {
            var bounds = placement.Bounds;
            if (bounds.IsEmpty) continue;
            if (columnHasBoard && columnTop - bounds.Height < gap)
            {
                columnX += columnWidth + gap;
                columnTop = workspaceHeight - gap - HeaderClearanceMillimetres;
                columnWidth = 0;
                columnHasBoard = false;
            }

            var offset = new PcbPoint(columnX - bounds.MinX, columnTop - bounds.MaxY);
            placements.Add(placement with { Offset = offset });
            columnTop -= bounds.Height + gap;
            columnWidth = Math.Max(columnWidth, bounds.Width);
            columnHasBoard = true;
        }

        var workspaceWidth = columnHasBoard ? columnX + columnWidth + gap : panelSize.Width;
        return new BoardStagingResult(placements, new PcbSize(Math.Max(panelSize.Width, workspaceWidth), workspaceHeight));
    }
}

public static class PanelPlacementRules
{
    private const double Tolerance = 0.000001;

    public static PanelPlacementState Classify(BoardPlacement placement, PcbSize panelSize)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!panelSize.IsPositive) throw new ArgumentOutOfRangeException(nameof(panelSize), "Panel dimensions must be positive.");
        var bounds = placement.Bounds;
        if (bounds.IsEmpty) return PanelPlacementState.Outside;
        if (bounds.MinX >= -Tolerance && bounds.MinY >= -Tolerance && bounds.MaxX <= panelSize.Width + Tolerance && bounds.MaxY <= panelSize.Height + Tolerance) return PanelPlacementState.Inside;
        if (bounds.MaxX <= 0 || bounds.MaxY <= 0 || bounds.MinX >= panelSize.Width || bounds.MinY >= panelSize.Height) return PanelPlacementState.Outside;
        return PanelPlacementState.CrossingBoundary;
    }
}
