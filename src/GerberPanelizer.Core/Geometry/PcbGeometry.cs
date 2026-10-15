namespace GerberPanelizer.Core.Geometry;

public readonly record struct PcbPoint(double X, double Y)
{
    public PcbPoint Translate(double x, double y) => new(X + x, Y + y);
}

public readonly record struct PcbSize(double Width, double Height)
{
    public bool IsPositive => Width > 0 && Height > 0;
}

public readonly record struct PcbBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public static PcbBounds Empty { get; } = new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

    public bool IsEmpty => double.IsPositiveInfinity(MinX);
    public double Width => IsEmpty ? 0 : MaxX - MinX;
    public double Height => IsEmpty ? 0 : MaxY - MinY;
    public PcbPoint Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);

    public PcbBounds Include(PcbPoint point) => IsEmpty
        ? new(point.X, point.Y, point.X, point.Y)
        : new(Math.Min(MinX, point.X), Math.Min(MinY, point.Y), Math.Max(MaxX, point.X), Math.Max(MaxY, point.Y));

    public PcbBounds Include(PcbBounds other) => other.IsEmpty
        ? this
        : Include(new PcbPoint(other.MinX, other.MinY)).Include(new PcbPoint(other.MaxX, other.MaxY));

    public PcbBounds Inflate(double amount) => IsEmpty ? this : new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
    public PcbBounds Translate(double x, double y) => IsEmpty ? this : new(MinX + x, MinY + y, MaxX + x, MaxY + y);
}
