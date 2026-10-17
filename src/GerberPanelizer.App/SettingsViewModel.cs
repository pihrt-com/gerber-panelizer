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
    private double workAreaWidthMm;
    private double workAreaHeightMm;
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
    private bool wegstrIncludeDrilling;
    private bool wegstrIncludeOutline;

    public SettingsViewModel(AppSettings settings, Window window)
    {
        this.window = window;
        language = settings.Language;
        workAreaWidthMm = settings.WorkAreaWidthMm;
        workAreaHeightMm = settings.WorkAreaHeightMm;
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
        wegstrIncludeDrilling = settings.WegstrIncludeDrilling;
        wegstrIncludeOutline = settings.WegstrIncludeOutline;
        SaveCommand = new RelayCommand(_ => Save());
        PickColorCommand = new RelayCommand(color => PickColor(color as LayerColorSettingViewModel));
        ExportSettingsCommand = new RelayCommand(_ => ExportSettings());
        ImportSettingsCommand = new RelayCommand(_ => ImportSettings());
    }

    public string Language { get => language; set => SetProperty(ref language, value); }
    public double WorkAreaWidthMm { get => workAreaWidthMm; set => SetProperty(ref workAreaWidthMm, value); }
    public double WorkAreaHeightMm { get => workAreaHeightMm; set => SetProperty(ref workAreaHeightMm, value); }
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
    public bool WegstrIncludeDrilling { get => wegstrIncludeDrilling; set => SetProperty(ref wegstrIncludeDrilling, value); }
    public bool WegstrIncludeOutline { get => wegstrIncludeOutline; set => SetProperty(ref wegstrIncludeOutline, value); }
    public ObservableCollection<LayerColorSettingViewModel> LayerColors { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand PickColorCommand { get; }
    public RelayCommand ExportSettingsCommand { get; }
    public RelayCommand ImportSettingsCommand { get; }

    private void Save()
    {
        if (WorkAreaWidthMm <= 0 || WorkAreaHeightMm <= 0 || BoardSpacingMm < 0 || SafetyMarginMm < 0)
        {
            MessageBox.Show(LocalizationService.Instance["InvalidDimensions"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (LayerColors.Any(color => !color.IsHexValid))
        {
            MessageBox.Show(LocalizationService.Instance["InvalidLayerColours"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (WegstrMaterialThicknessMm <= 0 || WegstrSafeZMm <= 0 || WegstrIsolationDepthMm >= 0 || WegstrIsolationOffsetMm <= 0 || WegstrOutlineDepthMm >= 0 || WegstrOutlineToolDiameterMm <= 0 || WegstrDrillingDepthMm >= 0 || WegstrMillingFeedMmPerMinute <= 0 || WegstrPlungeFeedMmPerMinute <= 0 || WegstrSpindleRpm is < 1 or > 100000 || WegstrMillingToolNumber is < 1 or > 99)
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
        WorkAreaWidthMm = WorkAreaWidthMm,
        WorkAreaHeightMm = WorkAreaHeightMm,
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
        WegstrIncludeDrilling = WegstrIncludeDrilling,
        WegstrIncludeOutline = WegstrIncludeOutline,
    };

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
            Language = imported.Language;
            WorkAreaWidthMm = imported.WorkAreaWidthMm;
            WorkAreaHeightMm = imported.WorkAreaHeightMm;
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
            WegstrIncludeDrilling = imported.WegstrIncludeDrilling;
            WegstrIncludeOutline = imported.WegstrIncludeOutline;
        }
        catch (IOException exception) { AppLog.Warning("Settings import failed because the file could not be read.", exception); MessageBox.Show(LocalizationService.Instance["SettingsReadFailed"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (JsonException exception) { AppLog.Warning("Settings import failed because the file contains invalid JSON.", exception); MessageBox.Show(LocalizationService.Instance["SettingsInvalid"], "Gerber Panelizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
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
