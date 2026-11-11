#nullable enable annotations
using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZipGenius_X.Utils;

public enum SfxDestinationFolder : uint
{
    Editable = 0, Desktop = 1, Documents = 2, Downloads = 3,
    Pictures = 4, Music = 5, Videos = 6, Favorites = 7
}

public enum SfxFileListMode { Hidden, Expanded, Collapsed }

public sealed class SfxPackageOptions
{
    public bool AutoStart { get; init; }
    public SfxFileListMode FileListMode { get; init; } = SfxFileListMode.Hidden;
    public bool ShowReadme { get; init; }
    public bool CloseOnSuccess { get; init; } = true;
    public string? RunAfterPath { get; init; }
    public string? IconPath { get; init; }
    public string? Message { get; init; }
    public SfxDestinationFolder DestinationFolder { get; init; }
    public string? DestinationSubpath { get; init; }
}

/// <summary>Incorpora archivio e opzioni nello stub compilato, senza eseguire un generatore esterno.</summary>
public static class SfxPackageWriter
{
    private const int FooterSize = 1980;
    private const int RunPathOffset = 40;
    private const int MessageOffset = 560;
    private const int DestinationFolderOffset = 1200;
    private const int DestinationSubpathOffset = 1204;
    private const int ArchiveNameOffset = 1460;
    private const ushort NeutralLanguage = 0;
    private const int IconResource = 3;
    private const int IconGroupResource = 14;
    private static readonly byte[] Magic = [ (byte)'Z', (byte)'G', (byte)'X', (byte)'S', (byte)'F', (byte)'X', (byte)'1', 0 ];

    public static void Create(string archivePath, string outputPath, SfxPackageOptions options) =>
        Create(Path.Combine(AppContext.BaseDirectory, "sfx_stub.exe"), archivePath, outputPath, options);

