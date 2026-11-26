using System.Globalization;
using System.Text;
using AWL.Core.Shell;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AWL.Desktop.ViewModels;

/// <summary>
/// Files dropped on AWL: their quoted full paths go to the clipboard for pasting into Claude (BLG-07, D08).
/// </summary>
public sealed partial class DropZoneViewModel(IClipboardService clipboard) : ObservableObject
{
    private static readonly CompositeFormat Copied = CompositeFormat.Parse(Strings.DropCopied);
    private static readonly CompositeFormat Skipped = CompositeFormat.Parse(Strings.DropSkipped);

    [ObservableProperty]
    public partial string Message { get; private set; } = Strings.DropHint;

    [ObservableProperty]
    public partial bool IsError { get; private set; }

    public void Drop(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var formatted = DroppedPaths.Format(paths);
        if (!formatted.IsSuccess)
        {
            Show(Strings.ResourceManager.GetString(formatted.Error.UserMessageKey, CultureInfo.CurrentUICulture)
                ?? Strings.DropNothingUsable, error: true);
            return;
        }
        if (!clipboard.TrySetText(formatted.Value.Text))
        {
            Show(Strings.CopyFailed, error: true);
            return;
        }
        string message = string.Format(CultureInfo.CurrentCulture, Copied, formatted.Value.Count);
        if (formatted.Value.Skipped > 0)
        {
            message += string.Format(CultureInfo.CurrentCulture, Skipped, formatted.Value.Skipped);
        }
        Show(message, error: false);
    }

    private void Show(string message, bool error)
    {
        Message = message;
        IsError = error;
    }
}
