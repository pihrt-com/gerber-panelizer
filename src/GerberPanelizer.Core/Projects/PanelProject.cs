namespace GerberPanelizer.Core.Projects;

/// <summary>
/// Portable project description. Source paths are stored relative to the project file,
/// so the project directory and its manufacturing data can be moved together.
/// </summary>
public sealed class PanelProject
{
    public int FormatVersion { get; set; } = 1;
    public string ManufacturingSide { get; set; } = "Both";
    public double WorkAreaWidthMm { get; set; }
    public double WorkAreaHeightMm { get; set; }
    public List<PanelProjectBoard> Boards { get; set; } = [];
    public List<PanelProjectPlacement> Placements { get; set; } = [];
}

public sealed class PanelProjectBoard
{
    public string InstanceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsIncluded { get; set; } = true;
    public List<string> SourcePaths { get; set; } = [];
}

public sealed class PanelProjectPlacement
{
    public string BoardInstanceId { get; set; } = string.Empty;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double RotationDegrees { get; set; }
}
