using Fterm.Core.Connections;
using Xunit;

namespace Fterm.Core.Tests;

public class JsonConnectionStoreTests
{
    [Fact]
    public async Task Save_and_load_roundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonConnectionStore(path);
            var c = Connection.NewSsh("test", "example.com", "alice");
            await store.SaveAsync(c);

            var loaded = await store.LoadAllAsync();
            Assert.Single(loaded);
            Assert.Equal("test", loaded[0].Name);
            Assert.Equal(ProtocolKind.Ssh, loaded[0].Protocol);
            Assert.Equal(22, loaded[0].Port);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Delete_removes_entry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonConnectionStore(path);
            var c = Connection.NewSsh("test", "example.com", "alice");
            await store.SaveAsync(c);
            await store.DeleteAsync(c.Id);
            Assert.Empty(await store.LoadAllAsync());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
