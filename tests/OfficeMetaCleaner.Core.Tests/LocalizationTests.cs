using System.Reflection;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class LocalizationTests
{
    private static readonly object Gate = new();

    [Fact]
    public void AllKeys_ExistInBothLanguages_AndNonEmpty()
    {
        Assert.NotEmpty(L10n.AllKeys);

        foreach (var key in L10n.AllKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(L10n.Get(key, "ru")), $"ru:{key}");
            Assert.False(string.IsNullOrWhiteSpace(L10n.Get(key, "en")), $"en:{key}");
        }
    }

    [Fact]
    public void AllProperties_MatchAllKeys_OneToOne()
    {
        var props = typeof(L10n).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.Name != nameof(L10n.Language))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var keys = L10n.AllKeys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.Equal(keys, props);
    }

    [Fact]
    public void Apply_SwitchesLanguage_AndRestoresRussian()
    {
        lock (Gate)
        {
            L10n.Apply(AppLanguage.English);
            try
            {
                Assert.Equal("en", L10n.Language);
                Assert.Equal("Done", L10n.StatusDone);

                var lines = ScrubResultDetails.Describe(new ScrubResult { Success = true });
                var line = Assert.Single(lines);
                Assert.Contains("Nothing to clean", line);
            }
            finally
            {
                L10n.Apply(AppLanguage.Russian);
            }

            Assert.Equal("ru", L10n.Language);
            Assert.Equal("Готово", L10n.StatusDone);
        }
    }
}
