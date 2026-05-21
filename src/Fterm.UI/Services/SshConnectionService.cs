using Fterm.Core.Connections;
using Fterm.Core.Security;
using Fterm.Protocols.Ssh;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Services;

public sealed class SshConnectionService : IConnectionService
{
    private readonly ICredentialStore _credentials;
    private readonly KnownHostsStore _knownHosts;
    private readonly IHostKeyPolicy _hostKeyPolicy;

    public SshConnectionService(ICredentialStore credentials, KnownHostsStore knownHosts, IHostKeyPolicy hostKeyPolicy)
    {
        _credentials = credentials;
        _knownHosts = knownHosts;
        _hostKeyPolicy = hostKeyPolicy;
    }

    public async Task<TerminalTabViewModel?> OpenTerminalAsync(Connection c, CancellationToken ct)
    {
        if (c.Protocol != ProtocolKind.Ssh)
        {
            throw new NotSupportedException($"Protocol {c.Protocol} is not yet supported in M3.");
        }

        Credential? cred = null;
        if (c.CredentialId is { } id)
        {
            cred = await _credentials.GetAsync(id, ct);
        }

        var options = new SshTerminalChannelOptions
        {
            Host = c.Host,
            Port = c.Port,
            Username = c.Username,
            Password = cred?.Kind == CredentialKind.Password ? cred.Secret : null,
            PrivateKeyPem = cred?.Kind == CredentialKind.PrivateKey ? cred.Secret : null,
            PrivateKeyPassphrase = cred?.Passphrase,
            TerminalName = c.TerminalType,
        };
        var channel = new SshTerminalChannel(options, _knownHosts, _hostKeyPolicy);
        var tab = new TerminalTabViewModel(c.Name, channel, options.InitialCols, options.InitialRows);
        await tab.StartAsync();
        return tab;
    }
}
