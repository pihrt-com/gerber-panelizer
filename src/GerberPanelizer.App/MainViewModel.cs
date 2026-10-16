using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using GerberPanelizer.Build;
using GerberPanelizer.Core.Exporting;
using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Importing;
using GerberPanelizer.Core.Models;
using GerberPanelizer.Core.Panelization;
using GerberPanelizer.Core.Projects;
using Microsoft.Win32;

namespace GerberPanelizer.App;

public sealed class MainViewModel : ObservableObject
{
    private const double PreviewWidth = 710;
    private const double PreviewHeight = 462;
    private readonly Window owner;
    private readonly ManufacturingImporter importer = new();
    private readonly ShelfPanelizer panelizer = new();
    private readonly PanelExportService exporter = new();
    private readonly WegstrGCodeExportService wegstrExporter = new();
    private static readonly HttpClient UpdateClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private AppSettings settings;
    private PanelizationResult? panelization;
    private string statusText;
    private string manufacturingSide = "Both";
    private string? projectPath;
    private ImportedBoardItemViewModel? selectedBoard;
    private bool isStatusLogExpanded;
    private bool isCheckingForUpdates;

    public MainViewModel(Window owner)
    {
        this.owner = owner;
        settings = AppSettingsService.Load();
        statusText = L("StatusReady");
        Layers = new([new("Drill", "LayerDrill", settings.DrillColor), new("Top", "LayerTop", settings.TopLayerColor), new("Bottom", "LayerBottom", settings.BottomLayerColor), new("Outline", "LayerOutline", settings.OutlineColor), new("Silkscreen", "LayerSilkscreen", settings.SilkscreenColor)]);
        ImportCommand = new(_ => ImportFiles());
        NewProjectCommand = new(_ => NewProject());
        SaveProjectCommand = new(_ => SaveProject());
        LoadProjectCommand = new(_ => LoadProject());
        DeleteBoardCommand = new(board => DeleteBoard(board as ImportedBoardItemViewModel ?? SelectedBoard), _ => SelectedBoard is not null);
        CloneBoardCommand = new(board => CloneBoard(board as ImportedBoardItemViewModel ?? SelectedBoard), _ => SelectedBoard is not null);
        DeletePlacementCommand = new(DeletePlacement);
        MovePlacementCommand = new(MovePlacement);
        ToggleStatusLogCommand = new(_ => IsStatusLogExpanded = !IsStatusLogExpanded);
        SelectSideCommand = new(side => SelectSide(side as string));
        PanelizeCommand = new(_ => Panelize(), _ => Boards.Any(board => board.IsIncluded));
        ExportCommand = new(_ => Export(), _ => panelization is { Placements.Count: > 0 });
        ExportWegstrCommand = new(_ => ExportWegstr(), _ => panelization is { Placements.Count: > 0 });
        PrintCommand = new(_ => Print());
        OpenSettingsCommand = new(_ => OpenSettings());
        CheckForUpdatesCommand = new(_ => CheckForUpdates(), _ => !IsCheckingForUpdates);
        AddStatus(StatusSeverity.Information, statusText);
    }

