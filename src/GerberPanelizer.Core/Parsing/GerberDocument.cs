using GerberPanelizer.Core.Geometry;

namespace GerberPanelizer.Core.Parsing;

public enum GerberOperationType { Move, Draw, Flash }

public enum GerberApertureShape { Unknown, Circle, Rectangle, Obround, Polygon }

public sealed record GerberAperture(GerberApertureShape Shape, double WidthMillimetres, double HeightMillimetres)
{
    public static readonly GerberAperture Unknown = new(GerberApertureShape.Unknown, 0, 0);

    public double EffectiveDiameterMillimetres => Math.Max(WidthMillimetres, HeightMillimetres);
}

public sealed record GerberOperation(GerberOperationType Type, PcbPoint Start, PcbPoint End, GerberAperture Aperture)
{
    public double ApertureDiameterMillimetres => Aperture.EffectiveDiameterMillimetres;
}

public sealed record GerberDocument(
    CoordinateFormat CoordinateFormat,
    PcbBounds Bounds,
    IReadOnlyList<GerberOperation> Operations,
    IReadOnlyList<string> Warnings);
