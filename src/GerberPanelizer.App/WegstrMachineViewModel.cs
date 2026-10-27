using System.Collections.ObjectModel;
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
    private readonly WegstrGCodeCompiler compiler = new();
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

    public WegstrMachineViewModel(Window owner, Func<WegstrProgramSet> preparePrograms, Func<WegstrDirectSettings> directSettings)
    {
        this.owner = owner;
        this.preparePrograms = preparePrograms;
        this.directSettings = directSettings;
        manualSpeedPercent = directSettings().SpeedPercent;
        status = L("WegstrStatusSimulatorSelected");
        RefreshPortsCommand = new(_ => RefreshPorts(), _ => !IsRunning && !IsManualMoving && !isConnecting);
        PrepareCommand = new(_ => Prepare(), _ => !IsRunning && !IsManualMoving);
        ConnectCommand = new(_ => Connect(), _ => !IsConnected && !IsRunning && !IsManualMoving && !isConnecting && (UseSimulator || !string.IsNullOrWhiteSpace(SelectedPort)));
        DisconnectCommand = new(_ => Disconnect(), _ => IsConnected && !IsRunning && !IsManualMoving);
        StartCommand = new(_ => StartOrResume(), _ => IsConnected && IsMachineReady && !IsManualMoving && SelectedProgram is not null && (!IsRunning || IsPaused));
        PauseCommand = new(_ => Pause(), _ => IsRunning && !IsPaused);
        StopCommand = new(_ => Stop(), _ => IsConnected && !isStopping);
        RapidCommand = new(_ => ManualSpeedPercent = 100, _ => !IsRunning && !IsManualMoving);
        ToggleSpindleCommand = new(_ => ToggleSpindle(), _ => CanUseManualControls);
        GoToZeroCommand = new(_ => GoToZero(), _ => CanUseManualControls && (positionXSteps != 0 || positionYSteps != 0 || positionZSteps != 0));
        ZeroAxisCommand = new(ZeroAxis, _ => !IsRunning && !IsManualMoving);
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

    public WegstrPreparedProgram? SelectedProgram
    {
        get => selectedProgram;
        set { if (SetProperty(ref selectedProgram, value)) RefreshCommands(); }
    }

    public string? SelectedPort
    {
        get => selectedPort;
        set { if (SetProperty(ref selectedPort, value)) RefreshCommands(); }
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
                RefreshCommands();
            }
        }
    }

    public Visibility HardwareControlsVisibility => UseSimulator ? Visibility.Collapsed : Visibility.Visible;
    public bool ConnectionModeEnabled => !IsConnected && !IsRunning && !IsManualMoving && !isConnecting;
    public bool CanUseManualControls => IsConnected && IsMachineReady && !IsRunning && !IsManualMoving;
    public bool CanJog => IsConnected && IsMachineReady && !IsRunning && (!IsManualMoving || jogRequested);
    public bool CoordinatesEditable => !IsRunning && !IsManualMoving;
    public bool IsConnected { get => isConnected; private set { if (SetProperty(ref isConnected, value)) { OnPropertyChanged(nameof(ConnectionModeEnabled)); OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanJog)); RefreshCommands(); } } }
    public bool IsMachineReady { get => isMachineReady; private set { if (SetProperty(ref isMachineReady, value)) { OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanJog)); RefreshCommands(); } } }
    public bool IsRunning { get => isRunning; private set { if (SetProperty(ref isRunning, value)) { OnPropertyChanged(nameof(ConnectionModeEnabled)); OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanJog)); OnPropertyChanged(nameof(CoordinatesEditable)); RefreshCommands(); } } }
    public bool IsPaused { get => isPaused; private set { if (SetProperty(ref isPaused, value)) RefreshCommands(); } }
    public bool IsManualMoving { get => isManualMoving; private set { if (SetProperty(ref isManualMoving, value)) { OnPropertyChanged(nameof(ConnectionModeEnabled)); OnPropertyChanged(nameof(CanUseManualControls)); OnPropertyChanged(nameof(CanJog)); OnPropertyChanged(nameof(CoordinatesEditable)); RefreshCommands(); } } }
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
            link = UseSimulator ? new SimulatedWegstrMachineLink() : new SerialWegstrMachineLink(SelectedPort!);
            if (!UseSimulator)
            {
                var answer = MessageBox.Show(owner,
                    L("WegstrConfirmConnect", SelectedPort ?? "-"),
                    L("WegstrConfirmConnectTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes) { await link.DisposeAsync(); link = null; return; }
            }
            await link.OpenAsync(CancellationToken.None);
            var probe = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), CancellationToken.None);
            if (probe is not (WegstrMachineStatus.Ready or WegstrMachineStatus.SpindleOverload or WegstrMachineStatus.MachineReleased)) throw new InvalidOperationException($"Unexpected Wegstr response: {(byte)probe}.");
            IsConnected = true;
            IsMachineReady = probe == WegstrMachineStatus.Ready;
            IsSpindleOn = false;
            Status = probe switch
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
            try { if (link.IsOpen) await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), CancellationToken.None); } catch { }
            await link.DisposeAsync();
        }
        link = null;
        IsConnected = false;
        IsMachineReady = false;
        IsSpindleOn = false;
        Status = L("WegstrStatusDisconnected");
    }

    private void StartOrResume()
    {
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
            for (var blockIndex = 0; blockIndex < program.Compiled.Blocks.Count; blockIndex++)
            {
                var block = program.Compiled.Blocks[blockIndex];
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
                        Status = L("WegstrStatusPaused");
                        await resumeSignal.Task.WaitAsync(cancellationToken);
                        if (runStopRequested) throw new OperationCanceledException();
                    }
                    await ExchangeReadyAsync(packet, cancellationToken);
                    ApplyAcknowledgedPacket(packet);
                    if (runStopRequested) throw new OperationCanceledException();
                    completedPackets++;
                    Progress = completedPackets * 100d / program.PacketCount;
                    Status = L("WegstrStatusRunning", program.DisplayName, completedPackets, program.PacketCount);
                }
            }
            Status = L("WegstrStatusCompleted", program.DisplayName);
        }
        catch (OperationCanceledException)
        {
            Status = L("WegstrStatusStopped");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or WegstrSafetyException)
        {
            IsMachineReady = false;
            Status = L("WegstrStatusSafetyStop", exception.Message);
        }
        finally
        {
            await SendFinalSpindleOffAsync();
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
            if (positionZSteps < safeZSteps) await SendFiniteMoveAsync(0, 0, safeZSteps - positionZSteps);
            if (!manualStopRequested) await SendFiniteMoveAsync(-positionXSteps, -positionYSteps, 0);
            if (!manualStopRequested) await SendFiniteMoveAsync(0, 0, -positionZSteps);
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

    private async Task SendFiniteMoveAsync(int deltaX, int deltaY, int deltaZ)
    {
        foreach (var packet in WegstrManualMotionPlanner.PlanFiniteMove(deltaX, deltaY, deltaZ, ManualSpeedPercent, IsSpindleOn))
        {
            if (manualStopRequested) break;
            await ExchangeReadyAsync(packet, lifetimeCancellation.Token);
            ApplyAcknowledgedPacket(packet);
        }
    }

    private async Task ExchangeReadyAsync(byte[] packet, CancellationToken cancellationToken)
    {
        if (link is null) throw new InvalidOperationException("Wegstr is not connected.");
        var response = await link.ExchangeAsync(packet, cancellationToken);
        if (response == WegstrMachineStatus.SpindleOverload) throw new WegstrSafetyException("The controller reported spindle overload.");
        if (response == WegstrMachineStatus.MachineReleased) throw new WegstrSafetyException("The controller reported machine release/emergency state.");
        if (response != WegstrMachineStatus.Ready) throw new WegstrSafetyException($"Unexpected controller response {(byte)response}.");
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
        Status = L("WegstrStatusCoordinatesReset");
        RefreshCommands();
    }

    private void Pause()
    {
        IsPaused = true;
        resumeSignal = NewSignal();
        Status = L("WegstrStatusPauseRequested");
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
                if (activeRunTask is not null) await activeRunTask;
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
                var finalStatus = await link.ExchangeAsync(WegstrPacketCodec.SpindleOffPacket(), CancellationToken.None);
                if (finalStatus != WegstrMachineStatus.Ready) IsMachineReady = false;
                else IsSpindleOn = false;
            }
        }
        catch { IsMachineReady = false; }
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
}

public sealed record WegstrDirectSettings(int CalibrationX, int CalibrationY, int CalibrationZ, int SpeedPercent, double SafeZMillimetres);

public sealed class WegstrPreparedProgram(string fileName, WegstrCompiledProgram compiled, string gCode)
{
    public string FileName { get; } = fileName;
    public WegstrCompiledProgram Compiled { get; } = compiled;
    public string GCode { get; } = gCode;
    public string DisplayName => Compiled.Name;
    public int PacketCount => Compiled.PacketCount;
    public int ToolChanges => Math.Max(0, Compiled.Blocks.Count - 1);
    public string EstimatedDuration => TimeSpan.FromSeconds(Compiled.EstimatedDurationSeconds).ToString(@"hh\:mm\:ss");
    public string Summary => $"{PacketCount:N0} packets • {ToolChanges} tool change(s) • approx. {EstimatedDuration}";
}
