namespace OfficeMetaCleaner.Core;

public enum OfficeContainerKind
{
    Ooxml,
    CfbLegacy,
    AceDatabase,
    Unknown
}

public sealed class ScrubOptions
{
    /// <summary>Only report what would change; do not write any output.</summary>
    public bool DryRun { get; set; }

    /// <summary>Replace the source file instead of writing a copy.</summary>
    public bool InPlace { get; set; }

    /// <summary>Also remove _xmlsignatures/* (invalidates existing signatures).</summary>
    public bool RemoveSignatures { get; set; }

    /// <summary>Normalize ZIP entry timestamps to a fixed date.</summary>
    public bool NormalizeTimestamps { get; set; } = true;

    /// <summary>Strip EXIF/XMP/comment metadata from images embedded in the package.</summary>
    public bool StripImageMetadata { get; set; } = true;

    /// <summary>Выставлять privacy-флаги (removePersonalInformation / removePersonalInfoOnSave) при очистке.</summary>
    public bool SetPrivacyFlags { get; set; } = true;

    /// <summary>Fixed timestamp used when NormalizeTimestamps is on.</summary>
    public DateTimeOffset FixedTimestamp { get; set; } = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

public sealed record ScrubAction(string Kind, string Target, string Detail);

public sealed class ScrubResult
{
    public bool Success { get; set; }
    public string InputPath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public bool ReplacedInPlace { get; set; }
    public string Container { get; set; } = "unknown";
    public int DroppedParts { get; set; }
    public int ScrubbedParts { get; set; }
    public List<ScrubAction> Actions { get; } = new();
    public List<string> Warnings { get; } = new();
}

public static partial class MetadataScrubber
{
    private static readonly string[] OoxmlExtensions =
    {
        ".docx", ".docm", ".dotx", ".dotm", ".xlsx", ".xlsm", ".xltx", ".xltm",
        ".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm",
        ".vsdx", ".vsdm", ".vssx", ".vssm", ".vstx", ".vstm"
    };

    private static readonly string[] CfbExtensions = { ".doc", ".xls", ".ppt", ".msg", ".vsd" };

    public static OfficeContainerKind Detect(string path)
    {
        if (AceDbScrubber.HasAceHeader(path))
            return OfficeContainerKind.AceDatabase;

        var ext = Path.GetExtension(path);
        if (AceDbScrubber.IsAccessExtension(path))
            return OfficeContainerKind.AceDatabase;

        if (CfbExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return OfficeContainerKind.CfbLegacy;

        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> head = stackalloc byte[8];
            var read = fs.Read(head);
            if (read >= 4 && head[0] == 0x50 && head[1] == 0x4B && (head[2] == 0x03 || head[2] == 0x05 || head[2] == 0x07))
                return OfficeContainerKind.Ooxml;
            if (read >= 8 && head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0)
                return OfficeContainerKind.CfbLegacy;
        }
        catch
        {
            // fall through
        }

        if (OoxmlExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return OfficeContainerKind.Ooxml;

        return OfficeContainerKind.Unknown;
    }

    public static ScrubResult Scrub(string inputPath, string? outputPath, ScrubOptions? options = null)
    {
        options ??= new ScrubOptions();
        var result = new ScrubResult { InputPath = Path.GetFullPath(inputPath) };

        if (!File.Exists(inputPath))
        {
            result.Warnings.Add(string.Format(L10n.Core_FileNotFound, inputPath));
            return result;
        }

        var kind = Detect(inputPath);
        result.Container = kind switch
        {
            OfficeContainerKind.Ooxml => "OOXML (ZIP/OPC)",
            OfficeContainerKind.CfbLegacy => "OLE/CFB (legacy)",
            OfficeContainerKind.AceDatabase => "Access (ACE/Jet)",
            _ => "unknown"
        };

        switch (kind)
        {
            case OfficeContainerKind.Ooxml:
                return ScrubOoxml(inputPath, outputPath, options, result);

            case OfficeContainerKind.CfbLegacy:
                return CfbScrubber.Scrub(inputPath, outputPath, options);

            case OfficeContainerKind.AceDatabase:
                return AceDbScrubber.Scrub(inputPath, outputPath, options);

            default:
                result.Success = false;
                result.Warnings.Add(L10n.Core_UnknownContainer);
                return result;
        }
    }
}
