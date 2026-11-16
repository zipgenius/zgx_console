namespace zg;

partial class Program
{
    private static void ExtractCabToPrivateDirectory(string archivePath, string destination, ISet<string>? selected = null)
    {
        GCabArchive.Extract(archivePath, destination, entries =>
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                string name = entry.Name.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') ||
                    name.Contains(':') || name.Split('/').Any(part => part is "" or "." or "..") ||
                    !seen.Add(name))
                    throw new InvalidDataException("Unsafe or duplicate CAB entry: " + entry.Name);
                _ = GetSafeExtractionPath(destination, name.Replace('/', Path.DirectorySeparatorChar));
            }
        }, selected);
    }

    private static string CreatePrivateCabDirectory()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string path = Path.Combine(TempPath, "zg-cab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private static ExitCode WriteCompatibleGcab(AddCommandOptions options, Dictionary<string, string> selectedFiles)
    {
        if (!string.IsNullOrEmpty(options.Password)) throw new ArgumentException("CAB archives do not support password protection.");
        if (options.VolumeSize != 0) throw new ArgumentException("Linux CAB creation does not support split volumes.");
        string staging = CreatePrivateCabDirectory();
        string directory = Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, ".zg-cab-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            if (File.Exists(options.ArchivePath)) ExtractCabToPrivateDirectory(options.ArchivePath, staging);
            using (var progress = CreateAddProgress(selectedFiles.Count, "Preparing CAB files..."))
                StageCompatibleArchiveFiles(options, selectedFiles, staging, progress);
            string[] files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
            if (files.Length == 0) return ExitCode.ZeroItemsArchive;
            if (files.Length > 65_535) throw new ArgumentException("CAB archives cannot contain more than 65535 files.");
            long total = 0;
            foreach (string file in files)
            {
                long size = new FileInfo(file).Length;
                if (size > 2L * 1024 * 1024 * 1024) throw new ArgumentException("CAB files cannot exceed 2 GB: " + file);
                total = checked(total + size);
            }
            if (total >= uint.MaxValue) throw new ArgumentException("CAB source data must be smaller than 4 GB.");
            GCabArchive.Write(staging, temporary, options.CompressionLevel != 0);
            File.Move(temporary, options.ArchivePath, true);
            CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
            return ExitCode.Success;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            DeleteExtractionStagingFolder(staging);
        }
    }

    private static int TestGcab(string archivePath)
    {
        _ = GCabArchive.List(archivePath); // Also reject split cabinets.
        return LibArchiveCabTest.Verify(archivePath);
    }

    private static ExitCode ExtractGcabFiles(ExtractCommandOptions options)
    {
        string extracted = CreatePrivateCabDirectory();
        string staging = CreatePrivateCabDirectory();
        try
        {
            var entries = GCabArchive.List(options.ArchivePath);
            var selected = entries.Where(entry => ShouldExtractStagedFile(entry.Name, FileAttributes.Normal, options)).ToList();
            if (selected.Count == 0) return ExitCode.ZeroItemsArchive;
            ExtractCabToPrivateDirectory(options.ArchivePath, extracted, selected.Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
            foreach (var entry in selected)
            {
                string source = GetSafeExtractionPath(extracted, entry.Name.Replace('/', Path.DirectorySeparatorChar));
                if (new FileInfo(source).Length != entry.Size) throw new InvalidDataException("CAB entry size does not match: " + entry.Name);
                string target = GetSafeExtractionPath(staging, GetExtractedEntryPath(entry.Name, options.RestorePaths));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, true);
                if (entry.Modified > DateTime.MinValue) File.SetLastWriteTime(target, entry.Modified);
            }
            int count = CommitStagedExtraction(staging, options);
            if (!IsSilent) Console.WriteLine(GetLocalizedString("ExtractCompleted", "Extracted {0} file(s) to {1}", count, options.DestinationPath));
            return ExitCode.Success;
        }
        finally { DeleteExtractionStagingFolder(staging); DeleteExtractionStagingFolder(extracted); }
    }
}
