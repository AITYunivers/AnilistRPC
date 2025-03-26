using AniListNet;
using AniListNet.Objects;
using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.ObjectModel;

namespace AnilistRPC;

public partial class SearchResults : UserControl
{
    public ObservableCollection<SearchResult> Results = [];

    public SearchResults()
    {
        InitializeComponent();
        ResultsList.Items.Clear();
        ResultsList.ItemsSource = Results;
    }

    public void SetSearchResults(AniPagination<Media> results)
    {
        Results.Clear();
        foreach (Media media in results.Data)
            Results.Add(new SearchResult(media));
    }

    public void SetWatchingResults(AniPagination<MediaEntry> results)
    {
        Results.Clear();
        foreach (MediaEntry mediaEntry in results.Data)
            Results.Add(new SearchResult(mediaEntry.Media));
    }

    private async void ResultFullSelected(object? sender, TappedEventArgs e)
    {
        if (ResultsList.SelectedItems?.Count == 0 || ResultsList.SelectedItem == null)
            return;

        SearchResult item = (SearchResult)ResultsList.SelectedItem;
        if (item.ResultMedia == MainWindow.Instance.AnimeSelected.CurrentMedia)
            return;
        MainWindow.Instance.AnimeSelected.SetMedia(item.ResultMedia, await SaveWrapper.GetMediaProgress(item.ResultMedia) + 1);

        Results.Clear();
        MainWindow.Instance.SearchBar.Clear();
    }
}