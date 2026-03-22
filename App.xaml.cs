using System;
using System.Configuration;
using System.Data;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MinecraftLuanch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => 
            {
                var ex = args.ExceptionObject as Exception;
                var innerEx = ex?.InnerException;
                var message = $"未处理的异常：{ex?.Message}";
                if (innerEx != null)
                {
                    message += $"\n\n内部异常：{innerEx.Message}\n{innerEx.StackTrace}";
                }
                else
                {
                    message += $"\n\n{ex?.StackTrace}";
                }
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) => 
            {
                var ex = args.Exception;
                var innerEx = ex?.InnerException;
                var message = $"UI 线程异常：{ex?.Message}";
                if (innerEx != null)
                {
                    message += $"\n\n内部异常：{innerEx.Message}\n\n{innerEx.StackTrace}";
                }
                else
                {
                    message += $"\n\n{ex?.StackTrace}";
                }
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
