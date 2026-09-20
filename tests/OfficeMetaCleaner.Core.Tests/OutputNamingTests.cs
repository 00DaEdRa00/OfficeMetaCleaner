using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class OutputNamingTests : IDisposable
{
    private readonly string _work;

    public OutputNamingTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void DefaultOutput_KeepsFileNameInCleanedSubfolder()
    {
        var input = Path.Combine(_work, "report.docx");
        ScrubFixtures.WriteFixture(input);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success);
        var expected = Path.Combine(_work, "cleaned", "report.docx");
        Assert.Equal(expected, result.OutputPath);
        Assert.True(File.Exists(expected));
        Assert.DoesNotContain(".clean", Path.GetFileName(result.OutputPath!), StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultOutput_AddsNumberWhenNameAlreadyExists()
    {
        var input = Path.Combine(_work, "again.docx");
        ScrubFixtures.WriteFixture(input);

        var first = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        var second = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        var third = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(first.Success && second.Success && third.Success);
        Assert.Equal(Path.Combine(_work, "cleaned", "again.docx"), first.OutputPath);
        Assert.Equal(Path.Combine(_work, "cleaned", "again (1).docx"), second.OutputPath);
        Assert.Equal(Path.Combine(_work, "cleaned", "again (2).docx"), third.OutputPath);
        Assert.True(File.Exists(first.OutputPath!));
        Assert.True(File.Exists(second.OutputPath!));
        Assert.True(File.Exists(third.OutputPath!));
    }

    [Fact]
    public void DefaultOutput_LegacyCfb_KeepsFileName()
    {
        var input = Path.Combine(_work, "legacy-name.doc");
        ScrubFixtures.CreateCompoundFile(input, "Ivan Petrov");

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success, string.Join("; ", result.Warnings));
        Assert.Equal(Path.Combine(_work, "cleaned", "legacy-name.doc"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath!));
    }
}
