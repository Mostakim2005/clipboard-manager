using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClipboardApp.Core.Security
{
    public sealed class VaultCiphertext
    {
        public string Nonce { get; init; } = "";
        public string Cipher { get; init; } = "";
        public string Tag { get; init; } = "";
    }

    /// <summary>
    /// API-key vault. Secrets are encrypted with AES-256-GCM using a random
    /// vault key. The vault key is wrapped with Windows DPAPI and optionally
    /// with a master password (PBKDF2-SHA256). No secret is ever written in
    /// plaintext to disk.
    /// </summary>
    public sealed class ApiVaultService
    {
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int Pbkdf2Iterations = 310_000;
        private static readonly byte[] KeyEntropy = Encoding.UTF8.GetBytes("ClipboardApp.ApiVault.Key.v1");
        private static readonly byte[] SecretAad = Encoding.UTF8.GetBytes("ClipboardApp.ApiKey.v1");

        private readonly string _keyFilePath;
        private readonly object _sync = new();
        private byte[]? _vaultKey;
        private VaultKeyFile? _keyFile;
        private DateTime _unlockUntilUtc = DateTime.MinValue;

        public bool IsPasswordProtected { get; private set; }
        public bool IsLocked
        {
            get { lock (_sync) return _vaultKey == null; }
        }
        public DateTime LastAccessUtc { get; private set; } = DateTime.UtcNow;

        public ApiVaultService(string? keyFilePath = null)
        {
            _keyFilePath = keyFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClipboardApp", "vault.key.json");

            Initialize();
        }

        private void Initialize()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_keyFilePath)!);

            if (File.Exists(_keyFilePath))
            {
                try
                {
                    _keyFile = JsonSerializer.Deserialize<VaultKeyFile>(File.ReadAllText(_keyFilePath));
                }
                catch
                {
                    _keyFile = null;
                }
            }

            if (_keyFile == null || _keyFile.Version != 1 || string.IsNullOrEmpty(_keyFile.WrappedKey))
            {
                CreateDpapiVault();
                return;
            }

            IsPasswordProtected = string.Equals(_keyFile.Mode, "password", StringComparison.OrdinalIgnoreCase);
            if (!IsPasswordProtected)
            {
                TryUnlockWithDpapi();
            }
        }

        private void CreateDpapiVault()
        {
            var key = RandomNumberGenerator.GetBytes(KeySize);
            var wrapped = ProtectedData.Protect(key, KeyEntropy, DataProtectionScope.CurrentUser);
            _keyFile = new VaultKeyFile
            {
                Version = 1,
                Mode = "dpapi",
                WrappedKey = Convert.ToBase64String(wrapped)
            };
            WriteKeyFile();
            _vaultKey = key;
            IsPasswordProtected = false;
        }

        private bool TryUnlockWithDpapi()
        {
            if (_keyFile == null) return false;
            try
            {
                var wrapped = Convert.FromBase64String(_keyFile.WrappedKey);
                var key = ProtectedData.Unprotect(wrapped, KeyEntropy, DataProtectionScope.CurrentUser);
                if (key.Length != KeySize) return false;
                lock (_sync)
                {
                    _vaultKey = key;
                    LastAccessUtc = DateTime.UtcNow;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Unlock(string password)
        {
            if (!IsPasswordProtected)
            {
                return TryUnlockWithDpapi();
            }
            if (string.IsNullOrEmpty(password) || _keyFile == null) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(_keyFile.Salt);
                byte[] nonce = Convert.FromBase64String(_keyFile.Nonce);
                byte[] wrapped = Convert.FromBase64String(_keyFile.WrappedKey);
                byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
                int cipherLength = wrapped.Length - TagSize;
                if (cipherLength != KeySize) return false;

                byte[] cipher = new byte[cipherLength];
                byte[] tag = new byte[TagSize];
                byte[] key = new byte[cipherLength];
                Buffer.BlockCopy(wrapped, 0, cipher, 0, cipherLength);
                Buffer.BlockCopy(wrapped, cipherLength, tag, 0, TagSize);

                using var aes = new AesGcm(derived, TagSize);
                aes.Decrypt(nonce, cipher, tag, key, SecretAad);

                lock (_sync)
                {
                    _vaultKey = key;
                    LastAccessUtc = DateTime.UtcNow;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Lock()
        {
            lock (_sync)
            {
                if (_vaultKey != null)
                {
                    CryptographicOperations.ZeroMemory(_vaultKey);
                    _vaultKey = null;
                }
                _unlockUntilUtc = DateTime.MinValue;
            }
        }

        public bool SetMasterPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;

            lock (_sync)
            {
                if (_vaultKey == null) return false;
                try
                {
                    byte[] salt = RandomNumberGenerator.GetBytes(16);
                    byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
                    byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
                        Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
                    byte[] cipher = new byte[KeySize];
                    byte[] tag = new byte[TagSize];
                    using (var aes = new AesGcm(derived, TagSize))
                    {
                        aes.Encrypt(nonce, _vaultKey, cipher, tag, SecretAad);
                    }

                    var combined = new byte[cipher.Length + tag.Length];
                    Buffer.BlockCopy(cipher, 0, combined, 0, cipher.Length);
                    Buffer.BlockCopy(tag, 0, combined, cipher.Length, tag.Length);

                    _keyFile = new VaultKeyFile
                    {
                        Version = 1,
                        Mode = "password",
                        Salt = Convert.ToBase64String(salt),
                        Nonce = Convert.ToBase64String(nonce),
                        WrappedKey = Convert.ToBase64String(combined)
                    };
                    WriteKeyFile();
                    IsPasswordProtected = true;
                    LastAccessUtc = DateTime.UtcNow;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool RemoveMasterPassword()
        {
            lock (_sync)
            {
                if (_vaultKey == null) return false;
                try
                {
                    var wrapped = ProtectedData.Protect(_vaultKey, KeyEntropy, DataProtectionScope.CurrentUser);
                    _keyFile = new VaultKeyFile
                    {
                        Version = 1,
                        Mode = "dpapi",
                        WrappedKey = Convert.ToBase64String(wrapped)
                    };
                    WriteKeyFile();
                    IsPasswordProtected = false;
                    LastAccessUtc = DateTime.UtcNow;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public VaultCiphertext EncryptSecret(string secret)
        {
            lock (_sync)
            {
                if (_vaultKey == null) throw new InvalidOperationException("The API vault is locked.");
                byte[] plain = Encoding.UTF8.GetBytes(secret ?? string.Empty);
                byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
                byte[] cipher = new byte[plain.Length];
                byte[] tag = new byte[TagSize];
                using (var aes = new AesGcm(_vaultKey, TagSize))
                {
                    aes.Encrypt(nonce, plain, cipher, tag, SecretAad);
                }
                LastAccessUtc = DateTime.UtcNow;
                return new VaultCiphertext
                {
                    Nonce = Convert.ToBase64String(nonce),
                    Cipher = Convert.ToBase64String(cipher),
                    Tag = Convert.ToBase64String(tag)
                };
            }
        }

        public string DecryptSecret(VaultCiphertext ciphertext)
        {
            lock (_sync)
            {
                if (_vaultKey == null) throw new InvalidOperationException("The API vault is locked.");
                byte[] nonce = Convert.FromBase64String(ciphertext.Nonce);
                byte[] cipher = Convert.FromBase64String(ciphertext.Cipher);
                byte[] tag = Convert.FromBase64String(ciphertext.Tag);
                byte[] plain = new byte[cipher.Length];
                using (var aes = new AesGcm(_vaultKey, TagSize))
                {
                    aes.Decrypt(nonce, cipher, tag, plain, SecretAad);
                }
                LastAccessUtc = DateTime.UtcNow;
                return Encoding.UTF8.GetString(plain);
            }
        }

        public void Touch()
        {
            LastAccessUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Keeps the vault unlocked for up to <paramref name="minutes"/> after an
        /// explicit unlock, even when nobody touches it. Without this, re-opening
        /// the vault window re-prompts for the master password minutes later.
        /// </summary>
        public void ExtendUnlockWindow(int minutes)
        {
            lock (_sync)
            {
                if (_vaultKey == null) return;
                int clamped = Math.Clamp(minutes, 1, 240);
                var until = DateTime.UtcNow.AddMinutes(clamped);
                _unlockUntilUtc = until > _unlockUntilUtc ? until : _unlockUntilUtc;
                LastAccessUtc = DateTime.UtcNow;
            }
        }

        /// <summary>Remaining time on the unlock window, or TimeSpan.Zero when not unlocked.</summary>
        public TimeSpan UnlockWindowRemaining
        {
            get
            {
                lock (_sync)
                {
                    if (_vaultKey == null) return TimeSpan.Zero;
                    var remaining = _unlockUntilUtc - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
        }

        public bool LockIfIdle(int minutes)
        {
            if (!IsPasswordProtected || minutes <= 0) return false;
            lock (_sync)
            {
                // An explicit unlock window wins over the idle timer.
                if (DateTime.UtcNow < _unlockUntilUtc) return false;
                if ((DateTime.UtcNow - LastAccessUtc).TotalMinutes < minutes) return false;
            }
            Lock();
            return true;
        }

        /// <summary>Seconds left before the timed unlock window closes (0 when closed).</summary>
        public int UnlockSecondsRemaining => (int)Math.Ceiling(UnlockWindowRemaining.TotalSeconds);

        private void WriteKeyFile()
        {
            try
            {
                var dir = Path.GetDirectoryName(_keyFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = _keyFilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_keyFile, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, _keyFilePath, overwrite: true);
            }
            catch
            {
                // A vault that cannot persist its key must not pretend it saved.
                throw;
            }
        }

        private sealed class VaultKeyFile
        {
            public int Version { get; set; } = 1;
            public string Mode { get; set; } = "dpapi";
            public string WrappedKey { get; set; } = "";
            public string Salt { get; set; } = "";
            public string Nonce { get; set; } = "";
        }
    }
}
