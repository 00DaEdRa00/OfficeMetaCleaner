using System.Text;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class AceDetectionTests : IDisposable
{
    private readonly string _work;

    public AceDetectionTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void AceDb_IsDetectedByHeader()
    {
        var input = Path.Combine(_work, "base.accdb");
        var bytes = new byte[64];
        Encoding.ASCII.GetBytes("Standard ACE DB").CopyTo(bytes, 4);
        File.WriteAllBytes(input, bytes);

        Assert.Equal(OfficeContainerKind.AceDatabase, MetadataScrubber.Detect(input));
    }

    [Fact]
    public void JetDb_IsDetectedByHeader()
    {
        var input = Path.Combine(_work, "old.mdb");
        var bytes = new byte[64];
        Encoding.ASCII.GetBytes("Standard Jet DB").CopyTo(bytes, 4);
        File.WriteAllBytes(input, bytes);

        Assert.Equal(OfficeContainerKind.AceDatabase, MetadataScrubber.Detect(input));
    }

    [Fact]
    public void AceFile_IsNotRoutedToOoxmlOrCfb()
    {
        var input = Path.Combine(_work, "routing.accdb");
        var bytes = new byte[64];
        Encoding.ASCII.GetBytes("Standard ACE DB").CopyTo(bytes, 4);
        File.WriteAllBytes(input, bytes);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.Equal("Access (ACE/Jet)", result.Container);
        Assert.NotEqual("OOXML (ZIP/OPC)", result.Container);
        Assert.NotEqual("OLE/CFB (legacy)", result.Container);
    }

    [Fact]
    public void AceExtension_IsRecognized()
    {
        Assert.True(AceDbScrubber.IsAccessExtension("baza.accdb"));
        Assert.True(AceDbScrubber.IsAccessExtension("baza.mdb"));
        Assert.False(AceDbScrubber.IsAccessExtension("doc.docx"));
    }
}
