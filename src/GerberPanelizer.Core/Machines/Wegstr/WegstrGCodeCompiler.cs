using System.Globalization;
using System.Text.RegularExpressions;

namespace GerberPanelizer.Core.Machines.Wegstr;

public sealed record WegstrCompiledBlock(int ToolNumber, IReadOnlyList<byte[]> Packets, int MotionSamples);

public sealed record WegstrCompiledProgram(
    string Name,
    IReadOnlyList<WegstrCompiledBlock> Blocks,
    int MotionSamples,
    int PacketCount,
    double EstimatedDurationSeconds);

/// <summary>Compiles the linear G-code emitted by Gerber Panelizer into Wegstr 3.2.0 motion packets.</summary>
public sealed class WegstrGCodeCompiler
{
    private const double WegstrFeedDivisor = 12.142857142857142;
    private static readonly Regex CommentPattern = new(@"\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex TokenPattern = new(@"([A-Z])([+-]?(?:\d+(?:\.\d*)?|\.\d+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public WegstrCompiledProgram Compile(string name, string gCode, int stepsPerTenMillimetresX = 2500, int stepsPerTenMillimetresY = 2500, int stepsPerTenMillimetresZ = 2500, int speedPercent = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(gCode);
        ValidateCalibration(stepsPerTenMillimetresX, nameof(stepsPerTenMillimetresX));
        ValidateCalibration(stepsPerTenMillimetresY, nameof(stepsPerTenMillimetresY));
        ValidateCalibration(stepsPerTenMillimetresZ, nameof(stepsPerTenMillimetresZ));
        if (speedPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(speedPercent));

        var state = new CompilerState(stepsPerTenMillimetresX / 10d, stepsPerTenMillimetresY / 10d, stepsPerTenMillimetresZ / 10d, speedPercent);
        foreach (var sourceLine in gCode.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            CompileLine(sourceLine, state);
        }
        state.Complete();

        var blocks = state.Blocks.Where(block => block.Packets.Count > 0)
            .Select(block => new WegstrCompiledBlock(block.ToolNumber, block.Packets, block.MotionSamples)).ToList();
        if (blocks.Count == 0) throw new InvalidOperationException("The G-code did not produce any Wegstr motion packets.");
        var samples = blocks.Sum(block => block.MotionSamples);
        var packets = blocks.Sum(block => block.Packets.Count);
        return new WegstrCompiledProgram(name, blocks, samples, packets, samples / 50.595238095238095d);
    }

    private static void CompileLine(string sourceLine, CompilerState state)
    {
        var line = CommentPattern.Replace(sourceLine, string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        if (line.Length == 0 || line == "%") return;
        var tokens = TokenPattern.Matches(line).Select(match => new Token(match.Groups[1].Value[0], double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))).ToList();
        if (tokens.Count == 0) return;

        foreach (var token in tokens.Where(token => token.Letter == 'G'))
        {
            var code = token.Value;
            if (code is 0 or 1) state.MotionMode = (int)code;
            else if (code == 90) state.Absolute = true;
            else if (code == 91) state.Absolute = false;
            else if (code is 17 or 21 or 40 or 43 or 49 or 71 or 80 or 94 || Math.Abs(code - 91.1) < .00001) { }
            else throw new NotSupportedException($"G-code G{code.ToString(CultureInfo.InvariantCulture)} is not supported by direct Wegstr control.");
        }

        foreach (var token in tokens.Where(token => token.Letter == 'M'))
        {
            var code = (int)token.Value;
            if (code is 3 or 4) state.SpindleOn = true;
            else if (code is 5 or 9 or 30) state.SpindleOn = false;
            else if (code != 6) throw new NotSupportedException($"M-code M{code} is not supported by direct Wegstr control.");
        }

        var tool = tokens.LastOrDefault(token => token.Letter == 'T');
        if (tool.Letter == 'T' && tokens.Any(token => token.Letter == 'M' && (int)token.Value == 6)) state.SelectTool((int)tool.Value);
        var feed = tokens.LastOrDefault(token => token.Letter == 'F');
        if (feed.Letter == 'F') state.FeedMillimetresPerMinute = Math.Min(feed.Value, 170);

        var x = tokens.LastOrDefault(token => token.Letter == 'X');
        var y = tokens.LastOrDefault(token => token.Letter == 'Y');
        var z = tokens.LastOrDefault(token => token.Letter == 'Z');
        if (x.Letter != 'X' && y.Letter != 'Y' && z.Letter != 'Z')
        {
            if (!state.SpindleOn) state.QueueSpindleOffIfNeeded();
            return;
        }

        var targetX = ResolveTarget(x, state.XMillimetres, state.Absolute);
        var targetY = ResolveTarget(y, state.YMillimetres, state.Absolute);
        var targetZ = ResolveTarget(z, state.ZMillimetres, state.Absolute);
        state.MoveTo(targetX, targetY, targetZ, state.MotionMode == 0);
    }

    private static double ResolveTarget(Token token, double current, bool absolute) => token.Letter == default ? current : absolute ? token.Value : current + token.Value;

    private static void ValidateCalibration(int value, string name)
    {
        if (value is < 2400 or > 2600) throw new ArgumentOutOfRangeException(name, "Wegstr calibration must be between 2400 and 2600 steps per 10 mm.");
    }

    private readonly record struct Token(char Letter, double Value);

    private sealed class CompilerState
    {
        private readonly double stepsPerMillimetreX;
        private readonly double stepsPerMillimetreY;
        private readonly double stepsPerMillimetreZ;
        private readonly int speedPercent;
        private readonly List<WegstrStep> pending = [];
        private int xSteps;
        private int ySteps;
        private int zSteps;
        private bool lastPacketSpindle;

        public CompilerState(double stepsPerMillimetreX, double stepsPerMillimetreY, double stepsPerMillimetreZ, int speedPercent)
        {
            this.stepsPerMillimetreX = stepsPerMillimetreX;
            this.stepsPerMillimetreY = stepsPerMillimetreY;
            this.stepsPerMillimetreZ = stepsPerMillimetreZ;
            this.speedPercent = speedPercent;
            Blocks.Add(new MutableBlock(0));
        }

        public List<MutableBlock> Blocks { get; } = [];
        public MutableBlock Current => Blocks[^1];
        public int MotionMode { get; set; }
        public bool Absolute { get; set; } = true;
        public bool SpindleOn { get; set; }
        public double FeedMillimetresPerMinute { get; set; } = 170;
        public double XMillimetres => xSteps / stepsPerMillimetreX;
        public double YMillimetres => ySteps / stepsPerMillimetreY;
        public double ZMillimetres => zSteps / stepsPerMillimetreZ;

        public void SelectTool(int toolNumber)
        {
            if (toolNumber is < 1 or > 99) throw new InvalidOperationException($"Invalid tool number T{toolNumber}.");
            SpindleOn = false;
            QueueSpindleOffIfNeeded();
            FlushPending();
            if (Current.Packets.Count == 0 && Current.ToolNumber == 0) Current.ToolNumber = toolNumber;
            else if (Current.ToolNumber != toolNumber) Blocks.Add(new MutableBlock(toolNumber));
        }

        public void MoveTo(double xMillimetres, double yMillimetres, double zMillimetres, bool rapid)
        {
            var targetX = (int)Math.Round(xMillimetres * stepsPerMillimetreX);
            var targetY = (int)Math.Round(yMillimetres * stepsPerMillimetreY);
            var targetZ = (int)Math.Round(zMillimetres * stepsPerMillimetreZ);
            var deltaX = targetX - xSteps;
            var deltaY = targetY - ySteps;
            var deltaZ = targetZ - zSteps;
            var distance = Math.Sqrt((double)deltaX * deltaX + (double)deltaY * deltaY + (double)deltaZ * deltaZ);
            if (distance == 0) return;
            var maxComponent = Math.Max(Math.Abs(deltaX), Math.Max(Math.Abs(deltaY), Math.Abs(deltaZ)));
            var increment = rapid
                ? 14d * speedPercent / 100d / maxComponent * distance
                : Math.Clamp(FeedMillimetresPerMinute * speedPercent / 100d / WegstrFeedDivisor, .1, 14);
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
                var step = new WegstrStep(nextX - emittedX, nextY - emittedY, nextZ - emittedZ, SpindleOn);
                if (step.X != 0 || step.Y != 0 || step.Z != 0)
                {
                    step.Validate();
                    Queue(step);
                    emittedX = nextX;
                    emittedY = nextY;
                    emittedZ = nextZ;
                }
            }
            xSteps = targetX;
            ySteps = targetY;
            zSteps = targetZ;
        }

        public void QueueSpindleOffIfNeeded()
        {
            if (!lastPacketSpindle && pending.All(step => !step.SpindleOn)) return;
            Queue(WegstrStep.Idle());
            Queue(WegstrStep.Idle());
            Queue(WegstrStep.Idle());
        }

        public void Complete()
        {
            SpindleOn = false;
            QueueSpindleOffIfNeeded();
            FlushPending();
        }

        private void Queue(WegstrStep step)
        {
            pending.Add(step);
            Current.MotionSamples++;
            if (pending.Count == 3) FlushPending();
        }

        private void FlushPending()
        {
            if (pending.Count == 0) return;
            while (pending.Count < 3) pending.Add(WegstrStep.Idle(SpindleOn));
            Current.Packets.Add(WegstrPacketCodec.EncodeProgram(pending));
            lastPacketSpindle = pending[^1].SpindleOn;
            pending.Clear();
        }
    }

    private sealed class MutableBlock(int toolNumber)
    {
        public int ToolNumber { get; set; } = toolNumber;
        public List<byte[]> Packets { get; } = [];
        public int MotionSamples { get; set; }
    }
}
