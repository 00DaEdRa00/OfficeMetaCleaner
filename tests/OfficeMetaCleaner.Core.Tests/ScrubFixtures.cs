using System.IO.Compression;
using System.Text;
using OpenMcdf;

namespace OfficeMetaCleaner.Tests;

/// <summary>Общие фикстуры OOXML/CFB и хелперы чтения для тестов MetadataScrubber.</summary>
internal static class ScrubFixtures
{
    internal static void CreateCompoundFile(string path, string personalData)
    {
        using var cf = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.Transacted);
        WriteStream(cf, "\u0005SummaryInformation", Encoding.ASCII.GetBytes(personalData));
        WriteStream(cf, "\u0005DocumentSummaryInformation", Encoding.ASCII.GetBytes(personalData));
        WriteStream(cf, "WordDocument", Encoding.ASCII.GetBytes("BODY"));
        cf.Commit();
    }

    private static void WriteStream(Storage storage, string name, byte[] data)
    {
        using var stream = storage.CreateStream(name);
        stream.Write(data, 0, data.Length);
    }

    internal static bool IsJpeg(byte[] data) => data.Length > 2 && data[0] == 0xFF && data[1] == 0xD8;

    internal static byte[] BuildJpegWithExif()
    {
        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(Encoding.ASCII.GetBytes("PERSONALDATA")).ToArray();
        var length = payload.Length + 2;

        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
        bytes.Add((byte)(length >> 8));
        bytes.Add((byte)(length & 0xFF));
        bytes.AddRange(payload);
        bytes.AddRange(new byte[] { 0xFF, 0xD9 });
        return bytes.ToArray();
    }

    internal static byte[] BuildPngWithMetadata()
    {
        var list = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        AddChunk(list, "IHDR", new byte[13]);
        AddChunk(list, "eXIf", Encoding.ASCII.GetBytes("PERSONALDATA"));
        AddChunk(list, "tEXt", Encoding.ASCII.GetBytes("Author\0Ivan Petrov"));
        AddChunk(list, "IDAT", new byte[4]);
        AddChunk(list, "IEND", Array.Empty<byte>());
        return list.ToArray();
    }

    private static void AddChunk(List<byte> list, string type, byte[] data)
    {
        var length = data.Length;
        list.Add((byte)(length >> 24));
        list.Add((byte)(length >> 16));
        list.Add((byte)(length >> 8));
        list.Add((byte)length);
        list.AddRange(Encoding.ASCII.GetBytes(type));
        list.AddRange(data);
        list.AddRange(new byte[4]); // CRC-заглушка: очистка CRC не проверяет
    }

    internal static void WriteFixtureWithImage(string path, string imagePartName, byte[] imageBytes)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "[Content_Types].xml", ContentTypes());
        Add(zip, "_rels/.rels", RootRels(false));
        Add(zip, "word/document.xml", DocumentXml());
        Add(zip, "docProps/core.xml", CoreXml());
        Add(zip, "docProps/app.xml", AppXml());
        AddBytes(zip, imagePartName, imageBytes);
    }

    internal static string ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidOperationException($"Entry not found: {name}");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static void WriteFixture(string path, bool includeThumbnail = false)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "[Content_Types].xml", ContentTypes());
        Add(zip, "_rels/.rels", RootRels(includeThumbnail));
        Add(zip, "word/document.xml", DocumentXml());
        Add(zip, "docProps/core.xml", CoreXml());
        Add(zip, "docProps/app.xml", AppXml());
        if (includeThumbnail)
            Add(zip, "docProps/thumbnail.jpeg", "FAKEIMAGE");
    }

    private static void Add(ZipArchive zip, string name, string content)
        => AddBytes(zip, name, new UTF8Encoding(false).GetBytes(content));

    private static void AddBytes(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string ContentTypes() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"""
        + """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">"""
        + """<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>"""
        + """<Default Extension="xml" ContentType="application/xml"/>"""
        + """<Default Extension="jpeg" ContentType="image/jpeg"/>"""
        + """<Default Extension="png" ContentType="image/png"/>"""
        + """<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>"""
        + """<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>"""
        + """<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>"""
        + "</Types>";

    private static string RootRels(bool includeThumbnail)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        sb.Append("""<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>""");
        sb.Append("""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>""");
        sb.Append("""<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>""");
        if (includeThumbnail)
            sb.Append("""<Relationship Id="rId4" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail" Target="docProps/thumbnail.jpeg"/>""");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string DocumentXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:body>
            <w:p w:rsidR="00AB12CD" w:rsidRDefault="00AB12CD">
              <w:r><w:t>Hello</w:t></w:r>
            </w:p>
            <w:p>
              <w:ins w:id="1" w:author="Ivan Petrov" w:date="2026-01-01T00:00:00Z" w:initials="IP">
                <w:r><w:t>tracked</w:t></w:r>
              </w:ins>
            </w:p>
          </w:body>
        </w:document>
        """;

    private static string CoreXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
                           xmlns:dc="http://purl.org/dc/elements/1.1/">
          <dc:creator>Ivan Petrov</dc:creator>
          <cp:lastModifiedBy>Ivan Petrov</cp:lastModifiedBy>
        </cp:coreProperties>
        """;

    private static string AppXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties">
          <Application>Microsoft Office Word</Application>
          <Company>Acme Corp</Company>
        </Properties>
        """;
}
