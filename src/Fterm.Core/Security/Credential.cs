namespace Fterm.Core.Security;

public enum CredentialKind
{
    Password,
    PrivateKey,
}

public sealed record Credential
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required CredentialKind Kind { get; init; }

    /// <summary>パスワード本体 or 秘密鍵 PEM 本体。</summary>
    public required string Secret { get; init; }

    /// <summary>秘密鍵のパスフレーズ（PrivateKey の場合のみ）。</summary>
    public string? Passphrase { get; init; }
}

public interface ICredentialStore
{
    Task<IReadOnlyList<CredentialSummary>> ListAsync(CancellationToken ct = default);
    Task<Credential?> GetAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Credential credential, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>UI 一覧で本体（Secret）を読み込まずに表示するためのメタ。</summary>
public sealed record CredentialSummary(Guid Id, string Name, CredentialKind Kind);
