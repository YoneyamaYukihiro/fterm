using System.Security.Cryptography;
using Fterm.Core.Security;
using Fterm.Security;
using Xunit;

namespace Fterm.Security.Tests;

public class EncryptedCredentialStoreTests
{
    private static (string path, byte[] key) NewStorage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-cred-{Guid.NewGuid():N}.json");
        var key = RandomNumberGenerator.GetBytes(32);
        return (path, key);
    }

    [Fact]
    public async Task Save_then_get_returns_secret()
    {
        var (path, key) = NewStorage();
        try
        {
            var store = new EncryptedCredentialStore(path, key);
            var c = new Credential
            {
                Id = Guid.NewGuid(),
                Name = "prod",
                Kind = CredentialKind.Password,
                Secret = "hunter2",
            };
            await store.SaveAsync(c);

            var got = await store.GetAsync(c.Id);
            Assert.NotNull(got);
            Assert.Equal("hunter2", got!.Secret);
            Assert.Equal(CredentialKind.Password, got.Kind);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task List_returns_metadata_only()
    {
        var (path, key) = NewStorage();
        try
        {
            var store = new EncryptedCredentialStore(path, key);
            await store.SaveAsync(new Credential
            {
                Id = Guid.NewGuid(), Name = "a", Kind = CredentialKind.Password, Secret = "x",
            });
            await store.SaveAsync(new Credential
            {
                Id = Guid.NewGuid(), Name = "b", Kind = CredentialKind.PrivateKey, Secret = "y", Passphrase = "p",
            });

            var list = await store.ListAsync();
            Assert.Equal(2, list.Count);
            Assert.Contains(list, s => s.Name == "a" && s.Kind == CredentialKind.Password);
            Assert.Contains(list, s => s.Name == "b" && s.Kind == CredentialKind.PrivateKey);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task File_does_not_contain_plaintext_secret()
    {
        var (path, key) = NewStorage();
        try
        {
            var store = new EncryptedCredentialStore(path, key);
            await store.SaveAsync(new Credential
            {
                Id = Guid.NewGuid(),
                Name = "prod",
                Kind = CredentialKind.Password,
                Secret = "Sup3rS3cret!XYZ",
            });

            var raw = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("Sup3rS3cret!XYZ", raw);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Delete_removes_credential()
    {
        var (path, key) = NewStorage();
        try
        {
            var store = new EncryptedCredentialStore(path, key);
            var c = new Credential
            {
                Id = Guid.NewGuid(), Name = "n", Kind = CredentialKind.Password, Secret = "s",
            };
            await store.SaveAsync(c);
            await store.DeleteAsync(c.Id);
            Assert.Null(await store.GetAsync(c.Id));
            Assert.Empty(await store.ListAsync());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Wrong_key_fails_to_decrypt()
    {
        var (path, key) = NewStorage();
        try
        {
            var store = new EncryptedCredentialStore(path, key);
            var c = new Credential
            {
                Id = Guid.NewGuid(), Name = "n", Kind = CredentialKind.Password, Secret = "s",
            };
            await store.SaveAsync(c);

            var wrongKey = RandomNumberGenerator.GetBytes(32);
            var other = new EncryptedCredentialStore(path, wrongKey);
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => other.GetAsync(c.Id));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
