using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

namespace AnilistRPC
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (Program.CheckLaunchArgument("--tray"))
                {
                    TrayIcons? iconsList = TrayIcon.GetIcons(this);
                    if (iconsList != null && iconsList.Count > 0)
                    {
                        TrayIcon icon = iconsList[0];
                        icon.IsVisible = true;
                    }
                }
                else
                    desktop.MainWindow = new MainWindow();
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Master.Startup();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private DateTime? trayIconDebounce;

        private void TrayIconClicked(object? sender, System.EventArgs e)
        {
            if (trayIconDebounce.HasValue && DateTime.UtcNow.TimeOfDay.TotalMilliseconds - trayIconDebounce.Value.TimeOfDay.TotalMilliseconds < 500)
                return;
            trayIconDebounce = DateTime.UtcNow;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = WindowState.Normal;
                    desktop.MainWindow.Show();
                }
                else
                {
                    desktop.MainWindow = new MainWindow();
                    desktop.MainWindow.Show();
                }
            }
        }
    }
}