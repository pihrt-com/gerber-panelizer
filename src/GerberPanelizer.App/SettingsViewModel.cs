using System.Windows;
using System.Text.Json;
using System.IO;
using Microsoft.Win32;
using System.Windows.Media;
using System.Collections.ObjectModel;

namespace GerberPanelizer.App;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Window window;
    private string language;
    private double boardSpacingMm;
    private double safetyMarginMm;
    private bool allowRotation;
    private string bottomMirrorPolicy;
    private double wegstrMaterialThicknessMm;
    private double wegstrSafeZMm;
    private double wegstrIsolationDepthMm;
    private double wegstrIsolationOffsetMm;
    private double wegstrOutlineDepthMm;
    private double wegstrOutlineToolDiameterMm;
    private double wegstrDrillingDepthMm;
    private double wegstrMillingFeedMmPerMinute;
    private double wegstrPlungeFeedMmPerMinute;
    private int wegstrSpindleRpm;
    private int wegstrMillingToolNumber;
    private int wegstrCalibrationX;
    private int wegstrCalibrationY;
    private int wegstrCalibrationZ;
    private int wegstrSpeedPercent;
    private double wegstrAutoLevelRasterMm;
    private double wegstrAutoLevelSafeZMm;
    private int wegstrAutoLevelZBacklashSteps;
    private bool wegstrIncludeDrilling;
    private bool wegstrIncludeOutline;
    private bool wegstrIncludeOutlineInCopper;
    private WorkAreaPresetSettingViewModel? selectedWorkAreaPreset;

    public SettingsViewModel(AppSettings settings, Window window)
    {
        this.window = window;
        AppSettingsService.NormalizeWorkAreaPresets(settings);
        language = settings.Language;
        boardSpacingMm = settings.BoardSpacingMm;
        safetyMarginMm = settings.SafetyMarginMm;
        allowRotation = settings.AllowRotation;
        LayerColors = new ObservableCollection<LayerColorSettingViewModel>
        {
            new("LayerDrill", settings.DrillColor, "#EF8A2F"),
            new("LayerTop", settings.TopLayerColor, "#D74F4A"),
            new("LayerBottom", settings.BottomLayerColor, "#3478C5"),
            new("LayerOutline", settings.OutlineColor, "#34434B"),
            new("LayerSilkscreen", settings.SilkscreenColor, "#F6F1D6"),
        };
        bottomMirrorPolicy = settings.BottomMirrorPolicy;
        wegstrMaterialThicknessMm = settings.WegstrMaterialThicknessMm;
        wegstrSafeZMm = settings.WegstrSafeZMm;
        wegstrIsolationDepthMm = settings.WegstrIsolationDepthMm;
        wegstrIsolationOffsetMm = settings.WegstrIsolationOffsetMm;
        wegstrOutlineDepthMm = settings.WegstrOutlineDepthMm;
        wegstrOutlineToolDiameterMm = settings.WegstrOutlineToolDiameterMm;
        wegstrDrillingDepthMm = settings.WegstrDrillingDepthMm;
        wegstrMillingFeedMmPerMinute = settings.WegstrMillingFeedMmPerMinute;
        wegstrPlungeFeedMmPerMinute = settings.WegstrPlungeFeedMmPerMinute;
        wegstrSpindleRpm = settings.WegstrSpindleRpm;
        wegstrMillingToolNumber = settings.WegstrMillingToolNumber;
        wegstrCalibrationX = settings.WegstrCalibrationX;
        wegstrCalibrationY = settings.WegstrCalibrationY;
        wegstrCalibrationZ = settings.WegstrCalibrationZ;
        wegstrSpeedPercent = settings.WegstrSpeedPercent;
        wegstrAutoLevelRasterMm = settings.WegstrAutoLevelRasterMm;
        wegstrAutoLevelSafeZMm = settings.WegstrAutoLevelSafeZMm;
        wegstrAutoLevelZBacklashSteps = settings.WegstrAutoLevelZBacklashSteps;
        wegstrIncludeDrilling = settings.WegstrIncludeDrilling;
        wegstrIncludeOutline = settings.WegstrIncludeOutline;
        wegstrIncludeOutlineInCopper = settings.WegstrIncludeOutlineInCopper;
        WorkAreaPresets = new ObservableCollection<WorkAreaPresetSettingViewModel>();
        LoadWorkAreaPresets(settings);
        SaveCommand = new RelayCommand(_ => Save());
        PickColorCommand = new RelayCommand(color => PickColor(color as LayerColorSettingViewModel));
        AddWorkAreaPresetCommand = new RelayCommand(_ => AddWorkAreaPreset());
        RemoveWorkAreaPresetCommand = new RelayCommand(preset => RemoveWorkAreaPreset(preset as WorkAreaPresetSettingViewModel), _ => WorkAreaPresets.Count > 1);
        ExportSettingsCommand = new RelayCommand(_ => ExportSettings());
        ImportSettingsCommand = new RelayCommand(_ => ImportSettings());
    }

    public string Language { get => language; set => SetProperty(ref language, value); }
    public double BoardSpacingMm { get => boardSpacingMm; set => SetProperty(ref boardSpacingMm, value); }
    public double SafetyMarginMm { get => safetyMarginMm; set => SetProperty(ref safetyMarginMm, value); }
    public bool AllowRotation { get => allowRotation; set => SetProperty(ref allowRotation, value); }
    public string BottomMirrorPolicy { get => bottomMirrorPolicy; set => SetProperty(ref bottomMirrorPolicy, value); }
    public double WegstrMaterialThicknessMm { get => wegstrMaterialThicknessMm; set => SetProperty(ref wegstrMaterialThicknessMm, value); }
    public double WegstrSafeZMm { get => wegstrSafeZMm; set => SetProperty(ref wegstrSafeZMm, value); }
    public double WegstrIsolationDepthMm { get => wegstrIsolationDepthMm; set => SetProperty(ref wegstrIsolationDepthMm, value); }
    public double WegstrIsolationOffsetMm { get => wegstrIsolationOffsetMm; set => SetProperty(ref wegstrIsolationOffsetMm, value); }
    public double WegstrOutlineDepthMm { get => wegstrOutlineDepthMm; set => SetProperty(ref wegstrOutlineDepthMm, value); }
    public double WegstrOutlineToolDiameterMm { get => wegstrOutlineToolDiameterMm; set => SetProperty(ref wegstrOutlineToolDiameterMm, value); }
    public double WegstrDrillingDepthMm { get => wegstrDrillingDepthMm; set => SetProperty(ref wegstrDrillingDepthMm, value); }
    public double WegstrMillingFeedMmPerMinute { get => wegstrMillingFeedMmPerMinute; set => SetProperty(ref wegstrMillingFeedMmPerMinute, value); }
    public double WegstrPlungeFeedMmPerMinute { get => wegstrPlungeFeedMmPerMinute; set => SetProperty(ref wegstrPlungeFeedMmPerMinute, value); }
    public int WegstrSpindleRpm { get => wegstrSpindleRpm; set => SetProperty(ref wegstrSpindleRpm, value); }
    public int WegstrMillingToolNumber { get => wegstrMillingToolNumber; set => SetProperty(ref wegstrMillingToolNumber, value); }
    public int WegstrCalibrationX { get => wegstrCalibrationX; set => SetProperty(ref wegstrCalibrationX, value); }
    public int WegstrCalibrationY { get => wegstrCalibrationY; set => SetProperty(ref wegstrCalibrationY, value); }
    public int WegstrCalibrationZ { get => wegstrCalibrationZ; set => SetProperty(ref wegstrCalibrationZ, value); }
    public int WegstrSpeedPercent { get => wegstrSpeedPercent; set => SetProperty(ref wegstrSpeedPercent, value); }
    public double WegstrAutoLevelRasterMm { get => wegstrAutoLevelRasterMm; set => SetProperty(ref wegstrAutoLevelRasterMm, value); }
    public double WegstrAutoLevelSafeZMm { get => wegstrAutoLevelSafeZMm; set => SetProperty(ref wegstrAutoLevelSafeZMm, value); }
    public int WegstrAutoLevelZBacklashSteps { get => wegstrAutoLevelZBacklashSteps; set => SetProperty(ref wegstrAutoLevelZBacklashSteps, value); }
    public bool WegstrIncludeDrilling { get => wegstrIncludeDrilling; set => SetProperty(ref wegstrIncludeDrilling, value); }
    public bool WegstrIncludeOutline { get => wegstrIncludeOutline; set => SetProperty(ref wegstrIncludeOutline, value); }
    public bool WegstrIncludeOutlineInCopper { get => wegstrIncludeOutlineInCopper; set => SetProperty(ref wegstrIncludeOutlineInCopper, value); }
    public ObservableCollection<WorkAreaPresetSettingViewModel> WorkAreaPresets { get; }
    public WorkAreaPresetSettingViewModel? SelectedWorkAreaPreset { get => selectedWorkAreaPreset; set => SetProperty(ref selectedWorkAreaPreset, value); }
    public ObservableCollection<LayerColorSettingViewModel> LayerColors { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand PickColorCommand { get; }
    public RelayCommand AddWorkAreaPresetCommand { get; }
    public RelayCommand RemoveWorkAreaPresetCommand { get; }
    public RelayCommand ExportSettingsCommand { get; }
    public RelayCommand ImportSettingsCommand { get; }

    private void Save()
    {
        if (BoardSpacingMm < 0 || SafetyMarginMm < 0)
        {
            MessageBox.Show(LocalizationService.Instance["InvalidDimensions"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (WorkAreaPresets.Any(preset => !preset.IsValid) || WorkAreaPresets.GroupBy(preset => preset.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Key.Length == 0 || group.Count() > 1))
        {
            MessageBox.Show(LocalizationService.Instance["InvalidWorkAreaPresets"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (LayerColors.Any(color => !color.IsHexValid))
        {
            MessageBox.Show(LocalizationService.Instance["InvalidLayerColours"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (WegstrMaterialThicknessMm <= 0 || WegstrSafeZMm <= 0 || WegstrIsolationDepthMm >= 0 || WegstrIsolationOffsetMm <= 0 || WegstrOutlineDepthMm >= 0 || WegstrOutlineToolDiameterMm <= 0 || WegstrDrillingDepthMm >= 0 || WegstrMillingFeedMmPerMinute <= 0 || WegstrPlungeFeedMmPerMinute <= 0 || WegstrSpindleRpm is < 1 or > 100000 || WegstrMillingToolNumber is < 1 or > 99 || WegstrCalibrationX is < 2400 or > 2600 || WegstrCalibrationY is < 2400 or > 2600 || WegstrCalibrationZ is < 2400 or > 2600 || WegstrSpeedPercent is < 1 or > 100 || WegstrAutoLevelRasterMm is < .5 or > 70 || WegstrAutoLevelSafeZMm is < .1 or > 40 || WegstrAutoLevelZBacklashSteps is < 0 or > 30)
        {
            MessageBox.Show(LocalizationService.Instance["InvalidWegstrSettings"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AppSettingsService.Save(ToSettings());
        LocalizationService.Instance.SetLanguage(Language);
        window.DialogResult = true;
    }

    private void PickColor(LayerColorSettingViewModel? layerColor)
    {
        if (layerColor is null) return;
        var dialog = new ColorPickerWindow(layerColor.Hex) { Owner = window };
        if (dialog.ShowDialog() == true)
        {
            layerColor.Hex = dialog.SelectedHex;
        }
    }

    private AppSettings ToSettings() => new()
    {
        Language = Language,
        WorkAreaPresets = WorkAreaPresets.Select(preset => preset.ToSettings()).ToList(),
        ActiveWorkAreaPresetName = SelectedWorkAreaPreset?.Name,
        BoardSpacingMm = BoardSpacingMm,
        SafetyMarginMm = SafetyMarginMm,
        AllowRotation = AllowRotation,
        DrillColor = LayerColors[0].Hex,
        TopLayerColor = LayerColors[1].Hex,
        BottomLayerColor = LayerColors[2].Hex,
        OutlineColor = LayerColors[3].Hex,
        SilkscreenColor = LayerColors[4].Hex,
        BottomMirrorPolicy = BottomMirrorPolicy,
        WegstrMaterialThicknessMm = WegstrMaterialThicknessMm,
        WegstrSafeZMm = WegstrSafeZMm,
        WegstrIsolationDepthMm = WegstrIsolationDepthMm,
        WegstrIsolationOffsetMm = WegstrIsolationOffsetMm,
        WegstrOutlineDepthMm = WegstrOutlineDepthMm,
        WegstrOutlineToolDiameterMm = WegstrOutlineToolDiameterMm,
        WegstrDrillingDepthMm = WegstrDrillingDepthMm,
        WegstrMillingFeedMmPerMinute = WegstrMillingFeedMmPerMinute,
        WegstrPlungeFeedMmPerMinute = WegstrPlungeFeedMmPerMinute,
        WegstrSpindleRpm = WegstrSpindleRpm,
        WegstrMillingToolNumber = WegstrMillingToolNumber,
        WegstrCalibrationX = WegstrCalibrationX,
        WegstrCalibrationY = WegstrCalibrationY,
        WegstrCalibrationZ = WegstrCalibrationZ,
        WegstrSpeedPercent = WegstrSpeedPercent,
        WegstrAutoLevelRasterMm = WegstrAutoLevelRasterMm,
        WegstrAutoLevelSafeZMm = WegstrAutoLevelSafeZMm,
        WegstrAutoLevelZBacklashSteps = WegstrAutoLevelZBacklashSteps,
        WegstrIncludeDrilling = WegstrIncludeDrilling,
        WegstrIncludeOutline = WegstrIncludeOutline,
        WegstrIncludeOutlineInCopper = WegstrIncludeOutlineInCopper,
    };

    private void LoadWorkAreaPresets(AppSettings settings)
    {
        AppSettingsService.NormalizeWorkAreaPresets(settings);
        WorkAreaPresets.Clear();
        foreach (var preset in settings.WorkAreaPresets) WorkAreaPresets.Add(new WorkAreaPresetSettingViewModel(preset));
        SelectedWorkAreaPreset = WorkAreaPresets.FirstOrDefault(preset => string.Equals(preset.Name, settings.ActiveWorkAreaPresetName, StringComparison.OrdinalIgnoreCase)) ?? WorkAreaPresets.FirstOrDefault();
        RemoveWorkAreaPresetCommand?.RaiseCanExecuteChanged();
    }

    private void AddWorkAreaPreset()
    {
        var baseName = LocalizationService.Instance["NewWorkAreaPreset"];
        var suffix = 1;
        var name = baseName;
        while (WorkAreaPresets.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))) name = $"{baseName} {++suffix}";
        var preset = new WorkAreaPresetSettingViewModel(new WorkAreaPreset { Name = name, WidthMm = SelectedWorkAreaPreset?.WidthMm ?? 200, HeightMm = SelectedWorkAreaPreset?.HeightMm ?? 130 });
        WorkAreaPresets.Add(preset);
        SelectedWorkAreaPreset = preset;
        RemoveWorkAreaPresetCommand.RaiseCanExecuteChanged();
    }

    private void RemoveWorkAreaPreset(WorkAreaPresetSettingViewModel? preset)
    {
        if (preset is null || WorkAreaPresets.Count <= 1) return;
        var index = WorkAreaPresets.IndexOf(preset);
        WorkAreaPresets.Remove(preset);
        if (ReferenceEquals(SelectedWorkAreaPreset, preset)) SelectedWorkAreaPreset = WorkAreaPresets[Math.Min(index, WorkAreaPresets.Count - 1)];
        RemoveWorkAreaPresetCommand.RaiseCanExecuteChanged();
    }

    private void ExportSettings()
    {
        var dialog = new SaveFileDialog { Filter = $"{LocalizationService.Instance["FilterSettingsJson"]}|*.json", DefaultExt = ".json", FileName = "gerber-panelizer-settings.json" };
        if (dialog.ShowDialog(window) == true) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(ToSettings(), new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ImportSettings()
    {
        var dialog = new OpenFileDialog { Filter = $"{LocalizationService.Instance["FilterSettingsJson"]}|*.json" };
        if (dialog.ShowDialog(window) != true) return;
        try
        {
            var imported = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(dialog.FileName));
            if (imported is null) throw new JsonException();
            AppSettingsService.NormalizeWorkAreaPresets(imported);
            Language = imported.Language;
            LoadWorkAreaPresets(imported);
            BoardSpacingMm = imported.BoardSpacingMm;
            SafetyMarginMm = imported.SafetyMarginMm;
            AllowRotation = imported.AllowRotation;
            LayerColors[0].Hex = imported.DrillColor;
            LayerColors[1].Hex = imported.TopLayerColor;
            LayerColors[2].Hex = imported.BottomLayerColor;
            LayerColors[3].Hex = imported.OutlineColor;
            LayerColors[4].Hex = imported.SilkscreenColor;
            BottomMirrorPolicy = imported.BottomMirrorPolicy;
            WegstrMaterialThicknessMm = imported.WegstrMaterialThicknessMm;
            WegstrSafeZMm = imported.WegstrSafeZMm;
            WegstrIsolationDepthMm = imported.WegstrIsolationDepthMm;
            WegstrIsolationOffsetMm = imported.WegstrIsolationOffsetMm;
            WegstrOutlineDepthMm = imported.WegstrOutlineDepthMm;
            WegstrOutlineToolDiameterMm = imported.WegstrOutlineToolDiameterMm;
            WegstrDrillingDepthMm = imported.WegstrDrillingDepthMm;
            WegstrMillingFeedMmPerMinute = imported.WegstrMillingFeedMmPerMinute;
            WegstrPlungeFeedMmPerMinute = imported.WegstrPlungeFeedMmPerMinute;
            WegstrSpindleRpm = imported.WegstrSpindleRpm;
            WegstrMillingToolNumber = imported.WegstrMillingToolNumber;
            WegstrCalibrationX = imported.WegstrCalibrationX;
            WegstrCalibrationY = imported.WegstrCalibrationY;
            WegstrCalibrationZ = imported.WegstrCalibrationZ;
            WegstrSpeedPercent = imported.WegstrSpeedPercent;
            WegstrAutoLevelRasterMm = imported.WegstrAutoLevelRasterMm;
            WegstrAutoLevelSafeZMm = imported.WegstrAutoLevelSafeZMm;
            WegstrAutoLevelZBacklashSteps = imported.WegstrAutoLevelZBacklashSteps;
            WegstrIncludeDrilling = imported.WegstrIncludeDrilling;
            WegstrIncludeOutline = imported.WegstrIncludeOutline;
            WegstrIncludeOutlineInCopper = imported.WegstrIncludeOutlineInCopper;
        }
        catch (IOException exception) { AppLog.Warning("Settings import failed because the file could not be read.", exception); MessageBox.Show(LocalizationService.Instance["SettingsReadFailed"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (JsonException exception) { AppLog.Warning("Settings import failed because the file contains invalid JSON.", exception); MessageBox.Show(LocalizationService.Instance["SettingsInvalid"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

public sealed class WorkAreaPresetSettingViewModel : ObservableObject
{
    private string name;
    private double widthMm;
    private double heightMm;

    public WorkAreaPresetSettingViewModel(WorkAreaPreset preset)
    {
        name = preset.Name;
        widthMm = preset.WidthMm;
        heightMm = preset.HeightMm;
    }

    public string Name { get => name; set { if (SetProperty(ref name, value)) OnPropertyChanged(nameof(IsValid)); } }
    public double WidthMm { get => widthMm; set { if (SetProperty(ref widthMm, value)) OnPropertyChanged(nameof(IsValid)); } }
    public double HeightMm { get => heightMm; set { if (SetProperty(ref heightMm, value)) OnPropertyChanged(nameof(IsValid)); } }
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && WidthMm > 0 && HeightMm > 0;
    public WorkAreaPreset ToSettings() => new() { Name = Name.Trim(), WidthMm = WidthMm, HeightMm = HeightMm };
}

public sealed class LayerColorSettingViewModel : ObservableObject
{
    private string hex;
    private int red;
    private int green;
    private int blue;

    public LayerColorSettingViewModel(string displayNameKey, string? initialHex, string fallbackHex)
    {
        DisplayNameKey = displayNameKey;
        hex = IsValidHex(initialHex) ? NormalizeHex(initialHex!) : fallbackHex;
        ApplyHex(hex);
        LocalizationService.Instance.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is "Item[]" or nameof(LocalizationService.Language)) OnPropertyChanged(nameof(DisplayName));
        };
    }

    public string DisplayNameKey { get; }
    public string DisplayName => LocalizationService.Instance[DisplayNameKey];
    public string Hex
    {
        get => hex;
        set
        {
            if (!SetProperty(ref hex, value ?? string.Empty)) return;
            if (IsValidHex(hex)) ApplyHex(hex);
            OnPropertyChanged(nameof(IsHexValid));
            OnPropertyChanged(nameof(PreviewBrush));
        }
    }
    public int Red { get => red; set => SetRgb(value, green, blue); }
    public int Green { get => green; set => SetRgb(red, value, blue); }
    public int Blue { get => blue; set => SetRgb(red, green, value); }
    public bool IsHexValid => IsValidHex(hex);
    public Color PreviewColor => IsHexValid ? Color.FromRgb((byte)red, (byte)green, (byte)blue) : Colors.Transparent;
    public Brush PreviewBrush => IsHexValid ? new SolidColorBrush(PreviewColor) : Brushes.Transparent;

    private void SetRgb(int newRed, int newGreen, int newBlue)
    {
        if (newRed is < 0 or > 255 || newGreen is < 0 or > 255 || newBlue is < 0 or > 255) return;
        if (red == newRed && green == newGreen && blue == newBlue) return;
        red = newRed;
        green = newGreen;
        blue = newBlue;
        hex = $"#{red:X2}{green:X2}{blue:X2}";
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(IsHexValid));
        OnPropertyChanged(nameof(PreviewBrush));
    }

    private void ApplyHex(string value)
    {
        var normalized = NormalizeHex(value);
        var color = (Color)ColorConverter.ConvertFromString(normalized)!;
        red = color.R;
        green = color.G;
        blue = color.B;
        if (!string.Equals(hex, normalized, StringComparison.Ordinal)) hex = normalized;
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(PreviewBrush));
    }

    private static bool IsValidHex(string? value) => value is not null && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
    private static string NormalizeHex(string value) => value.ToUpperInvariant();
}
