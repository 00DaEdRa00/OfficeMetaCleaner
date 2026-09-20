using System.Text;
using OfficeMetaCleaner.Core;
using OpenMcdf;

namespace OfficeMetaCleaner.Tests;

public sealed class CfbScrubTests : IDisposable
{
    private readonly string _work;

    public CfbScrubTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Cfb_IsDetected()
    {
        var input = Path.Combine(_work, "legacy.doc");
        ScrubFixtures.CreateCompoundFile(input, "Ivan Petrov");

        Assert.Equal(OfficeContainerKind.CfbLegacy, MetadataScrubber.Detect(input));
    }

    [Fact]
    public void Cfb_SummaryInformationIsCleared()
    {
        var input = Path.Combine(_work, "clear.doc");
        var output = Path.Combine(_work, "clear-clean.doc");
        var personal = "Ivan Petrov Acme Corp " + new string('X', 120); // длиннее пустого property-set
        ScrubFixtures.CreateCompoundFile(input, personal);

        var result = MetadataScrubber.Scrub(input, output, new ScrubOptions());

        Assert.True(result.Success, string.Join("; ", result.Warnings));
        Assert.True(File.Exists(output));

        var raw = Encoding.Latin1.GetString(File.ReadAllBytes(output));
        Assert.DoesNotContain("Ivan Petrov", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('X', 20), raw, StringComparison.Ordinal);

        // результат остаётся валидным compound-файлом, содержимое документа на месте
        using var reopened = RootStorage.OpenRead(output);
        Assert.True(reopened.ContainsEntry("WordDocument"));
    }

    [Fact]
    public void Cfb_OriginalFileIsNotModified()
    {
        var input = Path.Combine(_work, "keep-original.doc");
        ScrubFixtures.CreateCompoundFile(input, "Ivan Petrov");

        var before = File.ReadAllBytes(input);
        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success, string.Join("; ", result.Warnings));
        Assert.Equal(before, File.ReadAllBytes(input));
    }
}
