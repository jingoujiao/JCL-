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
            // 全局异常捕获
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => 
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"未处理的异常：{ex?.Message}\n\n{ex?.StackTrace}", "错误");
            };

            DispatcherUnhandledException += (sender, args) => 
            {
                MessageBox.Show($"UI 线程异常：{args.Exception.Message}", "错误");
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
