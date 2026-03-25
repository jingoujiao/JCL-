using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StarLight_Core.Utilities;

namespace MinecraftLuanch
{
    public sealed class JavaRuntimeService
    {
        private readonly Dictionary<string, int> _versionJavaRequirements = new(StringComparer.OrdinalIgnoreCase)
        {
            { "1.21", 21 },
            { "1.20.6", 21 },
            { "1.20.5", 21 },
            { "1.20", 17 },
            { "1.19", 17 },
            { "1.18", 17 },
            { "1.17", 16 },
            { "1.16", 8 },
            { "1.15", 8 },
            { "1.14", 8 },
            { "1.13", 8 },
            { "1.12", 8 },
            { "1.11", 8 },
            { "1.10", 8 },
            { "1.9", 8 },
            { "1.8", 8 },
            { "1.7", 7 }
        };

        public bool IsValidJavaPath(string? javaPath)
        {
            if (string.IsNullOrWhiteSpace(javaPath))
            {
                return false;
            }

            if (!File.Exists(javaPath))
            {
                return false;
            }

            var fileName = Path.GetFileName(javaPath);
            return fileName.Equals("java.exe", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("javaw.exe", StringComparison.OrdinalIgnoreCase);
        }

        public int GetJavaVersion(string javaPath)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                var output = process?.StandardError.ReadToEnd() ?? "";

                var startIndex = output.IndexOf('"');
                var endIndex = output.IndexOf('"', startIndex + 1);

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var versionString = output.Substring(startIndex + 1, endIndex - startIndex - 1);
                    var parts = versionString.Split('.');

                    if (parts.Length >= 1)
                    {
                        if (parts[0] == "1")
                        {
                            if (parts.Length >= 2 && int.TryParse(parts[1], out var minorVersion))
                            {
                                return minorVersion;
                            }
                        }
                        else if (int.TryParse(parts[0], out var majorVersion))
                        {
                            return majorVersion;
                        }
                    }
                }
            }
            catch
            {
            }