    public static void Create(string stubTemplatePath, string archivePath, string outputPath, SfxPackageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string template = Path.GetFullPath(stubTemplatePath);
        string archive = Path.GetFullPath(archivePath);
        string output = Path.GetFullPath(outputPath);
        string archiveName = Path.GetFileName(archive);
        string message = options.Message ?? StringUtils.GetResString("mainSfxDefaultMessage");
        if (archiveName.Length is 0 or >= 260 || archiveName.Contains('\0'))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError01"), nameof(archivePath));
        if (!File.Exists(template)) throw new FileNotFoundException(StringUtils.GetResString("sfxWriterError02"), template);
        if (!File.Exists(archive)) throw new FileNotFoundException(StringUtils.GetResString("sfxWriterError03"), archive);
        if (output.Equals(template, StringComparison.OrdinalIgnoreCase) ||
            output.Equals(archive, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError04"), nameof(outputPath));

        uint format = Path.GetExtension(archive).ToLowerInvariant() switch
        {
            ".zip" => 1,
            ".7z" => 2,
            ".rar" => 3,
            _ => throw new ArgumentException(StringUtils.GetResString("sfxWriterError05"), nameof(archivePath))
        };
        string runPath = options.RunAfterPath ?? string.Empty;
        if (runPath.Length >= 260 || (runPath.Length > 0 && !ValidRelativePath(runPath)))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError06"), nameof(options));
        if (!Enum.IsDefined(options.DestinationFolder))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError07"), nameof(options));
        if (!Enum.IsDefined(options.FileListMode))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError08"), nameof(options));
        string destinationSubpath = (options.DestinationSubpath ?? string.Empty).TrimEnd('\\', '/');
        if (options.DestinationFolder == SfxDestinationFolder.Editable && destinationSubpath.Length > 0)
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError09"), nameof(options));
        if (destinationSubpath.Length >= 128 || (destinationSubpath.Length > 0 && !ValidRelativePath(destinationSubpath)))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError10"), nameof(options));
        if (string.IsNullOrWhiteSpace(message) || message.Length >= 320 || message.Contains('\0'))
            throw new ArgumentException(StringUtils.GetResString("sfxWriterError11"), nameof(options));
        if (options.IconPath is { } iconPath && !File.Exists(iconPath))
            throw new FileNotFoundException(StringUtils.GetResString("sfxWriterError12"), iconPath);

        string scratch = Path.Combine(Path.GetTempPath(), "ZipGeniusSfxBuild-" + Guid.NewGuid().ToString("N"));
        string workingExe = Path.Combine(scratch, "stub.exe");
        string outputDirectory = Path.GetDirectoryName(output)!;
        string stagedOutput = Path.Combine(outputDirectory, "." + Path.GetFileName(output) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(scratch);
        try
        {
            File.Copy(template, workingExe);
            if (options.IconPath is { } icon) ReplaceIcon(workingExe, icon);

            long archiveOffset = new FileInfo(workingExe).Length;
            long archiveLength = new FileInfo(archive).Length;
            if (archiveLength <= 0) throw new InvalidDataException(StringUtils.GetResString("sfxWriterError13"));
            using (FileStream package = new(workingExe, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                using FileStream source = new(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
                source.CopyTo(package);
                byte[] footer = BuildFooter(options, message, archiveName, destinationSubpath, format, archiveOffset, archiveLength);
                package.Write(footer);
                package.Flush(true);
            }
            if (new FileInfo(workingExe).Length != archiveOffset + archiveLength + FooterSize)
                throw new IOException(StringUtils.GetResString("sfxWriterError14"));
            File.Copy(workingExe, stagedOutput);
            File.Move(stagedOutput, output, true);
        }
        finally
        {
            if (File.Exists(stagedOutput)) File.Delete(stagedOutput);
            Directory.Delete(scratch, true);
        }
    }

    private static byte[] BuildFooter(SfxPackageOptions options, string message, string archiveName, string destinationSubpath, uint format, long archiveOffset, long archiveLength)
    {
        byte[] footer = new byte[FooterSize];
        Magic.CopyTo(footer, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(8), 4);
        uint flags = (options.AutoStart ? 1u : 0u) | (options.FileListMode != SfxFileListMode.Hidden ? 2u : 0u) |
                     (string.IsNullOrEmpty(options.RunAfterPath) ? 0u : 4u) | (options.ShowReadme ? 8u : 0u) |
                     (options.CloseOnSuccess ? 16u : 0u) | (options.FileListMode == SfxFileListMode.Collapsed ? 32u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(12), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(16), format);
        BinaryPrimitives.WriteUInt64LittleEndian(footer.AsSpan(24), checked((ulong)archiveOffset));
        BinaryPrimitives.WriteUInt64LittleEndian(footer.AsSpan(32), checked((ulong)archiveLength));
        Encoding.Unicode.GetBytes(options.RunAfterPath ?? string.Empty, footer.AsSpan(RunPathOffset, 520));
        Encoding.Unicode.GetBytes(message, footer.AsSpan(MessageOffset, 640));
        BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(DestinationFolderOffset), (uint)options.DestinationFolder);
        Encoding.Unicode.GetBytes(destinationSubpath, footer.AsSpan(DestinationSubpathOffset, 256));
        Encoding.Unicode.GetBytes(archiveName, footer.AsSpan(ArchiveNameOffset, 520));
        return footer;
    }

    private static bool ValidRelativePath(string path)
    {
        if (path.Length == 0 || path[0] is '/' or '\\' || path[^1] is '/' or '\\' || path.Contains(':')) return false;
        foreach (string part in path.Split(['/', '\\']))
        {
            if (part.Length == 0 || part is "." or ".." || part[^1] is '.' or ' ' ||
                part.IndexOfAny(['<', '>', '|', '"', '*', '?']) >= 0) return false;
            foreach (char c in part) if (c < 32) return false;
            string stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '1' and <= '9')) return false;
        }
        return true;
    }

    private static void ReplaceIcon(string executable, string iconPath)
    {
        byte[] icon = File.ReadAllBytes(iconPath);
        if (icon.Length < 22 || BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(0)) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(2)) != 1)
            throw new InvalidDataException(StringUtils.GetResString("sfxWriterError15"));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(4));
        if (count is < 1 or > 128 || icon.Length < 6 + count * 16)
            throw new InvalidDataException(StringUtils.GetResString("sfxWriterError15"));

        byte[] group = new byte[6 + count * 14];
        icon.AsSpan(0, 6).CopyTo(group);
        IntPtr update = BeginUpdateResourceW(executable, false);
        if (update == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        bool commit = false;
        try
        {
            for (int index = 0; index < count; index++)
            {
                int entry = 6 + index * 16;
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(icon.AsSpan(entry + 8));
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(icon.AsSpan(entry + 12));
                if (offset > icon.Length || length > icon.Length - offset)
                    throw new InvalidDataException(StringUtils.GetResString("sfxWriterError16"));
                icon.AsSpan(entry, 12).CopyTo(group.AsSpan(6 + index * 14));
                BinaryPrimitives.WriteUInt16LittleEndian(group.AsSpan(6 + index * 14 + 12), checked((ushort)(index + 1)));
                byte[] image = icon.AsSpan(checked((int)offset), checked((int)length)).ToArray();
                if (!UpdateResourceW(update, (IntPtr)IconResource, (IntPtr)(index + 1), NeutralLanguage, image, checked((uint)image.Length)))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (!UpdateResourceW(update, (IntPtr)IconGroupResource, (IntPtr)1, NeutralLanguage, group, checked((uint)group.Length)))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            commit = true;
        }
        finally
        {
            if (!EndUpdateResourceW(update, !commit)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BeginUpdateResourceW(string fileName, [MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateResourceW(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint dataSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndUpdateResourceW(IntPtr update, [MarshalAs(UnmanagedType.Bool)] bool discard);
}
