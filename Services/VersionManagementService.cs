using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MinecraftLuanch
{
    public sealed class VersionManagementService
    {
        public List<string> GetInstalledVersionNames(string? root)
        {
            return GetInstalledVersions(root)
                .Select(version => version.VersionName)
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<VersionInfo> GetInstalledVersions(string? root)
        {
            var versions = new List<VersionInfo>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return versions;
            }

            var versionsDir = Path.Combine(root, "versions");
            if (!Directory.Exists(versionsDir))
            {
                return versions;
            }

            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var dirName = Path.GetFileName(dir);
                var jsonPath = Path.Combine(dir, $"{dirName}.json");
                if (!File.Exists(jsonPath))
                {
                    continue;
                }

                versions.Add(new VersionInfo
                {
                    VersionName = dirName,
                    VersionPath = dir
                });
            }

            return versions
                .OrderByDescending(version => version.VersionName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string GetVersionPath(string root, string versionName)
        {
            return Path.Combine(root, "versions", versionName);
        }

        public void RenameVersion(string root, string oldVersionName, string newVersionName)
        {
            var oldVersionPath = GetVersionPath(root, oldVersionName);
            if (!Directory.Exists(oldVersionPath))
            {
                throw new DirectoryNotFoundException($"版本目录不存在：{oldVersionName}");
            }

            var newVersionPath = GetVersionPath(root, newVersionName);
            if (Directory.Exists(newVersionPath))
            {
                throw new IOException("目标版本名称已存在");
            }

            Directory.Move(oldVersionPath, newVersionPath);

            var oldJsonPath = Path.Combine(newVersionPath, $"{oldVersionName}.json");
            var newJsonPath = Path.Combine(newVersionPath, $"{newVersionName}.json");
            if (File.Exists(oldJsonPath))
            {
                File.Move(oldJsonPath, newJsonPath);
            }

            var oldJarPath = Path.Combine(newVersionPath, $"{oldVersionName}.jar");
            var newJarPath = Path.Combine(newVersionPath, $"{newVersionName}.jar");
            if (File.Exists(oldJarPath))
            {
                File.Move(oldJarPath, newJarPath);
            }
        }

        public void DeleteVersion(string root, string versionName)
        {
            var versionPath = GetVersionPath(root, versionName);
            if (!Directory.Exists(versionPath))
            {
                throw new DirectoryNotFoundException($"版本目录不存在：{versionName}");
            }

            Directory.Delete(versionPath, true);
        }

        public string ImportVersion(string root, string sourcePath, bool overwrite)
        {
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException("所选版本文件夹不存在");
            }

            var versionsDir = Path.Combine(root, "versions");
            Directory.CreateDirectory(versionsDir);

            var sourceFolderName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(sourceFolderName))
            {
                throw new InvalidOperationException("无法识别版本文件夹名称");
            }

            var versionJsonPath = Path.Combine(sourcePath, $"{sourceFolderName}.json");
            if (!File.Exists(versionJsonPath))
            {
                throw new FileNotFoundException($"{sourceFolderName}.json");
            }

            var destPath = Path.Combine(versionsDir, sourceFolderName);
            if (Directory.Exists(destPath))
            {
                if (!overwrite)
                {
                    throw new IOException("目标版本已存在");
                }

                Directory.Delete(destPath, true);
            }

            CopyDirectory(sourcePath, destPath);
            return sourceFolderName;
        }

        public void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}
