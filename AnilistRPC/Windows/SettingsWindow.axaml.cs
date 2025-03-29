using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using System.Runtime.InteropServices.ComTypes;

namespace AnilistRPC;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        PlatformSpecific();

        CheckAuthentication();
        TrayCheckbox.IsChecked = SaveWrapper.GetMinimizeTraySetting();
        StartupCheckbox.IsChecked = SaveWrapper.GetStartupSetting();
    }

    public void PlatformSpecific()
    {
        if (OperatingSystem.IsLinux())
        {
            // Avalonia doesn't support tray icons on Linux
            ((Border)TraySetting.Parent!).IsVisible = false;
        }
    }

    private void CheckAuthentication()
    {
        if (MainWindow.AuthenticationUser == null)
        {
            AuthStatus.Text = "You are currently not signed into Anilist";
            AuthButton.Content = "Sign In";
        }
        else
        {
            AuthStatus.Text = $"Signed in as {MainWindow.AuthenticationUser.Name}";
            AuthButton.Content = "Sign Out";
        }
    }

    private async void SignInOut(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MainWindow.AuthenticationUser == null)
        {
            if (await SetupAuthentication())
                CheckAuthentication();
        }
        else
        {
            MainWindow.AuthenticationUser = null;
            SaveWrapper.ClearAuthenticationData();
            CheckAuthentication();
        }
    }

    public async Task<bool> SetupAuthentication()
    {
        AuthenticationData authData = new AuthenticationData();
        if (!await Authenticate(authData))
            return false;

        MainWindow.AuthenticationUser = await MainWindow.Anilist.GetAuthenticatedUserAsync();
        SaveWrapper.WriteAuthenticationData(authData);

        return true;
    }

    public static async Task<bool> Authenticate(AuthenticationData authData)
    {
        Process.Start(new ProcessStartInfo
        {
            // Using my Client ID, but you can use your own
            FileName = @"https://anilist.co/api/v2/oauth/authorize?client_id=25400&response_type=token",
            UseShellExecute = true
        });

        using var httpListener = new HttpListener();
        // 8925 is a random port I came up with bc 5000 and 8080 are common and were being used on my machine
        httpListener.Prefixes.Add("http://localhost:8295/callback/");
        httpListener.Start();

        // Workaround bc Anilist sends the fragment only to the client
        while (string.IsNullOrEmpty(authData.AccessToken))
        {
            var context = await httpListener.GetContextAsync();

            if (context.Request.HttpMethod == "GET")
            {
                string responseHtml = LoadAuthHtml();

                byte[] responseBytes = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html";
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            else if (context.Request.HttpMethod == "POST")
            {
                // Handle the POST request containing the access token
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                string requestBody = await reader.ReadToEndAsync();
                authData.AccessToken = ParsePropertyFromJson(requestBody, "access_token") ?? string.Empty;
                authData.TokenType = ParsePropertyFromJson(requestBody, "token_type") ?? string.Empty;
                authData.Expiry = DateTime.UtcNow + TimeSpan.FromSeconds(int.Parse(ParsePropertyFromJson(requestBody, "expires_in") ?? "0"));

                // Minimal response to avoid browser hang
                context.Response.StatusCode = 200;
                context.Response.Close();
            }
        }

        return await MainWindow.TryAuthenticate(authData);
    }

    private static string? ParsePropertyFromJson(string json, string property)
    {
        using var jsonDoc = JsonDocument.Parse(json);
        return jsonDoc.RootElement.GetProperty(property).GetString();
    }

    private static string LoadAuthHtml()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("AnilistRPC.Resources.Auth.html"))
        {
            if (stream == null)
                throw new FileNotFoundException($"Resource Auth.html not found.");

            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }

    private void ToggleMinimizeTray(object? sender, RoutedEventArgs e)
    {
        SaveWrapper.SetMinimizeTraySetting(TrayCheckbox.IsChecked ?? false);
    }

    private void ToggleStartup(object? sender, RoutedEventArgs e)
    {
        SaveWrapper.SetStartupSetting(StartupCheckbox.IsChecked ?? false);

        string shortcutPath = string.Empty;
        if (OperatingSystem.IsWindows())
            shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "AnilistRPC.lnk");
        else if (OperatingSystem.IsLinux())
            shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "AnilistRPC.desktop");

        if (StartupCheckbox.IsChecked == true)
        {
            if (File.Exists(shortcutPath))
                return;

            if (OperatingSystem.IsWindows())
            {
                ProcessModule? processModule = Process.GetCurrentProcess().MainModule;
                IShellLink shortcut = (IShellLink)new ShellLink();
                shortcut.SetPath(processModule?.FileName ?? Path.Combine(Directory.GetCurrentDirectory(), "AnilistRPC.exe"));
                shortcut.SetWorkingDirectory(Directory.GetCurrentDirectory());
                shortcut.SetDescription("AnilistRPC Shortcut, defaults to tray if setting is enabled");
                shortcut.SetArguments("--tray");

                IPersistFile file = (IPersistFile)shortcut;
                file.Save(shortcutPath, false);
            }
            else if (OperatingSystem.IsLinux())
            {
                StreamWriter writer = new StreamWriter(shortcutPath);
                writer.WriteLine("[Desktop Entry]");
                writer.WriteLine("Type=Application");
                writer.WriteLine("Name=AnilistRPC");
                writer.WriteLine("Exec=exec \"" + Path.Combine(Directory.GetCurrentDirectory(), "AnilistRPC") + "\"");
                writer.Dispose();
            }
        }
        else if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}