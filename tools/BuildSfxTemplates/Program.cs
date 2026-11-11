using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string rids = (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
string output = Path.Combine(root, "SfxPortable", "templates");
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--root") root = Path.GetFullPath(args[++i]);
    else if (args[i] == "--rids") rids = args[++i];
    else if (args[i] == "--output") output = Path.GetFullPath(args[++i]);
    else throw new ArgumentException("Unknown argument: " + args[i]);
}
string[] supported = ["win-x86", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"];
Directory.CreateDirectory(output);
var sources = Directory.EnumerateFiles(Path.Combine(root, "SfxPortable"), "*", SearchOption.AllDirectories)
    .Where(path => (path.EndsWith(".cs") || path.EndsWith(".csproj")) && !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
    .Append(Path.Combine(root, "zgshell", "ZipBombGuard.cs")).ToArray();
DateTime newest = sources.Select(File.GetLastWriteTimeUtc).Max();
// Refresh previously embedded targets too: never ship stale cached stubs after a source change.
var targets = rids.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
    .Concat(Directory.EnumerateFiles(output, "*.gz").Select(path => Path.GetFileName(path).Split('.')[0])).Distinct();
foreach (string rid in targets)
{
    if (!supported.Contains(rid)) throw new ArgumentException("Unsupported SFX runtime: " + rid);
    foreach (string ui in new[] { "terminal", "gui" })
    {
        // Keep the compact native Win32 GUI used by the main application.
        if (rid == "win-x64" && ui == "gui") continue;
        string destination = Path.Combine(output, rid + "." + ui + ".gz");
        if (File.Exists(destination) && File.GetLastWriteTimeUtc(destination) >= newest) continue;
        string project = ui == "terminal" ? "Terminal/Sfx.Terminal.csproj" : "Desktop/Sfx.Desktop.csproj";
        string cache = OperatingSystem.IsWindows() ? Path.GetTempPath() : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        string artifacts = Path.Combine(cache, "zg-sfx-build", rid, ui);
        string publish = Path.Combine(artifacts, "package");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        foreach (string argument in new[] { "publish", Path.Combine(root, "SfxPortable", project), "-c", "Release", "-r", rid,
            "--self-contained", "true", "--artifacts-path", artifacts, "-o", publish, "-v", "quiet" }) start.ArgumentList.Add(argument);
        Console.WriteLine($"Building SFX {rid}/{ui}...");
        using var process = Process.Start(start) ?? throw new IOException("Cannot start dotnet publish.");
        process.WaitForExit();
        if (process.ExitCode != 0) return process.ExitCode;
        string binary = Path.Combine(publish, "zg-sfx-" + (ui == "terminal" ? "terminal" : "gui") + (rid.StartsWith("win-") ? ".exe" : ""));
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var input = File.OpenRead(binary))
            using (var file = File.Create(temporary))
            using (var zip = new GZipStream(file, CompressionLevel.SmallestSize)) input.CopyTo(zip);
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
return 0;
