using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace zg
{
    internal static class SqxNative
    {
        private const int Success = 0;
        private const int MaxPath = 260;
        private static readonly object CurrentDirectoryLock = new object();
        private static readonly SqxCallback ContinueCallback = OnCallback;

        internal static void ExtractArchive(string archivePath, string outputPath, string password = "")
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                throw new PlatformNotSupportedException("SQX is not supported on ARM64.");
            Directory.CreateDirectory(outputPath);
            string bridgePath = Path.Combine(AppContext.BaseDirectory, "Libs", Environment.Is64BitProcess ? "x64" : "x86", "SqxBridge.dll");
            if (!File.Exists(bridgePath))
                throw new FileNotFoundException("SQX native bridge not found.", bridgePath);

            if (Environment.Is64BitProcess)
            {
                RunWithX86Bridge("RunSqxExtract", "estrarre", archivePath, outputPath, password ?? string.Empty);
            }
            else
            {
                IntPtr bridge = NativeLibrary.Load(bridgePath);
                try
                {
                    ExtractArchiveBridgeDelegate extract = Marshal.GetDelegateForFunctionPointer<ExtractArchiveBridgeDelegate>(
                        NativeLibrary.GetExport(bridge, "SqxExtractArchive"));
                    int result = extract(archivePath, outputPath, password ?? string.Empty);
                    if (result == 17) // SQX_ERR_FILE_ENCRYPTED
                        throw new InvalidOperationException("SQX archive requires a valid password.");
                    if (result != Success)
                        throw new InvalidOperationException($"SQX extraction failed with error code {result}.");
                }
                finally
                {
                    NativeLibrary.Free(bridge);
                }
            }
        }

        internal static void CreateArchive(string archivePath, string sourceFolder, int compressionRate, ulong volumeSize = 0, bool solidCompression = true, string password = "")
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                throw new PlatformNotSupportedException("SQX is not supported on ARM64.");
            string fullArchivePath = Path.GetFullPath(archivePath);
            string fullSourcePath = Path.GetFullPath(sourceFolder);
            string archiveDirectory = Path.GetDirectoryName(fullArchivePath);
            if (!string.IsNullOrWhiteSpace(archiveDirectory))
                Directory.CreateDirectory(archiveDirectory);

            string[] files = Directory.GetFiles(fullSourcePath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                return;

            if (Environment.Is64BitProcess)
            {
                RunWithX86Bridge("RunSqxCompress", "creare", fullArchivePath, fullSourcePath,
                    compressionRate.ToString(), solidCompression ? "1" : "0", volumeSize.ToString(), password ?? string.Empty);
                return;
            }

            lock (CurrentDirectoryLock)
            {
                string previousDirectory = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = archiveDirectory ?? previousDirectory;
                    File.Delete(fullArchivePath);

                    using Session session = Session.Open(Path.GetFileName(fullArchivePath));
                    if (session == null)
                        return;

                    IntPtr fileList = session.InitFileList(session.ArchiveHandle);
                    if (fileList == IntPtr.Zero)
                        return;

                    try
                    {
                        foreach (string file in files)
                        {
                            string relativePath = Path.GetRelativePath(fullSourcePath, file)
                                .Replace(Path.DirectorySeparatorChar, '\\');
                            session.AppendFileList(session.ArchiveHandle, fileList, relativePath);
                        }

                        SqxCompressOptions options = new SqxCompressOptions
                        {
                            Size = (uint)Marshal.SizeOf<SqxCompressOptions>(),
                            FileFormat = 1,
                            Compression = new SqxForceCompression
                            {
                                CompressionRate = (uint)Math.Clamp(compressionRate, 0, 5),
                                Solid = solidCompression ? 1 : 0,
                                DictionarySize = 10,
                                ExeCompression = 1,
                                TextCompression = 1,
                                MultimediaCompression = 1,
                                AudioCompression = 1,
                                RgbCompression = 1
                            },
                            Encryption = string.IsNullOrEmpty(password) ? 0u : 4u,
                            Password = password ?? string.Empty,
                            RetainFolderStructure = 1,
                            InputPath = fullSourcePath,
                            FileNames = 0,
                            StoreWin32FileTime = 1,
                            TempDirectory = Path.GetTempPath(),
                            VolumeSize = volumeSize,
                            AutoVolumeSize = 0
                        };

                        int result = session.CompressFiles(session.ArchiveHandle, ContinueCallback, IntPtr.Zero, fileList, ref options);
                        if (result != Success)
                            return;
                    }
                    finally
                    {
                        session.DoneFileList(session.ArchiveHandle, fileList);
                    }
                }
                finally
                {
                    Environment.CurrentDirectory = previousDirectory;
                }
            }
        }

        private static int OnCallback(IntPtr _, IntPtr __) => 1;

        private static void RunWithX86Bridge(string entryPoint, string operationName, params string[] arguments)
        {
            string x86BridgePath = Path.Combine(AppContext.BaseDirectory, "Libs", "x86", "SqxBridge.dll");
            if (!File.Exists(x86BridgePath))
                throw new FileNotFoundException("SQX x86 native bridge not found.", x86BridgePath);

            string rundll32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "rundll32.exe");
            if (!File.Exists(rundll32Path))
                throw new FileNotFoundException("SysWOW64 rundll32.exe not found.", rundll32Path);

            string statusPath = Path.Combine(Path.GetTempPath(), $"ZipGeniusSqx-{Guid.NewGuid():N}.result");
            string processArguments = $"\"{x86BridgePath}\",{entryPoint}";
            foreach (string argument in arguments)
                processArguments += " " + QuoteProcessArgument(argument);
            processArguments += " " + QuoteProcessArgument(statusPath);

            try
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = rundll32Path,
                    Arguments = processArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process.WaitForExit();

                int result = -1;
                bool completed = File.Exists(statusPath) && int.TryParse(File.ReadAllText(statusPath), out result);
                if (!completed)
                    throw new InvalidOperationException($"SQX native bridge did not return an {operationName} result.");
                if (result == 17) // SQX_ERR_FILE_ENCRYPTED
                    throw new InvalidOperationException("SQX archive requires a valid password.");
                if (result != Success)
                    throw new InvalidOperationException($"SQX native bridge failed to {operationName} with error code {result}.");
            }
            finally
            {
                if (File.Exists(statusPath))
                    File.Delete(statusPath);
            }
        }

        private static string QuoteProcessArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            StringBuilder quoted = new StringBuilder(argument.Length + 2);
            quoted.Append('"');

            int backslashCount = 0;
            foreach (char ch in argument)
            {
                if (ch == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (ch == '"')
                {
                    quoted.Append('\\', backslashCount * 2 + 1);
                    quoted.Append('"');
                    backslashCount = 0;
                    continue;
                }

                quoted.Append('\\', backslashCount);
                quoted.Append(ch);
                backslashCount = 0;
            }

            quoted.Append('\\', backslashCount * 2);
            quoted.Append('"');
            return quoted.ToString();
        }

        private static ProcessStartInfo WithArguments(this ProcessStartInfo startInfo, params string[] arguments)
        {
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            return startInfo;
        }

        private sealed class Session : IDisposable
        {
            private readonly IntPtr library;
            internal readonly InitArchiveDelegate InitArchive;
            internal readonly DoneArchiveDelegate DoneArchive;
            internal readonly InitFileListDelegate InitFileList;
            internal readonly AppendFileListDelegate AppendFileList;
            internal readonly DoneFileListDelegate DoneFileList;
            internal readonly ExtractFilesDelegate ExtractFiles;
            internal readonly CompressFilesDelegate CompressFiles;
            internal IntPtr ArchiveHandle;

            private Session(IntPtr library)
            {
                this.library = library;
                InitArchive = GetDelegate<InitArchiveDelegate>("SqxInitArchive");
                DoneArchive = GetDelegate<DoneArchiveDelegate>("SqxDoneArchive");
                InitFileList = GetDelegate<InitFileListDelegate>("SqxInitFileList");
                AppendFileList = GetDelegate<AppendFileListDelegate>("SqxAppendFileList");
                DoneFileList = GetDelegate<DoneFileListDelegate>("SqxDoneFileList");
                ExtractFiles = GetDelegate<ExtractFilesDelegate>("SqxExtractFiles");
                CompressFiles = GetDelegate<CompressFilesDelegate>("SqxCompressFiles");
            }

            internal static Session Open(string archivePath)
            {
                string libraryPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Libs",
                    Environment.Is64BitProcess ? "x64" : "x86",
                    "sqx20u.dll");
                if (!File.Exists(libraryPath))
                    return null;

                IntPtr library = NativeLibrary.Load(libraryPath);
                Session session = null;
                try
                {
                    session = new Session(library);
                    int result = session.InitArchive(archivePath, ContinueCallback, IntPtr.Zero, out session.ArchiveHandle);
                    if (result != Success || session.ArchiveHandle == IntPtr.Zero)
                        return null;
                    return session;
                }
                catch
                {
                    session?.Dispose();
                    if (session == null)
                        NativeLibrary.Free(library);
                    return null;
                }
            }

            private T GetDelegate<T>(string name) where T : Delegate =>
                Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

            public void Dispose()
            {
                if (ArchiveHandle != IntPtr.Zero)
                {
                    DoneArchive(ArchiveHandle);
                    ArchiveHandle = IntPtr.Zero;
                }
                NativeLibrary.Free(library);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SqxCallback(IntPtr parameter, IntPtr callbackInfo);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int ExtractArchiveBridgeDelegate(string archivePath, string outputPath, string password);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int InitArchiveDelegate(string archiveName, SqxCallback callback, IntPtr parameter, out IntPtr archiveHandle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void DoneArchiveDelegate(IntPtr archiveHandle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr InitFileListDelegate(IntPtr archiveHandle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate void AppendFileListDelegate(IntPtr archiveHandle, IntPtr fileMaskList, string fileName);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void DoneFileListDelegate(IntPtr archiveHandle, IntPtr fileMaskList);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ExtractFilesDelegate(IntPtr archiveHandle, SqxCallback callback, IntPtr parameter, IntPtr fileMaskList, IntPtr options);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CompressFilesDelegate(IntPtr archiveHandle, SqxCallback callback, IntPtr parameter, IntPtr fileMaskList, ref SqxCompressOptions options);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SqxForceCompression
        {
            public uint CompressionRate; public int Solid; public uint DictionarySize; public uint ExeCompression; public uint TextCompression;
            public uint TextMemory; public uint TextOrder; public uint MultimediaCompression; public uint MultimediaChannels; public uint AudioCompression;
            public uint AudioChannels; public uint AudioChannelsWidth; public uint RgbCompression; public uint UltraMatchLink;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        private struct SqxVersionInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string LegalCopyright;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string CompanyName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string FileDescription;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string FileVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string ProductName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string ProductVersion;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        private struct SqxSfxCommand
        {
            public int CreateSfx; public uint SfxType; public uint SfxSystem;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string HintTitle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4096)] public string HintMessage;
            public uint HintButton;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AboutMessage;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string NormalFolder;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string NormalCommand;
            public int NormalOverwrite; public int NormalAutoExtract;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string InstallMessage;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string InstallCommand;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string InstallWaitFor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string IconFile;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string LanguageFile;
            public SqxVersionInfo Version;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        private struct SqxCompressOptions
        {
            public uint Size; public uint FileFormat; public SqxForceCompression Compression; public SqxSfxCommand Sfx;
            public uint RecoveryData; public int ExternalRecoveryData; public IntPtr MainComment;
            public uint Encryption; public int EncryptHeaders; public int AvSignature; public int ExternalAvSignature; public uint AvEnvelope;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string SignatureFile;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string Password;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string HeaderPassword;
            public int RetainFolderStructure; public int RetainDrivePart;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string RelativePath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string InputPath;
            public uint FileNames; public int StoreWin32FileTime; public int StoreAcls; public int StoreStreams; public int AutoSaveComments; public int ExtendedTest;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string TempDirectory;
            public ulong VolumeSize; public int AutoVolumeSize; public int CreateAlwaysNewArchive; public int ResetArchiveAttribute; public uint LogType;
        }
    }
}
