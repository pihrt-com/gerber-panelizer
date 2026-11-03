using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Windows;
using GerberPanelizer.Core.Exporting;
using GerberPanelizer.Core.Machines.Wegstr;

namespace GerberPanelizer.App;

public sealed class WegstrMachineViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Window owner;
    private readonly Func<WegstrProgramSet> preparePrograms;
    private readonly Func<WegstrDirectSettings> directSettings;
    private readonly Func<bool> isSafetyNoticeAcknowledged;
    private readonly Action acknowledgeSafetyNotice;
    private readonly WegstrGCodeCompiler compiler = new();
    private readonly WegstrSurfaceLeveler surfaceLeveler = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private IWegstrMachineLink? link;
    private CancellationTokenSource? runCancellation;
    private Task? activeRunTask;
    private Task? activeManualTask;
    private TaskCompletionSource<bool> resumeSignal = CompletedSignal();
    private WegstrPreparedProgram? selectedProgram;
    private string? selectedPort;
    private bool useSimulator = true;
    private bool isConnecting;
    private bool isConnected;
    private bool isMachineReady;
    private bool isRunning;
    private bool runStopRequested;
    private bool isStopping;
    private bool isPaused;
    private bool pauseSpindleRequested;
    private bool restartSpindleAfterPause;
    private bool isManualMoving;
    private bool jogRequested;
    private bool manualStopRequested;
    private bool isSpindleOn;
    private int manualSpeedPercent = 100;
    private int positionXSteps;
    private int positionYSteps;
    private int positionZSteps;
    private double progress;
    private string status = string.Empty;
    private string currentTool = "-";
    private WegstrSurfaceMap? surfaceMap;
    private WegstrCompiledProgram? preparedCompensatedProgram;
    private bool isSurfaceCompensationEnabled;
    private bool probeDisconnectRequired;
    private string surfaceSummary = string.Empty;
    private bool isSafetyNoticeVisible;

    public WegstrMachineViewModel(Window owner, Func<WegstrProgramSet> preparePrograms, Func<WegstrDirectSettings> directSettings, Func<bool> isSafetyNoticeAcknowledged, Action acknowledgeSafetyNotice)
    {
        this.owner = owner;
        this.preparePrograms = preparePrograms;
        this.directSettings = directSettings;
        this.isSafetyNoticeAcknowledged = isSafetyNoticeAcknowledged;
        this.acknowledgeSafetyNotice = acknowledgeSafetyNotice;
        isSafetyNoticeVisible = !isSafetyNoticeAcknowledged();
        manualSpeedPercent = directSettings().SpeedPercent;
        status = L("WegstrStatusSimulatorSelected");
        surfaceSummary = L("WegstrSurfaceNotMeasured");
        RefreshPortsCommand = new(_ => RefreshPorts(), _ => !IsRunning && !IsManualMoving && !isConnecting);
        PrepareCommand = new(_ => Prepare(), _ => !IsRunning && !IsManualMoving);
        ConnectCommand = new(_ => Connect(), _ => !IsConnected && !IsRunning && !IsManualMoving && !isConnecting && (UseSimulator || !string.IsNullOrWhiteSpace(SelectedPort)));
        DisconnectCommand = new(_ => Disconnect(), _ => IsConnected && !IsRunning && !IsManualMoving);
        // Surface probing is optional. Each run performs its own controller preflight
        // and will still refuse an unsafe controller state before toolpath packets run.
        StartCommand = new(_ => StartOrResume(), _ => IsConnected && !IsManualMoving && SelectedProgram is not null && (!IsRunning || IsPaused));
        PauseCommand = new(_ => Pause(), _ => IsRunning && !IsPaused);
        StopCommand = new(_ => Stop(), _ => IsConnected && !isStopping);
        RapidCommand = new(_ => ManualSpeedPercent = 100, _ => !IsRunning && !IsManualMoving);
        ToggleSpindleCommand = new(_ => ToggleSpindle(), _ => CanUseManualControls);
        GoToZeroCommand = new(_ => GoToZero(), _ => CanUseManualControls && (positionXSteps != 0 || positionYSteps != 0 || positionZSteps != 0));
        ZeroAxisCommand = new(ZeroAxis, _ => !IsRunning && !IsManualMoving);
        MeasureSurfaceCommand = new(_ => MeasureSurface(), _ => CanMeasureSurface);
        ResetSurfaceCommand = new(_ => ResetSurfaceMap(), _ => surfaceMap is not null && !IsRunning && !IsManualMoving);
        RecoverCommand = new(_ => Recover(), _ => IsConnected && !IsRunning && !IsManualMoving && !isStopping);
        DismissSafetyNoticeCommand = new(_ => DismissSafetyNotice());
        RefreshPorts();
    }

    public ObservableCollection<string> Ports { get; } = [];
    public ObservableCollection<WegstrPreparedProgram> Programs { get; } = [];
    public RelayCommand RefreshPortsCommand { get; }
    public RelayCommand PrepareCommand { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RapidCommand { get; }
    public RelayCommand ToggleSpindleCommand { get; }
    public RelayCommand GoToZeroCommand { get; }
    public RelayCommand ZeroAxisCommand { get; }
    public RelayCommand MeasureSurfaceCommand { get; }
    public RelayCommand ResetSurfaceCommand { get; }
    public RelayCommand RecoverCommand { get; }
    public RelayCommand DismissSafetyNoticeCommand { get; }

    public WegstrPreparedProgram? SelectedProgram
    {
        get => selectedProgram;
        set { if (SetProperty(ref selectedProgram, value)) RefreshCommands(); }
    }

    public string? SelectedPort
    {
        get => selectedPort;
        set { if (SetProperty(ref selectedPort, value)) { OnPropertyChanged(nameof(ConnectionCaption)); RefreshCommands(); } }
    }

    public bool UseSimulator
    {
        get => useSimulator;
        set
        {
            if (!ConnectionModeEnabled) return;
            if (SetProperty(ref useSimulator, value))
            {
                OnPropertyChanged(nameof(HardwareControlsVisibility));
                OnPropertyChanged(nameof(ConnectionCaption));
                RefreshCommands();
            }
        }
    }

    public Visibility HardwareControlsVisibility => UseSimulator ? Visibility.Collapsed : Visibility.Visible;
    public bool ConnectionModeEnabled => !IsConnected && !IsRunning && !IsManualMoving && !isConnecting;
    public bool CanUseManualControls => IsConnected && IsMachineReady && !IsRunning && !IsManualMoving && !probeDisconnectRequired;
    public bool CanMeasureSurface => IsConnected && !IsRunning && !IsManualMoving && Programs.Count > 0;
    public bool CanJog => IsConnected && IsMachineReady && !IsRunning && !probeDisconnectRequired && (!IsManualMoving || jogRequested);
    public bool CoordinatesEditable => !IsRunning && !IsManualMoving;
    public bool IsConnected { get => isConnected; private set { if (SetProperty(ref isConnected, value)) { OnPropertyChanged(nameof(ConnectionModeEnabled)); OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanMeasureSurface)); OnPropertyChanged(nameof(CanJog)); OnPropertyChanged(nameof(ConnectionCaption)); OnPropertyChanged(nameof(ConnectionButtonText)); RefreshCommands(); } } }
    public bool IsMachineReady { get => isMachineReady; private set { if (SetProperty(ref isMachineReady, value)) { OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanMeasureSurface)); OnPropertyChanged(nameof(CanJog)); OnPropertyChanged(nameof(ConnectionCaption)); OnPropertyChanged(nameof(ConnectionButtonText)); RefreshCommands(); } } }
    public bool IsRunning { get => isRunning; private set { if (SetProperty(ref isRunning, value)) { OnPropertyChanged(nameof(ConnectionModeEnabled)); OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanMeasureSurface)); OnPropertyChanged(nameof(CanJog)); OnPropertyChanged(nameof(CoordinatesEditable)); RefreshCommands(); } } }
    public bool IsPaused { get => isPaused; private set { if (SetProperty(ref isPaused, value)) RefreshCommands(); } }
    public bool IsManualMoving { get => isManualMoving; private set { if (SetProperty(ref isManualMoving, value)) { OnPropertyChanged(nameof(ConnectionModeEnabled)); OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanMeasureSurface)); OnPropertyChanged(nameof(CanJog)); OnPropertyChanged(nameof(CoordinatesEditable)); RefreshCommands(); } } }
    public bool IsSpindleOn { get => isSpindleOn; private set => SetProperty(ref isSpindleOn, value); }
    public int ManualSpeedPercent
    {
        get => manualSpeedPercent;
        set
        {
            value = Math.Clamp(value, 1, 100);
            if (SetProperty(ref manualSpeedPercent, value)) OnPropertyChanged(nameof(ManualModeLabel));
        }
    }
    public string ManualModeLabel => ManualSpeedPercent == 1 ? L("WegstrModeStepping") : L("WegstrModeContinuous", ManualSpeedPercent);
    public double PositionXMillimetres { get => StepsToMillimetres(positionXSteps, directSettings().CalibrationX); set => SetPosition(WegstrAxis.X, value); }
    public double PositionYMillimetres { get => StepsToMillimetres(positionYSteps, directSettings().CalibrationY); set => SetPosition(WegstrAxis.Y, value); }
    public double PositionZMillimetres { get => StepsToMillimetres(positionZSteps, directSettings().CalibrationZ); set => SetPosition(WegstrAxis.Z, value); }
    public double Progress { get => progress; private set => SetProperty(ref progress, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string CurrentTool { get => currentTool; private set => SetProperty(ref currentTool, value); }
    public string SurfaceSummary { get => surfaceSummary; private set => SetProperty(ref surfaceSummary, value); }
    public Visibility SafetyNoticeVisibility => isSafetyNoticeVisible ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionCaption => UseSimulator
        ? L("WegstrSimulatorActive")
        : !IsConnected ? L("WegstrConnectionNotConnected")
        : IsMachineReady ? L("WegstrConnectionReady", link?.Name ?? "-") : L("WegstrConnectionNotReady", link?.Name ?? "-");
    public string ConnectionButtonText => IsConnected ? L("WegstrUsbConnected") : L("WegstrConnect");
    public bool IsSurfaceMapReady => surfaceMap is not null;
    public bool IsSurfaceCompensationEnabled
    {
        get => isSurfaceCompensationEnabled;
        set
        {
            if (value && surfaceMap is null) return;
            SetProperty(ref isSurfaceCompensationEnabled, value);
        }
    }

    public void ClearPreparedOperations()
    {
        // Prepared toolpaths belong to one particular panel. Do not discard them while
        // a physical command is active, but never retain them for a new/opened project.
        if (IsRunning || IsManualMoving) return;
        Programs.Clear();
        SelectedProgram = null;
        ResetSurfaceMap();
        Progress = 0;
        CurrentTool = "-";
        OnPropertyChanged(nameof(CanMeasureSurface));
        Status = L("WegstrStatusPreparedOperationsCleared");
        RefreshCommands();
    }

    public void BeginJog(WegstrAxis axis, int direction)
    {
        if (!CanUseManualControls || activeManualTask is { IsCompleted: false }) return;
        jogRequested = true;
        manualStopRequested = false;
        OnPropertyChanged(nameof(CanJog));
        activeManualTask = JogAsync(axis, direction);
    }

    public void EndJog()
    {
        jogRequested = false;
        OnPropertyChanged(nameof(CanJog));
    }

    private void RefreshPorts()
    {
        var previous = SelectedPort;
        Ports.Clear();
        foreach (var name in SerialPort.GetPortNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) Ports.Add(name);
        SelectedPort = previous is not null && Ports.Contains(previous) ? previous : Ports.FirstOrDefault();
        Status = Ports.Count == 0 ? L("WegstrStatusNoPorts") : L("WegstrStatusPortsFound", Ports.Count);
    }

    private async void Prepare()
    {
        try
        {
            Status = L("WegstrStatusPreparing");
            var set = preparePrograms();
            var machine = directSettings();
            var compiled = await Task.Run(() => set.Programs.Select(program =>
            {
                var result = compiler.Compile(program.DisplayName, program.GCode, machine.CalibrationX, machine.CalibrationY, machine.CalibrationZ, machine.SpeedPercent);
                return new WegstrPreparedProgram(program.FileName, result, program.GCode);
            }).ToList());
            Programs.Clear();
            foreach (var program in compiled) Programs.Add(program);
            SelectedProgram = Programs.FirstOrDefault();
            ResetSurfaceMap();
            OnPropertyChanged(nameof(CanMeasureSurface));
            RefreshCommands();
            Status = L("WegstrStatusPrepared", Programs.Count) + (set.Warnings.Count > 0 ? $" {L("WegstrStatusWarnings", string.Join(" ", set.Warnings))}" : string.Empty);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException or NotSupportedException)
        {
            Status = L("WegstrStatusPreparationFailed", exception.Message);
        }
    }

    private async void Connect()
    {
        isConnecting = true;
        OnPropertyChanged(nameof(ConnectionModeEnabled));
        RefreshCommands();
        try
        {
            var machine = directSettings();
            link = UseSimulator ? new SimulatedWegstrMachineLink(machine.CalibrationX, machine.CalibrationY, machine.CalibrationZ) : new SerialWegstrMachineLink(SelectedPort!);
            if (!UseSimulator)
            {
                var answer = MessageBox.Show(owner,
                    L("WegstrConfirmConnect", SelectedPort ?? "-"),
                    L("WegstrConfirmConnectTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes) { await link.DisposeAsync(); link = null; return; }
            }
            await link.OpenAsync(CancellationToken.None);
            var probe = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), false, CancellationToken.None);
            if (probe.Status is not (WegstrMachineStatus.Ready or WegstrMachineStatus.SpindleOverload or WegstrMachineStatus.MachineReleased)) throw new InvalidOperationException(L("WegstrErrorUnexpectedControllerResponse", (byte)probe.Status));
            IsConnected = true;
            IsMachineReady = probe.Status == WegstrMachineStatus.Ready;
            IsSpindleOn = false;
            Status = probe.Status switch
            {
                WegstrMachineStatus.Ready => L("WegstrStatusConnectedReady", link.Name),
                WegstrMachineStatus.SpindleOverload => L("WegstrStatusConnectedOverload", link.Name),
                _ => L("WegstrStatusConnectedReleased", link.Name),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            Status = L("WegstrStatusConnectionFailed", exception.Message);
            if (link is not null) await link.DisposeAsync();
            link = null;
            IsConnected = false;
            IsMachineReady = false;
        }
        finally
        {
            isConnecting = false;
            OnPropertyChanged(nameof(ConnectionModeEnabled));
            RefreshCommands();
        }
    }

    private async void Disconnect()
    {
        if (link is not null)
        {
            try { if (link.IsOpen) await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), false, CancellationToken.None); } catch { }
            await link.DisposeAsync();
        }
        link = null;
        IsConnected = false;
        IsMachineReady = false;
        IsSpindleOn = false;
        Status = L("WegstrStatusDisconnected");
    }

    private void DismissSafetyNotice()
    {
        if (!isSafetyNoticeVisible) return;
        acknowledgeSafetyNotice();
        isSafetyNoticeVisible = false;
        OnPropertyChanged(nameof(SafetyNoticeVisibility));
    }

    private async void Recover()
    {
        if (link is null || IsRunning || IsManualMoving || isStopping) return;
        isStopping = true;
        RefreshCommands();
        try
        {
            runStopRequested = false;
            manualStopRequested = false;
            ResetSurfaceMap();
            var response = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), false, CancellationToken.None);
            IsSpindleOn = false;
            IsMachineReady = response.Status == WegstrMachineStatus.Ready;
            if (response.Status == WegstrMachineStatus.Ready)
            {
                Status = L("WegstrStatusRecovered", link.Name);
            }
            else
            {
                Status = L("WegstrStatusRecoveryFailed", (byte)response.Status);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusRecoveryFailed", exception.Message);
        }
        finally
        {
            isStopping = false;
            RefreshCommands();
        }
    }

    private void MeasureSurface()
    {
        if (!CanMeasureSurface || link is null) return;
        manualStopRequested = false;
        var machine = directSettings();
        WegstrSurfaceProbePlan plan;
        try
        {
            plan = surfaceLeveler.CreateProbePlan(Programs.Select(program => program.Compiled), machine.AutoLevelRasterMillimetres, machine.CalibrationX, machine.CalibrationY);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            Status = L("WegstrStatusSurfacePlanFailed", exception.Message);
            return;
        }

        if (!UseSimulator)
        {
            var answer = MessageBox.Show(owner,
                L("WegstrConfirmSurfaceMeasure", plan.Points.Count, link.Name, machine.AutoLevelSafeZMillimetres),
                L("WegstrConfirmSurfaceMeasureTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        ResetSurfaceMap();
        runCancellation = new();
        runStopRequested = false;
        activeRunTask = MeasureSurfaceAsync(plan, machine, runCancellation.Token);
    }

    private async Task MeasureSurfaceAsync(WegstrSurfaceProbePlan plan, WegstrDirectSettings machine, CancellationToken cancellationToken)
    {
        IsRunning = true;
        IsPaused = false;
        Progress = 0;
        CurrentTool = L("WegstrProbeTool");
        var measured = new List<int>(plan.Points.Count);
        try
        {
            var safeZSteps = MillimetresToSteps(machine.AutoLevelSafeZMillimetres, machine.CalibrationZ);
            var probeLimitSteps = MillimetresToSteps(-10, machine.CalibrationZ);
            for (var index = 0; index < plan.Points.Count; index++)
            {
                if (runStopRequested) throw new OperationCanceledException();
                var point = plan.Points[index];
                if (positionZSteps < safeZSteps) await SendFiniteMoveAsync(0, 0, safeZSteps - positionZSteps, cancellationToken);
                await SendFiniteMoveAsync(point.XSteps - positionXSteps, point.YSteps - positionYSteps, 0, cancellationToken);
                var controllerTouchedZ = await ProbeDownAsync(probeLimitSteps, cancellationToken);
                measured.Add(controllerTouchedZ);
                Progress = (index + 1) * 100d / plan.Points.Count;
                Status = L("WegstrStatusSurfaceMeasuring", index + 1, plan.Points.Count, StepsToMillimetres(controllerTouchedZ, machine.CalibrationZ));
            }

            if (positionZSteps < safeZSteps) await SendFiniteMoveAsync(0, 0, safeZSteps - positionZSteps, cancellationToken);
            await SendFiniteMoveAsync(-positionXSteps, -positionYSteps, 0, cancellationToken);
            surfaceMap = surfaceLeveler.CreateSurfaceMap(plan, measured);
            IsSurfaceCompensationEnabled = true;
            SetProbeDisconnectRequired(false);
            OnPropertyChanged(nameof(IsSurfaceMapReady));
            var minimum = StepsToMillimetres(surfaceMap.MinimumCorrectionSteps, machine.CalibrationZ);
            var maximum = StepsToMillimetres(surfaceMap.MaximumCorrectionSteps, machine.CalibrationZ);
            SurfaceSummary = L("WegstrSurfaceMeasured", surfaceMap.PointCount, minimum, maximum);
            // The head remains at the safe Z above X0 Y0, so the probe opens by itself.
            // Reassert the ready state and selection so the next user action is an ordinary program start,
            // not a recovery that would discard the freshly measured surface map.
            IsMachineReady = true;
            var copperIsolation = Programs.FirstOrDefault(program => program.FileName.Equals("01-copper-isolation.txt", StringComparison.OrdinalIgnoreCase)) ?? Programs.FirstOrDefault();
            SelectedProgram = null;
            SelectedProgram = copperIsolation;
            if (SelectedProgram is not null)
            {
                Status = L("WegstrStatusPreparingCompensation");
                preparedCompensatedProgram = await Task.Run(
                    () => surfaceLeveler.Apply(SelectedProgram.Compiled, surfaceMap, machine.AutoLevelZBacklashSteps, cancellationToken),
                    cancellationToken);
                AppLog.Information($"Wegstr surface compensation prepared after probing: {SelectedProgram.FileName}; packets={preparedCompensatedProgram.PacketCount}.");
                LogCompensationPacketComparison(SelectedProgram.Compiled, preparedCompensatedProgram);
            }
            Progress = 0;
            Status = L("WegstrStatusSurfaceReadyToStart", surfaceMap.PointCount, minimum, maximum, SelectedProgram?.DisplayName ?? "-");
            AppLog.Information($"Wegstr surface measurement completed. Compensation is active; selected program={SelectedProgram?.FileName ?? "none"}; machine work position is X0 Y0 Z{positionZSteps} above the board.");
        }
        catch (OperationCanceledException)
        {
            Status = L("WegstrStatusSurfaceStopped");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusSurfaceFailed", exception.Message);
        }
        finally
        {
            IsRunning = false;
            CurrentTool = "-";
            runCancellation?.Dispose();
            runCancellation = null;
            RefreshCommands();
        }
    }

    private async Task<int> ProbeDownAsync(int probeLimitSteps, CancellationToken cancellationToken)
    {
        if (link is null) throw new InvalidOperationException(L("WegstrErrorNotConnected"));
        var initialResponse = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), true, cancellationToken);
        AppLog.Information($"Wegstr probe preflight response: status={(byte)initialResponse.Status}; touchZ={(initialResponse.ProbePositionSteps?.ToString() ?? "none")}; requestedZ={positionZSteps}.");
        ThrowForSafetyStatus(initialResponse.Status);
        if (initialResponse.Status == WegstrMachineStatus.Probe && initialResponse.ProbePositionSteps is short initialControllerTouchedZ)
        {
            return AcceptProbeContact(initialControllerTouchedZ);
        }
        if (initialResponse.Status != WegstrMachineStatus.Ready) throw new WegstrSafetyException(L("WegstrErrorProbePreflightResponse", (byte)initialResponse.Status));
        foreach (var packet in WegstrManualMotionPlanner.PlanFiniteMove(0, 0, probeLimitSteps - positionZSteps, 100, false))
        {
            if (runStopRequested) throw new OperationCanceledException();
            var response = await link.ExchangeAsync(packet, true, cancellationToken);
            AppLog.Information($"Wegstr probe response: status={(byte)response.Status}; touchZ={(response.ProbePositionSteps?.ToString() ?? "none")}; requestedZ={positionZSteps}.");
            ThrowForSafetyStatus(response.Status);
            if (response.Status == WegstrMachineStatus.Probe && response.ProbePositionSteps is short controllerTouchedZ)
            {
                return AcceptProbeContact(controllerTouchedZ);
            }
            if (response.Status != WegstrMachineStatus.Ready) throw new WegstrSafetyException(L("WegstrErrorProbeResponse", (byte)response.Status));
            ApplyAcknowledgedPacket(packet);
        }
        throw new WegstrSafetyException(L("WegstrErrorProbeLimit"));
    }

    private int AcceptProbeContact(int controllerTouchedZ)
    {
        // The Wegstr probe reply contains its own absolute Z counter. It is not in the work
        // coordinate system that the operator zeroes in this application. Never use it for
        // subsequent motion: a controller origin of e.g. -13652 would otherwise command a
        // roughly 55 mm retract when this work coordinate is zeroed at the board surface.
        // Keep the raw value only for the relative surface map and rebase local work Z at the
        // confirmed contact, so the next move is exactly AutoLevelSafeZ above that contact.
        positionZSteps = 0;
        IsSpindleOn = false;
        OnPropertyChanged(nameof(PositionZMillimetres));
        AppLog.Information($"Wegstr probe contact accepted: controllerTouchZ={controllerTouchedZ}; localWorkZ=0.");
        return controllerTouchedZ;
    }

    private void ResetSurfaceMap()
    {
        surfaceMap = null;
        preparedCompensatedProgram = null;
        IsSurfaceCompensationEnabled = false;
        SetProbeDisconnectRequired(false);
        SurfaceSummary = L("WegstrSurfaceNotMeasured");
        OnPropertyChanged(nameof(IsSurfaceMapReady));
        RefreshCommands();
    }

    private void StartOrResume()
    {
        AppLog.Information($"Wegstr start requested: connected={IsConnected}; ready={IsMachineReady}; running={IsRunning}; paused={IsPaused}; manualMoving={IsManualMoving}; selectedProgram={SelectedProgram?.FileName ?? "none"}; compensation={IsSurfaceCompensationEnabled}; surfaceMap={surfaceMap is not null}.");
        if (IsPaused && IsRunning)
        {
            IsPaused = false;
            resumeSignal.TrySetResult(true);
            Status = L("WegstrStatusResuming");
            return;
        }
        if (SelectedProgram is null || link is null) return;
        if (!UseSimulator)
        {
            var answer = MessageBox.Show(owner,
                L("WegstrConfirmStart", SelectedProgram.DisplayName, link.Name, SelectedProgram.PacketCount, SelectedProgram.EstimatedDuration),
                L("WegstrConfirmStartTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }
        runCancellation = new();
        runStopRequested = false;
        AppLog.Information($"Wegstr program start accepted: {SelectedProgram.FileName}; packets={SelectedProgram.PacketCount}; compensation={IsSurfaceCompensationEnabled && surfaceMap is not null}.");
        activeRunTask = RunAsync(SelectedProgram, runCancellation.Token);
    }

    private async Task RunAsync(WegstrPreparedProgram program, CancellationToken cancellationToken)
    {
        IsRunning = true;
        IsPaused = false;
        Progress = 0;
        var completedPackets = 0;
        try
        {
            var machine = directSettings();
            WegstrCompiledProgram compiled;
            if (IsSurfaceCompensationEnabled && surfaceMap is not null && preparedCompensatedProgram?.Name.Equals(program.Compiled.Name, StringComparison.Ordinal) == true)
            {
                compiled = preparedCompensatedProgram;
                AppLog.Information($"Wegstr compensated run begins: program={program.FileName}; mapPoints={surfaceMap.PointCount}; correctionSteps={surfaceMap.MinimumCorrectionSteps}..{surfaceMap.MaximumCorrectionSteps}; workPosition={positionXSteps},{positionYSteps},{positionZSteps}; spindleOn={IsSpindleOn}.");
            }
            else
            {
                compiled = program.Compiled;
                if (IsSurfaceCompensationEnabled && surfaceMap is not null) AppLog.Information($"Wegstr run without surface compensation: {program.FileName} is not the measured copper-isolation program.");
            }

            await VerifyProbeReleasedBeforeRunAsync(cancellationToken);

            for (var blockIndex = 0; blockIndex < compiled.Blocks.Count; blockIndex++)
            {
                var block = compiled.Blocks[blockIndex];
                CurrentTool = block.ToolNumber == 0 ? "Current tool" : $"T{block.ToolNumber}";
                if (blockIndex > 0)
                {
                    IsPaused = true;
                    resumeSignal = NewSignal();
                    Status = L("WegstrStatusToolChange", block.ToolNumber);
                    await resumeSignal.Task.WaitAsync(cancellationToken);
                    if (runStopRequested) throw new OperationCanceledException();
                    IsPaused = false;
                }
                foreach (var packet in block.Packets)
                {
                    if (IsPaused)
                    {
                        await PauseSpindleAsync(cancellationToken);
                        Status = L("WegstrStatusPaused");
                        await resumeSignal.Task.WaitAsync(cancellationToken);
                        if (runStopRequested) throw new OperationCanceledException();
                        await RestartSpindleAfterPauseAsync(cancellationToken);
                    }
                    var diagnosticContext = completedPackets < 12 ? $"program packet {completedPackets + 1}/{compiled.PacketCount}" : null;
                    await ExchangeReadyAsync(packet, cancellationToken, diagnosticContext);
                    ApplyAcknowledgedPacket(packet);
                    if (runStopRequested) throw new OperationCanceledException();
                    completedPackets++;
                    Progress = completedPackets * 100d / compiled.PacketCount;
                    Status = L("WegstrStatusRunning", program.DisplayName, completedPackets, compiled.PacketCount);
                }
            }
            Status = L("WegstrStatusCompleted", program.DisplayName);
        }
        catch (OperationCanceledException)
        {
            Status = L("WegstrStatusStopped");
        }
        catch (WegstrProbeStillActiveException exception)
        {
            IsMachineReady = true;
            Status = L("WegstrStatusProbeDisconnectRequired", exception.Message);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusSafetyStop", exception.Message);
        }
        catch (Exception exception)
        {
            AppLog.Error("Wegstr machining failed unexpectedly.", exception);
            IsMachineReady = false;
            Status = L("WegstrStatusSafetyStop", exception.Message);
        }
        finally
        {
            await SendFinalSpindleOffAsync();
            await RaiseToolForToolChangeAsync();
            IsPaused = false;
            IsRunning = false;
            CurrentTool = "-";
            runCancellation?.Dispose();
            runCancellation = null;
        }
    }

    private async Task JogAsync(WegstrAxis axis, int direction)
    {
        IsManualMoving = true;
        var planner = new WegstrManualMotionPlanner();
        try
        {
            do
            {
                var packet = planner.NextContinuousPacket(axis, direction, ManualSpeedPercent, IsSpindleOn);
                await ExchangeReadyAsync(packet, lifetimeCancellation.Token);
                ApplyAcknowledgedPacket(packet);
                Status = ManualSpeedPercent == 1
                    ? L("WegstrStatusManualStep", axis, direction > 0 ? "+" : "-")
                    : L("WegstrStatusManualMove", axis, direction > 0 ? "+" : "-", ManualSpeedPercent);
                if (ManualSpeedPercent == 1) jogRequested = false;
            }
            while (jogRequested && !manualStopRequested);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusManualSafetyStop", exception.Message);
        }
        finally
        {
            jogRequested = false;
            IsManualMoving = false;
        }
    }

    private void ToggleSpindle()
    {
        if (!CanUseManualControls) return;
        IsManualMoving = true;
        activeManualTask = ToggleSpindleAsync();
    }

    private async Task ToggleSpindleAsync()
    {
        try
        {
            var requested = !IsSpindleOn;
            var packet = WegstrPacketCodec.SpindlePacket(requested);
            await ExchangeReadyAsync(packet, lifetimeCancellation.Token);
            ApplyAcknowledgedPacket(packet);
            Status = requested ? L("WegstrStatusSpindleOn") : L("WegstrStatusSpindleOff");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusSpindleFailed", exception.Message);
        }
        finally { IsManualMoving = false; }
    }

    private async void GoToZero()
    {
        if (!CanUseManualControls || link is null) return;
        if (!UseSimulator)
        {
            var answer = MessageBox.Show(owner,
                L("WegstrConfirmReturnZero"),
                L("WegstrConfirmReturnZeroTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        manualStopRequested = false;
        IsManualMoving = true;
        activeManualTask = GoToZeroAsync();
        await activeManualTask;
    }

    private async Task GoToZeroAsync()
    {
        try
        {
            var machine = directSettings();
            var safeZSteps = MillimetresToSteps(machine.SafeZMillimetres, machine.CalibrationZ);
            if (positionZSteps < safeZSteps) await SendFiniteMoveAsync(0, 0, safeZSteps - positionZSteps, lifetimeCancellation.Token);
            if (!manualStopRequested) await SendFiniteMoveAsync(-positionXSteps, -positionYSteps, 0, lifetimeCancellation.Token);
            if (!manualStopRequested) await SendFiniteMoveAsync(0, 0, -positionZSteps, lifetimeCancellation.Token);
            Status = manualStopRequested ? L("WegstrStatusReturnZeroStopped") : L("WegstrStatusReturnedZero");
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusReturnZeroSafetyStop", exception.Message);
        }
        finally { IsManualMoving = false; }
    }

    private async Task SendFiniteMoveAsync(int deltaX, int deltaY, int deltaZ, CancellationToken cancellationToken)
    {
        foreach (var packet in WegstrManualMotionPlanner.PlanFiniteMove(deltaX, deltaY, deltaZ, ManualSpeedPercent, IsSpindleOn))
        {
            if (manualStopRequested) break;
            await ExchangeReadyAsync(packet, cancellationToken);
            ApplyAcknowledgedPacket(packet);
        }
    }

    private async Task ExchangeReadyAsync(byte[] packet, CancellationToken cancellationToken, string? diagnosticContext = null)
    {
        if (link is null) throw new InvalidOperationException(L("WegstrErrorNotConnected"));
        Stopwatch? stopwatch = null;
        if (diagnosticContext is not null)
        {
            stopwatch = Stopwatch.StartNew();
            AppLog.Information($"Wegstr {diagnosticContext} TX: {DescribePacket(packet)}.");
        }
        WegstrControllerResponse response;
        try
        {
            response = await link.ExchangeAsync(packet, false, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            AppLog.Error($"Wegstr packet exchange failed: {Convert.ToHexString(packet)}.", exception);
            throw;
        }
        ThrowForSafetyStatus(response.Status);
        if (response.Status != WegstrMachineStatus.Ready) throw new WegstrSafetyException(L("WegstrErrorUnexpectedControllerResponse", (byte)response.Status));
        if (diagnosticContext is not null)
        {
            stopwatch!.Stop();
            AppLog.Information($"Wegstr {diagnosticContext} RX: status={(byte)response.Status}; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F1}.");
        }
    }

    private async Task VerifyProbeReleasedBeforeRunAsync(CancellationToken cancellationToken)
    {
        if (link is null) throw new InvalidOperationException(L("WegstrErrorNotConnected"));
        AppLog.Information("Wegstr machining preflight TX: checking that the probe circuit is open.");
        var response = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), false, cancellationToken);
        AppLog.Information($"Wegstr machining preflight RX: status={(byte)response.Status}; touchZ={(response.ProbePositionSteps?.ToString() ?? "none")}.");
        ThrowForSafetyStatus(response.Status);
        if (response.Status == WegstrMachineStatus.Probe) throw new WegstrProbeStillActiveException(L("WegstrErrorDisconnectProbe"));
        if (response.Status != WegstrMachineStatus.Ready) throw new WegstrSafetyException(L("WegstrErrorUnexpectedControllerResponse", (byte)response.Status));
        SetProbeDisconnectRequired(false);
    }

    private static void LogCompensationPacketComparison(WegstrCompiledProgram source, WegstrCompiledProgram compensated)
    {
        var sourcePackets = source.Blocks.SelectMany(block => block.Packets).ToArray();
        var compensatedPackets = compensated.Blocks.SelectMany(block => block.Packets).ToArray();
        var comparedPackets = Math.Min(sourcePackets.Length, compensatedPackets.Length);
        var changedPacketIndexes = Enumerable.Range(0, comparedPackets).Where(index => !sourcePackets[index].SequenceEqual(compensatedPackets[index])).ToArray();
        AppLog.Information($"Wegstr compensation packet comparison: sourcePackets={sourcePackets.Length}; compensatedPackets={compensatedPackets.Length}; changedPackets={changedPacketIndexes.Length}; firstChangedPacket={(changedPacketIndexes.Length == 0 ? "none" : (changedPacketIndexes[0] + 1).ToString())}.");
        for (var index = 0; index < Math.Min(12, comparedPackets); index++)
        {
            AppLog.Information($"Wegstr compensation packet {index + 1}: equal={sourcePackets[index].SequenceEqual(compensatedPackets[index])}; source={DescribePacket(sourcePackets[index])}; compensated={DescribePacket(compensatedPackets[index])}.");
        }
        if (changedPacketIndexes.Length == 0) return;
        var firstChangedIndex = changedPacketIndexes[0];
        AppLog.Information($"Wegstr compensation first changed packet {firstChangedIndex + 1}: source={DescribePacket(sourcePackets[firstChangedIndex])}; compensated={DescribePacket(compensatedPackets[firstChangedIndex])}.");
    }

    private static string DescribePacket(byte[] packet) => $"{Convert.ToHexString(packet)} [{string.Join(", ", WegstrPacketCodec.Decode(packet).Select(step => $"X{step.X} Y{step.Y} Z{step.Z} S{(step.SpindleOn ? 1 : 0)}"))}]";

    private static void ThrowForSafetyStatus(WegstrMachineStatus status)
    {
        if (status == WegstrMachineStatus.SpindleOverload) throw new WegstrSafetyException(L("WegstrErrorSpindleOverload"));
        if (status == WegstrMachineStatus.MachineReleased) throw new WegstrSafetyException(L("WegstrErrorEmergency"));
    }

    private void ApplyAcknowledgedPacket(byte[] packet)
    {
        var steps = WegstrPacketCodec.Decode(packet);
        positionXSteps += steps.Sum(step => step.X);
        positionYSteps += steps.Sum(step => step.Y);
        positionZSteps += steps.Sum(step => step.Z);
        IsSpindleOn = steps[^1].SpindleOn;
        OnPropertyChanged(nameof(PositionXMillimetres));
        OnPropertyChanged(nameof(PositionYMillimetres));
        OnPropertyChanged(nameof(PositionZMillimetres));
        RefreshCommands();
    }

    private void SetPosition(WegstrAxis axis, double millimetres)
    {
        if (!CoordinatesEditable || double.IsNaN(millimetres) || double.IsInfinity(millimetres)) return;
        // Z nula is set after a tool change. It must not discard the measured
        // XY surface map: only a changed XY origin makes that map invalid.
        if (surfaceMap is not null && (axis is WegstrAxis.X or WegstrAxis.Y)) ResetSurfaceMap();
        millimetres = Math.Clamp(millimetres, -9999, 9999);
        var machine = directSettings();
        switch (axis)
        {
            case WegstrAxis.X: positionXSteps = MillimetresToSteps(millimetres, machine.CalibrationX); OnPropertyChanged(nameof(PositionXMillimetres)); break;
            case WegstrAxis.Y: positionYSteps = MillimetresToSteps(millimetres, machine.CalibrationY); OnPropertyChanged(nameof(PositionYMillimetres)); break;
            case WegstrAxis.Z: positionZSteps = MillimetresToSteps(millimetres, machine.CalibrationZ); OnPropertyChanged(nameof(PositionZMillimetres)); break;
        }
        RefreshCommands();
    }

    private void ZeroAxis(object? parameter)
    {
        // The small Z "0" button establishes the current tool's Z origin after
        // replacement. Keep a completed surface measurement for that workflow.
        if (surfaceMap is not null && ((parameter as string) is "X" or "Y" or "All")) ResetSurfaceMap();
        switch (parameter as string)
        {
            case "X": positionXSteps = 0; OnPropertyChanged(nameof(PositionXMillimetres)); break;
            case "Y": positionYSteps = 0; OnPropertyChanged(nameof(PositionYMillimetres)); break;
            case "Z": positionZSteps = 0; OnPropertyChanged(nameof(PositionZMillimetres)); break;
            case "All":
                positionXSteps = positionYSteps = positionZSteps = 0;
                OnPropertyChanged(nameof(PositionXMillimetres));
                OnPropertyChanged(nameof(PositionYMillimetres));
                OnPropertyChanged(nameof(PositionZMillimetres));
                break;
        }
        Status = IsConnected && !IsMachineReady
            ? L("WegstrStatusRecoveryRequiredAfterReset")
            : L("WegstrStatusCoordinatesReset");
        RefreshCommands();
    }

    private void Pause()
    {
        IsPaused = true;
        pauseSpindleRequested = true;
        resumeSignal = NewSignal();
        Status = L("WegstrStatusPauseRequested");
    }

    private async Task PauseSpindleAsync(CancellationToken cancellationToken)
    {
        if (!pauseSpindleRequested) return;
        pauseSpindleRequested = false;
        restartSpindleAfterPause = IsSpindleOn;
        if (!restartSpindleAfterPause) return;
        var packet = WegstrPacketCodec.SpindleOffPacket();
        await ExchangeReadyAsync(packet, cancellationToken);
        ApplyAcknowledgedPacket(packet);
    }

    private async Task RestartSpindleAfterPauseAsync(CancellationToken cancellationToken)
    {
        if (!restartSpindleAfterPause) return;
        restartSpindleAfterPause = false;
        var packet = WegstrPacketCodec.SpindlePacket(true);
        await ExchangeReadyAsync(packet, cancellationToken);
        ApplyAcknowledgedPacket(packet);
    }

    private async void Stop()
    {
        if (isStopping) return;
        isStopping = true;
        RefreshCommands();
        try
        {
            jogRequested = false;
            OnPropertyChanged(nameof(CanJog));
            manualStopRequested = true;
            if (IsRunning || IsPaused)
            {
                Status = L("WegstrStatusStoppingJob");
                runStopRequested = true;
                resumeSignal.TrySetResult(true);
                runCancellation?.Cancel();
                if (activeRunTask is not null) await activeRunTask;
                await SendFinalSpindleOffAsync();
            }
            else
            {
                Status = L("WegstrStatusStoppingManual");
                if (activeManualTask is not null)
                {
                    try { await activeManualTask; }
                    catch (OperationCanceledException) { }
                }
                await SendFinalSpindleOffAsync();
                Status = L("WegstrStatusStopped");
            }
        }
        finally
        {
            isStopping = false;
            RefreshCommands();
        }
    }

    private async Task SendFinalSpindleOffAsync()
    {
        try
        {
            if (link?.IsOpen == true)
            {
                var finalStatus = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), false, CancellationToken.None);
                if (finalStatus.Status == WegstrMachineStatus.Ready) IsSpindleOn = false;
                else if (finalStatus.Status == WegstrMachineStatus.Probe && probeDisconnectRequired) IsSpindleOn = false;
                else IsMachineReady = false;
            }
        }
        catch { IsMachineReady = false; }
    }

    private async Task RaiseToolForToolChangeAsync()
    {
        try
        {
            if (link?.IsOpen != true || !IsMachineReady) return;
            var machine = directSettings();
            // A 2 mm machining clearance is not enough to release a drill from the collet.
            // Between independent operations use a dedicated, conservative tool-change height.
            var safeZSteps = MillimetresToSteps(Math.Max(machine.SafeZMillimetres, 20d), machine.CalibrationZ);
            if (positionZSteps < safeZSteps)
            {
                AppLog.Information($"Wegstr raising tool for the next operation: Z{positionZSteps} -> Z{safeZSteps}.");
                await SendFiniteMoveAsync(0, 0, safeZSteps - positionZSteps, CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusSafetyStop", exception.Message);
        }
    }

    private void RefreshCommands()
    {
        RefreshPortsCommand.RaiseCanExecuteChanged();
        PrepareCommand.RaiseCanExecuteChanged();
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        RapidCommand.RaiseCanExecuteChanged();
        ToggleSpindleCommand.RaiseCanExecuteChanged();
        GoToZeroCommand.RaiseCanExecuteChanged();
        ZeroAxisCommand.RaiseCanExecuteChanged();
        MeasureSurfaceCommand.RaiseCanExecuteChanged();
        ResetSurfaceCommand.RaiseCanExecuteChanged();
        RecoverCommand.RaiseCanExecuteChanged();
    }

    private static int MillimetresToSteps(double millimetres, int calibration) => (int)Math.Round(millimetres * calibration / 10d);
    private static double StepsToMillimetres(int steps, int calibration) => Math.Round(steps * 10d / calibration, 3, MidpointRounding.AwayFromZero);
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<bool> CompletedSignal() { var signal = NewSignal(); signal.SetResult(true); return signal; }
    private static string L(string key, params object[] arguments) => string.Format(LocalizationService.Instance[key], arguments);

    public async ValueTask DisposeAsync()
    {
        jogRequested = false;
        OnPropertyChanged(nameof(CanJog));
        manualStopRequested = true;
        runStopRequested = true;
        resumeSignal.TrySetResult(true);
        if (activeRunTask is not null)
        {
            try { await activeRunTask; } catch (OperationCanceledException) { }
        }
        if (activeManualTask is not null)
        {
            try { await activeManualTask; } catch (OperationCanceledException) { }
        }
        await SendFinalSpindleOffAsync();
        lifetimeCancellation.Cancel();
        if (link is not null) await link.DisposeAsync();
        lifetimeCancellation.Dispose();
    }

    private sealed class WegstrSafetyException(string message) : Exception(message);
    private sealed class WegstrProbeStillActiveException(string message) : Exception(message);

    private void SetProbeDisconnectRequired(bool value)
    {
        if (probeDisconnectRequired == value) return;
        probeDisconnectRequired = value;
        OnPropertyChanged(nameof(CanUseManualControls));
        OnPropertyChanged(nameof(CanJog));
        RefreshCommands();
    }
}

public sealed record WegstrDirectSettings(
    int CalibrationX,
    int CalibrationY,
    int CalibrationZ,
    int SpeedPercent,
    double SafeZMillimetres,
    double AutoLevelRasterMillimetres,
    double AutoLevelSafeZMillimetres,
    int AutoLevelZBacklashSteps);

public sealed class WegstrPreparedProgram(string fileName, WegstrCompiledProgram compiled, string gCode)
{
    public string FileName { get; } = fileName;
    public WegstrCompiledProgram Compiled { get; } = compiled;
    public string GCode { get; } = gCode;
    public string DisplayName => Compiled.Name;
    public int PacketCount => Compiled.PacketCount;
    public int ToolChanges => Math.Max(0, Compiled.Blocks.Count - 1);
    public string EstimatedDuration => TimeSpan.FromSeconds(Compiled.EstimatedDurationSeconds).ToString(@"hh\:mm\:ss");
    public string Summary => string.Format(LocalizationService.Instance["WegstrOperationSummary"], PacketCount, ToolChanges, EstimatedDuration);
}
