using AniListNet.Objects;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Projektanker.Icons.Avalonia;
using System;
using System.Diagnostics;
using Image = Avalonia.Controls.Image;

namespace AnilistRPC;

public partial class AnimeSelected : UserControl
{
    public AnimeSelected()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            AnimeName.Text = "No Anime Selected";
            CurEpisode.IsVisible = false;
            EpisodeCount.IsVisible = false;
            WebpageButton.IsVisible = false;
        }
    }

    public void UpdateMedia(Media? media, int episode = 1)
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
        episode = Math.Clamp(episode, 1, Master.CurrentMedia?.Episodes ?? int.MaxValue);
        CurEpisode.Text = episode.ToString();
        LoadImage();
    }

    public void UpdateEpisode(int episode)
    {
        CurEpisode.Text = episode.ToString();
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
            Source = await ImageLoader.AsyncImageLoader.ProvideImageAsync(Master.CurrentMedia!.Cover.LargeImageUrl.AbsoluteUri)
        }; ;
        ImageBorder.Effect = new DropShadowDirectionEffect()
        {
            BlurRadius = 6,
            Color = Master.CurrentMedia!.Cover.Color.ToAvalonia(),
            Direction = 0,
            Opacity = 1
        };
    }

    private void EpisodeCountChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && Master.CurrentMedia != null)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                string text = string.Empty;
                foreach (char c in textBox.Text)
                    if (char.IsDigit(c))
                        text += c;
                textBox.Text = text;

                if (int.TryParse(textBox.Text, out int episode))
                    Master.UpdateEpisode(episode);
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

                Master.UpdateEpisode(episode);
            }
            else
                Master.UpdateEpisode(1);
        }
    }

    private void OpenURL(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Master.CurrentMedia != null)
            Process.Start(new ProcessStartInfo
            {
                FileName = Master.CurrentMedia.Url.AbsoluteUri,
                UseShellExecute = true
            });
    }
}