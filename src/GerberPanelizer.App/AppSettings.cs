using System.Text.Json;
using System.IO;

namespace GerberPanelizer.App;

public sealed class AppSettings
{
    public string Language { get; set; } = "cs";
    public double WorkAreaWidthMm { get; set; } = 200;
    public double WorkAreaHeightMm { get; set; } = 130;
    public List<WorkAreaPreset> WorkAreaPresets { get; set; } = [];
    public string? ActiveWorkAreaPresetName { get; set; }
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
    public int WegstrCalibrationX { get; set; } = 2500;
    public int WegstrCalibrationY { get; set; } = 2500;
    public int WegstrCalibrationZ { get; set; } = 2500;
    public int WegstrSpeedPercent { get; set; } = 100;
    public double WegstrAutoLevelRasterMm { get; set; } = 1;
    public double WegstrAutoLevelSafeZMm { get; set; } = 1;
    public int WegstrAutoLevelZBacklashSteps { get; set; } = 4;
    public bool WegstrIncludeDrilling { get; set; } = true;
    public bool WegstrIncludeOutline { get; set; }
    public bool WegstrIncludeOutlineInCopper { get; set; } = true;
}

public sealed class WorkAreaPreset
{
    public string Name { get; set; } = string.Empty;
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }
}

public static class AppSettingsService
{
    private static readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GerberPanelizer", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var settings = File.Exists(settingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new AppSettings()
                : new AppSettings();
            NormalizeWorkAreaPresets(settings);
            return settings;
        }
        catch (JsonException exception)
        {
            AppLog.Warning("The settings file contains invalid JSON. Default settings will be used.", exception);
            return new AppSettings();
        }
        catch (IOException exception)
        {
            AppLog.Warning("The settings file could not be read. Default settings will be used.", exception);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        NormalizeWorkAreaPresets(settings);
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void NormalizeWorkAreaPresets(AppSettings settings)
    {
        var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        settings.WorkAreaPresets = (settings.WorkAreaPresets ?? [])
            .Where(preset => !string.IsNullOrWhiteSpace(preset.Name) && preset.WidthMm > 0 && preset.HeightMm > 0)
            .Select(preset => new WorkAreaPreset { Name = preset.Name.Trim(), WidthMm = preset.WidthMm, HeightMm = preset.HeightMm })
            .Where(preset => uniqueNames.Add(preset.Name))
            .ToList();

        if (settings.WorkAreaPresets.Count == 0)
        {
            settings.WorkAreaPresets.Add(new WorkAreaPreset
            {
                Name = "Výchozí pracovní plocha",
                WidthMm = settings.WorkAreaWidthMm > 0 ? settings.WorkAreaWidthMm : 200,
                HeightMm = settings.WorkAreaHeightMm > 0 ? settings.WorkAreaHeightMm : 130,
            });
        }

        var activePreset = settings.WorkAreaPresets.FirstOrDefault(preset => string.Equals(preset.Name, settings.ActiveWorkAreaPresetName, StringComparison.OrdinalIgnoreCase))
            ?? settings.WorkAreaPresets.First();
        settings.ActiveWorkAreaPresetName = activePreset.Name;
        settings.WorkAreaWidthMm = activePreset.WidthMm;
        settings.WorkAreaHeightMm = activePreset.HeightMm;
    }
}
