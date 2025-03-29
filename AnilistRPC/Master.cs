using AniListNet;
using AniListNet.Objects;
using AniListNet.Parameters;
using AsyncImageLoader;
using Avalonia.Media;
using Avalonia;
using Avalonia.Threading;
using DiscordRPC;
using Projektanker.Icons.Avalonia;
using System;
using System.Threading.Tasks;
using RPCButton = DiscordRPC.Button;
using User = AniListNet.Objects.User;

namespace AnilistRPC
{
    public static class Master
    {
        public static AniClient Anilist = new AniClient();
        public static DiscordRpcClient DiscordRPC = new DiscordRpcClient("1353904975121747978"); // Default "Watching Anime" App
        public static RichPresence? RPCData;
        public static User? AuthenticationUser;
        public static Media? CurrentMedia;
        private static DispatcherTimer? _refreshTimer;
        private static DispatcherTimer? _saveDataTimer;
        private static int _curEpisode;
        private static int _oldEpisode;

        public static void Startup()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(20);
            _refreshTimer.Tick += RefreshRPC;
            _refreshTimer.Start();

            _saveDataTimer = new DispatcherTimer();
            _saveDataTimer.Interval = TimeSpan.FromSeconds(5);
            _saveDataTimer.Tick += SaveProgress;
            _saveDataTimer.Start();

            LoadAsync();
        }

        public static async void LoadAsync()
        {
            await CheckAuthentication();
            await LoadCurrentMedia();
        }

        public static async Task CheckAuthentication()
        {
            AuthenticationData? authData = SaveWrapper.ReadAuthenticationData();
            if (authData != null && await TryAuthenticate(authData))
            {
                if (DateTime.UtcNow >= authData.Expiry)
                    SaveWrapper.ClearAuthenticationData();

                AuthenticationUser = await Anilist.GetAuthenticatedUserAsync();
                MainWindow.Instance?.OnAuthenticated();

                if (CurrentMedia != null)
                {
                    MediaEntry? entry = await Anilist.GetMediaEntryAsync(CurrentMedia.Id);
                    if (entry != null)
                        MainWindow.Instance?.AnimeSelected.UpdateEpisode(entry.Progress + 1);
                }
            }
        }

        public static async Task LoadCurrentMedia()
        {
            Task<Media>? mediaTask = SaveWrapper.GetCurrentMedia(out int episode);
            if (mediaTask == null)
                return;

            Media media = await mediaTask;
            SetMedia(media, episode);

            if (AuthenticationUser != null)
            {
                MediaEntry? entry = await Anilist.GetMediaEntryAsync(media.Id);
                if (entry != null)
                    UpdateEpisode(entry.Progress + 1);
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

        private static void RefreshRPC(object? sender, EventArgs e)
        {
            if (RPCData == null)
                return;

            DiscordRPC.SetPresence(RPCData);
        }

        public static async Task<bool> TryAuthenticate(AuthenticationData authData)
        {
            return await Anilist.TryAuthenticateAsync(authData.AccessToken);
        }

        private static void SaveProgress(object? sender, EventArgs e)
        {
            if (CurrentMedia != null && _curEpisode != _oldEpisode)
            {
                _curEpisode = Math.Clamp(_curEpisode, 1, CurrentMedia.Episodes ?? int.MaxValue);
                SaveWrapper.SetMediaProgress(CurrentMedia, _curEpisode - 1, true);
                _oldEpisode = _curEpisode;
                UpdateRichPresence(CurrentMedia, _curEpisode);
            }
        }

        public static void SetMedia(Media? media, int episode = 1)
        {
            if (media != null)
            {
                CurrentMedia = media;
                episode = Math.Clamp(episode, 1, CurrentMedia?.Episodes ?? int.MaxValue);
                _curEpisode = episode;
                UpdateRichPresence(media, episode);

                _oldEpisode = episode;
                SaveWrapper.SetCurrentMedia(media, episode);
            }
            MainWindow.Instance?.AnimeSelected.UpdateMedia(media, episode);
        }

        public static void UpdateEpisode(int episode)
        {
            if (CurrentMedia != null)
            {
                episode = Math.Clamp(episode, 1, CurrentMedia.Episodes ?? int.MaxValue);
                _curEpisode = episode;
                UpdateRichPresence(CurrentMedia, episode);
            }
            MainWindow.Instance?.AnimeSelected.UpdateEpisode(episode);
        }

        public static void CatchupWindow()
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.AnimeSelected.UpdateMedia(CurrentMedia, _curEpisode);
                MainWindow.Instance.AnimeSelected.UpdateEpisode(_curEpisode);
                if (AuthenticationUser != null)
                    MainWindow.Instance.OnAuthenticated();
            }
        }
    }
}
