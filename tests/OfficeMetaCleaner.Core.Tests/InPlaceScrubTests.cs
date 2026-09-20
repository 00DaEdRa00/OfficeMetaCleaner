using System.IO.Compression;
using System.Text;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class InPlaceScrubTests : IDisposable
{
    private readonly string _work;

    public InPlaceScrubTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void InPlace_ReplacesOoxmlSource()
    {
        var input = Path.Combine(_work, "inplace.docx");
        ScrubFixtures.WriteFixture(input);
        var before = File.ReadAllBytes(input);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions { InPlace = true });

        Assert.True(result.Success);
        Assert.True(result.ReplacedInPlace);
        Assert.Equal(input, result.OutputPath);
        Assert.NotEqual(before, File.ReadAllBytes(input));

        using var zip = ZipFile.OpenRead(input);
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(zip.Entries, e => e.FullName == "word/document.xml");

        Assert.Empty(Directory.GetFiles(_work, "*.omc-tmp-*"));
    }

    [Fact]
    public void InPlace_ReplacesLegacyCfbSource()
    {
        var input = Path.Combine(_work, "inplace.doc");
        ScrubFixtures.CreateCompoundFile(input, "Ivan Petrov Acme Corp " + new string('X', 120));

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions { InPlace = true });

        Assert.True(result.Success, string.Join("; ", result.Warnings));
        Assert.True(result.ReplacedInPlace);

        var raw = Encoding.Latin1.GetString(File.ReadAllBytes(input));
        Assert.DoesNotContain("Ivan Petrov", raw, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_work, "*.omc-tmp-*"));
    }
}
