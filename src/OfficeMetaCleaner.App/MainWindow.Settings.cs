using System.Windows;
using Microsoft.Win32;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

public partial class MainWindow : Window
{
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
    }

    private void InPlace_Changed(object sender, RoutedEventArgs e)
    {
        if (OutputModePanel is null)
            return;

        var inPlace = InPlaceCheck?.IsChecked == true;
        OutputModePanel.IsEnabled = !inPlace;
        OutputModePanel.Opacity = inPlace ? 0.5 : 1.0;
    }

    private void OutputMode_Changed(object sender, RoutedEventArgs e)
    {
        var useFolder = OutputFolderRadio?.IsChecked == true;
        if (OutputFolderBox is not null) OutputFolderBox.IsEnabled = useFolder;
        if (BrowseButton is not null) BrowseButton.IsEnabled = useFolder;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = L10n.Gui_SaveTitle };
        if (dialog.ShowDialog(this) == true)
            OutputFolderBox.Text = dialog.FolderName;
    }
}
