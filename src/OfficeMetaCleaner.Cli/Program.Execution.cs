using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Cli;

internal static partial class Program
{
    /// <summary>Ждёт освобождения занятого файла. False — ожидание отменили через Ctrl+C.</summary>
    private static bool WaitForFile(string file, CancellationToken ct)
    {
        if (FileBusy.FindOwnerFile(file) is null && !FileBusy.IsBusy(file))
            return true;

        Console.WriteLine();
        Console.WriteLine($"Занят, жду закрытия: {file}");

        if (FileBusy.FindOwnerFile(file) is not null)
            Console.WriteLine("  документ открыт в Office (есть файл блокировки) — закройте его");

        var apps = FileBusy.RunningOfficeApps();
        if (apps.Count > 0)
            Console.WriteLine($"  запущены: {string.Join(", ", apps)}");

        Console.WriteLine("  Ctrl+C — пропустить файл");

        if (!FileBusy.WaitUntilFree(file, FileBusy.DefaultPollInterval, ct))
        {
            Console.WriteLine("  пропущен (ожидание отменено)");
            return false;
        }

        Console.WriteLine("  файл освобождён, обрабатываю…");
        return true;
    }

    private static int RunOne(string input, string? output, ScrubOptions options)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {input}");

        ScrubResult result;
        try
        {
            result = MetadataScrubber.Scrub(input, output, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ОШИБКА: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Контейнер: {result.Container}");

        var summary = new List<string>();
        if (result.DroppedParts > 0)
            summary.Add($"удалено частей: {result.DroppedParts}");
        if (result.ScrubbedParts > 0)
            summary.Add($"очищено элементов: {result.ScrubbedParts}");
        if (summary.Count == 0)
            summary.Add(result.Actions.Count > 0 ? $"выполнено операций: {result.Actions.Count}" : "нечего очищать");

        Console.WriteLine(string.Join("; ", summary));

        foreach (var action in result.Actions)
            Console.WriteLine($"  [{action.Kind}] {action.Target} — {action.Detail}");

        foreach (var warning in result.Warnings)
            Console.WriteLine($"  ! {warning}");

        if (options.DryRun)
        {
            Console.WriteLine("Режим dry-run: файл не записан.");
        }
        else if (result.Success && result.OutputPath is not null)
        {
            Console.WriteLine(result.ReplacedInPlace
                ? $"Файл заменён: {result.OutputPath}"
                : $"Записано: {result.OutputPath}");
        }

        if (!result.Success)
            return 1;

        if (!options.DryRun && result.Success)
        {
            var after = new FileInfo(result.OutputPath ?? input).Length;
            if (result.ReplacedInPlace)
                Console.WriteLine($"Новый размер: {after} байт");
            else
                Console.WriteLine($"Размер: {new FileInfo(input).Length} -> {after} байт");
        }

        return 0;
    }
}
