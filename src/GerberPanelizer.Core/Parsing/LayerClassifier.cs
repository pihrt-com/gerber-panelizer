using GerberPanelizer.Core.Models;

namespace GerberPanelizer.Core.Parsing;

public static class LayerClassifier
{
    private static readonly string[] GerberExtensions = [".gbr", ".ger", ".gtl", ".gbl", ".gko", ".gto", ".gbo", ".gts", ".gbs", ".gtp", ".gbp"];
    private static readonly string[] ExcellonExtensions = [".drl", ".xln", ".exc", ".txt"];

    public static FileFormat DetectFormat(string path, string? content = null)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (GerberExtensions.Contains(extension)) return FileFormat.Gerber;
        if (ExcellonExtensions.Contains(extension) && (content is null || LooksLikeExcellon(content))) return FileFormat.Excellon;
        if (!string.IsNullOrWhiteSpace(content))
        {
            if (LooksLikeGerber(content)) return FileFormat.Gerber;
            if (LooksLikeExcellon(content)) return FileFormat.Excellon;
        }

        return FileFormat.Unsupported;
    }

    public static ManufacturingLayerKind DetectLayer(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        if (name.Contains("drill") || name.Contains("drill_")) return ManufacturingLayerKind.Drill;
        if (name.Contains("profile") || name.Contains("outline") || name.Contains("board_edge") || name.Contains("edge_cuts")) return ManufacturingLayerKind.Profile;
        if (name.Contains("soldermask_top") || name.Contains("mask_top") || extension == ".gts") return ManufacturingLayerKind.SolderMaskTop;
        if (name.Contains("soldermask_bottom") || name.Contains("mask_bottom") || extension == ".gbs") return ManufacturingLayerKind.SolderMaskBottom;
        if (name.Contains("silkscreen_top") || name.Contains("legend_top") || extension == ".gto") return ManufacturingLayerKind.SilkScreenTop;
        if (name.Contains("silkscreen_bottom") || name.Contains("legend_bottom") || extension == ".gbo") return ManufacturingLayerKind.SilkScreenBottom;
        if (name.Contains("solderpaste_top") || name.Contains("paste_top") || extension == ".gtp") return ManufacturingLayerKind.PasteTop;
        if (name.Contains("solderpaste_bottom") || name.Contains("paste_bottom") || extension == ".gbp") return ManufacturingLayerKind.PasteBottom;
        if (name.Contains("copper_top") || name is "top" || name.EndsWith("_top") || extension == ".gtl") return ManufacturingLayerKind.CopperTop;
        if (name.Contains("copper_bottom") || name is "bottom" || name.EndsWith("_bottom") || extension == ".gbl") return ManufacturingLayerKind.CopperBottom;
        return ManufacturingLayerKind.Unknown;
    }

    private static bool LooksLikeGerber(string content) => content.Contains("%FSL", StringComparison.OrdinalIgnoreCase) || content.Contains("%ADD", StringComparison.OrdinalIgnoreCase);
    private static bool LooksLikeExcellon(string content) => content.StartsWith("M48", StringComparison.OrdinalIgnoreCase) || content.Contains("METRIC", StringComparison.OrdinalIgnoreCase) && content.Contains("T1C", StringComparison.OrdinalIgnoreCase);
}
