namespace PrThingy.App.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
