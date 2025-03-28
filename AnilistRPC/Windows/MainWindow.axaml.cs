using AniListNet;
using AniListNet.Objects;
using AniListNet.Parameters;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using DiscordRPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RPCButton = DiscordRPC.Button;
using User = AniListNet.Objects.User;

namespace AnilistRPC
{
    public partial class MainWindow : Window
    {
        public static AniClient Anilist = new AniClient();
        public static MainWindow Instance = null!;
        public static DiscordRpcClient DiscordRPC = new DiscordRpcClient("1353904975121747978"); // Default "Watching Anime" App
        public static RichPresence? RPCData;
        public static User? AuthenticationUser;
        private DispatcherTimer? _refreshTimer;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            PlatformSpecific();

            if (Design.IsDesignMode)
                return;

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(20);
            _refreshTimer.Tick += RefreshRPC;
            _refreshTimer.Start();

            CheckForUpdates();
            LoadAsync();
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

        public async void LoadAsync()
        {
            await CheckAuthentication();
            await LoadCurrentMedia();
            await GetWatchingResults();
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

        public async Task CheckAuthentication()
        {
            AuthenticationData? authData = SaveWrapper.ReadAuthenticationData();
            if (authData != null && await TryAuthenticate(authData))
            {
                if (DateTime.UtcNow >= authData.Expiry)
                    SaveWrapper.ClearAuthenticationData();

                AuthenticationUser = await Anilist.GetAuthenticatedUserAsync();

                if (AnimeSelected.CurrentMedia != null)
                {
                    MediaEntry? entry = await Anilist.GetMediaEntryAsync(AnimeSelected.CurrentMedia.Id);
                    if (entry != null)
                        AnimeSelected.UpdateEpisode(entry.Progress + 1);
                }
            }
        }

        public async Task LoadCurrentMedia()
        {
            Task<Media>? mediaTask = SaveWrapper.GetCurrentMedia(out int episode);
            if (mediaTask == null)
                return;

            Media media = await mediaTask;
            AnimeSelected.SetMedia(media, episode);

            if (AuthenticationUser != null)
            {
                MediaEntry? entry = await Anilist.GetMediaEntryAsync(media.Id);
                if (entry != null)
                    AnimeSelected.UpdateEpisode(entry.Progress + 1);
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
                    await GetWatchingResults();
                    return;
                }

                Task<AniPagination<Media>>? CurrentSearchTask = Anilist.SearchMediaAsync(new SearchMediaFilter()
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

        private async Task GetWatchingResults()
        {
            if (AuthenticationUser != null)
            {
                Task<AniPagination<MediaEntry>>? CurrentWatchingTask =
                    Anilist.GetUserEntriesAsync(AuthenticationUser.Id,
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

        public static void UpdateRichPresence(Media? media, int episode = 1)
        {
            if (media == null)
            {
                DiscordRPC.Deinitialize();
                return;
            }

            if (!DiscordRPC.IsInitialized)
                DiscordRPC.Initialize();

            RPCData = new RichPresence();
            RPCData.Details = media.Title.EnglishTitle ?? media.Title.PreferredTitle ?? "Unknown Anime";
            RPCData.State = "Episode " + episode + (media.Episodes != null ? "/" + media.Episodes : "");
            RPCData.Assets = new Assets()
            {
                LargeImageKey = media.Cover.LargeImageUrl.AbsoluteUri,
                LargeImageText = media.Title.EnglishTitle ?? media.Title.PreferredTitle ?? string.Empty,
            };
            RPCData.Buttons = 
            [
                new RPCButton()
                {
                    Label = media.Type == MediaType.Manga ? "View Manga" : "View Anime",
                    Url = media.Url.AbsoluteUri
                }
            ];
            if (AuthenticationUser != null)
            {
                RPCData.Buttons =
                [
                    RPCData.Buttons[0],
                    new RPCButton()
                    {
                        Label = $"View My {(media.Type == MediaType.Manga ? "Manga" : "Anime")} List",
                        Url = $"https://anilist.co/user/{AuthenticationUser.Name}/{(media.Type == MediaType.Manga ? "manga" : "anime")}list"
                    }
                ];
            }
            RPCData.Type = ActivityType.Watching;

            DiscordRPC.SetPresence(RPCData);
        }

        private void RefreshRPC(object? sender, EventArgs e)
        {
            if (RPCData == null)
                return;

            DiscordRPC.SetPresence(RPCData);
        }

        public static async Task<bool> TryAuthenticate(AuthenticationData authData)
        {
            return await Anilist.TryAuthenticateAsync(authData.AccessToken);
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