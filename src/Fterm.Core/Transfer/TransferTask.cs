using CommunityToolkit.Mvvm.ComponentModel;

namespace Fterm.Core.Transfer;

public enum TransferDirection { Upload, Download }

public enum TransferState { Pending, Running, Completed, Failed, Cancelled }

public sealed partial class TransferTask : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string DisplayName { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required TransferDirection Direction { get; init; }
    public required long TotalBytes { get; init; }

    /// <summary>
    /// 実際の転送ロジック。Progress に転送済みバイト数を絶対値で報告する。
    /// </summary>
    public required Func<IProgress<long>, CancellationToken, Task> Operation { get; init; }

    [ObservableProperty]
    private long _bytesTransferred;

    [ObservableProperty]
    private TransferState _state = TransferState.Pending;

    [ObservableProperty]
    private string? _errorMessage;

    public double ProgressFraction =>
        TotalBytes <= 0 ? 0 : Math.Min(1.0, (double)BytesTransferred / TotalBytes);
}
