using StarLight_Core.Authentication;
using StarLight_Core.Enum;
using StarLight_Core.Installer;
using StarLight_Core.Launch;
using StarLight_Core.Models.Authentication;
using StarLight_Core.Models.Launch;
using StarLight_Core.Utilities;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;

namespace MinecraftLuanch
{
    public class VersionInfo
    {
        public string VersionName { get; set; } = string.Empty;
        public string VersionPath { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        private string? _currentVersion;
        private List<string> _allVersions = new();
        
        private bool _isOnlineMode = false;
        private GetTokenResponse? _cachedTokenInfo;
        private string? _cachedPlayerName;
        private bool _isInitialized = false;
        
        private const string MicrosoftClientId = "e1e383f9-59d9-4aa2-bf5e-73fe83b15ba0";
        
        private List<string> _backgroundImages = new();
        private string _lastBackground = "";
        private Random _random = new();
        
        private Dictionary<string, int> _versionJavaRequirements = new()
        {
            { "1.21", 21 }, { "1.20.5", 21 }, { "1.20.6", 21 },
            { "1.20", 17 }, { "1.19", 17 }, { "1.18", 17 },
            { "1.17", 16 }, { "1.16", 8 }, { "1.15", 8 },
            { "1.14", 8 }, { "1.13", 8 }, { "1.12", 8 },
            { "1.11", 8 }, { "1.10", 8 }, { "1.9", 8 },
            { "1.8", 8 }, { "1.7", 7 }
        };

        private CancellationTokenSource? _installCts;
        
        private double _animationSpeed = 1.0;

        public MainWindow()
        {
            InitializeComponent();
            
            var defaultMinecraftPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft"
            );
            
            GameRoot.Text = defaultMinecraftPath;

            LoadBackgroundImages();
            SetRandomBackground();
            
            LoadSettingsFromFile();

            RefreshVersions();
            RefreshJava();
            
            VersionType.SelectedIndex = 0;
            _ = LoadVersionListAsync();
            _ = LoadAnnouncementAsync();
            
            _isInitialized = true;
            
            this.PreviewMouseDown += MainWindow_PreviewMouseDown;
            
            StartJellyAnimation();
            
            if (_isOnlineMode)
            {
                OfflineModeRadio.IsChecked = false;
                OnlineModeRadio.IsChecked = true;
                OfflineAccountPanel.Visibility = Visibility.Collapsed;
                OnlineAccountPanel.Visibility = Visibility.Visible;
            }
            
            UpdateAccountInfo();
            UpdateBackgroundCount();
        }

        private void LoadBackgroundImages()
        {
            _backgroundImages.Clear();
            var photosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photos");
            
            if (Directory.Exists(photosPath))
            {
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                var files = Directory.GetFiles(photosPath)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .OrderBy(f => f)
                    .ToList();
                
                _backgroundImages.AddRange(files);
            }
        }

        private void SetRandomBackground()
        {
            if (_backgroundImages.Count == 0)
            {
                LoadBackgroundImages();
            }
            
            if (_backgroundImages.Count == 0) return;
            
            var available = _backgroundImages.Where(b => b != _lastBackground).ToList();
            if (available.Count == 0) available = _backgroundImages;
            
            var selected = available[_random.Next(available.Count)];
            _lastBackground = selected;
            
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(selected);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                BackgroundImage.Source = bitmap;
            }
            catch { }
        }

        private void UpdateBackgroundCount()
        {
            BackgroundCountText.Text = $"当前背景数量：{_backgroundImages.Count} 张";
        }

