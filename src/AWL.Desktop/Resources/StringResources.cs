using System.Collections;
using System.Globalization;
using System.Windows;

namespace AWL.Desktop.Resources;

/// <summary>
/// Exposes every entry of <see cref="Strings"/> as an application resource named <c>T.&lt;key&gt;</c>, so that
/// XAML can use <c>{StaticResource T.Key}</c> while the text itself stays only in Strings.resx (AYR-03).
/// </summary>
internal static class StringResources
{
    public const string Prefix = "T.";

    public static void AddTo(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var set = Strings.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, createIfNotExists: true, tryParents: true)
            ?? throw new InvalidOperationException("The string resources are missing.");
        foreach (DictionaryEntry entry in set)
        {
            if (entry is { Key: string key, Value: string text })
            {
                dictionary[Prefix + key] = text;
            }
        }
    }
}
