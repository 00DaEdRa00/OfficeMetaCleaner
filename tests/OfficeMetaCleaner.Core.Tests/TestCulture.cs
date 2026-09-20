using System.Runtime.CompilerServices;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

/// <summary>
/// Пинит русский язык на весь тестовый прогон: детерминированность
/// не зависит от локали раннера CI (windows-latest — en-US).
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init() => L10n.Apply(AppLanguage.Russian);
}
