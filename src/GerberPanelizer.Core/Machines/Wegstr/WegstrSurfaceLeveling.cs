namespace GerberPanelizer.Core.Machines.Wegstr;

public readonly record struct WegstrSurfaceCell(int Column, int Row);

public readonly record struct WegstrProbePoint(WegstrSurfaceCell Cell, int XSteps, int YSteps);

public sealed class WegstrSurfaceProbePlan
{
    internal WegstrSurfaceProbePlan(int originXSteps, int originYSteps, int rasterXSteps, int rasterYSteps, IReadOnlyList<WegstrProbePoint> points)
    {
        OriginXSteps = originXSteps;
        OriginYSteps = originYSteps;
        RasterXSteps = rasterXSteps;
        RasterYSteps = rasterYSteps;
        Points = points;
    }

    public int OriginXSteps { get; }
    public int OriginYSteps { get; }
    public int RasterXSteps { get; }
    public int RasterYSteps { get; }
    public IReadOnlyList<WegstrProbePoint> Points { get; }

    public WegstrSurfaceCell GetCell(int xSteps, int ySteps) => new(
        FloorDivide(xSteps - OriginXSteps, RasterXSteps),
        FloorDivide(ySteps - OriginYSteps, RasterYSteps));

    private static int FloorDivide(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }
}

public sealed class WegstrSurfaceMap
{
    private readonly IReadOnlyDictionary<WegstrSurfaceCell, int> corrections;

    internal WegstrSurfaceMap(WegstrSurfaceProbePlan plan, IReadOnlyDictionary<WegstrSurfaceCell, int> corrections)
    {
        Plan = plan;
        this.corrections = corrections;
    }

    public WegstrSurfaceProbePlan Plan { get; }
    public int PointCount => corrections.Count;
    public int MinimumCorrectionSteps => corrections.Count == 0 ? 0 : corrections.Values.Min();
    public int MaximumCorrectionSteps => corrections.Count == 0 ? 0 : corrections.Values.Max();
    public bool TryGetCorrection(int xSteps, int ySteps, out int correctionSteps) => corrections.TryGetValue(Plan.GetCell(xSteps, ySteps), out correctionSteps);
}

/// <summary>Builds the path-scoped probing raster and applies Wegstr-style stepwise Z compensation.</summary>
public sealed class WegstrSurfaceLeveler
{
    private const double SamplesPerSecond = 50.595238095238095d;

    public WegstrSurfaceProbePlan CreateProbePlan(IEnumerable<WegstrCompiledProgram> programs, double rasterMillimetres, int calibrationX, int calibrationY)
    {
        ArgumentNullException.ThrowIfNull(programs);
        if (rasterMillimetres is < .5 or > 70) throw new ArgumentOutOfRangeException(nameof(rasterMillimetres), "The Wegstr probing raster must be between 0.5 and 70 mm.");
        ValidateCalibration(calibrationX, nameof(calibrationX));
        ValidateCalibration(calibrationY, nameof(calibrationY));

        var cuttingPositions = new List<(int X, int Y)>();
        foreach (var program in programs)
        {
            ArgumentNullException.ThrowIfNull(program);
            var x = 0;
            var y = 0;
            foreach (var block in program.Blocks)
            {
                foreach (var packet in block.Packets)
                {
                    foreach (var step in WegstrPacketCodec.Decode(packet))
                    {
                        x += step.X;
                        y += step.Y;
                        if (step.SpindleOn) cuttingPositions.Add((x, y));
                    }
                }
            }
        }

        if (cuttingPositions.Count == 0) throw new InvalidOperationException("The prepared Wegstr operations contain no spindle-on path to probe.");
        var originX = cuttingPositions.Min(point => point.X);
        var originY = cuttingPositions.Min(point => point.Y);
        var rasterX = Math.Max(1, MillimetresToSteps(rasterMillimetres, calibrationX));
        var rasterY = Math.Max(1, MillimetresToSteps(rasterMillimetres, calibrationY));
        var provisional = new WegstrSurfaceProbePlan(originX, originY, rasterX, rasterY, []);
        var cells = new HashSet<WegstrSurfaceCell>();
        var points = new List<WegstrProbePoint>();
        foreach (var position in cuttingPositions)
        {
            var cell = provisional.GetCell(position.X, position.Y);
            if (cells.Add(cell)) points.Add(new WegstrProbePoint(cell, position.X, position.Y));
        }
        return new WegstrSurfaceProbePlan(originX, originY, rasterX, rasterY, points);
    }

