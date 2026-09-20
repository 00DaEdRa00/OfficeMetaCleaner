using System.Text;

namespace OfficeMetaCleaner.Core;

/// <summary>
/// Очистка метаданных баз Microsoft Access (.accdb / .mdb и производные).
/// Формат ACE/Jet — не ZIP и не OLE/CFB, поэтому правим свойства через DAO
/// (DAO.DBEngine.120), а затем уплотняем базу, чтобы старые значения не остались
/// в освобождённых страницах.
/// </summary>
public static class AceDbScrubber
{
    private static readonly string[] AceExtensions =
    {
        ".accdb", ".accde", ".accdr", ".accdt", ".mdb", ".mde"
    };

    /// <summary>Служебные свойства DAO, которые нельзя удалять.</summary>
    private static readonly HashSet<string> BuiltInProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name", "Owner", "UserName", "Permissions", "AllPermissions",
        "Container", "DateCreated", "LastUpdated"
    };

    /// <summary>Свойства приложения в MSysDb, которые могут содержать локальные пути.</summary>
    private static readonly HashSet<string> OptionalAppProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppTitle", "AppIcon"
    };

    private static readonly byte[] AceMagic = Encoding.ASCII.GetBytes("Standard ACE DB");
    private static readonly byte[] JetMagic = Encoding.ASCII.GetBytes("Standard Jet DB");

    public static bool IsAccessExtension(string path)
        => AceExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Проверка по сигнатуре файла: "Standard ACE DB" / "Standard Jet DB" на смещении 4.</summary>
    public static bool HasAceHeader(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var head = new byte[20];
            var read = stream.Read(head, 0, head.Length);
            if (read < 20)
                return false;

            return Matches(head, 4, AceMagic) || Matches(head, 4, JetMagic);
        }
        catch
        {
            return false;
        }
    }

    private static bool Matches(byte[] data, int offset, byte[] pattern)
    {
        if (offset + pattern.Length > data.Length)
            return false;

        for (var i = 0; i < pattern.Length; i++)
        {
            if (data[offset + i] != pattern[i])
                return false;
        }

        return true;
    }

    public static ScrubResult Scrub(string inputPath, string? outputPath, ScrubOptions options)
    {
        var fullInput = Path.GetFullPath(inputPath);
        var result = new ScrubResult
        {
            InputPath = fullInput,
            Container = "Access (ACE/Jet)"
        };

        var finalTarget = ResolveTargetPath(fullInput, outputPath, options);
        finalTarget = MetadataScrubber.EnsureUniqueTarget(finalTarget, fullInput);
        var replacesSource = string.Equals(finalTarget, fullInput, StringComparison.OrdinalIgnoreCase);
        if (replacesSource)
        {
            result.ReplacedInPlace = true;
            result.Warnings.Add(L10n.Core_InPlaceWarning);
        }

        if (!OperatingSystem.IsWindows())
        {
            result.Success = false;
            result.Warnings.Add(L10n.Core_AceWindowsOnly);
            return result;
        }

        var engineType = Type.GetTypeFromProgID("DAO.DBEngine.120")
                         ?? Type.GetTypeFromProgID("DAO.DBEngine.36");

        if (engineType is null)
        {
            result.Success = false;
            result.Warnings.Add(L10n.Core_AceNoDriver);
            return result;
        }

        object? engine = null;
        try
        {
            engine = Activator.CreateInstance(engineType);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Warnings.Add(string.Format(L10n.Core_AceLaunchFailed, ex.Message));
            return result;
        }

        if (engine is null)
        {
            result.Success = false;
            result.Warnings.Add(L10n.Core_AceNoEngine);
            return result;
        }

        try
        {
            dynamic dao = engine;

            if (options.DryRun)
            {
                dynamic database = dao.OpenDatabase(fullInput, false, true);
                try
                {
                    ReportProperties(database, result);
                }
                finally
                {
                    database.Close();
                }

                result.Success = true;
                return result;
            }

            var dir = Path.GetDirectoryName(finalTarget);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempCopy = finalTarget + ".omc-src-" + Guid.NewGuid().ToString("N");
            var tempCompacted = finalTarget + ".omc-dst-" + Guid.NewGuid().ToString("N");

            try
            {
                File.Copy(fullInput, tempCopy, overwrite: true);

                dynamic database = dao.OpenDatabase(tempCopy, false, false);
                try
                {
                    ClearProperties(database, result);
                    // уплотнение выполняется после закрытия базы
                }
                finally
                {
                    database.Close();
                }

                dao.CompactDatabase(tempCopy, tempCompacted);

                result.Success = true;
                result.OutputPath = finalTarget;
            }
            finally
            {
                MetadataScrubber.TryDelete(tempCopy);
            }

            File.Move(tempCompacted, finalTarget, overwrite: true);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Warnings.Add(string.Format(L10n.Core_AceError, ex.Message));
        }
        finally
        {
            if (engine is not null && System.Runtime.InteropServices.Marshal.IsComObject(engine))
                System.Runtime.InteropServices.Marshal.ReleaseComObject(engine);
        }

        return result;
    }

    private static void ReportProperties(dynamic database, ScrubResult result)
    {
        dynamic containers = database.Containers;
        dynamic databases = containers["Databases"];
        dynamic documents = databases.Documents;

        foreach (dynamic document in documents)
        {
            var name = (string)document.Name;
            var removable = CollectRemovable(document, name);

            foreach (var property in removable)
            {
                result.Actions.Add(new ScrubAction("would-clear-ace-property", $"{name}.{property}",
                    L10n.Core_AceWouldDelete));
            }
        }
    }

    private static void ClearProperties(dynamic database, ScrubResult result)
    {
        dynamic containers = database.Containers;
        dynamic databases = containers["Databases"];
        dynamic documents = databases.Documents;

        foreach (dynamic document in documents)
        {
            var name = (string)document.Name;
            var removable = CollectRemovable(document, name);

            foreach (var property in removable)
            {
                try
                {
                    document.Properties.Delete(property);
                    result.ScrubbedParts++;
                    result.Actions.Add(new ScrubAction("clear-ace-property", $"{name}.{property}",
                        L10n.Core_AceDeleted));
                }
                catch (Exception ex)
                {
                    result.Warnings.Add(string.Format(L10n.Core_AceDeleteFailed, name, property, ex.Message));
                }
            }
        }
    }

    private static List<string> CollectRemovable(dynamic document, string documentName)
    {
        var names = new List<string>();

        foreach (dynamic property in document.Properties)
        {
            var name = (string)property.Name;

            if (BuiltInProperties.Contains(name))
                continue;

            var isMetadataDocument = documentName.Equals("SummaryInfo", StringComparison.OrdinalIgnoreCase)
                                     || documentName.Equals("UserDefined", StringComparison.OrdinalIgnoreCase);

            if (isMetadataDocument || OptionalAppProperties.Contains(name))
                names.Add(name);
        }

        return names;
    }

    private static string ResolveTargetPath(string fullInput, string? outputPath, ScrubOptions options)
    {
        if (options.InPlace)
            return fullInput;

        if (!string.IsNullOrEmpty(outputPath))
            return Path.GetFullPath(outputPath);

        var dir = Path.GetDirectoryName(fullInput) ?? ".";
        return Path.GetFullPath(Path.Combine(dir, "cleaned", Path.GetFileName(fullInput)));
    }
}
