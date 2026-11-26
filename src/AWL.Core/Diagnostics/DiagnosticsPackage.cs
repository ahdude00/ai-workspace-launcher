using AWL.Core.Common;

namespace AWL.Core.Diagnostics;

/// <summary>Versions shown on the About screen and written to the diagnostics package (AYR-04, TAN-03).</summary>
/// <param name="Awl">AWL's informational version.</param>
/// <param name="Claude">The verified Claude Code version, if known.</param>
/// <param name="Python">The version of AWL's Python environment, if it exists.</param>
/// <param name="Windows">The Windows version string.</param>
/// <param name="Architecture">The operating system architecture.</param>
public sealed record VersionReport(string Awl, string? Claude, string? Python, string Windows, string Architecture);

/// <summary>
/// Saves logs, versions and setup results as one ZIP file for support (TAN-03). The database, settings files and
/// documents are never included, so session titles and document contents cannot leak (TAN-02).
/// </summary>
public interface IDiagnosticsPackage
{
    Task<Result> SaveAsync(string destination, VersionReport versions, CancellationToken cancellationToken);
}
