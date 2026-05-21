using Fterm.Core.Transfer;
using Xunit;

namespace Fterm.Core.Tests;

public class TransferQueueTests
{
    private static TransferTask NewTask(long size, Func<IProgress<long>, CancellationToken, Task> op) => new()
    {
        DisplayName = "test",
        SourcePath = "src",
        DestinationPath = "dst",
        Direction = TransferDirection.Upload,
        TotalBytes = size,
        Operation = op,
    };

    [Fact]
    public async Task Completed_state_after_successful_operation()
    {
        using var q = new TransferQueue(maxParallel: 2);
        var t = NewTask(100, async (p, ct) =>
        {
            for (var i = 1; i <= 10; i++) { await Task.Delay(1, ct); p.Report(i * 10); }
        });
        q.Enqueue(t);
        await WaitFor(() => t.State == TransferState.Completed);
        Assert.Equal(100, t.BytesTransferred);
    }

    [Fact]
    public async Task Failed_state_when_operation_throws()
    {
        using var q = new TransferQueue(maxParallel: 1);
        var t = NewTask(100, (_, _) => throw new InvalidOperationException("boom"));
        q.Enqueue(t);
        await WaitFor(() => t.State == TransferState.Failed);
        Assert.Equal("boom", t.ErrorMessage);
    }

    [Fact]
    public async Task Cancelled_state_when_cancelled()
    {
        using var q = new TransferQueue(maxParallel: 1);
        var started = new TaskCompletionSource();
        var t = NewTask(100, async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        });
        q.Enqueue(t);
        await started.Task;
        q.Cancel(t.Id);
        await WaitFor(() => t.State == TransferState.Cancelled);
    }

    [Fact]
    public async Task Respects_max_parallel()
    {
        using var q = new TransferQueue(maxParallel: 2);
        var concurrent = 0;
        var peak = 0;
        var locker = new object();

        var tasks = Enumerable.Range(0, 5).Select(_ => NewTask(10, async (_, ct) =>
        {
            lock (locker) { concurrent++; peak = Math.Max(peak, concurrent); }
            await Task.Delay(80, ct);
            lock (locker) { concurrent--; }
        })).ToList();
        foreach (var t in tasks) q.Enqueue(t);

        await WaitFor(() => tasks.All(t => t.State == TransferState.Completed), timeout: TimeSpan.FromSeconds(5));
        Assert.True(peak <= 2, $"peak={peak}");
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan? timeout = null)
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < until)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        Assert.Fail("Condition not met within timeout");
    }
}
