using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Services;

public static class UiOperation
{
    public static async Task RunAsync(
        Func<Task> operation,
        string context,
        InfoBar? infoBar = null,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            await operation();
            completed?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Report(exception, context, infoBar);
        }
    }

    public static void Report(Exception exception, string context, InfoBar? infoBar = null)
    {
        App.Services.ApplicationLog.Error(exception, context);
        if (infoBar is null) return;
        infoBar.Severity = InfoBarSeverity.Error;
        infoBar.Message = $"{context}. Un diagnostic sécurisé a été enregistré dans les journaux de l’application.";
        infoBar.IsOpen = true;
    }
}
