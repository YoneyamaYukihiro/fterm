using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Fterm.Core.Transfer;

/// <summary>
/// 単純な並列実行付き転送キュー。MaxParallel 個まで Operation を同時に走らせ、
/// 残りは Pending で待機させる。タスクは Cancel() でキャンセル可能。
/// </summary>
public sealed class TransferQueue : IDisposable
{
    private readonly Dictionary<Guid, CancellationTokenSource> _ctsByTask = [];
    private readonly SemaphoreSlim _slots;
    private readonly object _lock = new();

    public ObservableCollection<TransferTask> Tasks { get; } = [];
    public int MaxParallel { get; }

    public TransferQueue(int maxParallel = 4)
    {
        MaxParallel = maxParallel;
        _slots = new SemaphoreSlim(maxParallel, maxParallel);
    }

    public void Enqueue(TransferTask task)
    {
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _ctsByTask[task.Id] = cts;
            Tasks.Add(task);
        }
        _ = RunAsync(task, cts.Token);
    }

    public void Cancel(Guid taskId)
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            _ctsByTask.TryGetValue(taskId, out cts);
        }
        cts?.Cancel();
    }

    public void Clear()
    {
        lock (_lock)
        {
            for (var i = Tasks.Count - 1; i >= 0; i--)
            {
                var t = Tasks[i];
                if (t.State is TransferState.Completed or TransferState.Failed or TransferState.Cancelled)
                {
                    Tasks.RemoveAt(i);
                    _ctsByTask.Remove(t.Id);
                }
            }
        }
    }

    private async Task RunAsync(TransferTask task, CancellationToken ct)
    {
        await _slots.WaitAsync(ct);
        try
        {
            if (ct.IsCancellationRequested)
            {
                task.State = TransferState.Cancelled;
                return;
            }

            task.State = TransferState.Running;
            var progress = new Progress<long>(b => task.BytesTransferred = b);
            await task.Operation(progress, ct);
            task.State = TransferState.Completed;
        }
        catch (OperationCanceledException)
        {
            task.State = TransferState.Cancelled;
        }
        catch (Exception ex)
        {
            task.ErrorMessage = ex.Message;
            task.State = TransferState.Failed;
        }
        finally
        {
            _slots.Release();
            lock (_lock)
            {
                if (_ctsByTask.Remove(task.Id, out var cts)) cts.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var cts in _ctsByTask.Values) cts.Cancel();
            foreach (var cts in _ctsByTask.Values) cts.Dispose();
            _ctsByTask.Clear();
        }
        _slots.Dispose();
    }
}
