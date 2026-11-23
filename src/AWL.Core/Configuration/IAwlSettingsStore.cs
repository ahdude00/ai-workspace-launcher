namespace AWL.Core.Configuration;

public interface IAwlSettingsStore
{
    AwlSettings Load();

    void Save(AwlSettings settings);
}
