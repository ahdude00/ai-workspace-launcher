using AWL.Core.Configuration;
using AWL.Infrastructure.Documents;

namespace AWL.Infrastructure.Tests.Documents;

public sealed class UdfToolInstallerTests
{
    [Fact]
    public void Installs_and_repairs_the_bundled_script()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var paths = new AwlPaths(root.FullName, root.FullName, root.FullName);
            UdfToolInstaller.EnsureInstalled(paths);
            var script = File.ReadAllText(paths.UdfToolFile);
            script.ShouldContain("def pack(");
            UdfToolInstaller.EnsureInstalled(paths);
            File.WriteAllText(paths.UdfToolFile, "out of date");
            UdfToolInstaller.EnsureInstalled(paths);
            File.ReadAllText(paths.UdfToolFile).ShouldBe(script);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