    public WegstrSurfaceMap CreateSurfaceMap(WegstrSurfaceProbePlan plan, IReadOnlyList<int> measuredZSteps)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(measuredZSteps);
        if (plan.Points.Count == 0) throw new ArgumentException("The probing plan is empty.", nameof(plan));
        if (measuredZSteps.Count != plan.Points.Count) throw new ArgumentException("Each probing point must have one measured Z position.", nameof(measuredZSteps));

        var reference = measuredZSteps[0];
        var corrections = new Dictionary<WegstrSurfaceCell, int>(plan.Points.Count);
        for (var index = 0; index < plan.Points.Count; index++)
        {
            corrections[plan.Points[index].Cell] = reference - measuredZSteps[index];
        }
        return new WegstrSurfaceMap(plan, corrections);
    }

    public WegstrCompiledProgram Apply(WegstrCompiledProgram program, WegstrSurfaceMap surfaceMap, int zBacklashSteps)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(surfaceMap);
        if (zBacklashSteps is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(zBacklashSteps), "Wegstr Z backlash compensation must be between 0 and 30 steps.");

        var x = 0;
        var y = 0;
        var appliedCorrection = 0;
        var direction = ZDirection.Down;
        var positiveRun = 0;
        var negativeRun = 0;
        var transformedBlocks = new List<WegstrCompiledBlock>(program.Blocks.Count);
        foreach (var block in program.Blocks)
        {
            var packets = new List<byte[]>(block.Packets.Count);
            foreach (var packet in block.Packets)
            {
                var transformed = new List<WegstrStep>(3);
                foreach (var sourceStep in WegstrPacketCodec.Decode(packet))
                {
                    var targetCorrection = surfaceMap.TryGetCorrection(x, y, out var correction) ? correction : 0;
                    var z = sourceStep.Z;
                    if (targetCorrection > appliedCorrection && z < 14)
                    {
                        z++;
                        appliedCorrection++;
                    }
                    else if (targetCorrection < appliedCorrection && z > -14)
                    {
                        z--;
                        appliedCorrection--;
                    }

                    if (z > 0)
                    {
                        positiveRun += Math.Abs(z);
                        negativeRun = 0;
                        if (direction == ZDirection.Down && positiveRun > 1)
                        {
                            appliedCorrection -= zBacklashSteps;
                            direction = ZDirection.Up;
                        }
                    }
                    else if (z < 0)
                    {
                        negativeRun += Math.Abs(z);
                        positiveRun = 0;
                        if (direction == ZDirection.Up && negativeRun > 1)
                        {
                            appliedCorrection += zBacklashSteps;
                            direction = ZDirection.Down;
                        }
                    }

                    transformed.Add(new WegstrStep(sourceStep.X, sourceStep.Y, z, sourceStep.SpindleOn));
                    x += sourceStep.X;
                    y += sourceStep.Y;
                }
                packets.Add(packet[0] == WegstrPacketCodec.ManualHeader
                    ? WegstrPacketCodec.EncodeManual(transformed)
                    : WegstrPacketCodec.EncodeProgram(transformed));
            }
            transformedBlocks.Add(new WegstrCompiledBlock(block.ToolNumber, packets, block.MotionSamples));
        }

        var samples = transformedBlocks.Sum(block => block.MotionSamples);
        var packetCount = transformedBlocks.Sum(block => block.Packets.Count);
        return new WegstrCompiledProgram(program.Name, transformedBlocks, samples, packetCount, samples / SamplesPerSecond);
    }

    private static int MillimetresToSteps(double millimetres, int calibration) => (int)Math.Round(millimetres * calibration / 10d);

    private static void ValidateCalibration(int value, string name)
    {
        if (value is < 2400 or > 2600) throw new ArgumentOutOfRangeException(name, "Wegstr calibration must be between 2400 and 2600 steps per 10 mm.");
    }

    private enum ZDirection
    {
        Down,
        Up,
    }
}
