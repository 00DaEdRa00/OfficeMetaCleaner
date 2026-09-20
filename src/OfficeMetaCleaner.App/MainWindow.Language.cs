using System.Windows;
using System.Windows.Controls;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

public partial class MainWindow : Window
{
    private bool _languageInit;

    private void InitLanguage()
    {
        LanguageBox.SelectedIndex = LanguageStorage.Load() switch
        {
            AppLanguage.Russian => 1,
            AppLanguage.English => 2,
            _ => 0
        };
        _languageInit = true;
    }

    private void LanguageBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageInit || LanguageBox.SelectedItem is not ComboBoxItem selected)
            return;

        var preference = (selected.Tag as string) switch
        {
            "ru" => AppLanguage.Russian,
            "en" => AppLanguage.English,
            _ => AppLanguage.Auto
        };

        LanguageStorage.Save(preference);
        LangRestartHint.Text = L10n.Gui_LangRestart;
        LangRestartHint.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Накатывает локализованные тексты поверх русских литералов из XAML.
    /// В XAML остаются русские значения как дизайн-тайм фолбэк.
    /// </summary>
    private void ApplyTexts()
    {
        Title = L10n.Gui_Title;
        SubtitleText.Text = L10n.Gui_Subtitle;
        SettingsButton.ToolTip = L10n.Gui_SettingsTip;

        StripImagesCheck.Content = L10n.Gui_OptImages;
        HintImagesText.Text = L10n.Gui_HintImages;
        RemoveSignaturesCheck.Content = L10n.Gui_OptSignatures;
        HintSignaturesText.Text = L10n.Gui_HintSignatures;
        WaitBusyCheck.Content = L10n.Gui_OptWait;
        HintWaitText.Text = L10n.Gui_HintWait;
        PrivacyFlagsCheck.Content = L10n.Gui_OptPrivacy;
        HintPrivacyText.Text = L10n.Gui_HintPrivacy;
        LanguageLabel.Text = L10n.Gui_Language;
        ((ComboBoxItem)LanguageBox.Items[0]).Content = L10n.Gui_LangSystem;

        FileColumn.Header = L10n.Gui_ColFile;
        TypeColumn.Header = L10n.Gui_ColType;
        StatusColumn.Header = L10n.Gui_ColStatus;
        DetailsColumn.Header = L10n.Gui_ColDetails;
        EmptyHint.Text = L10n.Gui_EmptyHint;

        FilesSectionText.Text = L10n.Gui_FilesSection;
        InPlaceCheck.Content = L10n.Gui_InPlace;
        OutputBesideRadio.Content = L10n.Gui_Beside;
        OutputFolderRadio.Content = L10n.Gui_ToFolder;

        AddFilesButton.Content = L10n.Gui_AddFiles;
        AddFolderButton.Content = L10n.Gui_AddFolder;
        RemoveSelectedButton.Content = L10n.Gui_RemoveSelected;
        ClearListButton.Content = L10n.Gui_ClearList;
        ResetMarksButton.Content = L10n.Gui_ResetMarks;
        OpenResultButton.Content = L10n.Gui_OpenResult;
        CancelButton.Content = L10n.Gui_Cancel;
        CleanButton.Content = L10n.Gui_Clean;

        UpdateUi();
    }
}
