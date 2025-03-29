using AniListNet;
using AniListNet.Objects;
using AniListNet.Parameters;
using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AnilistRPC
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance = null;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            PlatformSpecific();

            if (!Design.IsDesignMode)
            {
                CheckForUpdates();
                Master.CatchupWindow();
            }
        }

        public void OnAuthenticated()
        {
            GetWatchingResults();
        }

        public void PlatformSpecific()
        {
            if (OperatingSystem.IsLinux())
            {
                MainGrid.RowDefinitions[0].Height = new GridLength(0);
                MenuBar.IsVisible = false;

                // Possibly temporary? Avalonia doesn't support tray icons on Linux yet
                SaveWrapper.SetMinimizeTraySetting(false);
            }
        }

        public async void CheckForUpdates()
        {
            if (!SaveWrapper.GetUpdatePopupPreference())
                return;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "C# Application"); // Need this or else Github won't accept our request
            try
            {
                var response = await client.GetStringAsync("https://api.github.com/repos/AITYunivers/AnilistRPC/releases/latest");

                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                Version latestVer = Version.Parse(root.GetProperty("tag_name").GetString()!);

                if (latestVer > Program.Version)
                {
                    string url = root.GetProperty("html_url").GetString()!;
                    string ver = latestVer.ToString();
                    string[] body = root.GetProperty("body").GetString()!.Split("\r\n");
                    List<string> changes = [];
                    bool isChanges = false;
                    for (int i = 0; i < body.Length; i++)
                    {
                        if (!isChanges && body[i].StartsWith("###"))
                            isChanges = true;
                        else if (isChanges && body[i].StartsWith("#"))
                            break;
                        else if (isChanges)
                            changes.Add(body[i].Replace("\\", ""));
                    }

                    UpdateNotifier updateWindow = new UpdateNotifier();
                    updateWindow.SetData(url, ver, changes.ToArray());
                    await updateWindow.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching release: {ex.Message}");
            }
        }

        Task? _currentSearchTask;
        private async void SearchChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    SearchResults.Results.Clear();
                    GetWatchingResults();
                    return;
                }

                Task<AniPagination<Media>>? CurrentSearchTask = Master.Anilist.SearchMediaAsync(new SearchMediaFilter()
                {
                    Query = textBox.Text,
                    Type = MediaType.Anime
                });
                _currentSearchTask = CurrentSearchTask;
                await CurrentSearchTask;
                if (CurrentSearchTask == _currentSearchTask)
                    SearchResults.SetSearchResults(CurrentSearchTask.Result);
            }
        }

        private async void GetWatchingResults()
        {
            if (Master.AuthenticationUser != null)
            {
                Task<AniPagination<MediaEntry>>? CurrentWatchingTask =
                    Master.Anilist.GetUserEntriesAsync(Master.AuthenticationUser.Id,
                    new MediaEntryFilter()
                    {
                        Status = MediaEntryStatus.Current,
                        Type = MediaType.Anime
                    });
                _currentSearchTask = CurrentWatchingTask;
                await CurrentWatchingTask;
                if (CurrentWatchingTask == _currentSearchTask)
                    SearchResults.SetWatchingResults(CurrentWatchingTask.Result);
            }
        }

        private void OpenSettings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog(this);
        }

        private void WindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Window.WindowStateProperty && SaveWrapper.GetMinimizeTraySetting())
            {
                if (WindowState == WindowState.Minimized)
                    Hide();

                TrayIcons? iconsList = TrayIcon.GetIcons(Application.Current!);
                if (iconsList != null && iconsList.Count > 0)
                {
                    TrayIcon icon = iconsList[0];
                    icon.IsVisible = WindowState == WindowState.Minimized;
                }
            }
        }

        private void WindowClosed(object? sender, System.EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}