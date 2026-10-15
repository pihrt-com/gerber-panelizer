using GerberPanelizer.Core.Geometry;

namespace GerberPanelizer.Core.Parsing;

public enum GerberOperationType { Move, Draw, Flash }

public sealed record GerberOperation(GerberOperationType Type, PcbPoint Start, PcbPoint End, double ApertureDiameterMillimetres);

public sealed record GerberDocument(
    CoordinateFormat CoordinateFormat,
    PcbBounds Bounds,
    IReadOnlyList<GerberOperation> Operations,
    IReadOnlyList<string> Warnings);
