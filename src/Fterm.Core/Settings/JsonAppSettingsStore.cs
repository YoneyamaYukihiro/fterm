using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fterm.Core.Settings;

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonAppSettingsStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "fterm", "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<AppSettings>(s, Options, ct) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, settings, Options, ct);
        }
        finally
        {
            _lock.Release();
        }
    }
}
