using ZipGenius_X.Utils;

namespace zg;

partial class Program
{
    private static ExitCode CreateSfx(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[1] == "--list-targets")
            {
                foreach (string available in AvailableSfxTargets()) Console.WriteLine(available);
                return ExitCode.Success;
            }
            if (args.Length < 3)
                throw new ArgumentException("zg -sfx source.zip output [--target=platform-architecture] [--ui=gui|terminal]; zg -help -sfx");

            string source = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1]));
            string destination = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[2]));
            if (!File.Exists(source))
                throw new FileNotFoundException(GetLocalizedString("SfxSourceMissing", "Source archive not found: {0}", source));
            if (!new[] { ".zip", ".7z", ".rar" }.Contains(Path.GetExtension(source), StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(GetLocalizedString("SfxFormats", "SFX supports ZIP, 7Z and RAR archives."));
            if (source.Equals(destination, StringComparison.OrdinalIgnoreCase) ||
                destination.Equals(Path.Combine(AppContext.BaseDirectory, "sfx_stub.exe"), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(GetLocalizedString("SfxOutput", "Choose a different output file with the .exe extension."));

            bool autoStart = false, readme = true, close = true, overwrite = false;
            var fileList = SfxFileListMode.Hidden;
            var folder = SfxDestinationFolder.Editable;
            string? subfolder = null, runAfter = null, icon = null;
            string target = (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
                System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
            string ui = "gui";
            string message = GetLocalizedString("SfxDefaultMessage", "This is a self-extracting archive created with ZipGenius X.");
            foreach (string argument in args.Skip(3))
            {
                string[] parts = argument.Split('=', 2);
                string name = parts[0].ToLowerInvariant();
                string Value() => parts.Length == 2 && parts[1].Length > 0
                    ? parts[1] : throw new ArgumentException(GetLocalizedString("SfxMissingValue", "An option value is required: {0}", name));
                switch (name)
                {
                    case "--target": target = Value().ToLowerInvariant(); break;
                    case "--ui": ui = Value().ToLowerInvariant(); break;
                    case "--auto-start" when parts.Length == 1: autoStart = true; break;
                    case "--no-readme" when parts.Length == 1: readme = false; break;
                    case "--keep-open" when parts.Length == 1: close = false; break;
                    case "--overwrite" when parts.Length == 1: overwrite = true; break;
                    case "--message": message = Value(); break;
                    case "--icon": icon = Path.GetFullPath(Environment.ExpandEnvironmentVariables(Value())); break;
                    case "--subfolder": subfolder = Value(); break;
                    case "--run-after": runAfter = Value(); break;
                    case "--file-list":
                        fileList = Value().ToLowerInvariant() switch
                        {
                            "hidden" => SfxFileListMode.Hidden,
                            "expanded" => SfxFileListMode.Expanded,
                            "collapsed" => SfxFileListMode.Collapsed,
                            _ => throw new ArgumentException(GetLocalizedString("SfxFileList", "Use hidden, expanded or collapsed for --file-list."))
                        };
                        break;
                    case "--destination":
                        folder = Value().ToLowerInvariant() switch
                        {
                            "editable" => SfxDestinationFolder.Editable,
                            "desktop" => SfxDestinationFolder.Desktop,
                            "documents" => SfxDestinationFolder.Documents,
                            "downloads" => SfxDestinationFolder.Downloads,
                            "pictures" => SfxDestinationFolder.Pictures,
                            "music" => SfxDestinationFolder.Music,
                            "videos" => SfxDestinationFolder.Videos,
                            "favorites" => SfxDestinationFolder.Favorites,
                            _ => throw new ArgumentException(GetLocalizedString("SfxDestination", "Unknown destination folder: {0}", Value()))
                        };
                        break;
                    default:
                        if (!argument.StartsWith("--", StringComparison.Ordinal) || !IsGlobalOption(argument[2..]))
                            throw new ArgumentException(GetLocalizedString("SfxUnknownOption", "Unknown SFX option: {0}", argument));
                        break;
                }
            }

            if (!new[] { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" }.Contains(target) ||
                ui is not ("gui" or "terminal"))
                throw new ArgumentException("--target: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64; --ui: gui, terminal");
            bool macApp = target.StartsWith("osx-") && ui == "gui";
            if (target.StartsWith("win-") && !destination.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                macApp && !destination.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(GetLocalizedString("SfxOutputNames", "Windows: output.exe; macOS GUI: output.app; Linux/macOS terminal: executable file."));
            if ((File.Exists(destination) || Directory.Exists(destination)) && !overwrite)
                throw new ArgumentException(GetLocalizedString("SfxExists", "The output file already exists. Use --overwrite to replace it."));
            var safety = CheckZipBomb(source);
            if (safety.Blocked) return (ExitCode)safety.ExitCode;
            if (runAfter is not null && !ReadArchiveList(source, out _).Any(entry =>
                entry.Name.Replace('\\', '/').Equals(runAfter.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(GetLocalizedString("SfxRunMissing", "The file to open after extraction is not in the archive: {0}", runAfter));

            var options = new SfxPackageOptions
            {
                AutoStart = autoStart, ShowReadme = readme, CloseOnSuccess = close,
                FileListMode = fileList, DestinationFolder = folder,
                DestinationSubpath = subfolder, RunAfterPath = runAfter,
                IconPath = icon, Message = message
            };
            if (target != "win-x64" || ui != "gui")
            {
                CreatePortableSfx(source, destination, options, target, ui, overwrite);
                if (!IsSilent) Console.WriteLine(GetLocalizedString("SfxCreated", "Self-extracting archive created: {0}", destination));
                return ExitCode.Success;
            }
            if (icon is not null && !OperatingSystem.IsWindows())
                throw new ArgumentException(GetLocalizedString("SfxIconWindows", "L’icona della GUI Windows può essere personalizzata su Windows. / Customizing the Windows GUI icon requires Windows."));
            // The shared writer validates options and builds atomically. Publish separately
            // so an output created meanwhile cannot be overwritten without --overwrite.
            string staged = Path.Combine(Path.GetDirectoryName(destination)!, ".zg-sfx-" + Guid.NewGuid().ToString("N") + ".exe");
            string templateDirectory = Path.Combine(Path.GetTempPath(), "zg-sfx-template-" + Guid.NewGuid().ToString("N"));
            string template = Path.Combine(templateDirectory, "sfx_stub.exe");
            try
            {
                // Embedded resources also work when zg is published as a single file.
                using (Stream embedded = typeof(Program).Assembly.GetManifestResourceStream("zg.SfxStub.exe")
                    ?? throw new InvalidOperationException(GetLocalizedString("SfxEmbeddedMissing", "The built-in SFX template is missing. Reinstall zgshell.")))
                {
                    Directory.CreateDirectory(templateDirectory);
                    using var file = new FileStream(template, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    embedded.CopyTo(file);
                }
                SfxPackageWriter.Create(template, source, staged, options);
                File.Move(staged, destination, overwrite);
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
                if (File.Exists(template)) File.Delete(template);
                if (Directory.Exists(templateDirectory)) Directory.Delete(templateDirectory);
            }
            if (!IsSilent)
                Console.WriteLine(GetLocalizedString("SfxCreated", "Self-extracting archive created: {0}", destination));
            return ExitCode.Success;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.FileDoesNotExists;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.CompressionError;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.IOError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(GetLocalizedString("SfxFailed", "Could not create the self-extracting archive: {0}", ex.Message));
            return ExitCode.CompressionError;
        }
    }
}
