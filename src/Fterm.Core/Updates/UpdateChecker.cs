using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace Fterm.Core.Updates;

/// <summary>
/// 配布側が公開する更新マニフェスト。リリースワークフローが生成して
/// `https://example.com/fterm/manifest.json` のような場所に置く前提。
/// </summary>
public sealed record UpdateManifest
{
    [JsonPropertyName("version")] public required string Version { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("assets")] public required Dictionary<string, UpdateAsset> Assets { get; init; }
}

public sealed record UpdateAsset
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("sha256")] public required string Sha256 { get; init; }
    [JsonPropertyName("size")] public long? Size { get; init; }
}

public sealed record UpdateInfo(Version NewVersion, Version CurrentVersion, UpdateAsset Asset, string? Notes);

public interface IUpdateChecker
{
    Task<UpdateInfo?> CheckAsync(string manifestUrl, string ridKey, CancellationToken ct);
    Task<bool> VerifyAsync(string filePath, string expectedSha256, CancellationToken ct);
}

public sealed class UpdateChecker : IUpdateChecker
{
    private readonly HttpClient _http;
    private readonly Version _currentVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public UpdateChecker(HttpClient http, Version? currentVersion = null)
    {
        _http = http;
        _currentVersion = currentVersion ?? ResolveCurrentVersion();
    }

    public static Version ResolveCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (info is not null)
        {
            // "0.1.0-dev" のような pre-release suffix を落とす
            var plus = info.IndexOf('+');
            if (plus >= 0) info = info[..plus];
            var dash = info.IndexOf('-');
            if (dash >= 0) info = info[..dash];
            if (Version.TryParse(info, out var v)) return v;
        }
        return asm.GetName().Version ?? new Version(0, 0, 0);
    }

    public async Task<UpdateInfo?> CheckAsync(string manifestUrl, string ridKey, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(manifestUrl, ct);
            resp.EnsureSuccessStatusCode();
            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(s, JsonOptions, ct);
            if (manifest is null) return null;

            if (!Version.TryParse(manifest.Version, out var newVersion))
            {
                Log.Warning("Update manifest had unparsable version: {Version}", manifest.Version);
                return null;
            }
            if (newVersion <= _currentVersion) return null;
            if (!manifest.Assets.TryGetValue(ridKey, out var asset))
            {
                Log.Warning("Update manifest has no asset for rid={Rid}", ridKey);
                return null;
            }
            return new UpdateInfo(newVersion, _currentVersion, asset, manifest.Notes);
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Update check failed (network or parse).");
            return null;
        }
    }

    public async Task<bool> VerifyAsync(string filePath, string expectedSha256, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(fs, ct);
        var actual = Convert.ToHexString(hash);
        return string.Equals(actual, expectedSha256.Replace("-", "").Replace(":", ""), StringComparison.OrdinalIgnoreCase);
    }
}