        private void StartJellyAnimation()
        {
            var storyboard = (System.Windows.Media.Animation.Storyboard)FindResource("JellyAnimation");
            storyboard.Begin();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MainBorder.Margin = new Thickness(0);
            }
            else
            {
                WindowState = WindowState.Maximized;
                MainBorder.Margin = new Thickness(7);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void SwitchPage(string pageName, Button? clickedButton = null)
        {
            LaunchPage.Visibility = Visibility.Collapsed;
            VersionsPage.Visibility = Visibility.Collapsed;
            DownloadPage.Visibility = Visibility.Collapsed;
            AccountPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;
            MorePage.Visibility = Visibility.Collapsed;

            LaunchNavBtn.Style = (Style)FindResource("NavBarButton");
            VersionsNavBtn.Style = (Style)FindResource("NavBarButton");
            DownloadNavBtn.Style = (Style)FindResource("NavBarButton");
            AccountNavBtn.Style = (Style)FindResource("NavBarButton");
            SettingsNavBtn.Style = (Style)FindResource("NavBarButton");
            MoreNavBtn.Style = (Style)FindResource("NavBarButton");

            Button? activeBtn = null;
            Grid? activePage = null;
            
            switch (pageName)
            {
                case "Launch":
                    LaunchPage.Visibility = Visibility.Visible;
                    LaunchNavBtn.Style = (Style)FindResource("ActiveNavBarButton");
                    activeBtn = LaunchNavBtn;
                    activePage = LaunchPage;
                    break;
                case "Versions":
                    VersionsPage.Visibility = Visibility.Visible;
                    VersionsNavBtn.Style = (Style)FindResource("ActiveNavBarButton");
                    activeBtn = VersionsNavBtn;
                    activePage = VersionsPage;
                    RefreshVersionsList();
                    break;
                case "Download":
                    DownloadPage.Visibility = Visibility.Visible;
                    DownloadNavBtn.Style = (Style)FindResource("ActiveNavBarButton");
                    activeBtn = DownloadNavBtn;
                    activePage = DownloadPage;
                    break;
                case "Account":
                    AccountPage.Visibility = Visibility.Visible;
                    AccountNavBtn.Style = (Style)FindResource("ActiveNavBarButton");
                    activeBtn = AccountNavBtn;
                    activePage = AccountPage;
                    UpdateAccountInfo();
                    break;
                case "Settings":
                    SettingsPage.Visibility = Visibility.Visible;
                    SettingsNavBtn.Style = (Style)FindResource("ActiveNavBarButton");
                    activeBtn = SettingsNavBtn;
                    activePage = SettingsPage;
                    break;
                case "More":
                    MorePage.Visibility = Visibility.Visible;
                    MoreNavBtn.Style = (Style)FindResource("ActiveNavBarButton");
                    activeBtn = MoreNavBtn;
                    activePage = MorePage;
                    break;
            }
            
            if (activePage != null)
            {
                AnimatePageContent(activePage);
            }
            
            if (clickedButton != null)
            {
                PlayJellyAnimation(clickedButton);
            }
            else if (activeBtn != null)
            {
                PlayJellyAnimation(activeBtn);
            }
            
            SetRandomBackground();
        }

        private void PlayJellyAnimation(Button button)
        {
            var transform = new System.Windows.Media.ScaleTransform(1, 1);
            button.RenderTransform = transform;
            button.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            
            var scaleXAnim = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            var scaleYAnim = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            
            scaleXAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1, TimeSpan.Zero));
            scaleXAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.9, TimeSpan.FromSeconds(0.05 * _animationSpeed)));
            scaleXAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.05, TimeSpan.FromSeconds(0.12 * _animationSpeed)));
            scaleXAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1, TimeSpan.FromSeconds(0.2 * _animationSpeed)));
            
            scaleYAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1, TimeSpan.Zero));
            scaleYAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.1, TimeSpan.FromSeconds(0.05 * _animationSpeed)));
            scaleYAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.95, TimeSpan.FromSeconds(0.12 * _animationSpeed)));
            scaleYAnim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1, TimeSpan.FromSeconds(0.2 * _animationSpeed)));
            
            transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnim);
            transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        private void AnimatePageContent(Grid page)
        {
            var transform = new System.Windows.Media.TranslateTransform(0, -30);
            page.RenderTransform = transform;
            page.Opacity = 0;
            
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(-30, 0, TimeSpan.FromSeconds(0.3 * _animationSpeed));
            slideAnim.EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25 * _animationSpeed));
            
            transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);
            page.BeginAnimation(System.Windows.Controls.Grid.OpacityProperty, fadeAnim);
        }
        
        private void AnimationSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;
            
            _animationSpeed = e.NewValue;
            AnimationSpeedText.Text = $"{_animationSpeed:F1}x";
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e) => SwitchPage("Launch", sender as Button);
        private void VersionsButton_Click(object sender, RoutedEventArgs e) => SwitchPage("Versions", sender as Button);
        private void DownloadButton_Click(object sender, RoutedEventArgs e) => SwitchPage("Download", sender as Button);
        private void AccountButton_Click(object sender, RoutedEventArgs e) => SwitchPage("Account", sender as Button);
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => SwitchPage("Settings", sender as Button);
        private void MoreButton_Click(object sender, RoutedEventArgs e) => SwitchPage("More", sender as Button);

        private void VersionDropDown_Click(object sender, RoutedEventArgs e)
        {
            VersionPopup.IsOpen = !VersionPopup.IsOpen;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (VersionPopup.IsOpen)
            {
                var popupChild = VersionPopup.Child as FrameworkElement;
                if (popupChild != null)
                {
                    var pos = e.GetPosition(popupChild);
                    var bounds = new Rect(0, 0, popupChild.ActualWidth, popupChild.ActualHeight);
                    
                    var buttonBounds = VersionDropDown.TransformToAncestor(this)
                        .TransformBounds(new Rect(0, 0, VersionDropDown.ActualWidth, VersionDropDown.ActualHeight));
                    var mousePos = e.GetPosition(this);
                    
                    if (!bounds.Contains(pos) && !buttonBounds.Contains(mousePos))
                    {
                        VersionPopup.IsOpen = false;
                    }
                }
            }
        }

        private void ComboBoxBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.TemplatedParent is System.Windows.Controls.ComboBox comboBox)
            {
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            }
        }

        private void VersionSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var search = VersionSearchBox.Text?.ToLower() ?? "";
            var filtered = _allVersions.Where(v => v.ToLower().Contains(search)).ToList();
            VersionListBox.ItemsSource = filtered;
        }

        private void VersionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionListBox.SelectedItem is string version)
            {
                _currentVersion = version;
                SelectedVersionText.Text = $"当前版本: {version}";
                VersionPopup.IsOpen = false;
                RefreshJava();
            }
        }

        private async Task LoadAnnouncementAsync()
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await httpClient.GetAsync("https://jingoujiao.github.io/");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        AnnouncementText.Text = content.Trim();
                        return;
                    }
                }
            }
            catch { }
            
            ShowDefaultAnnouncement();
        }

        private void ShowDefaultAnnouncement()
        {
            AnnouncementText.Text = "🎉 欢迎使用 JCL 启动器！\n\n" +
                                   "✨ 支持离线模式和正版登录\n" +
                                   "🚀 使用 BMCLAPI 镜像源高速下载\n" +
                                   "🎨 现代化界面设计\n\n" +
                                   "祝你游戏愉快！";
        }

        private async Task LoadVersionListAsync()
        {
            try
            {
                var versions = await BmclApiInstaller.GetReleaseVersionsAsync();
                InstallVersion.ItemsSource = versions.Select(v => v.Id).ToList();
                
                if (InstallVersion.Items.Count > 0)
                {
                    InstallVersion.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private async void VersionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionType == null) return;
            
            var selected = VersionType.SelectedItem as ComboBoxItem;
            if (selected == null) return;
            
            var content = selected.Content?.ToString() ?? "";
            
            try
            {
                List<MinecraftVersionInfo> versions;
                
                if (content.Contains("测试版"))
                {
                    versions = await BmclApiInstaller.GetSnapshotVersionsAsync();
                }
                else
                {
                    versions = await BmclApiInstaller.GetReleaseVersionsAsync();
                }
                
                InstallVersion.ItemsSource = versions.Select(v => v.Id).ToList();
                if (InstallVersion.Items.Count > 0)
                {
                    InstallVersion.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private async void RefreshVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersionsBtn.IsEnabled = false;
            InstallVersion.IsEnabled = false;
            
            try
            {
                var selected = VersionType.SelectedItem as ComboBoxItem;
                var content = selected?.Content?.ToString() ?? "";
                
                List<MinecraftVersionInfo> versions;
                
                if (content.Contains("测试版"))
                {
                    versions = await BmclApiInstaller.GetSnapshotVersionsAsync();
                }
                else
                {
                    versions = await BmclApiInstaller.GetReleaseVersionsAsync();
                }
                
                InstallVersion.ItemsSource = versions.Select(v => v.Id).ToList();
                if (InstallVersion.Items.Count > 0)
                {
                    InstallVersion.SelectedIndex = 0;
                }
                
                MessageBox.Show("版本列表已刷新！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                RefreshVersionsBtn.IsEnabled = true;
                InstallVersion.IsEnabled = true;
            }
        }

        private void RefreshVersions()
        {
            var root = GameRoot.Text?.Trim();
            var versions = TryGetInstalledVersions(root);
            
            _allVersions = versions;
            VersionListBox.ItemsSource = versions;
            
            if (versions.Count > 0)
            {
                _currentVersion = versions[0];
                SelectedVersionText.Text = $"当前版本: {versions[0]}";
                RefreshJava();
            }
        }

        private void RefreshVersions_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersions();
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

        private void RefreshJava()
        {
            var javas = JavaUtil.GetJavas().ToList();
            var javaPaths = javas.Select(j => j.JavaPath).ToList();
            JavaPath.ItemsSource = javaPaths;
            
            if (javaPaths.Count > 0 && !string.IsNullOrWhiteSpace(_currentVersion))
            {
                SelectCompatibleJava(javas, _currentVersion);
            }
            else if (javaPaths.Count > 0)
            {
                JavaPath.SelectedIndex = 0;
            }
        }

        private void RefreshJava_Click(object sender, RoutedEventArgs e)
        {
            RefreshJava();
        }

        private void BrowseJava_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "选择 Java 可执行文件 (java.exe 或 javaw.exe)",
                Filter = "Java 可执行文件|java.exe;javaw.exe|所有文件|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                JavaPath.Text = dialog.FileName;
            }
        }

        private void SelectCompatibleJava(List<StarLight_Core.Models.Utilities.JavaInfo> javas, string version)
        {
            var requiredJavaVersion = GetRequiredJavaVersion(version);
            
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
            catch { }
            
            return 8;
        }

        private int GetRequiredJavaVersion(string minecraftVersion)
        {
            var parts = minecraftVersion.Split('.');
            if (parts.Length >= 2)
            {
                var majorVersion = $"{parts[0]}.{parts[1]}";
                if (_versionJavaRequirements.TryGetValue(majorVersion, out var version))
                {
                    return version;
                }
                
                if (int.TryParse(parts[0], out var firstPart))
                {
                    if (firstPart >= 21) return 21;
                    if (firstPart >= 20) return 21;
                    if (firstPart >= 17) return 17;
                    if (firstPart >= 16) return 16;
                    return 8;
                }
            }
            
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

        private void UpdateAccountInfo()
        {
            if (_isOnlineMode)
            {
                CurrentAccountInfo.Text = "模式：正版验证（微软账号）";
                CurrentPlayerName.Text = string.IsNullOrEmpty(_cachedPlayerName) 
                    ? "昵称：未登录" 
                    : $"昵称：{_cachedPlayerName}";
            }
            else
            {
                CurrentAccountInfo.Text = "模式：离线模式";
                var playerName = PlayerName.Text?.Trim();
                CurrentPlayerName.Text = string.IsNullOrWhiteSpace(playerName) 
                    ? "昵称：未设置" 
                    : $"昵称：{playerName}";
            }
        }

        private void OfflineModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _isOnlineMode = false;
            OfflineAccountPanel.Visibility = Visibility.Visible;
            OnlineAccountPanel.Visibility = Visibility.Collapsed;
            UpdateAccountInfo();
        }

        private void OnlineModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _isOnlineMode = true;
            OfflineAccountPanel.Visibility = Visibility.Collapsed;
            OnlineAccountPanel.Visibility = Visibility.Visible;
            UpdateAccountInfo();
        }

        private async void MicrosoftLoginButton_Click(object sender, RoutedEventArgs e)
        {
            MicrosoftLoginBtn.IsEnabled = false;
            LoginStatusText.Text = "正在启动登录流程...";
            
            try
            {
                var auth = new MicrosoftAuthentication(MicrosoftClientId);
                var deviceCodeInfo = await auth.RetrieveDeviceCodeInfo();
                
                LoginStatusText.Text = $"验证代码: {deviceCodeInfo.UserCode}\n请在浏览器中完成登录";
                
                Clipboard.SetText(deviceCodeInfo.UserCode);
                
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = deviceCodeInfo.VerificationUri,
                        UseShellExecute = true
                    });
                }
                catch { }
                
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
                    
                    LoginStatusText.Text = $"登录成功！\n玩家：{userInfo.Name}";
                    UpdateAccountInfo();
                    SaveSettingsToFile();
                    
                    MessageBox.Show($"微软账号登录成功！\n\n玩家名称：{userInfo.Name}", 
                        "登录成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LoginStatusText.Text = "登录失败：未获取到有效的访问令牌";
                }
            }
            catch (Exception ex)
            {
                LoginStatusText.Text = $"登录失败：{ex.Message}";
            }
            finally
            {
                MicrosoftLoginBtn.IsEnabled = true;
            }
        }

        private void RefreshVersionsList()
        {
            var root = GameRoot.Text?.Trim();
            var versions = GetInstalledVersions(root);
            VersionsList.ItemsSource = versions;
        }

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

        private void RefreshVersionsListButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersionsList();
            RefreshVersions();
            MessageBox.Show("版本列表已刷新！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenVersionFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string versionName)
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root)) return;

                var versionPath = Path.Combine(root, "versions", versionName);
                if (Directory.Exists(versionPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{versionPath}\"",
                        UseShellExecute = true
                    });
                }
            }
        }

        private void RenameVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string oldVersionName)
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root)) return;

                var oldVersionPath = Path.Combine(root, "versions", oldVersionName);
                if (!Directory.Exists(oldVersionPath)) return;

                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    "请输入新的版本名称：", "重命名版本", oldVersionName);
                
                if (string.IsNullOrWhiteSpace(input) || input == oldVersionName) return;

                var newVersionPath = Path.Combine(root, "versions", input);
                if (Directory.Exists(newVersionPath))
                {
                    MessageBox.Show("目标版本名称已存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    Directory.Move(oldVersionPath, newVersionPath);
                    
                    var oldJsonPath = Path.Combine(newVersionPath, $"{oldVersionName}.json");
                    var newJsonPath = Path.Combine(newVersionPath, $"{input}.json");
                    if (File.Exists(oldJsonPath)) File.Move(oldJsonPath, newJsonPath);

                    var oldJarPath = Path.Combine(newVersionPath, $"{oldVersionName}.jar");
                    var newJarPath = Path.Combine(newVersionPath, $"{input}.jar");
                    if (File.Exists(oldJarPath)) File.Move(oldJarPath, newJarPath);

                    RefreshVersionsList();
                    RefreshVersions();
                    MessageBox.Show($"版本重命名成功！\n\n从：{oldVersionName}\n到：{input}", 
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"重命名失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string versionName)
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root)) return;

                var versionPath = Path.Combine(root, "versions", versionName);
                if (!Directory.Exists(versionPath)) return;

                var result = MessageBox.Show(
                    $"确定要删除版本 \"{versionName}\" 吗？\n\n此操作不可恢复！",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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
                
                if (string.IsNullOrWhiteSpace(root)) return;

                var versionsDir = Path.Combine(root, "versions");
                Directory.CreateDirectory(versionsDir);

                var sourceFolderName = Path.GetFileNameWithoutExtension(sourcePath);
                if (string.IsNullOrEmpty(sourceFolderName)) return;

                var destPath = Path.Combine(versionsDir, sourceFolderName);

                if (Directory.Exists(destPath))
                {
                    var result = MessageBox.Show(
                        $"目标版本 \"{sourceFolderName}\" 已存在，是否覆盖？",
                        "确认覆盖", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes) return;
                    
                    try { Directory.Delete(destPath, true); }
                    catch { return; }
                }

                try
                {
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        CopyDirectory(sourcePath, destPath);
                    }

                    RefreshVersionsList();
                    RefreshVersions();
                    MessageBox.Show($"版本导入成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

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

        private void AddBackground_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "选择背景图片",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var photosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photos");
                Directory.CreateDirectory(photosPath);
                
                int count = _backgroundImages.Count + 1;
                
                foreach (var file in dialog.FileNames)
                {
                    try
                    {
                        var destFile = Path.Combine(photosPath, $"bg{count}.jpg");
                        File.Copy(file, destFile, true);
                        count++;
                    }
                    catch { }
                }
                
                LoadBackgroundImages();
                UpdateBackgroundCount();
                SetRandomBackground();
                
                MessageBox.Show($"已添加 {dialog.FileNames.Length} 张背景图片！", 
                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearBackgrounds_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清空所有背景图片吗？\n\n此操作不可恢复！",
                "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var photosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photos");
                
                try
                {
                    if (Directory.Exists(photosPath))
                    {
                        foreach (var file in Directory.GetFiles(photosPath))
                        {
                            File.Delete(file);
                        }
                    }
                    
                    _backgroundImages.Clear();
                    UpdateBackgroundCount();
                    
                    MessageBox.Show("背景图片已清空！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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

                SaveSettingsToFile();
                RefreshVersions();
                RefreshJava();

                MessageBox.Show("设置已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                config.AppendLine($"AnimationSpeed={_animationSpeed}");
                config.AppendLine($"IsOnlineMode={_isOnlineMode}");
                
                if (_isOnlineMode && _cachedTokenInfo != null)
                {
                    config.AppendLine($"AccessToken={_cachedTokenInfo.AccessToken}");
                    config.AppendLine($"RefreshToken={_cachedTokenInfo.RefreshToken}");
                    config.AppendLine($"PlayerNameOnline={_cachedPlayerName}");
                }

                File.WriteAllText(configPath, config.ToString());
            }
            catch { }
        }

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
                                case "AnimationSpeed":
                                    if (double.TryParse(value, out var animSpeed))
                                    {
                                        _animationSpeed = animSpeed;
                                        AnimationSpeedSlider.Value = animSpeed;
                                        AnimationSpeedText.Text = $"{animSpeed:F1}x";
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
                    }
                }
            }
            catch { }
        }

        private async void StartGame_Click(object sender, RoutedEventArgs e)
        {
            StartGame.IsEnabled = false;
            
            try
            {
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

                var version = _currentVersion;
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

                var requiredJavaVersion = GetRequiredJavaVersion(version);
                var currentJavaVersion = GetJavaVersion(java);
                
                if (currentJavaVersion < requiredJavaVersion)
                {
                    var result = MessageBox.Show(
                        $"警告：Java 版本不兼容！\n\n" +
                        $"当前选择的 Java 版本：Java {currentJavaVersion}\n" +
                        $"游戏 {version} 需要的 Java 版本：Java {requiredJavaVersion} 或更高\n\n" +
                        $"是否继续启动？",
                        "Java 版本不兼容",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.No)
                    {
                        var javas = JavaUtil.GetJavas().ToList();
                        if (javas.Count > 0)
                        {
                            SelectCompatibleJava(javas, version);
                            MessageBox.Show("已自动为您切换到兼容的 Java 版本，请重新点击开始游戏。", 
                                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    try
                    {
                        var msAuth = new MicrosoftAuthentication(MicrosoftClientId);
                        account = await msAuth.MicrosoftAuthAsync(_cachedTokenInfo, progress =>
                        {
                            Dispatcher.InvokeAsync(() => 
                            {
                                LaunchStatusText.Text = progress;
                                LaunchStatusText.Visibility = Visibility.Visible;
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        var result = MessageBox.Show(
                            $"微软账号认证失败：{ex.Message}\n\n是否切换到离线模式继续游戏？",
                            "认证失败",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Error);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            _isOnlineMode = false;
                            OfflineModeRadio.IsChecked = true;
                            OnlineModeRadio.IsChecked = false;
                            OfflineAccountPanel.Visibility = Visibility.Visible;
                            OnlineAccountPanel.Visibility = Visibility.Collapsed;
                            
                            var playerName = PlayerName.Text?.Trim() ?? "Player";
                            account = new OfflineAuthentication(playerName).OfflineAuth();
                        }
                        else
                        {
                            StartGame.IsEnabled = true;
                            return;
                        }
                    }
                }
                else
                {
                    var playerName = PlayerName.Text?.Trim() ?? "Player";
                    account = new OfflineAuthentication(playerName).OfflineAuth();
                }

                LaunchProgress.Visibility = Visibility.Visible;
                LaunchStatusText.Visibility = Visibility.Visible;

                LaunchConfig args = new()
                {
                    Account = new() { BaseAccount = account },
                    GameWindowConfig = new() { IsFullScreen = FullScreen.IsChecked == true },
                    GameCoreConfig = new()
                    {
                        Root = root,
                        Version = version,
                        IsVersionIsolation = true,
                    },
                    JavaConfig = new()
                    {
                        JavaPath = java,
                        MaxMemory = maxMem,
                        MinMemory = Math.Min(1024, Math.Max(256, maxMem / 4))
                    }
                };

                var launch = new MinecraftLauncher(args);

                LaunchResponse la;
                try
                {
                    la = await launch.LaunchAsync(report => 
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LaunchStatusText.Text = report?.ToString() ?? "未知进度";
                            
                            var progressStr = report?.ToString() ?? "";
                            if (progressStr.Contains("%"))
                            {
                                var parts = progressStr.Split('%');
                                if (parts.Length > 0)
                                {
                                    var numbers = parts[0].Where(char.IsDigit).ToArray();
                                    if (numbers.Length > 0 && int.TryParse(new string(numbers), out var value))
                                    {
                                        LaunchProgress.Value = Math.Min(100, value);
                                    }
                                }
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("启动过程发生异常：\n" + ex.Message);
                    return;
                }

                if (la.Status == Status.Succeeded)
                {
                    LaunchProgress.Value = 100;
                    LaunchStatusText.Text = "启动成功！";
                }
                else
                {
                    LaunchStatusText.Text = "启动失败";
                    MessageBox.Show("启动失败!" + la.Exception);
                }
            }
            finally
            {
                StartGame.IsEnabled = true;
            }
        }

        private async void InstallVanilla_Click(object sender, RoutedEventArgs e)
        {
            InstallVanillaBtn.IsEnabled = false;
            CancelDownloadBtn.Visibility = Visibility.Visible;
            
            try
            {
                var root = GameRoot.Text?.Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    MessageBox.Show("请先选择游戏目录（.minecraft）。");
                    return;
                }

                var targetVersion = InstallVersion.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(targetVersion))
                {
                    MessageBox.Show("请选择要安装的版本。");
                    return;
                }

                _installCts = new CancellationTokenSource();

                var installer = new BmclApiInstaller(targetVersion, root, 
                    (status, progress) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DownloadProgressText.Text = status;
                            var progressValue = double.Parse(progress.TrimEnd('%'));
                            DownloadProgressBar.Value = progressValue;
                            DownloadPercentText.Text = progress;
                        });
                    });

                await installer.InstallAsync(_installCts.Token);

                RefreshVersions();
                
                MessageBox.Show($"版本 {targetVersion} 安装成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("下载已取消", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装失败：\n{ex.Message}");
            }
            finally
            {
                InstallVanillaBtn.IsEnabled = true;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;
                _installCts?.Dispose();
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_installCts != null && !_installCts.IsCancellationRequested)
            {
                _installCts.Cancel();
            }
        }
    }
}
