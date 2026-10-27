namespace GerberPanelizer.Core.Machines.Wegstr;

public readonly record struct WegstrStep(int X, int Y, int Z, bool SpindleOn)
{
    public static WegstrStep Idle(bool spindleOn = false) => new(0, 0, 0, spindleOn);

    public void Validate()
    {
        if (Math.Abs(X) > 14 || Math.Abs(Y) > 14 || Math.Abs(Z) > 14)
        {
            throw new ArgumentOutOfRangeException(nameof(WegstrStep), "Each Wegstr sample is limited to 14 steps per axis.");
        }
    }
}

public enum WegstrMachineStatus : byte
{
    Ready = 1,
    SpindleOverload = 2,
    MachineReleased = 3,
    Probe = 4,
}

public readonly record struct WegstrControllerResponse(WegstrMachineStatus Status, short? ProbePositionSteps);

public static class WegstrResponseCodec
{
    public static bool TryDecode(IReadOnlyList<byte> bytes, out WegstrControllerResponse response, out int consumed)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        response = default;
        consumed = 0;
        if (bytes.Count == 0) return false;

        var status = (WegstrMachineStatus)bytes[0];
        if (status != WegstrMachineStatus.Probe)
        {
            response = new(status, null);
            consumed = 1;
            return true;
        }
        if (bytes.Count < 3) return false;

        response = new(status, unchecked((short)(bytes[1] << 8 | bytes[2])));
        consumed = 3;
        return true;
    }
}

/// <summary>Encoder for the seven-byte motion frames used by Wegstr 3.2.0.</summary>
public static class WegstrPacketCodec
{
    public const byte ProgramHeader = 0xFF;
    public const byte ManualHeader = 0x3F;
    public const int PacketLength = 7;

    public static byte[] EncodeProgram(IReadOnlyList<WegstrStep> steps) => Encode(ProgramHeader, steps);
    public static byte[] EncodeManual(IReadOnlyList<WegstrStep> steps) => Encode(ManualHeader, steps);

    public static byte[] SpindlePacket(bool spindleOn) => EncodeManual([WegstrStep.Idle(spindleOn), WegstrStep.Idle(spindleOn), WegstrStep.Idle(spindleOn)]);
    public static byte[] SpindleOffPacket() => SpindlePacket(false);

    public static IReadOnlyList<WegstrStep> Decode(byte[] packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Length != PacketLength) throw new ArgumentException("A Wegstr packet must contain exactly seven bytes.", nameof(packet));
        if (packet[0] is not (ProgramHeader or ManualHeader)) throw new ArgumentException("Unknown Wegstr packet header.", nameof(packet));

        var steps = new List<WegstrStep>(3);
        for (var index = 1; index < packet.Length; index += 2)
        {
            var xByte = packet[index];
            var yByte = packet[index + 1];
            var x = xByte & 0x0F;
            var y = yByte & 0x0F;
            var z = (xByte >> 5) & 0x07;
            if ((yByte & 0x20) != 0) z += 8;
            if ((xByte & 0x10) != 0) x = -x;
            if ((yByte & 0x10) != 0) y = -y;
            if ((yByte & 0x40) != 0) z = -z;
            steps.Add(new WegstrStep(x, y, z, (yByte & 0x80) != 0));
        }
        return steps;
    }

    private static byte[] Encode(byte header, IReadOnlyList<WegstrStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count != 3) throw new ArgumentException("A Wegstr packet must contain exactly three samples.", nameof(steps));

        var packet = new byte[PacketLength];
        packet[0] = header;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            step.Validate();
            var x = Math.Abs(step.X);
            var y = Math.Abs(step.Y);
            var z = Math.Abs(step.Z);
            if (step.X < 0) x |= 0x10;
            if (step.Y < 0) y |= 0x10;
            if (step.Z < 0) y |= 0x40;
            if (z >= 8)
            {
                x |= (z - 8) << 5;
                y |= 0x20;
            }
            else
            {
                x |= z << 5;
            }
            if (step.SpindleOn) y |= 0x80;
            packet[1 + index * 2] = (byte)x;
            packet[2 + index * 2] = (byte)y;
        }
        return packet;
    }
}
