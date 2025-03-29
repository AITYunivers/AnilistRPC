using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;

namespace AnilistRPC;

public partial class UpdateNotifier : Window
{
    private string URL = string.Empty;

    public UpdateNotifier()
    {
        InitializeComponent();
        PlatformSpecific();
    }

    public void PlatformSpecific()
    {
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = new GridLength(0);
            MenuBar.IsVisible = false;
        }
    }

    public void SetData(string url, string version, string[] changes)
    {
        URL = url;
        Version.Text = version;
        foreach (string change in changes)
            Changelog.Children.Add(new TextBlock()
            {
                Text = change,
            });
    }

    private void DontShowAgain(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveWrapper.SetUpdatePopupPreference(false);
        Close();
    }

    private void OpenUpdateURL(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = URL,
            UseShellExecute = true
        });
        Environment.Exit(0);
    }

    private void MaybeLater(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}