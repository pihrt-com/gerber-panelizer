using GerberPanelizer.Core.Geometry;

namespace GerberPanelizer.Core.Parsing;

public sealed record ExcellonHit(int ToolNumber, PcbPoint Position);

public sealed record ExcellonDocument(
    CoordinateFormat CoordinateFormat,
    IReadOnlyDictionary<int, double> Tools,
    IReadOnlyList<ExcellonHit> Hits,
    PcbBounds Bounds,
    IReadOnlyList<string> Warnings);
