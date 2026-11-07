using Microsoft.Maui.Controls;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>Windows' own folder picker (no package), opened over the app window.</summary>
public static class FolderPicker
{
    /// <summary>The chosen folder's path, or null when the picker was cancelled.</summary>
    public static async Task<string?> PickAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");

        var window = (MauiWinUIWindow)Application.Current!.Windows[0].Handler!.PlatformView!;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, window.WindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
