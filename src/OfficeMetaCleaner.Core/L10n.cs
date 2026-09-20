using System.Globalization;

namespace OfficeMetaCleaner.Core;

/// <summary>Язык интерфейса: авто (язык ОС), русский, английский.</summary>
public enum AppLanguage
{
    Auto,
    Russian,
    English
}

/// <summary>
/// Публичный фасад локализации. Словари — Strings.resx (нейтральный, русский)
/// и Strings.en.resx; сгенерированный класс ресурсов internal и наружу не торчит.
/// </summary>
public static class L10n
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");
    private static readonly CultureInfo EnglishCulture = new("en-US");

    /// <summary>Активный язык сообщений: "ru" или "en".</summary>
    public static string Language { get; private set; } = "ru";

    /// <summary>
    /// Переключает язык сообщений. Auto — английский на англоязычной ОС,
    /// русский на всех остальных (историческое поведение по умолчанию).
    /// Трогает только UI-культуру (поиск ресурсов), форматирование чисел/дат не меняет.
    /// </summary>
    public static void Apply(AppLanguage preference)
    {
        var english = preference switch
        {
            AppLanguage.English => true,
            AppLanguage.Russian => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("en", StringComparison.OrdinalIgnoreCase)
        };

        Language = english ? "en" : "ru";
        var culture = english ? EnglishCulture : RussianCulture;
        CultureInfo.CurrentUICulture = culture;
        // фоновые потоки (GUI считает в Task.Run) культуру сами не наследуют
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>Все известные ключи словарей. Используется тестами паритета RU/EN.</summary>
    public static IReadOnlyList<string> AllKeys { get; } = new[]
    {
        "Core_FileNotFound", "Core_UnknownContainer", "Core_InPlaceWarning",
        "Core_DropPartDetail", "Core_StripImageDetail", "Core_WriteFailed", "Core_PartFailed",
        "Core_ScrubXmlDetail", "Core_ContentTypesDetail", "Core_RelsDetail", "Core_PrivacyFlagDetail",
        "Core_CfbWouldClear", "Core_CfbStreamMissing", "Core_CfbStreamSkipped",
        "Core_CfbError", "Core_CfbCleared",
        "Core_AceWindowsOnly", "Core_AceNoDriver", "Core_AceLaunchFailed", "Core_AceNoEngine",
        "Core_AceError", "Core_AceWouldDelete", "Core_AceDeleted", "Core_AceDeleteFailed",
        "Core_NothingFound",
        "Cli_UnknownCommand", "Cli_OutNeedsValue", "Cli_NoInput", "Cli_InPlaceOutConflict",
        "Cli_InvalidLang", "Cli_NoFiles", "Cli_FoundFiles", "Cli_BusyFiles", "Cli_PathNotFound",
        "Cli_HelpAbout", "Cli_HelpFormats", "Cli_HelpAccess",
        "Cli_HelpOut", "Cli_HelpInPlace", "Cli_HelpDryRun", "Cli_HelpRecursive",
        "Cli_HelpSignatures", "Cli_HelpStrip", "Cli_HelpKeep", "Cli_HelpWait", "Cli_HelpLang",
        "Cli_Usage",
        "Cli_WaitLocked", "Cli_WaitOfficeOpen", "Cli_WaitRunning", "Cli_WaitSkip",
        "Cli_WaitCancelled", "Cli_WaitFreed",
        "Cli_Error", "Cli_Container", "Cli_Dropped", "Cli_Scrubbed", "Cli_Operations",
        "Cli_NothingToClean", "Cli_DryRunMode", "Cli_Replaced", "Cli_Written",
        "Cli_NewSize", "Cli_SizeBeforeAfter",
        "Gui_Title", "Gui_Subtitle", "Gui_SettingsTip",
        "Gui_OptImages", "Gui_HintImages", "Gui_OptSignatures", "Gui_HintSignatures",
        "Gui_OptWait", "Gui_HintWait", "Gui_OptPrivacy", "Gui_HintPrivacy",
        "Gui_Language", "Gui_LangSystem",
        "Gui_ColFile", "Gui_ColType", "Gui_ColStatus", "Gui_ColDetails",
        "Gui_EmptyHint", "Gui_FilesSection", "Gui_InPlace", "Gui_Beside", "Gui_ToFolder",
        "Gui_AddFiles", "Gui_AddFolder", "Gui_RemoveSelected", "Gui_ClearList", "Gui_ResetMarks",
        "Gui_OpenResult", "Gui_Cancel", "Gui_Clean",
        "Gui_NoFiles", "Gui_SelectedN", "Gui_ProcessedOf", "Gui_DoneSummary", "Gui_AlreadyDone",
        "Gui_AllDone", "Gui_NeedFolder", "Gui_NoOfficeFiles", "Gui_NotProcessed",
        "Status_Pending", "Status_Waiting", "Status_Processing",
        "Status_Done", "Status_Skipped", "Status_Error",
        "Gui_WaitDetail", "Gui_CancelledDetail",
        "Gui_PickFilesTitle", "Gui_FilesFilter", "Gui_AllFiles",
        "Gui_PickFolderTitle", "Gui_SaveTitle",
        "Gui_DetailsTitle", "Gui_DetailsFor", "Gui_Close",
        "Gui_DroppedParts", "Gui_ScrubbedParts", "Gui_OperationsDone", "Gui_NothingToClean",
        "Gui_DryRunTag", "Gui_ReplacedTag",
    };

    /// <summary>Прямой lookup для тестов: строка <paramref name="key"/> на языке <paramref name="language"/> ("ru"|"en").</summary>
    public static string Get(string key, string language)
    {
        var culture = language == "en" ? EnglishCulture : RussianCulture;
        return Strings.ResourceManager.GetString(key, culture)
            ?? throw new InvalidOperationException($"Missing string resource: {key} ({language})");
    }

    // --- Ядро ---
    public static string Core_FileNotFound => Strings.Core_FileNotFound;
    public static string Core_UnknownContainer => Strings.Core_UnknownContainer;
    public static string Core_InPlaceWarning => Strings.Core_InPlaceWarning;
    public static string Core_DropPartDetail => Strings.Core_DropPartDetail;
    public static string Core_StripImageDetail => Strings.Core_StripImageDetail;
    public static string Core_WriteFailed => Strings.Core_WriteFailed;
    public static string Core_PartFailed => Strings.Core_PartFailed;
    public static string Core_ScrubXmlDetail => Strings.Core_ScrubXmlDetail;
    public static string Core_ContentTypesDetail => Strings.Core_ContentTypesDetail;
    public static string Core_RelsDetail => Strings.Core_RelsDetail;
    public static string Core_PrivacyFlagDetail => Strings.Core_PrivacyFlagDetail;
    public static string Core_CfbWouldClear => Strings.Core_CfbWouldClear;
    public static string Core_CfbStreamMissing => Strings.Core_CfbStreamMissing;
    public static string Core_CfbStreamSkipped => Strings.Core_CfbStreamSkipped;
    public static string Core_CfbError => Strings.Core_CfbError;
    public static string Core_CfbCleared => Strings.Core_CfbCleared;
    public static string Core_AceWindowsOnly => Strings.Core_AceWindowsOnly;
    public static string Core_AceNoDriver => Strings.Core_AceNoDriver;
    public static string Core_AceLaunchFailed => Strings.Core_AceLaunchFailed;
    public static string Core_AceNoEngine => Strings.Core_AceNoEngine;
    public static string Core_AceError => Strings.Core_AceError;
    public static string Core_AceWouldDelete => Strings.Core_AceWouldDelete;
    public static string Core_AceDeleted => Strings.Core_AceDeleted;
    public static string Core_AceDeleteFailed => Strings.Core_AceDeleteFailed;
    public static string Core_NothingFound => Strings.Core_NothingFound;

    // --- Консоль ---
    public static string Cli_UnknownCommand => Strings.Cli_UnknownCommand;
    public static string Cli_OutNeedsValue => Strings.Cli_OutNeedsValue;
    public static string Cli_NoInput => Strings.Cli_NoInput;
    public static string Cli_InPlaceOutConflict => Strings.Cli_InPlaceOutConflict;
    public static string Cli_InvalidLang => Strings.Cli_InvalidLang;
    public static string Cli_NoFiles => Strings.Cli_NoFiles;
    public static string Cli_FoundFiles => Strings.Cli_FoundFiles;
    public static string Cli_BusyFiles => Strings.Cli_BusyFiles;
    public static string Cli_PathNotFound => Strings.Cli_PathNotFound;
    public static string Cli_HelpAbout => Strings.Cli_HelpAbout;
    public static string Cli_HelpFormats => Strings.Cli_HelpFormats;
    public static string Cli_HelpAccess => Strings.Cli_HelpAccess;
    public static string Cli_HelpOut => Strings.Cli_HelpOut;
    public static string Cli_HelpInPlace => Strings.Cli_HelpInPlace;
    public static string Cli_HelpDryRun => Strings.Cli_HelpDryRun;
    public static string Cli_HelpRecursive => Strings.Cli_HelpRecursive;
    public static string Cli_HelpSignatures => Strings.Cli_HelpSignatures;
    public static string Cli_HelpStrip => Strings.Cli_HelpStrip;
    public static string Cli_HelpKeep => Strings.Cli_HelpKeep;
    public static string Cli_HelpWait => Strings.Cli_HelpWait;
    public static string Cli_HelpLang => Strings.Cli_HelpLang;
    public static string Cli_Usage => Strings.Cli_Usage;
    public static string Cli_WaitLocked => Strings.Cli_WaitLocked;
    public static string Cli_WaitOfficeOpen => Strings.Cli_WaitOfficeOpen;
    public static string Cli_WaitRunning => Strings.Cli_WaitRunning;
    public static string Cli_WaitSkip => Strings.Cli_WaitSkip;
    public static string Cli_WaitCancelled => Strings.Cli_WaitCancelled;
    public static string Cli_WaitFreed => Strings.Cli_WaitFreed;
    public static string Cli_Error => Strings.Cli_Error;
    public static string Cli_Container => Strings.Cli_Container;
    public static string Cli_Dropped => Strings.Cli_Dropped;
    public static string Cli_Scrubbed => Strings.Cli_Scrubbed;
    public static string Cli_Operations => Strings.Cli_Operations;
    public static string Cli_NothingToClean => Strings.Cli_NothingToClean;
    public static string Cli_DryRunMode => Strings.Cli_DryRunMode;
    public static string Cli_Replaced => Strings.Cli_Replaced;
    public static string Cli_Written => Strings.Cli_Written;
    public static string Cli_NewSize => Strings.Cli_NewSize;
    public static string Cli_SizeBeforeAfter => Strings.Cli_SizeBeforeAfter;

    // --- GUI ---
    public static string Gui_Title => Strings.Gui_Title;
    public static string Gui_Subtitle => Strings.Gui_Subtitle;
    public static string Gui_SettingsTip => Strings.Gui_SettingsTip;
    public static string Gui_OptImages => Strings.Gui_OptImages;
    public static string Gui_HintImages => Strings.Gui_HintImages;
    public static string Gui_OptSignatures => Strings.Gui_OptSignatures;
    public static string Gui_HintSignatures => Strings.Gui_HintSignatures;
    public static string Gui_OptWait => Strings.Gui_OptWait;
    public static string Gui_HintWait => Strings.Gui_HintWait;
    public static string Gui_OptPrivacy => Strings.Gui_OptPrivacy;
    public static string Gui_HintPrivacy => Strings.Gui_HintPrivacy;
    public static string Gui_Language => Strings.Gui_Language;
    public static string Gui_LangSystem => Strings.Gui_LangSystem;
    public static string Gui_ColFile => Strings.Gui_ColFile;
    public static string Gui_ColType => Strings.Gui_ColType;
    public static string Gui_ColStatus => Strings.Gui_ColStatus;
    public static string Gui_ColDetails => Strings.Gui_ColDetails;
    public static string Gui_EmptyHint => Strings.Gui_EmptyHint;
    public static string Gui_FilesSection => Strings.Gui_FilesSection;
    public static string Gui_InPlace => Strings.Gui_InPlace;
    public static string Gui_Beside => Strings.Gui_Beside;
    public static string Gui_ToFolder => Strings.Gui_ToFolder;
    public static string Gui_AddFiles => Strings.Gui_AddFiles;
    public static string Gui_AddFolder => Strings.Gui_AddFolder;
    public static string Gui_RemoveSelected => Strings.Gui_RemoveSelected;
    public static string Gui_ClearList => Strings.Gui_ClearList;
    public static string Gui_ResetMarks => Strings.Gui_ResetMarks;
    public static string Gui_OpenResult => Strings.Gui_OpenResult;
    public static string Gui_Cancel => Strings.Gui_Cancel;
    public static string Gui_Clean => Strings.Gui_Clean;
    public static string Gui_NoFiles => Strings.Gui_NoFiles;
    public static string Gui_SelectedN => Strings.Gui_SelectedN;
    public static string Gui_ProcessedOf => Strings.Gui_ProcessedOf;
    public static string Gui_DoneSummary => Strings.Gui_DoneSummary;
    public static string Gui_AlreadyDone => Strings.Gui_AlreadyDone;
    public static string Gui_AllDone => Strings.Gui_AllDone;
    public static string Gui_NeedFolder => Strings.Gui_NeedFolder;
    public static string Gui_NoOfficeFiles => Strings.Gui_NoOfficeFiles;
    public static string Gui_NotProcessed => Strings.Gui_NotProcessed;
    public static string StatusPending => Strings.Status_Pending;
    public static string StatusWaiting => Strings.Status_Waiting;
    public static string StatusProcessing => Strings.Status_Processing;
    public static string StatusDone => Strings.Status_Done;
    public static string StatusSkipped => Strings.Status_Skipped;
    public static string StatusError => Strings.Status_Error;
    public static string Gui_WaitDetail => Strings.Gui_WaitDetail;
    public static string Gui_CancelledDetail => Strings.Gui_CancelledDetail;
    public static string Gui_PickFilesTitle => Strings.Gui_PickFilesTitle;
    public static string Gui_FilesFilter => Strings.Gui_FilesFilter;
    public static string Gui_AllFiles => Strings.Gui_AllFiles;
    public static string Gui_PickFolderTitle => Strings.Gui_PickFolderTitle;
    public static string Gui_SaveTitle => Strings.Gui_SaveTitle;
    public static string Gui_DetailsTitle => Strings.Gui_DetailsTitle;
    public static string Gui_DetailsFor => Strings.Gui_DetailsFor;
    public static string Gui_Close => Strings.Gui_Close;
    public static string Gui_DroppedParts => Strings.Gui_DroppedParts;
    public static string Gui_ScrubbedParts => Strings.Gui_ScrubbedParts;
    public static string Gui_OperationsDone => Strings.Gui_OperationsDone;
    public static string Gui_NothingToClean => Strings.Gui_NothingToClean;
    public static string Gui_DryRunTag => Strings.Gui_DryRunTag;
    public static string Gui_ReplacedTag => Strings.Gui_ReplacedTag;
}
