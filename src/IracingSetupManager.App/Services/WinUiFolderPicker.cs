using IracingSetupManager.Infrastructure.Settings;
using Windows.Storage.Pickers;

namespace IracingSetupManager.App.Services;

public sealed class WinUiFolderPicker : IArchiveFolderPicker
{
    public async Task<string?> PickArchiveFolderAsync(CancellationToken cancellationToken = default) =>
        await PickAsync(cancellationToken);

    public static async Task<string?> PickAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = App.MainWindowInstance
            ?? throw new InvalidOperationException("La fenêtre principale n'est pas disponible.");
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

