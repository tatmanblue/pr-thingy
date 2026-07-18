using CommunityToolkit.Mvvm.ComponentModel;
using PrThingy.Core.Models;

namespace PrThingy.App.ViewModels;

public partial class WatchedRepositoryRowViewModel(WatchedRepository repository) : ViewModelBase
{
    public string Id { get; } = repository.Id;
    public string StorageKey { get; } = repository.StorageKey;
    public string LocalPath { get; } = repository.LocalPath;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = repository.DisplayName;

    [ObservableProperty]
    public partial bool Enabled { get; set; } = repository.Enabled;

    public WatchedRepository ToModel() => new()
    {
        Id = Id,
        StorageKey = StorageKey,
        DisplayName = DisplayName,
        LocalPath = LocalPath,
        Enabled = Enabled
    };
}
