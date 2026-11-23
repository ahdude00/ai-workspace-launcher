namespace AWL.Core.Configuration;

public interface IAwlSettingsStore
{
    AwlSettings Load();

    /// <summary>Regenerates both Claude files from the current AWL settings.</summary>
    void RefreshClaudeFiles();

    void Save(AwlSettings settings);
}
