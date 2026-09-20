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
        Console.WriteLine(string.Format(L10n.Cli_WaitLocked, file));

        if (FileBusy.FindOwnerFile(file) is not null)
            Console.WriteLine(L10n.Cli_WaitOfficeOpen);

        var apps = FileBusy.RunningOfficeApps();
        if (apps.Count > 0)
            Console.WriteLine(string.Format(L10n.Cli_WaitRunning, string.Join(", ", apps)));

        Console.WriteLine(L10n.Cli_WaitSkip);

        if (!FileBusy.WaitUntilFree(file, FileBusy.DefaultPollInterval, ct))
        {
            Console.WriteLine(L10n.Cli_WaitCancelled);
            return false;
        }

        Console.WriteLine(L10n.Cli_WaitFreed);
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
            Console.Error.WriteLine(string.Format(L10n.Cli_Error, ex.Message));
            return 1;
        }

        Console.WriteLine(string.Format(L10n.Cli_Container, result.Container));

        var summary = new List<string>();
        if (result.DroppedParts > 0)
            summary.Add(string.Format(L10n.Cli_Dropped, result.DroppedParts));
        if (result.ScrubbedParts > 0)
            summary.Add(string.Format(L10n.Cli_Scrubbed, result.ScrubbedParts));
        if (summary.Count == 0)
            summary.Add(result.Actions.Count > 0 ? string.Format(L10n.Cli_Operations, result.Actions.Count) : L10n.Cli_NothingToClean);

        Console.WriteLine(string.Join("; ", summary));

        foreach (var action in result.Actions)
            Console.WriteLine($"  [{action.Kind}] {action.Target} — {action.Detail}");

        foreach (var warning in result.Warnings)
            Console.WriteLine($"  ! {warning}");

        if (options.DryRun)
        {
            Console.WriteLine(L10n.Cli_DryRunMode);
        }
        else if (result.Success && result.OutputPath is not null)
        {
            Console.WriteLine(result.ReplacedInPlace
                ? string.Format(L10n.Cli_Replaced, result.OutputPath)
                : string.Format(L10n.Cli_Written, result.OutputPath));
        }

        if (!result.Success)
            return 1;

        if (!options.DryRun && result.Success)
        {
            var after = new FileInfo(result.OutputPath ?? input).Length;
            if (result.ReplacedInPlace)
                Console.WriteLine(string.Format(L10n.Cli_NewSize, after));
            else
                Console.WriteLine(string.Format(L10n.Cli_SizeBeforeAfter, new FileInfo(input).Length, after));
        }

        return 0;
    }
}
