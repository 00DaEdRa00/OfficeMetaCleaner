using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

/// <summary>Строка списка файлов в окне.</summary>
public sealed class ScrubItem : INotifyPropertyChanged
{
    private string _container = "—";
    private string _status = "Ожидает";
    private string _detail = "—";

    public ScrubItem(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public string Name => Path.GetFileName(FilePath);

    public string Container
    {
        get => _container;
        set { _container = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string Detail
    {
        get => _detail;
        set { _detail = value; OnPropertyChanged(); }
    }

    public string? OutputPath { get; set; }

    /// <summary>Полный результат последней обработки для окна деталей.</summary>
    public ScrubResult? Result { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
