using AniListNet.Objects;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Projektanker.Icons.Avalonia;
using System;
using System.Diagnostics;
using Image = Avalonia.Controls.Image;

namespace AnilistRPC;

public partial class AnimeSelected : UserControl
{
    public Media? CurrentMedia;
    private DispatcherTimer? _saveDataTimer;
    private int _oldEpisode;

    public AnimeSelected()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            AnimeName.Text = "No Anime Selected";
            CurEpisode.IsVisible = false;
            EpisodeCount.IsVisible = false;
            WebpageButton.IsVisible = false;

            _saveDataTimer = new DispatcherTimer();
            _saveDataTimer.Interval = TimeSpan.FromSeconds(5);
            _saveDataTimer.Tick += SaveProgress;
            _saveDataTimer.Start();
        }
    }

    private void SaveProgress(object? sender, EventArgs e)
    {
        if (CurrentMedia != null && CurEpisode.Text != _oldEpisode.ToString())
        {
            if (int.TryParse(CurEpisode.Text, out int episode))
            {
                episode = Math.Clamp(episode - 1, 0, CurrentMedia.Episodes ?? int.MaxValue);
                SaveWrapper.SetMediaProgress(CurrentMedia, episode, true);
                _oldEpisode = episode;
                MainWindow.UpdateRichPresence(CurrentMedia, episode);
            }
        }
    }

    public void SetMedia(Media? media, int episode = 1)
    {
        if (media == null)
        {
            AnimeName.Text = "No Anime Selected";
            CurEpisode.IsVisible = false;
            EpisodeCount.IsVisible = false;
            WebpageButton.IsVisible = false;
            ImageBorder.Child = new Icon()
            {
                Foreground = new SolidColorBrush(0x50FFFFFF),
                Value = "mdi-image-sync-outline",
                FontSize = 30
            };
            return;
        }

        CurrentMedia = media;
        AnimeName.Text = media.Title.EnglishTitle ?? media.Title.PreferredTitle;
        EpisodeStackPanel.Children.Clear();

        if (media.Episodes != null)
        {
            EpisodeStackPanel.Children.Add(CurEpisode);
            EpisodeStackPanel.Children.Add(EpisodeCount);
            CurEpisode.Tag = media.Episodes;
            EpisodeCount.Text = "/ " + media.Episodes + " Episodes";
            EpisodeCount.Margin = new Thickness(5, 1, 0, 1);
        }
        else
        {
            EpisodeStackPanel.Children.Add(EpisodeCount);
            EpisodeStackPanel.Children.Add(CurEpisode);
            CurEpisode.Tag = null;
            EpisodeCount.Text = "Episode";
            EpisodeCount.Margin = new Thickness(0, 1, 5, 1);
        }

        CurEpisode.IsVisible = true;
        EpisodeCount.IsVisible = true;
        WebpageButton.IsVisible = true;
        episode = Math.Clamp(episode, 1, CurrentMedia.Episodes ?? int.MaxValue);
        CurEpisode.Text = episode.ToString();
        MainWindow.UpdateRichPresence(media, episode);
        LoadImage();

        _oldEpisode = episode;
        SaveWrapper.SetCurrentMedia(media, episode);
    }

    public void UpdateEpisode(int episode)
    {
        if (CurrentMedia != null)
        {
            episode = Math.Clamp(episode, 1, CurrentMedia.Episodes ?? int.MaxValue);
            CurEpisode.Text = episode.ToString();
            MainWindow.UpdateRichPresence(CurrentMedia, episode);
        }
    }

    public async void LoadImage()
    {
        ImageBorder.Child = new Icon()
        {
            Foreground = new SolidColorBrush(0x50FFFFFF),
            Value = "mdi-image-sync-outline",
            FontSize = 30
        };
        ImageBorder.Effect = null;

        ImageBorder.Child = new Image()
        {
            Stretch = Stretch.UniformToFill,
            Source = await ImageLoader.AsyncImageLoader.ProvideImageAsync(CurrentMedia!.Cover.LargeImageUrl.AbsoluteUri)
        }; ;
        ImageBorder.Effect = new DropShadowDirectionEffect()
        {
            BlurRadius = 6,
            Color = CurrentMedia.Cover.Color.ToAvalonia(),
            Direction = 0,
            Opacity = 1
        };
    }

    private void EpisodeCountChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && CurrentMedia != null)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                string text = string.Empty;
                foreach (char c in textBox.Text)
                    if (char.IsDigit(c))
                        text += c;
                textBox.Text = text;

                if (int.TryParse(textBox.Text, out int episode))
                {
                    if (textBox.Tag is int episodeMax && episode > episodeMax)
                        episode = episodeMax;
                    else if (episode < 1)
                        episode = 1;

                    textBox.Text = episode.ToString();
                    MainWindow.UpdateRichPresence(CurrentMedia, episode);
                }
            }
        }
    }

    private void ScrollEpisodeCount(object? sender, PointerWheelEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            if (int.TryParse(textBox.Text, out int episode))
            {
                int incrementBy = 1;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    incrementBy *= 10;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    incrementBy *= 10;

                if (e.Delta.Y > 0)
                    episode += incrementBy;
                else
                    episode -= incrementBy;

                if (textBox.Tag is int episodeMax && episode > episodeMax)
                    episode = episodeMax;
                else if (episode < 1)
                    episode = 1;
                
                textBox.Text = episode.ToString();
            }
            else
            {
                textBox.Text = "1";
            }
        }
    }

    private void OpenURL(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CurrentMedia != null)
            Process.Start(new ProcessStartInfo
            {
                FileName = CurrentMedia.Url.AbsoluteUri,
                UseShellExecute = true
            });
    }
}