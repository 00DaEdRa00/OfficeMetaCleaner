using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Cli;

internal static partial class Program
{
    private static readonly string[] OfficeExtensions =
    {
        ".docx", ".docm", ".dotx", ".dotm", ".xlsx", ".xlsm", ".xltx", ".xltm",
        ".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm",
        ".doc", ".xls", ".ppt",
        ".accdb", ".accde", ".accdr", ".accdt", ".mdb", ".mde",
        ".vsdx", ".vsdm", ".vssx", ".vssm", ".vstx", ".vstm", ".vsd"
    };

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* non-console host */ }

        // язык разбираем до любого вывода, чтобы и help, и ошибки парсинга были локализованы
        var langPref = AppLanguage.Auto;
        string? langRaw = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--lang", StringComparison.Ordinal))
                continue;
            langRaw = i + 1 < args.Length ? args[i + 1] : string.Empty;
        }

        if (langRaw is not null && !TryParseLanguage(langRaw, out langPref))
        {
            L10n.Apply(AppLanguage.Auto);
            Console.Error.WriteLine(string.Format(L10n.Cli_InvalidLang, langRaw));
            PrintUsage();
            return 1;
        }

        L10n.Apply(langPref);

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (!string.Equals(args[0], "clean", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(string.Format(L10n.Cli_UnknownCommand, args[0]));
            PrintUsage();
            return 1;
        }

        string? input = null;
        string? outDir = null;
        var dryRun = false;
        var recursive = false;
        var removeSignatures = false;
        var stripImages = true;
        var inPlace = false;
        var wait = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine(L10n.Cli_OutNeedsValue);
                        return 1;
                    }
                    outDir = args[++i];
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--recursive":
                    recursive = true;
                    break;
                case "--remove-signatures":
                    removeSignatures = true;
                    break;
                case "--strip-images":
                    stripImages = true;
                    break;
                case "--keep-images":
                    stripImages = false;
                    break;
                case "--in-place":
                    inPlace = true;
                    break;
                case "--wait":
                    wait = true;
                    break;
                case "--lang":
                    i++; // значение уже разобрано в пре-скане выше
                    break;
                default:
                    input ??= args[i];
                    break;
            }
        }

        if (input is null)
        {
            Console.Error.WriteLine(L10n.Cli_NoInput);
            PrintUsage();
            return 1;
        }

        if (inPlace && outDir is not null)
        {
            Console.Error.WriteLine(L10n.Cli_InPlaceOutConflict);
            return 1;
        }

        var options = new ScrubOptions
        {
            DryRun = dryRun,
            InPlace = inPlace,
            RemoveSignatures = removeSignatures,
            StripImageMetadata = stripImages
        };

        var exitCode = 0;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        if (File.Exists(input))
        {
            var output = (outDir is null || inPlace) ? null : Path.Combine(outDir, Path.GetFileName(input));
            if (wait && !WaitForFile(input, cts.Token))
                return 1;
            exitCode = RunOne(input, output, options);
        }
        else if (Directory.Exists(input))
        {
            var search = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(input, "*", search)
                .Where(f => OfficeExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                Console.WriteLine(L10n.Cli_NoFiles);
                return 0;
            }

            Console.WriteLine(string.Format(L10n.Cli_FoundFiles, files.Count));

            // с --wait сначала свободные, занятые — во вторую очередь с ожиданием закрытия
            var free = new List<string>();
            var busy = new List<string>();
            if (wait)
            {
                foreach (var f in files)
                    (FileBusy.FindOwnerFile(f) is null && !FileBusy.IsBusy(f) ? free : busy).Add(f);

                if (busy.Count > 0)
                    Console.WriteLine(string.Format(L10n.Cli_BusyFiles, busy.Count));
            }
            else
            {
                free.AddRange(files);
            }

            foreach (var file in free)
            {
                string? output = null;
                if (outDir is not null && !inPlace)
                {
                    var rel = Path.GetRelativePath(input, file);
                    output = Path.Combine(outDir, rel);
                }

                var code = RunOne(file, output, options);
                if (code != 0)
                    exitCode = code;
            }

            foreach (var file in busy)
            {
                if (!WaitForFile(file, cts.Token))
                {
                    exitCode = 1;
                    continue;
                }

                string? output = null;
                if (outDir is not null && !inPlace)
                {
                    var rel = Path.GetRelativePath(input, file);
                    output = Path.Combine(outDir, rel);
                }

                var code = RunOne(file, output, options);
                if (code != 0)
                    exitCode = code;
            }
        }
        else
        {
            Console.Error.WriteLine(string.Format(L10n.Cli_PathNotFound, input));
            return 1;
        }

        return exitCode;
    }

    private static bool TryParseLanguage(string raw, out AppLanguage language)
    {
        switch (raw.ToLowerInvariant())
        {
            case "ru": language = AppLanguage.Russian; return true;
            case "en": language = AppLanguage.English; return true;
            case "auto": language = AppLanguage.Auto; return true;
            default: language = AppLanguage.Auto; return false;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(L10n.Cli_Usage);
        Console.WriteLine();
        Console.WriteLine(L10n.Cli_HelpAbout);
        Console.WriteLine(L10n.Cli_HelpFormats);
        Console.WriteLine(L10n.Cli_HelpAccess);
        Console.WriteLine(L10n.Cli_HelpOut);
        Console.WriteLine(L10n.Cli_HelpInPlace);
        Console.WriteLine(L10n.Cli_HelpDryRun);
        Console.WriteLine(L10n.Cli_HelpRecursive);
        Console.WriteLine(L10n.Cli_HelpSignatures);
        Console.WriteLine(L10n.Cli_HelpStrip);
        Console.WriteLine(L10n.Cli_HelpKeep);
        Console.WriteLine(L10n.Cli_HelpWait);
        Console.WriteLine(L10n.Cli_HelpLang);
    }
}
