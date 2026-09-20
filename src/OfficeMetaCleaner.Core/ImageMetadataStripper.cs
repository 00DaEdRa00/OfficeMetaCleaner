using System.Text;

namespace OfficeMetaCleaner.Core;

/// <summary>
/// Удаляет метаданные из изображений на уровне байтов, не трогая пиксельные данные.
/// Поддерживаются JPEG (APP1/Exif, APP1/XMP, APP13/IPTC), PNG (eXIf, tEXt, iTXt, zTXt, tIME)
/// и GIF (комментарии, XMP application extension).
/// </summary>
public static class ImageMetadataStripper
{
    public static bool IsSupportedExtension(string pathOrName)
    {
        var ext = Path.GetExtension(pathOrName);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Возвращает новые байты; <paramref name="changed"/> показывает, были ли удаления.</summary>
    public static byte[] Strip(byte[] data, out bool changed)
    {
        changed = false;
        if (data is null || data.Length < 8)
            return data ?? Array.Empty<byte>();

        if (data[0] == 0xFF && data[1] == 0xD8)
            return StripJpeg(data, out changed);

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return StripPng(data, out changed);

        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return StripGif(data, out changed);

        return data;
    }

    private static byte[] StripJpeg(byte[] data, out bool changed)
    {
        changed = false;
        var output = new List<byte>(data.Length) { 0xFF, 0xD8 };

        var i = 2;
        while (i + 1 < data.Length)
        {
            if (data[i] != 0xFF)
                break;

            var marker = data[i + 1];

            // fill bytes
            if (marker == 0xFF)
            {
                i++;
                continue;
            }

            // standalone markers without payload
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9))
            {
                output.Add(0xFF);
                output.Add((byte)marker);
                i += 2;
                continue;
            }

            // start of scan — дальше сжатые данные, копируем как есть
            if (marker == 0xDA)
                break;

            if (i + 3 >= data.Length)
                break;

            var length = (data[i + 2] << 8) | data[i + 3];
            if (length < 2 || i + 2 + length > data.Length)
                break;

            var drop = false;
            if (marker == 0xE1 && length >= 8)
            {
                if (StartsWith(data, i + 4, "Exif\0\0") || StartsWith(data, i + 4, "http://ns.adobe.com/xap/"))
                    drop = true;
            }
            else if (marker == 0xED)
            {
                // APP13: Photoshop IRB / IPTC
                drop = true;
            }

            if (drop)
            {
                changed = true;
            }
            else
            {
                for (var k = 0; k < length + 2; k++)
                    output.Add(data[i + k]);
            }

            i += 2 + length;
        }

        for (; i < data.Length; i++)
            output.Add(data[i]);

        return changed ? output.ToArray() : data;
    }

    private static byte[] StripPng(byte[] data, out bool changed)
    {
        changed = false;
        var output = new List<byte>(data.Length);
        for (var k = 0; k < 8; k++)
            output.Add(data[k]);

        var i = 8;
        while (i + 8 <= data.Length)
        {
            var length = (data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3];
            if (length < 0)
                break;

            var type = Encoding.ASCII.GetString(data, i + 4, 4);
            var total = 12 + length;
            if (i + total > data.Length)
                break;

            var drop = type is "eXIf" or "tEXt" or "iTXt" or "zTXt" or "tIME";

            if (drop)
            {
                changed = true;
            }
            else
            {
                for (var k = 0; k < total; k++)
                    output.Add(data[i + k]);
            }

            i += total;
            if (type == "IEND")
                break;
        }

        for (; i < data.Length; i++)
            output.Add(data[i]);

        return changed ? output.ToArray() : data;
    }

    private static byte[] StripGif(byte[] data, out bool changed)
    {
        changed = false;
        var output = new List<byte>(data.Length);

        // header + logical screen descriptor
        var headerLength = 13;
        if (data.Length < headerLength)
            return data;

        for (var k = 0; k < headerLength; k++)
            output.Add(data[k]);

        var packed = data[10];
        if ((packed & 0x80) != 0)
        {
            var globalColorTableSize = 3 * (1 << ((packed & 0x07) + 1));
            for (var k = 0; k < globalColorTableSize && headerLength + k < data.Length; k++)
                output.Add(data[headerLength + k]);
            headerLength += globalColorTableSize;
        }

        var i = headerLength;
        while (i < data.Length)
        {
            if (data[i] == 0x3B) // trailer — остаток копируем ниже
                break;

            if (data[i] == 0x21 && i + 1 < data.Length)
            {
                var label = data[i + 1];

                // проход по цепочке подблоков: [size][data...] до нулевого размера
                var p = i + 2;
                while (p < data.Length && data[p] != 0x00)
                    p += 1 + data[p];
                var end = Math.Min(p + 1, data.Length);

                var drop = label == 0xFE
                           || (label == 0xFF && StartsWith(data, i + 3, "XMP DataXMP"));

                if (drop)
                {
                    changed = true;
                }
                else
                {
                    for (var k = i; k < end; k++)
                        output.Add(data[k]);
                }

                i = end;
                continue;
            }

            output.Add(data[i]);
            i++;
        }

        for (; i < data.Length; i++)
            output.Add(data[i]);

        return changed ? output.ToArray() : data;
    }

    private static bool StartsWith(byte[] data, int offset, string ascii)
    {
        if (offset + ascii.Length > data.Length)
            return false;

        for (var i = 0; i < ascii.Length; i++)
        {
            if (data[offset + i] != (byte)ascii[i])
                return false;
        }

        return true;
    }
}
