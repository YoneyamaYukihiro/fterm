namespace Fterm.Core.Connections;

public enum ProtocolKind
{
    Ssh,
    Telnet,
    Sftp,
    Ftp,
    Ftps,
    Serial,
}

public enum AuthMethod
{
    Password,
    PublicKey,
    KeyboardInteractive,
    Agent,
}

public sealed record Connection
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ProtocolKind Protocol { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Username { get; init; } = "";
    public AuthMethod AuthMethod { get; init; } = AuthMethod.Password;
    public Guid? CredentialId { get; init; }
    public string? InitialRemoteDirectory { get; init; }
    public string? InitialLocalDirectory { get; init; }
    public string Encoding { get; init; } = "utf-8";
    public string TerminalType { get; init; } = "xterm-256color";
    public IReadOnlyList<string> Tags { get; init; } = [];
    /// <summary>接続直後に走らせるマクロスクリプト（DSL は Fterm.Core.Macros.Macro 参照）。</summary>
    public string OnConnectMacro { get; init; } = "";

    public static Connection NewSsh(string name, string host, string username) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Protocol = ProtocolKind.Ssh,
        Host = host,
        Port = 22,
        Username = username,
    };
}
