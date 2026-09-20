using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class OoxmlScrubTests : IDisposable
{
    private readonly string _work;

    public OoxmlScrubTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Scrub_RemovesDocPropsParts()
    {
        var input = Path.Combine(_work, "sample.docx");
        ScrubFixtures.WriteFixture(input);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success);
        Assert.StartsWith("OOXML", result.Container, StringComparison.Ordinal);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

        Assert.Contains("word/document.xml", names);
        Assert.DoesNotContain(names, n => n.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scrub_KeepsPackageConsistent()
    {
        var input = Path.Combine(_work, "consistent.docx");
        ScrubFixtures.WriteFixture(input, includeThumbnail: true);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);

        var contentTypes = ScrubFixtures.ReadEntry(zip, "[Content_Types].xml");
        Assert.DoesNotContain("/docProps/core.xml", contentTypes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/docProps/app.xml", contentTypes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/word/document.xml", contentTypes, StringComparison.OrdinalIgnoreCase);

        var rels = ScrubFixtures.ReadEntry(zip, "_rels/.rels");
        Assert.DoesNotContain("docProps/", rels, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("word/document.xml", rels, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scrub_RemovesAuthorshipAndRsidAttributes()
    {
        var input = Path.Combine(_work, "authors.docx");
        ScrubFixtures.WriteFixture(input);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var docXml = ScrubFixtures.ReadEntry(zip, "word/document.xml");
        var doc = XDocument.Parse(docXml);
        var attributes = doc.Descendants().SelectMany(e => e.Attributes()).ToList();

        Assert.DoesNotContain(attributes, a => a.Name.LocalName.StartsWith("rsid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(attributes, a => a.Name.LocalName.Equals("author", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(attributes, a => a.Name.LocalName.Equals("date", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(attributes, a => a.Name.LocalName.Equals("initials", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("Hello", docXml, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_RemovesThumbnailRelationship()
    {
        var input = Path.Combine(_work, "thumb.docx");
        ScrubFixtures.WriteFixture(input, includeThumbnail: true);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var rels = ScrubFixtures.ReadEntry(zip, "_rels/.rels");
        Assert.DoesNotContain("thumbnail", rels, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DryRun_WritesNothing()
    {
        var input = Path.Combine(_work, "dry.docx");
        ScrubFixtures.WriteFixture(input);

        var before = Directory.GetFiles(_work).Length;
        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions { DryRun = true });
        var after = Directory.GetFiles(_work).Length;

        Assert.True(result.Success);
        Assert.Null(result.OutputPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Scrub_WritesToRequestedOutputPath()
    {
        var input = Path.Combine(_work, "out.docx");
        ScrubFixtures.WriteFixture(input);

        var output = Path.Combine(_work, "sub", "result.docx");
        var result = MetadataScrubber.Scrub(input, output, new ScrubOptions());

        Assert.True(result.Success);
        Assert.Equal(output, result.OutputPath);
        Assert.True(File.Exists(output));
        Assert.True(File.Exists(input));
    }

    [Fact]
    public void UnknownFormat_IsNotModified()
    {
        var input = Path.Combine(_work, "random.bin");
        File.WriteAllText(input, "not an office file");

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.False(result.Success);
        Assert.NotEmpty(result.Warnings);
    }

    // ---------- регрессия: объявление кодировки XML ----------

    [Fact]
    public void ScrubbedXmlParts_DeclareUtf8Encoding()
    {
        var input = Path.Combine(_work, "encoding.docx");
        ScrubFixtures.WriteFixture(input);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "word/document.xml" })
        {
            var entry = zip.GetEntry(name)!;
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("encoding=\"utf-8\"", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("utf-16", text, StringComparison.OrdinalIgnoreCase);

            // XML должен разбираться как самостоятельный документ (так его читает Word)
            using var reader = XmlReader.Create(new MemoryStream(bytes));
            while (reader.Read()) { }
        }
    }

    [Fact]
    public void ScrubbedOoxml_AllPartsParseWithOpenXmlSdk()
    {
        var input = Path.Combine(_work, "sdk.docx");
        ScrubFixtures.WriteFixture(input);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());
        Assert.True(result.Success);

        using var doc = WordprocessingDocument.Open(result.OutputPath!, false);
        Assert.NotNull(doc.MainDocumentPart);

        // принудительно разбираем все части пакета как XML
        Assert.NotEmpty(doc.Parts);
        foreach (var pair in doc.Parts)
        {
            using var stream = pair.OpenXmlPart.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream);
            while (reader.Read()) { }
        }
    }
}
