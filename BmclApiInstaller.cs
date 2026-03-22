using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MinecraftLuanch
{
    /// <summary>
    /// Minecraft 版本信息
    /// </summary>
    public class MinecraftVersionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public DateTime ReleaseTime { get; set; }
    }

    /// <summary>
    /// 使用 BMCLAPI 镜像源安装 Minecraft 原版
    /// </summary>
    public class BmclApiInstaller
    {
        private static readonly string BmclApiBase = "https://bmclapi2.bangbang93.com";
        
        private readonly string _version;
        private readonly string _minecraftRoot;
        private readonly Action<string, string>? _onProgress;
        private readonly Action<string>? _onLog;

        public BmclApiInstaller(string version, string minecraftRoot, Action<string, string>? onProgress = null, Action<string>? onLog = null)
        {
            _version = version;
            _minecraftRoot = minecraftRoot;
            _onProgress = onProgress;
            _onLog = onLog;
        }

        /// <summary>
        /// 从 BMCLAPI 获取版本列表
        /// </summary>
        public static async Task<List<MinecraftVersionInfo>> GetVersionListAsync()
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 优先尝试从 BMCLAPI 获取版本列表（BMCLAPI 有版本列表镜像）
            // 尝试使用 BMCLAPI 的 /mc/game/version_manifest.json 镜像
            var bmclApiManifestUrl = $"{BmclApiBase}/mc/game/version_manifest.json";
            
            try
            {
                var response = await httpClient.GetAsync(bmclApiManifestUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var versions = new List<MinecraftVersionInfo>();
                
                if (root.TryGetProperty("versions", out var versionsElement))
                {
                    foreach (var versionElement in versionsElement.EnumerateArray())
                    {
                        var version = new MinecraftVersionInfo();
                        
                        if (versionElement.TryGetProperty("id", out var idElement))
                            version.Id = idElement.GetString() ?? string.Empty;
                        
                        if (versionElement.TryGetProperty("type", out var typeElement))
                            version.Type = typeElement.GetString() ?? string.Empty;
                        
                        if (versionElement.TryGetProperty("time", out var timeElement))
                        {
                            if (DateTime.TryParse(timeElement.GetString(), out var time))
                                version.Time = time;
                        }
                        
                        if (versionElement.TryGetProperty("releaseTime", out var releaseTimeElement))
                        {
                            if (DateTime.TryParse(releaseTimeElement.GetString(), out var releaseTime))
                                version.ReleaseTime = releaseTime;
                        }
                        
                        versions.Add(version);
                    }
                }

                return versions;
            }
            catch
            {
                // 如果 BMCLAPI 也失败，使用内置的常用版本列表
                return GetFallbackVersionList();
            }
        }

        /// <summary>
        /// 获取内置的常用版本列表（备用方案）
        /// </summary>
        private static List<MinecraftVersionInfo> GetFallbackVersionList()
        {
            var versions = new List<MinecraftVersionInfo>
            {
                // 最新版本
                new MinecraftVersionInfo { Id = "1.21.1", Type = "release", ReleaseTime = DateTime.Parse("2024-08-08") },
                new MinecraftVersionInfo { Id = "1.21", Type = "release", ReleaseTime = DateTime.Parse("2024-06-13") },
                new MinecraftVersionInfo { Id = "1.20.6", Type = "release", ReleaseTime = DateTime.Parse("2024-04-29") },
                new MinecraftVersionInfo { Id = "1.20.5", Type = "release", ReleaseTime = DateTime.Parse("2024-04-23") },
                new MinecraftVersionInfo { Id = "1.20.4", Type = "release", ReleaseTime = DateTime.Parse("2023-12-07") },
                new MinecraftVersionInfo { Id = "1.20.3", Type = "release", ReleaseTime = DateTime.Parse("2023-12-05") },
                new MinecraftVersionInfo { Id = "1.20.2", Type = "release", ReleaseTime = DateTime.Parse("2023-09-21") },
                new MinecraftVersionInfo { Id = "1.20.1", Type = "release", ReleaseTime = DateTime.Parse("2023-06-12") },
                new MinecraftVersionInfo { Id = "1.20", Type = "release", ReleaseTime = DateTime.Parse("2023-06-07") },
                new MinecraftVersionInfo { Id = "1.19.4", Type = "release", ReleaseTime = DateTime.Parse("2023-03-14") },
                new MinecraftVersionInfo { Id = "1.19.3", Type = "release", ReleaseTime = DateTime.Parse("2022-12-07") },
                new MinecraftVersionInfo { Id = "1.19.2", Type = "release", ReleaseTime = DateTime.Parse("2022-08-05") },
                new MinecraftVersionInfo { Id = "1.19.1", Type = "release", ReleaseTime = DateTime.Parse("2022-07-27") },
                new MinecraftVersionInfo { Id = "1.19", Type = "release", ReleaseTime = DateTime.Parse("2022-06-07") },
                new MinecraftVersionInfo { Id = "1.18.2", Type = "release", ReleaseTime = DateTime.Parse("2022-02-28") },
                new MinecraftVersionInfo { Id = "1.18.1", Type = "release", ReleaseTime = DateTime.Parse("2021-12-10") },
                new MinecraftVersionInfo { Id = "1.18", Type = "release", ReleaseTime = DateTime.Parse("2021-11-30") },
                new MinecraftVersionInfo { Id = "1.17.1", Type = "release", ReleaseTime = DateTime.Parse("2021-07-06") },
                new MinecraftVersionInfo { Id = "1.17", Type = "release", ReleaseTime = DateTime.Parse("2021-06-08") },
                new MinecraftVersionInfo { Id = "1.16.5", Type = "release", ReleaseTime = DateTime.Parse("2021-01-15") },
                new MinecraftVersionInfo { Id = "1.16.4", Type = "release", ReleaseTime = DateTime.Parse("2020-11-02") },
                new MinecraftVersionInfo { Id = "1.16.3", Type = "release", ReleaseTime = DateTime.Parse("2020-09-10") },
                new MinecraftVersionInfo { Id = "1.16.2", Type = "release", ReleaseTime = DateTime.Parse("2020-08-11") },
                new MinecraftVersionInfo { Id = "1.16.1", Type = "release", ReleaseTime = DateTime.Parse("2020-06-24") },
                new MinecraftVersionInfo { Id = "1.16", Type = "release", ReleaseTime = DateTime.Parse("2020-06-23") },
                new MinecraftVersionInfo { Id = "1.15.2", Type = "release", ReleaseTime = DateTime.Parse("2020-01-21") },
                new MinecraftVersionInfo { Id = "1.15.1", Type = "release", ReleaseTime = DateTime.Parse("2019-12-17") },
                new MinecraftVersionInfo { Id = "1.15", Type = "release", ReleaseTime = DateTime.Parse("2019-12-10") },
                new MinecraftVersionInfo { Id = "1.14.4", Type = "release", ReleaseTime = DateTime.Parse("2019-07-19") },
                new MinecraftVersionInfo { Id = "1.14.3", Type = "release", ReleaseTime = DateTime.Parse("2019-06-24") },
                new MinecraftVersionInfo { Id = "1.14.2", Type = "release", ReleaseTime = DateTime.Parse("2019-05-27") },
                new MinecraftVersionInfo { Id = "1.14.1", Type = "release", ReleaseTime = DateTime.Parse("2019-05-13") },
                new MinecraftVersionInfo { Id = "1.14", Type = "release", ReleaseTime = DateTime.Parse("2019-04-23") },
                new MinecraftVersionInfo { Id = "1.13.2", Type = "release", ReleaseTime = DateTime.Parse("2018-10-22") },
                new MinecraftVersionInfo { Id = "1.13.1", Type = "release", ReleaseTime = DateTime.Parse("2018-08-22") },
                new MinecraftVersionInfo { Id = "1.13", Type = "release", ReleaseTime = DateTime.Parse("2018-07-18") },
                new MinecraftVersionInfo { Id = "1.12.2", Type = "release", ReleaseTime = DateTime.Parse("2017-09-18") },
                new MinecraftVersionInfo { Id = "1.12.1", Type = "release", ReleaseTime = DateTime.Parse("2017-06-02") },
                new MinecraftVersionInfo { Id = "1.12", Type = "release", ReleaseTime = DateTime.Parse("2017-06-07") },
                new MinecraftVersionInfo { Id = "1.11.2", Type = "release", ReleaseTime = DateTime.Parse("2016-12-21") },
                new MinecraftVersionInfo { Id = "1.11.1", Type = "release", ReleaseTime = DateTime.Parse("2016-12-20") },
                new MinecraftVersionInfo { Id = "1.11", Type = "release", ReleaseTime = DateTime.Parse("2016-11-14") },
                new MinecraftVersionInfo { Id = "1.10.2", Type = "release", ReleaseTime = DateTime.Parse("2016-06-23") },
                new MinecraftVersionInfo { Id = "1.10.1", Type = "release", ReleaseTime = DateTime.Parse("2016-06-22") },
                new MinecraftVersionInfo { Id = "1.10", Type = "release", ReleaseTime = DateTime.Parse("2016-06-08") },
                new MinecraftVersionInfo { Id = "1.9.4", Type = "release", ReleaseTime = DateTime.Parse("2016-05-10") },
                new MinecraftVersionInfo { Id = "1.9.3", Type = "release", ReleaseTime = DateTime.Parse("2016-05-10") },
                new MinecraftVersionInfo { Id = "1.9.2", Type = "release", ReleaseTime = DateTime.Parse("2016-03-30") },
                new MinecraftVersionInfo { Id = "1.9.1", Type = "release", ReleaseTime = DateTime.Parse("2016-03-30") },
                new MinecraftVersionInfo { Id = "1.9", Type = "release", ReleaseTime = DateTime.Parse("2016-02-29") },
                new MinecraftVersionInfo { Id = "1.8.9", Type = "release", ReleaseTime = DateTime.Parse("2015-12-09") },
                new MinecraftVersionInfo { Id = "1.8.8", Type = "release", ReleaseTime = DateTime.Parse("2015-07-28") },
                new MinecraftVersionInfo { Id = "1.8", Type = "release", ReleaseTime = DateTime.Parse("2014-09-02") },
                new MinecraftVersionInfo { Id = "1.7.10", Type = "release", ReleaseTime = DateTime.Parse("2014-06-26") },
                new MinecraftVersionInfo { Id = "1.7.2", Type = "release", ReleaseTime = DateTime.Parse("2013-10-25") },
                new MinecraftVersionInfo { Id = "1.6.4", Type = "release", ReleaseTime = DateTime.Parse("2013-09-19") },
                
                // 一些常用的快照版本
                new MinecraftVersionInfo { Id = "24w33a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-08-14") },
                new MinecraftVersionInfo { Id = "24w32a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-08-07") },
                new MinecraftVersionInfo { Id = "24w31a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-07-31") },
                new MinecraftVersionInfo { Id = "24w30a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-07-24") },
                new MinecraftVersionInfo { Id = "24w29a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-07-17") },
                new MinecraftVersionInfo { Id = "24w28a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-07-10") },
                new MinecraftVersionInfo { Id = "24w27a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-07-03") },
                new MinecraftVersionInfo { Id = "24w26a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-06-26") },
                new MinecraftVersionInfo { Id = "24w25a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-06-19") },
                new MinecraftVersionInfo { Id = "24w24a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-06-12") },
                new MinecraftVersionInfo { Id = "24w23a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-06-05") },
                new MinecraftVersionInfo { Id = "24w21a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-05-22") },
                new MinecraftVersionInfo { Id = "24w21b", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-05-24") },
                new MinecraftVersionInfo { Id = "24w20a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-05-15") },
                new MinecraftVersionInfo { Id = "24w19a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-05-08") },
                new MinecraftVersionInfo { Id = "24w18a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-05-01") },
                new MinecraftVersionInfo { Id = "24w17a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-04-24") },
                new MinecraftVersionInfo { Id = "24w16a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-04-17") },
                new MinecraftVersionInfo { Id = "24w15a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-04-10") },
                new MinecraftVersionInfo { Id = "24w14a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-04-03") },
                new MinecraftVersionInfo { Id = "24w13a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-03-27") },
                new MinecraftVersionInfo { Id = "24w12a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-03-20") },
                new MinecraftVersionInfo { Id = "24w11a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-03-13") },
                new MinecraftVersionInfo { Id = "24w10a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-03-06") },
                new MinecraftVersionInfo { Id = "24w09a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-02-28") },
                new MinecraftVersionInfo { Id = "24w08a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-02-21") },
                new MinecraftVersionInfo { Id = "24w07a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-02-14") },
                new MinecraftVersionInfo { Id = "24w06a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-02-07") },
                new MinecraftVersionInfo { Id = "24w05a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-01-31") },
                new MinecraftVersionInfo { Id = "24w04a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-01-24") },
                new MinecraftVersionInfo { Id = "24w03a", Type = "snapshot", ReleaseTime = DateTime.Parse("2024-01-17") },
                new MinecraftVersionInfo { Id = "23w51b", Type = "snapshot", ReleaseTime = DateTime.Parse("2023-12-21") },
                new MinecraftVersionInfo { Id = "23w51a", Type = "snapshot", ReleaseTime = DateTime.Parse("2023-12-20") },
            };

            return versions;
        }

        /// <summary>
        /// 获取正式版列表
        /// </summary>
        public static async Task<List<MinecraftVersionInfo>> GetReleaseVersionsAsync()
        {
            var allVersions = await GetVersionListAsync();
            return allVersions
                .Where(v => v.Type == "release")
                .OrderByDescending(v => v.ReleaseTime)
                .ToList();
        }

        /// <summary>
        /// 获取测试版（快照）列表
        /// </summary>
        public static async Task<List<MinecraftVersionInfo>> GetSnapshotVersionsAsync()
        {
            var allVersions = await GetVersionListAsync();
            return allVersions
                .Where(v => v.Type == "snapshot")
                .OrderByDescending(v => v.ReleaseTime)
                .ToList();
        }

        private void AppendLog(string message)
        {
            _onLog?.Invoke(message);
        }

        /// <summary>
        /// 安装指定版本的 Minecraft
        /// </summary>
        public async Task InstallAsync(CancellationToken cancellationToken = default)
        {
            ReportProgress("正在初始化...", "0%");
            AppendLog($"开始安装 Minecraft {_version}");
            
            // 先检查版本是否存在
            AppendLog("正在检查版本是否存在...");
            var versionExists = await CheckVersionExistsAsync(cancellationToken);
            if (!versionExists)
            {
                throw new Exception($"版本 {_version} 不存在，请确认版本号是否正确。\n例如：1.21.1, 1.20.4, 1.19.2 等");
            }

            var versionDir = Path.Combine(_minecraftRoot, "versions", _version);
            Directory.CreateDirectory(versionDir);

            // 1. 下载版本 JSON 文件
            ReportProgress("正在下载版本配置文件...", "10%");
            var versionJsonPath = Path.Combine(versionDir, $"{_version}.json");
            await DownloadVersionJsonAsync(versionJsonPath, cancellationToken);

            // 2. 解析 JSON 获取下载信息
            ReportProgress("正在解析版本信息...", "20%");
            var versionData = await ParseVersionJsonAsync(versionJsonPath, cancellationToken);

            // 3. 下载客户端 JAR
            ReportProgress("正在下载游戏核心...", "30%");
            var clientJarPath = Path.Combine(versionDir, $"{_version}.jar");
            await DownloadClientJarAsync(clientJarPath, versionData, cancellationToken);

            // 4. 下载资源文件（assets）
            ReportProgress("正在下载资源文件...", "60%");
            await DownloadAssetsAsync(versionData, cancellationToken);

            // 5. 下载 libraries（库文件）
            ReportProgress("正在下载依赖库...", "70%");
            await DownloadLibrariesAsync(versionData, cancellationToken);

            ReportProgress("安装完成", "100%");
        }

        /// <summary>
        /// 检查版本是否存在
        /// </summary>
        private async Task<bool> CheckVersionExistsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 尝试直接下载版本 JSON 来验证版本是否存在
                var url = $"{BmclApiBase}/version/{_version}/{_version}.json";
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                
                var response = await httpClient.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 下载版本 JSON 文件
        /// </summary>
        private async Task DownloadVersionJsonAsync(string savePath, CancellationToken cancellationToken)
        {
            // 尝试多个可能的 URL 格式
            string[] possibleUrls = new string[]
            {
                $"{BmclApiBase}/version/{_version}/{_version}.json",
                $"{BmclApiBase}/v1/packages/{_version}/{_version}.json",
            };

            Exception? lastException = null;
            
            foreach (var url in possibleUrls)
            {
                try
                {
                    AppendLog($"尝试下载：{url}");
                    await DownloadFileAsync(url, savePath, cancellationToken);
                    AppendLog($"版本 JSON 下载成功");
                    return;
                }
                catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    AppendLog($"URL 不存在 (404): {url}");
                    lastException = httpEx;
                    continue;
                }
                catch (Exception ex)
                {
                    AppendLog($"下载失败：{ex.Message}");
                    lastException = ex;
                    continue;
                }
            }
            
            AppendLog($"所有 URL 都失败了，请确认版本号 {_version} 是否正确");
            throw lastException ?? new Exception($"无法下载版本 {_version} 的 JSON 文件");
        }

        /// <summary>
        /// 解析版本 JSON 文件
        /// </summary>
        private async Task<JsonElement> ParseVersionJsonAsync(string jsonPath, CancellationToken cancellationToken)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            using var jsonDocument = JsonDocument.Parse(jsonContent);
            return jsonDocument.RootElement.Clone();
        }

        /// <summary>
        /// 下载客户端 JAR 文件
        /// </summary>
        private async Task DownloadClientJarAsync(string savePath, JsonElement versionData, CancellationToken cancellationToken)
        {
            // 尝试从 version JSON 中获取 client download URL
            string? clientUrl = null;
            
            if (versionData.TryGetProperty("downloads", out var downloadsElement))
            {
                if (downloadsElement.TryGetProperty("client", out var clientElement))
                {
                    if (clientElement.TryGetProperty("url", out var urlElement))
                    {
                        clientUrl = urlElement.GetString();
                    }
                }
            }

            // 如果找不到 URL，使用 BMCLAPI 的镜像
            if (string.IsNullOrEmpty(clientUrl))
            {
                clientUrl = $"{BmclApiBase}/version/{_version}/client.jar";
            }
            else
            {
                // 将官方 URL 替换为 BMCLAPI 镜像
                clientUrl = clientUrl
                    .Replace("https://launcher.mojang.com", BmclApiBase)
                    .Replace("https://piston-data.mojang.com", BmclApiBase);
            }

            await DownloadFileAsync(clientUrl, savePath, cancellationToken);
        }

        /// <summary>
        /// 下载资源文件
        /// </summary>
        private async Task DownloadAssetsAsync(JsonElement versionData, CancellationToken cancellationToken)
        {
            string? assetsIndexName = null;
            string? assetIndexUrl = null;
            
            if (versionData.TryGetProperty("assetIndex", out var assetIndexElement))
            {
                if (assetIndexElement.TryGetProperty("id", out var idElement))
                {
                    assetsIndexName = idElement.GetString();
                }
                if (assetIndexElement.TryGetProperty("url", out var urlElement))
                {
                    assetIndexUrl = urlElement.GetString();
                }
            }
            
            if (string.IsNullOrEmpty(assetsIndexName) && versionData.TryGetProperty("assets", out var assetsElement))
            {
                assetsIndexName = assetsElement.GetString();
            }

            if (string.IsNullOrEmpty(assetsIndexName))
            {
                AppendLog("未找到资源索引名称，跳过资源下载");
                return;
            }

            AppendLog($"资源索引名称: {assetsIndexName}");

            var assetsDir = Path.Combine(_minecraftRoot, "assets");
            Directory.CreateDirectory(assetsDir);

            var indexesDir = Path.Combine(assetsDir, "indexes");
            Directory.CreateDirectory(indexesDir);
            var indexJsonPath = Path.Combine(indexesDir, $"{assetsIndexName}.json");
            
            if (!File.Exists(indexJsonPath))
            {
                AppendLog($"需要下载资源索引: {assetsIndexName}.json");
                
                var indexUrls = new List<string>();
                
                if (!string.IsNullOrEmpty(assetIndexUrl))
                {
                    var bmclUrl = assetIndexUrl
                        .Replace("https://launchermeta.mojang.com", BmclApiBase)
                        .Replace("https://piston-meta.mojang.com", BmclApiBase);
                    indexUrls.Add(bmclUrl);
                    indexUrls.Add(assetIndexUrl);
                }
                
                indexUrls.Add($"{BmclApiBase}/assets/indexes/{assetsIndexName}.json");
                indexUrls.Add($"https://launchermeta.mojang.com/mc/game/assets/indexes/{assetsIndexName}.json");

                bool indexDownloaded = false;
                foreach (var url in indexUrls)
                {
                    try
                    {
                        AppendLog($"尝试从 {url} 下载资源索引");
                        await DownloadFileWithRetryAsync(url, indexJsonPath, 5, cancellationToken);
                        AppendLog("资源索引下载成功");
                        indexDownloaded = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"下载失败: {ex.Message}");
                    }
                }

                if (!indexDownloaded)
                {
                    AppendLog("警告: 无法下载资源索引文件，跳过资源下载");
                    return;
                }
            }
            else
            {
                AppendLog($"使用已存在的资源索引: {assetsIndexName}.json");
            }

            var indexJson = await File.ReadAllTextAsync(indexJsonPath, cancellationToken);
            using var indexDoc = JsonDocument.Parse(indexJson);
            
            if (!indexDoc.RootElement.TryGetProperty("objects", out var objects))
            {
                AppendLog("警告: 资源索引格式不正确，跳过资源下载");
                return;
            }

            var objectsDir = Path.Combine(assetsDir, "objects");
            Directory.CreateDirectory(objectsDir);

            var assetsToDownload = new List<(string name, string hash, string prefix)>();
            
            foreach (var prop in objects.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("hash", out var hashElement))
                    continue;

                var hash = hashElement.GetString();
                if (string.IsNullOrEmpty(hash)) continue;

                var prefix = hash.Substring(0, 2);
                var assetDir = Path.Combine(objectsDir, prefix);
                Directory.CreateDirectory(assetDir);
                
                var assetPath = Path.Combine(assetDir, hash);
                
                if (!File.Exists(assetPath))
                {
                    assetsToDownload.Add((prop.Name, hash, prefix));
                }
            }

            int total = assetsToDownload.Count;
            if (total == 0)
            {
                AppendLog("所有资源文件已存在，无需下载");
                return;
            }

            AppendLog($"共 {total} 个资源文件需要下载");

            int downloaded = 0;
            int failed = 0;
            var failedAssets = new List<string>();
            
            var semaphore = new SemaphoreSlim(8);
            var tasks = new List<Task>();

            foreach (var (name, hash, prefix) in assetsToDownload)
            {
                await semaphore.WaitAsync(cancellationToken);
                
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var assetDir = Path.Combine(objectsDir, prefix);
                        var assetPath = Path.Combine(assetDir, hash);
                        
                        string[] assetUrls = new string[]
                        {
                            $"{BmclApiBase}/assets/{prefix}/{hash}",
                            $"https://resources.download.minecraft.net/{prefix}/{hash}",
                        };

                        bool success = false;
                        Exception? lastEx = null;
                        int retryCount = 0;
                        
                        foreach (var url in assetUrls)
                        {
                            retryCount++;
                            try
                            {
                                await DownloadFileWithRetryAsync(url, assetPath, 5, cancellationToken);
                                success = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                lastEx = ex;
                                if (File.Exists(assetPath))
                                {
                                    try { File.Delete(assetPath); } catch { }
                                }
                                if (retryCount == 1 && assetUrls.Length > 1)
                                {
                                    AppendLog($"BMCLAPI下载失败，尝试官方源: {name}");
                                }
                            }
                        }

                        if (!success)
                        {
                            lock (failedAssets)
                            {
                                failedAssets.Add(name);
                                failed++;
                            }
                            if (failed <= 10)
                            {
                                AppendLog($"资源下载失败: {name} - {lastEx?.Message}");
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Increment(ref downloaded);
                        
                        if (downloaded % 50 == 0 || downloaded == total)
                        {
                            var progress = 50 + (downloaded * 20 / total);
                            ReportProgress($"正在下载资源文件 ({downloaded}/{total}, 失败: {failed})", $"{progress}%");
                        }
                    }
                }, cancellationToken);
                
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            AppendLog($"资源文件下载完成: 成功 {total - failed}, 失败 {failed}");
            
            if (failed > 0)
            {
                AppendLog($"失败的资源文件列表（前20个）:");
                foreach (var name in failedAssets.Take(20))
                {
                    AppendLog($"  - {name}");
                }
                AppendLog($"提示: 如果资源下载失败，可以尝试重新安装该版本，已下载的文件会被跳过");
            }
            
            if (failed > total * 0.1)
            {
                AppendLog($"警告: 超过10%的资源文件下载失败，游戏可能无法正常运行");
                AppendLog($"建议: 检查网络连接后重新安装该版本");
            }
        }

        /// <summary>
        /// 带重试的文件下载
        /// </summary>
        private async Task DownloadFileWithRetryAsync(string url, string savePath, int maxRetries, CancellationToken cancellationToken)
        {
            Exception? lastException = null;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var httpClient = new HttpClient
                    {
                        Timeout = TimeSpan.FromMinutes(2)
                    };
                    
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var dir = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var tempPath = savePath + ".tmp";
                    
                    using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = File.Create(tempPath))
                    {
                        var buffer = new byte[81920];
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        }
                    }
                    
                    if (File.Exists(savePath))
                    {
                        File.Delete(savePath);
                    }
                    File.Move(tempPath, savePath);
                    
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(1000 * (i + 1), cancellationToken);
                    }
                }
            }

            throw lastException ?? new Exception($"下载失败：{url}");
        }

        /// <summary>
        /// 下载依赖库文件
        /// </summary>
        private async Task DownloadLibrariesAsync(JsonElement versionData, CancellationToken cancellationToken)
        {
            var librariesDir = Path.Combine(_minecraftRoot, "libraries");
            Directory.CreateDirectory(librariesDir);

            if (!versionData.TryGetProperty("libraries", out var librariesElement))
            {
                AppendLog("未找到库文件列表，跳过库下载");
                return;
            }

            var libraries = new List<JsonElement>();
            foreach (var lib in librariesElement.EnumerateArray())
            {
                libraries.Add(lib);
            }

            int total = libraries.Count;
            int downloaded = 0;
            int failed = 0;
            int skipped = 0;

            AppendLog($"共 {total} 个库文件需要检查");

            foreach (var lib in libraries)
            {
                // 检查是否满足平台要求
                if (lib.TryGetProperty("rules", out var rulesElement))
                {
                    // 简单的规则检查 - 如果有 rules，暂时跳过复杂检查
                    // 实际应该根据操作系统等条件判断
                }

                // 检查是否有 downloads 信息
                if (!lib.TryGetProperty("downloads", out var downloadsElement))
                {
                    // 没有 downloads 信息，尝试从 name 构建 URL
                    if (lib.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            downloaded++;
                            continue;
                        }
                    }
                    continue;
                }

                // 获取 artifact 信息
                if (!downloadsElement.TryGetProperty("artifact", out var artifactElement))
                {
                    downloaded++;
                    continue;
                }

                // 获取路径
                string? libPath = null;
                if (artifactElement.TryGetProperty("path", out var pathElement))
                {
                    libPath = pathElement.GetString();
                }

                if (string.IsNullOrEmpty(libPath))
                {
                    // 从 name 构建 path
                    if (lib.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            var parts = name.Split(':');
                            if (parts.Length >= 3)
                            {
                                var groupId = parts[0].Replace('.', '/');
                                var artifactId = parts[1];
                                var version = parts[2];
                                libPath = $"{groupId}/{artifactId}/{version}/{artifactId}-{version}.jar";
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(libPath))
                {
                    downloaded++;
                    continue;
                }

                var fullPath = Path.Combine(librariesDir, libPath);
                var libDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(libDir))
                {
                    Directory.CreateDirectory(libDir);
                }

                if (File.Exists(fullPath))
                {
                    skipped++;
                    downloaded++;
                    continue;
                }

                // 获取下载 URL
                string? downloadUrl = null;
                if (artifactElement.TryGetProperty("url", out var urlElement))
                {
                    downloadUrl = urlElement.GetString();
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    // 从 name 构建 URL
                    if (lib.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            var parts = name.Split(':');
                            if (parts.Length >= 3)
                            {
                                var groupId = parts[0].Replace('.', '/');
                                var artifactId = parts[1];
                                var version = parts[2];
                                downloadUrl = $"{BmclApiBase}/maven/{groupId}/{artifactId}/{version}/{artifactId}-{version}.jar";
                            }
                        }
                    }
                }
                else
                {
                    // 将官方 URL 替换为 BMCLAPI 镜像
                    downloadUrl = downloadUrl
                        .Replace("https://libraries.minecraft.net", $"{BmclApiBase}/maven")
                        .Replace("https://maven.minecraftforge.net", $"{BmclApiBase}/maven")
                        .Replace("https://maven.fabricmc.net", $"{BmclApiBase}/maven");
                }

                bool libDownloaded = false;
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    // 尝试多个 URL
                    string[] urls = new string[]
                    {
                        downloadUrl,
                        downloadUrl.Replace(BmclApiBase, "https://libraries.minecraft.net"),
                    };

                    foreach (var url in urls)
                    {
                        try
                        {
                            await DownloadFileAsync(url, fullPath, cancellationToken);
                            libDownloaded = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            // 继续尝试下一个 URL
                        }
                    }
                }

                if (!libDownloaded)
                {
                    failed++;
                    if (failed <= 5)
                    {
                        AppendLog($"警告: 库文件 {Path.GetFileName(libPath)} 下载失败");
                    }
                }

                downloaded++;
                if (downloaded % 10 == 0 || downloaded == total)
                {
                    var progress = 70 + (downloaded * 20 / total);
                    ReportProgress($"正在下载依赖库 ({downloaded}/{total})", $"{progress}%");
                }
            }

            AppendLog($"库文件处理完成: {downloaded}/{total}, 跳过已存在: {skipped}, 失败: {failed}");
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        private async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            // 添加重试逻辑
            int retries = 3;
            Exception? lastException = null;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var dir = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var fileStream = File.Create(savePath);
                    
                    var buffer = new byte[81920];
                    int bytesRead;
                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    long downloadedBytes = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        downloadedBytes += bytesRead;
                        
                        // 报告下载进度（如果知道总大小）
                        if (totalBytes > 0)
                        {
                            var percent = (int)(downloadedBytes * 100 / totalBytes);
                            // 这里不直接调用 ReportProgress，因为外层会处理
                        }
                    }

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(1000 * (i + 1), cancellationToken);
                }
            }

            throw lastException ?? new Exception($"下载失败：{url}");
        }

        private void ReportProgress(string message, string percentage)
        {
            _onProgress?.Invoke(message, percentage);
        }
    }
}
