using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

/// <summary>
/// Выбор языка GUI. Хранится в %AppData%\OfficeMetaCleaner\lang.txt
/// ("auto"|"ru"|"en", по умолчанию auto — язык системы).
/// </summary>
internal static class LanguageStorage
{
    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OfficeMetaCleaner");
            return Path.Combine(dir, "lang.txt");
        }
    }

    internal static AppLanguage Load()
    {
        try
        {
            return Parse(File.ReadAllText(FilePath).Trim());
        }
        catch
        {
            return AppLanguage.Auto;
        }
    }

    internal static void Save(AppLanguage language)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, language switch
            {
                AppLanguage.Russian => "ru",
                AppLanguage.English => "en",
                _ => "auto"
            });
        }
        catch
        {
            // best effort: язык просто не запомнится
        }
    }

    internal static AppLanguage Parse(string raw) => raw.ToLowerInvariant() switch
    {
        "ru" or "russian" => AppLanguage.Russian,
        "en" or "english" => AppLanguage.English,
        _ => AppLanguage.Auto
    };
}
