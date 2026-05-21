namespace Fterm.Protocols.Ssh;

public enum HostKeyDecision { Accept, AcceptAndTrust, Reject }

public sealed record HostKeyPrompt(string Host, int Port, string Fingerprint, KnownHostResult Result);

/// <summary>
/// ホスト鍵が unknown / mismatch の場合に UI 側へ確認を委譲するための抽象。
/// CLI / バッチ用途では常に Reject を返す実装を、UI 用途ではダイアログ実装を差し込む。
/// </summary>
public interface IHostKeyPolicy
{
    Task<HostKeyDecision> DecideAsync(HostKeyPrompt prompt, CancellationToken ct);
}

public sealed class RejectUnknownHostKeyPolicy : IHostKeyPolicy
{
    public Task<HostKeyDecision> DecideAsync(HostKeyPrompt prompt, CancellationToken ct) =>
        Task.FromResult(HostKeyDecision.Reject);
}
