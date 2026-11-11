using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Zgx.Sfx;

public sealed class SafetyException(int code, string message) : IOException(message)
{
    public int ExitCode { get; } = code;
}

public sealed class Session(Options options, string work, string payload) : IDisposable
{
    public Options Options { get; } = options;
    public static string L(string it, string en) => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? it : en;
    public int CheckSafety(Action<string> report)
    {
        var safety = zg.ZipBombGuard.Inspect(payload);
        if (safety.ExitCode != 0)
            report($"ZIP: score={safety.Score}%; risk-code={safety.ExitCode}");
        if (safety.Blocked)
            throw new SafetyException(safety.ExitCode, L("Estrazione bloccata dalla protezione ZIP.", "Extraction blocked by ZIP protection."));
        return safety.ExitCode;
    }

    public IReadOnlyList<string> List(string? password = null)
    {
        using var archive = ArchiveFactory.OpenArchive(payload, new ReaderOptions { Password = password });
        return archive.Entries.Where(e => !e.IsDirectory).Select(e => e.Key ?? "").ToArray();
    }

    public string DefaultDestination()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string resolved = Options.Destination switch
        {
            "editable" => Path.Combine(Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(Options.ArchiveName)),
            "desktop" => KnownFolder("XDG_DESKTOP_DIR", Environment.SpecialFolder.DesktopDirectory, "Desktop"),
            "documents" => KnownFolder("XDG_DOCUMENTS_DIR", Environment.SpecialFolder.MyDocuments, "Documents"),
            "downloads" => KnownFolder("XDG_DOWNLOAD_DIR", null, "Downloads"),
            "pictures" => KnownFolder("XDG_PICTURES_DIR", Environment.SpecialFolder.MyPictures, "Pictures"),
            "music" => KnownFolder("XDG_MUSIC_DIR", Environment.SpecialFolder.MyMusic, "Music"),
            "videos" => KnownFolder("XDG_VIDEOS_DIR", Environment.SpecialFolder.MyVideos, "Movies"),
            "favorites" => Path.Combine(home, "Favorites"),
            _ => throw new ArgumentException("Invalid destination.")
        };
        return Options.Subfolder.Length == 0 ? resolved : Path.Combine(resolved, Package.SafeRelative(Options.Subfolder));
    }

    private static string KnownFolder(string xdg, Environment.SpecialFolder? special, string fallback)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsLinux())
        {
            string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config");
            string settings = Path.Combine(config, "user-dirs.dirs");
            if (File.Exists(settings))
                foreach (string line in File.ReadLines(settings))
                    if (line.StartsWith(xdg + "=", StringComparison.Ordinal))
                    {
                        string value = line[(xdg.Length + 1)..].Trim().Trim('"').Replace("$HOME", home);
                        if (Path.IsPathFullyQualified(value)) return value;
                    }
        }
        if (special.HasValue)
        {
            string value = Environment.GetFolderPath(special.Value);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return Path.Combine(home, fallback == "Movies" && !OperatingSystem.IsMacOS() ? "Videos" : fallback);
    }

    public void Extract(string destination, string? password, bool overwrite, IProgress<(int Percent, string File)>? progress, CancellationToken cancellation)
    {
        CheckSafety(_ => { });
        string full = Path.GetFullPath(destination);
        CheckNoLinks(full);
        string staging = Path.Combine(work, "files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            using var archive = ArchiveFactory.OpenArchive(payload, new ReaderOptions { Password = password });
            var entries = archive.Entries.ToList();
            var files = new List<(string Relative, string Staged)>();
            var directories = new List<string>();
            var seen = new HashSet<string>(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var entry = entries[i];
                string relative = Package.SafeRelative((entry.Key ?? "").TrimEnd('/', '\\'));
                if (!seen.Add(relative)) throw new InvalidDataException("Duplicate archive path: " + relative);
                string staged = Path.Combine(staging, relative);
                if (entry.IsDirectory) { Directory.CreateDirectory(staged); directories.Add(relative); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                using (var input = entry.OpenEntryStream())
                using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    long written = 0;
                    int count;
                    while ((count = input.Read(buffer)) > 0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        written = checked(written + count);
                        if (entry.Size >= 0 && written > entry.Size) throw new InvalidDataException("Archive entry exceeds its declared size.");
                        output.Write(buffer, 0, count);
                        progress?.Report(((int)((i + (entry.Size > 0 ? (double)written / entry.Size : 0)) * 100 / Math.Max(1, entries.Count)), relative));
                    }
                }
                if (entry.LastModifiedTime.HasValue) File.SetLastWriteTime(staged, entry.LastModifiedTime.Value);
                files.Add((relative, staged));
            }
            // Validate all destinations before publishing any extracted file.
            foreach (string relative in files.Select(f => f.Relative).Concat(directories))
            {
                string target = Path.Combine(full, relative);
                CheckNoLinks(target);
                if (!overwrite && File.Exists(target)) throw new IOException(L("Il file esiste già: ", "File already exists: ") + target);
            }
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(full);
            foreach (string directory in directories) Directory.CreateDirectory(Path.Combine(full, directory));
            foreach (var file in files)
            {
                string target = Path.Combine(full, file.Relative);
                CheckNoLinks(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(file.Staged, target, overwrite);
            }
            progress?.Report((100, L("Completato", "Completed")));
        }
        finally { Directory.Delete(staging, true); }
    }

    private static void CheckNoLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Extraction through a symbolic link is not allowed: " + current);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    public void OpenAfterExtraction(string destination)
    {
        var paths = new List<string>();
        if (Options.ShowReadme)
        {
            string? readme = Directory.EnumerateFiles(destination, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => new[] { "readme.txt", "leggimi.txt", "readme.html", "leggimi.html" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
            if (readme is not null) paths.Add(readme);
        }
        if (Options.RunAfter.Length > 0)
            paths.Add(Path.Combine(destination, Package.SafeRelative(Options.RunAfter)));
        foreach (string path in paths.Distinct())
        {
            CheckNoLinks(path);
            if (!File.Exists(path)) throw new FileNotFoundException("File to open not found.", path);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
                start.ArgumentList.Add(path);
                Process.Start(start);
            }
            else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    public void Dispose() { if (Directory.Exists(work)) Directory.Delete(work, true); }
}
