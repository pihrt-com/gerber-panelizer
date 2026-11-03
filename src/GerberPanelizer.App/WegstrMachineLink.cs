using System.IO;
using GerberPanelizer.Core.Machines.Wegstr;
using RJCP.IO.Ports;

namespace GerberPanelizer.App;

internal interface IWegstrMachineLink : IAsyncDisposable
{
    string Name { get; }
    bool IsOpen { get; }
    Task OpenAsync(CancellationToken cancellationToken);
    Task<WegstrControllerResponse> ExchangeAsync(byte[] packet, bool probeArmed, CancellationToken cancellationToken);
}

internal sealed class SimulatedWegstrMachineLink : IWegstrMachineLink
{
    private readonly int calibrationX;
    private readonly int calibrationY;
    private readonly int calibrationZ;
    private int xSteps;
    private int ySteps;
    private int zSteps;

    public SimulatedWegstrMachineLink(int calibrationX, int calibrationY, int calibrationZ)
    {
        this.calibrationX = calibrationX;
        this.calibrationY = calibrationY;
        this.calibrationZ = calibrationZ;
    }

    public string Name => LocalizationService.Instance["WegstrSimulatorName"];
    public bool IsOpen { get; private set; }
    public Task OpenAsync(CancellationToken cancellationToken) { IsOpen = true; return Task.CompletedTask; }
    public async Task<WegstrControllerResponse> ExchangeAsync(byte[] packet, bool probeArmed, CancellationToken cancellationToken)
    {
        if (!IsOpen) throw new InvalidOperationException(LocalizationService.Instance["WegstrErrorSimulatorNotConnected"]);
        if (packet.Length != WegstrPacketCodec.PacketLength) throw new ArgumentException("Invalid Wegstr packet length.", nameof(packet));
        await Task.Delay(59, cancellationToken);
        foreach (var step in WegstrPacketCodec.Decode(packet))
        {
            xSteps += step.X;
            ySteps += step.Y;
            var nextZ = zSteps + step.Z;
            if (probeArmed && step.Z < 0)
            {
                var surfaceZ = SimulatedSurfaceZSteps(xSteps, ySteps);
                if (nextZ <= surfaceZ)
                {
                    zSteps = surfaceZ;
                    return new WegstrControllerResponse(WegstrMachineStatus.Probe, checked((short)surfaceZ));
                }
            }
            zSteps = nextZ;
        }
        return new WegstrControllerResponse(WegstrMachineStatus.Ready, null);
    }

    private int SimulatedSurfaceZSteps(int x, int y)
    {
        var xMillimetres = x * 10d / calibrationX;
        var yMillimetres = y * 10d / calibrationY;
        var surfaceMillimetres = .025 * xMillimetres - .018 * yMillimetres + .04 * Math.Sin(xMillimetres / 12d) * Math.Cos(yMillimetres / 10d);
        return (int)Math.Round(surfaceMillimetres * calibrationZ / 10d);
    }
    public ValueTask DisposeAsync() { IsOpen = false; return ValueTask.CompletedTask; }
}

internal sealed class SerialWegstrMachineLink(string portName) : IWegstrMachineLink
{
    private readonly WinSerialPortStream port = new(portName, 9600, 8, Parity.None, StopBits.One)
    {
        DtrEnable = false,
        RtsEnable = false,
        ReadTimeout = 2000,
        WriteTimeout = 2000,
    };
    private readonly object sync = new();
    private readonly List<byte> receiveBuffer = [];
    private TaskCompletionSource<WegstrControllerResponse>? pendingResponse;

    public string Name => portName;
    public bool IsOpen => port.IsOpen;

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        port.DataReceived += OnDataReceived;
        port.Open();
        return Task.CompletedTask;
    }

    public async Task<WegstrControllerResponse> ExchangeAsync(byte[] packet, bool probeArmed, CancellationToken cancellationToken)
    {
        if (!port.IsOpen) throw new InvalidOperationException(LocalizationService.Instance["WegstrErrorSerialPortNotConnected"]);
        if (packet.Length != WegstrPacketCodec.PacketLength) throw new ArgumentException("Invalid Wegstr packet length.", nameof(packet));
        TaskCompletionSource<WegstrControllerResponse> response;
        lock (sync)
        {
            if (pendingResponse is not null) throw new InvalidOperationException(LocalizationService.Instance["WegstrErrorPacketAlreadyPending"]);
            pendingResponse = response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            if (probeArmed) AppLog.Information($"Wegstr probe TX: {Convert.ToHexString(packet)}.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            // System.IO.Ports blocks in the USB driver on COM6 immediately after probing.
            // SerialPortStream buffers this short frame on its own I/O worker, then uses
            // Win32 overlapped I/O to deliver it without blocking the control sequence.
            await port.WriteAsync(packet.AsMemory(), timeout.Token).ConfigureAwait(false);
            var controllerResponse = await response.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            if (probeArmed) AppLog.Information($"Wegstr probe RX: status={(byte)controllerResponse.Status}; touchZ={(controllerResponse.ProbePositionSteps?.ToString() ?? "none")}.");
            return controllerResponse;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(LocalizationService.Instance["WegstrErrorAcknowledgementTimeout"]);
        }
        finally
        {
            lock (sync) pendingResponse = null;
        }
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs eventArgs)
    {
        try
        {
            TaskCompletionSource<WegstrControllerResponse>? completedResponse = null;
            WegstrControllerResponse completedResponseValue = default;
            lock (sync)
            {
                while (port.IsOpen && port.BytesToRead > 0) receiveBuffer.Add((byte)port.ReadByte());
                if (pendingResponse is null || receiveBuffer.Count == 0) return;
                if (!WegstrResponseCodec.TryDecode(receiveBuffer, out var response, out var responseLength)) return;
                receiveBuffer.RemoveRange(0, responseLength);
                completedResponse = pendingResponse;
                completedResponseValue = response;
            }
            completedResponse.TrySetResult(completedResponseValue);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            TaskCompletionSource<WegstrControllerResponse>? response;
            lock (sync) response = pendingResponse;
            response?.TrySetException(exception);
        }
    }

    public ValueTask DisposeAsync()
    {
        port.DataReceived -= OnDataReceived;
        if (port.IsOpen) port.Close();
        port.Dispose();
        return ValueTask.CompletedTask;
    }
}
