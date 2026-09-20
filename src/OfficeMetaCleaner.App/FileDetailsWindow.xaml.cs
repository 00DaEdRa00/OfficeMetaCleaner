using System.Windows;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

public partial class FileDetailsWindow : Window
{
    public FileDetailsWindow(ScrubItem item)
    {
        InitializeComponent();

        Title = string.Format(L10n.Gui_DetailsFor, item.Name);
        CloseButton.Content = L10n.Gui_Close;
        FileNameText.Text = item.FilePath;
        FileNameText.ToolTip = item.FilePath;

        var result = item.Result!;
        SummaryText.Text = $"{result.Container} • {item.Status}"
            + (result.OutputPath is null ? string.Empty : $" → {result.OutputPath}");

        foreach (var line in ScrubResultDetails.Describe(result))
            DetailsList.Items.Add(line);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
