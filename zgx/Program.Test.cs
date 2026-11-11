using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using ZipFile = ICSharpCode.SharpZipLib.Zip.ZipFile;

namespace zg;

partial class Program
{
    private sealed class TestPasswordException : Exception { }

    private static ExitCode TestArchive(string[] args)
    {
        try
        {
            if (args.Length < 2 || args[1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(GetLocalizedString("TestSyntax", "Syntax: zg -test archive [Ppassword] [--verbose] [--silent]"));
            string path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1]));
            string password = "";
            foreach (string option in args.Skip(2))
            {
                if (option.StartsWith("--password=", StringComparison.OrdinalIgnoreCase)) password = option[11..];
                else if (option.StartsWith("--", StringComparison.Ordinal) && IsGlobalOption(option[2..])) { }
                else if (option.Length > 1 && char.ToUpperInvariant(option[0]) == 'P') password = option[1..] == "0" ? "" : option[1..];
                else throw new ArgumentException(GetLocalizedString("TestUnknownOption", "Unknown test option. Use zg -help -test."));
            }
            if (!File.Exists(path))
            {
                Console.Error.WriteLine(GetLocalizedString("TestMissing", "Archive not found: {0}", path));
                return ExitCode.FileDoesNotExists;
            }
            var safety = CheckZipBomb(path);
            if (safety.Blocked) return (ExitCode)safety.ExitCode;
            if (Path.GetExtension(path).Equals(".sqx", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(GetLocalizedString("TestSqxUnsupported", "SQX integrity testing is not available. No extraction was performed."));

            int attempts = 0;
            while (true)
            {
                try
                {
                    int tested = TestArchiveContents(path, password);
                    if (!IsSilent)
                        Console.WriteLine(GetLocalizedString("TestSuccess", "Test completed: {0} entries checked. No errors found.", tested));
                    return ExitCode.Success;
                }
                catch (Exception ex) when (ex is TestPasswordException or System.Security.Cryptography.CryptographicException ||
                    ex.GetType().Name.Contains("Password", StringComparison.OrdinalIgnoreCase))
                {
                    if (CanPromptForExtractionPassword() && attempts < 3)
                        password = ReadExtractionPassword(path, ++attempts);
                    else
                    {
                        Console.Error.WriteLine(GetLocalizedString("TestPassword", "Protected data could not be verified: supply the correct password. The encrypted data may also be damaged."));
                        return ExitCode.ArchiveTestPassword;
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(GetLocalizedString("TestDataError", "Damaged archive headers or data."), ex);
                }
            }
        }
        catch (OperationCanceledException) { return ExitCode.CanceledByUser; }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.UnknownError;
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine(GetLocalizedString("TestUnsupported", "This archive cannot be tested with the available engine: {0}", ex.Message));
            return ExitCode.ArchiveTestUnsupported;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException or DllNotFoundException ||
            ex is IOException and not EndOfStreamException)
        {
            Console.Error.WriteLine(GetLocalizedString("TestAccess", "Unable to access the archive or test engine: {0}", ex.Message));
            return ExitCode.IOError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(GetLocalizedString("TestFailed", "Integrity test failed: {0}", ex.Message));
            return ExitCode.ArchiveTestFailed;
        }
    }

    private static int TestArchiveContents(string path, string password)
    {
        // Use a managed ZIP reader on every OS. This validates both headers and
        // data (including ZIP CRC/AES authentication), without writing entries.
        bool zip = IsZipFile(Path.GetExtension(path));
        using (var input = File.OpenRead(path))
        {
            Span<byte> header = stackalloc byte[4];
            if (input.Read(header) == 4 && header[0] == 'P' && header[1] == 'K') zip = true;
        }
        if (zip) return TestZipContents(path, password);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        bool knownArchive = new[] { ".7z", ".rar", ".cab", ".wim", ".iso", ".dmg", ".rpm", ".deb", ".arj", ".cpio", ".tar", ".gz", ".gzip", ".bz2", ".xz", ".z" }.Contains(extension)
            || TarArchiveEditor.IsTarArchivePath(path);
        if (!knownArchive && !ArchiveFactory.IsArchive(path, out _))
            throw new NotSupportedException(GetLocalizedString("TestNotArchive", "The file is not a recognised archive."));
        if (IsWindows())
        {
            bool encryptedHeaders = HasEncryptedSevenZipTestHeader(path);
            bool encrypted = false;
            try
            {
                SevenZip.SevenZipBase.SetLibraryPath(FileUtils.GetSevenZipLibs());
                using var archive = string.IsNullOrEmpty(password)
                    ? new SevenZip.SevenZipExtractor(path) : new SevenZip.SevenZipExtractor(path, password);
                encrypted = archive.ArchiveFileData.Any(entry => entry.Encrypted);
                if (encrypted && password.Length == 0) throw new TestPasswordException();
                using var progress = CreateAddProgress(100, GetLocalizedString("TestRunning", "Testing archive..."));
                archive.Extracting += (_, e) => progress?.Tick(e.PercentDone);
                if (!archive.Check())
                {
                    if (encrypted) throw new TestPasswordException();
                    throw new InvalidDataException(GetLocalizedString("TestDataError", "Damaged archive headers or data."));
                }
                progress?.Tick(100);
                if (IsVerbose && !IsSilent)
                    foreach (var entry in archive.ArchiveFileData) Console.WriteLine("OK: " + entry.FileName);
                return archive.ArchiveFileData.Count;
            }
            catch (Exception ex) when ((encryptedHeaders || encrypted) && ex is not IOException && ex is not UnauthorizedAccessException)
            {
                throw new TestPasswordException();
            }
        }

        // The forward-only reader also handles solid 7Z/RAR archives. Never use
        // TarArchiveEditor here: opening compressed TAR there may write a temp TAR.
        if (!ArchiveFactory.IsArchive(path, out _) && extension is not (".7z" or ".rar" or ".tar" or ".gz" or ".gzip"))
            throw new NotSupportedException(GetLocalizedString("TestManagedFormat", "This format is not supported by the test engine on this platform."));
        using var managed = OpenManagedTestArchive(path, password);
        int totalEntries = managed.Entries.Count();
        using var reader = managed.ExtractAllEntries();
        using var bar = CreateAddProgress(100, GetLocalizedString("TestRunning", "Testing archive..."));
        byte[] buffer = new byte[81920];
        int count = 0;
        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            count++;
            if (entry.IsDirectory) continue;
            if (entry.IsEncrypted && password.Length == 0) throw new TestPasswordException();
            try
            {
                using var stream = reader.OpenEntryStream();
                var crc = new Crc32();
                long length = 0;
                int bytes;
                while ((bytes = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    length = checked(length + bytes);
                    if (entry.Size >= 0 && length > entry.Size) throw new InvalidDataException(GetLocalizedString("TestEntryTooLarge", "Entry exceeds its declared size."));
                    crc.Update(new ArraySegment<byte>(buffer, 0, bytes));
                    double fraction = entry.Size > 0 ? (double)length / entry.Size : 0;
                    bar?.Tick(Math.Min(100, (int)((count - 1 + fraction) * 100 / Math.Max(1, totalEntries))), entry.Key ?? "");
                }
                if (entry.Size >= 0 && length != entry.Size) throw new InvalidDataException(GetLocalizedString("TestTruncatedEntry", "Truncated archive entry: ") + entry.Key);
                if (managed.Type is ArchiveType.Zip or ArchiveType.Rar or ArchiveType.SevenZip or ArchiveType.GZip &&
                    unchecked((uint)entry.Crc) != unchecked((uint)crc.Value))
                    throw new InvalidDataException(GetLocalizedString("TestCrcMismatch", "CRC mismatch: ") + entry.Key);
            }
            catch (Exception ex) when (entry.IsEncrypted && ex is not UnauthorizedAccessException)
            {
                throw new TestPasswordException();
            }
            if (IsVerbose && !IsSilent) Console.WriteLine("OK: " + entry.Key);
            bar?.Tick(Math.Min(100, count * 100 / Math.Max(1, totalEntries)), entry.Key ?? "");
        }
        bar?.Tick(100);
        return count;
    }

    private static IArchive OpenManagedTestArchive(string path, string password)
    {
        bool encryptedHeader = HasEncryptedSevenZipTestHeader(path);
        try
        {
            var archive = ArchiveFactory.Open(path, new ReaderOptions { Password = password });
            try { _ = archive.Entries.Count(); return archive; }
            catch { archive.Dispose(); throw; }
        }
        catch (Exception ex) when (encryptedHeader && ex is not IOException && ex is not UnauthorizedAccessException)
        {
            // Some managed 7Z readers report only "Data Error" for locked headers.
            // Identify the validated encrypted-header metadata, rather than treating
            // every data error as a password failure (which would hide real damage).
            throw new TestPasswordException();
        }
    }

    private static bool HasEncryptedSevenZipTestHeader(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length < 32) return false;
        byte[] start = new byte[32]; file.ReadExactly(start);
        if (!start.AsSpan(0, 6).SequenceEqual(new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c })) return false;
        var crc = new Crc32(); crc.Update(new ArraySegment<byte>(start, 12, 20));
        if ((uint)crc.Value != System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(start.AsSpan(8))) return false;
        long offset = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(start.AsSpan(12));
        long size = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(start.AsSpan(20));
        if (offset < 0 || size is < 1 or > 1024 * 1024 || offset > file.Length - 32 || size > file.Length - 32 - offset) return false;
        file.Position = 32 + offset;
        byte[] header = new byte[(int)size]; file.ReadExactly(header);
        crc.Reset(); crc.Update(header);
        if ((uint)crc.Value != System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(start.AsSpan(28))) return false;
        // Encoded 7Z header; AES256/SHA256 coder descriptor with properties.
        return header[0] == 0x17 && header.AsSpan().IndexOf(new byte[] { 0x24, 0x06, 0xf1, 0x07, 0x01 }) >= 0;
    }

    private static int TestZipContents(string path, string password)
    {
        using var archive = new ZipFile(path);
        archive.Password = password;
        bool encrypted = archive.Cast<ZipEntry>().Any(entry => entry.IsCrypted);
        if (encrypted && password.Length == 0) throw new TestPasswordException();
        // DotNetZip-generated AES archives can carry differing local/central
        // extraction versions. SharpZipLib's strict header test rejects these
        // before reading data. Use the originating AES-capable engine to check
        // decryption, CRC and authentication instead of falsely reporting damage.
        if (archive.Cast<ZipEntry>().Any(entry => entry.AESKeySize > 0))
            return TestAesZipContents(path, password);
        using var progress = CreateAddProgress(100, GetLocalizedString("TestRunning", "Testing archive..."));
        int completed = 0;
        string? failure = null;
        bool valid = archive.TestArchive(true, TestStrategy.FindFirstError, (status, message) =>
        {
            if (message is not null)
            {
                failure = message;
                if (IsVerbose && !IsSilent) Console.Error.WriteLine(message);
            }
            if (status.Operation == TestOperation.EntryComplete)
            {
                completed++;
                if (status.EntryValid && IsVerbose && !IsSilent) Console.WriteLine("OK: " + status.Entry?.Name);
            }
            double fraction = status.Entry is { Size: > 0 } entry ? Math.Min(1, (double)status.BytesTested / entry.Size) : 0;
            progress?.Tick(Math.Min(100, (int)((completed + fraction) * 100 / Math.Max(1, archive.Count))), status.Entry?.Name ?? "");
        });
        if (!valid)
        {
            if (encrypted) throw new TestPasswordException();
            throw new InvalidDataException(failure ?? GetLocalizedString("TestDataError", "Damaged archive headers or data."));
        }
        return checked((int)archive.Count);
    }

    private static int TestAesZipContents(string path, string password)
    {
        using var archive = Ionic.Zip.ZipFile.Read(path);
        using var progress = CreateAddProgress(100, GetLocalizedString("TestRunning", "Testing archive..."));
        int completed = 0;
        archive.ExtractProgress += (_, e) =>
        {
            double fraction = e.TotalBytesToTransfer > 0 ? (double)e.BytesTransferred / e.TotalBytesToTransfer : 0;
            progress?.Tick(Math.Min(100, (int)((completed + fraction) * 100 / Math.Max(1, archive.Count))), e.CurrentEntry?.FileName ?? "");
        };
        foreach (Ionic.Zip.ZipEntry entry in archive)
        {
            try { if (!entry.IsDirectory) entry.ExtractWithPassword(Stream.Null, password); }
            catch (Exception ex) when (entry.UsesEncryption && ex is not UnauthorizedAccessException) { throw new TestPasswordException(); }
            completed++;
            if (IsVerbose && !IsSilent) Console.WriteLine("OK: " + entry.FileName);
        }
        progress?.Tick(100);
        return archive.Count;
    }
}
