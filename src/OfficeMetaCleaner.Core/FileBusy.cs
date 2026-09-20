using System.Diagnostics;

namespace OfficeMetaCleaner.Core;

/// <summary>
/// Проверка, занят ли файл другим процессом (например, открыт в Word),
/// и ожидание его освобождения.
/// </summary>
public static class FileBusy
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

    private static readonly (string ProcessName, string AppName)[] OfficeApps =
    {
        ("WINWORD", "Word"),
        ("EXCEL", "Excel"),
        ("POWERPNT", "PowerPoint"),
        ("VISIO", "Visio"),
        ("MSACCESS", "Access")
    };

    /// <summary>
    /// True, если файл заблокирован другим процессом.
    /// Отсутствующий файл занятым не считается.
    /// </summary>
    public static bool IsBusy(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Путь к файлу-владельцу Office (~$имя) рядом с документом, если он есть.
    /// Такой файл создаёт сам Word/Excel/PowerPoint у открытого документа.
    /// </summary>
    public static string? FindOwnerFile(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(dir))
                return null;

            var candidate = Path.Combine(dir, "~$" + Path.GetFileName(path));
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Имена приложений Office, запущенных сейчас у пользователя.</summary>
    public static IReadOnlyList<string> RunningOfficeApps()
    {
        var names = new List<string>();
        foreach (var (processName, appName) in OfficeApps)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                    names.Add(appName);
            }
            catch
            {
                // нет прав на опрос процессов — пропускаем
            }
        }

        return names;
    }

    /// <summary>
    /// Ждёт, пока файл освободят (закроют в редакторе): пропадёт блокировка
    /// и файл-владелец Office. Возвращает false, если ожидание отменили.
    /// </summary>
    public static bool WaitUntilFree(string path, TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (FindOwnerFile(path) is null && !IsBusy(path))
                return true;

            if (cancellationToken.WaitHandle.WaitOne(pollInterval))
                return false;
        }
    }

    public static bool WaitUntilFree(string path, CancellationToken cancellationToken = default)
        => WaitUntilFree(path, DefaultPollInterval, cancellationToken);
}
