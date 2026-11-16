namespace zg;

partial class Program
{
    private static ExitCode ExtractWimFiles(ExtractCommandOptions options)
    {
        string extracted = Path.Combine(TempPath, "zg-wim-extract-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(TempPath, "zg-wim-stage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var entries = WimLibWriter.Entries(options.ArchivePath);
            // Reject links before letting the native engine write any archive data.
            foreach (var entry in entries)
            {
                if (entry.IsReparsePoint) throw new InvalidDataException("WIM extraction does not support symbolic links or reparse points.");
                _ = GetSafeExtractionPath(extracted, entry.Name.Replace('/', Path.DirectorySeparatorChar));
            }
            var selected = entries.Where(entry => (!entry.IsDirectory || options.Recursive) &&
                ShouldExtractStagedFile(entry.Name, entry.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal, options)).ToList();
            if (selected.Count == 0) return ExitCode.ZeroItemsArchive;
            Directory.CreateDirectory(extracted);
            Directory.CreateDirectory(staging);
            WimLibWriter.Extract(options.ArchivePath, extracted);
            foreach (var entry in selected)
            {
                string source = GetSafeExtractionPath(extracted, entry.Name.Replace('/', Path.DirectorySeparatorChar));
                if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("WIM extraction produced a symbolic link.");
                string target = GetSafeExtractionPath(staging, GetExtractedEntryPath(entry.Name, options.RestorePaths));
                if (entry.IsDirectory)
                {
                    if (options.RestorePaths) Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, true);
                File.SetLastWriteTime(target, File.GetLastWriteTime(source));
            }
            int count = CommitStagedExtraction(staging, options);
            if (!IsSilent) Console.WriteLine(GetLocalizedString("ExtractCompleted", "Extracted {0} file(s) to {1}", count, options.DestinationPath));
            return ExitCode.Success;
        }
        finally
        {
            DeleteExtractionStagingFolder(staging);
            DeleteExtractionStagingFolder(extracted);
        }
    }
}
