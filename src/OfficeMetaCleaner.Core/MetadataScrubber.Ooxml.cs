using System.IO.Compression;
using System.Text;

namespace OfficeMetaCleaner.Core;

public static partial class MetadataScrubber
{
    private static ScrubResult ScrubOoxml(string inputPath, string? outputPath, ScrubOptions options, ScrubResult result)
    {
        var fullInput = Path.GetFullPath(inputPath);
        var finalTarget = ResolveTargetPath(fullInput, outputPath, options);
        finalTarget = EnsureUniqueTarget(finalTarget, fullInput);
        var replacesSource = string.Equals(finalTarget, fullInput, StringComparison.OrdinalIgnoreCase);

        if (replacesSource)
        {
            result.ReplacedInPlace = true;
            result.Warnings.Add("Результат записывается поверх исходного файла.");
        }

        var dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? tempPath = null;

        using (var source = ZipFile.OpenRead(fullInput))
        {
            var entries = source.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name) || e.FullName.EndsWith("/", StringComparison.Ordinal))
                .ToList();

            foreach (var entry in entries)
            {
                var name = Normalize(entry.FullName);
                if (IsDroppedPart(name, options))
                {
                    dropped.Add(name);
                    result.Actions.Add(new ScrubAction("drop-part", name, "Метаданные контейнера удалены"));
                }
            }

            foreach (var entry in entries)
            {
                var name = Normalize(entry.FullName);
                if (!name.Contains("/_rels/", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var owner = OwnerPartOfRels(name);
                if (owner.Length > 0 && dropped.Contains(owner))
                    dropped.Add(name);
            }

            result.DroppedParts = dropped.Count;

            if (!options.DryRun)
            {
                var dir = Path.GetDirectoryName(finalTarget);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                // пишем во временный файл рядом с целью, затем атомарно заменяем —
                // это позволяет безопасно писать «поверх» исходника
                tempPath = finalTarget + ".omc-tmp-" + Guid.NewGuid().ToString("N");

                using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                using (var destination = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var entry in entries)
                    {
                        var name = Normalize(entry.FullName);
                        if (string.IsNullOrEmpty(name) || dropped.Contains(name))
                            continue;

                        var created = destination.CreateEntry(name, CompressionLevel.Optimal);
                        if (options.NormalizeTimestamps)
                            created.LastWriteTime = options.FixedTimestamp;

                        if (IsTextPart(name))
                        {
                            var text = ReadEntryText(entry);
                            var rewritten = RewritePart(name, text, dropped, options, result);
                            using var writer = new StreamWriter(created.Open(), new UTF8Encoding(false));
                            writer.Write(rewritten);
                        }
                        else if (options.StripImageMetadata && IsMediaImagePart(name))
                        {
                            var bytes = ReadEntryBytes(entry);
                            var stripped = ImageMetadataStripper.Strip(bytes, out var imageChanged);
                            if (imageChanged)
                            {
                                result.ScrubbedParts++;
                                result.Actions.Add(new ScrubAction("strip-image-metadata", name,
                                    "Удалены EXIF/XMP/комментарии изображения"));
                            }

                            using var destinationStream = created.Open();
                            destinationStream.Write(stripped, 0, stripped.Length);
                        }
                        else
                        {
                            using var src = entry.Open();
                            using var dst = created.Open();
                            src.CopyTo(dst);
                        }
                    }
                }
            }
        }

        if (options.DryRun)
        {
            result.Success = true;
            result.OutputPath = null;
            return result;
        }

        try
        {
            File.Move(tempPath!, finalTarget, overwrite: true);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            result.Success = false;
            result.Warnings.Add($"Не удалось записать результат: {ex.Message}");
            return result;
        }

        result.Success = true;
        result.OutputPath = finalTarget;
        return result;
    }

    private static bool IsDroppedPart(string name, ScrubOptions options)
    {
        if (name.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (options.RemoveSignatures && name.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsMediaImagePart(string name)
    {
        if (!ImageMetadataStripper.IsSupportedExtension(name))
            return false;

        var lower = name.ToLowerInvariant();
        return lower.Contains("/media/") || lower.StartsWith("media/");
    }

    private static bool IsTextPart(string name)
    {
        return name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
