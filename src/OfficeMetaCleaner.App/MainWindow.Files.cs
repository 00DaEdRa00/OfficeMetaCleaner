using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace OfficeMetaCleaner.App;

public partial class MainWindow : Window
{
    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Выберите файлы Microsoft Office",
            Filter = "Файлы Office|*.docx;*.docm;*.xlsx;*.xlsm;*.pptx;*.pptm;*.doc;*.xls;*.ppt;*.vsdx;*.vsd;*.accdb;*.mdb|Все файлы|*.*"
        };

        if (dialog.ShowDialog(this) == true)
            AddPaths(dialog.FileNames);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку с файлами" };
        if (dialog.ShowDialog(this) == true)
            AddPaths(new[] { dialog.FolderName });
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_busy)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            AddPaths(paths);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_busy && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => e.Handled = true;

    private void AddPaths(IEnumerable<string> paths)
    {
        var added = 0;
        var skipped = 0;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (AddFile(file)) added++; else skipped++;
                }
            }
            else if (File.Exists(path))
            {
                if (AddFile(path)) added++; else skipped++;
            }
        }

        UpdateUi();

        if (added == 0 && skipped > 0)
            MessageBox.Show(this, "Подходящих файлов Office не найдено.", "OfficeMetaCleaner",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool AddFile(string path)
    {
        if (!OfficeExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return false;

        var full = Path.GetFullPath(path);
        if (Items.Any(i => string.Equals(i.FilePath, full, StringComparison.OrdinalIgnoreCase)))
            return false;

        Items.Add(new ScrubItem(full));
        return true;
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        foreach (var item in FilesGrid.SelectedItems.Cast<ScrubItem>().ToList())
            Items.Remove(item);

        UpdateUi();
    }

    private void ClearList_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        Items.Clear();
        _lastOutputDir = null;
        UpdateUi();
    }

    private void ResetMarks_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        foreach (var item in Items)
        {
            item.Status = "Ожидает";
            item.Detail = "—";
            item.Result = null;
        }

        UpdateUi();
    }
}
