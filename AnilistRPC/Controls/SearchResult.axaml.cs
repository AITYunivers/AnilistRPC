using AniListNet.Objects;
using AsyncImageLoader;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Image = Avalonia.Controls.Image;

namespace AnilistRPC;

public partial class SearchResult : UserControl
{
    public Media ResultMedia;

    public SearchResult()
    {
        InitializeComponent();
        ResultMedia = null!;
    }

    public SearchResult(Media result)
    {
        InitializeComponent();
        AnimeName.Text = result.Title.EnglishTitle ?? result.Title.PreferredTitle;
        Description.Text = string.Join(' ', result.SeasonYear, result.Format);

        ResultMedia = result;
        LoadImage();
    }

    public async void LoadImage()
    {
        Image image = new Image();
        image.Stretch = Stretch.UniformToFill;
        image.Source = await ImageLoader.AsyncImageLoader.ProvideImageAsync(ResultMedia.Cover.MediumImageUrl.AbsoluteUri);
        ImageBorder.Child = image;
    }
}