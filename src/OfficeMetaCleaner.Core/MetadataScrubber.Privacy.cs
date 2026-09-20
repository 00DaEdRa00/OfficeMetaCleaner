using System.Xml.Linq;

namespace OfficeMetaCleaner.Core;

public static partial class MetadataScrubber
{
    /// <summary>
    /// Включает в word/settings.xml флаг w:removePersonalInformation:
    /// Word после этого сам вычищает сведения о пользователе при каждом сохранении,
    /// файл остаётся чистым и после правок. Нетронутым возвращается как есть.
    /// </summary>
    private static string EnsureWordPrivacyFlag(string name, string text, ScrubResult result)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        var root = doc.Root;
        if (root is null || root.Name != w + "settings")
            return text;

        var element = root.Element(w + "removePersonalInformation");
        if (element is null)
        {
            root.Add(new XElement(w + "removePersonalInformation"));
        }
        else
        {
            var val = element.Attribute(w + "val");
            if (val is null || !IsOffValue(val.Value))
                return text;

            val.Value = "true";
        }

        result.ScrubbedParts++;
        result.Actions.Add(new ScrubAction("set-privacy-flag", name,
            "Включено удаление сведений о пользователе при сохранении"));
        return Serialize(doc);
    }

    /// <summary>
    /// Включает в ppt/presentation.xml атрибут removePersonalInfoOnSave="1":
    /// PowerPoint после этого сам вычищает сведения о пользователе при каждом сохранении.
    /// </summary>
    private static string EnsurePresentationPrivacyFlag(string name, string text, ScrubResult result)
    {
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        var root = doc.Root;
        if (root is null || !root.Name.LocalName.Equals("presentation", StringComparison.Ordinal))
            return text;

        var attribute = root.Attribute("removePersonalInfoOnSave");
        if (attribute is not null && IsOnValue(attribute.Value))
            return text;

        if (attribute is null)
            root.SetAttributeValue("removePersonalInfoOnSave", "1");
        else
            attribute.Value = "1";

        result.ScrubbedParts++;
        result.Actions.Add(new ScrubAction("set-privacy-flag", name,
            "Включено удаление сведений о пользователе при сохранении"));
        return Serialize(doc);
    }

    private static bool IsOffValue(string value)
        => value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.Ordinal)
            || value.Equals("off", StringComparison.OrdinalIgnoreCase);

    private static bool IsOnValue(string value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}
