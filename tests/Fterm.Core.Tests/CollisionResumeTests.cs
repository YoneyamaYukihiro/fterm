using Fterm.Core.Files;
using Xunit;

namespace Fterm.Core.Tests;

public class CollisionResumeTests
{
    [Fact]
    public async Task ExistsAsync_returns_true_for_file()
    {
        var dir = Directory.CreateTempSubdirectory("fterm-co-");
        try
        {
            var path = Path.Combine(dir.FullName, "a.txt");
            File.WriteAllText(path, "x");
            var ch = new LocalFileChannel();
            Assert.True(await ch.ExistsAsync(path, CancellationToken.None));
            Assert.False(await ch.ExistsAsync(Path.Combine(dir.FullName, "missing"), CancellationToken.None));
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public async Task GetSizeAsync_returns_file_size()
    {
        var dir = Directory.CreateTempSubdirectory("fterm-co-");
        try
        {
            var path = Path.Combine(dir.FullName, "a.bin");
            File.WriteAllBytes(path, new byte[1024]);
            var ch = new LocalFileChannel();
            Assert.Equal(1024, await ch.GetSizeAsync(path, CancellationToken.None));
            Assert.Equal(-1, await ch.GetSizeAsync(dir.FullName, CancellationToken.None));
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public async Task OpenAppendAsync_appends_to_existing_file()
    {
        var dir = Directory.CreateTempSubdirectory("fterm-co-");
        try
        {
            var path = Path.Combine(dir.FullName, "a.txt");
            File.WriteAllText(path, "hello");
            var ch = new LocalFileChannel();
            await using (var s = await ch.OpenAppendAsync(path, CancellationToken.None))
            {
                await s.WriteAsync(System.Text.Encoding.UTF8.GetBytes(" world"));
            }
            Assert.Equal("hello world", File.ReadAllText(path));
        }
        finally { dir.Delete(recursive: true); }
    }
}
