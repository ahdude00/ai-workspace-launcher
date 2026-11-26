using System.Runtime.InteropServices;
using System.Windows;

namespace AWL.Desktop.Services;

internal sealed class WpfClipboardService : IClipboardService
{
    public bool TrySetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        // Another program can hold the clipboard open for a moment (CLIPBRD_E_CANT_OPEN).
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(50);
            }
        }
        return false;
    }
}
