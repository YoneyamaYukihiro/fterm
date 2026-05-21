using System.Security.Cryptography;

namespace Fterm.Protocols.Ssh;

public enum KnownHostResult { Unknown, Trusted, Mismatch }

public sealed record KnownHost(string Host, int Port, string Fingerprint);

/// <summary>
/// シンプルなホスト鍵キャッシュ。フォーマット:
///   host port sha256-fingerprint
/// （OpenSSH 互換ではないが M3 では十分。後続で互換化予定）。
/// </summary>
public sealed class KnownHostsStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public KnownHostsStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "fterm", "known_hosts");
    }

    public static string Fingerprint(byte[] hostKey)
    {
        var hash = SHA256.HashData(hostKey);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    public KnownHostResult Check(string host, int port, string fingerprint)
    {
        lock (_lock)
        {
            foreach (var entry in Load())
            {
                if (!string.Equals(entry.Host, host, StringComparison.OrdinalIgnoreCase) || entry.Port != port) continue;
                return entry.Fingerprint == fingerprint ? KnownHostResult.Trusted : KnownHostResult.Mismatch;
            }
            return KnownHostResult.Unknown;
        }
    }

    public void Trust(string host, int port, string fingerprint)
    {
        lock (_lock)
        {
            var entries = Load()
                .Where(e => !(string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase) && e.Port == port))
                .Append(new KnownHost(host, port, fingerprint))
                .ToList();
            Save(entries);
        }
    }

    private List<KnownHost> Load()
    {
        if (!File.Exists(_path)) return [];
        var list = new List<KnownHost>();
        foreach (var line in File.ReadAllLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var parts = line.Split(' ', 3);
            if (parts.Length != 3 || !int.TryParse(parts[1], out var port)) continue;
            list.Add(new KnownHost(parts[0], port, parts[2]));
        }
        return list;
    }

    private void Save(List<KnownHost> entries)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllLines(_path, entries.Select(e => $"{e.Host} {e.Port} {e.Fingerprint}"));
    }
}
