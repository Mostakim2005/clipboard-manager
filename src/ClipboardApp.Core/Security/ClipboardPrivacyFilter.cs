using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ClipboardApp.Core.Interop;

namespace ClipboardApp.Core.Security
{
    public static class ClipboardPrivacyFilter
    {
        // Standard Windows Privacy & Password Manager Format Names
        public const string ExcludeFromMonitor = "ExcludeClipboardContentFromMonitorProcessing";
        public const string CanIncludeInHistory = "CanIncludeInClipboardHistory";
        public const string CanUploadToCloud = "CanUploadToCloudClipboard";
        public const string ClipboardViewerIgnore = "Clipboard Viewer Ignore";

        private static readonly uint FormatExcludeFromMonitor = NativeMethods.RegisterClipboardFormat(ExcludeFromMonitor);
        private static readonly uint FormatCanIncludeInHistory = NativeMethods.RegisterClipboardFormat(CanIncludeInHistory);
        private static readonly uint FormatCanUploadToCloud = NativeMethods.RegisterClipboardFormat(CanUploadToCloud);
        private static readonly uint FormatViewerIgnore = NativeMethods.RegisterClipboardFormat(ClipboardViewerIgnore);

        private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(250);

        // Regex patterns for sensitive content (supports optional spaces/hyphens in card numbers & Mastercard 2-series)
        private static readonly Regex s_creditCardRegex = new(
            @"(?<!\d)(?:4(?:[\s-]?\d){12}(?:(?:[\s-]?\d){3})?|(?:5[1-5]|222[1-9]|22[3-9]\d|2[3-6]\d{2}|27[01]\d|2720)(?:[\s-]?\d){12,14}|3[47](?:[\s-]?\d){13}|6(?:011|5\d{2})(?:[\s-]?\d){12})(?!\d)",
            RegexOptions.Compiled, s_regexTimeout);

        private static readonly Regex s_apiKeyRegex = new(
            @"(?:" +
            @"AKIA[0-9A-Z]{16}|" +                                                  // AWS Access Key ID
            @"gh[pousr]_[0-9a-zA-Z]{36}|github_pat_[0-9a-zA-Z_]{82}|" +             // GitHub tokens
            @"xox[baprs]-[0-9a-zA-Z\-]{10,64}|" +                                  // Slack token (with hyphens)
            @"sk-ant-[0-9a-zA-Z_\-]{32,}|" +                                       // Anthropic Claude API key
            @"sk-proj-[0-9a-zA-Z_\-]{32,}|" +                                      // OpenAI Project API key
            @"sk-[0-9a-zA-Z]{32,}|" +                                              // DeepSeek / OpenAI secret key
            @"AIzaSy[0-9a-zA-Z_\-]{33}|" +                                         // Google AI / Gemini API key
            @"(?:sk|rk|pk)_(?:live|test)_[0-9a-zA-Z]{24,}|" +                       // Stripe API keys
            @"hf_[0-9a-zA-Z]{34,}|" +                                              // HuggingFace token
            @"(?:api[_-]?key|secret[_-]?key|access[_-]?token)[\s:=]+['""]?[0-9a-zA-Z\-_]{16,}['""]?" + // Generic label=key
            @")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase, s_regexTimeout);

        private static readonly Regex s_privateKeyRegex = new(
            @"-----BEGIN (?:[A-Z ]+ PRIVATE KEY|PGP PRIVATE KEY BLOCK)-----",
            RegexOptions.Compiled, s_regexTimeout);

        /// <summary>
        /// Checks if the clipboard data has explicit flags requesting exclusion from history or monitor tools.
        /// </summary>
        public static bool IsExcludedByPrivacyFlags(IDataObject? dataObject)
        {
            if (dataObject == null) return false;

            // Direct format presence check via native format identifiers
            if (FormatExcludeFromMonitor != 0 && NativeMethods.IsClipboardFormatAvailable(FormatExcludeFromMonitor))
                return true;
            if (FormatViewerIgnore != 0 && NativeMethods.IsClipboardFormatAvailable(FormatViewerIgnore))
                return true;

            // Managed IDataObject checks
            try
            {
                if (dataObject.GetDataPresent(ExcludeFromMonitor)) return true;
                if (dataObject.GetDataPresent(ClipboardViewerIgnore)) return true;

                if (dataObject.GetDataPresent(CanIncludeInHistory))
                {
                    var val = dataObject.GetData(CanIncludeInHistory);
                    if (IsZeroDwordOrFalse(val)) return true;
                }
            }
            catch
            {
                // Access error reading IDataObject format data
            }

            return false;
        }

        private static bool IsZeroDwordOrFalse(object? val)
        {
            if (val is int intVal) return intVal == 0;
            if (val is uint uintVal) return uintVal == 0;
            if (val is bool boolVal) return !boolVal;
            if (val is byte[] bytes && bytes.Length >= 4)
            {
                return BitConverter.ToInt32(bytes, 0) == 0;
            }
            if (val is MemoryStream ms && ms.Length >= 4)
            {
                long pos = ms.Position;
                try
                {
                    ms.Position = 0;
                    Span<byte> buf = stackalloc byte[4];
                    if (ms.Read(buf) == 4)
                    {
                        return BitConverter.ToInt32(buf) == 0;
                    }
                }
                finally
                {
                    if (ms.CanSeek) ms.Position = pos;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the source process is in the ignored/excluded applications list.
        /// </summary>
        public static bool IsSourceAppExcluded(string? sourceAppName, IEnumerable<string>? ignoredApps)
        {
            if (string.IsNullOrWhiteSpace(sourceAppName) || ignoredApps == null) return false;

            var trimmedSource = sourceAppName.Trim();
            var cleanSource = trimmedSource.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmedSource.Substring(0, trimmedSource.Length - 4)
                : trimmedSource;

            foreach (var app in ignoredApps)
            {
                if (string.IsNullOrWhiteSpace(app)) continue;
                var cleanApp = app.Trim();
                if (cleanApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    cleanApp = cleanApp.Substring(0, cleanApp.Length - 4);

                if (string.Equals(cleanSource, cleanApp, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if the text contains sensitive patterns like credit card numbers or private/API keys.
        /// </summary>
        public static bool ContainsSensitiveData(string text, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            try
            {
                if (s_privateKeyRegex.IsMatch(text))
                {
                    reason = "Private Key";
                    return true;
                }

                if (s_apiKeyRegex.IsMatch(text))
                {
                    reason = "API Key / Token";
                    return true;
                }

                foreach (Match cardMatch in s_creditCardRegex.Matches(text))
                {
                    if (cardMatch.Success && PassesLuhn(cardMatch.Value))
                    {
                        reason = "Credit Card Number";
                        return true;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Guard against catastrophic backtracking on very large clipboard strings.
            }

            return false;
        }

        private static bool PassesLuhn(string number)
        {
            int sum = 0;
            bool alternate = false;
            for (int i = number.Length - 1; i >= 0; i--)
            {
                char c = number[i];
                if (!char.IsDigit(c)) continue;
                int n = c - '0';
                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n = (n % 10) + 1;
                }
                sum += n;
                alternate = !alternate;
            }
            return (sum % 10 == 0);
        }
    }
}
