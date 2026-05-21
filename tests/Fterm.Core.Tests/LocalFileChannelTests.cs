using Fterm.Core.Files;
using Xunit;

namespace Fterm.Core.Tests;

public class LocalFileChannelTests
{
    [Fact]
    public async Task Lists_directory()
    {
        var dir = Directory.CreateTempSubdirectory("fterm-local-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "a.txt"), "hello");
            Directory.CreateDirectory(Path.Combine(dir.FullName, "sub"));

            var ch = new LocalFileChannel();
            await ch.ConnectAsync(CancellationToken.None);
            var entries = new List<string>();
            await foreach (var e in ch.ListAsync(dir.FullName, CancellationToken.None))
            {
                entries.Add($"{(e.IsDirectory ? "d" : "f")}:{e.Name}");
            }
            Assert.Contains("f:a.txt", entries);
            Assert.Contains("d:sub", entries);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Read_write_roundtrip()
    {
        var dir = Directory.CreateTempSubdirectory("fterm-local-");
        try
        {
            var path = Path.Combine(dir.FullName, "x.bin");
            var ch = new LocalFileChannel();

            await using (var w = await ch.OpenWriteAsync(path, CancellationToken.None))
            {
                await w.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });
            }

            await using var r = await ch.OpenReadAsync(path, CancellationToken.None);
            using var ms = new MemoryStream();
            await r.CopyToAsync(ms);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, ms.ToArray());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
