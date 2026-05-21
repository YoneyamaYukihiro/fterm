using Fterm.Core.Connections;
using Fterm.Core.Files;
using Fterm.Core.Security;
using Fterm.Core.Sessions;
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
        if (c.Protocol is not (ProtocolKind.Ssh or ProtocolKind.Telnet))
        {
            throw new NotSupportedException($"Terminal cannot be opened for {c.Protocol}.");
        }

        var cred = await ResolveCredentialAsync(c, ct);
        var options = new SshTerminalChannelOptions
        {
            Host = c.Host, Port = c.Port, Username = c.Username,
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

    public async Task<FileTabViewModel?> OpenFileBrowserAsync(Connection c, CancellationToken ct)
    {
        if (c.Protocol is not (ProtocolKind.Sftp or ProtocolKind.Ssh))
        {
            throw new NotSupportedException($"File browser is not yet supported for {c.Protocol}.");
        }

        var cred = await ResolveCredentialAsync(c, ct);
        IFileChannel remote = new SftpFileChannel(new SftpFileChannelOptions
        {
            Host = c.Host, Port = c.Port, Username = c.Username,
            Password = cred?.Kind == CredentialKind.Password ? cred.Secret : null,
            PrivateKeyPem = cred?.Kind == CredentialKind.PrivateKey ? cred.Secret : null,
            PrivateKeyPassphrase = cred?.Passphrase,
        }, _knownHosts, _hostKeyPolicy);

        IFileChannel local = new LocalFileChannel();

        var localInitial = c.InitialLocalDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var remoteInitial = c.InitialRemoteDirectory ?? ".";

        var tab = new FileTabViewModel($"{c.Name} (SFTP)", local, localInitial, remote, remoteInitial);
        await tab.InitializeAsync();
        return tab;
    }

    private async Task<Credential?> ResolveCredentialAsync(Connection c, CancellationToken ct)
    {
        if (c.CredentialId is not { } id) return null;
        return await _credentials.GetAsync(id, ct);
    }
}
