using System.IO.Compression;
using System.Text;

namespace OfficeMetaCleaner.Tests;

using Core;

/// <summary>
/// Word/PowerPoint умеют сами вычищать сведения о пользователе при каждом
/// сохранении, если в файле выставлен соответствующий флаг. Проверяем, что
/// очистка такой флаг включает: файл остаётся чистым и после правок в Office.
/// </summary>
public sealed class PrivacyFlagTests : IDisposable
{
    private readonly string _work;

    public PrivacyFlagTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "omc-privacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Scrub_SetsRemovePersonalInformation_InWordSettings()
    {
        var input = Path.Combine(_work, "privacy.docx");
        WriteDocxWithSettings(input, """<w:defaultTabStop w:val="720"/>""");

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success);
        Assert.Contains(result.Actions,
            a => a.Kind == "set-privacy-flag" && a.Target == "word/settings.xml");

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var settings = ReadEntry(zip, "word/settings.xml");
        Assert.Contains("removePersonalInformation", settings, StringComparison.Ordinal);
        Assert.Contains("defaultTabStop", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_FlipsRemovePersonalInformationFalse_ToTrue()
    {
        var input = Path.Combine(_work, "privacy-off.docx");
        WriteDocxWithSettings(input, """<w:removePersonalInformation w:val="false"/>""");

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success);

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var settings = ReadEntry(zip, "word/settings.xml");
        Assert.Contains("removePersonalInformation", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("false", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_SetsRemovePersonalInfoOnSave_InPresentation()
    {
        var input = Path.Combine(_work, "privacy.pptx");
        WritePptx(input, removeInfo: false);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success);
        Assert.Contains(result.Actions,
            a => a.Kind == "set-privacy-flag" && a.Target == "ppt/presentation.xml");

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var presentation = ReadEntry(zip, "ppt/presentation.xml");
        Assert.Contains("removePersonalInfoOnSave", presentation, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_KeepsEnabledRemovePersonalInfoOnSave_AsIs()
    {
        var input = Path.Combine(_work, "privacy-on.pptx");
        WritePptx(input, removeInfo: true);

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions());

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Actions, a => a.Kind == "set-privacy-flag");

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var presentation = ReadEntry(zip, "ppt/presentation.xml");
        Assert.Contains("removePersonalInfoOnSave", presentation, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_SkipsPrivacyFlag_WhenDisabled()
    {
        var input = Path.Combine(_work, "privacy-disabled.docx");
        WriteDocxWithSettings(input, """<w:defaultTabStop w:val="720"/>""");

        var result = MetadataScrubber.Scrub(input, null, new ScrubOptions { SetPrivacyFlags = false });

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Actions, a => a.Kind == "set-privacy-flag");

        using var zip = ZipFile.OpenRead(result.OutputPath!);
        var settings = ReadEntry(zip, "word/settings.xml");
        Assert.DoesNotContain("removePersonalInformation", settings, StringComparison.Ordinal);
        Assert.Contains("defaultTabStop", settings, StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private static string ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidOperationException($"Entry not found: {name}");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void WriteDocxWithSettings(string path, string settingsInnerXml)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">"""
            + """<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>"""
            + """<Default Extension="xml" ContentType="application/xml"/>"""
            + """<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>"""
            + """<Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>"""
            + """<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>"""
            + "</Types>");
        Add(zip, "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">"""
            + """<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>"""
            + """<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>"""
            + "</Relationships>");
        Add(zip, "word/document.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">"""
            + "<w:body><w:p><w:r><w:t>Hello</w:t></w:r></w:p></w:body></w:document>");
        Add(zip, "word/settings.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">"""
            + settingsInnerXml
            + "</w:settings>");
        Add(zip, "docProps/core.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/">"""
            + "<dc:creator>Ivan Petrov</dc:creator></cp:coreProperties>");
    }

    private static void WritePptx(string path, bool removeInfo)
    {
        var flag = removeInfo ? """" removePersonalInfoOnSave="1" """" : string.Empty;
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">"""
            + """<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>"""
            + """<Default Extension="xml" ContentType="application/xml"/>"""
            + """<Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>"""
            + """<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>"""
            + "</Types>");
        Add(zip, "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">"""
            + """<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>"""
            + """<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>"""
            + "</Relationships>");
        Add(zip, "ppt/presentation.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + $"<p:presentation{flag} xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">"
            + "<p:sldIdLst/>"
            + "</p:presentation>");
        Add(zip, "docProps/core.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
            + """<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/">"""
            + "<dc:creator>Ivan Petrov</dc:creator></cp:coreProperties>");
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
