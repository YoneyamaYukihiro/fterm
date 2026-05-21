using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fterm.Core.Security;

namespace Fterm.Security;

/// <summary>
/// AES-GCM でファイル暗号化する CredentialStore 実装。
/// マスター鍵は <see cref="MasterKeyProvider"/> から取得する。
/// ファイル形式:
///   { "items": [ { "id", "name", "kind", "nonce" (base64), "ciphertext" (base64), "tag" (base64) } ] }
/// </summary>
public sealed class EncryptedCredentialStore : ICredentialStore
{
    private readonly string _path;
    private readonly byte[] _masterKey;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public EncryptedCredentialStore(string path, byte[] masterKey)
    {
        if (masterKey.Length != 32) throw new ArgumentException("Master key must be 32 bytes", nameof(masterKey));
        _path = path;
        _masterKey = masterKey;
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "fterm", "credentials.json");
    }

    public async Task<IReadOnlyList<CredentialSummary>> ListAsync(CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct);
        return file.Items
            .Select(e => new CredentialSummary(e.Id, e.Name, e.Kind))
            .ToList();
    }

    public async Task<Credential?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct);
        var entry = file.Items.FirstOrDefault(e => e.Id == id);
        if (entry is null) return null;
        return Decrypt(entry);
    }

    public async Task SaveAsync(Credential credential, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var file = await LoadFileInternalAsync(ct);
            var encrypted = Encrypt(credential);
            var idx = file.Items.FindIndex(e => e.Id == credential.Id);
            if (idx >= 0) file.Items[idx] = encrypted;
            else file.Items.Add(encrypted);
            await WriteAsync(file, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var file = await LoadFileInternalAsync(ct);
            file.Items.RemoveAll(e => e.Id == id);
            await WriteAsync(file, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<EncryptedFile> LoadFileAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await LoadFileInternalAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<EncryptedFile> LoadFileInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return new EncryptedFile { Items = [] };
        }
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<EncryptedFile>(stream, JsonOptions, ct)
            ?? new EncryptedFile { Items = [] };
    }

    private async Task WriteAsync(EncryptedFile file, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, file, JsonOptions, ct);
    }

    private EncryptedEntry Encrypt(Credential c)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new SecretPayload(c.Secret, c.Passphrase));
        var ciphertext = new byte[payload.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_masterKey, tagSizeInBytes: 16);
        aes.Encrypt(nonce, payload, ciphertext, tag);

        return new EncryptedEntry
        {
            Id = c.Id,
            Name = c.Name,
            Kind = c.Kind,
            Nonce = Convert.ToBase64String(nonce),
            Ciphertext = Convert.ToBase64String(ciphertext),
            Tag = Convert.ToBase64String(tag),
        };
    }

    private Credential Decrypt(EncryptedEntry e)
    {
        var nonce = Convert.FromBase64String(e.Nonce);
        var ciphertext = Convert.FromBase64String(e.Ciphertext);
        var tag = Convert.FromBase64String(e.Tag);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_masterKey, tagSizeInBytes: 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        var payload = JsonSerializer.Deserialize<SecretPayload>(plaintext, JsonOptions)
            ?? throw new CryptographicException("Decryption produced empty payload");
        return new Credential
        {
            Id = e.Id,
            Name = e.Name,
            Kind = e.Kind,
            Secret = payload.Secret,
            Passphrase = payload.Passphrase,
        };
    }

    private sealed class EncryptedFile
    {
        public List<EncryptedEntry> Items { get; set; } = [];
    }

    private sealed class EncryptedEntry
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public CredentialKind Kind { get; set; }
        public string Nonce { get; set; } = "";
        public string Ciphertext { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    private sealed record SecretPayload(string Secret, string? Passphrase);
}
