namespace AWL.Desktop.Services;

/// <summary>Puts text on the clipboard; returns false when another program keeps it locked.</summary>
public interface IClipboardService
{
    bool TrySetText(string text);
}
