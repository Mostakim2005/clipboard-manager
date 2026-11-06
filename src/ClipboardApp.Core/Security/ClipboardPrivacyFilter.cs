using System;
using System.Collections.Generic;
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

        // Regex patterns for sensitive content
        private static readonly Regex s_creditCardRegex = new(
            @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12})\b",
            RegexOptions.Compiled);

        private static readonly Regex s_apiKeyRegex = new(
            @"(?:" +
            @"AKIA[0-9A-Z]{16}|" +                                                  // AWS Access Key ID
            @"ghp_[0-9a-zA-Z]{36}|github_pat_[0-9a-zA-Z_]{82}|" +                   // GitHub Personal Access Token
            @"xox[baprs]-[0-9a-zA-Z]{10,48}|" +                                    // Slack token
            @"sk-ant-[0-9a-zA-Z_\-]{32,}|" +                                       // Anthropic Claude API key
            @"sk-proj-[0-9a-zA-Z_\-]{32,}|" +                                      // OpenAI Project API key
            @"sk-[0-9a-zA-Z]{32,}|" +                                              // DeepSeek / OpenAI secret key
            @"AIzaSy[0-9a-zA-Z_\-]{33}|" +                                         // Google AI / Gemini API key
            @"(?:sk|rk|pk)_(?:live|test)_[0-9a-zA-Z]{24,}|" +                       // Stripe API keys
            @"hf_[0-9a-zA-Z]{34,}|" +                                              // HuggingFace token
            @"(?:api[_-]?key|secret[_-]?key|access[_-]?token)[\s:=]+['""]?[0-9a-zA-Z\-_]{16,}['""]?" + // Generic label=key
            @")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex s_privateKeyRegex = new(
            @"-----BEGIN [A-Z ]+ PRIVATE KEY-----",
            RegexOptions.Compiled);

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
                    if (val is int intVal && intVal == 0) return true;
                    if (val is bool boolVal && !boolVal) return true;
                }
            }
            catch
            {
                // Access error reading IDataObject format data
            }

            return false;
        }

        /// <summary>
        /// Checks if the source process is in the ignored/excluded applications list.
        /// </summary>
        public static bool IsSourceAppExcluded(string? sourceAppName, IEnumerable<string>? ignoredApps)
        {
            if (string.IsNullOrWhiteSpace(sourceAppName) || ignoredApps == null) return false;

            var cleanSource = sourceAppName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? sourceAppName.Substring(0, sourceAppName.Length - 4)
                : sourceAppName;

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

            var cardMatch = s_creditCardRegex.Match(text);
            if (cardMatch.Success && PassesLuhn(cardMatch.Value))
            {
                reason = "Credit Card Number";
                return true;
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
