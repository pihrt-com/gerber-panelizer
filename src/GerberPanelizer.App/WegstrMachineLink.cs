using System.IO.Ports;
using System.IO;
using GerberPanelizer.Core.Machines.Wegstr;

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
        if (!IsOpen) throw new InvalidOperationException("Simulator is not connected.");
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
    private readonly SerialPort port = new(portName, 9600, Parity.None, 8, StopBits.One)
    {
        Handshake = Handshake.None,
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
        if (!port.IsOpen) throw new InvalidOperationException("Serial port is not connected.");
        if (packet.Length != WegstrPacketCodec.PacketLength) throw new ArgumentException("Invalid Wegstr packet length.", nameof(packet));
        TaskCompletionSource<WegstrControllerResponse> response;
        lock (sync)
        {
            if (pendingResponse is not null) throw new InvalidOperationException("Only one Wegstr packet may be in flight.");
            receiveBuffer.Clear();
            port.DiscardInBuffer();
            pendingResponse = response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            port.Write(packet, 0, packet.Length);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            return await response.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Wegstr did not acknowledge the packet within two seconds.");
        }
        finally
        {
            lock (sync) pendingResponse = null;
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs eventArgs)
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
                if (receiveBuffer.Count > 0)
                {
                    receiveBuffer.Clear();
                    port.DiscardInBuffer();
                }
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
