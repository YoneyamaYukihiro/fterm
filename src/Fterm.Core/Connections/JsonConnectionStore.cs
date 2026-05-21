using System.Text.Json;

namespace Fterm.Core.Connections;

public sealed class JsonConnectionStore : IConnectionStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonConnectionStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "fterm", "connections.json");
    }

    public async Task<IReadOnlyList<Connection>> LoadAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            await using var stream = File.OpenRead(_path);
            var list = await JsonSerializer.DeserializeAsync<List<Connection>>(stream, Options, ct);
            return list ?? [];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(Connection connection, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = (await LoadAllInternalAsync(ct)).ToList();
            var idx = list.FindIndex(c => c.Id == connection.Id);
            if (idx >= 0)
            {
                list[idx] = connection;
            }
            else
            {
                list.Add(connection);
            }
            await WriteAsync(list, ct);
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
            var list = (await LoadAllInternalAsync(ct)).Where(c => c.Id != id).ToList();
            await WriteAsync(list, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<Connection>> LoadAllInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return [];
        }
        await using var stream = File.OpenRead(_path);
        var list = await JsonSerializer.DeserializeAsync<List<Connection>>(stream, Options, ct);
        return list ?? [];
    }

    private async Task WriteAsync(List<Connection> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, list, Options, ct);
    }
}
