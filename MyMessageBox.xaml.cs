using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace MinecraftLuanch
{
    public partial class MyMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        public MyMessageBox()
        {
            InitializeComponent();
            Loaded += MyMessageBox_Loaded;
        }

        private void MyMessageBox_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = (Storyboard)FindResource("FadeInAnimation");
            fadeIn.Begin();
        }

        public static MessageBoxResult Show(string message, string caption = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            var msgBox = new MyMessageBox
            {
                Title = caption
            };

            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow != msgBox)
            {
                msgBox.Owner = mainWindow;
            }

            msgBox.TitleText.Text = caption;
            msgBox.MessageText.Text = message;
            msgBox.SetIcon(icon);
            msgBox.CreateButtons(buttons);

            msgBox.ShowDialog();
            return msgBox._result;
        }

        private void SetIcon(MessageBoxImage icon)
        {
            string iconText = "";

            switch (icon)
            {
                case MessageBoxImage.Information:
                    iconText = "ℹ️";
                    break;
                case MessageBoxImage.Question:
                    iconText = "❓";
                    break;
                case MessageBoxImage.Warning:
                    iconText = "⚠️";
                    break;
                case MessageBoxImage.Error:
                    iconText = "❌";
                    break;
                case MessageBoxImage.None:
                default:
                    iconText = "💬";
                    break;
            }

            IconText.Text = iconText;
        }

        private void CreateButtons(MessageBoxButton buttons)
        {
            ButtonPanel.Children.Clear();

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton("确定", MessageBoxResult.OK, "MsgButton");
                    break;

                case MessageBoxButton.OKCancel:
                    AddButton("取消", MessageBoxResult.Cancel, "SecondaryMsgButton");
                    AddButton("确定", MessageBoxResult.OK, "MsgButton");
                    break;

                case MessageBoxButton.YesNo:
                    AddButton("否", MessageBoxResult.No, "SecondaryMsgButton");
                    AddButton("是", MessageBoxResult.Yes, "MsgButton");
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddButton("取消", MessageBoxResult.Cancel, "SecondaryMsgButton");
                    AddButton("否", MessageBoxResult.No, "SecondaryMsgButton");
                    AddButton("是", MessageBoxResult.Yes, "MsgButton");
                    break;
            }
        }

        private void AddButton(string content, MessageBoxResult result, string styleKey)
        {
            var button = new System.Windows.Controls.Button
            {
                Content = content,
                Style = (Style)FindResource(styleKey),
                Margin = new Thickness(8, 0, 0, 0)
            };

            button.Click += (s, e) =>
            {
                _result = result;
                CloseWithAnimation();
            };

            ButtonPanel.Children.Add(button);
        }

        private void CloseWithAnimation()
        {
            var fadeOut = new Storyboard();
            
            var opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = System.TimeSpan.FromSeconds(0.15),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            
            var scaleXAnim = new DoubleAnimation
            {
                From = 1,
                To = 0.95,
                Duration = System.TimeSpan.FromSeconds(0.15),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            
            var scaleYAnim = new DoubleAnimation
            {
                From = 1,
                To = 0.95,
                Duration = System.TimeSpan.FromSeconds(0.15),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard.SetTarget(opacityAnim, MainBorder);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));
            
            Storyboard.SetTarget(scaleXAnim, MainBorder);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            
            Storyboard.SetTarget(scaleYAnim, MainBorder);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            fadeOut.Children.Add(opacityAnim);
            fadeOut.Children.Add(scaleXAnim);
            fadeOut.Children.Add(scaleYAnim);

            fadeOut.Completed += (s, e) => Close();
            fadeOut.Begin();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var primaryButton = ButtonPanel.Children[ButtonPanel.Children.Count - 1] as System.Windows.Controls.Button;
                if (primaryButton != null)
                {
                    primaryButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                var cancelButton = ButtonPanel.Children[0] as System.Windows.Controls.Button;
                if (cancelButton != null)
                {
                    cancelButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }
            }
        }
    }
}