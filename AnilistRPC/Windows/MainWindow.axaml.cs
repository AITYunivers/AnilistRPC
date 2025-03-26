using AniListNet;
using AniListNet.Objects;
using AniListNet.Parameters;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using DiscordRPC;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(20);
            _refreshTimer.Tick += RefreshRPC;
            _refreshTimer.Start();

            LoadAsync();
        }

        public async void LoadAsync()
        {
            await CheckAuthentication();
            await LoadCurrentMedia();
            await GetWatchingResults();
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
    }
}