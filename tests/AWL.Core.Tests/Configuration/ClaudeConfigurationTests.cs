using System.Text;
using System.Text.Json;
using AWL.Core.Configuration;

namespace AWL.Core.Tests.Configuration;

public sealed class ClaudeConfigurationTests
{
    private readonly AwlPaths _paths = new(Path.Combine(Path.GetTempPath(), "Öz'ün Dosyaları"),
        Path.GetTempPath(), Path.GetTempPath());

    [Fact]
    public void Settings_match_the_CLI_contract()
    {
        using var buffer = new MemoryStream();
        ClaudeSettingsWriter.Write(buffer, _paths, new AwlSettings { RetentionDays = 75 });
        buffer.Position = 0;
        using var json = JsonDocument.Parse(buffer);
        JsonElement root = json.RootElement;
        root.GetProperty("permissions").GetProperty("defaultMode").GetString().ShouldBe("default");
        root.GetProperty("cleanupPeriodDays").GetInt32().ShouldBe(75);
        JsonElement env = root.GetProperty("env");
        env.GetProperty("AWL_HOME").GetString().ShouldBe(_paths.AwlHome);
        env.GetProperty("VIRTUAL_ENV").GetString().ShouldBe(_paths.VirtualEnvironmentDirectory);
    }

    [Fact]
    public void Context_includes_document_rules_and_quoted_PowerShell_examples()
    {
        using var buffer = new MemoryStream();
        ContextFileWriter.Write(buffer, _paths);
        string context = Encoding.UTF8.GetString(buffer.ToArray());
        string python = _paths.PythonExecutable.Replace("'", "''", StringComparison.Ordinal);
        string udf = _paths.UdfToolFile.Replace("'", "''", StringComparison.Ordinal);
        context.ShouldContain("Türkçe");
        context.ShouldContain($"& '{python}' -m pip install");
        context.ShouldContain($"& '{python}' '{udf}' --help");
        context.ShouldContain("<ad> - düzenlendi (2).<uzantı>");
        context.ShouldContain("kopyada yap");
        context.ShouldContain("önce belge skill'lerini kullan");
        context.ShouldContain("Python kütüphanelerini kullan");
        context.ShouldContain("AWL_Ciktilar");
    }
}
