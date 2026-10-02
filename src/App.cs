using System;
using System.Windows;
using JiYuKiller.Core;
using JiYuKiller.UI;

namespace JiYuKiller
{
    internal static class App
    {
        [STAThread]
        private static void Main()
        {
            if (!AppRuntime.AcquireSingleInstance())
            {
                MessageBox.Show("JiYu Killer 已经在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                AppRuntime.WriteCrashReport(e.ExceptionObject as Exception);
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        ex.ToString());
                }
                catch { }
                throw;
            }
        }

        private static void Run()
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose,
            };
            app.DispatcherUnhandledException += (s, e) =>
            {
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        e.Exception.ToString());
                }
                catch { }
                MessageBox.Show(e.Exception.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            // "重要电脑"标记检查
            if (PromiseMark.IsMarked())
            {
                MessageBox.Show(
                    "本软件无法在此电脑上运行。此电脑被标记为重要电脑，不允许整蛊。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 先构造主窗口并挂到 app.MainWindow：
            // 否则免责声明窗口关闭时 MainWindow 为 null, OnMainWindowClose 会把整个应用关停。
            var window = new MainWindow();
            app.MainWindow = window;

            // 免责声明
            var disclaimer = new DisclaimerWindow();
            var agreed = disclaimer.ShowDialog();
            if (agreed != true)
            {
                if (disclaimer.MarkImportantComputer)
                {
                    // 标记写入是尽力而为: 目录只读等场景写不进也不应崩溃
                    try { PromiseMark.Write(); }
                    catch { }
                }
                return;
            }

            app.Run(window);
        }
    }
}
