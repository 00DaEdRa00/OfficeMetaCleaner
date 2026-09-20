using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OfficeMetaCleaner.Core;

public static partial class MetadataScrubber
{
    private static string ResolveTargetPath(string fullInput, string? outputPath, ScrubOptions options)
    {
        if (options.InPlace)
            return fullInput;

        if (!string.IsNullOrEmpty(outputPath))
            return Path.GetFullPath(outputPath);

        // по умолчанию — та же папка в подкаталоге cleaned, имя файла без изменений
        var dir = Path.GetDirectoryName(fullInput) ?? ".";
        return Path.GetFullPath(Path.Combine(dir, "cleaned", Path.GetFileName(fullInput)));
    }

    /// <summary>
    /// Если файл с таким именем уже существует, добавляет номер: «имя (1).ext», «имя (2).ext».
    /// Если целевой путь совпадает с исходным — нумерация не применяется (это замена файла).
    /// </summary>
    internal static string EnsureUniqueTarget(string target, string sourcePath)
    {
        var fullTarget = Path.GetFullPath(target);
        var fullSource = Path.GetFullPath(sourcePath);

        if (string.Equals(fullTarget, fullSource, StringComparison.OrdinalIgnoreCase))
            return fullTarget;

        if (!File.Exists(fullTarget))
            return fullTarget;

        var dir = Path.GetDirectoryName(fullTarget) ?? ".";
        var name = Path.GetFileNameWithoutExtension(fullTarget);
        var ext = Path.GetExtension(fullTarget);

        for (var i = 1; i < 100000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name} ({Guid.NewGuid():N}){ext}");
    }

    internal static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }

    private static string Normalize(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(part);
        }

        return string.Join('/', stack);
    }

    /// <summary>
    /// Важно: объявление кодировки должно быть utf-8.
    /// XDocument.Save(TextWriter) пишет encoding="utf-16", потому что StringWriter
    /// сообщает кодировку UTF-16, а байты затем сохраняются в UTF-8 — это ломает пакет.
    /// Поэтому сериализуем через XmlWriter с явным UTF-8.
    /// </summary>
    private static string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            doc.Save(writer);
        }

        return new UTF8Encoding(false).GetString(buffer.ToArray());
    }
}
