using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Fterm.Security;

/// <summary>
/// アプリ固有のマスター鍵 (32 byte) を取得する。
/// 既定実装ではユーザプロファイル下の 0600 権限ファイルに保存する。
/// 後続マイルストーンで Windows: DPAPI / macOS: Keychain /
/// Linux: libsecret を優先する実装を追加予定。
/// </summary>
public static class MasterKeyProvider
{
    private const int KeySize = 32;

    public static byte[] GetOrCreate(string path)
    {
        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(path, key);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // 権限設定に失敗してもアプリは継続。
            }
        }

        return key;
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "fterm", "master.key");
    }
}
