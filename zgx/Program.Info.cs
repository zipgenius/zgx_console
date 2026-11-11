using ConsoleTables;
using DiscUtils;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace zg;

partial class Program
{
    // Stable field names and invariant values make redirected reports reproducible.
    private sealed class InfoReport
    {
        public bool Incomplete;
        public void Table(string title, params (string Key, object? Value)[] rows)
        {
            Console.WriteLine("\n" + CleanInfo(title));
            var table = new ConsoleTable("Field", "Value");
            foreach (var row in rows) table.AddRow(CleanInfo(row.Key), CleanInfo(row.Value));
            table.Write(Format.Minimal);
        }
        public void Warning(string context, Exception ex)
        {
            Incomplete = true;
            Console.Error.WriteLine($"{context}: {CleanInfo(ex.GetBaseException().Message)}");
        }
        public void Properties(string title, object? value, params string[] names)
        {
            if (value == null) { Table(title, ("Metadata", null)); return; }
            var rows = new List<(string, object?)>();
            foreach (var p in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 &&
                    (names.Length == 0 || names.Contains(p.Name))).OrderBy(p => p.Name))
            {
                var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                if (!(t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Guid) ||
                    t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(decimal))) continue;
                try { rows.Add((p.Name, p.GetValue(value))); }
                catch (Exception ex) when (ex.GetBaseException() is NotSupportedException or NotImplementedException)
                { rows.Add((p.Name, "not available from this reader")); }
                catch (Exception ex) { Warning(title + "." + p.Name, ex); }
            }
            if (rows.Count != 0) Table(title, rows.ToArray());
        }
    }

    private static string CleanInfo(object? value)
    {
        string s = value switch
        {
            null => "not available",
            DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };
        return string.Concat(s.Select(c => char.IsControl(c) ? ' ' : c));
    }

    private static ExitCode FileInformation(string[] args)
    {
        if (args.Length < 2 || args[1].StartsWith('-'))
        { Console.Error.WriteLine(GetLocalizedString("InfoUsage", "Usage: zg -info file [--verbose] [--silent]")); return ExitCode.UnknownError; }
        // Unlike progress reporting, --silent must not remove the requested report or --verbose details.
        bool verbose = args.Skip(2).Any(a => a is "--verbose" or "--v");
        if (args.Skip(2).Any(a => a is not ("--verbose" or "--v" or "--silent" or "--s" or "--noprogress" or "--p")))
        { Console.Error.WriteLine(GetLocalizedString("InfoUsage", "Usage: zg -info file [--verbose] [--silent]")); return ExitCode.UnknownError; }
        try
        {
            var file = new FileInfo(args[1]);
            if (!file.Exists)
            { Console.Error.WriteLine(GetLocalizedString("InfoMissing", "File not found: {0}", CleanInfo(file.FullName))); return ExitCode.FileDoesNotExists; }
            var report = new InfoReport();
            report.Table("File", ("Path", file.FullName), ("File length (bytes)", file.Length),
                ("Last write UTC", file.LastWriteTimeUtc), ("Read mode", "read-only; no mounting or extraction"));
            using var stream = System.IO.File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] head = new byte[Math.Min(512, file.Length)];
            stream.ReadExactly(head);
            string signature = FileUtils.GuessFileFormat(head);
            if (signature == "wmdk") signature = "vmdk";
            if (signature == "arc" && head.AsSpan().StartsWith("%PDF"u8)) signature = "pdf";
            if (head.Length >= 68 && BitConverter.ToUInt32(head, 64) == 0xbeda107f) signature = "vdi";
            string ext = file.Extension.ToLowerInvariant();
            string type = DetectInfoDisk(stream, head, ext);
            report.Table("Identification", ("Detected signature", signature), ("Extension", ext), ("Reader", type));
            if (verbose) report.Table("File details", ("Attributes", file.Attributes),
                ("Header (first 64 bytes)", Convert.ToHexString(head.AsSpan(0, Math.Min(64, head.Length)))));
            if (type == "ISO/UDF") InfoOptical(stream, report, verbose);
            else if (type != "Archive") InfoDisk(file.FullName, type, report, verbose);
            else
            {
                var safety = CheckZipBomb(file.FullName, true);
                report.Table("ZIP safety", ("Score (0-99)", safety.Score), ("Incomplete", safety.Incomplete), ("Risk code", safety.ExitCode));
                InfoArchive(file.FullName, report, verbose, signature is not ("unknown" or "pdf" or "sqlite3" or "elf") ||
                    new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".cab", ".wim", ".sqx" }.Contains(ext));
                if (safety.ExitCode != 0) return (ExitCode)safety.ExitCode;
            }
            return report.Incomplete ? ExitCode.InfoIncomplete : ExitCode.Success;
        }
        catch (NotSupportedException ex) { Console.Error.WriteLine(CleanInfo(ex.Message)); return (ExitCode)7; }
        catch (FileNotFoundException ex) { Console.Error.WriteLine(CleanInfo(ex.Message)); return ExitCode.FileDoesNotExists; }
        catch (UnauthorizedAccessException ex) { Console.Error.WriteLine(CleanInfo(ex.Message)); return ExitCode.IOError; }
        catch (Exception ex) { Console.Error.WriteLine(CleanInfo(ex.Message)); return ExitCode.InfoIncomplete; }
    }

    private static string DetectInfoDisk(Stream stream, byte[] head, string ext)
    {
        string ascii = Encoding.ASCII.GetString(head);
        if (ascii.StartsWith("vhdxfile")) return "VHDX";
        if (ascii.StartsWith("KDMV") || ascii.StartsWith("# Disk DescriptorFile")) return "VMDK";
        if (head.Length >= 68 && BitConverter.ToUInt32(head, 64) == 0xbeda107f) return "VDI";
        if (stream.Length >= 512)
        {
            stream.Position = stream.Length - 512;
            byte[] footer = new byte[8]; stream.ReadExactly(footer);
            if (Encoding.ASCII.GetString(footer) == "conectix") return "VHD";
            if (Encoding.ASCII.GetString(footer, 0, 4) == "koly") return "DMG";
        }
        if (stream.Length >= 32775)
        {
            stream.Position = 32769;
            byte[] magic = new byte[5]; stream.ReadExactly(magic);
            if (Encoding.ASCII.GetString(magic) == "CD001") return "ISO/UDF";
        }
        return ext switch { ".iso" or ".udf" => "ISO/UDF", ".vhdx" => "VHDX", ".vhd" => "VHD",
            ".vmdk" => "VMDK", ".vdi" => "VDI", ".dmg" => "DMG",
            ".img" or ".ima" or ".raw" => "RAW", _ => "Archive" };
    }

    private static void InfoDisk(string path, string type, InfoReport report, bool verbose)
    {
        if (verbose)
        {
            if (type == "VDI") InfoVdiHeader(path, report);
            if (type == "VMDK") InfoVmdkDescriptor(path, report);
        }
        using VirtualDisk disk = type switch
        {
            "VHDX" => new DiscUtils.Vhdx.Disk(path, FileAccess.Read),
            "VHD" => new DiscUtils.Vhd.Disk(path, FileAccess.Read),
            "VMDK" => new DiscUtils.Vmdk.Disk(path, FileAccess.Read),
            "VDI" => new DiscUtils.Vdi.Disk(path, FileAccess.Read),
            "DMG" => new DiscUtils.Dmg.Disk(System.IO.File.OpenRead(path), DiscUtils.Streams.Ownership.Dispose),
            _ => new DiscUtils.Raw.Disk(path, FileAccess.Read)
        };
        report.Table("Virtual disk", ("Format", type), ("Virtual capacity (bytes)", disk.Capacity),
            ("Logical sector (bytes)", disk.SectorSize), ("Class", disk.DiskClass));
        report.Properties("Disk type", disk.DiskTypeInfo);
        if (verbose)
        {
            report.Properties("Geometry", disk.Geometry);
            int index = 0;
            foreach (var layer in disk.Layers)
            {
                string title = "Layer " + index++;
                report.Properties(title, layer);
                int extentIndex = 0;
                foreach (var extent in layer.Extents)
                    report.Properties(title + " extent " + extentIndex++, extent);
                if (layer.GetType().GetProperty("ExtentPaths")?.GetValue(layer) is IEnumerable<string> paths)
                    report.Table(title + " extents", paths.Select((p, i) => (i.ToString(), (object?)p)).ToArray());
                var info = layer.GetType().GetProperty("Information")?.GetValue(layer);
                if (info != null)
                {
                    report.Properties(title + " metadata", info);
                    if (info.GetType().GetProperty("ParentLocatorEntries")?.GetValue(info) is IEnumerable<KeyValuePair<string, string>> locators)
                        report.Table(title + " parent locators", locators.Select(p => (p.Key, (object?)p.Value)).ToArray());
                    foreach (string nested in new[] { "ActiveHeader", "FirstHeader", "SecondHeader" })
                        if (info.GetType().GetProperty(nested)?.GetValue(info) is object header)
                            report.Properties(title + " " + nested, header);
                }
            }
        }
        try
        {
            report.Table("Partition layout", ("Partitioned", disk.IsPartitioned));
            if (disk.IsPartitioned)
            {
                var t = new ConsoleTable("Partition", "Type", "First sector", "Last sector", "Sectors", "Type GUID");
                int i = 0;
                foreach (var p in disk.Partitions.Partitions)
                    t.AddRow(i++, CleanInfo(p.TypeAsString), p.FirstSector, p.LastSector, p.SectorCount,
                        p.GuidType == Guid.Empty ? "not applicable" : p.GuidType.ToString());
                t.Write(Format.Minimal);
                if (verbose)
                {
                    i = 0;
                    foreach (var p in disk.Partitions.Partitions) report.Properties("Partition " + i++, p);
                }
            }
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
            foreach (var v in new VolumeManager(disk).GetLogicalVolumes())
            {
                report.Table("Volume", ("Identity", v.Identity), ("Length (bytes)", v.Length),
                    ("Physical start sector", v.PhysicalStartSector), ("Status", v.Status));
                var detected = FileSystemManager.DetectFileSystems(v);
                if (detected.Count == 0) report.Table("Filesystem", ("Detection", "not recognised; may be blank, encrypted or unsupported"));
                foreach (var f in detected)
                {
                    try { using var fs = f.Open(v); InfoFileSystem(fs, report, verbose, f.Name); }
                    catch (Exception ex) { report.Warning("Filesystem " + f.Name, ex); }
                }
            }
        }
        catch (Exception ex) { report.Warning("Partitions/volumes", ex); }
    }

    private static void InfoFileSystem(DiscFileSystem fs, InfoReport report, bool verbose, string name)
    {
        report.Table("Filesystem", ("Type", name), ("Label", fs.VolumeLabel));
        report.Properties("Filesystem details", fs, "Size", "UsedSpace", "AvailableSpace");
        if (verbose) report.Properties("Filesystem metadata", fs);
    }

    private static void InfoOptical(Stream stream, InfoReport report, bool verbose)
    {
        stream.Position = 0;
        bool found = false;
        if (DiscUtils.Iso9660.CDReader.Detect(stream))
        {
            stream.Position = 0;
            using var cd = new DiscUtils.Iso9660.CDReader(stream, true);
            InfoFileSystem(cd, report, verbose, "ISO 9660");
            report.Table("Optical disc", ("Variant", cd.ActiveVariant), ("Boot image", cd.HasBootImage),
                ("Sector size (bytes)", cd.SectorSize), ("Sectors", cd.TotalSectors));
            // Read the standard primary descriptor; no directory traversal or file decompression.
            for (int n = 16; n < 272 && (long)(n + 1) * 2048 <= stream.Length; n++)
            {
                stream.Position = (long)n * 2048;
                byte[] d = new byte[2048]; stream.ReadExactly(d);
                if (Encoding.ASCII.GetString(d, 1, 5) != "CD001") break;
                if (d[0] == 255) break;
                if (d[0] != 1) continue;
                string Text(int start, int count) => Encoding.ASCII.GetString(d, start, count).Trim(' ', '\0');
                report.Table("ISO primary descriptor", ("System identifier", Text(8, 32)), ("Volume identifier", Text(40, 32)),
                    ("Volume blocks", System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(80))),
                    ("Logical block (bytes)", System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(128))));
                if (verbose) report.Table("ISO publishing metadata", ("Volume set", Text(190, 128)),
                    ("Publisher", Text(318, 128)), ("Data preparer", Text(446, 128)), ("Application", Text(574, 128)),
                    ("Copyright file", Text(702, 37)), ("Abstract file", Text(739, 37)), ("Bibliographic file", Text(776, 37)),
                    ("Creation (ISO timestamp with signed 15-minute offset byte)", Text(813, 16) + " offset=" + unchecked((sbyte)d[829])),
                    ("Modification (ISO timestamp with signed 15-minute offset byte)", Text(830, 16) + " offset=" + unchecked((sbyte)d[846])));
                break;
            }
            found = true;
        }
        stream.Position = 0;
        if (DiscUtils.Udf.UdfReader.Detect(stream))
        {
            stream.Position = 0;
            using var udf = new DiscUtils.Udf.UdfReader(stream);
            InfoFileSystem(udf, report, verbose, "UDF");
            found = true;
        }
        if (!found) throw new NotSupportedException("No supported ISO 9660/UDF filesystem found.");
    }

    private static void InfoArchive(string path, InfoReport report, bool verbose, bool recognised)
    {
        if (Path.GetExtension(path).Equals(".sqx", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("The SQX engine does not expose read-only metadata for -info.");
        try
        {
            using var archive = SharpCompress.Archives.ArchiveFactory.OpenArchive(path);
            long count = 0, folders = 0; decimal size = 0, packed = 0; bool encrypted = false;
            var table = verbose ? new ConsoleTable("Entry", "Bytes", "Packed bytes", "Encrypted", "CRC32", "Modified") : null;
            foreach (var e in archive.Entries)
            {
                count++; if (e.IsDirectory) folders++;
                size += e.Size; packed += e.CompressedSize; encrypted |= e.IsEncrypted;
                string crc = archive.Type.ToString() is "Zip" or "Rar" or "SevenZip" or "GZip" ? e.Crc.ToString("X8") : "not available";
                table?.AddRow(CleanInfo(e.Key), e.Size, e.CompressedSize, e.IsEncrypted, crc, CleanInfo(e.LastModifiedTime));
            }
            report.Table("Archive", ("Format", archive.Type), ("Entries", count), ("Directories", folders),
                ("Files", count - folders), ("Unpacked bytes (declared)", size), ("Packed entry bytes (declared)", packed),
                ("Encrypted entries", encrypted), ("Complete", archive.IsComplete));
            if (!archive.IsComplete) report.Incomplete = true;
            table?.Write(Format.Minimal);
            if (verbose)
            {
                report.Properties("Archive metadata", archive);
                foreach (var entry in archive.Entries)
                    report.Properties("Entry metadata " + CleanInfo(entry.Key), entry,
                        "CompressionType", "IsSolid", "IsSplitAfter", "IsEncrypted", "LinkTarget", "Attrib", "CreatedTime", "LastAccessedTime");
            }
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            SevenZipExtractor.ArchiveFile opened;
            try { opened = new SevenZipExtractor.ArchiveFile(path, FileUtils.GetSevenZipLibs()); }
            catch (Exception ex) when (!recognised) { throw new NotSupportedException("Format not recognised by the available readers.", ex); }
            using var archive = opened;
            var entries = archive.Entries;
            report.Table("Archive", ("Reader", "7-Zip"), ("Entries", entries.Count),
                ("Unpacked bytes (declared)", entries.Sum(e => (decimal)e.Size)));
            if (verbose)
            {
                report.Properties("Archive metadata", archive);
                foreach (var entry in entries) report.Properties("Entry " + CleanInfo(entry.FileName), entry);
            }
        }
        catch (Exception ex) when (!recognised) { throw new NotSupportedException("Format not recognised by the available readers.", ex); }
    }

    private static void InfoVdiHeader(string path, InfoReport report)
    {
        // VDI 1.1 header layout, independently read with bounded sizes. Older versions stay with DiscUtils metadata.
        // Layout reference: DiscUtils.Vdi/HeaderRecord.cs (upstream DiscUtils).
        using var s = System.IO.File.OpenRead(path);
        byte[] h = new byte[(int)Math.Min(472, s.Length)]; s.ReadExactly(h);
        if (h.Length < 72) throw new InvalidDataException("Truncated VDI pre-header.");
        uint U(int n) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(n));
        if (U(64) != 0xbeda107f || U(68) != 0x00010001) return;
        if (h.Length < 472) throw new InvalidDataException("Truncated VDI 1.1 header.");
        string Id(int n) => new Guid(h.AsSpan(n, 16)).ToString();
        report.Table("VDI 1.1 header", ("Header bytes", U(72)), ("Image type (VDI code)", U(76)),
            ("Flags", U(80)), ("Comment", Encoding.UTF8.GetString(h, 84, 256).TrimEnd('\0')),
            ("Block map offset (bytes)", U(340)), ("Data offset (bytes)", U(344)),
            ("Block size (bytes)", U(376)), ("Block extra bytes", U(380)),
            ("Blocks", U(384)), ("Allocated blocks", U(388)), ("Image UUID", Id(392)),
            ("Modification UUID", Id(408)), ("Parent UUID", Id(424)), ("Parent modification UUID", Id(440)));
    }

    private static void InfoVmdkDescriptor(string path, InfoReport report)
    {
        // Hosted sparse header and descriptor; cap the text read to 1 MiB and validate sector arithmetic.
        using var s = System.IO.File.OpenRead(path);
        byte[] h = new byte[512]; int read = (int)Math.Min(512, s.Length); s.ReadExactly(h.AsSpan(0, read));
        long offset = 0, length = s.Length;
        if (read >= 79 && Encoding.ASCII.GetString(h, 0, 4) == "KDMV")
        {
            ulong U(int n) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(n));
            report.Table("VMDK sparse header", ("Version", BitConverter.ToUInt32(h, 4)),
                ("Flags", BitConverter.ToUInt32(h, 8)), ("Grain sectors (512 bytes each)", U(20)),
                ("Unclean shutdown", h[72] != 0), ("Compression code", BitConverter.ToUInt16(h, 77)));
            ulong sector = U(28), sectors = U(36);
            if (sector == 0 || sectors == 0) return;
            if (sector > (ulong)s.Length / 512 || sectors > 2048 || sectors * 512 > (ulong)s.Length - sector * 512)
                throw new InvalidDataException("VMDK descriptor outside the file or larger than the 1 MiB report limit.");
            offset = (long)sector * 512; length = (long)sectors * 512;
        }
        else if (length > 1024 * 1024) throw new InvalidDataException("VMDK text descriptor exceeds the 1 MiB report limit.");
        s.Position = offset;
        byte[] text = new byte[(int)length]; s.ReadExactly(text);
        var rows = new List<(string, object?)>();
        foreach (string line in Encoding.UTF8.GetString(text).TrimEnd('\0').Split('\n'))
        {
            string l = line.Trim();
            if (l.Length == 0 || l.StartsWith('#')) continue;
            int equals = l.IndexOf('=');
            rows.Add(equals > 0 ? (l[..equals].Trim(), l[(equals + 1)..].Trim().Trim('"')) : ("Extent declaration", l));
        }
        report.Table("VMDK descriptor (stored values)", rows.ToArray());
    }
}
