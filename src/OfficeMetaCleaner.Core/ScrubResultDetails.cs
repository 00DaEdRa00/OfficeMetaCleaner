namespace OfficeMetaCleaner.Core;

/// <summary>
/// Человекочитаемые строки результата очистки для показа в GUI.
/// Формат строк совпадает с выводом консольной утилиты.
/// </summary>
public static class ScrubResultDetails
{
    public static IReadOnlyList<string> Describe(ScrubResult result)
    {
        var lines = new List<string>();

        foreach (var action in result.Actions)
            lines.Add($"[{action.Kind}] {action.Target} — {action.Detail}");

        foreach (var warning in result.Warnings)
            lines.Add($"! {warning}");

        if (lines.Count == 0)
            lines.Add(L10n.Core_NothingFound);

        return lines;
    }
}
