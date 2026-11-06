using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using ClipboardApp.Core.Services;
using ClipboardApp.Core.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace ClipboardApp.Tests
{
    public class ApiVaultTests
    {
        [Fact]
        public void SecretsRoundTrip_AndMasterPasswordLocksTheVault()
        {
            string dir = Path.Combine(Path.GetTempPath(), "clipapp-vault-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string keyPath = Path.Combine(dir, "vault.key.json");
                var vault = new ApiVaultService(keyPath);
                var cipher = vault.EncryptSecret("sk-test-123456");
                Assert.Equal("sk-test-123456", vault.DecryptSecret(cipher));

                Assert.True(vault.SetMasterPassword("correct horse battery staple"));
                vault.Lock();
                Assert.True(vault.IsLocked);
                Assert.False(vault.Unlock("wrong password"));
                Assert.True(vault.Unlock("correct horse battery staple"));
                Assert.Equal("sk-test-123456", vault.DecryptSecret(cipher));

                Assert.True(vault.RemoveMasterPassword());
                Assert.False(vault.IsPasswordProtected);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public void ApiKeyRepository_EncryptsSecretAtRest()
        {
            string dir = Path.Combine(Path.GetTempPath(), "clipapp-apikeys-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string dbPath = Path.Combine(dir, "api-vault.db");
                string keyPath = Path.Combine(dir, "vault.key.json");
                var vault = new ApiVaultService(keyPath);
                using (var repo = new ApiKeyRepository(dbPath, vault))
                {
                    repo.Upsert(new ApiKey
                    {
                        Name = "OpenAI",
                        Service = "OpenAI",
                        Environment = "Production",
                        Secret = "sk-super-secret-value",
                        Tags = new List<string> { "ai", "prod" }
                    });

                    var loaded = repo.GetAll();
                    Assert.Single(loaded);
                    Assert.Equal("sk-super-secret-value", loaded[0].Secret);
                    Assert.Contains("ai", loaded[0].Tags);
                }

                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                byte[] bytes = File.ReadAllBytes(dbPath);
                string raw = Encoding.UTF8.GetString(bytes);
                Assert.DoesNotContain("sk-super-secret-value", raw);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public void BatchCopy_FormatsAreDeterministic()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Alias = "OPENAI_KEY", Content = "sk-1" },
                new() { Alias = "STRIPE_KEY", Content = "sk-2" }
            };

            Assert.Equal("sk-1\r\n\r\nsk-2", BatchCopyService.Build(entries, "Plain", "\r\n\r\n"));
            Assert.Contains("OPENAI_KEY=sk-1", BatchCopyService.Build(entries, "Env", "\r\n"));
            Assert.Contains("\"OPENAI_KEY\": \"sk-1\"", BatchCopyService.Build(entries, "Json", "\r\n"));
            Assert.StartsWith("name,value", BatchCopyService.Build(entries, "Csv", "\r\n"));
        }

        [Fact]
        public void ApiVault_BatchCopy_UsesOnePayload()
        {
            string payload = BatchCopyService.BuildApiKeys(
                new[] { ("OPENAI_KEY", "sk-1"), ("STRIPE_KEY", "sk-2") }, "Env");
            Assert.Equal("OPENAI_KEY=sk-1\nSTRIPE_KEY=sk-2", payload.Replace("\r\n", "\n"));
        }
    }
}
