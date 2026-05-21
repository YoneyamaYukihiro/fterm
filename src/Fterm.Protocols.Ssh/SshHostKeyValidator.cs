using Renci.SshNet;

namespace Fterm.Protocols.Ssh;

/// <summary>
/// <see cref="BaseClient.HostKeyReceived"/> へぶら下げる検証ロジックを共通化する。
/// SSH ターミナルと SFTP の両方から再利用される。
/// </summary>
public static class SshHostKeyValidator
{
    public static void Attach(BaseClient client, string host, int port, KnownHostsStore knownHosts, IHostKeyPolicy policy, CancellationToken ct, Action<Exception> reportFailure)
    {
        client.HostKeyReceived += (_, e) =>
        {
            var fp = KnownHostsStore.Fingerprint(e.HostKey);
            var existing = knownHosts.Check(host, port, fp);
            if (existing == KnownHostResult.Trusted)
            {
                e.CanTrust = true;
                return;
            }

            HostKeyDecision decision;
            try
            {
                decision = policy.DecideAsync(new HostKeyPrompt(host, port, fp, existing), ct).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                reportFailure(ex);
                e.CanTrust = false;
                return;
            }

            if (decision == HostKeyDecision.AcceptAndTrust)
            {
                knownHosts.Trust(host, port, fp);
            }
            e.CanTrust = decision != HostKeyDecision.Reject;
        };
    }
}
