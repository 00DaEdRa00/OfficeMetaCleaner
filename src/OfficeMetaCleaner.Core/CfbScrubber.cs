using OpenMcdf;

namespace OfficeMetaCleaner.Core;

/// <summary>
/// Очистка персональных метаданных в legacy-контейнерах OLE/CFB (.doc/.xls/.ppt).
/// Контейнер пересобирается в новый файл, а потоки SummaryInformation и
/// DocumentSummaryInformation перезаписываются пустыми property-set'ами прямо на месте —
/// без удаления потоков, чтобы старые байты не остались в освобождённых секторах.
/// </summary>
public static class CfbScrubber
{
    private const string SummaryInformation = "\u0005SummaryInformation";
    private const string DocumentSummaryInformation = "\u0005DocumentSummaryInformation";

    private static readonly byte[] SummaryFmtId =
    {
        0xE0, 0x85, 0x9F, 0xF2, 0xF9, 0x4F, 0x68, 0x10, 0xAB, 0x91, 0x08, 0x00, 0x2B, 0x27, 0xB3, 0xD9
    };

    private static readonly byte[] DocumentSummaryFmtId =
    {
        0x02, 0xD5, 0xCD, 0xD5, 0x9C, 0x2E, 0x1B, 0x10, 0x93, 0x97, 0x08, 0x00, 0x2B, 0x2C, 0xF9, 0xAE
    };

    public static ScrubResult Scrub(string inputPath, string? outputPath, ScrubOptions options)
    {
        var fullInput = Path.GetFullPath(inputPath);
        var result = new ScrubResult
        {
            InputPath = fullInput,
            Container = "OLE/CFB (legacy)"
        };

        var finalTarget = ResolveTargetPath(fullInput, outputPath, options);
        finalTarget = MetadataScrubber.EnsureUniqueTarget(finalTarget, fullInput);
        var replacesSource = string.Equals(finalTarget, fullInput, StringComparison.OrdinalIgnoreCase);
        if (replacesSource)
        {
            result.ReplacedInPlace = true;
            result.Warnings.Add("Результат записывается поверх исходного файла.");
        }

        try
        {
            if (options.DryRun)
            {
                using var probe = RootStorage.OpenRead(fullInput);
                foreach (var name in new[] { SummaryInformation, DocumentSummaryInformation })
                {
                    if (HasStream(probe, name))
                        result.Actions.Add(new ScrubAction("would-clear-cfb-stream", name,
                            "Поток будет заменён пустым property-set"));
                    else
                        result.Warnings.Add($"Поток {name} не найден.");
                }

                result.Success = true;
                return result;
            }

            var dir = Path.GetDirectoryName(finalTarget);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = finalTarget + ".omc-tmp-" + Guid.NewGuid().ToString("N");

            using (var source = RootStorage.OpenRead(fullInput))
            {
                using var destination = RootStorage.Create(tempPath, OpenMcdf.Version.V3, StorageModeFlags.Transacted);
                source.CopyTo(destination);

                if (!OverwriteWithEmpty(destination, SummaryInformation, SummaryFmtId, result))
                    result.Warnings.Add($"Поток {SummaryInformation} не найден — пропущен.");

                if (!OverwriteWithEmpty(destination, DocumentSummaryInformation, DocumentSummaryFmtId, result))
                    result.Warnings.Add($"Поток {DocumentSummaryInformation} не найден — пропущен.");

                destination.Commit();
            }

            File.Move(tempPath, finalTarget, overwrite: true);

            result.Success = true;
            result.OutputPath = finalTarget;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Warnings.Add($"Ошибка очистки CFB: {ex.Message}");
        }

        return result;
    }

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

    private static bool HasStream(Storage storage, string name)
    {
        if (!storage.TryOpenStream(name, out var stream))
            return false;

        stream.Dispose();
        return true;
    }

    private static bool OverwriteWithEmpty(Storage storage, string name, byte[] fmtId, ScrubResult result)
    {
        if (!storage.TryOpenStream(name, out var stream))
            return false;

        using (stream)
        {
            // Перезаписываем содержимое на месте, не удаляя поток:
            // если исходные данные длиннее пустого property-set, добиваем нулями до исходной длины,
            // чтобы в контейнере не осталось ни одного байта старых метаданных.
            var originalLength = stream.Length;
            var emptySet = BuildEmptyPropertySet(fmtId);
            var totalLength = Math.Max(originalLength, emptySet.Length);

            var buffer = new byte[totalLength];
            Array.Copy(emptySet, buffer, emptySet.Length);

            stream.Position = 0;
            stream.Write(buffer, 0, buffer.Length);
            stream.SetLength(totalLength);
            stream.Flush();

            result.ScrubbedParts++;
            result.Actions.Add(new ScrubAction("clear-cfb-stream", name,
                $"Метаданные очищены ({originalLength} -> {totalLength} байт, остаток обнулён)"));
        }

        return true;
    }

    /// <summary>Пустой валидный PropertySetStream: заголовок + одна секция без свойств.</summary>
    private static byte[] BuildEmptyPropertySet(byte[] fmtId)
    {
        var buffer = new byte[56];

        buffer[0] = 0xFE;                        // wByteOrder = 0xFFFE (little-endian)
        buffer[1] = 0xFF;
        // wFormat = 0
        buffer[4] = 0x06;                        // dwOSVer = 0x00020006
        buffer[5] = 0x00;
        buffer[6] = 0x02;
        buffer[7] = 0x00;
        // clsid = нули (8..23)
        buffer[24] = 0x01;                       // cSections = 1
        Array.Copy(fmtId, 0, buffer, 28, 16);    // fmtid секции
        buffer[44] = 48;                         // dwOffset = 0x30

        buffer[48] = 8;                          // cbSection = 8
        // cProperties = 0

        return buffer;
    }
}
