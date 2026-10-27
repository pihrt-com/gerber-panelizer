namespace GerberPanelizer.Core.Machines.Wegstr;

public enum WegstrAxis
{
    X,
    Y,
    Z,
}

/// <summary>Creates manual-motion frames with the same stepping and acceleration rules as Wegstr 3.2.0.</summary>
public sealed class WegstrManualMotionPlanner
{
    private int currentMagnitude;
    private double fractionalRemainder;

    public byte[] NextContinuousPacket(WegstrAxis axis, int direction, int speedPercent, bool spindleOn)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (speedPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(speedPercent));

        var targetMagnitude = speedPercent == 1 ? 14d * .003d : 14d * speedPercent / 100d;
        var steps = new WegstrStep[3];
        for (var index = 0; index < steps.Length; index++)
        {
            var magnitude = NextMagnitude(targetMagnitude);
            steps[index] = CreateStep(axis, magnitude * direction, spindleOn);
        }
        return WegstrPacketCodec.EncodeManual(steps);
    }

    public static IReadOnlyList<byte[]> PlanFiniteMove(int deltaX, int deltaY, int deltaZ, int speedPercent, bool spindleOn)
    {
        if (speedPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(speedPercent));
        if (deltaX == 0 && deltaY == 0 && deltaZ == 0) return [];

        var distance = Math.Sqrt((double)deltaX * deltaX + (double)deltaY * deltaY + (double)deltaZ * deltaZ);
        var increment = speedPercent == 1 ? 1d : Math.Max(1d, 14d * speedPercent / 100d);
        var samples = new List<WegstrStep>();
        var travelled = 0d;
        var emittedX = 0;
        var emittedY = 0;
        var emittedZ = 0;
        while (travelled < distance)
        {
            travelled = Math.Min(distance, travelled + increment);
            var nextX = (int)Math.Round(deltaX / distance * travelled);
            var nextY = (int)Math.Round(deltaY / distance * travelled);
            var nextZ = (int)Math.Round(deltaZ / distance * travelled);
            var step = new WegstrStep(nextX - emittedX, nextY - emittedY, nextZ - emittedZ, spindleOn);
            if (step.X != 0 || step.Y != 0 || step.Z != 0)
            {
                step.Validate();
                samples.Add(step);
                emittedX = nextX;
                emittedY = nextY;
                emittedZ = nextZ;
            }
        }

        var packets = new List<byte[]>();
        for (var index = 0; index < samples.Count; index += 3)
        {
            var packetSamples = samples.Skip(index).Take(3).ToList();
            while (packetSamples.Count < 3) packetSamples.Add(WegstrStep.Idle(spindleOn));
            packets.Add(WegstrPacketCodec.EncodeManual(packetSamples));
        }
        return packets;
    }

    public void Reset()
    {
        currentMagnitude = 0;
        fractionalRemainder = 0;
    }

    private int NextMagnitude(double targetMagnitude)
    {
        if (targetMagnitude < 1)
        {
            return 1;
        }
        if (currentMagnitude + 3 <= 10 && currentMagnitude + 3 <= targetMagnitude)
        {
            currentMagnitude += 3;
        }
        else if (currentMagnitude + 1 >= 10 && currentMagnitude + 1 <= targetMagnitude)
        {
            currentMagnitude++;
        }
        else if (targetMagnitude < 10)
        {
            currentMagnitude = (int)Math.Floor(targetMagnitude);
        }

        var fractional = targetMagnitude - currentMagnitude;
        if (fractional >= 1) return currentMagnitude;
        fractionalRemainder += fractional;
        if (fractionalRemainder >= 1)
        {
            fractionalRemainder -= 1;
            return currentMagnitude + 1;
        }
        return currentMagnitude;
    }

    private static WegstrStep CreateStep(WegstrAxis axis, int value, bool spindleOn) => axis switch
    {
        WegstrAxis.X => new WegstrStep(value, 0, 0, spindleOn),
        WegstrAxis.Y => new WegstrStep(0, value, 0, spindleOn),
        WegstrAxis.Z => new WegstrStep(0, 0, value, spindleOn),
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };
}
