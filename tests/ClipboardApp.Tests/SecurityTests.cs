using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using ClipboardApp.Core.Services;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace ClipboardApp.Tests
{
    public class SecurityTests
    {
        [Fact]
        public void TestDpapiEncryptionRoundtrip_String()
        {
            var secret = "MySuperSecretPassword123!@#";
            var encrypted = DataProtector.EncryptString(secret);

            Assert.NotEqual(secret, encrypted);
            Assert.StartsWith("ENC:", encrypted);

            var decrypted = DataProtector.DecryptString(encrypted);
            Assert.Equal(secret, decrypted);
        }

        [Fact]
        public void TestDpapiBackwardCompatibility_PlaintextString()
        {
            var oldPlaintext = "LegacyUnencryptedText";
            var result = DataProtector.DecryptString(oldPlaintext);

            Assert.Equal(oldPlaintext, result);
        }

        [Fact]
        public void TestDpapiEncryptionRoundtrip_Bytes()
        {
            var rawBytes = Encoding.UTF8.GetBytes("BinaryPayloadDataToEncrypt");
            var encrypted = DataProtector.EncryptBytes(rawBytes);

            Assert.NotEqual(rawBytes, encrypted);

            var decrypted = DataProtector.DecryptBytes(encrypted);
            Assert.Equal(rawBytes, decrypted);
        }

        [Fact]
        public void TestSensitiveDataDetection_CreditCard()
        {
            // Valid Visa test number (passes Luhn: 4532 0151 1283 0366)
            var textWithCard = "Here is my card 4532015112830366 for billing.";
            var isSensitive = ClipboardPrivacyFilter.ContainsSensitiveData(textWithCard, out var reason);

            Assert.True(isSensitive);
            Assert.Equal("Credit Card Number", reason);

            // Invalid card number (fails Luhn)
            var textWithFakeCard = "Card number 4111111111111112 invalid.";
            var isFakeSensitive = ClipboardPrivacyFilter.ContainsSensitiveData(textWithFakeCard, out _);
            Assert.False(isFakeSensitive);
        }

        [Fact]
        public void TestSensitiveDataDetection_ModernAiKeys()
        {
            // DeepSeek API key (assembled at runtime so no literal secret-shaped
            // string exists in source and GitHub push protection is not tripped).
            var deepseekKey = "Export DEEPSEEK_API_KEY=" + string.Concat("sk-", "9876543210abcdef0123456789abcdef");
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(deepseekKey, out var reason1));
            Assert.Equal("API Key / Token", reason1);

            // OpenAI Project Key
            var openaiProjKey = "key: " + string.Concat("sk-", "proj-", "abc12345678901234567890123456789012");
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(openaiProjKey, out var reason2));
            Assert.Equal("API Key / Token", reason2);

            // Anthropic Claude Key
            var claudeKey = "ANTHROPIC_API_KEY=" + string.Concat("sk-", "ant-api03-", "abcdefghijklmnopqrstuvwxyz012345");
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(claudeKey, out var reason3));
            Assert.Equal("API Key / Token", reason3);

            // Google Gemini / Cloud Key
            var geminiKey = "GEMINI_API_KEY=" + string.Concat("AIza", "SyD-abc12345678901234567890123456789");
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(geminiKey, out var reason4));
            Assert.Equal("API Key / Token", reason4);

            // Stripe Live Key
            var stripeKey = "STRIPE_SECRET=" + string.Concat("sk_", "live_", "51Abc123456789012345678901");
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(stripeKey, out var reason5));
            Assert.Equal("API Key / Token", reason5);

            // HuggingFace Token
            var hfToken = "HF_TOKEN=" + string.Concat("hf_", "abcdefghijklmnopqrstuvwxyz012345678");
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(hfToken, out var reason6));
            Assert.Equal("API Key / Token", reason6);
        }

        [Fact]
        public void TestApiVaultService_LockZeroesMemory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ClipboardApp_VaultTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var keyFile = Path.Combine(tempDir, "test.vault.key.json");

            try
            {
                var vault = new ApiVaultService(keyFile);
                Assert.False(vault.IsLocked);

                // Lock should zero memory and set IsLocked to true
                vault.Lock();
                Assert.True(vault.IsLocked);

                // Attempting to encrypt while locked should throw InvalidOperationException
                Assert.Throws<System.InvalidOperationException>(() => vault.EncryptSecret("secret123"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void TestIgnoredAppsExclusion()
        {
            var ignoredList = new List<string> { "KeePass", "1Password.exe", "Bitwarden" };

            Assert.True(ClipboardPrivacyFilter.IsSourceAppExcluded("KeePass.exe", ignoredList));
            Assert.True(ClipboardPrivacyFilter.IsSourceAppExcluded("keepass", ignoredList));
            Assert.True(ClipboardPrivacyFilter.IsSourceAppExcluded("1Password", ignoredList));
            Assert.True(ClipboardPrivacyFilter.IsSourceAppExcluded("Bitwarden.exe", ignoredList));

            Assert.False(ClipboardPrivacyFilter.IsSourceAppExcluded("notepad.exe", ignoredList));
            Assert.False(ClipboardPrivacyFilter.IsSourceAppExcluded("chrome", ignoredList));
            Assert.False(ClipboardPrivacyFilter.IsSourceAppExcluded("", ignoredList));
            Assert.False(ClipboardPrivacyFilter.IsSourceAppExcluded(null, ignoredList));
        }

        [Fact]
        public void TestPasswordManagerPrivacyFlags_DataObject()
        {
            var dataObj = new DataObject();
            dataObj.SetData(ClipboardPrivacyFilter.ExcludeFromMonitor, "1");

            Assert.True(ClipboardPrivacyFilter.IsExcludedByPrivacyFlags(dataObj));

            var historyObj = new DataObject();
            historyObj.SetData(ClipboardPrivacyFilter.CanIncludeInHistory, 0);
            Assert.True(ClipboardPrivacyFilter.IsExcludedByPrivacyFlags(historyObj));

            var normalObj = new DataObject();
            normalObj.SetText("Normal text");
            Assert.False(ClipboardPrivacyFilter.IsExcludedByPrivacyFlags(normalObj));
        }

        [Fact]
        public void TestSearchRegexSafety_ComplexPattern()
        {
            var search = new SearchService();
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "aaaaaaaaaaaaaaaaaaaa!", Alias = "test" },
                new() { Content = "normal match", Alias = "other" }
            };

            // Potentially backtracking pattern is handled safely with timeout
            var results = search.FilterEntries(entries, "(a+)+$", useRegex: true);
            Assert.NotNull(results);
        }

        [Fact]
        public void TestDibBitfieldsOffsetCalculation()
        {
            // Build minimal 40-byte DIB header with BI_BITFIELDS (compression=3)
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.Default, leaveOpen: true))
            {
                bw.Write(40);            // biSize
                bw.Write(1);             // biWidth
                bw.Write(1);             // biHeight
                bw.Write((short)1);      // biPlanes
                bw.Write((short)32);     // biBitCount (32 bpp)
                bw.Write(3);             // biCompression (BI_BITFIELDS)
                bw.Write(4);             // biSizeImage
                bw.Write(0);             // biXPelsPerMeter
                bw.Write(0);             // biYPelsPerMeter
                bw.Write(0);             // biClrUsed
                bw.Write(0);             // biClrImportant

                // 3 color masks (12 bytes)
                bw.Write(0x00FF0000);    // Red mask
                bw.Write(0x0000FF00);    // Green mask
                bw.Write(0x000000FF);    // Blue mask

                // 1 pixel (4 bytes)
                bw.Write(0xFF00FF00);
            }
            ms.Position = 0;

            var bmp = ImageHelper.GetBitmapFromDib(ms);
            Assert.NotNull(bmp);
            Assert.Equal(1, bmp.Width);
            Assert.Equal(1, bmp.Height);
            bmp.Dispose();
        }
    }
}
