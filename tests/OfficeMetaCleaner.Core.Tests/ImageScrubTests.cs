using System.IO.Compression;
using System.Text;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class ImageScrubTests : IDisposable
{
    private readonly string _work;

    public ImageScrubTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Scrub_StripsExifFromEmbeddedJpeg()
    {
        var input = Path.Combine(_work, "image.docx");
        ScrubFixtures.WriteFixtureWithImage(input, "word/media/image1.jpeg", ScrubFixtures.BuildJpegWithExif());

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var entry = zip.GetEntry("word/media/image1.jpeg")!;
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        Assert.True(ScrubFixtures.IsJpeg(bytes));
        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_StripsPngTextChunks()
    {
        var input = Path.Combine(_work, "image.png.docx");
        ScrubFixtures.WriteFixtureWithImage(input, "word/media/image2.png", ScrubFixtures.BuildPngWithMetadata());

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var entry = zip.GetEntry("word/media/image2.png")!;
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());

        Assert.DoesNotContain("eXIf", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tEXt", text, StringComparison.Ordinal);
        Assert.Contains("IHDR", text, StringComparison.Ordinal);
        Assert.Contains("IEND", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_KeepsImageMetadataWhenDisabled()
    {
        var input = Path.Combine(_work, "keep.docx");
        ScrubFixtures.WriteFixtureWithImage(input, "word/media/image1.jpeg", ScrubFixtures.BuildJpegWithExif());

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions { StripImageMetadata = false });
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var entry = zip.GetEntry("word/media/image1.jpeg")!;
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        Assert.Contains("Exif", Encoding.Latin1.GetString(ms.ToArray()), StringComparison.Ordinal);
    }
}
