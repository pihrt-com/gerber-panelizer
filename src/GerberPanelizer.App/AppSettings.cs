using System.Text.Json;
using System.IO;

namespace GerberPanelizer.App;

public sealed class AppSettings
{
    public string Language { get; set; } = "cs";
    public double WorkAreaWidthMm { get; set; } = 200;
    public double WorkAreaHeightMm { get; set; } = 130;
    public double BoardSpacingMm { get; set; } = 3;
    public double SafetyMarginMm { get; set; } = 5;
    public bool AllowRotation { get; set; } = true;
    public string DrillColor { get; set; } = "#EF8A2F";
    public string TopLayerColor { get; set; } = "#D74F4A";
    public string BottomLayerColor { get; set; } = "#3478C5";
    public string OutlineColor { get; set; } = "#34434B";
    public string SilkscreenColor { get; set; } = "#F6F1D6";
    public string BottomMirrorPolicy { get; set; } = "Off";
    public double WegstrMaterialThicknessMm { get; set; } = 1.5;
    public double WegstrSafeZMm { get; set; } = 2;
    public double WegstrIsolationDepthMm { get; set; } = -0.08;
    public double WegstrIsolationOffsetMm { get; set; } = 0.1;
    public double WegstrOutlineDepthMm { get; set; } = -1.7;
    public double WegstrOutlineToolDiameterMm { get; set; } = 1;
    public double WegstrDrillingDepthMm { get; set; } = -1.8;
    public double WegstrMillingFeedMmPerMinute { get; set; } = 170;
    public double WegstrPlungeFeedMmPerMinute { get; set; } = 170;
    public int WegstrSpindleRpm { get; set; } = 12000;
    public int WegstrMillingToolNumber { get; set; } = 1;
    public bool WegstrIncludeDrilling { get; set; } = true;
    public bool WegstrIncludeOutline { get; set; }
}

public static class AppSettingsService
{
    private static readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GerberPanelizer", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(settingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
