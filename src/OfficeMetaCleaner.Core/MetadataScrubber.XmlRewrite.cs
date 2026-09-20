using System.Xml.Linq;

namespace OfficeMetaCleaner.Core;

public static partial class MetadataScrubber
{
    private static readonly HashSet<string> AuthorAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "author", "initials", "date", "lastModifiedBy", "creator",
        "company", "manager", "codeName", "lastPrinted", "template",
        "userName", "userId", "avatarId", "providerId"
    };

    private static string RewritePart(string name, string text, HashSet<string> dropped, ScrubOptions options, ScrubResult result)
    {
        try
        {
            if (name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                return RewriteRels(name, text, dropped, result);

            if (name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                return RewriteContentTypes(text, dropped, result);

            if (options.SetPrivacyFlags)
            {
                if (name.Equals("word/settings.xml", StringComparison.OrdinalIgnoreCase))
                    text = EnsureWordPrivacyFlag(name, text, result);
                else if (name.Equals("ppt/presentation.xml", StringComparison.OrdinalIgnoreCase))
                    text = EnsurePresentationPrivacyFlag(name, text, result);
            }

            if (text.IndexOf("rsid", StringComparison.Ordinal) < 0
                && text.IndexOf("author", StringComparison.Ordinal) < 0
                && text.IndexOf("creator", StringComparison.Ordinal) < 0
                && text.IndexOf("codeName", StringComparison.Ordinal) < 0
                && text.IndexOf("lastModifiedBy", StringComparison.Ordinal) < 0
                && text.IndexOf("initials", StringComparison.Ordinal) < 0)
            {
                return text;
            }

            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var changed = ScrubXml(doc);
            if (changed)
            {
                result.ScrubbedParts++;
                result.Actions.Add(new ScrubAction("scrub-xml", name, L10n.Core_ScrubXmlDetail));
            }

            return Serialize(doc);
        }
        catch (Exception ex)
        {
            result.Warnings.Add(string.Format(L10n.Core_PartFailed, name, ex.Message));
            return text;
        }
    }

    private static bool ScrubXml(XDocument doc)
    {
        var changed = false;

        foreach (var element in doc.Descendants().ToList())
        {
            var local = element.Name.LocalName;
            if (local.StartsWith("rsid", StringComparison.OrdinalIgnoreCase))
            {
                element.Remove();
                changed = true;
                continue;
            }

            foreach (var attribute in element.Attributes().ToList())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;

                var attrLocal = attribute.Name.LocalName;
                if (attrLocal.StartsWith("rsid", StringComparison.OrdinalIgnoreCase)
                    || AuthorAttributeNames.Contains(attrLocal))
                {
                    attribute.Remove();
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static string RewriteContentTypes(string text, HashSet<string> dropped, ScrubResult result)
    {
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        var removed = 0;

        foreach (var element in doc.Root!.Elements().ToList())
        {
            if (!element.Name.LocalName.Equals("Override", StringComparison.OrdinalIgnoreCase))
                continue;

            var partName = element.Attribute("PartName")?.Value;
            if (string.IsNullOrEmpty(partName))
                continue;

            var normalized = Normalize(partName);
            if (dropped.Contains(normalized))
            {
                element.Remove();
                removed++;
            }
        }

        if (removed > 0)
        {
            result.Actions.Add(new ScrubAction("update-content-types", "[Content_Types].xml",
                string.Format(L10n.Core_ContentTypesDetail, removed)));
        }

        return Serialize(doc);
    }

    private static string RewriteRels(string relsName, string text, HashSet<string> dropped, ScrubResult result)
    {
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        if (doc.Root is null)
            return text;

        var removed = 0;
        foreach (var element in doc.Root.Elements().ToList())
        {
            if (!element.Name.LocalName.Equals("Relationship", StringComparison.OrdinalIgnoreCase))
                continue;

            if (element.Attribute("TargetMode")?.Value is string mode
                && mode.Equals("External", StringComparison.OrdinalIgnoreCase))
                continue;

            var target = element.Attribute("Target")?.Value;
            if (string.IsNullOrEmpty(target))
                continue;

            var resolved = ResolveRelationshipTarget(relsName, target);
            if (dropped.Contains(resolved))
            {
                element.Remove();
                removed++;
            }
        }

        if (removed > 0)
        {
            result.Actions.Add(new ScrubAction("update-rels", relsName,
                string.Format(L10n.Core_RelsDetail, removed)));
        }

        return Serialize(doc);
    }

    private static string ResolveRelationshipTarget(string relsName, string target)
    {
        if (target.StartsWith("/", StringComparison.Ordinal))
            return Normalize(target);

        var owner = OwnerPartOfRels(relsName);
        var baseDir = "";
        var slash = owner.LastIndexOf('/');
        if (slash >= 0)
            baseDir = owner[..slash];

        var combined = baseDir.Length == 0 ? target : baseDir + "/" + target;
        return Normalize(combined);
    }

    private static string OwnerPartOfRels(string relsName)
    {
        var idx = relsName.LastIndexOf("/_rels/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return string.Empty;

        var dir = relsName[..idx];
        var file = relsName[(idx + "/_rels/".Length)..];
        var ownerFile = file.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
            ? file[..^".rels".Length]
            : file;
        return dir.Length == 0 ? ownerFile : dir + "/" + ownerFile;
    }
}
