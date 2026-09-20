using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class FileBusyTests : IDisposable
{
    private readonly string _work;
    private readonly List<FileStream> _locks = new();

    public FileBusyTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-busy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        foreach (var fs in _locks)
            fs.Dispose();
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Блокировка в стиле Word: чтение-запись себе, остальным только чтение.</summary>
    private string LockLikeWord(string name = "doc.docx")
    {
        var path = Path.Combine(_work, name);
        File.WriteAllText(path, "content");
        _locks.Add(new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read));
        return path;
    }

    [Fact]
    public void IsBusy_FreeFile_ReturnsFalse()
    {
        var path = Path.Combine(_work, "free.docx");
        File.WriteAllText(path, "content");

        Assert.False(FileBusy.IsBusy(path));
    }

    [Fact]
    public void IsBusy_WordLockedFile_ReturnsTrue()
    {
        var path = LockLikeWord();

        Assert.True(FileBusy.IsBusy(path));
    }

    [Fact]
    public void IsBusy_MissingFile_ReturnsFalse()
    {
        Assert.False(FileBusy.IsBusy(Path.Combine(_work, "nope.docx")));
    }

    [Fact]
    public void FindOwnerFile_Present_ReturnsPath()
    {
        var path = Path.Combine(_work, "report.docx");
        File.WriteAllText(path, "content");
        var owner = Path.Combine(_work, "~$report.docx");
        File.WriteAllText(owner, "owner");

        Assert.Equal(owner, FileBusy.FindOwnerFile(path));
    }

    [Fact]
    public void FindOwnerFile_Absent_ReturnsNull()
    {
        var path = Path.Combine(_work, "report.docx");
        File.WriteAllText(path, "content");

        Assert.Null(FileBusy.FindOwnerFile(path));
    }

    [Fact]
    public void WaitUntilFree_AlreadyFree_ReturnsTrueImmediately()
    {
        var path = Path.Combine(_work, "free.docx");
        File.WriteAllText(path, "content");

        Assert.True(FileBusy.WaitUntilFree(path, TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void WaitUntilFree_Cancelled_ReturnsFalse()
    {
        var path = LockLikeWord();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(FileBusy.WaitUntilFree(path, TimeSpan.FromMilliseconds(50), cts.Token));
    }

    [Fact]
    public void WaitUntilFree_ReleasedWhileWaiting_ReturnsTrue()
    {
        var path = Path.Combine(_work, "releasing.docx");
        File.WriteAllText(path, "content");
        var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        Task.Run(async () =>
        {
            await Task.Delay(300);
            held.Dispose();
        });

        Assert.True(FileBusy.WaitUntilFree(path, TimeSpan.FromMilliseconds(50)));
        held.Dispose();
    }
}
