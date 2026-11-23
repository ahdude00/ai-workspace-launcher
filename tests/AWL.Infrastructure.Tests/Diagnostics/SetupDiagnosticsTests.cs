using System.IO.Compression;
using AWL.Core.Configuration;
using AWL.Infrastructure.Diagnostics;

namespace AWL.Infrastructure.Tests.Diagnostics;

public sealed class SetupDiagnosticsTests
{
    [Fact]
    public void Support_archive_contains_only_the_latest_attempt()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var paths = new AwlPaths(root.FullName, root.FullName, root.FullName);
            SetupDiagnostics.Save(paths, "1.0", ["old failure"]);
            string output = SetupDiagnostics.Save(paths, "1.1", ["preflight: proxy"]);
            using var archive = ZipFile.OpenRead(output);
            var entry = Assert.Single(archive.Entries);
            entry.FullName.ShouldBe("kurulum.txt");
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            text.ShouldContain("AWL 1.1");
            text.ShouldContain("preflight: proxy");
            text.ShouldNotContain("old failure");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
