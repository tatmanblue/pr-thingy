namespace PrThingy.App.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
