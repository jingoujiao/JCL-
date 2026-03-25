using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MinecraftLuanch
{
    public sealed class LauncherSettingsData
    {
        public string GameRoot { get; set; } = string.Empty;
        public string MaxMemory { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string JavaPath { get; set; } = string.Empty;
        public string JavaTargetVersion { get; set; } = "auto";
        public string JavaDownloadSource { get; set; } = "tuna";
        public bool? FullScreen { get; set; }
        public double? AnimationSpeed { get; set; }
        public bool? IsOnlineMode { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string PlayerNameOnline { get; set; } = string.Empty;
    }

    public sealed class LauncherSettingsStore
    {
        private static readonly byte[] LegacyEncryptionKey = Encoding.UTF8.GetBytes("JCLauncher2024SecretKey32Bytes!!");
        private static readonly byte[] LegacyEncryptionIV = Encoding.UTF8.GetBytes("JCLInitVector16B");
        private static readonly byte[] SettingsEntropy = Encoding.UTF8.GetBytes("JCLauncher.Settings.v2");

        public string GetLegacySettingsPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.dat");
        }

        public string GetSettingsFilePath()
        {
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JCLauncher");
            Directory.CreateDirectory(settingsDir);
            return Path.Combine(settingsDir, "settings.dat");
        }

        public void Save(LauncherSettingsData settings)
        {
            var configPath = GetSettingsFilePath();
            var config = new StringBuilder();
            config.AppendLine($"GameRoot={settings.GameRoot}");
            config.AppendLine($"MaxMemory={settings.MaxMemory}");
            config.AppendLine($"PlayerName={ProtectString(settings.PlayerName)}");
            config.AppendLine($"JavaPath={ProtectString(settings.JavaPath)}");
            config.AppendLine($"JavaTargetVersion={settings.JavaTargetVersion}");
            config.AppendLine($"JavaDownloadSource={settings.JavaDownloadSource}");
            config.AppendLine($"FullScreen={settings.FullScreen}");
            config.AppendLine($"AnimationSpeed={settings.AnimationSpeed}");
            config.AppendLine($"IsOnlineMode={settings.IsOnlineMode}");

            if (settings.IsOnlineMode == true &&
                !string.IsNullOrWhiteSpace(settings.AccessToken) &&
                !string.IsNullOrWhiteSpace(settings.RefreshToken))
            {
                config.AppendLine($"AccessToken={ProtectString(settings.AccessToken)}");
                config.AppendLine($"RefreshToken={ProtectString(settings.RefreshToken)}");
                config.AppendLine($"PlayerNameOnline={ProtectString(settings.PlayerNameOnline)}");
            }

            File.WriteAllText(configPath, ProtectString(config.ToString()));
        }

        public LauncherSettingsData? Load()
        {
            var configPath = GetSettingsFilePath();
            var legacyConfigPath = GetLegacySettingsPath();

            if (!File.Exists(configPath) && File.Exists(legacyConfigPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.Copy(legacyConfigPath, configPath, false);
            }

            if (!File.Exists(configPath))
            {
                return null;
            }

            var encryptedData = File.ReadAllText(configPath);
            var configText = ReadSettingsPayload(encryptedData);
            if (string.IsNullOrEmpty(configText))
            {
                return null;
            }

            var settings = new LauncherSettingsData();
            var config = configText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in config)
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "GameRoot":
                        settings.GameRoot = value;
                        break;
                    case "MaxMemory":
                        settings.MaxMemory = value;
                        break;
                    case "PlayerName":
                        settings.PlayerName = ReadProtectedStringOrLegacy(value);
                        break;
                    case "JavaPath":
                        settings.JavaPath = ReadProtectedStringOrLegacy(value);
                        break;
                    case "JavaTargetVersion":
                        settings.JavaTargetVersion = value;
                        break;
                    case "JavaDownloadSource":
                        settings.JavaDownloadSource = value;
                        break;
                    case "FullScreen":
                        if (bool.TryParse(value, out var fullScreen))
                        {
                            settings.FullScreen = fullScreen;
                        }
                        break;
                    case "AnimationSpeed":
                        if (double.TryParse(value, out var animationSpeed))
                        {
                            settings.AnimationSpeed = animationSpeed;
                        }
                        break;
                    case "IsOnlineMode":
                        if (bool.TryParse(value, out var isOnlineMode))
                        {
                            settings.IsOnlineMode = isOnlineMode;
                        }
                        break;
                    case "AccessToken":
                        settings.AccessToken = ReadProtectedStringOrLegacy(value);
                        break;
                    case "RefreshToken":
                        settings.RefreshToken = ReadProtectedStringOrLegacy(value);
                        break;
                    case "PlayerNameOnline":
                        settings.PlayerNameOnline = ReadProtectedStringOrLegacy(value);
                        break;
                }
            }

            return settings;
        }

        private static string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var protectedBytes = ProtectedData.Protect(plainBytes, SettingsEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string UnprotectString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            var buffer = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(buffer, SettingsEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private static string DecryptLegacyString(string cipherText)
        {
            try
            {
                var buffer = Convert.FromBase64String(cipherText);
                using var aes = Aes.Create();
                aes.Key = LegacyEncryptionKey;
                aes.IV = LegacyEncryptionIV;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var msDecrypt = new MemoryStream(buffer);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                return srDecrypt.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryReadProtectedString(string value, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            try
            {
                result = UnprotectString(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadProtectedStringOrLegacy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (TryReadProtectedString(value, out var protectedValue))
            {
                return protectedValue;
            }

            var legacyValue = DecryptLegacyString(value);
            return string.IsNullOrEmpty(legacyValue) ? value : legacyValue;
        }

        private static string ReadSettingsPayload(string encryptedData)
        {
            if (string.IsNullOrEmpty(encryptedData))
            {
                return string.Empty;
            }

            if (TryReadProtectedString(encryptedData, out var protectedValue))
            {
                return protectedValue;
            }

            return DecryptLegacyString(encryptedData);
        }
    }
}