    public ObservableCollection<LayerItemViewModel> Layers { get; }
    public ObservableCollection<ImportedBoardItemViewModel> Boards { get; } = [];
    public ObservableCollection<PlacementPreviewItemViewModel> PlacementPreviews { get; } = [];
    public ObservableCollection<StatusEntryViewModel> StatusEntries { get; } = [];
    public RelayCommand ImportCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand SaveProjectCommand { get; }
    public RelayCommand LoadProjectCommand { get; }
    public RelayCommand DeleteBoardCommand { get; }
    public RelayCommand CloneBoardCommand { get; }
    public RelayCommand DeletePlacementCommand { get; }
    public RelayCommand MovePlacementCommand { get; }
    public RelayCommand ToggleStatusLogCommand { get; }
    public RelayCommand SelectSideCommand { get; }
    public RelayCommand PanelizeCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ExportWegstrCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand CheckForUpdatesCommand { get; }
    public string VersionCaption => $"v{BuildStamp.Version}";
    public string BuildCaption => $"{BuildStamp.DateUtc} · {BuildStamp.Revision}";
    public string WorkAreaCaption => $"{settings.WorkAreaWidthMm:0.##} × {settings.WorkAreaHeightMm:0.##} mm";
    public double WorkAreaWidthMm => settings.WorkAreaWidthMm;
    public double WorkAreaHeightMm => settings.WorkAreaHeightMm;
    public IReadOnlyList<BoardPlacement> RenderPlacements => panelization?.Placements ?? Array.Empty<BoardPlacement>();
    public string ManufacturingSide { get => manufacturingSide; private set => SetProperty(ref manufacturingSide, value); }
    public string StatusText { get => statusText; private set => SetProperty(ref statusText, value); }
    public ImportedBoardItemViewModel? SelectedBoard
    {
        get => selectedBoard;
        set { if (SetProperty(ref selectedBoard, value)) RefreshBoardCommands(); }
    }
    public bool IsStatusLogExpanded { get => isStatusLogExpanded; set => SetProperty(ref isStatusLogExpanded, value); }
    public bool IsCheckingForUpdates
    {
        get => isCheckingForUpdates;
        private set
        {
            if (SetProperty(ref isCheckingForUpdates, value)) CheckForUpdatesCommand.RaiseCanExecuteChanged();
        }
    }
    public Visibility BoardPreviewVisibility => PlacementPreviews.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyPreviewVisibility => PlacementPreviews.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

    private static string L(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, LocalizationService.Instance[key], values);

