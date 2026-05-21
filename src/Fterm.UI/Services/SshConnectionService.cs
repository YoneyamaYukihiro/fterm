using Fterm.Core.Connections;
using Fterm.Core.Files;
using Fterm.Core.Security;
using Fterm.Core.Sessions;
using Fterm.Protocols.Ftp;
using Fterm.Protocols.Serial;
using Fterm.Protocols.Ssh;
using Fterm.Protocols.Telnet;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Services;

/// <summary>
/// 接続定義からプロトコル別にチャネルを構築し、UI タブを返す。
/// 名前は歴史的経緯で Ssh となっているが SSH/SFTP/FTP/FTPS/Telnet/Serial を扱う。
/// </summary>
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
        var cred = await ResolveCredentialAsync(c, ct);
        ITerminalChannel channel = c.Protocol switch
        {
            ProtocolKind.Ssh => new SshTerminalChannel(new SshTerminalChannelOptions
            {
                Host = c.Host, Port = c.Port, Username = c.Username,
                Password = cred?.Kind == CredentialKind.Password ? cred.Secret : null,
                PrivateKeyPem = cred?.Kind == CredentialKind.PrivateKey ? cred.Secret : null,
                PrivateKeyPassphrase = cred?.Passphrase,
                TerminalName = c.TerminalType,
            }, _knownHosts, _hostKeyPolicy),
            ProtocolKind.Telnet => new TelnetTerminalChannel(new TelnetTerminalChannelOptions
            {
                Host = c.Host, Port = c.Port,
            }),
            ProtocolKind.Serial => new SerialTerminalChannel(new SerialTerminalChannelOptions
            {
                PortName = c.Host, BaudRate = c.Port > 0 ? c.Port : 115200,
            }),
            _ => throw new NotSupportedException($"Terminal cannot be opened for {c.Protocol}."),
        };
        var tab = new TerminalTabViewModel(c.Name, channel);
        await tab.StartAsync();
        return tab;
    }

    public async Task<FileTabViewModel?> OpenFileBrowserAsync(Connection c, CancellationToken ct)
    {
        var cred = await ResolveCredentialAsync(c, ct);
        IFileChannel remote = c.Protocol switch
        {
            ProtocolKind.Sftp or ProtocolKind.Ssh => new SftpFileChannel(new SftpFileChannelOptions
            {
                Host = c.Host, Port = c.Port, Username = c.Username,
                Password = cred?.Kind == CredentialKind.Password ? cred.Secret : null,
                PrivateKeyPem = cred?.Kind == CredentialKind.PrivateKey ? cred.Secret : null,
                PrivateKeyPassphrase = cred?.Passphrase,
            }, _knownHosts, _hostKeyPolicy),
            ProtocolKind.Ftp => new FtpFileChannel(new FtpFileChannelOptions
            {
                Host = c.Host, Port = c.Port, Username = c.Username,
                Password = cred?.Secret,
                Security = FtpSecurityMode.Plain,
            }),
            ProtocolKind.Ftps => new FtpFileChannel(new FtpFileChannelOptions
            {
                Host = c.Host, Port = c.Port, Username = c.Username,
                Password = cred?.Secret,
                Security = c.Port == 990 ? FtpSecurityMode.ImplicitTls : FtpSecurityMode.ExplicitTls,
            }),
            _ => throw new NotSupportedException($"File browser is not supported for {c.Protocol}."),
        };

        IFileChannel local = new LocalFileChannel();
        var localInitial = c.InitialLocalDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var remoteInitial = c.InitialRemoteDirectory ?? (c.Protocol is ProtocolKind.Ftp or ProtocolKind.Ftps ? "/" : ".");

        var tab = new FileTabViewModel($"{c.Name} ({c.Protocol})", local, localInitial, remote, remoteInitial);
        await tab.InitializeAsync();
        return tab;
    }

    private async Task<Credential?> ResolveCredentialAsync(Connection c, CancellationToken ct)
    {
        if (c.CredentialId is not { } id) return null;
        return await _credentials.GetAsync(id, ct);
    }
}
