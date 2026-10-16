using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;

namespace GerberPanelizer.Core.Panelization;

/// <summary>Provides predictable, non-overlapping placement by filling horizontal shelves from bottom to top.</summary>
public sealed class ShelfPanelizer
{
    public PanelizationResult Arrange(IEnumerable<ImportedBoard> boards, PanelDefinition panel, bool allowRotation = false)
    {
        ArgumentNullException.ThrowIfNull(boards);
        panel.Validate();
        var usable = panel.UsableBounds;
        var placements = new List<BoardPlacement>();
        var unplaced = new List<ImportedBoard>();
        var cursorX = usable.MinX;
        var cursorY = usable.MinY;
        var shelfHeight = 0d;

        foreach (var board in boards.Where(board => !board.Bounds.IsEmpty).OrderByDescending(board => board.Bounds.Height).ThenByDescending(board => board.Bounds.Width))
        {
            var candidates = new[] { CreateCandidate(board, 0), CreateCandidate(board, 90) }
                .Where((_, index) => index == 0 || allowRotation)
                .Where(candidate => candidate.Bounds.Width <= usable.Width && candidate.Bounds.Height <= usable.Height)
                .ToList();
            if (candidates.Count == 0)
            {
                unplaced.Add(board);
                continue;
            }

            var candidate = candidates.FirstOrDefault(item => cursorX + item.Bounds.Width <= usable.MaxX && cursorY + item.Bounds.Height <= usable.MaxY);
            if (candidate is null)
            {
                cursorX = usable.MinX;
                cursorY += shelfHeight + panel.SpacingMillimetres;
                shelfHeight = 0;
                candidate = candidates.FirstOrDefault(item => cursorY + item.Bounds.Height <= usable.MaxY);
            }

            if (candidate is null)
            {
                unplaced.Add(board);
                continue;
            }

            var offset = new PcbPoint(cursorX - candidate.Bounds.MinX, cursorY - candidate.Bounds.MinY);
            var placement = candidate with { Offset = offset };
            placements.Add(placement);
            cursorX += placement.Bounds.Width + panel.SpacingMillimetres;
            shelfHeight = Math.Max(shelfHeight, placement.Bounds.Height);
        }

        var used = placements.Aggregate(PcbBounds.Empty, (current, placement) => current.Include(placement.Bounds));
        return new PanelizationResult(placements, unplaced, used);
    }

    private static BoardPlacement CreateCandidate(ImportedBoard board, double rotationDegrees) => new(board, new PcbPoint(0, 0), rotationDegrees);
}
