using StarLight_Core.Authentication;
using StarLight_Core.Enum;
using StarLight_Core.Installer;
using StarLight_Core.Launch;
using StarLight_Core.Models.Authentication;
using StarLight_Core.Models.Launch;
using StarLight_Core.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace MinecraftLuanch
{
    /// <summary>
    /// 版本信息模型
    /// </summary>
    public class VersionInfo
    {
        public string VersionName { get; set; } = string.Empty;
        public string VersionPath { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        private DateTime _lastRootChange = DateTime.MinValue;
        private string? _currentVersion;
        
        private bool _isOnlineMode = false;
        private GetTokenResponse? _cachedTokenInfo;
        private string? _cachedPlayerName;
        private bool _isInitialized = false;
        
        private const string MicrosoftClientId = "e1e383f9-59d9-4aa2-bf5e-73fe83b15ba0";
        
        private Dictionary<string, int> _versionJavaRequirements = new()
        {
            // Minecraft 版本 -> 所需的最低 Java 版本
            { "1.21", 21 },
            { "1.20.5", 21 },
            { "1.20.6", 21 },
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

        public MainWindow()
        {
            InitializeComponent();
            
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            var defaultMinecraftPath = Path.Combine(appPath, ".minecraft");
            
            GameRoot.Text = defaultMinecraftPath;
            CurrentGameRoot.Text = defaultMinecraftPath;

            LoadSettingsFromFile();

            if (!Directory.Exists(GameRoot.Text))
            {
                try
                {
                    Directory.CreateDirectory(GameRoot.Text);
                    Directory.CreateDirectory(Path.Combine(GameRoot.Text, "versions"));
                    AppendLog($"已创建游戏目录: {GameRoot.Text}");
                }
                catch (Exception ex)
                {
                    AppendLog($"创建游戏目录失败: {ex.Message}");
                }
            }

            RefreshVersions();
            RefreshJava();
            
            // 初始化时加载版本列表
            _ = LoadVersionListAsync();
            
            // 加载公告
            _ = LoadAnnouncementAsync();
            
            _isInitialized = true;
            
            if (_isOnlineMode)
            {
                OfflineModeRadio.IsChecked = false;
                OnlineModeRadio.IsChecked = true;
                OfflineAccountPanel.Visibility = Visibility.Collapsed;
                OnlineAccountPanel.Visibility = Visibility.Visible;
                SaveAccountButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                SaveAccountButton.Visibility = Visibility.Visible;
            }
            
            UpdateAccountInfo();
        }

        /// <summary>
        /// 加载公告栏内容
        /// </summary>
        private async Task LoadAnnouncementAsync()
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                
                // 尝试从 jingoujiao.github.io 获取公告
                var response = await httpClient.GetAsync("https://jingoujiao.github.io/");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // 如果内容不为空，则显示获取的内容
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        AnnouncementText.Text = content.Trim();
                        AppendLog("公告栏加载成功");
                        return;
                    }
                }
                
                // 如果获取失败或内容为空，显示默认公告
                ShowDefaultAnnouncement();
            }
            catch (Exception ex)
            {
                AppendLog($"加载公告栏失败：{ex.Message}");
                // 如果网络错误，显示默认公告
                ShowDefaultAnnouncement();
            }
        }

        /// <summary>
        /// 显示默认公告
        /// </summary>
        private void ShowDefaultAnnouncement()
        {
            var defaultAnnouncement = "🎉 欢迎使用 Minecraft 启动器！\n\n" +
                                     "✨ 本启动器具有以下特点：\n" +
                                     "• 支持离线模式，无需正版账号\n" +
                                     "• 自动选择适配的 Java 版本\n" +
                                     "• 使用 BMCLAPI 镜像源高速下载\n" +
                                     "• 简洁美观的多界面设计\n\n" +
                                     "📢 最近更新：\n" +
                                     "• 优化界面布局，更加美观\n" +
                                     "• 添加设置保存功能\n" +
                                     "• 改进版本选择体验\n\n" +
                                     "祝你游戏愉快！";
            
            AnnouncementText.Text = defaultAnnouncement;
        }

        /// <summary>
        /// 加载版本列表
        /// </summary>
        private async Task LoadVersionListAsync()
        {
            try
            {
                AppendLog("正在加载版本列表...");
                var versions = await BmclApiInstaller.GetReleaseVersionsAsync();
                
                InstallVersion.ItemsSource = versions.Select(v => v.Id).ToList();
                
                if (InstallVersion.Items.Count > 0)
                {
                    InstallVersion.SelectedIndex = 0;
                    AppendLog($"已加载 {versions.Count} 个正式版版本");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"加载版本列表失败：{ex.Message}");
                MessageBox.Show($"加载版本列表失败：\n{ex.Message}\n\n请检查网络连接，或点击\"刷新\"按钮重新加载。");
            }
        }

        /// <summary>
        /// 根据版本类型加载版本列表
        /// </summary>
        private async Task LoadVersionListByTypeAsync(string type)
        {
            try
            {
                List<MinecraftVersionInfo> versions;
                
                if (type == "snapshot")
                {
                    versions = await BmclApiInstaller.GetSnapshotVersionsAsync();
                    AppendLog($"已加载 {versions.Count} 个测试版（快照）版本");
                }
                else
                {
                    versions = await BmclApiInstaller.GetReleaseVersionsAsync();
                    AppendLog($"已加载 {versions.Count} 个正式版版本");
                }
                
                InstallVersion.ItemsSource = versions.Select(v => v.Id).ToList();
                
                if (InstallVersion.Items.Count > 0)
                {
                    InstallVersion.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"加载版本列表失败：{ex.Message}");
                MessageBox.Show($"加载版本列表失败：\n{ex.Message}");
            }
        }

        private void InstallVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentVersion = InstallVersion.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(_currentVersion))
            {
                // 版本改变时，自动刷新 Java 选择
                RefreshJava();
            }
        }

        private void GameVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentVersion = GameVersion.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(_currentVersion))
            {
                // 版本改变时，自动刷新 Java 选择
                RefreshJava();
            }
        }

        private void RefreshJava()
        {
            var javas = JavaUtil.GetJavas().ToList();
            JavaPath.ItemsSource = javas;
            
            // 根据当前选择的版本自动选择合适的 Java
            if (javas.Count > 0 && !string.IsNullOrWhiteSpace(_currentVersion))
            {
                SelectCompatibleJava(javas, _currentVersion);
            }
            else if (javas.Count > 0)
            {
                JavaPath.SelectedIndex = 0;
            }
        }

        private void SelectCompatibleJava(List<StarLight_Core.Models.Utilities.JavaInfo> javas, string version)
        {
            var requiredJavaVersion = GetRequiredJavaVersion(version);
            AppendLog($"游戏版本 {version} 需要 Java {requiredJavaVersion} 或更高版本");
            
            // 尝试找到兼容的 Java 版本
            int bestIndex = -1;
            int bestVersion = int.MaxValue;
            
            for (int i = 0; i < javas.Count; i++)
            {
                var javaPath = javas[i].JavaPath;
                var javaVersion = GetJavaVersion(javaPath);
                
                if (javaVersion >= requiredJavaVersion && javaVersion < bestVersion)
                {
                    bestVersion = javaVersion;
                    bestIndex = i;
                }
            }
            
            // 如果没有找到完全兼容的，选择一个版本最高的
            if (bestIndex == -1)
            {
                int maxVersion = 0;
                for (int i = 0; i < javas.Count; i++)
                {
                    var javaPath = javas[i].JavaPath;
                    var javaVersion = GetJavaVersion(javaPath);
                    if (javaVersion > maxVersion)
                    {
                        maxVersion = javaVersion;
                        bestIndex = i;
                    }
                }
            }
            
            if (bestIndex >= 0)
            {
                JavaPath.SelectedIndex = bestIndex;
                AppendLog($"已自动选择 Java 版本：{javas[bestIndex].JavaPath}");
            }
        }

        private int GetJavaVersion(string javaPath)
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
                
                // 解析 Java 版本
                var versionString = output;
                var startIndex = versionString.IndexOf('"');
                var endIndex = versionString.IndexOf('"', startIndex + 1);
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    versionString = versionString.Substring(startIndex + 1, endIndex - startIndex - 1);
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
            
            return 8; // 默认返回 Java 8
        }

        private int GetRequiredJavaVersion(string minecraftVersion)
        {
            // 提取主版本号（如 1.21.1 -> 1.21）
            var parts = minecraftVersion.Split('.');
            if (parts.Length >= 2)
            {
                var majorVersion = $"{parts[0]}.{parts[1]}";
                if (_versionJavaRequirements.TryGetValue(majorVersion, out var version))
                {
                    return version;
                }
                
                // 尝试只匹配第一个数字（如 21w 快照版本）
                if (int.TryParse(parts[0], out var firstPart))
                {
                    if (firstPart >= 21) return 21;
                    if (firstPart >= 20) return 21;
                    if (firstPart >= 17) return 17;
                    if (firstPart >= 16) return 16;
                    return 8;
                }
            }
            
            // 默认返回 8
            return 8;
        }

        private void BrowseRoot_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择 .minecraft 文件夹";
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                GameRoot.Text = dialog.SelectedPath;
            }
        }

        private void GameRoot_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 防止循环触发
            if (_lastRootChange + TimeSpan.FromMilliseconds(100) > DateTime.Now)
                return;

            RefreshVersions();
            _lastRootChange = DateTime.Now;
        }

        private void RefreshVersions()
        {
            var root = GameRoot.Text?.Trim();
            var versions = TryGetInstalledVersions(root);
            GameVersion.ItemsSource = versions;
            if (versions.Count > 0)
            {
                GameVersion.SelectedIndex = 0;
                // 初始化时设置当前版本
                _currentVersion = versions[0];
                // 初始化时自动选择兼容的 Java
                RefreshJava();
            }
        }

        private List<string> TryGetInstalledVersions(string? root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return new List<string>();

            var versionsDir = Path.Combine(root, "versions");
            if (!Directory.Exists(versionsDir))
                return new List<string>();

            var versions = new List<string>();

            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var dirName = Path.GetFileName(dir);
                var jsonPath = Path.Combine(dir, $"{dirName}.json");
                if (File.Exists(jsonPath))
                {
                    versions.Add(dirName);
                }
            }

            return versions.OrderByDescending(v => v).ToList();
        }

        private void RefreshVersions_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersions();
        }

        private void RefreshJava_Click(object sender, RoutedEventArgs e)
        {
            RefreshJava();
        }

        private void AppendLog(string line)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(line + Environment.NewLine);
                LogBox.ScrollToEnd();
            });
        }

        /// <summary>
        /// 切换界面
        /// </summary>
        private void SwitchPage(string pageName)
        {
            LaunchPage.Visibility = Visibility.Collapsed;
            DownloadPage.Visibility = Visibility.Collapsed;
            VersionsPage.Visibility = Visibility.Collapsed;
            AccountPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;
            MorePage.Visibility = Visibility.Collapsed;

            switch (pageName)
            {
                case "Launch":
                    LaunchPage.Visibility = Visibility.Visible;
                    LaunchButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    DownloadButton.Background = System.Windows.Media.Brushes.Transparent;
                    VersionsButton.Background = System.Windows.Media.Brushes.Transparent;
                    AccountButton.Background = System.Windows.Media.Brushes.Transparent;
                    SettingsButton.Background = System.Windows.Media.Brushes.Transparent;
                    MoreButton.Background = System.Windows.Media.Brushes.Transparent;
                    break;
                case "Download":
                    DownloadPage.Visibility = Visibility.Visible;
                    LaunchButton.Background = System.Windows.Media.Brushes.Transparent;
                    DownloadButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    VersionsButton.Background = System.Windows.Media.Brushes.Transparent;
                    AccountButton.Background = System.Windows.Media.Brushes.Transparent;
                    SettingsButton.Background = System.Windows.Media.Brushes.Transparent;
                    MoreButton.Background = System.Windows.Media.Brushes.Transparent;
                    break;
                case "Versions":
                    VersionsPage.Visibility = Visibility.Visible;
                    LaunchButton.Background = System.Windows.Media.Brushes.Transparent;
                    DownloadButton.Background = System.Windows.Media.Brushes.Transparent;
                    VersionsButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    AccountButton.Background = System.Windows.Media.Brushes.Transparent;
                    SettingsButton.Background = System.Windows.Media.Brushes.Transparent;
                    MoreButton.Background = System.Windows.Media.Brushes.Transparent;
                    break;
                case "Account":
                    AccountPage.Visibility = Visibility.Visible;
                    UpdateAccountInfo();
                    LaunchButton.Background = System.Windows.Media.Brushes.Transparent;
                    DownloadButton.Background = System.Windows.Media.Brushes.Transparent;
                    VersionsButton.Background = System.Windows.Media.Brushes.Transparent;
                    AccountButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    SettingsButton.Background = System.Windows.Media.Brushes.Transparent;
                    MoreButton.Background = System.Windows.Media.Brushes.Transparent;
                    break;
                case "Settings":
                    SettingsPage.Visibility = Visibility.Visible;
                    CurrentGameRoot.Text = GameRoot.Text;
                    LaunchButton.Background = System.Windows.Media.Brushes.Transparent;
                    DownloadButton.Background = System.Windows.Media.Brushes.Transparent;
                    VersionsButton.Background = System.Windows.Media.Brushes.Transparent;
                    AccountButton.Background = System.Windows.Media.Brushes.Transparent;
                    SettingsButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    MoreButton.Background = System.Windows.Media.Brushes.Transparent;
                    break;
                case "More":
                    MorePage.Visibility = Visibility.Visible;
                    LaunchButton.Background = System.Windows.Media.Brushes.Transparent;
                    DownloadButton.Background = System.Windows.Media.Brushes.Transparent;
                    VersionsButton.Background = System.Windows.Media.Brushes.Transparent;
                    AccountButton.Background = System.Windows.Media.Brushes.Transparent;
                    SettingsButton.Background = System.Windows.Media.Brushes.Transparent;
                    MoreButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    break;
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPage("Launch");
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPage("Download");
        }

        private void VersionsButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPage("Versions");
            RefreshVersionsList();
        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPage("Account");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPage("Settings");
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPage("More");
        }

        /// <summary>
        /// 更新账号信息显示
        /// </summary>
        private void UpdateAccountInfo()
        {
            if (_isOnlineMode)
            {
                CurrentAccountInfo.Text = "模式：正版验证（微软账号）";
                CurrentPlayerName.Text = string.IsNullOrEmpty(_cachedPlayerName) 
                    ? "昵称：未登录" 
                    : $"昵称：{_cachedPlayerName}";
                CurrentPlayerUuid.Visibility = Visibility.Collapsed;
            }
            else
            {
                CurrentAccountInfo.Text = "模式：离线模式";
                var playerName = PlayerName.Text?.Trim();
                CurrentPlayerName.Text = string.IsNullOrWhiteSpace(playerName) 
                    ? "昵称：未设置" 
                    : $"昵称：{playerName}";
                CurrentPlayerUuid.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 离线模式选择
        /// </summary>
        private void OfflineModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _isOnlineMode = false;
            OfflineAccountPanel.Visibility = Visibility.Visible;
            OnlineAccountPanel.Visibility = Visibility.Collapsed;
            SaveAccountButton.Visibility = Visibility.Visible;
            UpdateAccountInfo();
        }

        /// <summary>
        /// 正版模式选择
        /// </summary>
        private void OnlineModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _isOnlineMode = true;
            OfflineAccountPanel.Visibility = Visibility.Collapsed;
            OnlineAccountPanel.Visibility = Visibility.Visible;
            SaveAccountButton.Visibility = Visibility.Collapsed;
            UpdateAccountInfo();
        }

        /// <summary>
        /// 微软账号登录
        /// </summary>
        private async void MicrosoftLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() => MicrosoftLoginButton.IsEnabled = false);
            await Dispatcher.InvokeAsync(() => LoginStatusText.Text = "正在启动登录流程...");
            
            try
            {
                var auth = new MicrosoftAuthentication(MicrosoftClientId);
                var deviceCodeInfo = await auth.RetrieveDeviceCodeInfo();
                
                await Dispatcher.InvokeAsync(() => 
                    LoginStatusText.Text = $"正在打开浏览器...\n\n验证代码: {deviceCodeInfo.UserCode}\n\n请在浏览器中完成登录");
                
                System.Windows.Clipboard.SetText(deviceCodeInfo.UserCode);
                
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = deviceCodeInfo.VerificationUri,
                        UseShellExecute = true
                    });
                    MessageBox.Show($"验证代码已复制到剪贴板！\n\n代码: {deviceCodeInfo.UserCode}\n\n浏览器已打开，请在浏览器中粘贴代码并完成登录。", 
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception)
                {
                    MessageBox.Show($"无法自动打开浏览器，验证代码已复制到剪贴板。\n\n请手动访问：{deviceCodeInfo.VerificationUri}\n输入代码：{deviceCodeInfo.UserCode}", 
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                var tokenInfo = await auth.GetTokenResponse(deviceCodeInfo);
                var userInfo = await auth.MicrosoftAuthAsync(tokenInfo, progress =>
                {
                    Dispatcher.InvokeAsync(() => LoginStatusText.Text = progress);
                });
                
                if (userInfo != null && !string.IsNullOrEmpty(userInfo.AccessToken))
                {
                    _cachedTokenInfo = tokenInfo;
                    _cachedPlayerName = userInfo.Name;
                    _isOnlineMode = true;
                    
                    AppendLog($"登录成功 - AccessToken长度: {tokenInfo?.AccessToken?.Length ?? 0}, RefreshToken长度: {tokenInfo?.RefreshToken?.Length ?? 0}");
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LoginStatusText.Text = $"登录成功！\n玩家：{userInfo.Name}";
                        UpdateAccountInfo();
                    });
                    
                    SaveSettingsToFile();
                    
                    MessageBox.Show($"微软账号登录成功！\n\n玩家名称：{userInfo.Name}\nUUID：{userInfo.Uuid}\n\n账号信息已自动保存。", 
                        "登录成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => LoginStatusText.Text = "登录失败：未获取到有效的访问令牌");
                    MessageBox.Show("登录失败，请重试。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => LoginStatusText.Text = $"登录失败：{ex.Message}");
                MessageBox.Show($"登录失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => MicrosoftLoginButton.IsEnabled = true);
            }
        }

        /// <summary>
        /// 保存账号设置
        /// </summary>
        private void SaveAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOnlineMode)
            {
                if (_cachedTokenInfo == null)
                {
                    MessageBox.Show("请先登录微软账号！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                var playerName = PlayerName.Text?.Trim();
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    MessageBox.Show("请输入离线昵称！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            UpdateAccountInfo();
            SaveSettingsToFile();
            MessageBox.Show("账号设置已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 删除账号
        /// </summary>
        private void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要删除当前账号信息吗？\n\n删除后需要重新登录。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _isOnlineMode = false;
                _cachedTokenInfo = null;
                _cachedPlayerName = null;
                PlayerName.Text = "";
                
                OfflineModeRadio.IsChecked = true;
                OnlineModeRadio.IsChecked = false;
                OfflineAccountPanel.Visibility = Visibility.Visible;
                OnlineAccountPanel.Visibility = Visibility.Collapsed;
                
                LoginStatusText.Text = "";
                UpdateAccountInfo();
                SaveSettingsToFile();
                
                MessageBox.Show("账号已删除！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 刷新版本列表
        /// </summary>
        private void RefreshVersionsList()
        {
            var root = GameRoot.Text?.Trim();
            var versions = GetInstalledVersions(root);
            VersionsList.ItemsSource = versions;
            AppendLog($"已加载 {versions.Count} 个已安装版本");
        }

        /// <summary>
        /// 获取已安装的版本列表
        /// </summary>
        private List<VersionInfo> GetInstalledVersions(string? root)
        {
            var versions = new List<VersionInfo>();
            
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return versions;

            var versionsDir = Path.Combine(root, "versions");
            if (!Directory.Exists(versionsDir))
                return versions;

            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var dirName = Path.GetFileName(dir);
                var jsonPath = Path.Combine(dir, $"{dirName}.json");
                
                if (File.Exists(jsonPath))
                {
                    versions.Add(new VersionInfo
                    {
                        VersionName = dirName,
                        VersionPath = dir
                    });
                }
            }

            return versions.OrderByDescending(v => v.VersionName).ToList();
        }

        /// <summary>
        /// 刷新版本列表按钮点击
        /// </summary>
        private void RefreshVersionsListButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersionsList();
            RefreshVersions();
            MessageBox.Show("版本列表已刷新！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 打开版本文件夹
        /// </summary>
        private void OpenVersionFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string versionName)
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("游戏目录未设置！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var versionPath = Path.Combine(root, "versions", versionName);
                if (Directory.Exists(versionPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{versionPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"打开文件夹失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("版本文件夹不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 重命名版本
        /// </summary>
        private void RenameVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string oldVersionName)
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("游戏目录未设置！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var oldVersionPath = Path.Combine(root, "versions", oldVersionName);
                if (!Directory.Exists(oldVersionPath))
                {
                    MessageBox.Show("版本文件夹不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var inputDialog = new Window
                {
                    Title = "重命名版本",
                    Width = 400,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
                };

                var grid = new Grid { Margin = new Thickness(20) };
                var row1 = new RowDefinition { Height = GridLength.Auto };
                var row2 = new RowDefinition { Height = GridLength.Auto };
                var row3 = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
                grid.RowDefinitions.Add(row1);
                grid.RowDefinitions.Add(row2);
                grid.RowDefinitions.Add(row3);

                var label = new TextBlock
                {
                    Text = "请输入新的版本名称：",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(label, 0);

                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = oldVersionName,
                    FontSize = 14,
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 10),
                    VerticalContentAlignment = System.Windows.VerticalAlignment.Center
                };
                Grid.SetRow(textBox, 1);

                var buttonPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                Grid.SetRow(buttonPanel, 2);

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "确定",
                    Width = 80,
                    Height = 32,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 13
                };

                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "取消",
                    Width = 80,
                    Height = 32,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(0),
                    FontSize = 13
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                grid.Children.Add(label);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);

                inputDialog.Content = grid;

                bool? dialogResult = false;
                string? newVersionName = null;

                okButton.Click += (s, args) => 
                { 
                    newVersionName = textBox.Text?.Trim();
                    if (string.IsNullOrWhiteSpace(newVersionName))
                    {
                        MessageBox.Show("版本名称不能为空！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (newVersionName == oldVersionName)
                    {
                        inputDialog.Close();
                        return;
                    }
                    dialogResult = true;
                    inputDialog.Close(); 
                };
                cancelButton.Click += (s, args) => { inputDialog.Close(); };

                inputDialog.Loaded += (s, args) => 
                {
                    textBox.Focus();
                    textBox.SelectAll();
                };

                inputDialog.ShowDialog();

                if (dialogResult == true && !string.IsNullOrWhiteSpace(newVersionName))
                {
                    var newVersionPath = Path.Combine(root, "versions", newVersionName);
                    if (Directory.Exists(newVersionPath))
                    {
                        MessageBox.Show("目标版本名称已存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    try
                    {
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

                        RefreshVersionsList();
                        RefreshVersions();
                        MessageBox.Show($"版本重命名成功！\n\n从：{oldVersionName}\n到：{newVersionName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"重命名失败：{ex.Message}\n\n详细信息：{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 删除版本
        /// </summary>
        private void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string versionName)
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("游戏目录未设置！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var versionPath = Path.Combine(root, "versions", versionName);
                if (!Directory.Exists(versionPath))
                {
                    MessageBox.Show("版本文件夹不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要删除版本 \"{versionName}\" 吗？\n\n此操作不可恢复！",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.Delete(versionPath, true);
                        RefreshVersionsList();
                        RefreshVersions();
                        MessageBox.Show("版本已删除！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 导入版本
        /// </summary>
        private void ImportVersionButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "选择版本文件夹",
                Filter = "版本文件夹|*.*|所有文件|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var sourcePath = dialog.FileName;
                var root = GameRoot.Text?.Trim();
                
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("游戏目录未设置！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var versionsDir = Path.Combine(root, "versions");
                Directory.CreateDirectory(versionsDir);

                var sourceDir = Path.GetDirectoryName(sourcePath);
                var sourceFolderName = Path.GetFileName(sourcePath);

                if (string.IsNullOrEmpty(sourceFolderName))
                {
                    MessageBox.Show("无法确定源文件夹名称！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var destPath = Path.Combine(versionsDir, sourceFolderName);

                if (Directory.Exists(destPath))
                {
                    var result = MessageBox.Show(
                        $"目标版本 \"{sourceFolderName}\" 已存在，是否覆盖？",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    try
                    {
                        Directory.Delete(destPath, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除旧版本失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                try
                {
                    if (File.Exists(sourcePath))
                    {
                        var versionName = Path.GetFileNameWithoutExtension(sourcePath);
                        destPath = Path.Combine(versionsDir, versionName);
                        
                        if (File.Exists(destPath))
                        {
                            var result = MessageBox.Show(
                                $"目标版本 \"{versionName}\" 已存在，是否覆盖？",
                                "确认覆盖",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result != MessageBoxResult.Yes)
                            {
                                return;
                            }
                            
                            File.Delete(destPath);
                        }
                        
                        File.Copy(sourcePath, destPath, true);
                        MessageBox.Show($"版本文件导入成功：{versionName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        CopyDirectory(sourcePath, destPath);
                        MessageBox.Show($"版本文件夹导入成功：{sourceFolderName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("选择的路径不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    RefreshVersionsList();
                    RefreshVersions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 递归复制目录
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
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

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证内存设置
                if (!int.TryParse(MaxMemory.Text, out var maxMem))
                {
                    MessageBox.Show("内存设置必须是数字！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (maxMem < 512)
                {
                    MessageBox.Show("最大内存太小，建议至少 512MB。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (maxMem > 32768)
                {
                    MessageBox.Show("最大内存过大，请确认您的物理内存是否足够。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证昵称
                if (string.IsNullOrWhiteSpace(PlayerName.Text))
                {
                    MessageBox.Show("请输入离线昵称！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 保存到配置文件
                SaveSettingsToFile();

                MessageBox.Show("设置已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        private void SaveSettingsToFile()
        {
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var configPath = Path.Combine(appPath, "settings.txt");

                var config = new StringBuilder();
                config.AppendLine($"GameRoot={GameRoot.Text}");
                config.AppendLine($"MaxMemory={MaxMemory.Text}");
                config.AppendLine($"PlayerName={PlayerName.Text}");
                config.AppendLine($"FullScreen={FullScreen.IsChecked}");
                config.AppendLine($"IsOnlineMode={_isOnlineMode}");
                
                if (_isOnlineMode && _cachedTokenInfo != null)
                {
                    AppendLog($"保存Token - AccessToken: {(_cachedTokenInfo.AccessToken?.Length > 20 ? _cachedTokenInfo.AccessToken.Substring(0, 20) + "..." : "null")}, RefreshToken: {(_cachedTokenInfo.RefreshToken?.Length > 20 ? _cachedTokenInfo.RefreshToken.Substring(0, 20) + "..." : "null")}");
                    config.AppendLine($"AccessToken={_cachedTokenInfo.AccessToken}");
                    config.AppendLine($"RefreshToken={_cachedTokenInfo.RefreshToken}");
                    config.AppendLine($"PlayerNameOnline={_cachedPlayerName}");
                }

                File.WriteAllText(configPath, config.ToString());
                AppendLog("设置已保存到配置文件");
            }
            catch (Exception ex)
            {
                AppendLog($"保存配置文件失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载设置
        /// </summary>
        private void LoadSettingsFromFile()
        {
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var configPath = Path.Combine(appPath, "settings.txt");

                if (File.Exists(configPath))
                {
                    var config = File.ReadAllLines(configPath);
                    string? accessToken = null;
                    string? refreshToken = null;
                    string? playerNameOnline = null;
                    
                    foreach (var line in config)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            switch (key)
                            {
                                case "GameRoot":
                                    GameRoot.Text = value;
                                    CurrentGameRoot.Text = value;
                                    break;
                                case "MaxMemory":
                                    MaxMemory.Text = value;
                                    break;
                                case "PlayerName":
                                    PlayerName.Text = value;
                                    break;
                                case "FullScreen":
                                    if (bool.TryParse(value, out var fullScreen))
                                    {
                                        FullScreen.IsChecked = fullScreen;
                                    }
                                    break;
                                case "IsOnlineMode":
                                    if (bool.TryParse(value, out var isOnline))
                                    {
                                        _isOnlineMode = isOnline;
                                    }
                                    break;
                                case "AccessToken":
                                    accessToken = value;
                                    break;
                                case "RefreshToken":
                                    refreshToken = value;
                                    break;
                                case "PlayerNameOnline":
                                    playerNameOnline = value;
                                    break;
                            }
                        }
                    }
                    
                    if (_isOnlineMode && !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                    {
                        _cachedTokenInfo = new GetTokenResponse
                        {
                            AccessToken = accessToken,
                            RefreshToken = refreshToken
                        };
                        _cachedPlayerName = playerNameOnline;
                        AppendLog($"加载Token成功 - AccessToken长度: {accessToken.Length}, RefreshToken长度: {refreshToken.Length}");
                    }
                    else if (_isOnlineMode)
                    {
                        AppendLog($"警告: 在线模式但Token为空 - AccessToken: {(string.IsNullOrEmpty(accessToken) ? "空" : "有值")}, RefreshToken: {(string.IsNullOrEmpty(refreshToken) ? "空" : "有值")}");
                    }
                    
                    AppendLog("已从配置文件加载设置");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"加载配置文件失败：{ex.Message}");
            }
        }

        private async void StartGame_Click(object sender, RoutedEventArgs e)
        {
            StartGame.IsEnabled = false;
            try
            {
                LogBox.Clear();

                if (_isOnlineMode)
                {
                    if (_cachedTokenInfo == null)
                    {
                        MessageBox.Show("请先在账号管理中登录微软账号。");
                        return;
                    }
                }
                else
                {
                    var playerName = PlayerName.Text?.Trim();
                    if (string.IsNullOrWhiteSpace(playerName))
                    {
                        MessageBox.Show("请输入离线昵称。");
                        return;
                    }
                }

                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("请选择游戏目录（.minecraft）。");
                    return;
                }

                var version = GameVersion.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(version))
                {
                    MessageBox.Show("请选择要启动的版本。");
                    return;
                }

                var java = JavaPath.SelectedValue as string;
                if (string.IsNullOrWhiteSpace(java))
                {
                    MessageBox.Show("请选择 Java。");
                    return;
                }

                // 检查 Java 版本与游戏版本的兼容性
                var requiredJavaVersion = GetRequiredJavaVersion(version);
                var currentJavaVersion = GetJavaVersion(java);
                
                if (currentJavaVersion < requiredJavaVersion)
                {
                    var result = MessageBox.Show(
                        $"警告：Java 版本不兼容！\n\n" +
                        $"当前选择的 Java 版本：Java {currentJavaVersion}\n" +
                        $"游戏 {version} 需要的 Java 版本：Java {requiredJavaVersion} 或更高\n\n" +
                        $"是否继续启动？（可能会导致游戏无法运行）",
                        "Java 版本不兼容",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.No)
                    {
                        var javas = JavaUtil.GetJavas().ToList();
                        if (javas.Count > 0)
                        {
                            SelectCompatibleJava(javas, version);
                            MessageBox.Show("已自动为您切换到兼容的 Java 版本，请重新点击开始游戏。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("未找到其他可用的 Java 版本，请安装合适的 Java 后再试。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return;
                    }
                }

                if (!int.TryParse(MaxMemory.Text, out var maxMem))
                {
                    MessageBox.Show("内存设置必须是数字。");
                    return;
                }

                if (maxMem < 512)
                {
                    MessageBox.Show("最大内存太小，建议至少 512MB。");
                    return;
                }

                dynamic account;
                if (_isOnlineMode && _cachedTokenInfo != null)
                {
                    var msAuth = new MicrosoftAuthentication(MicrosoftClientId);
                    account = await msAuth.MicrosoftAuthAsync(_cachedTokenInfo, progress =>
                    {
                        Dispatcher.InvokeAsync(() => AppendLog(progress));
                    });
                }
                else
                {
                    var playerName = PlayerName.Text?.Trim() ?? "Player";
                    account = new OfflineAuthentication(playerName).OfflineAuth();
                }

                LaunchConfig args = new() // 配置启动参数
                {
                    Account = new()
                    {
                        BaseAccount = account
                    },
                    GameWindowConfig = new()
                    {
                        IsFullScreen = FullScreen.IsChecked == true
                    },
                    GameCoreConfig = new()
                    {
                        Root = root, // 游戏根目录 (绝对路径)
                        Version = version, // 启动的版本
                        IsVersionIsolation = true, //版本隔离
                    },
                    JavaConfig = new()
                    {
                        JavaPath = java,
                        MaxMemory = maxMem,
                        MinMemory = Math.Min(1024, Math.Max(256, maxMem / 4))
                    }
                };

                AppendLog($"开始启动：Root={root}, Version={version}");

                var launch = new MinecraftLauncher(args); // 实例化启动器

                LaunchResponse la;
                try
                {
                    // 包装 ReportProgress 以匹配 Action<ProgressReport> 委托
                    la = await launch.LaunchAsync(report => ReportProgress(report?.ToString() ?? "未知进度")); // 启动
                }
                catch (Exception ex)
                {
                    AppendLog("LaunchAsync 抛出异常：");
                    AppendLog(ex.ToString());
                    MessageBox.Show("启动过程发生异常：\n" + ex.Message);
                    return;
                }

                if (la.Status == Status.Succeeded)
                {
                    MessageBox.Show("启动成功!");
                }
                else
                {
                    MessageBox.Show("启动失败!" + la.Exception);
                }
            }
            finally
            {
                StartGame.IsEnabled = true;
            }
        }

        private void ReportProgress(string progress)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = progress;
                LogBox.AppendText("[启动] " + progress + Environment.NewLine);
                LogBox.ScrollToEnd();
                
                // 尝试解析进度百分比
                if (progress.Contains("%"))
                {
                    var parts = progress.Split('%');
                    if (parts.Length > 0)
                    {
                        var lastPart = parts[parts.Length - 1];
                        var numbers = lastPart.Where(char.IsDigit).ToArray();
                        if (numbers.Length > 0)
                        {
                            if (int.TryParse(new string(numbers), out var value))
                            {
                                DownloadProgress.Value = Math.Min(100, value);
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 版本类型选择改变
        /// </summary>
        private async void VersionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = (sender as System.Windows.Controls.ComboBox)?.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem == null) return;

            var content = selectedItem.Content.ToString();
            if (content == null) return;

            // 根据选择的类型加载版本列表
            if (content.Contains("测试版") || content.Contains("Snapshot"))
            {
                await LoadVersionListByTypeAsync("snapshot");
            }
            else
            {
                await LoadVersionListByTypeAsync("release");
            }
        }

        /// <summary>
        /// 刷新版本列表按钮点击
        /// </summary>
        private async void RefreshVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = VersionType.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem == null) return;

            var content = selectedItem.Content.ToString();
            if (content == null) return;

            InstallVersion.IsEnabled = false;
            RefreshVersionsButton.IsEnabled = false;
            
            try
            {
                if (content.Contains("测试版") || content.Contains("Snapshot"))
                {
                    await LoadVersionListByTypeAsync("snapshot");
                }
                else
                {
                    await LoadVersionListByTypeAsync("release");
                }
                
                MessageBox.Show("版本列表刷新成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                InstallVersion.IsEnabled = true;
                RefreshVersionsButton.IsEnabled = true;
            }
        }

        private async void InstallVanilla_Click(object sender, RoutedEventArgs e)
        {
            InstallVanillaButton.IsEnabled = false;
            try
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("请先选择游戏目录（.minecraft）。");
                    return;
                }

                // 从 ComboBox 获取选择的版本号
                var targetVersion = InstallVersion.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(targetVersion))
                {
                    MessageBox.Show("请选择要安装的版本。");
                    return;
                }

                AppendLog($"开始安装原版：{targetVersion} 到 {root}");
                AppendLog("使用 BMCLAPI 镜像源进行下载（国内高速）");

                using var cts = new CancellationTokenSource();

                try
                {
                    // 使用 BMCLAPI 镜像源安装
                    var installer = new BmclApiInstaller(targetVersion, root, 
                        (status, progress) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                DownloadProgressText.Text = $"{status} - {progress}";
                                DownloadProgressBar.Value = double.Parse(progress.TrimEnd('%'));
                            });
                            AppendLog($"[安装] {status} - {progress}");
                        },
                        (log) =>
                        {
                            AppendLog(log);
                        });

                    AppendLog("开始下载和安装...");
                    await installer.InstallAsync(cts.Token);

                    AppendLog("原版安装完成。");
                    AppendLog($"请检查目录：{Path.Combine(root, "versions", targetVersion)}");
                    RefreshVersions();
                    
                    MessageBox.Show($"版本 {targetVersion} 安装成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AppendLog("安装失败：");
                    AppendLog($"异常类型：{ex.GetType().Name}");
                    AppendLog($"错误信息：{ex.Message}");
                    if (ex.InnerException != null)
                    {
                        AppendLog($"内部异常：{ex.InnerException.Message}");
                    }
                    MessageBox.Show($"安装原版时发生异常：\n{ex.Message}\n\n请检查：\n1. 网络连接是否正常\n2. 版本号是否正确（如 1.21.1）\n3. 游戏目录是否有写入权限");
                    return;
                }
            }
            catch (Exception ex)
            {
                AppendLog("安装原版过程中发生异常：");
                AppendLog(ex.ToString());
                MessageBox.Show("安装原版时发生异常：\n" + ex.Message);
            }
            finally
            {
                InstallVanillaButton.IsEnabled = true;
            }
        }
    }
}
