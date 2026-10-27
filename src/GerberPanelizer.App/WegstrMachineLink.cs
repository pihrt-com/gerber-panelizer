using System.IO.Ports;
using System.IO;
using GerberPanelizer.Core.Machines.Wegstr;

namespace GerberPanelizer.App;

internal interface IWegstrMachineLink : IAsyncDisposable
{
    string Name { get; }
    bool IsOpen { get; }
    Task OpenAsync(CancellationToken cancellationToken);
    Task<WegstrMachineStatus> ExchangeAsync(byte[] packet, CancellationToken cancellationToken);
}

internal sealed class SimulatedWegstrMachineLink : IWegstrMachineLink
{
    public string Name => LocalizationService.Instance["WegstrSimulatorName"];
    public bool IsOpen { get; private set; }
    public Task OpenAsync(CancellationToken cancellationToken) { IsOpen = true; return Task.CompletedTask; }
    public async Task<WegstrMachineStatus> ExchangeAsync(byte[] packet, CancellationToken cancellationToken)
    {
        if (!IsOpen) throw new InvalidOperationException("Simulator is not connected.");
        if (packet.Length != WegstrPacketCodec.PacketLength) throw new ArgumentException("Invalid Wegstr packet length.", nameof(packet));
        await Task.Delay(59, cancellationToken);
        return WegstrMachineStatus.Ready;
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
    private TaskCompletionSource<WegstrMachineStatus>? pendingResponse;

    public string Name => portName;
    public bool IsOpen => port.IsOpen;

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        port.DataReceived += OnDataReceived;
        port.Open();
        return Task.CompletedTask;
    }

    public async Task<WegstrMachineStatus> ExchangeAsync(byte[] packet, CancellationToken cancellationToken)
    {
        if (!port.IsOpen) throw new InvalidOperationException("Serial port is not connected.");
        if (packet.Length != WegstrPacketCodec.PacketLength) throw new ArgumentException("Invalid Wegstr packet length.", nameof(packet));
        TaskCompletionSource<WegstrMachineStatus> response;
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
            TaskCompletionSource<WegstrMachineStatus>? completedResponse = null;
            WegstrMachineStatus completedStatus = default;
            lock (sync)
            {
                while (port.IsOpen && port.BytesToRead > 0) receiveBuffer.Add((byte)port.ReadByte());
                if (pendingResponse is null || receiveBuffer.Count == 0) return;

                if (!WegstrResponseCodec.TryDecode(receiveBuffer, out var response, out var responseLength)) return;
                receiveBuffer.RemoveRange(0, responseLength);
                completedResponse = pendingResponse;
                completedStatus = response.Status;
                if (receiveBuffer.Count > 0)
                {
                    receiveBuffer.Clear();
                    port.DiscardInBuffer();
                }
            }
            completedResponse.TrySetResult(completedStatus);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            TaskCompletionSource<WegstrMachineStatus>? response;
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
