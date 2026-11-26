using System.IO;
using System.Reflection;

namespace AWL.Desktop.Resources;

/// <summary>The embedded THIRD-PARTY-NOTICES.txt (AYR-04, LIS-01).</summary>
internal static class ThirdPartyNotices
{
    private static readonly Lazy<string> Content = new(() =>
    {
        using var stream = typeof(ThirdPartyNotices).Assembly.GetManifestResourceStream("AWL.ThirdPartyNotices")
            ?? throw new InvalidOperationException("THIRD-PARTY-NOTICES.txt is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static string Text => Content.Value;
}
