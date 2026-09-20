using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.Tests;

public sealed class ResultDetailsTests
{
    [Fact]
    public void Describe_ListsEveryAction()
    {
        var result = new ScrubResult { Container = "OOXML (ZIP/OPC)", Success = true };
        result.Actions.Add(new ScrubAction("drop-part", "docProps/core.xml", "Метаданные контейнера удалены"));
        result.Actions.Add(new ScrubAction("scrub-xml", "word/document.xml", "Удалены атрибуты авторства/rsid"));

        var lines = ScrubResultDetails.Describe(result);

        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.Contains("drop-part") && l.Contains("docProps/core.xml"));
        Assert.Contains(lines, l => l.Contains("scrub-xml") && l.Contains("word/document.xml"));
    }

    [Fact]
    public void Describe_AppendsWarnings()
    {
        var result = new ScrubResult { Success = true };
        result.Warnings.Add("Результат записывается поверх исходного файла.");

        var lines = ScrubResultDetails.Describe(result);

        Assert.Single(lines);
        Assert.StartsWith("!", lines[0]);
        Assert.Contains("поверх исходного файла", lines[0]);
    }

    [Fact]
    public void Describe_EmptyResult_ExplainsNothingFound()
    {
        var lines = ScrubResultDetails.Describe(new ScrubResult { Success = true });

        Assert.Single(lines);
        Assert.NotEmpty(lines[0]);
    }
}
