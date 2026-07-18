using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace PrThingy.App.Services;

public sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            return;

        await clipboard.SetTextAsync(text);
    }
}
