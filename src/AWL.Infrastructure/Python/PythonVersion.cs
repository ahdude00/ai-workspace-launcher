using AWL.Core.Configuration;

namespace AWL.Infrastructure.Python;

/// <summary>Reads the Python version from the virtual environment's pyvenv.cfg without starting Python.</summary>
internal static class PythonVersion
{
    public static string? Read(AwlPaths paths)
    {
        string config = Path.Combine(paths.VirtualEnvironmentDirectory, "pyvenv.cfg");
        try
        {
            string? fallback = null;
            foreach (string line in File.ReadLines(config).Take(50))
            {
                int separator = line.IndexOf('=', StringComparison.Ordinal);
                if (separator < 0)
                {
                    continue;
                }
                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();
                if (key == "version_info" && IsVersion(value))
                {
                    return value;
                }
                if (key == "version" && IsVersion(value))
                {
                    fallback = value;
                }
            }
            return fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsVersion(string value) =>
        value.Length is > 0 and <= 32 && value.All(c => char.IsAsciiDigit(c) || c is '.' || char.IsAsciiLetter(c));
}
