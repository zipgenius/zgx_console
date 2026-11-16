using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Lzw;
using SharpCompress.Compressors.Xz;

namespace zg;

partial class Program
{
    private static bool IsStandaloneCompressedStream(string path) =>
        !TarArchiveEditor.IsTarArchivePath(path) &&
        new[] { ".bz2", ".xz", ".z" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static Stream OpenSimpleCompressedStream(string path)
    {
        Stream input = File.OpenRead(path);
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".bz2" => new BZip2InputStream(input),
                ".xz" => new XZStream(input),
                ".z" => new LzwInputStream(input) { IsStreamOwner = true },
                _ => throw new NotSupportedException("Unsupported compressed stream.")
            };
        }
        catch { input.Dispose(); throw; }
    }

    private static void ValidateSimpleStreamHeader(string path)
    {
        using var input = File.OpenRead(path);
        Span<byte> header = stackalloc byte[6];
        int length = input.Read(header);
        bool valid = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".bz2" => length >= 4 && header[..3].SequenceEqual("BZh"u8) && header[3] is >= (byte)'1' and <= (byte)'9',
            ".xz" => length >= 6 && header.SequenceEqual(new byte[] { 0xfd, 0x37, 0x7a, 0x58, 0x5a, 0x00 }),
            ".z" => length >= 3 && header[0] == 0x1f && header[1] == 0x9d,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Invalid compressed stream header.");
    }

    private static int TestSimpleArchive(string path)
    {
        if (Path.GetExtension(path).Equals(".deb", StringComparison.OrdinalIgnoreCase))
        {
            using var input = File.OpenRead(path);
            byte[] signature = new byte[8];
            input.ReadExactly(signature);
            if (!signature.AsSpan().SequenceEqual("!<arch>\n"u8)) throw new InvalidDataException("Invalid Debian package header.");
            var members = ReadArList(input, out _);
            // ar has no data checksum. Read every declared byte and validate structure;
            // nested control/data archives are separate files and are not recursively tested.
            byte[] buffer = new byte[81920];
            foreach (var member in members)
            {
                input.Position = member.DataOffset;
                long remaining = checked((long)member.Size);
                while (remaining > 0) { int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (n == 0) throw new EndOfStreamException(); remaining -= n; }
            }
            return members.Count;
        }
        using var stream = OpenSimpleCompressedStream(path);
        stream.CopyTo(Stream.Null);
        return 1;
    }

    private static ExitCode ExtractSimpleArchive(ExtractCommandOptions options)
    {
        string stagingRoot = Path.Combine(TempPath, "zgshell-extract-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(stagingRoot);
            int selected = 0;
            if (Path.GetExtension(options.ArchivePath).Equals(".deb", StringComparison.OrdinalIgnoreCase))
            {
                using var input = File.OpenRead(options.ArchivePath);
                byte[] signature = new byte[8]; input.ReadExactly(signature);
                if (!signature.AsSpan().SequenceEqual("!<arch>\n"u8)) throw new InvalidDataException("Invalid Debian package header.");
                var entries = ReadArList(input, out _);
                foreach (var entry in entries)
                {
                    if (!ShouldExtractStagedFile(entry.Name, FileAttributes.Normal, options)) continue;
                    string outputPath = GetSafeExtractionPath(stagingRoot, GetExtractedEntryPath(entry.Name, options.RestorePaths));
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    input.Position = entry.DataOffset;
                    using var output = File.Create(outputPath);
                    byte[] buffer = new byte[81920]; long remaining = checked((long)entry.Size);
                    while (remaining > 0) { int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (n == 0) throw new EndOfStreamException(); output.Write(buffer, 0, n); remaining -= n; }
                    selected++;
                }
            }
            else
            {
                string name = Path.GetFileNameWithoutExtension(options.ArchivePath);
                if (ShouldExtractStagedFile(name, FileAttributes.Normal, options))
                {
                    string path = GetSafeExtractionPath(stagingRoot, GetExtractedEntryPath(name, options.RestorePaths));
                    using var input = OpenSimpleCompressedStream(options.ArchivePath);
                    using var output = File.Create(path);
                    input.CopyTo(output);
                    selected++;
                }
            }
            if (selected == 0) return ExitCode.ZeroItemsArchive;
            int count = CommitStagedExtraction(stagingRoot, options);
            if (!IsSilent) Console.WriteLine(GetLocalizedString("ExtractCompleted", "Extracted {0} file(s) to {1}", count, options.DestinationPath));
            return ExitCode.Success;
        }
        finally { DeleteExtractionStagingFolder(stagingRoot); }
    }
}
