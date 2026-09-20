using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

public partial class MainWindow : Window
{
    private bool _detailsOpen;

    private void DetailButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ScrubItem item)
            ShowDetails(item);
    }

    private void FilesGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FilesGrid.SelectedItem is ScrubItem item)
            ShowDetails(item);
    }

    private void ShowDetails(ScrubItem? item)
    {
        if (_detailsOpen)
            return;

        if (item?.Result is null)
        {
            MessageBox.Show(this, L10n.Gui_NotProcessed, "OfficeMetaCleaner",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _detailsOpen = true;
        try
        {
            new FileDetailsWindow(item) { Owner = this }.ShowDialog();
        }
        finally
        {
            _detailsOpen = false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _runCts?.Cancel();
    }

    private void OpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastOutputDir) || !Directory.Exists(_lastOutputDir))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastOutputDir,
            UseShellExecute = true
        });
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CleanButton.IsEnabled = !busy && Items.Count > 0;
        CancelButton.IsEnabled = busy;
        FilesGrid.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private void UpdateUi()
    {
        EmptyHint.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CleanButton.IsEnabled = !_busy && Items.Count > 0;
        OpenResultButton.IsEnabled = !_busy && !string.IsNullOrWhiteSpace(_lastOutputDir);

        if (!_busy && Items.Count == 0)
            SummaryText.Text = L10n.Gui_NoFiles;
        else if (!_busy && Items.All(i => i.Kind == ScrubStatus.Pending))
            SummaryText.Text = string.Format(L10n.Gui_SelectedN, Items.Count);
    }
}
