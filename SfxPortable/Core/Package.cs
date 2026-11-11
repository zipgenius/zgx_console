using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Zgx.Sfx;

public sealed class Options
{
    public string ArchiveName { get; set; } = "archive.zip";
    public string Message { get; set; } = "ZipGenius X";
    public string Sha256 { get; set; } = "";
    public string Target { get; set; } = "";
    public bool AutoStart { get; set; }
    public string FileList { get; set; } = "hidden";
    public bool ShowReadme { get; set; } = true;
    public bool CloseOnSuccess { get; set; } = true;
    public string Destination { get; set; } = "editable";
    public string Subfolder { get; set; } = "";
    public string RunAfter { get; set; } = "";
    public string IconBase64 { get; set; } = "";
}

// A different versioned format from the existing Win32 stub. No wchar_t or native layout.
public static partial class Package
{
    public const int FooterSize = 40;
    private static ReadOnlySpan<byte> Magic => "ZGXSFXP1"u8;

    public static void Write(Stream template, string archive, string output, Options options)
    {
        Validate(options);
        using (var input = File.OpenRead(archive)) options.Sha256 = Convert.ToHexString(SHA256.HashData(input));
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(options);
        using var result = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        template.CopyTo(result);
        long offset = result.Position;
        using (var input = File.OpenRead(archive)) input.CopyTo(result);
        long length = result.Position - offset;
        result.Write(metadata);
        Span<byte> footer = stackalloc byte[FooterSize];
        footer.Clear();
        Magic.CopyTo(footer);
        BinaryPrimitives.WriteInt64LittleEndian(footer[8..], offset);
        BinaryPrimitives.WriteInt64LittleEndian(footer[16..], length);
        BinaryPrimitives.WriteInt64LittleEndian(footer[24..], metadata.Length);
        BinaryPrimitives.WriteInt64LittleEndian(footer[32..], 1);
        result.Write(footer);
        result.Flush(true);
    }

    public static void Validate(Options options)
    {
        if (options.Message.Length is < 1 or > 319 || options.Message.Contains('\0')) throw new ArgumentException("Message: 1 to 319 characters required.");
        if (options.FileList is not ("hidden" or "expanded" or "collapsed")) throw new ArgumentException("Invalid file list mode.");
        if (options.Destination is not ("editable" or "desktop" or "documents" or "downloads" or "pictures" or "music" or "videos" or "favorites")) throw new ArgumentException("Invalid destination.");
        if (options.Subfolder.Length > 127 || (options.Destination == "editable" && options.Subfolder.Length > 0)) throw new ArgumentException("A subfolder requires a fixed destination (127 characters maximum).");
        if (options.Subfolder.Length > 0) SafeRelative(options.Subfolder);
        if (options.RunAfter.Length > 0) SafeRelative(options.RunAfter);
        if (options.RunAfter.Length >= 260) throw new ArgumentException("Run-after path is too long.");
        if (options.ArchiveName != Path.GetFileName(options.ArchiveName) || options.ArchiveName.Contains('\\')) throw new ArgumentException("Invalid archive name.");
    }

    public static string SafeRelative(string name)
    {
        string normalized = name.Replace('\\', '/');
        if (string.IsNullOrEmpty(normalized) || normalized.StartsWith('/') || normalized.Contains(':') ||
            normalized.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(char.IsControl) || part.IndexOfAny(['<', '>', '|', '"', '*', '?']) >= 0 || IsDevice(part)))
            throw new InvalidDataException("Unsafe archive path: " + name);
        return normalized;
    }

    private static bool IsDevice(string part)
    {
        string stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9';
    }
}
