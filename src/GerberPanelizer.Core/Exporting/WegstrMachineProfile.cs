namespace GerberPanelizer.Core.Exporting;

public sealed record WegstrMachineProfile
{
    public double WorkAreaWidthMillimetres { get; init; } = 200;
    public double WorkAreaHeightMillimetres { get; init; } = 130;
    public double MaterialThicknessMillimetres { get; init; } = 1.5;
    public double SafeZMillimetres { get; init; } = 2;
    public double IsolationDepthMillimetres { get; init; } = -0.08;
    public double IsolationOffsetMillimetres { get; init; } = 0.1;
    public double OutlineDepthMillimetres { get; init; } = -1.7;
    public double OutlineToolDiameterMillimetres { get; init; } = 1;
    public double DrillingDepthMillimetres { get; init; } = -1.8;
    public double MillingFeedMillimetresPerMinute { get; init; } = 170;
    public double PlungeFeedMillimetresPerMinute { get; init; } = 170;
    public int SpindleRpm { get; init; } = 12000;
    public int MillingToolNumber { get; init; } = 1;
    public bool IncludeDrilling { get; init; } = true;
    public bool IncludeOutline { get; init; }

    public void Validate()
    {
        if (WorkAreaWidthMillimetres <= 0 || WorkAreaHeightMillimetres <= 0) throw new ArgumentOutOfRangeException(nameof(WorkAreaWidthMillimetres), "Wegstr work-area dimensions must be greater than zero.");
        if (MaterialThicknessMillimetres <= 0) throw new ArgumentOutOfRangeException(nameof(MaterialThicknessMillimetres), "Wegstr material thickness must be greater than zero.");
        if (SafeZMillimetres <= 0) throw new ArgumentOutOfRangeException(nameof(SafeZMillimetres), "Wegstr safe Z must be greater than zero.");
        if (IsolationDepthMillimetres >= 0 || OutlineDepthMillimetres >= 0 || DrillingDepthMillimetres >= 0) throw new ArgumentOutOfRangeException(nameof(IsolationDepthMillimetres), "Wegstr cutting depths must be below zero.");
        if (IsolationOffsetMillimetres <= 0) throw new ArgumentOutOfRangeException(nameof(IsolationOffsetMillimetres), "Wegstr isolation offset must be greater than zero.");
        if (OutlineToolDiameterMillimetres <= 0) throw new ArgumentOutOfRangeException(nameof(OutlineToolDiameterMillimetres), "Wegstr outline tool diameter must be greater than zero.");
        if (MillingFeedMillimetresPerMinute <= 0 || PlungeFeedMillimetresPerMinute <= 0) throw new ArgumentOutOfRangeException(nameof(MillingFeedMillimetresPerMinute), "Wegstr feed rates must be greater than zero.");
        if (SpindleRpm is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(SpindleRpm), "Wegstr spindle speed must be between 1 and 100000 RPM.");
        if (MillingToolNumber is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(MillingToolNumber), "Wegstr milling tool number must be between 1 and 99.");
    }
}