            return 8;
        }

        public int GetRequiredJavaVersion(string minecraftVersion)
        {
            if (string.IsNullOrWhiteSpace(minecraftVersion))
            {
                return 8;
            }

            var parts = minecraftVersion.Split('.');
            if (parts.Length >= 3)
            {
                var exactVersion = $"{parts[0]}.{parts[1]}.{parts[2]}";
                if (_versionJavaRequirements.TryGetValue(exactVersion, out var exactRequiredVersion))
                {
                    return exactRequiredVersion;
                }
            }

            if (parts.Length >= 2)
            {
                var majorVersion = $"{parts[0]}.{parts[1]}";
                if (_versionJavaRequirements.TryGetValue(majorVersion, out var requiredVersion))
                {
                    return requiredVersion;
                }

                return majorVersion switch
                {
                    _ => GuessRequiredJavaVersion(parts[0])
                };
            }

            return 8;
        }

        public List<string> GetInstalledJavaPaths()
        {
            return JavaUtil.GetJavas()
                .Select(j => j.JavaPath)
                .Where(IsValidJavaPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<string> ReorderJavaPathsWithPreferred(IEnumerable<string> javaPaths, string? preferredPath)
        {
            var orderedPaths = javaPaths.ToList();
            if (string.IsNullOrWhiteSpace(preferredPath) || !IsValidJavaPath(preferredPath))
            {
                return orderedPaths;
            }

            orderedPaths.RemoveAll(path => string.Equals(path, preferredPath, StringComparison.OrdinalIgnoreCase));
            orderedPaths.Insert(0, preferredPath);
            return orderedPaths;
        }

        public string? SelectCompatibleJavaPath(IEnumerable<string> javaPaths, string version)
        {
            var candidates = javaPaths.ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var requiredJavaVersion = GetRequiredJavaVersion(version);
            string? bestPath = null;
            var bestVersion = int.MaxValue;

            foreach (var javaPath in candidates)
            {
                var javaVersion = GetJavaVersion(javaPath);
                if (javaVersion >= requiredJavaVersion && javaVersion < bestVersion)
                {
                    bestVersion = javaVersion;
                    bestPath = javaPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestPath))
            {
                return bestPath;
            }

            var maxVersion = 0;
            foreach (var javaPath in candidates)
            {
                var javaVersion = GetJavaVersion(javaPath);
                if (javaVersion > maxVersion)
                {
                    maxVersion = javaVersion;
                    bestPath = javaPath;
                }
            }

            return bestPath;
        }

        public int ResolveRequestedJavaVersion(string targetKey, int autoRequiredVersion)
        {
            if (int.TryParse(targetKey, out var selectedVersion))
            {
                return Math.Max(8, selectedVersion);
            }

            return Math.Max(8, autoRequiredVersion);
        }

        public string BuildJavaVersionHint(string? currentVersion, string targetKey)
        {
            var autoRequiredVersion = !string.IsNullOrWhiteSpace(currentVersion)
                ? GetRequiredJavaVersion(currentVersion)
                : 17;
            var resolvedVersion = ResolveRequestedJavaVersion(targetKey, autoRequiredVersion);
            var targetVersionText = targetKey == "auto"
                ? $"自动（当前版本建议 Java {autoRequiredVersion}）"
                : $"手动选择 Java {resolvedVersion}";
            return $"当前选择：{targetVersionText}；对应关系：1.21+ -> Java 21，1.17-1.20 -> Java 17，1.16及以下 -> Java 8";
        }

        public string? FindJavaExecutable(string rootDir)
        {
            if (!Directory.Exists(rootDir))
            {
                return null;
            }

            var javaw = Directory.GetFiles(rootDir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(javaw))
            {
                return javaw;
            }

            return Directory.GetFiles(rootDir, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
        }

        public List<(string Url, string SourceKey)> BuildJavaDownloadUrls(
            string officialUrl,
            string packageName,
            int javaVersion,
            string preferredSourceKey)
        {
            var all = new List<(string Url, string SourceKey)>
            {
                ($"https://mirrors.tuna.tsinghua.edu.cn/Adoptium/{javaVersion}/jdk/x64/windows/{packageName}", "tuna"),
                (officialUrl, "official"),
            };

            var preferred = all
                .Where(item => string.Equals(item.SourceKey, preferredSourceKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var fallback = all
                .Where(item => !string.Equals(item.SourceKey, preferredSourceKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            preferred.AddRange(fallback);
            return preferred;
        }

        public async Task<(string DownloadUrl, string PackageName)> GetJavaPackageInfoAsync(
            int javaVersion,
            CancellationToken cancellationToken)
        {
            var apiUrl =
                $"https://api.adoptium.net/v3/assets/latest/{javaVersion}/hotspot?os=windows&architecture=x64&image_type=jdk&jvm_impl=hotspot&heap_size=normal&vendor=eclipse";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await client.GetAsync(apiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                throw new Exception($"未找到 Java {javaVersion} 的可用安装包");
            }

            var first = root[0];
            if (!first.TryGetProperty("binary", out var binary) ||
                !binary.TryGetProperty("package", out var package) ||
                !package.TryGetProperty("link", out var linkElement))
            {
                throw new Exception("Java 下载信息格式异常（缺少 link）");
            }

            var link = linkElement.GetString();
            if (string.IsNullOrWhiteSpace(link))
            {
                throw new Exception("Java 下载地址为空");
            }

            var packageName = package.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : Path.GetFileName(new Uri(link).AbsolutePath);

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new Exception("无法解析 Java 包名");
            }

            return (link, packageName);
        }

        private static int GuessRequiredJavaVersion(string firstPartText)
        {
            if (!int.TryParse(firstPartText, out var firstPart))
            {
                return 8;
            }

            if (firstPart >= 21) return 21;
            if (firstPart >= 20) return 21;
            if (firstPart >= 17) return 17;
            if (firstPart >= 16) return 16;
            return 8;
        }
    }
}
