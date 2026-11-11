using System.Diagnostics;
using System.IO.Compression;
using System.Security;
using ZipGenius_X.Utils;
using Zgx.Sfx;

namespace zg;

partial class Program
{
    private static string[] AvailableSfxTargets() => typeof(Program).Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith("zg.SfxTemplates.", StringComparison.Ordinal) && name.EndsWith(".gz", StringComparison.Ordinal))
        .Select(name => name["zg.SfxTemplates.".Length..^3].Replace('.', '/'))
        .Append("win-x64/gui").Distinct().Order(StringComparer.Ordinal).ToArray();

    private static void CreatePortableSfx(string source, string destination, SfxPackageOptions settings, string target, string ui, bool overwrite)
    {
        if (target.StartsWith("osx-") && !OperatingSystem.IsMacOS())
            throw new ArgumentException(GetLocalizedString("SfxMacSigning", "La finalizzazione di uno SFX macOS richiede macOS (firma locale dell’eseguibile). / macOS SFX finalization requires macOS for local code signing."));
        if (ui == "terminal" && settings.IconPath is not null)
            throw new ArgumentException(GetLocalizedString("SfxGuiIcon", "--icon richiede --ui=gui. / --icon requires --ui=gui."));
        var options = new Options
        {
            ArchiveName = Path.GetFileName(source), Message = settings.Message!, Target = target,
            AutoStart = settings.AutoStart, FileList = settings.FileListMode.ToString().ToLowerInvariant(),
            ShowReadme = settings.ShowReadme, CloseOnSuccess = settings.CloseOnSuccess,
            Destination = settings.DestinationFolder.ToString().ToLowerInvariant(),
            Subfolder = settings.DestinationSubpath ?? "", RunAfter = settings.RunAfterPath ?? ""
        };
        Package.Validate(options);
        if (settings.IconPath is not null)
        {
            byte[] icon = File.ReadAllBytes(settings.IconPath);
            if (icon.Length is < 6 or > 4 * 1024 * 1024 || icon[0] != 0 || icon[1] != 0 || icon[2] != 1 || icon[3] != 0)
                throw new ArgumentException(GetLocalizedString("SfxInvalidIcon", "Invalid ICO icon."));
            options.IconBase64 = Convert.ToBase64String(icon);
        }
        using Stream compressed = typeof(Program).Assembly.GetManifestResourceStream($"zg.SfxTemplates.{target}.{ui}.gz")
            ?? throw new ArgumentException(GetLocalizedString("SfxTemplateMissing", "Stub {0}/{1} is not included in this distribution. Available: {2}. Rebuild with SfxTemplateRids={0}.", target, ui, string.Join(", ", AvailableSfxTargets())));
        using var template = new GZipStream(compressed, CompressionMode.Decompress);
        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        bool app = target.StartsWith("osx-") && ui == "gui";
        string scratch = Path.Combine(parent, ".zg-sfx-" + Guid.NewGuid().ToString("N") + (app ? ".app" : ".tmp"));
        string unsignedTemplate = Path.Combine(parent, ".zg-sfx-template-" + Guid.NewGuid().ToString("N"));
        try
        {
            string binary = scratch;
            if (app)
            {
                string contents = Path.Combine(scratch, "Contents");
                Directory.CreateDirectory(Path.Combine(contents, "MacOS"));
                binary = Path.Combine(contents, "MacOS", "zg-sfx");
                string title = SecurityElement.Escape(Path.GetFileNameWithoutExtension(destination))!;
                File.WriteAllText(Path.Combine(contents, "Info.plist"), $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict><key>CFBundleExecutable</key><string>zg-sfx</string><key>CFBundleName</key><string>{title}</string><key>CFBundleIdentifier</key><string>it.zipgenius.sfx</string><key>CFBundlePackageType</key><string>APPL</string><key>NSHighResolutionCapable</key><true/></dict></plist>");
            }
            if (target.StartsWith("osx-"))
            {
                // Remove an existing Mach-O signature BEFORE appending the archive.
                // Replacing a signature embedded in the old template after appending
                // could truncate the newly added payload.
                using (var file = new FileStream(unsignedTemplate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    template.CopyTo(file);
                if (RunSfxCodesign(["--display", unsignedTemplate], out _) == 0 &&
                    RunSfxCodesign(["--remove-signature", unsignedTemplate], out string removalError) != 0)
                    throw new IOException("codesign: " + removalError);
                using var unsigned = File.OpenRead(unsignedTemplate);
                Package.Write(unsigned, source, binary, options);
            }
            else Package.Write(template, source, binary, options);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            if (target.StartsWith("osx-"))
            {
                if (RunSfxCodesign(["--force", "--sign", "-", app ? scratch : binary], out string error) != 0)
                    throw new IOException("codesign: " + error);
            }
            if (app)
            {
                string backup = Path.Combine(parent, ".zg-sfx-backup-" + Guid.NewGuid().ToString("N") + ".app");
                if (Directory.Exists(destination))
                {
                    if (!overwrite) throw new IOException(GetLocalizedString("SfxOutputExists", "Output already exists."));
                    Directory.Move(destination, backup);
                }
                try { Directory.Move(scratch, destination); }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, destination); throw; }
                if (Directory.Exists(backup)) DeleteSfxWorkDirectory(backup, parent);
            }
            else File.Move(scratch, destination, overwrite);
        }
        finally
        {
            if (File.Exists(unsignedTemplate)) File.Delete(unsignedTemplate);
            if (app && Directory.Exists(scratch)) DeleteSfxWorkDirectory(scratch, parent);
            else if (File.Exists(scratch)) File.Delete(scratch);
        }
    }

    private static int RunSfxCodesign(string[] arguments, out string error)
    {
        var start = new ProcessStartInfo("/usr/bin/codesign") { UseShellExecute = false, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException(GetLocalizedString("SfxCodesignStart", "Cannot start codesign."));
        error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static void DeleteSfxWorkDirectory(string path, string parent)
    {
        string full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(parent), StringComparison.Ordinal) ||
            !Path.GetFileName(full).StartsWith(".zg-sfx-", StringComparison.Ordinal))
            throw new IOException(GetLocalizedString("SfxUnsafeDirectory", "Refusing to remove a directory outside the SFX work area."));
        Directory.Delete(full, true);
    }
}