    private void ImportFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = $"{L("FilterProductionData")}|*.zip;*.rar;*.gbr;*.ger;*.gtl;*.gbl;*.gko;*.gm1;*.gml;*.gto;*.gbo;*.gts;*.gbs;*.gtp;*.gbp;*.drl;*.drd;*.xln;*.exc;*.txt|{L("FilterArchives")}|*.zip;*.rar|{L("FilterGerberExcellon")}|*.gbr;*.ger;*.gtl;*.gbl;*.gko;*.gm1;*.gml;*.gto;*.gbo;*.gts;*.gbs;*.gtp;*.gbp;*.drl;*.drd;*.xln;*.exc;*.txt|{L("FilterAllFiles")}|*.*", Title = LocalizationService.Instance["ImportFiles"] };
        if (dialog.ShowDialog(owner) != true) return;
        try
        {
            var results = new List<ImportResult>();
            results.AddRange(dialog.FileNames.Where(IsArchive).Select(path => importer.ImportFiles([path])));
            var looseFiles = dialog.FileNames.Where(path => !IsArchive(path)).ToArray();
            if (looseFiles.Length > 0) results.Add(importer.ImportFiles(looseFiles));
            var boards = results.SelectMany(result => result.Boards).ToList();
            foreach (var board in boards) AddBoard(board);
            ClearPanelization();
            var issues = results.SelectMany(result => result.Issues).ToList();
            AddImportStatus(boards, issues);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AddStatus(StatusSeverity.Error, L("StatusImportFailed", exception.Message));
        }
    }

    private static bool IsArchive(string path) => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".rar", StringComparison.OrdinalIgnoreCase);

    private void AddImportStatus(IReadOnlyCollection<ImportedBoard> boards, IReadOnlyCollection<ImportIssue> issues)
    {
        var errors = issues.Count(issue => issue.IsError);
        var warnings = issues.Count - errors;
        var details = issues.Select(issue => $"{Path.GetFileName(issue.Source)}: {issue.Message}");
        AddStatus(boards.Count == 0 ? (errors > 0 ? StatusSeverity.Error : StatusSeverity.Warning) : (errors > 0 ? StatusSeverity.Warning : StatusSeverity.Success),
            boards.Count == 0 ? (errors > 0 ? L("StatusNoBoardImported", errors) : L("StatusNoLayersRecognised")) : L("StatusImported", boards.Count, boards.Sum(board => board.Files.Count), errors, warnings), details);
    }

    private void AddBoard(ImportedBoard board, bool included = true)
    {
        var item = new ImportedBoardItemViewModel(board, included);
        item.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ImportedBoardItemViewModel.IsIncluded)) PanelizeCommand.RaiseCanExecuteChanged(); };
        Boards.Add(item);
        RefreshBoardCommands();
    }

    private void NewProject()
    {
        if (Boards.Count > 0 && MessageBox.Show(owner, LocalizationService.Instance["NewProjectConfirm"], LocalizationService.Instance["NewProject"], MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Boards.Clear(); SelectedBoard = null; projectPath = null; ManufacturingSide = "Both"; ClearPanelization(); RefreshBoardCommands();
        AddStatus(StatusSeverity.Information, L("StatusNewProject"));
    }

    private void SaveProject()
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            var dialog = new SaveFileDialog { Filter = $"{L("FilterProject")}|*.gpanel", DefaultExt = ".gpanel", FileName = "panel-project.gpanel", Title = LocalizationService.Instance["SaveProject"] };
            if (dialog.ShowDialog(owner) != true) return;
            projectPath = dialog.FileName;
        }
        try { PanelProjectStore.Save(projectPath, BuildProject(projectPath)); AddStatus(StatusSeverity.Success, L("StatusProjectSaved", Path.GetFileName(projectPath))); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { AddStatus(StatusSeverity.Error, L("StatusProjectSaveFailed", exception.Message)); }
    }

    private PanelProject BuildProject(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        return new PanelProject
        {
            ManufacturingSide = ManufacturingSide,
            WorkAreaWidthMm = settings.WorkAreaWidthMm,
            WorkAreaHeightMm = settings.WorkAreaHeightMm,
            Boards = Boards.Select(board => new PanelProjectBoard { InstanceId = board.InstanceId, DisplayName = board.Name, IsIncluded = board.IsIncluded, SourcePaths = board.SourcePaths.Select(source => Path.GetRelativePath(directory, source)).ToList() }).ToList(),
            Placements = (panelization?.Placements ?? []).Select(placement => new PanelProjectPlacement { BoardInstanceId = placement.Board.Id, OffsetX = placement.Offset.X, OffsetY = placement.Offset.Y, RotationDegrees = placement.RotationDegrees, MirrorMode = placement.MirrorMode.ToString() }).ToList(),
        };
    }

    private void LoadProject()
    {
        var dialog = new OpenFileDialog { Filter = $"{L("FilterProject")}|*.gpanel|{L("FilterAllFiles")}|*.*", Title = LocalizationService.Instance["LoadProject"] };
        if (dialog.ShowDialog(owner) != true) return;
        try
        {
            var project = PanelProjectStore.Load(dialog.FileName);
            ApplyProjectWorkArea(project);
            LoadProjectBoards(project, Path.GetDirectoryName(dialog.FileName)!);
            projectPath = dialog.FileName;
            ManufacturingSide = project.ManufacturingSide is "Top" or "Bottom" or "Both" ? project.ManufacturingSide : "Both";
            RestorePlacements(project.Placements);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { AddStatus(StatusSeverity.Error, L("StatusProjectLoadFailed", exception.Message)); }
    }

    private void LoadProjectBoards(PanelProject project, string directory)
    {
        Boards.Clear(); ClearPanelization();
        var missing = new List<string>(); var failures = new List<string>();
        foreach (var entry in project.Boards)
        {
            if (entry.SourcePaths.Count == 0) { failures.Add(L("StatusProjectNoSources", entry.DisplayName)); continue; }
            var paths = entry.SourcePaths.Select(path => ResolveProjectSource(directory, path)).ToArray();
            var unavailable = paths.Where(path => !File.Exists(path)).ToArray();
            if (unavailable.Length > 0) { missing.AddRange(unavailable.Select(path => Path.GetFileName(path) ?? path)); continue; }
            var result = importer.ImportFiles(paths);
            if (result.Boards.Count == 0) { failures.AddRange(result.Issues.Select(issue => $"{entry.DisplayName}: {issue.Message}")); continue; }
            AddBoard(result.Boards[0] with { Id = entry.InstanceId, DisplayName = entry.DisplayName, SourceFilePaths = paths }, entry.IsIncluded);
            failures.AddRange(result.Issues.Select(issue => $"{entry.DisplayName}: {issue.Message}"));
        }
        var details = missing.Concat(failures).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        AddStatus(details.Count == 0 ? StatusSeverity.Success : StatusSeverity.Warning, L("StatusProjectLoaded", Boards.Count, missing.Count), details);
    }

    private static string ResolveProjectSource(string directory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidDataException(L("StatusProjectAbsoluteSource"));
        return Path.GetFullPath(Path.Combine(directory, relativePath));
    }

    private void RestorePlacements(IEnumerable<PanelProjectPlacement> saved)
    {
        var boards = Boards.ToDictionary(item => item.InstanceId, StringComparer.OrdinalIgnoreCase);
        var placements = saved.Where(item => boards.ContainsKey(item.BoardInstanceId)).Select(item => new BoardPlacement(boards[item.BoardInstanceId].Board, new PcbPoint(item.OffsetX, item.OffsetY), item.RotationDegrees, Enum.TryParse<BoardMirrorMode>(item.MirrorMode, true, out var mirrorMode) ? mirrorMode : BoardMirrorMode.None)).ToList();
        if (placements.Count == 0) return;
        panelization = new(placements, [], GetUsedBounds(placements)); RebuildPlacementPreviews(); OnPropertyChanged(nameof(RenderPlacements)); ExportCommand.RaiseCanExecuteChanged(); ExportWegstrCommand.RaiseCanExecuteChanged();
    }

    private void ApplyProjectWorkArea(PanelProject project)
    {
        if (project.WorkAreaWidthMm <= 0 || project.WorkAreaHeightMm <= 0) return;
        settings.WorkAreaWidthMm = project.WorkAreaWidthMm;
        settings.WorkAreaHeightMm = project.WorkAreaHeightMm;
        OnPropertyChanged(nameof(WorkAreaCaption));
        OnPropertyChanged(nameof(WorkAreaWidthMm));
        OnPropertyChanged(nameof(WorkAreaHeightMm));
    }

    private void DeleteBoard(ImportedBoardItemViewModel? board)
    {
        if (board is null) return;
        DeletePlacementForBoard(board.Board); Boards.Remove(board); if (ReferenceEquals(SelectedBoard, board)) SelectedBoard = null;
        RefreshBoardCommands(); AddStatus(StatusSeverity.Information, L("StatusBoardDeleted", board.Name));
    }

    private void CloneBoard(ImportedBoardItemViewModel? board)
    {
        if (board is null) return;
        AddBoard(board.Board with { Id = Guid.NewGuid().ToString("N"), DisplayName = L("BoardCopyName", board.Name) }, board.IsIncluded);
        SelectedBoard = Boards.Last(); ClearPanelization(); AddStatus(StatusSeverity.Success, L("StatusBoardCloned", board.Name));
    }

    private void DeletePlacement(object? parameter) { if (parameter is BoardPlacement placement) DeletePlacementForBoard(placement.Board); }
    private void DeletePlacementForBoard(ImportedBoard board)
    {
        if (panelization is null) return;
        var remaining = panelization.Placements.Where(placement => !string.Equals(placement.Board.Id, board.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (remaining.Count == panelization.Placements.Count) return;
        panelization = new(remaining, panelization.UnplacedBoards, GetUsedBounds(remaining)); RebuildPlacementPreviews(); OnPropertyChanged(nameof(RenderPlacements)); ExportCommand.RaiseCanExecuteChanged(); ExportWegstrCommand.RaiseCanExecuteChanged();
    }
    private void MovePlacement(object? request)
    {
        if (request is not PlacementMoveRequest move || panelization is null) return;
        var original = move.Placement;
        var offset = move.RequestedOffset;
        var placements = panelization.Placements.Select(placement => string.Equals(placement.Board.Id, original.Board.Id, StringComparison.OrdinalIgnoreCase) ? placement with { Offset = offset } : placement).ToList();
        panelization = new(placements, panelization.UnplacedBoards, GetUsedBounds(placements)); RebuildPlacementPreviews(); OnPropertyChanged(nameof(RenderPlacements)); AddStatus(StatusSeverity.Information, L("StatusBoardMoved", original.Board.DisplayName));
    }

    private void SelectSide(string? side)
    {
        ManufacturingSide = side ?? "Both";
        ClearPanelization();
        AddStatus(StatusSeverity.Information, L("StatusManufacturingSide", L($"Side{ManufacturingSide}")));
    }
    private void Panelize()
    {
        var selected = Boards.Where(board => board.IsIncluded).Select(board => FilterBoardForManufacturingSide(board.Board)).ToList();
        if (selected.Count == 0) { AddStatus(StatusSeverity.Warning, L("StatusSelectBoard")); return; }
        try
        {
            panelization = panelizer.Arrange(selected, new(new(settings.WorkAreaWidthMm, settings.WorkAreaHeightMm), settings.SafetyMarginMm, settings.BoardSpacingMm), settings.AllowRotation);
            if (ManufacturingSide == "Bottom" && Enum.TryParse<BoardMirrorMode>(settings.BottomMirrorPolicy, true, out var mirrorMode) && mirrorMode != BoardMirrorMode.None)
            {
                panelization = panelization with { Placements = panelization.Placements.Select(placement => placement with { MirrorMode = mirrorMode }).ToList() };
            }
            RebuildPlacementPreviews(); OnPropertyChanged(nameof(RenderPlacements)); ExportCommand.RaiseCanExecuteChanged(); ExportWegstrCommand.RaiseCanExecuteChanged();
            AddStatus(panelization.UnplacedBoards.Count == 0 ? StatusSeverity.Success : StatusSeverity.Warning, panelization.UnplacedBoards.Count == 0 ? L("StatusPanelized", panelization.Placements.Count, WorkAreaCaption) : L("StatusPanelizedUnplaced", panelization.Placements.Count, panelization.UnplacedBoards.Count, WorkAreaCaption));
        }
        catch (ArgumentException exception) { AddStatus(StatusSeverity.Error, L("StatusPanelSettingsInvalid", exception.Message)); }
    }
    private void RebuildPlacementPreviews()
    {
        PlacementPreviews.Clear(); foreach (var placement in panelization?.Placements ?? []) PlacementPreviews.Add(CreatePreview(placement));
        OnPropertyChanged(nameof(BoardPreviewVisibility)); OnPropertyChanged(nameof(EmptyPreviewVisibility));
    }
    private PlacementPreviewItemViewModel CreatePreview(BoardPlacement placement)
    {
        var b = placement.Bounds; var x = b.MinX / settings.WorkAreaWidthMm * PreviewWidth; var y = b.MinY / settings.WorkAreaHeightMm * PreviewHeight;
        return new(placement.Board.DisplayName, x, PreviewHeight - y - Math.Max(3, b.Height / settings.WorkAreaHeightMm * PreviewHeight), Math.Max(3, b.Width / settings.WorkAreaWidthMm * PreviewWidth), Math.Max(3, b.Height / settings.WorkAreaHeightMm * PreviewHeight));
    }
    private ImportedBoard FilterBoardForManufacturingSide(ImportedBoard board) => ManufacturingSide == "Both" ? board : board with { Files = board.Files.Where(file => file.Layer is ManufacturingLayerKind.Profile or ManufacturingLayerKind.Drill || IsSelectedSideLayer(file.Layer)).ToList() };
    private bool IsSelectedSideLayer(ManufacturingLayerKind layer) => ManufacturingSide switch { "Top" => layer is ManufacturingLayerKind.CopperTop or ManufacturingLayerKind.SolderMaskTop or ManufacturingLayerKind.SilkScreenTop or ManufacturingLayerKind.PasteTop, "Bottom" => layer is ManufacturingLayerKind.CopperBottom or ManufacturingLayerKind.SolderMaskBottom or ManufacturingLayerKind.SilkScreenBottom or ManufacturingLayerKind.PasteBottom, _ => true };
    private void Export()
    {
        if (panelization is not { Placements.Count: > 0 }) { AddStatus(StatusSeverity.Warning, L("StatusPanelizeBeforeExport")); return; }
        var dialog = new SaveFileDialog { Filter = $"{L("FilterZipArchive")}|*.zip", DefaultExt = ".zip", FileName = "panelized-production-data.zip", Title = LocalizationService.Instance["Export"], OverwritePrompt = false };
        if (dialog.ShowDialog(owner) != true) return;
        if (Directory.Exists(dialog.FileName)) { AddStatus(StatusSeverity.Error, L("StatusExportFailed", L("ExportTargetIsDirectory"))); return; }
        if (File.Exists(dialog.FileName) && MessageBox.Show(owner, L("ExportOverwriteText", dialog.FileName), L("ExportOverwriteTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        var temporaryArchivePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dialog.FileName))!, $".{Path.GetFileNameWithoutExtension(dialog.FileName)}-{Guid.NewGuid():N}.tmp");
        try
        {
            var result = exporter.Export(temporaryArchivePath, panelization.Placements);
            File.Move(temporaryArchivePath, dialog.FileName, overwrite: true);
            AddStatus(result.Warnings.Count == 0 ? StatusSeverity.Success : StatusSeverity.Warning, L("StatusExported", result.Entries.Count, Path.GetFileName(dialog.FileName), result.Warnings.Count), result.Warnings);
            MessageBox.Show(owner, L("ExportSuccessText", dialog.FileName), L("ExportSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) { AddStatus(StatusSeverity.Error, L("StatusExportFailed", exception.Message)); }
        finally
        {
            if (File.Exists(temporaryArchivePath)) File.Delete(temporaryArchivePath);
        }
    }

    private void ExportWegstr()
    {
        if (panelization is not { Placements.Count: > 0 }) { AddStatus(StatusSeverity.Warning, L("StatusPanelizeBeforeExport")); return; }
        var dialog = new SaveFileDialog { Filter = $"{L("FilterZipArchive")}|*.zip", DefaultExt = ".zip", FileName = "wegstr-machine.zip", Title = LocalizationService.Instance["ExportWegstr"], OverwritePrompt = false };
        if (dialog.ShowDialog(owner) != true) return;
        if (Directory.Exists(dialog.FileName)) { AddStatus(StatusSeverity.Error, L("StatusWegstrExportFailed", L("ExportTargetIsDirectory"))); return; }
        if (File.Exists(dialog.FileName) && MessageBox.Show(owner, L("ExportOverwriteText", dialog.FileName), L("ExportOverwriteTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var temporaryArchivePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dialog.FileName))!, $".{Path.GetFileNameWithoutExtension(dialog.FileName)}-{Guid.NewGuid():N}.tmp");
        try
        {
            var profile = new WegstrMachineProfile
            {
                WorkAreaWidthMillimetres = settings.WorkAreaWidthMm,
                WorkAreaHeightMillimetres = settings.WorkAreaHeightMm,
                MaterialThicknessMillimetres = settings.WegstrMaterialThicknessMm,
                SafeZMillimetres = settings.WegstrSafeZMm,
                IsolationDepthMillimetres = settings.WegstrIsolationDepthMm,
                IsolationOffsetMillimetres = settings.WegstrIsolationOffsetMm,
                OutlineDepthMillimetres = settings.WegstrOutlineDepthMm,
                OutlineToolDiameterMillimetres = settings.WegstrOutlineToolDiameterMm,
                DrillingDepthMillimetres = settings.WegstrDrillingDepthMm,
                MillingFeedMillimetresPerMinute = settings.WegstrMillingFeedMmPerMinute,
                PlungeFeedMillimetresPerMinute = settings.WegstrPlungeFeedMmPerMinute,
                SpindleRpm = settings.WegstrSpindleRpm,
                MillingToolNumber = settings.WegstrMillingToolNumber,
                IncludeDrilling = settings.WegstrIncludeDrilling,
                IncludeOutline = settings.WegstrIncludeOutline,
            };
            var result = wegstrExporter.Export(temporaryArchivePath, panelization.Placements, profile);
            File.Move(temporaryArchivePath, dialog.FileName, overwrite: true);
            AddStatus(result.Warnings.Count == 0 ? StatusSeverity.Success : StatusSeverity.Warning, L("StatusWegstrExported", result.Entries.Count, Path.GetFileName(dialog.FileName), result.Warnings.Count), result.Warnings);
            MessageBox.Show(owner, L("WegstrExportSuccessText", dialog.FileName), L("ExportSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentOutOfRangeException) { AddStatus(StatusSeverity.Error, L("StatusWegstrExportFailed", exception.Message)); }
        finally
        {
            if (File.Exists(temporaryArchivePath)) File.Delete(temporaryArchivePath);
        }
    }

    private async void CheckForUpdates()
    {
        IsCheckingForUpdates = true;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/pihrt-com/gerber-panelizer/releases/latest");
            request.Headers.UserAgent.ParseAdd("GerberPanelizer/1.0");
            using var response = await UpdateClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            var tagName = document.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            var releaseUrl = document.RootElement.GetProperty("html_url").GetString() ?? "https://github.com/pihrt-com/gerber-panelizer/releases";
            if (!Version.TryParse(tagName.TrimStart('v', 'V'), out var latestVersion)) throw new InvalidDataException("GitHub release does not contain a valid version tag.");
            var currentVersion = Version.Parse(BuildStamp.Version);
            if (latestVersion > currentVersion)
            {
                AddStatus(StatusSeverity.Information, L("StatusUpdateAvailable", latestVersion));
                if (MessageBox.Show(owner, L("UpdateAvailable", latestVersion), L("UpdateCheck"), MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
                }
            }
            else
            {
                AddStatus(StatusSeverity.Success, L("StatusUpdateCurrent", currentVersion));
                MessageBox.Show(owner, L("UpdateCurrent", currentVersion), L("UpdateCheck"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            AddStatus(StatusSeverity.Warning, L("StatusUpdateFailed", exception.Message));
            MessageBox.Show(owner, L("UpdateFailed", exception.Message), L("UpdateCheck"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
    private void Print()
    {
        if (panelization is not { Placements.Count: > 0 }) { AddStatus(StatusSeverity.Warning, L("StatusPanelizeBeforePrint")); return; }
        new PanelPrintPreviewWindow(owner, RenderPlacements, Layers, WorkAreaWidthMm, WorkAreaHeightMm).ShowDialog();
        AddStatus(StatusSeverity.Information, L("StatusPrintPreviewOpened"));
    }
    private void OpenSettings()
    {
        var dialog = new SettingsWindow(settings) { Owner = owner };
        if (dialog.ShowDialog() != true) return;
        settings = AppSettingsService.Load();
        ApplyLayerColors();
        OnPropertyChanged(nameof(WorkAreaCaption));
        OnPropertyChanged(nameof(WorkAreaWidthMm));
        OnPropertyChanged(nameof(WorkAreaHeightMm));
        ClearPanelization();
        AddStatus(StatusSeverity.Information, L("StatusSettingsSaved"));
    }

    private void ApplyLayerColors()
    {
        Layers[0].SetColor(settings.DrillColor);
        Layers[1].SetColor(settings.TopLayerColor);
        Layers[2].SetColor(settings.BottomLayerColor);
        Layers[3].SetColor(settings.OutlineColor);
        Layers[4].SetColor(settings.SilkscreenColor);
    }
    private void AddStatus(StatusSeverity severity, string message, IEnumerable<string>? details = null)
    {
        StatusText = message; StatusEntries.Insert(0, new(DateTimeOffset.Now, severity, message, details?.Where(detail => !string.IsNullOrWhiteSpace(detail)).ToArray() ?? []));
        while (StatusEntries.Count > 100) StatusEntries.RemoveAt(StatusEntries.Count - 1);
    }
    private void ClearPanelization() { panelization = null; PlacementPreviews.Clear(); OnPropertyChanged(nameof(BoardPreviewVisibility)); OnPropertyChanged(nameof(EmptyPreviewVisibility)); OnPropertyChanged(nameof(RenderPlacements)); ExportCommand.RaiseCanExecuteChanged(); ExportWegstrCommand.RaiseCanExecuteChanged(); }
    private void RefreshBoardCommands() { PanelizeCommand.RaiseCanExecuteChanged(); DeleteBoardCommand.RaiseCanExecuteChanged(); CloneBoardCommand.RaiseCanExecuteChanged(); }
    private static PcbBounds GetUsedBounds(IEnumerable<BoardPlacement> placements) => placements.Aggregate(PcbBounds.Empty, (bounds, placement) => bounds.Include(placement.Bounds));
}

public sealed class LayerItemViewModel : ObservableObject
{
    private bool isVisible = true;
    private Brush brush;
    private readonly string localizationKey;

    public LayerItemViewModel(string identifier, string localizationKey, string color)
    {
        Identifier = identifier;
        this.localizationKey = localizationKey;
        brush = CreateBrush(color);
        LocalizationService.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is "Item[]" or nameof(LocalizationService.Language)) OnPropertyChanged(nameof(Name));
        };
    }

    public string Identifier { get; }
    public string Name => LocalizationService.Instance[localizationKey];
    public Brush Brush { get => brush; private set => SetProperty(ref brush, value); }
    public bool IsVisible { get => isVisible; set => SetProperty(ref isVisible, value); }

    public void SetColor(string color) => Brush = CreateBrush(color);

    private static Brush CreateBrush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
}
public sealed class ImportedBoardItemViewModel : ObservableObject
{
    private bool isIncluded;
    public ImportedBoardItemViewModel(ImportedBoard board, bool isIncluded = true) { Board = board; this.isIncluded = isIncluded; }
    public ImportedBoard Board { get; } public string InstanceId => Board.Id; public IReadOnlyList<string> SourcePaths => Board.SourceFilePaths; public string Name => Board.DisplayName;
    public string Detail => Board.Bounds.IsEmpty
        ? string.Format(CultureInfo.CurrentCulture, LocalizationService.Instance["BoardDetailNoOutline"], Board.Files.Count)
        : string.Format(CultureInfo.CurrentCulture, LocalizationService.Instance["BoardDetail"], Board.Bounds.Width, Board.Bounds.Height, Board.Files.Count);
    public bool IsIncluded { get => isIncluded; set => SetProperty(ref isIncluded, value); }
}
public enum StatusSeverity { Information, Success, Warning, Error }
public sealed class StatusEntryViewModel(DateTimeOffset timestamp, StatusSeverity severity, string message, IReadOnlyList<string> details)
{
    public DateTimeOffset Timestamp { get; } = timestamp; public StatusSeverity Severity { get; } = severity; public string Message { get; } = message; public IReadOnlyList<string> Details { get; } = details; public bool HasDetails => Details.Count > 0;
}
public sealed record PlacementPreviewItemViewModel(string Name, double Left, double Top, double Width, double Height);
