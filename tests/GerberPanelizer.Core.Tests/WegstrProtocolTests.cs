using GerberPanelizer.Core.Machines.Wegstr;
using Xunit;

namespace GerberPanelizer.Core.Tests;

public sealed class WegstrProtocolTests
{
    [Fact]
    public void PacketCodecMatchesWegstr320BitLayout()
    {
        var packet = WegstrPacketCodec.EncodeProgram([
            new WegstrStep(1, -2, 3, true),
            new WegstrStep(-14, 14, -8, false),
            new WegstrStep(0, 0, 14, true),
        ]);

        Assert.Equal(new byte[] { 0xFF, 0x61, 0x92, 0x1E, 0x6E, 0xC0, 0xA0 }, packet);
        Assert.Equal(new byte[] { 0x3F, 0, 0, 0, 0, 0, 0 }, WegstrPacketCodec.SpindleOffPacket());
    }

    [Theory]
    [InlineData(15, 0, 0)]
    [InlineData(0, -15, 0)]
    [InlineData(0, 0, 15)]
    public void PacketCodecRejectsUnsafeStepMagnitude(int x, int y, int z)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WegstrPacketCodec.EncodeProgram([
            new WegstrStep(x, y, z, false), WegstrStep.Idle(), WegstrStep.Idle(),
        ]));
    }

    [Fact]
    public void PacketCodecRoundTripsEverySignAndSpindleBit()
    {
        var expected = new[]
        {
            new WegstrStep(-14, 13, -12, true),
            new WegstrStep(11, -10, 9, false),
            new WegstrStep(0, 0, 0, true),
        };

        var packet = WegstrPacketCodec.EncodeManual(expected);

        Assert.Equal(WegstrPacketCodec.ManualHeader, packet[0]);
        Assert.Equal(expected, WegstrPacketCodec.Decode(packet));
    }

    [Fact]
    public void SpindlePacketsNeverRequestMotion()
    {
        var on = WegstrPacketCodec.Decode(WegstrPacketCodec.SpindlePacket(true));
        var off = WegstrPacketCodec.Decode(WegstrPacketCodec.SpindleOffPacket());

        Assert.All(on, step => Assert.Equal(new WegstrStep(0, 0, 0, true), step));
        Assert.All(off, step => Assert.Equal(WegstrStep.Idle(), step));
    }

    [Fact]
    public void ProbeResponseWaitsForAndConsumesBothPositionBytes()
    {
        Assert.False(WegstrResponseCodec.TryDecode(new byte[] { 4, 0x12 }, out _, out var incompleteConsumed));
        Assert.Equal(0, incompleteConsumed);

        Assert.True(WegstrResponseCodec.TryDecode(new byte[] { 4, 0xFE, 0xDC, 1 }, out var response, out var consumed));
        Assert.Equal(WegstrMachineStatus.Probe, response.Status);
        Assert.Equal(unchecked((short)0xFEDC), response.ProbePositionSteps);
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void CompilerProducesBoundedPacketsAndExactDestination()
    {
        const string gCode = """
            G00G21G17G90G40G49G80
            T1M06
            G00X0.000Y0.000Z2.000
            S12000M03
            G1X1.000Y-0.500Z-0.080F170.0
            G00Z2.000
            M30
            %
            """;

        var result = new WegstrGCodeCompiler().Compile("test", gCode);

        Assert.Single(result.Blocks);
        Assert.True(result.PacketCount > 0);
        var decoded = result.Blocks.SelectMany(block => block.Packets).SelectMany(packet => Decode(packet)).ToList();
        Assert.All(decoded, step =>
        {
            Assert.InRange(step.X, -14, 14);
            Assert.InRange(step.Y, -14, 14);
            Assert.InRange(step.Z, -14, 14);
        });
        Assert.Equal(250, decoded.Sum(step => step.X));
        Assert.Equal(-125, decoded.Sum(step => step.Y));
        Assert.Equal(500, decoded.Sum(step => step.Z));
        Assert.False(decoded[^1].SpindleOn);
    }

    [Fact]
    public void CompilerSplitsProgramAtToolChanges()
    {
        const string gCode = """
            G21G90
            T1M06
            G00X1
            T2M06
            G00X2
            M30
            """;

        var result = new WegstrGCodeCompiler().Compile("drill", gCode);

        Assert.Equal([1, 2], result.Blocks.Select(block => block.ToolNumber));
        Assert.All(result.Blocks, block => Assert.NotEmpty(block.Packets));
    }

    [Fact]
    public void CompilerRejectsUnsupportedMotionInsteadOfApproximatingIt()
    {
        Assert.Throws<NotSupportedException>(() => new WegstrGCodeCompiler().Compile("arc", "G21G90\nG02X1Y1I0J1"));
    }

    [Fact]
    public void ManualContinuousJogUsesManualHeaderAndWegstrMaximumAtRapidSpeed()
    {
        var planner = new WegstrManualMotionPlanner();
        var packets = Enumerable.Range(0, 4).Select(_ => planner.NextContinuousPacket(WegstrAxis.X, 1, 100, false)).ToList();
        var decoded = packets.SelectMany(packet => Decode(packet, WegstrPacketCodec.ManualHeader)).ToList();

        Assert.All(packets, packet => Assert.Equal(WegstrPacketCodec.ManualHeader, packet[0]));
        Assert.Equal([3, 6, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14], decoded.Select(step => step.X));
        Assert.All(decoded, step => Assert.InRange(step.X, -14, 14));
    }

    [Fact]
    public void ManualSteppingModeProducesOneThreeStepPacketLikeWegstr320()
    {
        var packet = new WegstrManualMotionPlanner().NextContinuousPacket(WegstrAxis.Z, -1, 1, false);

        Assert.Equal([-1, -1, -1], Decode(packet, WegstrPacketCodec.ManualHeader).Select(step => step.Z));
    }

    [Fact]
    public void ManualFiniteMoveReachesExactDestinationWithBoundedSamples()
    {
        var packets = WegstrManualMotionPlanner.PlanFiniteMove(250, -125, 25, 100, true);
        var decoded = packets.SelectMany(packet => Decode(packet, WegstrPacketCodec.ManualHeader)).ToList();

        Assert.Equal(250, decoded.Sum(step => step.X));
        Assert.Equal(-125, decoded.Sum(step => step.Y));
        Assert.Equal(25, decoded.Sum(step => step.Z));
        Assert.All(decoded, step =>
        {
            Assert.InRange(step.X, -14, 14);
            Assert.InRange(step.Y, -14, 14);
            Assert.InRange(step.Z, -14, 14);
            Assert.True(step.SpindleOn);
        });
    }

    private static IEnumerable<WegstrStep> Decode(byte[] packet, byte expectedHeader = WegstrPacketCodec.ProgramHeader)
    {
        Assert.Equal(expectedHeader, packet[0]);
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
            yield return new WegstrStep(x, y, z, (yByte & 0x80) != 0);
        }
    }
}
