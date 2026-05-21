using Fterm.Security;
using Xunit;

namespace Fterm.Security.Tests;

public class MasterKeyProviderTests
{
    [Fact]
    public void GetOrCreate_creates_32_byte_key_on_first_call()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-mk-{Guid.NewGuid():N}.bin");
        try
        {
            var key = MasterKeyProvider.GetOrCreate(path);
            Assert.Equal(32, key.Length);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GetOrCreate_returns_same_key_on_subsequent_calls()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-mk-{Guid.NewGuid():N}.bin");
        try
        {
            var first = MasterKeyProvider.GetOrCreate(path);
            var second = MasterKeyProvider.GetOrCreate(path);
            Assert.Equal(first, second);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
