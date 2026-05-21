namespace Fterm.Core.Sessions;

public interface ITerminalChannel : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct);
    Task ResizeAsync(int cols, int rows, CancellationToken ct);
    event EventHandler<DisconnectedEventArgs>? Disconnected;
}

public sealed class DisconnectedEventArgs(string? reason) : EventArgs
{
    public string? Reason { get; } = reason;
}
