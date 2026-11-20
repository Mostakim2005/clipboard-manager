using System;
using System.Security.Cryptography;
using System.Text;

namespace ClipboardApp.Core.Security
{
    public static class DataProtector
    {
        private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("ClipboardApp.DPAPI.v1");

        private static volatile bool s_lastOperationFailed;
        private static volatile string? s_lastError;

        public static bool LastOperationFailed => s_lastOperationFailed;
        public static string? LastError => s_lastError;

        public static byte[] EncryptBytes(byte[]? plain)
        {
            if (plain == null || plain.Length == 0) return Array.Empty<byte>();
            try
            {
                var protectedBytes = ProtectedData.Protect(plain, s_entropy, DataProtectionScope.CurrentUser);
                s_lastOperationFailed = false;
                s_lastError = null;
                return protectedBytes;
            }
            catch (PlatformNotSupportedException ex)
            {
                // Explicit non-Windows test environment fallback only.
                s_lastOperationFailed = true;
                s_lastError = ex.Message;
                return plain;
            }
            catch (Exception ex)
            {
                s_lastOperationFailed = true;
                s_lastError = ex.Message;
                throw;
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
            var bytes = Encoding.UTF8.GetBytes(plain);
            var encrypted = EncryptBytes(bytes);
            // Never disguise unencrypted fallback bytes under the "ENC:" ciphertext prefix.
            if (s_lastOperationFailed)
            {
                return plain;
            }
            return "ENC:" + Convert.ToBase64String(encrypted);
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
