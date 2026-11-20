using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using ClipboardApp.Core.Services;
using Xunit;

namespace ClipboardApp.Tests
{
    public class AuditRegressionTests
    {
        [Fact]
        public void PrivacyFilter_HonorsCanIncludeInHistoryZeroDwordMemoryStream()
        {
            var data = new DataObject();
            using var ms = new MemoryStream(BitConverter.GetBytes(0));
            data.SetData(ClipboardPrivacyFilter.CanIncludeInHistory, ms);

            Assert.True(ClipboardPrivacyFilter.IsExcludedByPrivacyFlags(data));
        }

        [Fact]
        public void PrivacyFilter_DetectsFormattedCreditCardAndSecondMatch()
        {
            string text = "Order 1234567890123456 paid with 4111-1111-1111-1111";
            Assert.True(ClipboardPrivacyFilter.ContainsSensitiveData(text, out var reason));
            Assert.Equal("Credit Card Number", reason);
        }

        [Fact]
        public void ApiVault_DoesNotOverwriteCorruptedKeyFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ClipboardAppTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string keyFile = Path.Combine(tempDir, "vault.key.json");
            try
            {
                File.WriteAllText(keyFile, "{corrupt json");
                using var vault = new ApiVaultService(keyFile);

                Assert.True(vault.IsCorrupted);
                Assert.True(vault.IsLocked);
                Assert.Equal("{corrupt json", File.ReadAllText(keyFile));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void PasteService_BuildCfHtml_DoesNotDoubleWrapExistingHeader()
        {
            string existing = "Version:0.9\r\nStartHTML:0000000097\r\nEndHTML:0000000130\r\nStartFragment:0000000110\r\nEndFragment:0000000120\r\n<html><body><b>hi</b></body></html>";
            string result = PasteService.BuildCfHtml(existing);
            Assert.Equal(existing, result);
        }

        [Fact]
        public void PasteService_HtmlAndRtfToPlainText_StripNoiseAndPreserveStructure()
        {
            string html = "<style>body{color:red}</style><p>Hello</p><p>World</p><script>alert(1)</script>";
            Assert.Equal("Hello\nWorld", PasteService.HtmlToPlainText(html));

            string rtf = @"{\rtf1{\fonttbl{\f0 Arial;}}\pard Hello\par World}";
            Assert.Equal("Hello\nWorld", PasteService.RtfToPlainText(rtf));
        }

        [Fact]
        public void SearchService_MultiTokenAndBareHashEdgeCase()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Id = "1", Content = "quarterly invoice.pdf", SourceApp = "chrome.exe", Tags = new List<string> { "finance" } },
                new() { Id = "2", Content = "notes", SourceApp = "notepad.exe", Tags = new List<string> { "personal" } },
            };

            var multi = SearchService.Filter(entries, "chrome invoice");
            Assert.Single(multi);
            Assert.Equal("1", multi[0].Id);

            var bareHash = SearchService.Filter(entries, "#");
            Assert.Empty(bareHash);
        }

        [Fact]
        public void TextTransformService_HandlesNullCamelAndAcronymSnakeCase()
        {
            Assert.Equal(string.Empty, TextTransformService.ToUpper(null!));
            Assert.Equal(string.Empty, TextTransformService.ToLower(null!));
            Assert.Equal("fooBar", TextTransformService.ToCamelCase("FOO_BAR"));
            Assert.Equal("html_parser", TextTransformService.ToSnakeCase("HTMLParser"));
        }
    }
}
