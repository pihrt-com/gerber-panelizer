using GerberPanelizer.Core.Geometry;

namespace GerberPanelizer.Core.Models;

public enum ManufacturingLayerKind
{
    Unknown,
    Profile,
    Drill,
    CopperTop,
    CopperBottom,
    SolderMaskTop,
    SolderMaskBottom,
    SilkScreenTop,
    SilkScreenBottom,
    PasteTop,
    PasteBottom,
}

public enum FileFormat
{
    Gerber,
    Excellon,
    Unsupported,
}

public sealed record ManufacturingFile(
    string Name,
    string SourcePath,
    ManufacturingLayerKind Layer,
    FileFormat Format,
    string Content,
    PcbBounds Bounds,
    IReadOnlyList<string> Warnings);

public sealed record ImportedBoard(
    string Id,
    string DisplayName,
    IReadOnlyList<ManufacturingFile> Files,
    PcbBounds Bounds)
{
    /// <summary>Files selected by the user to create this board. These are retained for portable project files.</summary>
    public IReadOnlyList<string> SourceFilePaths { get; init; } = Array.Empty<string>();
    public PcbSize Size => new(Bounds.Width, Bounds.Height);
}

public sealed record ImportIssue(string Source, string Message, bool IsError);

public sealed record ImportResult(IReadOnlyList<ImportedBoard> Boards, IReadOnlyList<ImportIssue> Issues);
