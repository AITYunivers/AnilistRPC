using AniListNet.Objects;
using AniListNet.Parameters;
using IniParser;
using IniParser.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AnilistRPC
{
    public static class SaveWrapper
    {
        public static IniData Ini;

        static SaveWrapper()
        {
            if (File.Exists(GetPath()))
                Ini = new FileIniDataParser().ReadFile(GetPath());
            else
                Ini = new IniData();
        }

        public static Task<Media>? GetCurrentMedia(out int episode)
        {
            if (!Ini.Sections.ContainsSection("CurrentMedia"))
            {
                episode = 0;
                return null;
            }
            episode = int.Parse(Ini["CurrentMedia"]["Episode"]);
            return MainWindow.Anilist.GetMediaAsync(int.Parse(Ini["CurrentMedia"]["Id"]));
        }

        public static void SetCurrentMedia(Media media, int episode)
        {
            Ini["CurrentMedia"]["Id"] = media.Id.ToString();
            Ini["CurrentMedia"]["Episode"] = episode.ToString();
            Save();
        }

        public static async Task<int> GetMediaProgress(Media media)
        {
            if (MainWindow.AuthenticationUser != null)
            {
                MediaEntry? entry = await MainWindow.Anilist.GetMediaEntryAsync(media.Id);
                if (entry != null)
                {
                    SetMediaProgress(media, entry.Progress);
                    return entry.Progress;
                }
            }

            if (!Ini.Sections.ContainsSection("MediaProgress") || !Ini["MediaProgress"].ContainsKey(media.Id.ToString()))
                return 0;

            return int.Parse(Ini["MediaProgress"][media.Id.ToString()]);
        }

        public static void SetMediaProgress(Media media, int episode, bool saveToAnilist = false)
        {
            Ini["MediaProgress"][media.Id.ToString()] = episode.ToString();
            Save();

            if (saveToAnilist && MainWindow.AuthenticationUser != null)
                MainWindow.Anilist.SaveMediaEntryAsync(media.Id, new MediaEntryMutation() { Progress = episode });
        }

        public static void WriteAuthenticationData(AuthenticationData data)
        {
            Ini["Authentication"]["AccessToken"] = data.AccessToken;
            Ini["Authentication"]["TokenType"] = data.TokenType;
            Ini["Authentication"]["Expiry"] = data.Expiry.ToString();
            Save();
        }

        public static AuthenticationData? ReadAuthenticationData()
        {
            if (!Ini.Sections.ContainsSection("Authentication"))
                return null;

            return new AuthenticationData()
            {
                AccessToken = Ini["Authentication"]["AccessToken"],
                TokenType = Ini["Authentication"]["TokenType"],
                Expiry = DateTime.Parse(Ini["Authentication"]["Expiry"])
            };
        }

        public static void ClearAuthenticationData()
        {
            Ini.Sections.RemoveSection("Authentication");
            Save();
        }

        public static bool GetMinimizeTraySetting()
        {
            if (!Ini.Sections.ContainsSection("Settings") || !Ini["Settings"].ContainsKey("MinimizeTray"))
                return true;

            return bool.Parse(Ini["Settings"]["MinimizeTray"]);
        }

        public static void SetMinimizeTraySetting(bool minimizeTray)
        {
            Ini["Settings"]["MinimizeTray"] = minimizeTray.ToString();
        }

        public static bool GetStartupSetting()
        {
            if (!Ini.Sections.ContainsSection("Settings") || !Ini["Settings"].ContainsKey("Startup"))
                return false; // Too intrusive to default to true imo

            return bool.Parse(Ini["Settings"]["Startup"]);
        }

        public static void SetStartupSetting(bool startup)
        {
            Ini["Settings"]["Startup"] = startup.ToString();
        }

        public static bool GetUpdatePopupPreference()
        {
            if (!Ini.Sections.ContainsSection("Settings") || !Ini["Settings"].ContainsKey("UpdatePopup"))
                return true;

            return Ini["Settings"]["UpdatePopup"] != Program.Version.ToString();
        }

        public static void SetUpdatePopupPreference(bool updatePopup)
        {
            if (updatePopup)
                Ini["Settings"]["UpdatePopup"] = Program.Version.ToString();
            else
                Ini["Settings"].RemoveKey("UpdatePopup");
        }

        public static void Save()
        {
            if (!Directory.Exists(Path.GetDirectoryName(GetPath())))
                Directory.CreateDirectory(Path.GetDirectoryName(GetPath())!);
            new FileIniDataParser().WriteFile(GetPath(), Ini);
        }

        private static string GetPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnilistRPC", "config.ini");
        }
    }
}
