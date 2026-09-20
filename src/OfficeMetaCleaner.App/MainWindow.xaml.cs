using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace OfficeMetaCleaner.App;

public partial class MainWindow : Window
{
    private static readonly string[] OfficeExtensions =
    {
        ".docx", ".docm", ".dotx", ".dotm", ".xlsx", ".xlsm", ".xltx", ".xltm",
        ".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm",
        ".doc", ".xls", ".ppt",
        ".accdb", ".accde", ".accdr", ".accdt", ".mdb", ".mde",
        ".vsdx", ".vsdm", ".vssx", ".vssm", ".vstx", ".vstm", ".vsd"
    };

    private bool _busy;
    private string? _lastOutputDir;
    private CancellationTokenSource? _runCts;

    public ObservableCollection<ScrubItem> Items { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Items.CollectionChanged += (_, _) => UpdateUi();
        UpdateUi();
    }
}
