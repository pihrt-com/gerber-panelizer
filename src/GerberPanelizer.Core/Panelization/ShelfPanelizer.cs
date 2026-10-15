using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;

namespace GerberPanelizer.Core.Panelization;

/// <summary>Provides predictable, non-overlapping placement by filling horizontal shelves from bottom to top.</summary>
public sealed class ShelfPanelizer
{
    public PanelizationResult Arrange(IEnumerable<ImportedBoard> boards, PanelDefinition panel)
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
            if (board.Bounds.Width > usable.Width || board.Bounds.Height > usable.Height)
            {
                unplaced.Add(board);
                continue;
            }

            if (cursorX + board.Bounds.Width > usable.MaxX)
            {
                cursorX = usable.MinX;
                cursorY += shelfHeight + panel.SpacingMillimetres;
                shelfHeight = 0;
            }

            if (cursorY + board.Bounds.Height > usable.MaxY)
            {
                unplaced.Add(board);
                continue;
            }

            var offset = new PcbPoint(cursorX - board.Bounds.MinX, cursorY - board.Bounds.MinY);
            placements.Add(new BoardPlacement(board, offset));
            cursorX += board.Bounds.Width + panel.SpacingMillimetres;
            shelfHeight = Math.Max(shelfHeight, board.Bounds.Height);
        }

        var used = placements.Aggregate(PcbBounds.Empty, (current, placement) => current.Include(placement.Bounds));
        return new PanelizationResult(placements, unplaced, used);
    }
}
