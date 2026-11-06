using System;
using System.Security.Cryptography;
using System.Text;

namespace ClipboardApp.Core.Security
{
    public static class DataProtector
    {
        private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("ClipboardApp.DPAPI.v1");

        public static bool LastOperationFailed { get; private set; }
        public static string? LastError { get; private set; }

        public static byte[] EncryptBytes(byte[]? plain)
        {
            if (plain == null || plain.Length == 0) return Array.Empty<byte>();
            try
            {
                var protectedBytes = ProtectedData.Protect(plain, s_entropy, DataProtectionScope.CurrentUser);
                LastOperationFailed = false;
                return protectedBytes;
            }
            catch (Exception ex)
            {
                LastOperationFailed = true;
                LastError = ex.Message;
                // Fallback if DPAPI is unavailable (e.g. non-Windows test environment)
                return plain;
            }
        }

        public static byte[] DecryptBytes(byte[]? cipher)
        {
            if (cipher == null || cipher.Length == 0) return Array.Empty<byte>();
            try
            {
                return ProtectedData.Unprotect(cipher, s_entropy, DataProtectionScope.CurrentUser);
            }
            catch
            {
                // In case it was already plaintext or from a fallback
                return cipher;
            }
        }

        public static string EncryptString(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                var bytes = Encoding.UTF8.GetBytes(plain);
                var encrypted = EncryptBytes(bytes);
                return "ENC:" + Convert.ToBase64String(encrypted);
            }
            catch
            {
                return plain;
            }
        }

        public static string DecryptString(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return "";
            if (!cipher.StartsWith("ENC:", StringComparison.Ordinal))
            {
                // Backward-compatibility: plaintext from previous versions
                return cipher;
            }

            try
            {
                var base64 = cipher.Substring(4);
                var bytes = Convert.FromBase64String(base64);
                var decrypted = DecryptBytes(bytes);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return cipher;
            }
        }
    }
}
