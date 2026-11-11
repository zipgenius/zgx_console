using ConsoleTables;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Iso9660;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using DiscUtils.Vhd;
using DiscUtils.Wim;
using Ionic.Zip;
using ICSharpCode.SharpZipLib.BZip2;
using Microsoft.CST.RecursiveExtractor;
using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Tsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Utilities;
// using SevenZip;
using SevenZipExtractor;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using ShellProgressBar;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Linq;
//using System.IO.Compression;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Windows.Devices.Geolocation;
using Windows.Graphics.Imaging;
using Windows.Services.Maps.OfflineMaps;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Shapes;
using zg.Properties;
using static System.ConsoleColor;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using CompressionLevel = SevenZip.CompressionLevel;
using Entry = SevenZipExtractor.Entry;
using File = System.IO.File;
using FileAttributes = System.IO.FileAttributes;
using Path = System.IO.Path;
using ZipFile = Ionic.Zip.ZipFile;




//using ZipFile = Ionic.Zip.ZipFile;

namespace zg
{
    partial class Program
    {
        public ResourceManager res_man;
        public CultureInfo cul;
        public static Ionic.Zip.ZipFile zip;

        public static ProgressBar pBar;

        const int buffSize = 2097152;

        public static bool DoWait = false;
        public static bool IsSilent = false;
        public static bool IsVerbose = false;
        public static bool DoOpenFolder = false;
        public static bool DoShowProgress = true;
        public static bool DoAlwaysConfirm = false;
        public static bool DoSimulate = false;
        public static bool IsDeleting = false;

        public static string TempPath = Path.GetTempPath();
        public static int VerboseLevel = 0;

        public static ExitCode ErrorCode;
        public static EncryptionMode EncMode;

        public static List<string> fInclusion;
        public static List<string> fExclusion;
        public static List<string> fExclusionExt;
        public static Dictionary<string, string> filesDictionary;
        public static Dictionary<string, string> dirsDictionary;
        public static Dictionary<string, string> hashDictionary;

        static int FilesAdded = 0;
        static int FilesToAdd = 0;
        static int DirsAdded = 0;
        static int FilesInArc = 0;
        static int SkippedFiles = 0;

        static int BlockLines = 1;

        static long NestedGlobalSize = 0;

        public enum ExitCode : int
        {
            Success = 0,
            CompressionError = 1,
            ExtractionError = 2,
            FileDoesNotExists = 3,
            ZeroItemsArchive = 4,
            DeprecatedCommand = 5,
            ArchiveTestFailed = 6,
            ArchiveTestUnsupported = 7,
            ArchiveTestPassword = 8,
            InfoIncomplete = 9,
            BytesPatternNotFound = 10,
            TextPatternNotFound = 11,
            CanceledByUser = 20,
            IOError = 30,
            ZipBombWarning = 41,
            ZipBombDanger = 42,
            ZipBombUnverified = 43,
            UnknownError = 99
        }


        public enum PathStoreMode : int
        {
            NoPath = 0,
            RelativeFolder = 1,
            FullPathNoDrive = 2,
            FullPathWithDrive = 3
        }

        public enum EncryptionMode : int
        {
            encZipCrypto = 0,
            encAES128 = 1,
            encAES256 = 2
        }

        public static string[] ZipLike =
        {
            ".zip",".xpi",".apk",
            ".odt",".ods",".odp",".odg",".otg",
            ".otp",".odm",".odf",".odb",".ots",
            ".ott",".oxt",".docx",".xlsx",".pptx",
            ".cbz", ".mskin", ".wal", ".dsfz",
            ".wmz",".pcv",".sit",".bsz",".wsz",
            ".pak",".pk3",".eopkg", ".vsix", ".msixbundle",
            ".msix",".jar",".war",".ear", ".appx",
            ".bin"
        };

        public static string[] SevenZipLike =
        {
            ".7z",".msi",".cab",".deb",
            ".arj", ".dmg", ".rpm",".cpio",".tar",
            ".lzh",".lha",".wim",".nrg"
        };
        public static string[] ExoticLike =
        {
            ".yz1",".bh",".rar",".c2d",".pkg", ".img"
        };

        public static string[] IsoLike =
        {
            ".iso",
            ".hfs",".udf",".vdi",
            ".vhd", ".vhdx",".vmdk",
            ".xfs",".xva"
        };



        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.CancelKeyPress += Console_CancelKeyPress;
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Gray;
            ErrorCode = 0;
            //Console.WriteLine(args);
            if (args.Length > 0)
            {
                if (args[0].Equals("-dumpdisc", StringComparison.OrdinalIgnoreCase) ||
                    args[0].Equals("-dd", StringComparison.OrdinalIgnoreCase))
                    Console.OutputEncoding = new UTF8Encoding(false);

                // foreach (string entry in args)
                //    Console.WriteLine("> " + entry);

                foreach (string par in args)
                {
                    if (par == "|")
                        break;
                    if (par.StartsWith("--"))
                    {
                        string pSwitch = par.Remove(0, 2).ToLower();
                        if (pSwitch.Split('=', 2)[0] is "t" or "temp")
                        {
                            string[] tParts = null;
                            try
                            {
                                tParts = pSwitch.Split("=");
                                TempPath = tParts[1];
                            }
                            catch
                            {
                                TempPath = Path.GetTempPath();
                            }

                        }
                        else if (pSwitch.Split('=', 2)[0] is "v" or "verbose")
                        {
                            /// --verbose
                            /// --verbose or --verbose=0 -> Only main info
                            /// --verbose=1 -> also prints files and dirs list on screen
                            /// --verbose=2 -> verbose info are written to file "(archive name_ext).txt" in the same folder of the zip archive
                            /// 

                            IsVerbose = true;
                            string[] vParts = null;
                            try
                            {
                                vParts = pSwitch.Split("=");
                                if (!int.TryParse(vParts[1], out VerboseLevel))
                                {
                                    VerboseLevel = 0;
                                }
                                if (VerboseLevel > 2 || VerboseLevel < 0)
                                    VerboseLevel = 0;
                            }
                            catch
                            {
                                VerboseLevel = 0;
                            }
                        }
                        else
                        {
                            switch (pSwitch)
                            {
                                case "w":
                                case "wait":
                                    {
                                        DoWait = true;
                                        break;
                                    }
                                /// --s or --silent turn off:
                                /// - Header
                                /// - Progressbar
                                /// - Verbose info by default (IsVerbose will be ignored)
                                /// - Waiting for a key (DoWait will be ignored)
                                case "s":
                                case "silent":
                                    {
                                        IsSilent = true;
                                        break;
                                    }
                                //case "v":
                                //case "verbose":
                                //    {
                                //        IsVerbose = true;
                                //        break;
                                //    }
                                case "o":
                                case "open":
                                    {
                                        DoOpenFolder = true;
                                        break;
                                    }
                                case "p":
                                case "progressoff":
                                case "noprogress":
                                    {
                                        DoShowProgress = false;
                                        break;
                                    }
                                case "y":
                                case "always":
                                case "yes":
                                case "donotask":
                                    {
                                        DoAlwaysConfirm = true;
                                        break;
                                    }
                                case "simulate":
                                    {
                                        DoSimulate = true;
                                        break;
                                    }
                            }
                        }
                    }
                }

                if (!IsSilent)
                {
                    PrintHeader();
                }
                else
                {
                    IsVerbose = false;
                    DoOpenFolder = false;
                    DoWait = false;
                    DoShowProgress = false;
                }

                //Console.WriteLine(args[0].ToLower());
                switch (args[0].ToLower())
                {
                    case "-install":
                        {
                            bool systemWide = args.Skip(1).Any(arg =>
                                arg.Equals("--system-wide", StringComparison.OrdinalIgnoreCase) ||
                                arg.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                                arg.Equals("system", StringComparison.OrdinalIgnoreCase));
                            InstallApp(systemWide);
                            break;
                        }
                    case "-uninstall":
                        {
                            bool systemWide = args.Skip(1).Any(arg =>
                                arg.Equals("--system-wide", StringComparison.OrdinalIgnoreCase) ||
                                arg.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                                arg.Equals("system", StringComparison.OrdinalIgnoreCase));
                            UninstallApp(systemWide);
                            break;
                        }
                    case "-yourpath":
                        {
                            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            string directory = System.IO.Path.GetDirectoryName(exePath);
                            Console.WriteLine(GetLocalizedString("ApplicationLocation", "I am installed into the following path:"));
                            Console.WriteLine(directory);
                            break;
                        }
                    case "-a":
                    case "-add":
                        {
                            ErrorCode = ProcessAddCompatible(args);
                            break;
                        }
                    case "-b":
                    case "-backup":
                        {
                            ErrorCode = CreateBackup(args);
                            break;
                        }
                    case "-l":
                    case "-list":
                        {
                            ErrorCode = OpenArchive(args);
                            break;
                        }
                    case "-info":
                        {
                            ErrorCode = FileInformation(args);
                            break;
                        }
                    case "-t":
                    case "-test":
                        {
                            ErrorCode = TestArchive(args);
                            break;
                        }
                    case "-export":
                        {
                            ErrorCode = ExportArchiveList(args);
                            break;
                        }
                    case "-x":
                    case "-extract":
                        {
                            ErrorCode = ExtractFiles(args);
                            break;
                        }
                    case "-hh":
                    case "-hash":
                        {
                            ErrorCode = HashArchive(args);
                            break;
                        }
                    case "-d":
                    case "-delete":
                        {
                            ErrorCode = ProcessDelete(args);
                            break;
                        }
                    case "-destroy":
                        {
                            ErrorCode = DestroyArchive(args);
                            break;
                        }
                    case "-dumpdisc":
                    case "-dd":
                        {
                            DumpDisc(args);
                            break;
                        }
                    
                    case "-hexview":
                        {
                            ErrorCode = HexDump3(args);
                            break;
                        }
                    case "-sfx":
                        {
                            ErrorCode = CreateSfx(args);
                            break;
                        }
                    case "-opt":
                    case "-optim":
                    case "-optimize":
                        {
                            ErrorCode = OptimizeFiles(args);
                            break;
                        }
                    case "-convert":
                    case "-cvt":
                        {
                            ErrorCode = ConvertArchive(args);
                            break;
                        }
#if DEBUG
                    case "-bombscan":
                        {
                            ScanForBomb2(args);
                            break;
                        }
                    case "-testtable":
                        {
                            //DoCheck2(args);
                            PrintArchiveInfoTable(args);
                            break;
                        }
                    case "-testdump":
                        {


                            // string hexValues = "50 4b 03 04 14 00 00 00";
                            // byte[] hBytes = new byte[16]{ 0x50,0x4b,0x03,0x04,0x14,0x00,0x00,0x00,
                            //                                 0x08,0x00,0x60,0x08,0xD9,0x32,0x08,0xED};


                            byte[] hBytes = File.ReadAllBytes("/home/matteo/Scaricati/UPX.zip");
                            var result = HexDumper.ByteConverter.ConvertToHexDump(hBytes);

                            Console.WriteLine(result);

                            // string[] hexValuesSplit = hexValues.Split(' ');
                            // foreach (string hex in hexValuesSplit)
                            // {
                            //     // Convert the number expressed in base-16 to an integer.
                            //     int value = Convert.ToInt32(hex, 16);
                            //     // Get the character corresponding to the integral value.
                            //     string stringValue = Char.ConvertFromUtf32(value);
                            //     char charValue = (char)value;
                            //     Console.WriteLine("hexadecimal value = {0}, int value = {1}, char value = {2} or {3}",
                            //                         hex, value, stringValue, charValue);
                            // }
                            break;
                        }
                    case "-testsig":
                        {
                            Console.WriteLine(CheckSignature(args[1]));
                            break;
                        }
                    case "-testexport":
                        {
                            Dictionary<string, string> dic = new Dictionary<string, string>();
                            for (int i = 0; i < 20; i++)
                            {
                                Random r = new Random();
                                int rVal = r.Next(255);
                                dic.Add("Entry " + i.ToString(), rVal.ToString());
                            }
                            ExportDictToJson(dic,
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "JSON_test", "test.json"),
                                @"Z:\Test\MyTestHash8.zip");
                            ExportDictToXml(dic,
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "XML_test", "test.xml"),
                                @"Z:\Test\MyTestHash8.zip");
                            ExportDictToHtml(dic,
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HTML_test", "test.html"),
                                @"Z:\Test\MyTestHash8.zip");
                            Console.WriteLine(GetLocalizedString("DoneLower", "done"));
                            break;
                        }
                    case "-testextract":
                        {
                            ZipFile zip = new ZipFile(@"Z:\Test\MyTest_638010425422770009.zip");
                            zip.ExtractProgress += Zip_ExtractProgress;
                            string dest = Environment.TickCount.ToString();
                            foreach (var entry in zip.Entries)
                            {
                                //Console.WriteLine(entry.FileName + "\t\t\t\t" + entry.Attributes.ToString());
                                entry.Extract(@"Z:\Test\ExtractionTest\" + dest);
                            }
                            //zip.ExtractAll(@"Z:\Test\ExtractionTest\");

                            break;
                        }
                    case "-testbeep":
                        {

                            DoSuccessSound();

                            break;
                        }
                    case "-testvar":
                        {
                            string res = Environment.ExpandEnvironmentVariables(@"%UserProfile%\Desktop\test 1\test 2\test 3\text.txt");
                            Console.WriteLine(res);
                            break;
                        }

                    case "-testcab":
                        {
                            ProcessAddCab(args);
                            break;
                        }
                    case "-testprog":
                        {
                            TestProgress();
                            break;
                        }
                    case "-testzip":
                        {
                            string LibPath = FileUtils.GetSevenZipLibs();
                            ArchiveFile nFile = new ArchiveFile(args[1], LibPath);
                            Console.WriteLine(args[1]);
                            Console.WriteLine(nFile.Entries.Count.ToString());
                            foreach (SevenZipExtractor.Entry entry in nFile.Entries)
                            {
                                Console.WriteLine(entry.FileName + " - " + entry.PackedSize.ToString() + "/" + entry.Size.ToString());
                            }
                            break;
                        }

#endif
                    case "-help":
                    case "-?":
                        {
                            // Show Help
                            string? helpCommand = args.Skip(1).FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
                            ShowHelp(helpCommand ?? "NOCMD");
                            break;
                        }
                    default:
                        {
                            Console.Error.WriteLine(GetLocalizedString("UnknownCommand",
                                "Unknown command: {0}. Use zg -help to view available commands.", args[0]));
                            ErrorCode = ExitCode.UnknownError;
                            break;
                        }
                }

            }
            else
            {
                PrintStartupHelp();
            }

            if (DoWait)
                WaitInput();
            Console.ResetColor();
            Environment.Exit((int)ErrorCode);

        }

        private static ExitCode DumpDisc(string[] args){
            if (args.Length <= 1){
                //Console.WriteLine(args[1]);
                return ExitCode.FileDoesNotExists;
            } else {
                if (Path.GetExtension(args[1]).ToLower() == ".vhd" ||
                    Path.GetExtension(args[1]).ToLower() == ".vhdx")
                    return DumpVhd(args);
                else
                return DumpIso(args);
            }
        }

        private static ExitCode DumpIso(string[] args){

            string isoPath = args[1];
            bool mOnlyDirs = false;     // -od --only-dirs
            bool mOnlyFiles = false;    // -of --only-files
            bool mShowSize = false;     // -sz --show-size
            bool mShowDate = false;     // -sd --show-date
            bool mShowSummary = false;  // -sm --show-summary
            bool mShowTitle = false;    // -st --show-title
            bool mShowHash = false;     // -sh --show-hash

            for (int i=2; i<args.Length; i++){
                switch(args[i].ToLower()){
                    case "-od":
                    case "--only-dirs":{
                        mOnlyDirs = true; break;
                    }
                    case "-of":
                    case "--only-files":{
                        mOnlyFiles = true; break;
                    }
                    case "-sz":
                    case "--show-size":{
                        mShowSize = true; break;
                    }
                    case "-sd":
                    case "--show-date":{
                        mShowSize = true; break;
                    }
                    case "-sm":
                    case "--show-summary":{
                        mShowSummary = true; break;
                    }
                    case "-st":
                    case "--show-title":
                        {
                            mShowTitle = true; break;
                        }
                    case "-sh":
                    case "--show-hash":{
                        mShowHash= true; break;
                    }
                }
            }
            if (mOnlyDirs == true && mOnlyFiles == true){
                mOnlyDirs = false;
                mOnlyFiles = false;
            }
            try
            {
                using FileStream isoStream = File.OpenRead(isoPath);
                
                CDReader cd = new CDReader(isoStream, true);
                if (mShowTitle){
                    FileInfo fInfo = new FileInfo(isoPath);
                    string mSha256 = "";
                    if (mShowHash)
                         mSha256 = CryptoUtils.Sha256FileHash(isoPath);
                    Console.WriteLine($"ISO file: {Path.GetFileName(isoPath)}");
                    string volumeName = cd.VolumeLabel;
                    Console.WriteLine($"Size: {fInfo.Length.ToString("N0")} bytes");
                    if (mShowHash)
                        Console.WriteLine($"SHA-256: {mSha256}");
                    //Console.WriteLine($"Volume Identifier: {volumeName}");

                    byte[] sector = new byte[2048];
                    isoStream.Seek(16 * 2048, SeekOrigin.Begin); // Sector 16
                    isoStream.Read(sector, 0, sector.Length);

                    string volumeId = Encoding.ASCII.GetString(sector, 40, 32);
                    string applicationId = Encoding.ASCII.GetString(sector, 318, 128);
                    string creationDateRaw = Encoding.ASCII.GetString(sector, 813, 17).TrimEnd('\0');
                    Console.WriteLine($"Volume Identifier:   {volumeId}");
                    Console.WriteLine($"Application ID:      {applicationId}");
                    Console.WriteLine($"Creation Date (raw): {creationDateRaw}");
                    string dt = creationDateRaw.Substring(0, 14);
                    DateTime creationDate = DateTime.ParseExact(dt.ToString().Trim(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    Console.WriteLine($"Creation Date:       {creationDate}");
                    string msgJoliet = "";
                    if (cd.ActiveVariant == Iso9660Variant.Joliet)
                        msgJoliet = "ISO uses Joliet extensions";
                    else
                        msgJoliet = "ISO does not use Joliet extensions";
                    Console.WriteLine(msgJoliet);
                    Console.WriteLine("--------------------------------------------------------");
                }

                
                int fileCount = 0;
                int dirCount = 0;

                PrintDirectory(cd, "");
                
                void PrintDirectory(CDReader reader, string path)
                {
                    foreach (string entry in reader.GetFileSystemEntries(path))
                    {
                        string fullPath = Path.Combine(path, entry);
                        if (reader.DirectoryExists(fullPath))
                        {
                            dirCount++;
                            if ((!mOnlyFiles && !mOnlyDirs) || (!mOnlyFiles && mOnlyDirs))
                                Console.WriteLine($"[DIR]  {fullPath}");
                            PrintDirectory(reader, fullPath);
                        }
                        else if (mOnlyDirs!=true)
                        {
                            fileCount++;
                            string mFileOutput = $"[FILE] {fullPath}";
                            if (mShowSize){
                                long size = reader.GetFileLength(fullPath);
                                string formattedSize = size.ToString("N0");
                                mFileOutput+=$" ({formattedSize} bytes)";
                            }
                            Console.WriteLine(mFileOutput);
                            
                        }
                    }
                }

                if (mShowSummary){
                    Console.WriteLine("--------------------------------------------------------");
                    Console.WriteLine($"Directories: {dirCount.ToString("N0")}");
                    Console.WriteLine($"Files: {fileCount.ToString("N0")}");
                    Console.WriteLine("--------------------------------------------------------");
                }

                return ExitCode.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading ISO: {ex.Message}");
                return ExitCode.IOError;
            }
        }

        private static ExitCode DumpVhd(string[] args)
        {
            Console.WriteLine("VHD Dump");
            string vhdPath = args[1];
            try
            {
                using FileStream vhdStream = File.OpenRead(vhdPath);
                VirtualDisk vhd = new Disk(vhdStream, Ownership.None);

                PartitionTable partitionTable = vhd.Partitions;

                foreach (PartitionInfo partition in partitionTable.Partitions)
                {
                    Console.WriteLine($"Partition: {partition.TypeAsString} ({partition.FirstSector}–{partition.LastSector})");

                    using Stream fsStream = partition.Open();

                    DiscFileSystem fs = null;

                    // Try NTFS
                    try
                    {
                        fs = new NtfsFileSystem(fsStream);
                        Console.WriteLine("Filesystem: NTFS");
                    }
                    catch
                    {
                        // Try FAT
                        try
                        {
                            fsStream.Position = 0;
                            fs = new FatFileSystem(fsStream);
                            Console.WriteLine("Filesystem: FAT");
                        }
                        catch
                        {
                            Console.WriteLine("Unsupported or unknown filesystem.");
                        }
                    }

                    if (fs != null)
                    {
                        ListFiles(fs, @"\");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading VHD: {ex.Message}");
            }
            return ExitCode.Success;
        }

        static void ListFiles(DiscFileSystem fs, string path)
        {
            foreach (var entry in fs.GetFileSystemEntries(path))
            {
                string fullPath = Path.Combine(path, Path.GetFileName(entry));
                if (fs.DirectoryExists(fullPath))
                {
                    Console.WriteLine($"[DIR]  {fullPath}");
                    ListFiles(fs, fullPath);
                }
                else
                {
                    long size = fs.GetFileLength(fullPath);
                    Console.WriteLine($"[FILE] {fullPath} ({size:N0} bytes)");
                }
            }
        }

        private static void PrintArchiveInfoTable(string[] args)
        {
            string ArcFile = args[1];
            Console.WriteLine("");
            Console.WriteLine(ArcFile);
            var table = new ConsoleTable("File", "Size", "Packed size", "Ratio", "CRC-32");
            //table.Options.NumberAlignment = Alignment.Right;
            table.Configure(o => o.NumberAlignment = Alignment.Right);
            ArchiveFile inFile = new ArchiveFile(ArcFile, FileUtils.GetSevenZipLibs());

            foreach (Entry entry in inFile.Entries)
                if (!entry.IsFolder)
                    table.AddRow(entry.FileName, entry.Size.ToString("N0"), entry.PackedSize.ToString("N0"), 0, entry.CRC.ToString("X8"));

            table.Write(Format.Minimal);
            Console.WriteLine();
        }

        /*
       private static ExitCode DoCheck(string[] args)
       {
           string arcFile = args[1];
           string LibPath = string.Empty;
           if (IsLinux())
           {
               Console.WriteLine("We are in linux");
               LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "linux", "7z.so");
           }
           else
           {
               Console.WriteLine("This is WIndows");
               if (Environment.Is64BitProcess)
                   LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "64bit", "7z.dll");
               else
                   LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "32bit", "7z.dll");
           }
           SevenZip.SevenZipBase.SetLibraryPath(LibPath);
           using (FileStream fs = new FileStream(arcFile, FileMode.Open, FileAccess.Read))
           {
               var uncompressedSizeCounts = new Dictionary<long, int>();
               var compressionRatioCounts = new Dictionary<double, int>();
               var crcCounts = new Dictionary<int, int>();

               int nestedZip = 0;
               int sameCrc = 0;
               int sameSize = 0;
               int sameRatio = 0;
               bool IsNested = false;
               long GlobalUnpackedSize = 0;

               SevenZipExtractor szip = new SevenZipExtractor(fs);

               if (IsVerbose)
               {
                   FileInfo fInfo = new FileInfo(args[1]);
                   Console.WriteLine("Reading " + args[1] + " (" + FormatSize(fInfo.Length) + ")...");
                   Console.WriteLine("------------------------------------");
               }


               foreach (ArchiveFileInfo entry2 in szip.ArchiveFileData)
               {

                   if (entry2.IsDirectory)
                       continue;
                   Console.WriteLine("# " + entry2.FileName);
                   using (MemoryStream ms2 = new MemoryStream())
                   {
                       szip.ExtractFile(entry2.Index, ms2);
                       //Console.WriteLine("Length: "+ms2.Length+" | "+entry2..ToString());
                   }
               }

               return ExitCode.Success;
               foreach (ArchiveFileInfo entry in szip.ArchiveFileData)
               {
                   if (!entry.IsDirectory)
                   {
                       int crc = (int)entry.Crc;

                       using (MemoryStream ms = new MemoryStream())
                       {
                           Console.WriteLine(entry.FileName);
                           //szip.ExtractFile(entry.Index, ms);
                           GlobalUnpackedSize += 3; //ms.Length;
                           long uncompressedSize = 3; //ms.Length;
                           double compressionRatio = (uncompressedSize - 3) / uncompressedSize;

                           if (Path.GetExtension(entry.FileName).ToLower() == ".zip")
                           {
                               nestedZip++;
                               //TraverseZip(ms);

                           }
                           if (uncompressedSizeCounts.ContainsKey(uncompressedSize))
                           {
                               uncompressedSizeCounts[uncompressedSize]++;
                           }
                           else
                           {
                               uncompressedSizeCounts[uncompressedSize] = 1;
                           }

                           if (compressionRatioCounts.ContainsKey(compressionRatio))
                           {
                               compressionRatioCounts[compressionRatio]++;
                           }
                           else
                           {
                               compressionRatioCounts[compressionRatio] = 1;
                           }

                           if (crcCounts.ContainsKey(crc))
                           {
                               crcCounts[crc]++;
                           }
                           else
                           {
                               crcCounts[crc] = 1;
                           }

                           if (IsVerbose)
                           {
                               string eRatio = compressionRatio.ToString("0.00") + "%";
                               Console.WriteLine(entry.FileName + "\t" + entry.Crc.ToString("X8") + "\t" + FormatSizeEx((double)uncompressedSize) + "\t" + eRatio);
                           }

                           foreach (var pair in uncompressedSizeCounts)
                           {
                               if (pair.Value > 1)
                               {
                                   //Console.WriteLine($"Uncompressed size: {pair.Key}, Count: {pair.Value}");
                                   sameSize += pair.Value;
                               }
                           }

                           // return ExitCode.Success;

                           Console.WriteLine("");
                           int DepthLevel = 1;
                           int DepthCount = 0;
                           long PayloadSize = 0;
                           foreach (ArchiveFileInfo fEntry in szip.ArchiveFileData)
                           {
                               if (fEntry.IsDirectory)
                                   continue;

                               if (fEntry.FileName.EndsWith(".zip"))
                               {

                                   if (IsVerbose)
                                   {
                                       Console.WriteLine("");
                                       Console.WriteLine("Let's check how deep is this archive...");
                                       Console.WriteLine("---------------------------------------");
                                   }
                                   if (fEntry.Encrypted)
                                   {
                                       Console.Write("This archive needs a password (or CTRL-C to quit): ");
                                       string pswd = ReadPassword();
                                       //szip.Password = pswd;
                                   }
                                   if (IsVerbose)
                                   {
                                       Console.WriteLine(Path.GetFileName(args[1]) + " - Archive level");
                                   }
                                   //ScanNestedZip(fEntry, DepthLevel, out DepthCount, out PayloadSize);
                                   break; // we just need the first zip to start scanning its nested zips
                               }

                           }

                           if (PayloadSize == -1)
                           {
                               Console.WriteLine("This archive cannot be scanned.");
                               Console.WriteLine("Please, obtain the correct password and retry.");
                               return ExitCode.ExtractionError;
                           }

                           if (PayloadSize == -2)
                           {
                               Console.WriteLine("Depth analysis stopped at level " + DepthCount + " for safety reasons.");
                               Console.WriteLine("This archive looks like a 'quine' and it is a zipbomb for sure.");
                               Console.WriteLine("Score: 99%");
                               return ExitCode.Success;
                           }
                           IsNested = (DepthCount >= 2);
                           int score = 0;
                           if (IsNested)
                           {
                               if (nestedZip >= 4 && nestedZip < 16)
                                   score += 5;
                               else if (nestedZip >= 16)
                                   score += 11;
                               if (nestedZip == sameCrc)
                                   score += 11;
                               if (nestedZip == sameSize)
                                   score += 11;
                               //Console.WriteLine("Curr score: "+score);    
                               if (DepthCount >= 2 && DepthCount < 6)
                                   score += 15;
                               else if (DepthCount >= 6)
                               {
                                   score += 33;
                                   //    Console.WriteLine("Curr score: "+score);
                               }
                               if (PayloadSize >= 1048576 && PayloadSize < 1073741824)
                                   score += 15;
                               else if (PayloadSize >= 1073741824)
                               {
                                   score += 33;
                                   //    Console.WriteLine("Curr score: "+score);
                               }
                           }
                           else
                           {
                               if (sameCrc >= 4 && sameCrc < 8)
                                   score += 5;
                               else if (sameCrc >= 8)
                                   score += 10;
                               if (sameSize >= 4 && sameSize < 8)
                                   score += 5;
                               else if (sameSize >= 8)
                                   score += 10;
                               if (sameRatio >= 4 && sameRatio < 8)
                                   score += 5;
                               else if (sameRatio >= 8)
                                   score += 10;
                               if (sameCrc == sameRatio && sameRatio == sameSize && sameCrc == sameSize)
                                   score += 15;
                               if (GlobalUnpackedSize >= 4294967296 && GlobalUnpackedSize < 10737418240)
                                   score += 15;
                               else if (GlobalUnpackedSize >= 10737418240 && GlobalUnpackedSize < 34359738368)
                                   score += 35;
                               else if (GlobalUnpackedSize >= 34359738368)
                                   score += 55;
                           }
                           if (IsVerbose)
                           {
                               Console.WriteLine("");
                               Console.WriteLine("Summary");
                               Console.WriteLine("---------------------------------------\n");
                               if (nestedZip > 0)
                                   Console.WriteLine("Nested Zips at top level: " + nestedZip);
                               Console.WriteLine("Files with same CRC: " + sameCrc);
                               Console.WriteLine("Files with same uncompressed file size: " + sameSize);
                               Console.WriteLine("Files with same compression ratio: " + sameRatio);
                               if (IsNested)
                               {
                                   Console.WriteLine("This archive looks like it has multiple levels.");
                                   Console.WriteLine("Depth Levels: " + DepthCount);
                                   Console.WriteLine("Payload size: " + PayloadSize + " bytes (" + FormatSizeEx((double)PayloadSize) + ")");
                                   double GlobalPayload = Math.Pow(nestedZip, DepthCount) * PayloadSize;
                                   Console.WriteLine("Global attack size: " + GlobalPayload + " bytes (" + FormatSizeEx(GlobalPayload) + ")");
                                   Console.WriteLine("or...");
                                   Console.WriteLine("(" + nestedZip + "^" + DepthCount + ")*" + PayloadSize + " = " + FormatSizeEx(GlobalPayload));
                               }
                               else
                               {
                                   Console.WriteLine("This archive has just files at the top level.");
                                   Console.WriteLine("When fully decompressed, the archive will occupy " + FormatSizeEx((double)GlobalUnpackedSize) + " of storage.");
                               }



                               Console.WriteLine("");
                           }
                           Console.WriteLine("Score: " + score + "%");
                           if (score < 40)
                               Console.WriteLine("The file " + args[1] + " does not look like a zipbomb.");
                           else if (score >= 40 && score < 60)
                               Console.WriteLine("The file " + args[1] + " is highly suspected to be a zipbomb.");
                           else if (score >= 60)
                               Console.WriteLine("The file " + args[1] + " looks like a zipbomb.");



                       }
                   }
               }

               return ExitCode.Success;
           }
       }

       private static void DoCheck2(string[] args)
       {
           string arcFile = args[1];
           using (FileStream fs = new FileStream(arcFile, FileMode.Open, FileAccess.Read))
           {
               System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(fs, ZipArchiveMode.Read);
               foreach (ZipArchiveEntry entry in zip.Entries)
               {
                   Console.WriteLine("# " + entry.Name + " | " + entry.CompressedLength.ToString() + "/" + entry.Length.ToString() + " | ");
                   if (entry != null)
                   {

                       using (Stream zipStream = entry.Open())
                       {
                           using (MemoryStream ms = new MemoryStream())
                           {
                               try
                               {
                                   zipStream.CopyTo(ms);
                                   Console.WriteLine(">> " + entry.Name + " | " + ms.Length.ToString());
                                   if (Path.GetExtension(entry.Name).ToLower() == ".zip")
                                       TraverseZip(ms);
                               } catch (System.IO.InvalidDataException ex){
                               if (ex.Message.ToLower().Contains("lzma"))
                               {
                                   using(Stream zs = StreamFromLZMA(ms)){
                                       TraverseZip(zs);
                                   };
                                   // Console.WriteLine("ZS size: "+zs.Length.ToString());
                                   // System.IO.Compression.ZipArchive zip2 = new System.IO.Compression.ZipArchive(zs,ZipArchiveMode.Read);
                                   // Console.WriteLine("##### zs Count: "+zip2.Entries.Count.ToString());
                               }
                           }
                       }
                   }

               }
           }
       }

   }

   private static Stream StreamFromLZMA(MemoryStream ms)
   {
       string LibPath = string.Empty;
       if (IsLinux())
       {
           Console.WriteLine("We are in linux");
           LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "linux", "7z.so");
       }
       else
       {
           Console.WriteLine("This is WIndows");
           if (Environment.Is64BitProcess)
               LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "64bit", "7z.dll");
           else
               LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "32bit", "7z.dll");
       }
       SevenZip.SevenZipBase.SetLibraryPath(LibPath);
       using (SevenZipExtractor szip = new SevenZipExtractor(ms))
       {
           MemoryStream cs = new MemoryStream();
           foreach (ArchiveFileInfo entry in szip.ArchiveFileData)
           {
               szip.ExtractFile(entry.Index, cs);
               Console.WriteLine(entry.FileName + ": " + ms.Length.ToString());
               // if (Path.GetExtension(entry.FileName).ToLower() == ".zip")
               // {
               //     TraverseZip(ms);
               //     break;
               // }
           }
           return cs;
       }
   }
   */
        private static void TraverseZip(Stream ms)
        {
            Console.WriteLine("ms: " + ms.Length.ToString());
            ms.Position = 0;

            ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                Console.WriteLine("#### " + entry.Name + " | " + entry.CompressedLength + " / " + entry.Length);
                if (entry != null)
                {
                    using (Stream zipStream = entry.Open())
                    {
                        using (MemoryStream ms2 = new MemoryStream())
                        {
                            try
                            {
                                zipStream.CopyTo(ms2);
                                Console.WriteLine(">> " + entry.Name + " | " + ms2.Length.ToString());
                                if (Path.GetExtension(entry.Name).ToLower() == ".zip")
                                    TraverseZip(ms2);
                            }
                            catch (System.IO.InvalidDataException ex)
                            {
                                //  if (ex.Message.ToLower().Contains("lzma"))
                                //     {
                                //         using(Stream zs = StreamFromLZMA(ms2)){
                                //             TraverseZip(zs);
                                //         };
                                //     }
                            }
                        }
                    }
                }
            }


            // string LibPath = string.Empty;
            // if (IsLinux())
            // {
            //     Console.WriteLine("We are in linux");
            //     LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "linux", "7z.so");
            // }
            // else
            // {
            //     Console.WriteLine("This is WIndows");
            //     if (Environment.Is64BitProcess)
            //         LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "64bit", "7z.dll");
            //     else
            //         LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "32bit", "7z.dll");
            // }
            // SevenZip.SevenZipBase.SetLibraryPath(LibPath);
            // using (SevenZipExtractor szip = new SevenZipExtractor(ms))
            // {
            //     Console.WriteLine("## " + szip.ArchiveFileData.Count.ToString());
            //     foreach (ArchiveFileInfo entry in szip.ArchiveFileData)
            //     {
            //         szip.ExtractFile(entry.Index, ms);
            //         Console.WriteLine(entry.FileName + ": " + ms.Length.ToString());
            //         if (Path.GetExtension(entry.FileName).ToLower() == ".zip")
            //         {
            //             TraverseZip(ms);
            //             break;
            //         }
            //     }
            // }
            // Ionic.Zip.ZipFile zip = ZipFile.Read(ms);
            // foreach (ZipEntry entry in zip.Entries)
            // {
            //     if (entry.IsDirectory)
            //         continue;
            //     using (MemoryStream ms2 = new MemoryStream())
            //     {
            //         entry.Extract(ms2);
            //         Console.WriteLine(entry.FileName + ": " + ms2.Length.ToString());
            //     }
            // }
        }

        private static void testRun(ZipEntry entry)
        {
            if (Path.GetExtension(entry.FileName) == ".zip")
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    entry.Extract(ms);
                    ms.Position = 0;
                    ZipFile zip2 = ZipFile.Read(ms);
                    ZipEntry zEntry = zip2.Entries.First();
                    Console.WriteLine(zEntry.FileName);
                };
            }

        }

        private static ExitCode ProcessDelete(string[] args)
        {
            if (args.Length < 3)
            {
                WriteLocalizedError("DeleteMissingArguments", "Syntax: zg -delete archive +entry [+[entry] ...]");
                return ExitCode.CompressionError;
            }

            string arcFile = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1].Trim('"')));
            if (!File.Exists(arcFile))
            {
                WriteLocalizedError("DeleteArchiveNotFound", "Archive not found: {0}", arcFile);
                return ExitCode.FileDoesNotExists;
            }

            string extension = Path.GetExtension(arcFile);
            try
            {
                if (IsZipFile(extension))
                    return DeleteFileFromZip(arcFile, args);
                if (TarArchiveEditor.IsTarArchivePath(arcFile))
                    return DeleteFileFromTar(arcFile, args);
                if (extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                    return DeleteFileFromSevenZip(arcFile, args);

                WriteLocalizedError("DeleteUnsupportedFormat", "Deleting entries is not supported for this archive format: {0}", extension);
                return ExitCode.CompressionError;
            }
            catch (IOException ex)
            {
                WriteLocalizedError("DeleteFailed", "Could not delete entries from {0}: {1}", arcFile, ex.Message);
                return ExitCode.IOError;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLocalizedError("DeleteFailed", "Could not delete entries from {0}: {1}", arcFile, ex.Message);
                return ExitCode.IOError;
            }
            catch (Exception ex)
            {
                WriteLocalizedError("DeleteFailed", "Could not delete entries from {0}: {1}", arcFile, ex.Message);
                return ExitCode.UnknownError;
            }
        }

        private static ExitCode DeleteFileFromZip(string arcFile, string[] args)
        {
            using ZipFile zip = ZipFile.Read(arcFile);
            zip.SaveProgress += Zip_SaveProgress;
            bool updateHash = zip.ContainsEntry("zg-hash.txt");
            List<ZipEntry> deletedEntries = zip.Entries.Where(entry => IsSelectedForDeletion(entry.FileName, args)).ToList();
            if (deletedEntries.Count == 0)
                return ReportNoDeleteMatches(arcFile);

            List<string> deletedFiles = deletedEntries.Select(entry => entry.FileName).ToList();
            zip.RemoveEntries(deletedEntries);
            IsDeleting = true;
            if (DoShowProgress)
                pBar = new ProgressBar(deletedEntries.Count, "Deleting files...", ConsoleColor.Cyan);
            zip.Save();
            if (updateHash)
                DeleteFromHashFile(arcFile, deletedFiles.ToArray());
            if (!IsSilent)
                Console.WriteLine(GetLocalizedString("DeleteCompleted", "Deleted {0} entries from {1}", deletedEntries.Count, arcFile));
            return ExitCode.Success;
        }

        private static ExitCode DeleteFileFromTar(string arcFile, string[] args)
        {
            string[] selectedEntries;
            using (IArchive archive = ArchiveFactory.Open(arcFile))
            {
                selectedEntries = archive.Entries.Select(entry => entry.Key ?? string.Empty)
                    .Where(entry => IsSelectedForDeletion(entry, args)).ToArray();
            }
            if (selectedEntries.Length == 0)
                return ReportNoDeleteMatches(arcFile);

            TarArchiveEditor.DeleteEntries(arcFile, selectedEntries);
            if (!IsSilent)
                Console.WriteLine(GetLocalizedString("DeleteCompleted", "Deleted {0} entries from {1}", selectedEntries.Length, arcFile));
            return ExitCode.Success;
        }

        private static ExitCode DeleteFileFromSevenZip(string arcFile, string[] args)
        {
            SevenZip.SevenZipBase.SetLibraryPath(FileUtils.GetSevenZipLibs());
            Dictionary<int, string> entriesToDelete;
            using (var extractor = new SevenZip.SevenZipExtractor(arcFile))
            {
                entriesToDelete = extractor.ArchiveFileNames
                    .Select((entryName, index) => new { entryName = entryName ?? string.Empty, index })
                    .Where(entry => IsSelectedForDeletion(entry.entryName, args))
                    .ToDictionary(entry => entry.index, _ => (string)null!);
            }
            if (entriesToDelete.Count == 0)
                return ReportNoDeleteMatches(arcFile);

            var compressor = new SevenZip.SevenZipCompressor();
            compressor.ModifyArchive(arcFile, entriesToDelete, null);
            if (!IsSilent)
                Console.WriteLine(GetLocalizedString("DeleteCompleted", "Deleted {0} entries from {1}", entriesToDelete.Count, arcFile));
            return ExitCode.Success;
        }

        private static ExitCode ReportNoDeleteMatches(string archivePath)
        {
            WriteLocalizedError("DeleteNoMatches", "No archive entries matched the specified deletion patterns: {0}", archivePath);
            return ExitCode.ZeroItemsArchive;
        }

        private static bool IsSelectedForDeletion(string entryName, string[] args)
        {
            string normalizedEntryName = NormalizeArchiveEntryPath(entryName);
            string[] inclusions = args.Skip(2).Where(argument => argument.StartsWith("+", StringComparison.Ordinal))
                .Select(argument => NormalizeArchiveEntryPath(argument[1..])).Where(argument => !string.IsNullOrWhiteSpace(argument)).ToArray();
            string[] exclusions = args.Skip(2).Where(argument => argument.StartsWith("-", StringComparison.Ordinal) && !argument.StartsWith("--", StringComparison.Ordinal))
                .Select(argument => NormalizeArchiveEntryPath(argument[1..])).Where(argument => !string.IsNullOrWhiteSpace(argument)).ToArray();

            return inclusions.Any(pattern => ArchivePathMatches(normalizedEntryName, pattern)) &&
                   !exclusions.Any(pattern => ArchivePathMatches(normalizedEntryName, pattern));
        }

        private static string NormalizeArchiveEntryPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static bool ArchivePathMatches(string entryName, string pattern)
        {
            string normalizedPattern = NormalizeArchiveEntryPath(pattern);
            if (string.IsNullOrWhiteSpace(normalizedPattern))
                return false;
            string expression = "^" + Regex.Escape(normalizedPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(entryName, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static void DoSuccessSound()
        {
            Console.Beep(600, 100);
            Console.Beep(600, 100);
            Console.Beep(900, 100);
            Console.Beep(1200, 600);
        }

        private static void DoErrorSound()
        {
            Console.Beep(600, 100);
            Console.Beep(400, 100);
            Console.Beep(200, 100);
        }

        private static void ProcessAddCab(string[] args)
        {
            if (IsVerbose)
            {
                Console.WriteLine("I'm going to create a .CAB archive with \"makecab.exe\".");
            }
            PathStoreMode pMode = PathStoreMode.NoPath;
            SearchOption sMode = SearchOption.TopDirectoryOnly;
            long zVolSize = 0;
            string arcFile = Environment.ExpandEnvironmentVariables(args[1]);
            string DestPath = Path.GetDirectoryName(arcFile);
            string DestFile = Path.GetFileName(arcFile);
            if (IsVerbose)
            {
                Console.WriteLine("The archive file will be " + DestFile + ".");
            }
            fInclusion = new List<string>();
            filesDictionary = new Dictionary<string, string>();
            StringBuilder sbParams = new StringBuilder();
            sbParams.AppendLine(".OPTION EXPLICIT");
            for (int i = 2; i < args.Length; i++)
            {
                string cPar = args[i];
                if (cPar.StartsWith("C"))
                {
                    switch (cPar)
                    {
                        case "C0":
                            {
                                sbParams.AppendLine(".Set Compress = off");
                                break;
                            }
                        case "C1":
                            {
                                sbParams.AppendLine(".Set Compress = on");
                                sbParams.AppendLine(".Set CompressionType=MSZIP");
                                break;
                            }
                        case "C5":
                            {
                                sbParams.AppendLine(".Set Compress = on");
                                sbParams.AppendLine(".Set CompressionType=LZX");
                                sbParams.AppendLine(".Set CompressionMemory=17");
                                break;
                            }
                        case "C9":
                            {
                                sbParams.AppendLine(".Set Compress = on");
                                sbParams.AppendLine(".Set CompressionType=LZX");
                                sbParams.AppendLine(".Set CompressionMemory=19");
                                break;
                            }
                        case "CB":
                            {
                                sbParams.AppendLine(".Set Compress = on");
                                sbParams.AppendLine(".Set CompressionType=LZX");
                                sbParams.AppendLine(".Set CompressionMemory=21");
                                break;
                            }
                        default:
                            {
                                sbParams.AppendLine(".Set Compress = off");
                                break;
                            }
                    }
                }
                if (cPar.StartsWith("+"))
                {
                    string fPar = cPar.Remove(0, 1);
                    fInclusion.Add(fPar);
                }
                if (cPar.StartsWith("F"))
                {
                    switch (cPar)
                    {
                        case "F0":
                            {
                                pMode = PathStoreMode.NoPath;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("Files path will not be stored in the archive.");
                                }
                                break;
                            }
                        case "F1":
                            {
                                pMode = PathStoreMode.RelativeFolder;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive will store the relative folder path of every file.");
                                }
                                break;
                            }
                        case "F2":
                        case "F3":
                            {
                                pMode = PathStoreMode.FullPathNoDrive;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive will store the full path of every file without the drive letter.");
                                }
                                break;
                            }
                        default:
                            {
                                pMode = PathStoreMode.NoPath;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("Files path will not be stored in the archive. Unrecognized option for 'F'.");
                                }
                                break;
                            }
                    }
                }
                if (cPar.StartsWith("R"))
                {
                    switch (cPar)
                    {
                        case "R0":
                            {
                                sMode = SearchOption.TopDirectoryOnly;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("Subfolders will NOT be scanned for files to store in the archive.");
                                }
                                break;
                            }
                        case "R1":
                            {
                                sMode = SearchOption.AllDirectories;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("Subfolders will be scanned for files to store in the archive.");
                                }
                                break;
                            }
                        default:
                            {
                                sMode = SearchOption.TopDirectoryOnly;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("Subfolders will not be scanned for files to store in the archive. Unrecognized option for 'R'.");
                                }
                                break;
                            }
                    }
                }
                if (cPar.StartsWith("V") || cPar.StartsWith("S"))
                {
                    string vPar = cPar.Remove(0, 1);
                    switch (vPar)
                    {
                        case "vm720":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 719872;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vm880":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 875520;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vm1440":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 1433600;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vm2880":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 2867200;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vmEmail":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 249856;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vmWeb":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 749568;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vmCDR650":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 649999360;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vmCDR700":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 696320000;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        case "vmCDR800":
                            {
                                DestFile = Path.GetFileNameWithoutExtension(args[1]) + "_*" + Path.GetExtension(args[1]);
                                zVolSize = 793600000;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will be renamed as " + DestFile + " because it will have multiple volumes.");
                                    Console.WriteLine("Each volume will be sized up to " + FormatSize(zVolSize) + ".");
                                }
                                break;
                            }
                        default:
                            {
                                DestFile = Path.GetFileName(args[1]);
                                zVolSize = 0;
                                if (IsVerbose)
                                {
                                    Console.WriteLine("The archive file will not be renamed because no volumes will be created due to an unrecognized option for 'V'.");
                                }
                                break;
                            }
                    }
                }
                if (cPar.StartsWith("@"))
                {
                    string fFile = cPar.Remove(0, 1);
                    Console.WriteLine("The archive will get the files list from the file: \"" + fFile + "\".");
                    var lines = File.ReadLines(fFile);
                    foreach (var line in lines)
                    {
                        if (line[0].ToString() == "+")
                        {
                            string nFile = line.Remove(0, 1);
                            fInclusion.Add(nFile);
                        }
                        if (line[0].ToString() == "-" && line[1].ToString() != "-")
                        {
                            string nFile = line.Remove(0, 1);
                            //Console.WriteLine("## nFile - " + nFile);
                            if (nFile.StartsWith("*"))
                            {
                                string ext = Path.GetExtension(nFile);
                                //Console.WriteLine("##> " + ext);
                                fExclusionExt.Add(ext);
                            }
                            else
                                fExclusion.Add(nFile);
                        }

                    }
                }
            }
            sbParams.AppendLine(".Set CabinetNameTemplate=\"" + DestFile + "\"");
            sbParams.AppendLine(".Set Cabinet=on");
            sbParams.AppendLine(".Set MaxDiskSize = " + zVolSize.ToString());
            // Fill dictionary here
            foreach (string fPar in fInclusion)
            {
                if (Path.GetFileName(fPar) == "*.*" ||
                    Path.GetFileName(fPar) == "*" ||
                    Path.GetFileName(fPar).Contains("*") ||
                    Path.GetFileName(fPar).Contains("?"))
                {
                    AddDirsToDictionary(filesDictionary, Path.GetDirectoryName(fPar), Path.GetFileName(fPar), "", sMode, pMode);
                }
                else
                    AddFileToDictionary(filesDictionary, fPar);
            }
            sbParams.AppendLine(".Set DiskDirectoryTemplate=\"" + DestPath + "\"");


            Thread.Sleep(200);
            string ddf = Path.Combine(Path.GetTempPath(), "zgx.ddf");
            Console.WriteLine("Temp: " + ddf);
            foreach (var entry in filesDictionary)
            {
                sbParams.AppendLine("\"" + entry.Value + "\" \"" + entry.Key + "\"");
            }
            string output = "";
            if (IsSilent)
                output = " > stdout";
            File.WriteAllText(ddf, sbParams.ToString());
            Process.Start("makecab.exe", "/F \"" + ddf + "\"" + output).WaitForExit();
            Console.WriteLine(GetLocalizedString("Done", "Done"));
            //File.Delete(ddf);


            //  .OPTION EXPLICIT; Generate errors on variable typos
            //  ;
            //  .Set CabinetNameTemplate = test.wsp; The name of the WSP file
            //  .set DiskDirectoryTemplate = CDROM; All cabinets go in a single directory
            //  .Set CompressionType = MSZIP; 
            //  .Set Cabinet = on; 
            //  .Set Compress = on;
            //  .Set DiskDirectory1 =.               ; Use the specified directory for the output CAB file
            //  ;
            //  ; ***Disable size limits for wsp(cab) files;
            //  ;
            //  .Set CabinetFileCountThreshold = 0
            //  .Set FolderFileCountThreshold = 0
            //  .Set FolderSizeThreshold = 0
            //  .Set MaxCabinetSize = 0
            //  .Set MaxDiskFileCount = 0
            //  .Set MaxDiskSize = 0
            //  ;
            //  ; ***Files to zip;
            //  ;
            //  "C:\test\12\template\features\MyFeature\Elements.xml" "MyFeature\Elements.xml"
            //  "C:\test\12\template\features\MyFeature\Feature.xml" "MyFeature\Feature.xml"
            //  "C:\test\12\template\features\MyFeature\MyList\Schema.xml" "MyFeature\MyList\Schema.xml"
            //  "C:\test\manifest.xml" "manifest.xml"



            //writer.WriteLine(".Set Compress=on");
            //writer.WriteLine(".Set CabinetFileCountThreshold = 0");
            //writer.WriteLine(".Set FolderFileCountThreshold = 0");
            //writer.WriteLine(".Set FolderSizeThreshold = 7200");
            //writer.WriteLine(".Set MaxCabinetSize = 0");
            //writer.WriteLine(".Set MaxDiskFileCount = 0");
            //writer.WriteLine(".Set MaxDiskSize = 2816000");
            //writer.WriteLine(".Set DiskDirectory1=\"" + DestPath + "\"");


        }

        #region -hexview related code
        private static ExitCode HexDump3(string[] args)
        {
            if (args.Length < 2)
            {
                ShowHelp("-hexview");
                return ExitCode.UnknownError;
            }
            Console.ResetColor();
            Int64 StartOff = 0;
            string arcFile = Environment.ExpandEnvironmentVariables(args[1]);
            if (!File.Exists(arcFile))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(GetLocalizedString("HexMissingFile", "ERROR > File does not exists, quitting."));
                Console.ResetColor();
                return ExitCode.FileDoesNotExists;
            }

            bool DoPause = false;
            bool DoMonoChrome = false;
            bool DoDecimal = false;

            int MaxLines = 20;
            if (args.Length > 2)
            {
                for (int i = 2; i < args.Length; i++)
                {
                    string cPar = args[i];
                    if (cPar == "P")
                        DoPause = true;
                    else if (cPar == "M")
                        DoMonoChrome = true;
                    else if (cPar == "D")
                        DoDecimal = true;
                    else if (cPar.StartsWith("S="))
                    {
                        string[] sOffParts = cPar.Split("=");
                        if (!Int64.TryParse(sOffParts[1], out StartOff))
                            StartOff = 0;
                        //Console.WriteLine("S=" + StartOff);
                    }
                    else if (cPar.StartsWith("L="))
                    {
                        DoPause = true;
                        string[] sLineParts = cPar.Split("=");
                        if (!int.TryParse(sLineParts[1], out MaxLines))
                            MaxLines = 20;
                        if (MaxLines > 500)
                        {
                            if (!IsSilent)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine(GetLocalizedString("HexMaxLines", "Max 500 lines allowed. Lines limit set to default value"));
                                Console.ResetColor();
                            }
                            MaxLines = 20;
                        }
                        if (MaxLines < 1)
                        {
                            if (!IsSilent)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine(GetLocalizedString("HexMinLines", "Min 1 line allowed. Lines limit set to default value"));
                                Console.ResetColor();
                            }
                            MaxLines = 20;
                        }
                    }
                    else if (cPar.StartsWith("FB="))
                    {

                        for (int x = 2; x < args.Length; x++)
                        {
                            string xPar = args[x].ToUpper();
                            if (xPar.StartsWith("BL="))
                            {
                                string[] blParts = xPar.Split("=");
                                if (!int.TryParse(blParts[1], out BlockLines))
                                {
                                    BlockLines = 1;
                                }
                                else
                                {
                                    if (BlockLines > 10 || BlockLines < 1)
                                    {
                                        BlockLines = 1;
                                    }
                                }
                                break;
                            }
                        }
                        string[] sLineParts = cPar.Split("=");
                        string bytePart = sLineParts[1].Replace("-", " ");

                        Console.WriteLine(GetLocalizedString("HexSearchBytes", "Searching for bytes ") + bytePart + "..." + Environment.NewLine);
                        byte[] bArray = StringToByteArrayFastest(bytePart.Replace(" ", ""));
                        var fStream2 = File.Open(arcFile, FileMode.Open, FileAccess.Read);
                        List<long> bPos = new List<long>();
                        bPos = FindPosition(fStream2, bArray);
                        if (bPos.Count == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine(GetLocalizedString("HexNoBytes", "Bytes sequence not found. Quitting."));
                            Console.ResetColor();
                            return ExitCode.BytesPatternNotFound;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(bPos.Count.ToString() + GetLocalizedString("HexMatches", " occurrence(s) found!") + Environment.NewLine);
                            Console.ResetColor();
                        }
                        //Console.WriteLine("Pos: " + bPos.ToString("D8") + " (" + bPos.ToString("X8") + ")");
                        int nLines = 1;
                        int cLines = 1;
                        foreach (long entry in bPos)
                        {
                            Console.WriteLine(GetLocalizedString("HexResult", "Result #") + cLines.ToString());
                            DumpAtOffSet(entry, arcFile);
                            if (bPos.Count > 5)
                            {
                                if (nLines == 5)
                                {
                                    WaitInput(GetLocalizedString("PauseContinue", "Press any key to continue (or CTRL+C to quit)..."));
                                    nLines = 1;
                                }
                                else nLines++;
                            }
                            cLines++;
                        }
                        fStream2.Dispose();
                        return ExitCode.Success;

                    }
                    else if (cPar.StartsWith("FT="))
                    {
                        string[] sLineParts = cPar.Split("=");
                        string bytePart = sLineParts[1].Replace("\"", "");
                        string bHexPart = Convert.ToHexString(Encoding.UTF8.GetBytes(bytePart));
                        string cHexPart = string.Join(' ', bHexPart.Chunk(size: 2).Select(x => new string(x)));
                        Console.WriteLine(GetLocalizedString("HexSearchText", "Searching for text \"") + bytePart + GetLocalizedString("HexByteLabel", "\" (Bytes: ") + cHexPart + ")..." + Environment.NewLine);
                        for (int x = 2; x < args.Length; x++)
                        {
                            string xPar = args[x].ToUpper();
                            if (xPar.StartsWith("BL="))
                            {
                                string[] blParts = xPar.Split("=");
                                if (!int.TryParse(blParts[1], out BlockLines))
                                {
                                    BlockLines = 1;
                                }
                                else
                                {
                                    if (BlockLines > 10 || BlockLines < 1)
                                    {
                                        BlockLines = 1;
                                    }
                                }
                                break;
                            }
                        }

                        byte[] bArray = StringToByteArrayFastest(bHexPart);
                        var fStream2 = File.Open(arcFile, FileMode.Open, FileAccess.Read);
                        List<long> bPos = new List<long>();
                        bPos = FindPosition(fStream2, bArray);
                        if (bPos.Count == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine(GetLocalizedString("HexNoText", "Specified text not found (REMEMBER: text search is Case sensistive!) Quitting."));
                            Console.ResetColor();
                            return ExitCode.TextPatternNotFound;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(bPos.Count.ToString() + GetLocalizedString("HexMatches", " occurrence(s) found!") + Environment.NewLine);
                            Console.ResetColor();
                        }
                        //Console.WriteLine("Pos: " + bPos.ToString("D8") + " (" + bPos.ToString("X8") + ")");
                        int nLines = 1;
                        int cLines = 1;
                        foreach (long entry in bPos)
                        {
                            Console.WriteLine(GetLocalizedString("HexResult", "Result #") + cLines.ToString());
                            DumpAtOffSet(entry, arcFile);
                            if (bPos.Count > 5)
                            {
                                if (nLines == 5)
                                {
                                    WaitInput(GetLocalizedString("PauseContinue", "Press any key to continue (or CTRL+C to quit)..."));
                                    nLines = 1;
                                }
                                else nLines++;
                            }
                            cLines++;
                        }
                        fStream2.Dispose();
                        return ExitCode.Success;

                    }

                }
            }
            var fStream = File.Open(arcFile, FileMode.Open, FileAccess.Read);
            long xMaxBytes = fStream.Length;
            int lCount = 1;
            int bRead = 0;
            int bytesPerLine = 16;
            byte[] bytes = new byte[xMaxBytes];

            fStream.Seek(StartOff, 0);
            string DumpHead = "ADDRESS    0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F    ASCII OUTPUT    " + Environment.NewLine;
            DumpHead += "------------------------------------------------------------------------------";
            Console.WriteLine(GetLocalizedString("HexDumping", "Dumping file: ") + arcFile + " (" + FormatSize(xMaxBytes) + ")" + Environment.NewLine);

            Console.WriteLine(DumpHead);
            while ((bRead = fStream.Read(bytes, 0, bytes.Length)) > 0)
            {
                //if (bytes == null) return "<null>";
                int bytesLength = bytes.Length;
                char[] HexChars = "0123456789ABCDEF".ToCharArray();
                int firstHexColumn =
                  8                   // 8 characters for the address
                + 3;                  // 3 spaces

                int firstCharColumn = firstHexColumn
                    + bytesPerLine * 3       // - 2 digit for the hexadecimal value and 1 space
                    + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                    + 2;                  // 2 spaces 

                int lineLength = firstCharColumn
                    + bytesPerLine;           // - characters to show the ascii value
                                              //+ Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)

                char[] line = (new String(' ', lineLength)).ToCharArray();
                int expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;

                for (int i = Convert.ToInt32(StartOff); i < bytesLength; i += bytesPerLine)
                {
                    if (!DoDecimal)
                    {
                        line[0] = HexChars[(i >> 28) & 0xF];
                        line[1] = HexChars[(i >> 24) & 0xF];
                        line[2] = HexChars[(i >> 20) & 0xF];
                        line[3] = HexChars[(i >> 16) & 0xF];
                        line[4] = HexChars[(i >> 12) & 0xF];
                        line[5] = HexChars[(i >> 8) & 0xF];
                        line[6] = HexChars[(i >> 4) & 0xF];
                        line[7] = HexChars[(i >> 0) & 0xF];
                    }
                    else
                    {
                        string offset = i.ToString("D8");
                        offset.CopyTo(0, line, 0, offset.Length);
                    }
                    int hexColumn = firstHexColumn;
                    int charColumn = firstCharColumn;

                    for (int j = 0; j < bytesPerLine; j++)
                    {
                        if (j > 0 && (j & 7) == 0) hexColumn++;
                        if (i + j >= bytesLength)
                        {
                            line[hexColumn] = ' ';
                            line[hexColumn + 1] = ' ';
                            line[charColumn] = ' ';
                        }
                        else
                        {
                            byte b = bytes[i + j];
                            line[hexColumn] = HexChars[(b >> 4) & 0xF];
                            line[hexColumn + 1] = HexChars[b & 0xF];
                            // line[charColumn] = (b < 32 ? '»' : (char)b);


                            char mark = Encoding.ASCII.GetChars(new byte[] { b })[0];
                            if (char.IsControl(mark) || char.IsSurrogate(mark) || mark.Equals('?'))
                                line[charColumn] = '.';
                            else
                                line[charColumn] = mark;


                            // if (b == 133)
                            //     line[charColumn] = ('…');
                            // else if (b >= 128 && b <= 159)
                            // {
                            //     var e = Encoding.GetEncoding(437);
                            //     var s = e.GetString(new byte[] { b }).ToCharArray();
                            //     line[charColumn] = s[0];
                            // }  // line[charColumn] = (' ');
                            // else
                            //     line[charColumn] = (b < 32 ? '·' : (char)b);
                        }
                        hexColumn += 3;
                        charColumn++;
                    }

                    if (!DoMonoChrome && (lCount % 2 == 0))
                    {
                        //Console.BackgroundColor = ConsoleColor.DarkGray;
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                    }
                    Console.OutputEncoding = Encoding.Unicode;
                    Console.WriteLine(line);
                    Console.ResetColor();
                    if (DoPause)
                    {
                        if (lCount == MaxLines)
                        {
                            WaitInput(GetLocalizedString("PauseNextPage", "Press a key to continue (or CTRL+C to quit)..."));
                            lCount = 1;
                        }
                        else
                            lCount++;
                    }
                    else lCount++;
                }
            }
            fStream.Dispose();

            return ExitCode.Success;
        }

        private static string DecodeLine(byte[] inBytes, long StartOff, bool DoDecimal = true)
        {
            string cLine = BitConverter.ToString(inBytes);
            //Console.WriteLine("# " + cLine);
            int bytesPerLine = 16;
            int bytesLength = inBytes.Length;
            char[] HexChars = "0123456789ABCDEF".ToCharArray();
            int firstHexColumn =
                8                   // 8 characters for the address
                + 3;                // 3 spaces

            int firstCharColumn = firstHexColumn
                + bytesPerLine * 3          // - 2 digit for the hexadecimal value and 1 space
                + (bytesPerLine - 1) / 8    // - 1 extra space every 8 characters from the 9th
                + 2;                        // 2 spaces 

            int lineLength = firstCharColumn
                + bytesPerLine;           // - characters to show the ascii value
                                          //+ Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)
            string xLine = string.Empty;
            char[] line = (new string(' ', lineLength)).ToCharArray();
            int expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;

            for (int i = 0; i < bytesLength; i += bytesPerLine)
            {
                if (!DoDecimal)
                {
                    line[0] = HexChars[(i >> 28) & 0xF];
                    line[1] = HexChars[(i >> 24) & 0xF];
                    line[2] = HexChars[(i >> 20) & 0xF];
                    line[3] = HexChars[(i >> 16) & 0xF];
                    line[4] = HexChars[(i >> 12) & 0xF];
                    line[5] = HexChars[(i >> 8) & 0xF];
                    line[6] = HexChars[(i >> 4) & 0xF];
                    line[7] = HexChars[(i >> 0) & 0xF];
                }
                else
                {
                    string offset = (i + StartOff).ToString("D8");
                    offset.CopyTo(0, line, 0, offset.Length);
                }
                int hexColumn = firstHexColumn;
                int charColumn = firstCharColumn;

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        byte b = inBytes[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        if (b == 133)
                            line[charColumn] = '…';
                        else if (b >= 128 && b <= 159)
                        {
                            var e = Encoding.GetEncoding(437);
                            var s = e.GetString(new byte[] { b }).ToCharArray();
                            line[charColumn] = s[0];
                        }
                        else
                            line[charColumn] = (b < 32 ? '·' : (char)b);
                    }
                    hexColumn += 3;
                    charColumn++;
                }
                string chars = BytesToChars(inBytes);
                //chars.CopyTo(0, line, firstCharColumn, chars.Length);
                //Console.OutputEncoding = Encoding.UTF8;
                //Console.WriteLine(">> "+line);
                xLine += new string(line) + Environment.NewLine;
            }
            return xLine;
        }

        private static ExitCode DumpAtOffSet(long StartOff, string arcFile, bool DoDecimal = true)
        {
            Console.ResetColor();
            Console.WriteLine(GetLocalizedString("HexPosition", "Dumping at POS: ") + StartOff.ToString("D8") + " (HEX: " + StartOff.ToString("X8") + ")");
            using (FileStream fStream = new FileStream(arcFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int bRead = 0;
                byte[] bytes = new byte[16 * BlockLines];
                string DumpHead = "ADDRESS    0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F    ASCII OUTPUT    " + Environment.NewLine;
                DumpHead += "------------------------------------------------------------------------------";
                Console.WriteLine(DumpHead);
                fStream.Seek(StartOff, 0);
                bRead = fStream.Read(bytes, 0, bytes.Length);
                string cLine = BitConverter.ToString(bytes);
                var xLine = DecodeLine(bytes, StartOff);
                Console.WriteLine(xLine);
            }
            return ExitCode.Success;
        }
        public static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception(GetLocalizedString("HexOddDigits", "The binary key cannot have an odd number of digits"));

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        //public static long FindPosition(Stream stream, byte[] byteSequence)
        public static List<long> FindPosition(Stream stream, byte[] byteSequence, bool OnlyFirst = false)
        {
            if (byteSequence.Length > stream.Length)
                return null; //-1
            List<long> list = new List<long>();

            byte[] buffer = new byte[byteSequence.Length];

            using (BufferedStream bufStream = new BufferedStream(stream, byteSequence.Length))
            {
                int i;
                //bufStream.Seek(30, SeekOrigin.Begin);
                //while ((i = bufStream.Read(buffer, 0, byteSequence.Length)) == byteSequence.Length)
                while ((i = bufStream.Read(buffer, 0, byteSequence.Length)) > 0)
                {
                    if (byteSequence.SequenceEqual(buffer))
                    //return bufStream.Position - byteSequence.Length;
                    {
                        list.Add(bufStream.Position - byteSequence.Length);
                        if (OnlyFirst)
                            break;
                    }
                    else
                        //bufStream.Position += byteSequence.Length + PadLeftSequence(buffer, byteSequence);
                        bufStream.Position -= byteSequence.Length - PadLeftSequence(buffer, byteSequence);
                }
            }

            return list;
        }

        private static int PadLeftSequence(byte[] bytes, byte[] seqBytes)
        {
            int i = 1;
            while (i < bytes.Length)
            {
                int n = bytes.Length - i;
                byte[] aux1 = new byte[n];
                byte[] aux2 = new byte[n];
                Array.Copy(bytes, i, aux1, 0, n);
                Array.Copy(seqBytes, aux2, n);
                if (aux1.SequenceEqual(aux2))
                    return i;
                i++;
            }
            return i;
        }

        private static string BytesToChars(byte[] inBytes)
        {
            string sChar = string.Empty;
            for (int i = 0; i < inBytes.Length; i++)
            {
                byte b = inBytes[i];
                if (b == 133)
                    sChar += ('…');
                else if (b >= 128 && b <= 159)
                {
                    var e = Encoding.GetEncoding(437);
                    var s = e.GetString(new byte[] { b }).ToCharArray();
                    sChar += s[0];
                }  // line[charColumn] = (' ');
                else
                    sChar += (b < 32 ? '·' : (char)b);
            }
            return sChar;
        }
        #endregion

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            //Console.Clear();
            if (pBar != null)
            {
                pBar.Dispose();
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(GetLocalizedString("Cancelled", "CTRL+C pressed. Quitting."));
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Gray;
            string ddf = Path.Combine(Path.GetTempPath(), "zgx.ddf");
            if (File.Exists(ddf))
                File.Delete(ddf);
            Environment.Exit((int)ExitCode.CanceledByUser);
        }

        private static ExitCode DestroyArchive(string[] args)
        {
            ExitCode intExitCode = ExitCode.Success;
            int nPasses = 1;
            string arcFile = Environment.ExpandEnvironmentVariables(args[1]);

            if (!File.Exists(args[1]))
            {
                Console.WriteLine(GetLocalizedString("DestroyMissingPrefix", "ERROR! File ") + args[1] + GetLocalizedString("MissingSuffix", " does not exists."));
                intExitCode = ExitCode.FileDoesNotExists;
                return intExitCode;
            }

            if (args.Length > 2)
            {
                if (!int.TryParse(args[2], out nPasses))
                    nPasses = 1;
                if (nPasses > 10)
                {
                    var origColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    if (!IsSilent)
                        Console.WriteLine(GetLocalizedString("DestroyMaxPasses", "WARNING! Only max 10 passes allowed. Number of passes has been set to 1."));
                    nPasses = 1;
                    Console.WriteLine("");
                    Console.ForegroundColor = origColor;
                }
            }
            if (!IsSilent)
            {
                Console.WriteLine(GetLocalizedString("DestroyIntro", "I am going to destroy the archive ") + arcFile + ".");
                Console.WriteLine(GetLocalizedString("DestroyRandom", "I will fill the file with random bytes for ") + nPasses + GetLocalizedString("DestroyTimes", " time(s)."));
                Console.WriteLine(GetLocalizedString("DestroyIrreversible", "This action will make the archive NOT RECOVERABLE and UNDOABLE!"));
                Console.WriteLine("");

                bool CanGoOn = false;
                if (!DoAlwaysConfirm)
                {
                    Console.WriteLine(GetLocalizedString("DestroyConfirm", "Last chance: do you want to continue? (Y/N): "));
                    ConsoleKeyInfo keyPress = Console.ReadKey(intercept: true);
                    switch (keyPress.Key)
                    {
                        case ConsoleKey.Y:
                            {
                                CanGoOn = true;
                                break;
                            }
                        case ConsoleKey.N:
                            {
                                Console.WriteLine(GetLocalizedString("CannotContinue", "Unable to continue, quitting."));
                                intExitCode = ExitCode.CanceledByUser;
                                CanGoOn = false;
                                break;
                            }
                        default:
                            {
                                Console.WriteLine(GetLocalizedString("CannotContinue", "Unable to continue, quitting."));
                                intExitCode = ExitCode.CanceledByUser;
                                CanGoOn = false;
                                break;
                            }
                    }
                }
                else CanGoOn = true;

                if (!CanGoOn)
                {
                    return intExitCode;
                }
            }
            var optMain = new ProgressBarOptions
            {
                EnableTaskBarProgress = IsWindows(),
                ShowEstimatedDuration = false,
                CollapseWhenFinished = false,
                PercentageFormat = "{0:N0}% ",
                //BackgroundColor = ConsoleColor.White,
                ForegroundColor = ConsoleColor.Cyan,

                ForegroundColorDone = ConsoleColor.Green,
                ProgressBarOnBottom = true
            };
            if (!IsWindows())
                optMain.ProgressCharacter = '-';
            ProgressBar pBar = null;
            if (DoShowProgress)
                pBar = new ProgressBar(nPasses, GetLocalizedString("DestroyProgress", "Destroying archive ") + arcFile, optMain);
            for (int j = 0; j < nPasses; j++)
            {
                long bSize = 1048576;
                using (FileStream fStream = new FileStream(arcFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    Int64 nTicks = fStream.Length / 1048576;
                    if (fStream.Length < 1048576)
                    {
                        byte[] buffer = new byte[fStream.Length];
                        Random rnd = new Random();
                        rnd.NextBytes(buffer);
                        fStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        var optChild = new ProgressBarOptions
                        {
                            EnableTaskBarProgress = IsWindows(),
                            ShowEstimatedDuration = false,
                            CollapseWhenFinished = false,
                            PercentageFormat = "{0:N0}% ",
                            ProgressCharacter = '-',
                            //BackgroundColor = ConsoleColor.White,
                            ForegroundColor = ConsoleColor.Cyan,
                            ForegroundColorDone = ConsoleColor.Green,
                            ProgressBarOnBottom = true
                        };
                        ChildProgressBar cpBar = null;
                        if (DoShowProgress)
                            cpBar = pBar.Spawn(Convert.ToInt32(nTicks), "Pass #" + (j + 1).ToString(), optChild);
                        byte[] buffer = new byte[bSize];
                        long bytes = 0;
                        Random rnd = new Random();
                        rnd.NextBytes(buffer);
                        while (bytes < fStream.Length)
                        {
                            bytes += buffer.Length;
                            if (bytes > fStream.Length)
                            {
                                long nDiff = 1048576 - (bytes - fStream.Length);
                                bSize = nDiff;
                                bytes -= buffer.Length;
                                buffer = new byte[bSize];
                                rnd.NextBytes(buffer);
                                bytes += bSize;
                            }
                            fStream.Write(buffer, 0, buffer.Length);
                            if (DoShowProgress)
                                cpBar.Tick(bytes.ToString() + "/" + fStream.Length.ToString());
                            Thread.Sleep(200);
                        }
                    }
                    if (DoShowProgress)
                        pBar.Tick("Pass #" + (j + 1).ToString() + " completed.");
                }
            }
            if (DoShowProgress)
                pBar.Dispose();
            if (!IsSilent)
                Console.WriteLine(GetLocalizedString("ArchivePrefix", "The archive ") + arcFile + GetLocalizedString("DestroyDone", " has been destroyed."));

            return intExitCode;
        }

        private static bool IsZipFile(string ext)
        {
            return ZipLike.Contains(ext);
        }
        private static void ShowHelp(string cmd)
        {
            /// ^ -> Syntax (Green)            
            /// * -> Example (Yellow)
            /// [ -> Parameters (Dark Yellow)
            /// ] -> Values (Blue)
            /// % -> Highlighted (Black on Yellow)

            switch (cmd)
            {

                case "NOCMD":
                    {
                        ShowManual("startup");
                        break;
                    }
                case "add":
                case "a":
                case "-a":
                case "-add":
                    {
                        ShowManual("add");
                        break;
                    }
                case "extract":
                case "x":
                case "-x":
                case "-extract":
                    {
                        ShowManual("extract");
                        break;
                    }
                case "backup":
                case "b":
                case "-b":
                case "-backup":
                    {
                        ShowManual("backup");
                        break;
                    }
                case "convert":
                case "cvt":
                case "-convert":
                case "-cvt":
                    {
                        ShowManual("convert");
                        break;
                    }
                case "list":
                case "l":
                case "-l":
                case "-list":
                    {
                        ShowManual("list");
                        break;
                    }
                case "export":
                case "-export":
                    {
                        ShowManual("export");
                        break;
                    }
                case "sfx":
                case "-sfx":
                    {
                        ShowManual("sfx");
                        break;
                    }
                case "info":
                case "-info":
                    {
                        ShowManual("info");
                        break;
                    }
                case "test":
                case "t":
                case "-test":
                case "-t":
                    {
                        ShowManual("test");
                        break;
                    }
                case "errorcodes":
                case "exitcodes":
                case "-errorcodes":
                case "-exitcodes":
                    {
                        ShowManual("exitcodes");
                        break;
                    }
                case "-dumpdisc":
                    {
                        ShowManual("dumpdisc");
                        break;
                    }
                case "hexview":
                case "-hexview":
                    {
                        ShowManual("hexview");
                        break;
                    }
                case "hash":
                case "hh":
                case "-hh":
                case "-hash":
                    {
                        ShowManual("hash");
                        break;
                    }
                case "optim":
                case "opt":
                case "-optim":
                case "-opt":
                case "-optimize":
                    {
                        ShowManual("optim");
                        break;
                    }
            }




        }

        public static void ShowManual(string s)
        {
            /* 
             
             ^ -> Syntax (Green)            
             * -> Example (Yellow)
             [ -> Parameters (Dark Yellow)
             ] -> Values (Blue)
             % -> Highlighted (Black on Yellow)
             >> -> Pause for user input

            */


            string manpage = "generichelp.txt";
            switch (s)
            {
                case "add":
                    {
                        manpage = "add.txt";
                        break;
                    }
                case "extract":
                    {
                        manpage = "extract.txt";
                        break;
                    }
                case "backup":
                    {
                        manpage = "backup.txt";
                        break;
                    }
                case "convert":
                    {
                        manpage = "convert.txt";
                        break;
                    }
                case "list":
                case "export":
                case "hexview":
                case "startup":
                    {
                        manpage = s + ".txt";
                        break;
                    }
                case "dumpdisc":
                    {
                        manpage = "dumpdisc.txt";
                        break;
                    }
                case "hash":
                    {
                        manpage = "hash.txt";
                        break;
                    }
                case "optim":
                    {
                        manpage = "optim.txt";
                        break;
                    }
                case "generic":
                    {
                        manpage = "generichelp.txt";
                        break;
                    }
                case "exitcode":
                case "exitcodes":
                    {
                        manpage = "exitcode.txt";
                        break;
                    }
                case "sfx":
                    {
                        manpage = "sfx.txt";
                        break;
                    }
                case "info":
                    {
                        manpage = "info.txt";
                        break;
                    }
                case "test":
                    {
                        manpage = "test.txt";
                        break;
                    }
                default:
                    {
                        manpage = "generichelp.txt";
                        break;
                    }
            }
            
            RenderHelpLines(EmbeddedManuals.ReadLines(manpage, CultureInfo.CurrentUICulture));
        }

        #region Install / Uninstall
        private static void InstallApp(bool systemWide)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string appDirectory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
                string escapedDirectory = ShellQuote(appDirectory);
                string pathLine = "export PATH=\"$PATH\":" + escapedDirectory;
                string targetFile;
                string profileLine;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (!systemWide && string.IsNullOrWhiteSpace(homeDir))
                    {
                        Console.Error.WriteLine(GetLocalizedString("InstallHome", "Unable to determine the current user's home directory."));
                        return;
                    }

                    targetFile = systemWide
                        ? "/etc/paths.d/zipgenius-x"
                        : Path.Combine(homeDir, ".zprofile");
                    profileLine = systemWide ? appDirectory : pathLine;
                }
                else
                {
                    targetFile = systemWide ? "/etc/profile.d/zipgenius-x.sh" : string.Empty;
                    profileLine = systemWide ? pathLine : string.Empty;
                }

                if (!systemWide && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (string.IsNullOrWhiteSpace(homeDir))
                    {
                        Console.Error.WriteLine(GetLocalizedString("InstallHome", "Unable to determine the current user's home directory."));
                        return;
                    }

                    targetFile = Path.Combine(homeDir, ".bashrc");
                    string homePrefix = Path.GetFullPath(homeDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string pathForShell;
                    if (appDirectory.Equals(Path.GetFullPath(homeDir).TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
                        pathForShell = "$HOME";
                    else if (appDirectory.StartsWith(homePrefix, StringComparison.Ordinal))
                        pathForShell = "$HOME/" + ShellQuote(appDirectory[homePrefix.Length..]);
                    else
                        pathForShell = escapedDirectory;
                    profileLine = "export PATH=\"$PATH\":" + pathForShell;
                }

                AddPathEntryToFile(targetFile, profileLine, "/etc/");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string directory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Console.WriteLine(GetLocalizedString("InstallPathAdd", "Application path to add: ") + directory);

                var name = "PATH";
                var scope = systemWide ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
                try
                {
                    var oldValue = Environment.GetEnvironmentVariable(name, scope);
                    var newValue = WindowsPathRegistration.Prepend(oldValue, directory);
                    if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    {
                        Console.WriteLine(GetLocalizedString("InstallAlready", "App is already installed!"));
                    }
                    else
                    {
                        Environment.SetEnvironmentVariable(name, newValue, scope);
                        Console.WriteLine(GetLocalizedString("InstallPriority", "Now app is installed with priority in PATH. Close and reopen your terminal application to refresh PATH."));
                    }
                }
                catch (Exception ex)
                {
                    ErrorCode = ExitCode.IOError;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(GetLocalizedString("ErrorPrefix", "Error: ") + ex.Message);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(GetLocalizedString("InstallAdmin", "For system-wide install of this application, PowerShell/Command Prompt/Windows Terminal must be executed as Administrator!"));
                }
            }
        }

        private static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

        private static void AddPathEntryToFile(string filePath, string line, string privilegedPathPrefix)
        {
            try
            {
                var existingLines = File.Exists(filePath) ? File.ReadAllLines(filePath) : Array.Empty<string>();
                if (existingLines.Contains(line, StringComparer.Ordinal))
                {
                    Console.WriteLine(GetLocalizedString("InstallAlready", "App is already installed!"));
                    return;
                }

                string directory = Path.GetDirectoryName(filePath)!;
                Directory.CreateDirectory(directory);
                string prefix = File.Exists(filePath) && new FileInfo(filePath).Length > 0 && !File.ReadAllText(filePath).EndsWith('\n') ? Environment.NewLine : string.Empty;
                File.AppendAllText(filePath, prefix + line + Environment.NewLine);
                Console.WriteLine(GetLocalizedString("InstallLogin", "Now app is installed. Open a new login shell to refresh PATH."));
            }
            catch (Exception ex)
            {
                ErrorCode = ExitCode.IOError;
                Console.Error.WriteLine(GetLocalizedString("InstallUpdate", "Unable to update ") + filePath + ": " + ex.Message);
                if (filePath.StartsWith(privilegedPathPrefix, StringComparison.Ordinal))
                    Console.Error.WriteLine(GetLocalizedString("InstallPrivileges", "System-wide installation requires administrator privileges."));
            }
        }

        private static void UninstallApp(bool systemWide)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string filePath;
                string entry;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && systemWide)
                {
                    filePath = "/etc/paths.d/zipgenius-x";
                    entry = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && systemWide)
                {
                    filePath = "/etc/profile.d/zipgenius-x.sh";
                    entry = "export PATH=\"$PATH\":" + ShellQuote(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar));
                }
                else
                {
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (string.IsNullOrWhiteSpace(homeDir))
                    {
                        Console.Error.WriteLine(GetLocalizedString("InstallHome", "Unable to determine the current user's home directory."));
                        return;
                    }

                    filePath = Path.Combine(homeDir, RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".zprofile" : ".bashrc");
                    string appDirectory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
                    string homePrefix = Path.GetFullPath(homeDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string pathForShell = appDirectory.Equals(Path.GetFullPath(homeDir).TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)
                        ? "$HOME"
                        : appDirectory.StartsWith(homePrefix, StringComparison.Ordinal)
                            ? "$HOME/" + ShellQuote(appDirectory[homePrefix.Length..])
                            : ShellQuote(appDirectory);
                    entry = "export PATH=\"$PATH\":" + pathForShell;
                }

                RemovePathEntryFromFile(filePath, entry);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string directory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Console.WriteLine(GetLocalizedString("UninstallPath", "Application path to remove: ") + directory);

                var name = "PATH";
                var scope = systemWide ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
                try
                {
                    var oldValue = Environment.GetEnvironmentVariable(name, scope);
                    var newValue = WindowsPathRegistration.Remove(oldValue, directory);
                    if (!string.Equals(oldValue ?? string.Empty, newValue, StringComparison.Ordinal))
                    {
                        Environment.SetEnvironmentVariable(name, newValue, scope);
                        Console.WriteLine(GetLocalizedString("UninstallDone", "App is uninstalled now. Close and reopen your terminal application to refresh PATH."));
                    }
                    else
                    {
                        Console.WriteLine(GetLocalizedString("UninstallAlready", "App is already uninstalled."));
                    }
                }
                catch (Exception ex)
                {
                    ErrorCode = ExitCode.IOError;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(GetLocalizedString("ErrorPrefix", "Error: ") + ex.Message);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(GetLocalizedString("UninstallAdmin", "For system-wide uninstall of this application, PowerShell/Command Prompt/Windows Terminal must be executed as Administrator!"));
                }
            }
        }

        private static void RemovePathEntryFromFile(string filePath, string entry)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine(GetLocalizedString("UninstallAlready", "App is already uninstalled."));
                    return;
                }

                var lines = File.ReadAllLines(filePath).ToList();
                if (!lines.Remove(entry))
                {
                    Console.WriteLine(GetLocalizedString("UninstallAlready", "App is already uninstalled."));
                    return;
                }

                File.WriteAllLines(filePath, lines);
                Console.WriteLine(GetLocalizedString("UninstallShell", "App is uninstalled now. Open a new shell session to refresh PATH."));
            }
            catch (Exception ex)
            {
                ErrorCode = ExitCode.IOError;
                Console.Error.WriteLine(GetLocalizedString("InstallUpdate", "Unable to update ") + filePath + ": " + ex.Message);
                if (filePath.StartsWith("/etc/", StringComparison.Ordinal))
                    Console.Error.WriteLine(GetLocalizedString("UninstallPrivileges", "System-wide uninstallation requires administrator privileges."));
            }
        }
        #endregion


        private sealed class AddCommandOptions
        {
            public string ArchivePath { get; set; } = string.Empty;
            public List<string> Inclusions { get; } = new();
            public List<string> Exclusions { get; } = new();
            public int CompressionLevel { get; set; }
            public char Mode { get; set; } = 'A';
            public string Password { get; set; } = string.Empty;
            public int EncryptionMethod { get; set; } = 1;
            public PathStoreMode PathMode { get; set; } = PathStoreMode.RelativeFolder;
            public bool Recursive { get; set; }
            public bool RemoveArchiveAttribute { get; set; }
            public string AttributeFilter { get; set; } = "X";
            public long VolumeSize { get; set; }
        }

        private static void RenderHelpLines(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                if (line == ">>")
                {
                    if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
                        WaitInput(GetLocalizedString("HelpMore", "Press any key to read more (or CTRL+C to quit)..."));
                    continue;
                }
                if (string.IsNullOrEmpty(line))
                {
                    Console.WriteLine();
                    continue;
                }

                string output = line;
                switch (line[0])
                {
                    case '^': Console.ForegroundColor = ConsoleColor.Green; output = line.TrimStart('^'); break;
                    case '*': Console.ForegroundColor = ConsoleColor.Yellow; output = line.TrimStart('*'); break;
                    case '[': Console.ForegroundColor = ConsoleColor.DarkYellow; output = line.TrimStart('['); break;
                    case ']': Console.ForegroundColor = ConsoleColor.Blue; output = line.TrimStart(']'); break;
                    case '%': Console.ForegroundColor = ConsoleColor.Black; Console.BackgroundColor = ConsoleColor.Yellow; output = line.TrimStart('%'); break;
                }
                Console.WriteLine(output);
                Console.ResetColor();
            }
        }

        // ZipGenius 6 optimizes OpenDocument files and legacy media-player skins by
        // rebuilding their ZIP container with the strongest regular ZIP compression.
        // The original is retained as <file>.BAK and a ZIP comment marks the result.
        private static readonly HashSet<string> OptimizableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".odt", ".ods", ".odp", ".odg", ".otg", ".otp", ".odm", ".odf", ".odb", ".ots", ".ott", ".oxt",
            ".wsz", ".wmz", ".mskin", ".wal", ".dsfz"
        };

        private static ExitCode OptimizeFiles(string[] args)
        {
            if (args.Length < 2)
            {
                WriteLocalizedError("OptimizeMissingTarget", "Specify at least one LibreOffice/OpenOffice document or WinAmp/WMP skin.");
                return ExitCode.CompressionError;
            }

            bool recursive = args.Any(argument => argument.Equals("R1", StringComparison.OrdinalIgnoreCase) || argument.Equals("--recursive", StringComparison.OrdinalIgnoreCase));
            bool force = args.Any(argument => argument.Equals("--force", StringComparison.OrdinalIgnoreCase));
            List<string> targets = new();
            foreach (string argument in args.Skip(1))
            {
                if (argument.StartsWith("--", StringComparison.Ordinal) || argument.Equals("R0", StringComparison.OrdinalIgnoreCase) || argument.Equals("R1", StringComparison.OrdinalIgnoreCase))
                    continue;
                string path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(argument.Trim('"')));
                if (File.Exists(path))
                    targets.Add(path);
                else if (Directory.Exists(path))
                    targets.AddRange(Directory.EnumerateFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                        .Where(file => OptimizableExtensions.Contains(Path.GetExtension(file))));
                else
                {
                    WriteLocalizedError("OptimizeTargetNotFound", "File or folder not found: {0}", path);
                    return ExitCode.FileDoesNotExists;
                }
            }

            targets = targets.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (targets.Count == 0)
            {
                WriteLocalizedError("OptimizeNoSupportedFiles", "No supported documents or skins were found.");
                return ExitCode.ZeroItemsArchive;
            }

            int failures = 0;
            foreach (string target in targets)
            {
                if (!OptimizableExtensions.Contains(Path.GetExtension(target)))
                {
                    WriteLocalizedError("OptimizeUnsupportedFile", "Unsupported optimization target: {0}", target);
                    failures++;
                    continue;
                }
                if (!OptimizeZipContainer(target, force))
                    failures++;
            }
            return failures == 0 ? ExitCode.Success : ExitCode.CompressionError;
        }

        private static bool OptimizeZipContainer(string sourcePath, bool force)
        {
            string backupPath = sourcePath + ".BAK";
            string directory = Path.GetDirectoryName(sourcePath)!;
            string temporaryPath = Path.Combine(directory, ".zg-optim-" + Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath));
            try
            {
                FileAttributes originalAttributes = File.GetAttributes(sourcePath);
                DateTime originalLastWrite = File.GetLastWriteTimeUtc(sourcePath);
                using (ZipFile source = ZipFile.Read(sourcePath))
                {
                    if (source.Entries.Any(entry => entry.UsesEncryption))
                    {
                        WriteLocalizedError("OptimizeEncrypted", "Refusing to optimize encrypted file: {0}", sourcePath);
                        return false;
                    }
                    if (!force && string.Equals(source.Comment, "ZG", StringComparison.Ordinal))
                    {
                        if (!IsSilent)
                            Console.WriteLine(GetLocalizedString("OptimizeAlreadyDone", "Already optimized: {0}", sourcePath));
                        return true;
                    }

                    using (ZipFile optimized = new())
                    {
                        optimized.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;
                        optimized.Comment = "ZG";
                        foreach (ZipEntry entry in source.Entries)
                        {
                            ZipEntry replacement;
                            if (entry.IsDirectory)
                                replacement = optimized.AddDirectoryByName(entry.FileName);
                            else
                            {
                                ZipEntry originalEntry = entry;
                                replacement = optimized.AddEntry(entry.FileName, (_, output) =>
                                {
                                    using Stream input = originalEntry.OpenReader();
                                    input.CopyTo(output);
                                });
                            }
                            replacement.LastModified = entry.LastModified;
                            replacement.Attributes = entry.Attributes;
                            replacement.Comment = entry.Comment;
                        }
                        optimized.Save(temporaryPath);
                    }
                }

                // Keep the recoverable backup prescribed by the classic guide before replacing the document.
                // The source ZipFile has been disposed above, which is required before replacing it on Windows.
                File.Copy(sourcePath, backupPath, true);
                File.Move(temporaryPath, sourcePath, true);
                File.SetLastWriteTimeUtc(sourcePath, originalLastWrite);
                File.SetAttributes(sourcePath, originalAttributes);

                if (!IsSilent)
                    Console.WriteLine(GetLocalizedString("OptimizeCompleted", "Optimized {0}; backup saved as {1}", sourcePath, backupPath));
                return true;
            }
            catch (Exception ex) when (ex is ZipException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                WriteLocalizedError("OptimizeFailed", "Could not optimize {0}: {1}", sourcePath, ex.Message);
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static ExitCode CreateBackup(string[] args)
        {
            if (args.Length < 3)
            {
                WriteLocalizedError("BackupMissingArguments", "Syntax: zg -backup archive source [source ...] [options]");
                return ExitCode.CompressionError;
            }

            bool timestamp = args.Any(argument => argument.Equals("T", StringComparison.OrdinalIgnoreCase) || argument.Equals("--timestamp", StringComparison.OrdinalIgnoreCase));
            bool update = args.Any(argument => argument.Equals("U", StringComparison.OrdinalIgnoreCase) || argument.Equals("AU", StringComparison.OrdinalIgnoreCase) || argument.Equals("--update", StringComparison.OrdinalIgnoreCase));
            bool recursive = !args.Any(argument => argument.Equals("R0", StringComparison.OrdinalIgnoreCase));
            string compression = args.FirstOrDefault(argument => Regex.IsMatch(argument, "^C[0159B]$", RegexOptions.IgnoreCase))?.ToUpperInvariant() ?? "C5";
            string archivePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1].Trim('"')));
            if (string.IsNullOrEmpty(Path.GetExtension(archivePath)))
                archivePath += ".zip";
            if (timestamp)
                archivePath = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + Path.GetExtension(archivePath));
            if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                WriteLocalizedError("BackupZipRequired", "Backup archives must use the .zip extension: {0}", archivePath);
                return ExitCode.CompressionError;
            }
            if (File.Exists(archivePath) && !update)
            {
                WriteLocalizedError("BackupArchiveExists", "Backup archive already exists. Use --update to update it or --timestamp to create a new dated archive: {0}", archivePath);
                return ExitCode.CompressionError;
            }

            List<string> sources = new();
            foreach (string argument in args.Skip(2))
            {
                if (argument.StartsWith("--", StringComparison.Ordinal))
                {
                    string? knownFolder = ResolveBackupKnownFolder(argument);
                    if (knownFolder is not null)
                        sources.Add(knownFolder);
                    continue;
                }
                if (argument.Equals("U", StringComparison.OrdinalIgnoreCase) || argument.Equals("AU", StringComparison.OrdinalIgnoreCase) ||
                    argument.Equals("T", StringComparison.OrdinalIgnoreCase) || argument.Equals("R0", StringComparison.OrdinalIgnoreCase) || argument.Equals("R1", StringComparison.OrdinalIgnoreCase) ||
                    Regex.IsMatch(argument, "^C[0159B]$", RegexOptions.IgnoreCase))
                    continue;
                string sourcePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(argument.TrimStart('+').Trim('"')));
                if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                {
                    WriteLocalizedError("BackupSourceNotFound", "Backup source not found: {0}", sourcePath);
                    return ExitCode.FileDoesNotExists;
                }
                sources.Add(sourcePath);
            }
            sources = sources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (sources.Count == 0)
            {
                WriteLocalizedError("BackupNoSources", "No backup source was specified.");
                return ExitCode.ZeroItemsArchive;
            }

            // Reuse the hardened archive writer: backup is ZIP creation with a deliberate update policy.
            List<string> addArguments = new() { "-add", archivePath, compression, update ? "AU" : "AA", recursive ? "R1" : "R0", "F1" };
            addArguments.AddRange(sources.Select(source => "+" + source));
            ExitCode result = ProcessAddCompatible(addArguments.ToArray());
            if (result == ExitCode.Success && !IsSilent)
                Console.WriteLine(GetLocalizedString("BackupCompleted", "Backup completed: {0}", archivePath));
            return result;
        }

        private static string? ResolveBackupKnownFolder(string option)
        {
            return option.ToLowerInvariant() switch
            {
                "--documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "--music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "--pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "--videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "--favorites" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                _ => null
            };
        }

        private static readonly HashSet<string> ConvertibleOutputExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".cab", ".tar", ".tgz", ".tbz", ".tbz2", ".txz", ".sqx", ".bz2", ".z"
        };

        private static ExitCode ConvertArchive(string[] args)
        {
            if (args.Length < 3)
            {
                WriteLocalizedError("ConvertMissingArguments", "Syntax: zg -convert source-archive destination-archive [options]");
                return ExitCode.CompressionError;
            }

            string sourcePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1].Trim('"')));
            string destinationPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[2].Trim('"')));
            if (!File.Exists(sourcePath))
            {
                WriteLocalizedError("ConvertSourceNotFound", "Source archive not found: {0}", sourcePath);
                return ExitCode.FileDoesNotExists;
            }
            if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                WriteLocalizedError("ConvertSamePath", "The source and destination archives must be different.");
                return ExitCode.CompressionError;
            }
            if (!IsConvertibleOutputPath(destinationPath))
            {
                WriteLocalizedError("ConvertUnsupportedDestination", "Unsupported destination archive format: {0}", destinationPath);
                return ExitCode.CompressionError;
            }

            if (OperatingSystem.IsLinux() &&
                (GetArchiveExtension(destinationPath) is ".cab" or ".sqx" ||
                 GetArchiveExtension(sourcePath) == ".sqx"))
            {
                WriteLocalizedError("ConvertLinuxUnsupportedFormat",
                    "SQX conversion and CAB creation are not supported on Linux.");
                return ExitCode.CompressionError;
            }

            bool overwrite = args.Any(argument => argument.Equals("--overwrite", StringComparison.OrdinalIgnoreCase));
            if (File.Exists(destinationPath) && !overwrite)
            {
                WriteLocalizedError("ConvertDestinationExists", "Destination archive already exists. Use --overwrite to replace it: {0}", destinationPath);
                return ExitCode.CompressionError;
            }

            string stagingRoot = Path.Combine(TempPath, "zgshell-convert-" + Guid.NewGuid().ToString("N"));
            string outputRoot = Path.Combine(Path.GetDirectoryName(destinationPath)!, ".zgshell-convert-" + Guid.NewGuid().ToString("N"));
            string stagedDestination = Path.Combine(outputRoot, Path.GetFileName(destinationPath));
            try
            {
                Directory.CreateDirectory(stagingRoot);
                List<string> extractionArguments = new() { "-extract", sourcePath, stagingRoot, "R1", "F1", "O0" };
                string? password = args.Skip(3).FirstOrDefault(argument => argument.StartsWith("P", StringComparison.OrdinalIgnoreCase) && argument.Length > 1);
                if (password is not null)
                    extractionArguments.Add(password);
                ExitCode extractionResult = ExtractFiles(extractionArguments.ToArray());
                if (extractionResult != ExitCode.Success)
                    return extractionResult;

                string compression = args.Skip(3).FirstOrDefault(argument => Regex.IsMatch(argument, "^C[0159B]$", RegexOptions.IgnoreCase))?.ToUpperInvariant() ?? "C5";
                List<string> addArguments = new() { "-add", stagedDestination, compression, "AA", "R1", "F1", "DXF" };
                // Add staging children individually: adding the staging wildcard itself would leak the
                // randomly named temporary root into the converted archive.
                addArguments.AddRange(Directory.EnumerateFileSystemEntries(stagingRoot).Select(path => "+" + path));
                string? encryptionAlgorithm = args.Skip(3).FirstOrDefault(argument => Regex.IsMatch(argument, "^Q[123]$", RegexOptions.IgnoreCase));
                if (password is not null)
                    addArguments.Add(password);
                if (encryptionAlgorithm is not null)
                    addArguments.Add(encryptionAlgorithm);

                Directory.CreateDirectory(outputRoot);
                ExitCode conversionResult = ProcessAddCompatible(addArguments.ToArray());
                if (conversionResult == ExitCode.Success)
                    File.Move(stagedDestination, destinationPath, overwrite);
                if (conversionResult == ExitCode.Success && !IsSilent)
                    Console.WriteLine(GetLocalizedString("ConvertCompleted", "Converted {0} to {1}", sourcePath, destinationPath));
                return conversionResult;
            }
            catch (IOException ex)
            {
                WriteLocalizedError("ConvertFailed", "Could not convert {0}: {1}", sourcePath, ex.Message);
                return ExitCode.IOError;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLocalizedError("ConvertFailed", "Could not convert {0}: {1}", sourcePath, ex.Message);
                return ExitCode.IOError;
            }
            finally
            {
                DeleteExtractionStagingFolder(stagingRoot);
                DeleteExtractionStagingFolder(outputRoot);
            }
        }

        private static bool IsConvertibleOutputPath(string path)
        {
            string extension = GetArchiveExtension(path);
            return ConvertibleOutputExtensions.Contains(extension) || TarArchiveEditor.IsTarArchivePath(path);
        }

        private static ExitCode ProcessAddCompatible(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("ERROR > Syntax: zg -add archive {options} +file {-file}");
                return ExitCode.CompressionError;
            }

            try
            {
                AddCommandOptions options = ParseAddCommand(args);
                Dictionary<string, string> selectedFiles = BuildAddFileList(options);
                if (selectedFiles.Count == 0)
                {
                    Console.WriteLine("ERROR > No files match the supplied inclusion and exclusion criteria.");
                    return ExitCode.ZeroItemsArchive;
                }

                // Check everything before any writer stages, modifies, or moves sources.
                foreach (string candidate in selectedFiles.Values.Where(File.Exists)
                    .Prepend(options.ArchivePath).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var safety = CheckZipBomb(candidate);
                    if (safety.Blocked)
                        return (ExitCode)safety.ExitCode;
                }

                string extension = GetArchiveExtension(options.ArchivePath);
                if (extension == ".cab")
                    return WriteCompatibleCab(options, selectedFiles);
                if (extension == ".sqx")
                    return WriteCompatibleSqx(options, selectedFiles);
                if (TarArchiveEditor.IsTarArchivePath(options.ArchivePath))
                    return WriteCompatibleTar(options, selectedFiles);
                if (extension is ".bz2" or ".z")
                    return WriteCompatibleSingleStream(options, selectedFiles, extension);
                if (IsZipFile(extension) && options.VolumeSize <= int.MaxValue)
                    return WriteCompatibleZip(options, selectedFiles);

                return WriteCompatibleSevenZip(options, selectedFiles, extension);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("ERROR > " + ex.Message);
                return ExitCode.CompressionError;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("ERROR > " + ex.Message);
                return ExitCode.IOError;
            }
            catch (IOException ex)
            {
                Console.WriteLine("ERROR > " + ex.Message);
                return ExitCode.IOError;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR > " + ex.Message);
                return ExitCode.UnknownError;
            }
        }

        private static AddCommandOptions ParseAddCommand(string[] args)
        {
            var options = new AddCommandOptions
            {
                ArchivePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1]))
            };

            for (int i = 2; i < args.Length; i++)
            {
                string parameter = args[i];
                if (string.IsNullOrWhiteSpace(parameter))
                    continue;
                if (parameter.StartsWith("--", StringComparison.Ordinal))
                    continue;

                char prefix = char.ToUpperInvariant(parameter[0]);
                string value = parameter.Length > 1 ? parameter[1..] : string.Empty;
                switch (prefix)
                {
                    case '+':
                        options.Inclusions.Add(UnquoteAndExpand(value));
                        break;
                    case '-':
                        options.Exclusions.Add(UnquoteAndExpand(value));
                        break;
                    case '@':
                    case 'L':
                        ReadLegacyAddList(UnquoteAndExpand(value), options);
                        break;
                    case 'C':
                        options.CompressionLevel = value.ToUpperInvariant() switch
                        {
                            "0" => 0,
                            "1" => 1,
                            "5" => 5,
                            "9" => 9,
                            "B" => 10,
                            _ => throw new ArgumentException($"Invalid compression level: {parameter}")
                        };
                        break;
                    case 'A':
                        options.Mode = value.ToUpperInvariant() switch
                        {
                            "A" => 'A',
                            "M" => 'M',
                            "U" => 'U',
                            _ => throw new ArgumentException($"Invalid compression mode: {parameter}")
                        };
                        break;
                    case 'P':
                        options.Password = value == "0" ? string.Empty : value;
                        break;
                    case 'Q':
                        if (!int.TryParse(value, out int encryptionMethod) || encryptionMethod < 0 || encryptionMethod > 3)
                            throw new ArgumentException($"Invalid encryption method: {parameter}");
                        options.EncryptionMethod = encryptionMethod;
                        break;
                    case 'F':
                        options.PathMode = value switch
                        {
                            "0" => PathStoreMode.NoPath,
                            "1" => PathStoreMode.RelativeFolder,
                            "2" => PathStoreMode.FullPathNoDrive,
                            "3" => PathStoreMode.FullPathWithDrive,
                            _ => throw new ArgumentException($"Invalid path mode: {parameter}")
                        };
                        break;
                    case 'R':
                        options.Recursive = ParseLegacyBoolean(parameter, value);
                        break;
                    case 'K':
                        DoShowProgress = ParseLegacyBoolean(parameter, value);
                        break;
                    case 'I':
                        options.RemoveArchiveAttribute = ParseLegacyBoolean(parameter, value);
                        break;
                    case 'D':
                        options.AttributeFilter = value.ToUpperInvariant();
                        if (options.AttributeFilter.Length == 0 || options.AttributeFilter.Any(c => !"AHRSFZX".Contains(c)))
                            throw new ArgumentException($"Invalid attribute filter: {parameter}");
                        break;
                    case 'V':
                    case 'S':
                        options.VolumeSize = ParseLegacyVolumeSize(prefix, value, options.ArchivePath);
                        break;
                    case 'E':
                    case 'O':
                        // Kept as accepted no-op switches for command-line compatibility.
                        break;
                    case 'U':
                        options.ArchivePath = Path.Combine(
                            Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory(),
                            Path.GetFileNameWithoutExtension(options.ArchivePath) + "_" + DateTime.Now.Ticks + Path.GetExtension(options.ArchivePath));
                        break;
                    case 'H':
                        // Current zgshell extension: handled by the legacy writer only.
                        break;
                    default:
                        throw new ArgumentException($"Unknown -add parameter: {parameter}");
                }
            }

            if (options.Inclusions.Count == 0)
                throw new ArgumentException(GetLocalizedString("AddNeedInclusion", "At least one +file or @list inclusion is required."));
            if (IsSilent || args.Any(argument => argument.Equals("--noprogress", StringComparison.OrdinalIgnoreCase) ||
                argument.Equals("--progressoff", StringComparison.OrdinalIgnoreCase) ||
                argument.Equals("--p", StringComparison.OrdinalIgnoreCase)))
                DoShowProgress = false;
            return options;
        }

        private static bool ParseLegacyBoolean(string parameter, string value)
        {
            return value switch
            {
                "0" => false,
                "1" => true,
                _ => throw new ArgumentException($"Invalid boolean option: {parameter}")
            };
        }

        private static string UnquoteAndExpand(string value)
        {
            return Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        }

        private static void ReadLegacyAddList(string listPath, AddCommandOptions options)
        {
            foreach (string rawLine in File.ReadLines(listPath))
            {
                string line = rawLine.Trim();
                if (line.Length < 2 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                    continue;
                if (line[0] == '+')
                    options.Inclusions.Add(UnquoteAndExpand(line[1..]));
                else if (line[0] == '-')
                    options.Exclusions.Add(UnquoteAndExpand(line[1..]));
            }
        }

        private static long ParseLegacyVolumeSize(char prefix, string value, string archivePath)
        {
            string normalized = value.Trim();
            if (normalized.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                string root = Path.GetPathRoot(archivePath) ?? string.Empty;
                DriveInfo drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.Equals(root, StringComparison.OrdinalIgnoreCase));
                if (drive == null || !drive.IsReady)
                    throw new ArgumentException(GetLocalizedString("AddMediaCapacity", "Unable to determine the destination media capacity."));
                return drive.AvailableFreeSpace;
            }

            string expectedPrefix = prefix == 'S' ? "sm" : "vm";
            if (normalized.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized[2..];

            return normalized.ToUpperInvariant() switch
            {
                "720" => 720_000L,
                "880" => 880_000L,
                "1440" => 1_440_000L,
                "2880" => 2_880_000L,
                "EMAIL" => 250_000L,
                "WEB" => 750_000L,
                "CDR650" => 650_000_000L,
                "CDR700" => 700_000_000L,
                "CDR800" => 800_000_000L,
                "DVD5" => 4_700_000_000L,
                "DVD9" => 8_500_000_000L,
                _ => throw new ArgumentException($"Invalid volume size: {prefix}{value}")
            };
        }

        private static Dictionary<string, string> BuildAddFileList(AddCommandOptions options)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string inclusion in options.Inclusions)
            {
                string expanded = Path.GetFullPath(inclusion);
                bool hasWildcards = expanded.IndexOfAny(new[] { '*', '?' }) >= 0;
                string searchRoot;
                string pattern;
                string relativeRoot;

                if (hasWildcards)
                {
                    searchRoot = Path.GetDirectoryName(expanded) ?? Directory.GetCurrentDirectory();
                    pattern = Path.GetFileName(expanded);
                    relativeRoot = Directory.GetParent(searchRoot)?.FullName ?? searchRoot;
                }
                else if (Directory.Exists(expanded))
                {
                    AddSelectedPath(result, expanded, Directory.GetParent(expanded)?.FullName ?? expanded, options);
                    searchRoot = expanded;
                    pattern = "*";
                    relativeRoot = Directory.GetParent(searchRoot)?.FullName ?? searchRoot;
                    AddSelectedPath(result, expanded, relativeRoot, options);
                }
                else if (File.Exists(expanded))
                {
                    AddSelectedPath(result, expanded, Path.GetDirectoryName(expanded) ?? Directory.GetCurrentDirectory(), options);
                    continue;
                }
                else
                {
                    throw new FileNotFoundException(GetLocalizedString("AddMissingInclusion", "The included file or folder does not exist."), inclusion);
                }

                SearchOption searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string filePath in Directory.EnumerateFiles(searchRoot, pattern, searchOption))
                    AddSelectedPath(result, filePath, relativeRoot, options);
                foreach (string directoryPath in Directory.EnumerateDirectories(searchRoot, pattern, searchOption))
                    AddSelectedPath(result, directoryPath, relativeRoot, options);
            }

            foreach (string key in result.Where(pair => IsExcluded(pair.Value, pair.Key, options.Exclusions)).Select(pair => pair.Key).ToList())
                result.Remove(key);
            return result;
        }

        private static void AddSelectedPath(Dictionary<string, string> result, string filePath, string relativeRoot, AddCommandOptions options)
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!MatchesAttributeFilter(fullPath, options.AttributeFilter))
                return;

            string entryName = options.PathMode switch
            {
                PathStoreMode.NoPath => Path.GetFileName(fullPath),
                PathStoreMode.RelativeFolder => Path.GetRelativePath(relativeRoot, fullPath),
                PathStoreMode.FullPathNoDrive => fullPath[Path.GetPathRoot(fullPath)!.Length..],
                PathStoreMode.FullPathWithDrive => fullPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                _ => Path.GetFileName(fullPath)
            };
            entryName = entryName.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            if (Directory.Exists(fullPath))
                entryName = entryName.TrimEnd('/') + "/";
            if (result.TryGetValue(entryName, out string existing) && !existing.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"More than one source file maps to the archive path '{entryName}'. Use F1, F2 or F3.");
            result[entryName] = fullPath;
        }

        private static bool IsExcluded(string fullPath, string entryName, IEnumerable<string> exclusions)
        {
            foreach (string exclusion in exclusions)
            {
                string candidate = exclusion.Replace('\\', '/');
                string normalizedFullPath = fullPath.Replace('\\', '/');
                if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(candidate, Path.GetFileName(fullPath), true) ||
                    System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(candidate, entryName, true) ||
                    System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(candidate, normalizedFullPath, true) ||
                    normalizedFullPath.Contains("/" + candidate.Trim('/') + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool MatchesAttributeFilter(string filePath, string filter)
        {
            FileAttributes attributes = File.GetAttributes(filePath);
            bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
            if (isDirectory)
                return filter.Contains('F');
            if (filter.Contains('X'))
                return true;
            if (filter.Contains('Z') && attributes == FileAttributes.Normal)
                return true;
            return (filter.Contains('A') && attributes.HasFlag(FileAttributes.Archive)) ||
                (filter.Contains('H') && attributes.HasFlag(FileAttributes.Hidden)) ||
                (filter.Contains('R') && attributes.HasFlag(FileAttributes.ReadOnly)) ||
                (filter.Contains('S') && attributes.HasFlag(FileAttributes.System)) ||
                (filter.Contains('F') && attributes.HasFlag(FileAttributes.Directory));
        }

        private static string GetArchiveExtension(string archivePath)
        {
            string lower = archivePath.ToLowerInvariant();
            foreach (string compound in new[] { ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.z" })
                if (lower.EndsWith(compound, StringComparison.Ordinal))
                    return compound;
            return Path.GetExtension(lower);
        }

        private static ExitCode WriteCompatibleZip(AddCommandOptions options, Dictionary<string, string> selectedFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory());
            using ZipFile output = File.Exists(options.ArchivePath) ? ZipFile.Read(options.ArchivePath) : new ZipFile();
            output.Name = options.ArchivePath;
            output.AlternateEncoding = Encoding.UTF8;
            output.AlternateEncodingUsage = ZipOption.Always;
            output.CompressionLevel = options.CompressionLevel switch
            {
                0 => Ionic.Zlib.CompressionLevel.None,
                1 => Ionic.Zlib.CompressionLevel.BestSpeed,
                5 => Ionic.Zlib.CompressionLevel.Default,
                _ => Ionic.Zlib.CompressionLevel.BestCompression
            };
            output.MaxOutputSegmentSize = checked((int)options.VolumeSize);
            if (!string.IsNullOrEmpty(options.Password))
            {
                output.Password = options.Password;
                output.Encryption = options.EncryptionMethod switch
                {
                    2 => Ionic.Zip.EncryptionAlgorithm.WinZipAes128,
                    3 => Ionic.Zip.EncryptionAlgorithm.WinZipAes256,
                    _ => Ionic.Zip.EncryptionAlgorithm.PkzipWeak
                };
            }

            foreach ((string entryName, string sourcePath) in selectedFiles)
            {
                if (Directory.Exists(sourcePath))
                {
                    if (!output.ContainsEntry(entryName))
                        output.AddDirectoryByName(entryName);
                    continue;
                }
                bool exists = output.ContainsEntry(entryName);
                if (exists && options.Mode == 'A')
                    continue;
                if (exists)
                    output.RemoveEntry(entryName);
                string directory = Path.GetDirectoryName(entryName)?.Replace('\\', '/') ?? string.Empty;
                ZipEntry entry = output.AddFile(sourcePath, directory);
                entry.FileName = entryName;
                if (options.RemoveArchiveAttribute)
                    entry.Attributes &= ~FileAttributes.Archive;
            }

            using ProgressBar? progress = CreateAddProgress(100, GetLocalizedString("CompressFiles", "Compressing files..."));
            if (progress is not null)
            {
                output.SaveProgress += (_, eventArgs) =>
                {
                    if (eventArgs.EventType == ZipProgressEventType.Saving_AfterWriteEntry)
                        progress.Tick(eventArgs.EntriesSaved * 100 / Math.Max(1, output.Entries.Count), eventArgs.CurrentEntry?.FileName ?? string.Empty);
                    else if (eventArgs.EventType == ZipProgressEventType.Saving_EntryBytesRead && eventArgs.TotalBytesToTransfer > 0)
                        progress.Tick((int)((eventArgs.EntriesSaved + (double)eventArgs.BytesTransferred / eventArgs.TotalBytesToTransfer) * 100 / Math.Max(1, output.Entries.Count)), eventArgs.CurrentEntry?.FileName ?? string.Empty);
                };
            }
            output.Save(options.ArchivePath);
            CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
            return ExitCode.Success;
        }

        private static ExitCode WriteCompatibleCab(AddCommandOptions options, Dictionary<string, string> selectedFiles)
        {
            const int maximumCabFiles = 65_535;
            const long maximumCabFileSize = 2L * 1024 * 1024 * 1024;
            if (!string.IsNullOrEmpty(options.Password))
                throw new ArgumentException(GetLocalizedString("CabPassword", "CAB archives do not support password protection."));
            string workRoot = Path.Combine(Path.GetTempPath(), "zg-cab-" + Guid.NewGuid().ToString("N"));
            string stagingRoot = Path.Combine(workRoot, "files");
            string temporaryArchive = Path.Combine(workRoot, "output.cab");

            try
            {
                Directory.CreateDirectory(stagingRoot);
                if (File.Exists(options.ArchivePath))
                    new CabInfo(options.ArchivePath).Unpack(stagingRoot);

                using (ProgressBar? stagingProgress = CreateAddProgress(selectedFiles.Count, "Preparing CAB files..."))
                    StageCompatibleArchiveFiles(options, selectedFiles, stagingRoot, stagingProgress);
                string[] stagedFiles = Directory.GetFiles(stagingRoot, "*", SearchOption.AllDirectories);
                if (stagedFiles.Length == 0)
                    return ExitCode.ZeroItemsArchive;
                if (stagedFiles.Length > maximumCabFiles)
                    throw new ArgumentException($"CAB archives cannot contain more than {maximumCabFiles} files.");
                string? oversizedFile = stagedFiles.FirstOrDefault(path => new FileInfo(path).Length > maximumCabFileSize);
                if (oversizedFile != null)
                    throw new ArgumentException($"CAB archives cannot contain files larger than 2 GB: {oversizedFile}");

                var fileSet = stagedFiles.ToDictionary(
                    path => Path.GetRelativePath(stagingRoot, path),
                    path => Path.GetRelativePath(stagingRoot, path),
                    StringComparer.OrdinalIgnoreCase);
                var cab = new CabInfo(temporaryArchive);
                using (ProgressBar? compressionProgress = CreateAddProgress(stagedFiles.Length, "Compressing CAB archive..."))
                {
                    cab.PackFileSet(stagingRoot, fileSet, options.CompressionLevel switch
                    {
                        0 => Microsoft.Deployment.Compression.CompressionLevel.None,
                        1 => Microsoft.Deployment.Compression.CompressionLevel.Min,
                        5 => Microsoft.Deployment.Compression.CompressionLevel.Normal,
                        _ => Microsoft.Deployment.Compression.CompressionLevel.Max
                    }, (_, eventArgs) => compressionProgress?.Tick(
                        Math.Clamp(eventArgs.CurrentFileNumber, 0, stagedFiles.Length),
                        eventArgs.CurrentFileName ?? string.Empty));
                    compressionProgress?.Tick(stagedFiles.Length, "CAB archive completed.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory());
                File.Move(temporaryArchive, options.ArchivePath, true);
                CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
                return ExitCode.Success;
            }
            finally
            {
                if (Directory.Exists(workRoot))
                    Directory.Delete(workRoot, true);
            }
        }

        private static ExitCode WriteCompatibleTar(AddCommandOptions options, Dictionary<string, string> selectedFiles)
        {
            if (!string.IsNullOrEmpty(options.Password))
                throw new ArgumentException(GetLocalizedString("TarPassword", "TAR-based archives do not support password protection."));

            Dictionary<string, string> files = selectedFiles
                .Where(item => File.Exists(item.Value))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> directories = selectedFiles
                .Where(item => Directory.Exists(item.Value))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

            if (options.Mode == 'A' && File.Exists(options.ArchivePath))
            {
                using IArchive existingArchive = TarArchiveEditor.OpenTarArchive(options.ArchivePath)
                    ?? throw new InvalidDataException(GetLocalizedString("TarUnreadable", "The existing TAR archive cannot be read."));
                HashSet<string> existingPaths = existingArchive.Entries
                    .Select(entry => entry.Key.Replace('\\', '/').Trim('/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string path in files.Keys.Where(path => existingPaths.Contains(path.Trim('/'))).ToList())
                    files.Remove(path);
                foreach (string path in directories.Keys.Where(path => existingPaths.Contains(path.Trim('/'))).ToList())
                    directories.Remove(path);
            }

            int entryCount = Math.Max(1, files.Count + directories.Count);
            using ProgressBar? progress = CreateAddProgress(entryCount, "Compressing TAR archive...");
            HashSet<string> completedEntries = new(StringComparer.OrdinalIgnoreCase);
            TarArchiveEditor.AddOrUpdateEntries(options.ArchivePath, files, directories, (entryName, processed, total) =>
            {
                if ((total <= 0 || processed >= total) && completedEntries.Add(entryName))
                    progress?.Tick(Math.Min(completedEntries.Count, entryCount), entryName);
            });
            progress?.Tick(entryCount, "TAR archive completed.");
            CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
            return ExitCode.Success;
        }

        private static ExitCode WriteCompatibleSingleStream(
            AddCommandOptions options,
            Dictionary<string, string> selectedFiles,
            string extension)
        {
            string[] files = selectedFiles.Values.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length != 1 || selectedFiles.Values.Any(Directory.Exists))
                throw new ArgumentException($"{extension.ToUpperInvariant()} archives can contain one file only.");
            if (!string.IsNullOrEmpty(options.Password))
                throw new ArgumentException($"{extension.ToUpperInvariant()} archives do not support password protection.");
            if (options.Mode == 'A' && File.Exists(options.ArchivePath))
                return ExitCode.Success;

            Directory.CreateDirectory(Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory());
            string temporaryArchive = options.ArchivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using ProgressBar? progress = CreateAddProgress(100, $"Compressing {extension.ToUpperInvariant()} stream...");
                if (extension == ".z")
                {
                    TarArchiveEditor.WriteUnixCompressArchive(files[0], temporaryArchive);
                    progress?.Tick(100, ".Z stream completed.");
                }
                else
                {
                    using FileStream input = File.OpenRead(files[0]);
                    using FileStream output = File.Create(temporaryArchive);
                    using BZip2OutputStream bzip2 = new BZip2OutputStream(output, options.CompressionLevel switch
                    {
                        0 or 1 => 1,
                        5 => 5,
                        _ => 9
                    });
                    CopyStreamWithAddProgress(input, bzip2, progress, files[0]);
                }
                File.Move(temporaryArchive, options.ArchivePath, true);
                CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
                return ExitCode.Success;
            }
            finally
            {
                if (File.Exists(temporaryArchive))
                    File.Delete(temporaryArchive);
            }
        }

        private static void CopyStreamWithAddProgress(Stream input, Stream output, ProgressBar? progress, string message)
        {
            byte[] buffer = new byte[81920];
            long total = input.CanSeek ? input.Length : 0;
            long processed = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                processed += read;
                if (total > 0)
                    progress?.Tick((int)Math.Clamp(processed * 100 / total, 0, 100), message);
            }
            progress?.Tick(100, message);
        }

        private static ExitCode WriteCompatibleSqx(AddCommandOptions options, Dictionary<string, string> selectedFiles)
        {
            if (!IsWindows())
                throw new PlatformNotSupportedException(GetLocalizedString("SqxWindows", "SQX archive creation is available only on Windows."));

            string workRoot = Path.Combine(Path.GetTempPath(), "zg-sqx-" + Guid.NewGuid().ToString("N"));
            string stagingRoot = Path.Combine(workRoot, "files");
            string outputRoot = Path.Combine(workRoot, "output");
            string temporaryArchive = Path.Combine(outputRoot, Path.GetFileName(options.ArchivePath));

            try
            {
                Directory.CreateDirectory(stagingRoot);
                Directory.CreateDirectory(outputRoot);
                if (File.Exists(options.ArchivePath))
                    SqxNative.ExtractArchive(options.ArchivePath, stagingRoot, options.Password);

                using (ProgressBar? stagingProgress = CreateAddProgress(selectedFiles.Count, "Preparing SQX files..."))
                    StageCompatibleArchiveFiles(options, selectedFiles, stagingRoot, stagingProgress);
                if (!Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories).Any())
                    return ExitCode.ZeroItemsArchive;

                int compressionRate = options.CompressionLevel switch
                {
                    0 => 0,
                    1 => 1,
                    5 => 3,
                    9 => 4,
                    _ => 5
                };
                using (ProgressBar? compressionProgress = CreateAddProgress(2, "Compressing SQX archive..."))
                {
                    compressionProgress?.Tick(1, "Compressing SQX archive...");
                    SqxCompressor.CreateArchive(temporaryArchive, stagingRoot, compressionRate,
                        checked((ulong)Math.Max(0, options.VolumeSize)), true, options.Password);
                    compressionProgress?.Tick(2, "SQX archive completed.");
                }

                string[] outputs = Directory.GetFiles(outputRoot);
                if (outputs.Length == 0)
                    throw new IOException(GetLocalizedString("SqxNoOutput", "The SQX compression engine did not create an archive."));
                Directory.CreateDirectory(Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory());
                string temporaryName = Path.GetFileName(temporaryArchive);
                string destinationName = Path.GetFileName(options.ArchivePath);
                foreach (string output in outputs)
                {
                    string outputName = Path.GetFileName(output);
                    string destinationPartName = outputName.StartsWith(temporaryName, StringComparison.OrdinalIgnoreCase)
                        ? destinationName + outputName[temporaryName.Length..]
                        : outputName;
                    File.Move(output, Path.Combine(Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory(), destinationPartName), true);
                }

                CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
                return ExitCode.Success;
            }
            finally
            {
                if (Directory.Exists(workRoot))
                    Directory.Delete(workRoot, true);
            }
        }

        private static ProgressBar? CreateAddProgress(int maximum, string message)
        {
            return DoShowProgress && !IsSilent
                ? new ProgressBar(Math.Max(1, maximum), message, ConsoleColor.Cyan)
                : null;
        }

        private static void StageCompatibleArchiveFiles(
            AddCommandOptions options,
            Dictionary<string, string> selectedFiles,
            string stagingRoot,
            ProgressBar? progress = null)
        {
            string normalizedRoot = Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            int processed = 0;
            foreach ((string entryName, string sourcePath) in selectedFiles)
            {
                string relativePath = GetSafeStagingPath(entryName);
                string destinationPath = Path.GetFullPath(Path.Combine(stagingRoot, relativePath));
                if (!destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Invalid archive path: {entryName}");

                if (Directory.Exists(sourcePath))
                {
                    Directory.CreateDirectory(destinationPath);
                    progress?.Tick(++processed, entryName);
                    continue;
                }

                bool exists = File.Exists(destinationPath);
                if (exists && options.Mode == 'A')
                {
                    progress?.Tick(++processed, entryName);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, true);
                File.SetLastWriteTime(destinationPath, File.GetLastWriteTime(sourcePath));
                if (options.RemoveArchiveAttribute && IsWindows())
                    File.SetAttributes(destinationPath, File.GetAttributes(destinationPath) & ~FileAttributes.Archive);
                progress?.Tick(++processed, entryName);
            }
        }

        private static string GetSafeStagingPath(string entryName)
        {
            string normalized = entryName.Replace('/', Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (normalized.Length >= 2 && normalized[1] == ':')
                normalized = normalized[0] + normalized[2..];
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Split(Path.DirectorySeparatorChar).Any(part => part == ".."))
                throw new ArgumentException($"Invalid archive path: {entryName}");
            return normalized;
        }

        private static ExitCode WriteCompatibleSevenZip(AddCommandOptions options, Dictionary<string, string> selectedFiles, string extension)
        {
            if (OperatingSystem.IsLinux())
            {
                using ProgressBar? nativeProgress = CreateAddProgress(100, "Compressing archive...");
                LibArchiveWriter.Compress(options.ArchivePath, selectedFiles, extension.TrimStart('.'),
                    options.CompressionLevel, options.Password, options.VolumeSize,
                    percent => nativeProgress?.Tick(percent, "Compressing archive..."));
                CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
                return ExitCode.Success;
            }

            SevenZip.OutArchiveFormat format = IsZipFile(extension) ? SevenZip.OutArchiveFormat.Zip : extension switch
            {
                ".7z" => SevenZip.OutArchiveFormat.SevenZip,
                ".tar" => SevenZip.OutArchiveFormat.Tar,
                ".gz" or ".tgz" or ".tar.gz" => SevenZip.OutArchiveFormat.GZip,
                ".bz2" or ".tbz" or ".tbz2" or ".tar.bz2" => SevenZip.OutArchiveFormat.BZip2,
                ".xz" or ".txz" or ".tar.xz" => SevenZip.OutArchiveFormat.XZ,
                ".wim" => SevenZip.OutArchiveFormat.Wim,
                _ => throw new ArgumentException($"Creation of '{extension}' archives is not supported by the current cross-platform compression engine.")
            };

            SevenZip.SevenZipBase.SetLibraryPath(FileUtils.GetSevenZipLibs());
            var compressor = new SevenZip.SevenZipCompressor
            {
                ArchiveFormat = format,
                CompressionLevel = options.CompressionLevel switch
                {
                    0 => SevenZip.CompressionLevel.None,
                    1 => SevenZip.CompressionLevel.Fast,
                    5 => SevenZip.CompressionLevel.Normal,
                    9 => SevenZip.CompressionLevel.High,
                    _ => SevenZip.CompressionLevel.Ultra
                },
                CompressionMode = File.Exists(options.ArchivePath) ? SevenZip.CompressionMode.Append : SevenZip.CompressionMode.Create,
                DirectoryStructure = true,
                VolumeSize = options.VolumeSize,
                ZipEncryptionMethod = options.EncryptionMethod switch
                {
                    2 => SevenZip.ZipEncryptionMethod.Aes128,
                    3 => SevenZip.ZipEncryptionMethod.Aes256,
                    _ => SevenZip.ZipEncryptionMethod.ZipCrypto
                }
            };
            Directory.CreateDirectory(Path.GetDirectoryName(options.ArchivePath) ?? Directory.GetCurrentDirectory());
            ProgressBar? progress = null;
            if (DoShowProgress && !IsSilent)
            {
                progress = new ProgressBar(100, GetLocalizedString("CompressFiles", "Compressing files..."), ConsoleColor.Cyan);
                compressor.Compressing += (_, eventArgs) => progress.Tick(eventArgs.PercentDone, eventArgs.PercentDone + "%");
            }
            compressor.CompressFileDictionary(selectedFiles, options.ArchivePath, options.Password);
            progress?.Dispose();
            CompleteLegacyMoveAndAttributes(options, selectedFiles.Values);
            return ExitCode.Success;
        }

        private static void CompleteLegacyMoveAndAttributes(AddCommandOptions options, IEnumerable<string> sourceFiles)
        {
            foreach (string sourceFile in sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (options.RemoveArchiveAttribute && IsWindows())
                    File.SetAttributes(sourceFile, File.GetAttributes(sourceFile) & ~FileAttributes.Archive);
                if (options.Mode == 'M')
                {
                    if (File.Exists(sourceFile))
                        File.Delete(sourceFile);
                    else if (Directory.Exists(sourceFile) && !Directory.EnumerateFileSystemEntries(sourceFile).Any())
                        Directory.Delete(sourceFile);
                }
            }
        }

        private static ExitCode ProcessAddZip(string[] args)
        {
            ExitCode intExitCode = ExitCode.Success;
            //string arcFile = Environment.ExpandEnvironmentVariables(args[1]);

            string currDir = Directory.GetCurrentDirectory();
            string msgIntro = string.Empty;

            fInclusion = new List<string>();
            fExclusion = new List<string>();
            fExclusionExt = new List<string>();

            bool doRecursive = false; // R0

            // EncryptionAlgorithm zEncAlgo = EncryptionAlgorithm.None;
            ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod zEncAlgo = ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod.ZipCrypto;
            string zEncPsw = string.Empty;
            CompressionLevel zLevel = CompressionLevel.None;
            bool zEncAlgoSet = false;
            bool doMoveFiles = false;
            bool doUpdateFiles = false;
            bool doCheckAttrs = false;
            bool UniqueName = false;
            bool doHashFile = false;
            FileAttributes CheckAttrs = new FileAttributes();
            int zVolSize = 0;
            bool DoRemoveArchiveAttr = false;
            PathStoreMode PathMode = PathStoreMode.NoPath;
            int ExportHashMode = 0;
            #region Parameters section
            string parVerbose = string.Empty;
            // Examine allowed parameters, others will be ignored
            for (int i = 2; i < args.Length; i++)
            {
                //Console.WriteLine(args[i]);
                string cPar = args[i];
                // C - compression level
                if (cPar[0].ToString() == "C")
                {
                    string cLev = cPar[1].ToString();
                    switch (cLev)
                    {
                        case "0":
                            {
                                zLevel = CompressionLevel.None;
                                parVerbose += "C0 -> No compression" + Environment.NewLine; ;
                                break;
                            }
                        case "1":
                        case "2":
                        case "3":
                            {
                                zLevel = CompressionLevel.Fast;
                                parVerbose += cPar + " -> Fast compression" + Environment.NewLine; ;
                                break;
                            }
                        case "4":
                        case "5":
                        case "6":
                            {
                                zLevel = CompressionLevel.Normal;
                                parVerbose += cPar + " -> Normal compression" + Environment.NewLine; ;
                                break;
                            }
                        case "7":
                        case "8":
                        case "9":
                            {
                                zLevel = CompressionLevel.High;
                                parVerbose += cPar + " -> High compression" + Environment.NewLine; ;
                                break;
                            }
                        case "B":
                            {
                                zLevel = CompressionLevel.Ultra;
                                parVerbose += "CB -> Brutal compression" + Environment.NewLine; ;
                                break;
                            }
                        default:
                            {
                                zLevel = CompressionLevel.None;
                                parVerbose += "No compression set because of an invalid parameter (" + cPar + ")" + Environment.NewLine; ;
                                break;
                            }
                    }
                }

                // + - add files
                if (cPar[0].ToString() == "+")
                {
                    string nFile = cPar.Remove(0, 1);
                    if (!fInclusion.Contains(nFile))
                        fInclusion.Add(nFile);
                }

                // - - exclude files
                if (cPar[0].ToString() == "-" && cPar[1].ToString() != "-")
                {
                    string nFile = cPar.Remove(0, 1);
                    //Console.WriteLine("## nFile - " + nFile);
                    if (nFile.StartsWith("*"))
                    {
                        string ext = Path.GetExtension(nFile);
                        //Console.WriteLine("##> " + ext);
                        fExclusionExt.Add(ext);
                    }
                    else
                        fExclusion.Add(nFile);
                }

                // @ - read inclusions/exclusions from file
                if (cPar[0].ToString() == "@" || cPar[0].ToString() == "L")
                {
                    string fFile = cPar.Remove(0, 1);
                    var lines = File.ReadLines(fFile);
                    foreach (var line in lines)
                    {
                        if (line[0].ToString() == "+")
                        {
                            string nFile = line.Remove(0, 1);
                            if (!fInclusion.Contains(nFile))
                                fInclusion.Add(nFile);
                        }
                        if (line[0].ToString() == "-" && line[1].ToString() != "-")
                        {
                            string nFile = line.Remove(0, 1);
                            //Console.WriteLine("## nFile - " + nFile);
                            if (nFile.StartsWith("*"))
                            {
                                string ext = Path.GetExtension(nFile);
                                //Console.WriteLine("##> " + ext);
                                fExclusionExt.Add(ext);
                            }
                            else
                                fExclusion.Add(nFile);
                        }

                    }
                }

                // Q - Encryption algorythm [0|1|2|3] > 0: None; 1: PKZip; 2: AES128; 3: AES256
                if (cPar[0].ToString() == "Q")
                {
                    string qPar = cPar[1].ToString();
                    switch (qPar)
                    {
                        case "0":
                            {
                                zEncAlgo = ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod.ZipCrypto;
                                zEncAlgoSet = false;
                                parVerbose += "Q0 -> No encryption" + Environment.NewLine; ;
                                break;
                            }
                        case "1":
                            {
                                zEncAlgo = ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod.ZipCrypto;
                                zEncAlgoSet = true;
                                parVerbose += "Q1 -> ZipCrypto encryption (weak)" + Environment.NewLine; ;
                                break;
                            }
                        case "2":
                            {
                                zEncAlgo = ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod.AES128;
                                zEncAlgoSet = true;
                                parVerbose += "Q2 -> WinZip Aes128 encryption" + Environment.NewLine; ;
                                break;
                            }
                        case "3":
                            {
                                zEncAlgo = ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod.AES256;
                                zEncAlgoSet = true;
                                parVerbose += "Q3 -> WinZip Aes256 encryption" + Environment.NewLine; ;
                                break;
                            }
                        default:
                            {
                                zEncAlgo = ICSharpCode.SharpZipLib.Zip.ZipEncryptionMethod.ZipCrypto;
                                zEncAlgoSet = false;
                                parVerbose += "ZipCrypto encryption set by default due to invalid parameter (" + cPar + ")" + Environment.NewLine; ;
                                break;
                            }
                    }
                }

                // P - set password (0 > no encryption)
                if (cPar[0].ToString() == "P")
                {
                    string pPar = cPar.Remove(0, 1);
                    if (pPar.Length == 0 && pPar[1].ToString() == "0")
                    {
                        //    zEncAlgo = EncryptionAlgorithm.None;
                        parVerbose += "P0 -> Password is not set, encryption setting will be ignored" + Environment.NewLine; ;
                    }
                    else
                    {
                        zEncPsw = pPar;
                        parVerbose += "P1 -> Password is set" + Environment.NewLine; ;
                    }
                }

                // A - set compression mode [A|M|U]
                if (cPar[0].ToString() == "A")
                {
                    var aPar = cPar[1].ToString();
                    if (aPar == "U")
                    {
                        doMoveFiles = false;
                        doUpdateFiles = true;
                        parVerbose += "AU -> Files in archive will be updated" + Environment.NewLine; ;
                    }
                    else if (aPar == "M")
                    {
                        doMoveFiles = true;
                        doUpdateFiles = false;
                        parVerbose += "AM -> Files will be added to archive and delete from original location" + Environment.NewLine; ;
                    }
                    else
                    {
                        doMoveFiles = false;
                        doUpdateFiles = false;
                        parVerbose += "AA -> Files will be added to archive" + Environment.NewLine; ;
                    }
                }

                // V - set multivolume mode and volume size
                if (cPar[0].ToString() == "V" || cPar[0].ToString() == "S")
                {
                    string vPar = cPar.Remove(0, 1);
                    switch (vPar)
                    {
                        case "vm720":
                            {
                                zVolSize = 720000;
                                break;
                            }
                        case "vm880":
                            {
                                zVolSize = 880000;
                                break;
                            }
                        case "vm1440":
                            {
                                zVolSize = 1440000;
                                break;
                            }
                        case "vm2880":
                            {
                                zVolSize = 2880000;
                                break;
                            }
                        case "vmEmail":
                            {
                                zVolSize = 250000;
                                break;
                            }
                        case "vmWeb":
                            {
                                zVolSize = 750000;
                                break;
                            }
                        case "vmCDR650":
                            {
                                zVolSize = 650000000;
                                break;
                            }
                        case "vmCDR700":
                            {
                                zVolSize = 700000000;
                                break;
                            }
                        case "vmCDR800":
                            {
                                zVolSize = 800000000;
                                break;
                            }
                        default:
                            {
                                zVolSize = 0;
                                break;
                            }
                    }
                    parVerbose += cPar + " -> The archive will have multiple volumes sized " + zVolSize.ToString() + " bytes" + Environment.NewLine; ;
                }

                // D - add files with these parameters (must be tested)
                if (cPar[0].ToString() == "D")
                {
                    doCheckAttrs = true;
                    string setOfAttrs = cPar.Remove(0, 1);
                    if (setOfAttrs.Contains("A"))
                        CheckAttrs += 32;
                    if (setOfAttrs.Contains("R"))
                        CheckAttrs += 1;
                    if (setOfAttrs.Contains("H"))
                        CheckAttrs += 2;
                    if (setOfAttrs.Contains("S"))
                        CheckAttrs += 4;
                    if (setOfAttrs.Contains("F"))
                        CheckAttrs += 16;
                    if (setOfAttrs.Contains("N"))
                        CheckAttrs += 128;


                }

                // R - Set recursion active/inactive
                if (cPar[0].ToString() == "R")
                {
                    string rPar = cPar.Remove(0, 1);
                    if (rPar.ToString() == "0")
                    {
                        doRecursive = false;
                        parVerbose += "R0 -> Directories will not be scanned recursively" + Environment.NewLine; ;
                    }
                    else if (rPar.ToString() == "1")
                    {
                        doRecursive = true;
                        parVerbose += "R1 -> Directories will be scanned recursively" + Environment.NewLine; ;
                    }
                }

                // F - set how to store paths
                if (cPar[0].ToString() == "F")
                {
                    string fPar = cPar.Remove(0, 1);
                    switch (fPar)
                    {
                        case "0":
                            {
                                PathMode = PathStoreMode.NoPath;
                                parVerbose += "F0 -> No file path will be stored in archive" + Environment.NewLine; ;
                                break;
                            }
                        case "1":
                            {
                                PathMode = PathStoreMode.RelativeFolder;
                                parVerbose += "F1 -> Files will be archived in their relative folder path" + Environment.NewLine; ;
                                break;
                            }
                        case "2":
                            {
                                PathMode = PathStoreMode.FullPathNoDrive;
                                parVerbose += "F2 -> The full file path will be stored (no drive letter)" + Environment.NewLine; ;
                                break;
                            }
                        //case "3":
                        //    {
                        //        PathMode = PathStoreMode.FullPathWithDrive;
                        //        break;
                        //    }
                        default:
                            {
                                PathMode = PathStoreMode.NoPath;
                                parVerbose += "No file path will be stored by default because of an invalid parameter (" + cPar + ")" + Environment.NewLine; ;
                                break;
                            }
                    }
                }

                // I - remove "A" attribute from file in archive
                if (cPar[0].ToString() == "I")
                {
                    string iPar = cPar.Remove(0, 1);
                    switch (iPar)
                    {
                        case "0":
                            {
                                DoRemoveArchiveAttr = false;
                                parVerbose += "I0 -> The Archive file attribute will be maintained in files" + Environment.NewLine; ;
                                break;
                            }
                        case "1":
                            {
                                DoRemoveArchiveAttr = true;
                                parVerbose += "I1 -> The Archive file attribute will be removed from files" + Environment.NewLine; ;
                                break;
                            }
                        default:
                            {
                                DoRemoveArchiveAttr = false;
                                parVerbose += "The Archive file attribute will be removed from files by default because of an invalid parameter (" + cPar + ")" + Environment.NewLine; ;
                                break;
                            }
                    }
                }

                // U - if set, uses unique name for archive
                if (cPar == "U")
                {
                    UniqueName = true;
                }

                // H - if set, hashes all files being compressed with SHA-256 and writes "zg-hash.txt" to the archive; if file exists, it will be updated
                if (cPar.StartsWith("H"))
                {
                    doHashFile = true;
                    if (cPar.Length == 2)
                    {
                        char hPar = cPar[1];
                        Console.WriteLine(hPar);
                        if (hPar == 'Z') // into zip file comments
                            ExportHashMode = 0;
                        else if (hPar == 'H') // HTML
                            ExportHashMode = 1;
                        else if (hPar == 'J') // JSON
                            ExportHashMode = 2;
                        else if (hPar == 'X') // XML
                            ExportHashMode = 3;
                        else if (hPar == 'C') // CSV
                            ExportHashMode = 4;
                    }
                }


            }
            #endregion
            string arcFile = string.Empty;
            if (UniqueName)
                arcFile =
                Path.Combine(Path.GetDirectoryName(args[1]),
                Path.GetFileNameWithoutExtension(args[1]) + "_" + DateTime.Now.Ticks.ToString() + Path.GetExtension(args[1]));
            else
                arcFile = args[1];

            if (!File.Exists(arcFile) && IsVerbose)
                msgIntro = "We are going to create the archive " + arcFile + ".";
            else
                msgIntro = "We are going to add files to the archive " + arcFile + ".";
            msgIntro += Environment.NewLine + "Current directory is: " + currDir + ".";
            if (IsVerbose)
                Console.WriteLine(msgIntro);

            string cmdVerbose = "These settings will be used:" + Environment.NewLine;
            cmdVerbose += "For unspecified parameters, the default settings will apply." + Environment.NewLine + parVerbose;
            if (IsVerbose)
                Console.WriteLine(cmdVerbose);

            msgIntro = "There will be " + fInclusion.Count.ToString() + " inclusion(s) and " + fExclusion.Count.ToString() + " exclusion(s).";
            if (IsVerbose)
                Console.WriteLine(msgIntro);


            string arcPath = Path.GetDirectoryName(arcFile);
            if (!Directory.Exists(arcPath))
            {
                bool CanGoOn = false;

                Console.WriteLine("The path \"" + arcPath + "\" doesn't exist. Do you  want to create it? (Y/N): ");
                ConsoleKeyInfo keyPress = Console.ReadKey(intercept: true);
                switch (keyPress.Key)
                {
                    case ConsoleKey.Y:
                        {
                            try
                            {
                                Directory.CreateDirectory(arcPath);
                                CanGoOn = true;
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine(GetLocalizedString("ErrorPrefix", "Error: ") + ex.Message + ". Quitting.");
                                intExitCode = ExitCode.IOError;
                                CanGoOn = false;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(GetLocalizedString("ErrorPrefix", "Error: ") + ex.Message + ". Quitting.");
                                intExitCode = ExitCode.UnknownError;
                                CanGoOn = false;
                            }
                            break;
                        }
                    case ConsoleKey.N:
                        {
                            Console.WriteLine(GetLocalizedString("CannotContinue", "Unable to continue, quitting."));
                            intExitCode = ExitCode.CanceledByUser;
                            CanGoOn = false;
                            break;
                        }
                    default:
                        {
                            break;
                        }

                }

                if (!CanGoOn)
                    return intExitCode;
            }

            if (!IsSilent)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Please, wait: I'm building the list of files to compress...");
                Console.ResetColor();
            }
            FilesAdded = 0;
            FilesToAdd = 0;
            DirsAdded = 0;
            filesDictionary = new Dictionary<string, string>();
            dirsDictionary = new Dictionary<string, string>();
            hashDictionary = new Dictionary<string, string>();
            foreach (var nEntry in fInclusion)
            {
                string entry = nEntry.Replace("\"", "");
                string pFilter = string.Empty;
                string fFilename = Path.GetFileName(entry);
                if (fFilename.Contains("*") || fFilename.Contains("?"))
                {
                    string dPath = Path.GetDirectoryName(entry);
                    if (string.IsNullOrEmpty(dPath))
                        dPath = Directory.GetCurrentDirectory();
                    var dInfo = Directory.EnumerateDirectories(dPath, fFilename, SearchOption.AllDirectories);
                    //Console.WriteLine("## " + dInfo.Length.ToString());
                    SearchOption sOpt = SearchOption.TopDirectoryOnly;
                    if (doRecursive)
                    {
                        //Console.WriteLine("Do Recursive!");
                        sOpt = SearchOption.AllDirectories;
                    }
                    //AddEmptyDirsToDictionary(dirsDictionary, dPath, "", sOpt, PathMode);
                    string pPath = Directory.GetParent(dPath).FullName;
                    AddDirsToDictionary(filesDictionary, dPath, pPath, fFilename, sOpt, PathMode, arcFile);
                }
                else
                    AddFileToDictionary(filesDictionary, entry);
            }

            FilesToAdd = filesDictionary.Count; // + dirsDictionary.Count;

            var options = new ProgressBarOptions
            {
                EnableTaskBarProgress = IsWindows(),
                ShowEstimatedDuration = false,
                CollapseWhenFinished = false,
                PercentageFormat = "{0:N0}% ",
                //BackgroundColor = ConsoleColor.White,
                ForegroundColor = ConsoleColor.Cyan,
                ForegroundColorDone = ConsoleColor.Green,
                ProgressBarOnBottom = true
            };
            if (!IsWindows())
                options.ProgressCharacter = '-';
            if (!DoShowProgress && !IsSilent)
                Console.WriteLine("Please, wait: files are being compressed.");
            try
            {
                zip = new ZipFile(arcFile);
                zip.TempFileFolder = TempPath;
                if (File.Exists(arcFile))
                {
                    FilesInArc = zip.Count;
                }
                zip.ZipError += Zip_ZipError;
                zip.MaxOutputSegmentSize = zVolSize;
                zip.SaveProgress += Zip_SaveProgress;
                if (!IsSilent)
                    if (!doUpdateFiles && FilesInArc > 0)
                        Console.WriteLine("This archive has " + FilesInArc.ToString() + " files: files already in archive will be skipped because parameter \"AA\" is applied.");

                foreach (var fEntry in filesDictionary)
                {
                    FileAttributes attrs = File.GetAttributes(fEntry.Value);

                    if (doCheckAttrs)
                    {
                        if (!CompareAttributes(attrs, CheckAttrs))
                        {
                            goto DoSkip;
                        }
                        else
                        {
                            ZipEntry zEntry = zip.AddFile(fEntry.Value, Path.GetDirectoryName(fEntry.Key));
                            if (DoRemoveArchiveAttr)
                                attrs -= FileAttributes.Archive;
                            zEntry.Attributes = attrs;
                        }
                    }
                    else
                    {
                        ZipEntry zEntry = zip.AddFile(fEntry.Value, Path.GetDirectoryName(fEntry.Key));
                        if (DoRemoveArchiveAttr)
                            attrs -= FileAttributes.Archive;
                        zEntry.Attributes = attrs;
                    }
                DoSkip:
                    Thread.Sleep(0);
                }
                foreach (var dEntry in dirsDictionary)
                {
                    DirectoryInfo di = new DirectoryInfo(dEntry.Value);
                    if (doCheckAttrs)
                    {
                        if (CompareAttributes(di.Attributes, CheckAttrs))
                        {
                            ZipEntry zEntry = zip.AddEntry(dEntry.Key, dEntry.Value);
                            zEntry.Attributes = di.Attributes;
                        }
                    }
                    else
                    {
                        ZipEntry zEntry = zip.AddEntry(dEntry.Key, dEntry.Value);
                        zEntry.Attributes = di.Attributes;
                    }
                }

                if (zip.Entries.Count == 0)
                {
                    if (!IsSilent)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR!");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("A zip archive with 0 entries cannot exist! Please check the parameters you have used.");
                        Console.ResetColor();
                        DoErrorSound();
                    }
                    return ExitCode.ZeroItemsArchive;
                }

                //zip.Comment = "This zip was created at " + System.DateTime.Now.ToString("G");
                if (DoShowProgress)
                {
                    pBar = new ProgressBar(zip.Entries.Count(), GetLocalizedString("CompressFiles", "Compressing files..."), options);
                };
                if (!DoSimulate)
                    zip.Save();
            }
            catch (IOException ex)
            {
                pBar.Dispose();
                Console.WriteLine(GetLocalizedString("ErrorMarker", "ERROR > ") + ex.Message);
                DoErrorSound();
                intExitCode = ExitCode.IOError;
            }
            catch (Ionic.Zip.ZipException ex)
            {
                if (DoShowProgress && pBar != null)
                    pBar.Dispose();
                Console.WriteLine(GetLocalizedString("ErrorMarker", "ERROR > ") + ex.Message);
                DoErrorSound();
                intExitCode = ExitCode.IOError;
            }

            //}
            zip.Dispose();
            if (doHashFile)
            {
                WriteHashFile(arcFile, ExportHashMode);
            }

            if (intExitCode == ExitCode.Success)
                DoSuccessSound();
            return intExitCode;


            #region Old Code
            //using (ZipOutputStream oStream = new ZipOutputStream(arcFile))
            //{   byte[] buffer = new byte[4096];
            //    oStream.CompressionLevel=0; // 0 - store only to 9 - means best compression
            //    var options = new ProgressBarOptions
            //    {
            //        EnableTaskBarProgress = false,
            //        ShowEstimatedDuration = false,
            //        CollapseWhenFinished = false,
            //        PercentageFormat = "{0:N0}% ",
            //        //BackgroundColor = ConsoleColor.White,
            //        ForegroundColor = ConsoleColor.Blue,
            //        ForegroundColorDone = ConsoleColor.Green,
            //        ProgressBarOnBottom = true
            //    };
            //    using (ProgressBar progressBar = new ProgressBar(FilesToAdd, "My Progress Message", options))
            //    {
            //        foreach(var entry in dirsDictionary)
            //        {
            //            ZipEntry zEntry = new ZipEntry();
            //            zEntry.AlternateEncoding = System.Text.Encoding.Unicode;
            //            zEntry.AlternateEncodingUsage = ZipOption.Always;
            //            zEntry.ModifiedTime= File.GetLastWriteTime(entry.Value);
            //            zEntry.Attributes = FileAttributes.Directory;
            //            oStream.PutNextEntry(entry.Key);
            //        }


            //        foreach (var entry in filesDictionary)
            //        {
            //            //Console.WriteLine("<> " + entry.Key + " = " + entry.Value);
            //            using (FileStream fs = File.OpenRead(entry.Value)) {
            //                ZipEntry zEntry = new ZipEntry();
            //                zEntry.AlternateEncoding = System.Text.Encoding.Unicode;
            //                zEntry.AlternateEncodingUsage = ZipOption.Always;
            //                zEntry.FileName = entry.Key;
            //                zEntry.InputStream = fs;
            //                zEntry.ModifiedTime = File.GetLastWriteTime(entry.Value);
            //                FileAttributes fAttr = File.GetAttributes(entry.Value);
            //                if (DoRemoveArchiveAttr)
            //                    zEntry.Attributes = (FileAttributes)(fAttr - FileAttributes.Archive);
            //                oStream.PutNextEntry(entry.Key);
            //                FilesAdded++;
            //                double pct = (FilesAdded / (double)FilesToAdd) * 100;
            //                int ppp = (int)Math.Round(pct);
            //                ProgressUtils.UpdateProgress(progressBar, ppp, entry.Value);
            //            }
            //            //using (FileStream fs = File.OpenRead(entry.Value))
            //            //{
            //            //    StreamUtils.Copy(fs, oStream, buffer); //,ProgrHandler, TimeSpan.FromSeconds(1),null,"");
            //            //    FilesAdded++;
            //            //    double pct = (FilesAdded / (double)FilesToAdd) * 100;
            //            //    int ppp = (int)Math.Round(pct);
            //            //    ProgressUtils.UpdateProgress(progressBar, ppp, entry.Value);
            //            //    //ProgressUtils.WriteProgressBar(ppp);
            //            //    // Console.WriteLine();
            //            //}
            //        }
            //    }
            //    if (DoOpenFolder)
            //        Process.Start("explorer.exe", Path.GetDirectoryName(arcFile));
            //    Console.WriteLine("Finished! ");
            //    if (IsVerbose)
            //    {
            //        Console.WriteLine("------------------");
            //        Console.WriteLine("The archive " + arcFile + " is ready.");
            //        Console.WriteLine("Files added: " + filesDictionary.Count.ToString());
            //        Console.WriteLine("Directories: " + DirsAdded.ToString());
            //        Console.WriteLine("------------------");
            //        Console.WriteLine("Files added.");
            //        Console.WriteLine("");
            //        int i = 1;
            //        foreach(var entry in filesDictionary)
            //        {
            //            Console.WriteLine(i.ToString() + " > " + entry.Key);
            //            i++;
            //        }
            //        Console.WriteLine("------------------");
            //        Console.WriteLine("Directories added.");
            //        Console.WriteLine("");
            //        int j = 1;
            //        foreach (var entry in dirsDictionary)
            //        {
            //            Console.WriteLine(j.ToString() + " > " + entry.Key);
            //            j++;
            //        }
            //    }
            //} 
            #endregion

        }

        private static void WriteHashFile(string arcFile, int expMode = 0)
        {
            // Remember to update this file when files are deleted from archive!!!
            ZipFile zip = ZipFile.Read(arcFile);
            Dictionary<string, string> cDictionary = new Dictionary<string, string>();
            //zip.ExtractSelectedEntries("name = zg-hash.txt","","Z:\\Test",ExtractExistingFileAction.OverwriteSilently);
            //ZipEntry zHashFile = new ZipEntry();
            //zHashFile.FileName = "zg-hash.txt";
            var result = zip.Where(entry => entry.FileName.Equals("zg-hash.txt"));
            //zHashFile.Extract("Z:\\Test");
            if (result.Count() > 0)
            {
                ((ZipEntry)result.ElementAt(0)).Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                using (FileStream hStream = new FileStream(Path.Combine(Path.GetTempPath(), "zg-hash.txt"), FileMode.Open, FileAccess.Read))
                {
                    //hStream.Position = 0;
                    var reader = new StreamReader(hStream);
                    string? line = reader.ReadLine();
                    Console.WriteLine("# " + line);
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] rLine = line.Split(" --> ");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(line);
                        cDictionary.Add(rLine[0], rLine[1]);
                    }
                    Console.ResetColor();
                    reader.Dispose();
                }
            }

            foreach (var hEntry in hashDictionary)
            {
                cDictionary.Add(hEntry.Key, hEntry.Value);
            }

            using (FileStream hStream = new FileStream(Path.Combine(Path.GetTempPath(), "zg-hash.txt"), FileMode.OpenOrCreate, FileAccess.Write))
            using (StreamWriter writer = new StreamWriter(hStream))
            {

                foreach (var cEntry in cDictionary)
                    writer.WriteLine(cEntry.Key + " --> " + cEntry.Value);
                //hStream.Seek(0, SeekOrigin.Begin);

            }
            Console.WriteLine("MOde: " + expMode.ToString());
            switch (expMode)
            {
                case 0:
                    {
                        break;
                    }
                case 1:
                    {
                        ExportDictToHtml(cDictionary, Path.Combine(Path.GetDirectoryName(arcFile), Path.GetFileName(arcFile) + "-zg-hash.html"), arcFile);
                        break;
                    }
                case 2:
                    {
                        ExportDictToJson(cDictionary, Path.Combine(Path.GetDirectoryName(arcFile), Path.GetFileName(arcFile) + "-zg-hash.json"), arcFile);
                        break;
                    }
                case 3:
                    {
                        ExportDictToXml(cDictionary, Path.Combine(Path.GetDirectoryName(arcFile), Path.GetFileName(arcFile) + "-zg-hash.xml"), arcFile);
                        break;
                    }
                case 4:
                    {
                        using (FileStream inStream = new FileStream(Path.Combine(Path.GetTempPath(), "zg-hash.txt"), FileMode.Open, FileAccess.Read))
                        using (FileStream outStream = new FileStream(Path.Combine(Path.GetDirectoryName(arcFile), Path.GetFileName(arcFile) + "-zg-hash.csv"), FileMode.OpenOrCreate, FileAccess.Write))
                        using (StreamReader inReader = new StreamReader(inStream))
                        using (StreamWriter outWriter = new StreamWriter(outStream))
                        {
                            string xLine = string.Empty;
                            outWriter.WriteLine("File,SHA-256 hash");
                            while ((xLine = inReader.ReadLine()) != null)
                            {
                                string cLine = xLine.Replace(" -> ", ",");
                                outWriter.WriteLine(cLine);
                            }
                        }
                        break;
                    }
            }

            zip.UpdateFile(Path.Combine(Path.GetTempPath(), "zg-hash.txt"), "");
            zip.Save();
            File.Delete(Path.Combine(Path.GetTempPath(), "zg-hash.txt"));
            zip.Dispose();
        }

        private static void DeleteFromHashFile(string arcFile, string[] dltFiles)
        {
            Console.WriteLine("Updating hash file");
            ZipFile zip = new ZipFile(arcFile);
            Dictionary<string, string> cDictionary = new Dictionary<string, string>();
            if (zip.ContainsEntry("zg-hash.txt"))
            {
                zip.ExtractSelectedEntries("name=zg-hash.txt", "", Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                zip.Save();
            }
            else
            {
                Console.WriteLine("No zg-hash.txt found");
                zip.Dispose();
                return;
            }

            //var result = zip.Where(entry => entry.FileName.Equals("zg-hash.txt"));
            //if (result.Count() <= 0)
            //{
            //    zip.Dispose();
            //    return;
            //}
            //((ZipEntry)result.ElementAt(0)).Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);

            var tempFile = Path.Combine(Path.GetTempPath(), "zg-hash.txt");
            var linesToKeep = File.ReadLines(tempFile);

            foreach (var ss in linesToKeep)
                Console.WriteLine("-> " + ss);

            List<string> newFiles = new List<string>();
            Console.WriteLine(dltFiles.Count().ToString());
            foreach (var line in dltFiles)
                Console.WriteLine("## " + line);
            foreach (string cLine in linesToKeep)
            {

                string[] sCheck = cLine.Split(" --> ");
                if (!dltFiles.Contains(sCheck[0]))
                    newFiles.Add(cLine);
                else
                    Console.WriteLine(">> " + cLine);
            }
            try
            {
                File.WriteAllLines(tempFile, newFiles);

            }
            finally
            {
                zip.UpdateFile(tempFile, "");
                zip.Save();
                File.Delete(tempFile);
                zip.Dispose();
            }

        }

        public static bool HasEnvironmentVariable(string path)
        {
            Regex regex = new Regex(@"%[A-Za-z0-9\(\)]*%");
            Match match = regex.Match(path);

            return match.Success;
        }
        private static void Zip_ZipError(object? sender, ZipErrorEventArgs e)
        {
            if (e.EventType == ZipProgressEventType.Error_Saving)
            {
                Console.WriteLine("Error!");
            }
        }

        private static void Zip_ExtractProgress(object? sender, ExtractProgressEventArgs e)
        {
            switch (e.EventType)
            {
                case ZipProgressEventType.Extracting_AfterExtractEntry:
                    {
                        FileAttributes attrs = e.CurrentEntry.Attributes;
                        string DestEntry = Path.Combine(e.ExtractLocation, e.CurrentEntry.FileName).Replace('/', Path.DirectorySeparatorChar);
                        Console.WriteLine(DestEntry);
                        File.SetAttributes(DestEntry, attrs);
                        break;
                    }
            }
            return;
        }

        private static void Zip_SaveProgress(object? sender, SaveProgressEventArgs e)
        {
            //Console.WriteLine(e.EventType.ToString());
            switch (e.EventType)
            {
                case ZipProgressEventType.Error_Saving:
                    {
                        e.Cancel = true;
                        Console.WriteLine("Error");
                        if (DoShowProgress)
                            pBar.WriteErrorLine("ERROR! ");
                        break;
                    }
                case ZipProgressEventType.Saving_BeforeWriteEntry:
                    {
                        //pBar.Message="Entries: " + e.EntriesSaved.ToString();
                        break;
                    }
                case ZipProgressEventType.Saving_EntryBytesRead:
                    {
                        string msg = e.CurrentEntry.FileName;

                        if (e.CurrentEntry.Source == ZipEntrySource.FileSystem)
                        {
                            pBar.Message = msg;

                        }
                        break;
                    }
                case ZipProgressEventType.Saving_AfterWriteEntry:
                    {
                        //double pct = (FilesAdded / (double)FilesToAdd) * 100;
                        if (DoShowProgress)
                        {
                            pBar.Tick();
                        }
                        if (e.CurrentEntry.Source == ZipEntrySource.FileSystem)
                        {
                            FilesAdded++;
                        }
                        else if (e.CurrentEntry.Source != ZipEntrySource.ZipFile)
                            if (e.CurrentEntry.FileName.EndsWith('/'))
                                DirsAdded++;
                        break;
                    }
                case ZipProgressEventType.Saving_Completed:
                    {
                        if (DoShowProgress)
                            pBar.Dispose();

                        if (!IsSilent)
                        {
                            int fDiff = FilesAdded - FilesInArc;
                            if (fDiff < 0)
                                fDiff = 0;
                            FileInfo fInfo = new FileInfo(e.ArchiveName);
                            long fArcSize = fInfo.Length;

                            if (IsVerbose)
                            {
                                if (IsDeleting)
                                {

                                }
                                else
                                {
                                    Console.WriteLine("------------------");
                                    Console.WriteLine(GetLocalizedString("ArchivePrefix", "The archive ") + e.ArchiveName + " is ready.");
                                    Console.WriteLine("Total entries: " + zip.Count.ToString() + ".");
                                    Console.WriteLine("Archive size: " + FormatSize(fArcSize) + ".");
                                    if (FilesInArc > 0)
                                    {
                                        Console.WriteLine("Files added: " + (fDiff).ToString() + ".");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Files added: " + FilesAdded.ToString() + ".");
                                    }
                                    Console.WriteLine("Directories: " + DirsAdded.ToString() + ".");
                                    Console.WriteLine("------------------");
                                    if (VerboseLevel == 1)
                                    {
                                        Console.WriteLine("Files added.");
                                        Console.WriteLine("");
                                        int i = 1;
                                        foreach (var entry in filesDictionary)
                                        {
                                            Console.WriteLine(i.ToString() + " > " + entry.Key);
                                            i++;
                                        }
                                        Console.WriteLine("------------------");
                                        Console.WriteLine("Directories added.");
                                        Console.WriteLine("");
                                        int j = 1;
                                        foreach (var entry in dirsDictionary)
                                        {
                                            Console.WriteLine(j.ToString() + " > " + entry.Key);
                                            j++;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (IsDeleting)
                                {

                                }
                                else
                                {
                                    Console.WriteLine("Archive " + e.ArchiveName + " is ready.");
                                    Console.WriteLine("Folders added: " + DirsAdded.ToString());
                                    Console.WriteLine("Files added: " + FilesAdded.ToString());

                                }
                            }
                        }

                        //foreach (var entry in zip.Entries)
                        //    Console.WriteLine(">> " + entry.FileName +Environment.NewLine+"\t"+ entry.Attributes.ToString());

                        break;
                    }

            }
        }

        //private static void ZFile_AddProgress(object? sender, AddProgressEventArgs e)
        //{
        //    if (e.EventType == ZipProgressEventType.Adding_AfterAddEntry)
        //    {
        //        FilesAdded++;
        //        double pct = (FilesAdded /(double)FilesToAdd) * 100;
        //        int ppp = (int)Math.Round(pct);
        //        ProgressUtils.WriteProgressBar(ppp);
        //        //Console.WriteLine("> " + pct.ToString());

        //    }
        //}

        private static ExitCode ProcessAddSevenZip(string[] args)
        {
            ExitCode intExitCode = ExitCode.Success;
            string arcFile = Environment.ExpandEnvironmentVariables(args[1]);
            string currDir = Directory.GetCurrentDirectory();
            string msgVerbose = string.Empty;
            if (!File.Exists(arcFile) && IsVerbose)
                msgVerbose = "We are going to create the archive " + arcFile + ".";
            else
                msgVerbose = "We are going to add files to the archive " + arcFile + ".";
            msgVerbose += Environment.NewLine + "Current directory is: " + currDir + ".";
            Console.WriteLine(msgVerbose);

            fInclusion = new List<string>();
            fExclusion = new List<string>();
            fExclusionExt = new List<string>();

            bool doRecursive = false; // R0

            // EncryptionAlgorithm zEncAlgo = EncryptionAlgorithm.None;
            // Init 7-zip engine
            string LibPath = FileUtils.GetSevenZipLibs();

            SevenZip.ZipEncryptionMethod zEncAlgo = SevenZip.ZipEncryptionMethod.ZipCrypto;
            string zEncPsw = string.Empty;
            CompressionLevel zLevel = CompressionLevel.None;
            bool zEncAlgoSet = false;
            bool doMoveFiles = false;
            bool doUpdateFiles = false;
            int zVolSize = 0;
            PathStoreMode PathMode = PathStoreMode.NoPath;
            // Examine allowed parameters, others will be ignored
            string cmdVerbose = "These settings will be used:" + Environment.NewLine;
            for (int i = 2; i < args.Length; i++)
            {
                Console.WriteLine(args[i]);
                string cPar = args[i];
                // C - compression level
                if (cPar[0].ToString() == "C")
                {
                    string cLev = cPar[1].ToString();
                    switch (cLev)
                    {
                        case "0":
                            {
                                zLevel = CompressionLevel.None;
                                cmdVerbose += "C0 -> No compression" + Environment.NewLine;
                                break;
                            }
                        case "1":
                            {
                                zLevel = CompressionLevel.Fast;
                                cmdVerbose += cPar + " -> Fast compression" + Environment.NewLine; ;
                                break;
                            }
                        case "5":
                            {
                                zLevel = CompressionLevel.Normal;
                                cmdVerbose += cPar + " -> Normal compression" + Environment.NewLine; ;
                                break;
                            }
                        case "9":
                            {
                                zLevel = CompressionLevel.High;
                                cmdVerbose += cPar + " -> High compression" + Environment.NewLine; ;
                                break;
                            }
                        case "B":
                            {
                                zLevel = CompressionLevel.Ultra;
                                cmdVerbose += "CB -> Brutal compression" + Environment.NewLine; ;
                                break;
                            }
                        default:
                            {
                                zLevel = CompressionLevel.None;
                                cmdVerbose += "No compression set because of an invalid parameter (" + cPar + ")" + Environment.NewLine; ;
                                break;
                            }
                    }
                }

                // + - add files
                if (cPar[0].ToString() == "+")
                {
                    string nFile = cPar.Remove(0, 1);
                    fInclusion.Add(nFile);
                }

                // - - exclude files
                if (cPar[0].ToString() == "-")
                {
                    string nFile = cPar.Remove(0, 1);
                    Console.WriteLine("## nFile - " + nFile);
                    if (nFile.StartsWith("*"))
                    {
                        string ext = Path.GetExtension(nFile);
                        Console.WriteLine("##> " + ext);
                        fExclusionExt.Add(ext);
                    }
                    else
                        fExclusion.Add(nFile);
                }

                // @ - read inclusions/exclusions from file
                if (cPar[0].ToString() == "@")
                {
                    string fFile = cPar.Remove(0, 1);
                    var lines = File.ReadLines(fFile);
                    foreach (var line in lines)
                    {
                        if (line[0].ToString() == "+")
                        {
                            string nFile = line.Remove(0, 1);
                            fInclusion.Add(nFile);
                        }
                        if (line[0].ToString() == "-")
                        {
                            string nFile = line.Remove(0, 1);
                            Console.WriteLine("## nFile - " + nFile);
                            if (nFile.StartsWith("*"))
                            {
                                string ext = Path.GetExtension(nFile);
                                Console.WriteLine("##> " + ext);
                                fExclusionExt.Add(ext);
                            }
                            else
                                fExclusion.Add(nFile);
                        }

                    }
                }

                // Q - Encryption algorythm [0|1|2|3] > 0: None; 1: PKZip; 2: AES128; 3: AES256
                if (cPar[0].ToString() == "Q")
                {
                    string qPar = cPar[1].ToString();
                    switch (qPar)
                    {
                        case "0":
                            {

                                zEncAlgo = SevenZip.ZipEncryptionMethod.ZipCrypto;
                                zEncAlgoSet = false;
                                cmdVerbose += "Q0 -> No encryption" + Environment.NewLine; ;
                                break;
                            }
                        case "1":
                            {
                                zEncAlgo = SevenZip.ZipEncryptionMethod.ZipCrypto;
                                zEncAlgoSet = true;
                                cmdVerbose += "Q1 -> ZipCrypto encryption (weak)" + Environment.NewLine; ;
                                break;
                            }
                        case "2":
                            {
                                zEncAlgo = SevenZip.ZipEncryptionMethod.Aes128;
                                zEncAlgoSet = true;
                                cmdVerbose += "Q2 -> WinZip Aes128 encryption" + Environment.NewLine; ;
                                break;
                            }
                        case "3":
                            {
                                zEncAlgo = SevenZip.ZipEncryptionMethod.Aes256;
                                zEncAlgoSet = true;
                                cmdVerbose += "Q3 -> WinZip Aes256 encryption" + Environment.NewLine; ;
                                break;
                            }
                        default:
                            {
                                zEncAlgo = SevenZip.ZipEncryptionMethod.ZipCrypto;
                                zEncAlgoSet = false;

                                break;
                            }
                    }
                }

                // P - set password (0 > no encryption)
                if (cPar[0].ToString() == "P")
                {
                    string pPar = cPar.Remove(0, 1);
                    if (pPar.Length == 0 && pPar[1].ToString() == "0")
                    {
                        //    zEncAlgo = EncryptionAlgorithm.None;
                    }
                    else
                    {
                        zEncPsw = pPar;
                    }
                }

                // A - set compression mode [A|M|U]
                if (cPar[0].ToString() == "A")
                {
                    var aPar = cPar[1].ToString();
                    if (aPar == "U")
                    {
                        doMoveFiles = false;
                        doUpdateFiles = true;
                    }
                    else if (aPar == "M")
                    {
                        doMoveFiles = true;
                        doUpdateFiles = false;
                    }
                    else
                    {
                        doMoveFiles = false;
                        doUpdateFiles = false;
                    }
                }

                // V - set multivolume mode and volume size
                if (cPar[0].ToString() == "V" || cPar[0].ToString() == "S")
                {
                    string vPar = cPar.Remove(0, 1);
                    switch (vPar)
                    {
                        case "vm720":
                            {
                                zVolSize = 720000;
                                break;
                            }
                        case "vm880":
                            {
                                zVolSize = 880000;
                                break;
                            }
                        case "vm1440":
                            {
                                zVolSize = 1440000;
                                break;
                            }
                        case "vm2880":
                            {
                                zVolSize = 2880000;
                                break;
                            }
                        case "vmEmail":
                            {
                                zVolSize = 250000;
                                break;
                            }
                        case "vmWeb":
                            {
                                zVolSize = 750000;
                                break;
                            }
                        case "vmCDR650":
                            {
                                zVolSize = 650000000;
                                break;
                            }
                        case "vmCDR700":
                            {
                                zVolSize = 700000000;
                                break;
                            }
                        case "vmCDR800":
                            {
                                zVolSize = 800000000;
                                break;
                            }
                        default:
                            {
                                zVolSize = 0;
                                break;
                            }
                    }
                }

                // D - add files with these parameters (must be tested)

                // R - Set recursion active/inactive
                if (cPar[0].ToString() == "R")
                {
                    string rPar = cPar.Remove(0, 1);
                    if (rPar.ToString() == "0")
                    {
                        doRecursive = false;
                    }
                    else if (rPar.ToString() == "1")
                    {
                        doRecursive = true;
                    }
                }

                // F - set how to store paths
                if (cPar[0].ToString() == "F")
                {
                    string fPar = cPar.Remove(0, 1);
                    switch (fPar)
                    {
                        case "0":
                            {
                                PathMode = PathStoreMode.NoPath;
                                break;
                            }
                        case "1":
                            {
                                PathMode = PathStoreMode.RelativeFolder;
                                break;
                            }
                        case "2":
                        case "3":
                            {
                                PathMode = PathStoreMode.FullPathNoDrive;
                                break;
                            }
                        //case "3":
                        //    {
                        //        PathMode = PathStoreMode.FullPathWithDrive;
                        //        break;
                        //    }
                        default:
                            {
                                PathMode = PathStoreMode.NoPath;
                                break;
                            }
                    }
                }

                // I - remove "A" attribute from file in archive
                //if (cPar[0].ToString() == "I")
                //{
                //    string iPar = cPar.Remove(0, 1);
                //    switch (iPar)
                //    {
                //        case "0":
                //            {
                //                DoRemoveArchiveAttr = false;
                //                cmdVerbose += "I0 -> The Archive file attribute will be maintained in files" + Environment.NewLine; ;
                //                break;
                //            }
                //        case "1":
                //            {
                //                DoRemoveArchiveAttr = true;
                //                cmdVerbose += "I1 -> The Archive file attribute will be removed from files" + Environment.NewLine; ;
                //                break;
                //            }
                //        default:
                //            {
                //                DoRemoveArchiveAttr = false;
                //                cmdVerbose += "The Archive file attribute will be removed from files by default because of an invalid parameter (" + cPar + ")" + Environment.NewLine; ;
                //                break;
                //            }
                //    }
                //}
            }

            cmdVerbose += "For unspecified parameters, the default settings will apply." + Environment.NewLine;
            if (IsVerbose)
                Console.WriteLine(cmdVerbose);

            //msgVerbose = "There will be " + fInclusion.Count.ToString() + " inclusion(s) and " + fExclusion.Count.ToString() + " exclusion(s).";
            //Console.WriteLine(msgVerbose);
            string SelCriteria = string.Empty;


            SevenZip.SevenZipBase.SetLibraryPath(LibPath);
            SevenZip.SevenZipCompressor compressor = new SevenZip.SevenZipCompressor();
            compressor.ArchiveFormat = SevenZip.OutArchiveFormat.Tar;
            compressor.CompressionLevel = zLevel;
            //compressor.ZipEncryptionMethod = zEncAlgo;
            compressor.IncludeEmptyDirectories = true;
            compressor.VolumeSize = (long)zVolSize;
            compressor.FileCompressionFinished += Compressor_FileCompressionFinished1;
            // compressor.FileCompressionStarted += Compressor_FileCompressionStarted;
            compressor.Compressing += Compressor_Compressing;
            // compressor.FilesFound += Compressor_FilesFound;
            filesDictionary = new Dictionary<string, string>();
            dirsDictionary = new Dictionary<string, string>();
            hashDictionary = new Dictionary<string, string>();
            foreach (var entry in fInclusion)
            {
                string pFilter = string.Empty;
                string fFilename = Path.GetFileName(entry);
                if (fFilename.Contains("*") || fFilename.Contains("?"))
                {
                    string dPath = Path.GetDirectoryName(entry);
                    if (string.IsNullOrEmpty(dPath))
                        dPath = Directory.GetCurrentDirectory();
                    SearchOption sOpt = SearchOption.TopDirectoryOnly;
                    if (doRecursive)
                    {
                        Console.WriteLine("Do Recursive!");
                        sOpt = SearchOption.AllDirectories;
                    }

                    AddDirsToDictionary(filesDictionary, dPath, fFilename, "", sOpt, PathMode);
                }
                else
                    AddFileToDictionary(filesDictionary, entry);
            }
            var options = new ProgressBarOptions
            {
                EnableTaskBarProgress = IsWindows(),
                ShowEstimatedDuration = false,
                CollapseWhenFinished = false,
                PercentageFormat = "{0:N0}% ",
                //BackgroundColor = ConsoleColor.White,
                ForegroundColor = ConsoleColor.Cyan,
                ForegroundColorDone = ConsoleColor.Green,
                ProgressBarOnBottom = true
            };
            if (!IsWindows())
                options.ProgressCharacter = '-';
            pBar = new ProgressBar(100, GetLocalizedString("Compressing", "Compressing..."), options);

            compressor.CompressFileDictionary(filesDictionary, arcFile);

            return ExitCode.Success;

            //using (FileStream fStream = new FileStream(arcFile, FileMode.OpenOrCreate, FileAccess.Write))
            //using (ZipOutputStream oStream = new ZipOutputStream(fStream))
            //{
            //    byte[] buffer = new byte[4096];
            //    oStream.SetLevel(0); // 0 - store only to 9 - means best compression
            //    foreach (var entry in filesDictionary)
            //    {
            //        Console.WriteLine("<> " + entry.Key + " = " + entry.Value);
            //        ZipEntry zEntry = new ZipEntry(entry.Key);
            //        oStream.PutNextEntry(zEntry);
            //        using (FileStream fs = File.OpenRead(entry.Value))
            //        {
            //            StreamUtils.Copy(fs, oStream, buffer);
            //        }
            //    }
            //}

        }

        private static void Compressor_FileCompressionFinished1(object? sender, EventArgs e)
        {

        }

        private static void Compressor_FileCompressionFinished(object? sender, EventArgs e)
        {

        }

        private static void Compressor_Compressing(object? sender, SevenZip.ProgressEventArgs e)
        {
            pBar.AsProgress<byte>().Report(e.PercentDone);


        }

        private static void Compressor_FileCompressionStarted(object? sender, SevenZip.FileNameEventArgs e)
        {
            pBar.Message = e.FileName;

            //Console.WriteLine("Compression started");

            //ProgressUtils.WriteProgressBar(Convert.ToInt32(e.PercentDone));
            //var cPos = Console.GetCursorPosition();
            //Console.CursorVisible = false;
            //Console.SetCursorPosition(1, cPos.Top - 1);
            //Console.WriteLine();
        }
        private static void AddEmptyDirsToDictionary(Dictionary<string, string> dirDictionary, string pathToDirectory, string filter = "*.*", SearchOption sOpt = SearchOption.TopDirectoryOnly, PathStoreMode pathMode = PathStoreMode.NoPath)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(pathToDirectory);
            DirectoryInfo[] dirInfos = dirInfo.GetDirectories(filter, sOpt);

            foreach (DirectoryInfo di in dirInfos)
            {
                string NewPath = string.Empty;
                switch (pathMode)
                {
                    case PathStoreMode.NoPath:
                        {
                            NewPath = di.FullName.Replace(pathToDirectory + "\\", "");
                            break;
                        }
                    case PathStoreMode.RelativeFolder:
                        {
                            NewPath = di.FullName.Replace(dirInfo.Parent.FullName + "\\", "");
                            break;
                        }
                    case PathStoreMode.FullPathNoDrive:
                        {
                            //Console.WriteLine("> Root: " + dirInfo.Root);
                            NewPath = di.FullName.Replace(dirInfo.Root.ToString(), "");
                            break;
                        }
                    case PathStoreMode.FullPathWithDrive:
                        {
                            NewPath = di.FullName;
                            break;
                        }
                }
                dirDictionary.Add(NewPath, di.FullName);
            }
        }

        private static void AddDirsToDictionary(Dictionary<string, string> filesDictionary, string pathToDirectory, string parentFolder = "", string filter = "*.*", SearchOption sOpt = SearchOption.TopDirectoryOnly, PathStoreMode pathMode = PathStoreMode.NoPath, string arcFile = "")
        {

            string DirPath = pathToDirectory;
            //if (HasEnvironmentVariable(pathToDirectory))
            //    DirPath = Environment.ExpandEnvironmentVariables(pathToDirectory);
            string dirParent = Directory.GetParent(DirPath).FullName;

            IEnumerable<string> dirs = Directory.EnumerateDirectories(DirPath, filter, sOpt);
            IEnumerable<string> files = Directory.EnumerateFiles(DirPath, filter, sOpt);

            string NewRootPath = DirPath.Replace(dirParent + Path.DirectorySeparatorChar, "") + Path.DirectorySeparatorChar;
            foreach (string dir in dirs)
            {
                switch (pathMode)
                {
                    case PathStoreMode.NoPath:
                        {
                            //NewPathDir = di.FullName.Replace(pathToDirectory + "\\", "");
                            string NewPathDir = "";
                            break;
                        }
                    case PathStoreMode.RelativeFolder:
                        {
                            //string cNewPath = fi.FullName.Replace(dirInfo.Root.ToString(), "");
                            //NewPathDir = di.FullName.Replace(dirInfo.Parent.FullName + Path.DirectorySeparatorChar, "");
                            string NewPathDir = dir.Replace(dirParent + Path.DirectorySeparatorChar, "") + Path.DirectorySeparatorChar;
                            dirsDictionary.Add(NewPathDir, dir);

                            break;
                        }
                    case PathStoreMode.FullPathWithDrive:
                    case PathStoreMode.FullPathNoDrive:
                        {
                            //Console.WriteLine("> Root: " + dirInfo.Root);
                            //NewPathDir = di.FullName.Replace(dirInfo.Root.ToString(), "");
                            string NewPathDir = dir.Replace(Directory.GetDirectoryRoot(dir) + Path.DirectorySeparatorChar, "") + Path.DirectorySeparatorChar;
                            break;
                        }
                }
            }
            dirsDictionary.Add(NewRootPath, DirPath);
            foreach (string nFile in files)
            {
                switch (pathMode)
                {
                    case PathStoreMode.NoPath:
                        {
                            string NewPath = Path.GetFileName(nFile);
                            break;
                        }
                    case PathStoreMode.RelativeFolder:
                        {
                            string NewPath = Path.Combine(Path.GetDirectoryName(nFile).Replace(dirParent + Path.DirectorySeparatorChar, ""), Path.GetFileName(nFile));
                            filesDictionary.Add(NewPath, nFile);
                            if (!string.IsNullOrEmpty(NewPath))
                                hashDictionary.Add(NewPath, CryptoUtils.Sha256FileHash(nFile));
                            break;
                        }
                    case PathStoreMode.FullPathWithDrive:
                    case PathStoreMode.FullPathNoDrive:
                        {
                            //Console.WriteLine("> Root: " + dirInfo.Root);
                            //NewPath = fi.FullName.Replace(dirInfo.Root.ToString(), "");
                            //NewPath = Path.GetDirectoryName(nFile).Replace(Directory.GetDirectoryRoot(nFile), "");
                            break;
                        }
                }

            }

            #region Debug Code
            //List<string> cEntries = new List<string>();
            //foreach (var xEntry in dirsDictionary)
            //    cEntries.Add(xEntry.Key + Environment.NewLine + "---> " +
            //        xEntry.Value);
            //cEntries.Add("-------------------");
            //foreach (var xEntry in filesDictionary)
            //    cEntries.Add(xEntry.Key + Environment.NewLine + "---> " +
            //        xEntry.Value);
            //Console.WriteLine(": "+arcFile);
            //string fName = Path.Combine(
            //    Path.GetDirectoryName(arcFile),
            //    "Dict_" + Path.GetFileNameWithoutExtension(arcFile) + ".txt");
            //File.WriteAllLines(fName, cEntries.ToArray());

            //List<string> cEntries = new List<string>();
            //foreach (var xEntry in zip.Entries)
            //    cEntries.Add(xEntry.FileName);
            //string fName = Path.Combine(
            //    Path.GetDirectoryName(zip.Name),
            //    "Dict_" + Path.GetFileNameWithoutExtension(zip.Name) + ".txt");
            //File.WriteAllLines(fName, cEntries.ToArray()); 
            #endregion
        }

        private static void AddFileToDictionary(Dictionary<string, string> filesDictionary, string nFile)
        {
            string cFile = string.Empty;
            if (HasEnvironmentVariable(nFile))
                cFile = Environment.ExpandEnvironmentVariables(nFile);
            else
                cFile = nFile;
            filesDictionary.Add(Path.GetFileName(cFile), cFile);
            hashDictionary.Add(nFile, CryptoUtils.Sha256FileHash(nFile));
        }

        //public static void ZipFolder(string RootFolder, string CurrentFolder, ZipOutputStream zStream, bool UseRecursion=false, string filter="*.*")
        //{
        //    if (UseRecursion)
        //    {
        //        string[] SubFolders = Directory.GetDirectories(CurrentFolder);

        //        foreach (string Folder in SubFolders)
        //            ZipFolder(RootFolder, Folder, zStream, UseRecursion, filter);
        //    }

        //    string relativePath = CurrentFolder.Substring(RootFolder.Length) + "/";

        //    if (relativePath.Length > 1)
        //    {
        //        ZipEntry dirEntry;

        //        dirEntry = new ZipEntry(relativePath);
        //        dirEntry.DateTime = DateTime.Now;
        //    }
        //    EnumerationOptions enumOptions = new EnumerationOptions();

        //    foreach (string file in Directory.GetFiles(CurrentFolder,filter))
        //    {
        //        if (filter != "*.*" && (Path.GetExtension(file) != filter))
        //            continue;
        //        else
        //            AddFileToZip(zStream, relativePath, file);
        //    }
        //}

        //private static void AddFileToZip(ZipOutputStream zStream, string relativePath, string file)
        //{
        //    byte[] buffer = new byte[4096];
        //    string fileRelativePath = (relativePath.Length > 1 ? relativePath : string.Empty) + Path.GetFileName(file);

        //    string ext = Path.GetExtension(file);
        //    Console.WriteLine("# " + ext);
        //    bool IsBanned = fExclusionExt.Contains(ext) || fExclusion.Contains(Path.GetFileName(file));
        //    if (IsBanned)
        //    {
        //        Console.WriteLine("Banned!");
        //        goto DoSkip;
        //    }

        //    ZipEntry entry = new ZipEntry(fileRelativePath);

        //    entry.DateTime = DateTime.Now;
        //    zStream.PutNextEntry(entry);

        //    using (FileStream fs = File.OpenRead(file))
        //    {
        //        int sourceBytes;

        //        do
        //        {
        //            sourceBytes = fs.Read(buffer, 0, buffer.Length);
        //            zStream.Write(buffer, 0, sourceBytes);
        //        } while (sourceBytes > 0);
        //    }
        //DoSkip:
        //    return;
        //}

#if DEBUG
        public static void TestProgress()
        {
            //ProgressUtils.WriteProgressBar(0);
            var options = new ProgressBarOptions
            {
                EnableTaskBarProgress = IsWindows(),
                ShowEstimatedDuration = true,
                CollapseWhenFinished = false,
                PercentageFormat = "{0:N0}% ",

                //BackgroundColor = ConsoleColor.White,
                ForegroundColor = ConsoleColor.Blue,
                ForegroundColorDone = ConsoleColor.Green,
                ProgressBarOnBottom = false
            };
            if (!IsWindows())
                options.ProgressCharacter = '-';
            ProgressBar pBar = new ProgressBar(100, "aaa", options);
            for (int i = 0; i < 100; i++)
            {
                pBar.Tick(i);
                Thread.Sleep(500);
            }


            //for (var i = 0; i <= 100; ++i)
            //{
            //    ProgressUtils.WriteProgressBar(i, true);
            //    Thread.Sleep(50);
            //}
            //Console.WriteLine();

        }
#endif
        public static void WaitInput(string? msg = null)
        {
            msg ??= GetLocalizedString("PauseQuit", "Press any key to quit app.");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(Environment.NewLine + msg);
            Console.ResetColor();
            ConsoleKeyInfo keyinfo;
            do
            {
                keyinfo = Console.ReadKey(true);
            }

            while (keyinfo.Key == null);
        }

        public static string FormatSizeEx(double fileSize, bool Thousands = false)
        {
            string[] sizeSuffixes = {
            "Bytes",
            "KiloBytes",
            "MegaBytes",
            "GigaBytes",
            "TeraBytes",
            "PetaBytes",
            "ExaBytes",
            "ZetaBytes",
            "YottaBytes"};
            string[] sizeSuffixesSingle = {
            "Byte",
            "KiloByte",
            "MegaByte",
            "GigaByte",
            "TeraByte",
            "PetaByte",
            "ExaByte",
            "ZetaByte",
            "YottaByte"};

            int suffixIndex = 0;

            while (fileSize >= 1024 && suffixIndex < sizeSuffixes.Length - 1)
            {
                fileSize /= 1024;
                suffixIndex++;
            }
            string formattedSize = "";
            if (fileSize == 1)
                formattedSize = string.Format("{0:#,0.##} {1}", fileSize, sizeSuffixesSingle[suffixIndex]);
            else
                formattedSize = string.Format("{0:#,0.##} {1}", fileSize, sizeSuffixes[suffixIndex]);
            return formattedSize;
        }
        public static string FormatSize(long bytes)
        {
            if (bytes == 0)
                return string.Empty;
            else
            {
                string[] suffixes = {
                    "bytes", "KB", "MB", "GB", "TB", "PB"
                //StringUtils.GetResString("commonBytes"),
                //StringUtils.GetResString("commonKB"),
                //StringUtils.GetResString("commonMB"),
                //StringUtils.GetResString("commonGB"),
                //StringUtils.GetResString("commonTB"),
                //StringUtils.GetResString("commonPB")
            };

                int counter = 0;
                decimal number = (decimal)bytes;
                while (Math.Round(number / 1024) >= 1)
                {
                    number = number / 1024;
                    counter++;
                }
                return string.Format("{0:n1} {1}", number, suffixes[counter]);
            }
        }

        private sealed class ArchiveListExportEntry
        {
            public string Name { get; init; } = string.Empty;
            public DateTime LastWriteTime { get; init; }
            public ulong PackedSize { get; init; }
            public ulong Size { get; init; }
            public uint Crc32 { get; init; }
        }

        public static ExitCode ExportArchiveList(string[] args)
        {
            if (args.Length < 4)
            {
                WriteLocalizedError("ExportMissingArguments", "Syntax: zg -export archive HTML|CSV|XML|TXT output-folder [--overwrite]");
                return ExitCode.CompressionError;
            }

            string archivePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1].Trim('"')));
            if (!File.Exists(archivePath))
            {
                WriteLocalizedError("ExportSourceNotFound", "Archive not found: {0}", archivePath);
                return ExitCode.FileDoesNotExists;
            }

            string format = args[2].Trim().TrimStart('-', '.').ToUpperInvariant();
            string? extension = format switch
            {
                "HTML" or "HTM" => ".html",
                "CSV" => ".csv",
                "XML" => ".xml",
                "TXT" => ".txt",
                _ => null
            };
            if (extension is null)
            {
                WriteLocalizedError("ExportUnsupportedFormat", "Unsupported export format: {0}", args[2]);
                return ExitCode.CompressionError;
            }

            string outputFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[3].Trim('"')));
            string outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(archivePath) + extension);
            bool overwrite = args.Any(argument => argument.Equals("--overwrite", StringComparison.OrdinalIgnoreCase));
            if (File.Exists(outputPath) && !overwrite)
            {
                WriteLocalizedError("ExportDestinationExists", "Exported file already exists. Use --overwrite to replace it: {0}", outputPath);
                return ExitCode.CompressionError;
            }

            try
            {
                var entries = new List<ArchiveListExportEntry>();
                using (ArchiveFile archive = new ArchiveFile(archivePath, FileUtils.GetSevenZipLibs()))
                {
                    foreach (SevenZipExtractor.Entry entry in archive.Entries.Where(entry => !entry.IsFolder))
                    {
                        entries.Add(new ArchiveListExportEntry
                        {
                            Name = entry.FileName,
                            LastWriteTime = entry.LastWriteTime,
                            PackedSize = entry.PackedSize,
                            Size = entry.Size,
                            Crc32 = entry.CRC
                        });
                    }
                }

                if (entries.Count == 0)
                {
                    WriteLocalizedError("ExportEmptyArchive", "The archive contains no files to export: {0}", archivePath);
                    return ExitCode.ZeroItemsArchive;
                }

                Directory.CreateDirectory(outputFolder);
                using FileStream stream = new FileStream(outputPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                switch (format)
                {
                    case "HTML":
                    case "HTM":
                        WriteArchiveListHtml(writer, archivePath, entries);
                        break;
                    case "CSV":
                        WriteArchiveListCsv(writer, entries);
                        break;
                    case "XML":
                        writer.Flush();
                        WriteArchiveListXml(stream, archivePath, entries);
                        break;
                    default:
                        WriteArchiveListText(writer, archivePath, entries);
                        break;
                }

                if (!IsSilent)
                    Console.WriteLine(GetLocalizedString("ExportCompleted", "Exported {0} entries to {1}", entries.Count, outputPath));
                return ExitCode.Success;
            }
            catch (IOException ex)
            {
                WriteLocalizedError("ExportFailed", "Could not export {0}: {1}", archivePath, ex.Message);
                return ExitCode.IOError;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLocalizedError("ExportFailed", "Could not export {0}: {1}", archivePath, ex.Message);
                return ExitCode.IOError;
            }
            catch (Exception ex)
            {
                WriteLocalizedError("ExportFailed", "Could not export {0}: {1}", archivePath, ex.Message);
                return ExitCode.UnknownError;
            }
        }

        private static void WriteArchiveListCsv(TextWriter writer, IEnumerable<ArchiveListExportEntry> entries)
        {
            writer.WriteLine("Name,DateTime,PackedSize,Size,Ratio,CRC-32");
            foreach (ArchiveListExportEntry entry in entries)
            {
                double ratio = GetExportCompressionRatio(entry);
                writer.WriteLine(string.Join(",", new[]
                {
                    EscapeCsv(entry.Name),
                    EscapeCsv(entry.LastWriteTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                    entry.PackedSize.ToString(CultureInfo.InvariantCulture),
                    entry.Size.ToString(CultureInfo.InvariantCulture),
                    ratio.ToString("P2", CultureInfo.InvariantCulture),
                    entry.Crc32.ToString("X8", CultureInfo.InvariantCulture)
                }));
            }
        }

        private static void WriteArchiveListText(TextWriter writer, string archivePath, IEnumerable<ArchiveListExportEntry> entries)
        {
            writer.WriteLine("Archive: " + archivePath);
            writer.WriteLine();
            foreach (ArchiveListExportEntry entry in entries)
            {
                writer.WriteLine("Name: " + entry.Name);
                writer.WriteLine("Date/Time: " + entry.LastWriteTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteLine("Packed size: " + entry.PackedSize.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Size: " + entry.Size.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("CRC-32: " + entry.Crc32.ToString("X8", CultureInfo.InvariantCulture));
                writer.WriteLine();
            }
        }

        private static void WriteArchiveListHtml(TextWriter writer, string archivePath, IEnumerable<ArchiveListExportEntry> entries)
        {
            writer.WriteLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Archive file list</title><style>body{font-family:Segoe UI,Arial,sans-serif;margin:2rem}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:.4rem;text-align:left}th{background:#eee}td.num{text-align:right}</style></head><body>");
            writer.WriteLine("<h1>Archive file list</h1><p>" + System.Net.WebUtility.HtmlEncode(archivePath) + "</p><table><thead><tr><th>Name</th><th>Date/Time</th><th>Packed size</th><th>Size</th><th>Ratio</th><th>CRC-32</th></tr></thead><tbody>");
            foreach (ArchiveListExportEntry entry in entries)
            {
                double ratio = GetExportCompressionRatio(entry);
                writer.WriteLine("<tr><td>" + System.Net.WebUtility.HtmlEncode(entry.Name) + "</td><td>" + entry.LastWriteTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) + "</td><td class=\"num\">" + entry.PackedSize.ToString("N0", CultureInfo.InvariantCulture) + "</td><td class=\"num\">" + entry.Size.ToString("N0", CultureInfo.InvariantCulture) + "</td><td class=\"num\">" + ratio.ToString("P2", CultureInfo.InvariantCulture) + "</td><td>" + entry.Crc32.ToString("X8", CultureInfo.InvariantCulture) + "</td></tr>");
            }
            writer.WriteLine("</tbody></table></body></html>");
        }

        private static void WriteArchiveListXml(Stream stream, string archivePath, IEnumerable<ArchiveListExportEntry> entries)
        {
            using XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false });
            writer.WriteStartDocument();
            writer.WriteStartElement("archiveFileList");
            writer.WriteAttributeString("archive", Path.GetFileName(archivePath));
            foreach (ArchiveListExportEntry entry in entries)
            {
                writer.WriteStartElement("file");
                writer.WriteElementString("name", entry.Name);
                writer.WriteElementString("dateTime", entry.LastWriteTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteElementString("packedSize", entry.PackedSize.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("size", entry.Size.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("crc32", entry.Crc32.ToString("X8", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static string EscapeCsv(string value)
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        private static double GetExportCompressionRatio(ArchiveListExportEntry entry)
        {
            ulong largestSize = Math.Max(entry.PackedSize, entry.Size);
            return largestSize == 0 ? 0 : (double)(largestSize - Math.Min(entry.PackedSize, entry.Size)) / largestSize;
        }

        private static List<ArchiveListExportEntry> ReadArchiveList(string archivePath, out int entryCount)
        {
            var entries = new List<ArchiveListExportEntry>();
            // The native SevenZip wrappers depend on Windows APIs. Use the managed
            // reader on other platforms so listing does not require a native handle.
            if (!OperatingSystem.IsWindows())
            {
                using IArchive archive = ArchiveFactory.Open(archivePath);
                var archiveEntries = archive.Entries.ToList();
                entryCount = archiveEntries.Count;
                foreach (var entry in archiveEntries.Where(entry => !entry.IsDirectory))
                {
                    entries.Add(new ArchiveListExportEntry
                    {
                        Name = entry.Key ?? string.Empty,
                        LastWriteTime = entry.LastModifiedTime ?? DateTime.MinValue,
                        PackedSize = (ulong)Math.Max(0, entry.CompressedSize),
                        Size = (ulong)Math.Max(0, entry.Size),
                        Crc32 = unchecked((uint)entry.Crc)
                    });
                }
            }
            else
            {
                using var archive = new ArchiveFile(archivePath, FileUtils.GetSevenZipLibs());
                entryCount = archive.Entries.Count;
                foreach (var entry in archive.Entries.Where(entry => !entry.IsFolder))
                {
                    entries.Add(new ArchiveListExportEntry
                    {
                        Name = entry.FileName,
                        LastWriteTime = entry.LastWriteTime,
                        PackedSize = entry.PackedSize,
                        Size = entry.Size,
                        Crc32 = entry.CRC
                    });
                }
            }
            return entries;
        }

        private static void WriteVerboseArchiveTable(IEnumerable<ArchiveListExportEntry> entries, int terminalWidth)
        {
            // Keep each rendered line narrower than the terminal to avoid auto-wrap.
            int lineWidth = Math.Max(32, terminalWidth - 1);
            const int separatorWidth = 3;
            int packedWidth = Math.Clamp(entries.Select(entry => entry.PackedSize.ToString("N0").Length).DefaultIfEmpty(6).Max(), 6, 12);
            int sizeWidth = Math.Clamp(entries.Select(entry => entry.Size.ToString("N0").Length).DefaultIfEmpty(4).Max(), 4, 12);
            const int ratioWidth = 6;
            const int crcWidth = 8;
            int nameWidth = Math.Max(8, lineWidth - separatorWidth * 4 - packedWidth - sizeWidth - ratioWidth - crcWidth);

            string Clip(string value, int width)
            {
                if (value.Length <= width)
                    return value.PadRight(width);
                return width == 1 ? "…" : value[..(width - 1)] + "…";
            }

            string Right(string value, int width) => value.Length > width ? value[^width..] : value.PadLeft(width);
            string Header = string.Join(" | ", Clip("File", nameWidth), Right("Packed", packedWidth), Right("Size", sizeWidth), Right("Ratio", ratioWidth), Right("CRC-32", crcWidth));
            Console.WriteLine(Header);
            Console.WriteLine(new string('-', Math.Min(lineWidth, Header.Length)));
            foreach (ArchiveListExportEntry entry in entries)
            {
                string fileName = Path.GetFileName(entry.Name.TrimEnd('/', '\\'));
                string directory = entry.Name[..Math.Max(0, entry.Name.Length - fileName.Length)].TrimEnd('/', '\\');
                string name = Clip(fileName, nameWidth);
                double compressionRatio = GetExportCompressionRatio(entry);
                string details = string.Join(" | ", name, Right(entry.PackedSize.ToString("N0"), packedWidth), Right(entry.Size.ToString("N0"), sizeWidth), Right(compressionRatio.ToString("P1"), ratioWidth), Right(entry.Crc32.ToString("X8"), crcWidth));
                Console.WriteLine(details);

                if (!string.IsNullOrEmpty(directory))
                {
                    string path = Clip(directory, lineWidth);
                    Console.WriteLine("\u001b[3m" + path.TrimEnd() + "\u001b[0m");
                }
                Console.WriteLine(new string('-', Math.Min(lineWidth, details.Length)));
            }
        }

        public static ExitCode OpenArchive(string[] args)
        {
            if (args.Length < 2 || !File.Exists(args[1])){
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable to find the archive to list.");
                Console.ResetColor();
                return ExitCode.FileDoesNotExists;
            }
            var safety = CheckZipBomb(args[1], listing: true);
            try
            {
                var entries = ReadArchiveList(args[1], out int entryCount);
                if (entryCount<=0){
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Unable to read file "+args[1]);
                    Console.ResetColor();
                    return safety.ExitCode != 0 ? (ExitCode)safety.ExitCode : ExitCode.ZeroItemsArchive;
                }
                FileInfo info = new FileInfo(args[1]);
                Console.WriteLine("\nContents of " + args[1]);
                if (IsVerbose)
                {
                    ulong totalSize = entries.Aggregate(0UL, (total, entry) => total + entry.Size);
                    ulong totalPackedSize = entries.Aggregate(0UL, (total, entry) => total + entry.PackedSize);
                    double totalRatio = GetExportCompressionRatio(new ArchiveListExportEntry
                    {
                        Size = totalSize,
                        PackedSize = totalPackedSize
                    });

                    Console.WriteLine(new string('─', 80));
                    Console.WriteLine(GetLocalizedString("ListStats", "Files: {0:N0}    Folders: {1:N0}    Archive size: {2:N0} bytes ({3})", entries.Count, entryCount - entries.Count, info.Length, FormatSizeEx(info.Length, true)));
                    Console.WriteLine(GetLocalizedString("ListSizes", "Unpacked: {0:N0} bytes    Packed: {1:N0} bytes    Compression: {2:P1}", totalSize, totalPackedSize, totalRatio));
                    Console.WriteLine(GetLocalizedString("ListModified", "Modified: {0:yyyy-MM-dd HH:mm:ss}", info.LastWriteTime));
                    Console.WriteLine();
                }
                if (IsVerbose)
                {
                    int terminalWidth;
                    try
                    {
                        terminalWidth = Console.WindowWidth;
                    }
                    catch (IOException)
                    {
                        terminalWidth = 100;
                    }
                    WriteVerboseArchiveTable(entries, terminalWidth);
                }
                else
                    foreach (ArchiveListExportEntry entry in entries)
                        Console.WriteLine(entry.Name);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable to read file " + args[1] + ": " + ex.Message);
                Console.ResetColor();
                return safety.ExitCode != 0 ? (ExitCode)safety.ExitCode : ExitCode.UnknownError;
            }

            return (ExitCode)safety.ExitCode;
        }

        public static async Task<int> OpenArchiveOld(string[] args)
        {
            if (IsZipFile(Path.GetExtension(args[1])))
            {
                ListZip(args[1]);
            }
            else if (IsTarFile(args[1]))
            {
                ListTar(args[1]);
            }
            else if (Is7zFile(args[1]))
            {
                string LibPath = FileUtils.GetSevenZipLibs();
                SevenZip.SevenZipBase.SetLibraryPath(LibPath);
                Console.WriteLine(args[1]);
                string ext = Path.GetExtension(args[1]).ToLower();
                /*
                            SevenZipExtractor extractor = new SevenZipExtractor(args[1]);
                            switch (ext)
                            {
                                case ".dmg":
                                    {
                                        extractor = new SevenZipExtractor(args[1], InArchiveFormat.Dmg);
                                        break;
                                    }
                                case ".rpm":
                                    {
                                        extractor = new SevenZipExtractor(args[1], InArchiveFormat.Rpm);
                                        break;
                                    }
                                case ".mssi":
                                    {
                                        extractor = new SevenZipExtractor(args[1], InArchiveFormat.Msi);
                                        break;
                                    }
                                default:
                                    {
                                        extractor = new SevenZipExtractor(args[1]);
                                        Console.WriteLine("Generic extractor");
                                        break;
                                    }
                            }

                            if (Path.GetExtension(args[1].ToLower()) == ".dmg")
                            {
                                var hfsFile = extractor.ArchiveFileNames.FirstOrDefault(x => x.ToLower().Contains("hfs"));
                                if (hfsFile == null)
                                    return (int)ExitCode.UnknownError;
                                string dmgHfsFile = Path.Combine(Path.GetTempPath(), hfsFile);
                                Console.WriteLine(dmgHfsFile);
                                using (FileStream tStream = new FileStream(dmgHfsFile, FileMode.Create, FileAccess.Write))
                                {
                                    extractor.ExtractFile(hfsFile, tStream);
                                    SevenZipExtractor extrDmg = new SevenZipExtractor(dmgHfsFile, InArchiveFormat.Hfs);
                                    foreach (string dEntry in extrDmg.ArchiveFileNames)
                                    {
                                        Console.WriteLine(dEntry);
                                    }
                                }
                                //File.Delete(dmgHfsFile);

                            }
                            else
                            {
                                Console.WriteLine("Not a DMG");
                                foreach (string entry in extractor.ArchiveFileNames)
                                {
                                    Console.WriteLine(entry);
                                }
                            }
                            */
            }
            else if (Path.GetExtension(args[1].ToLower()) == ".crx")
            {
                ListCrx(args[1]);
            }
            else if (ExoticLike.Contains(Path.GetExtension(args[1]).ToLower()))
            {
                Console.WriteLine("Reading " + args[1] + "...");
                using var archive = ArchiveFactory.Open(args[1]);
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        Console.WriteLine(entry.Key);
                    }
                }
                //ReaderOptions opt = new ReaderOptions();
                //opt.LookForHeader = true;
                //opt.DisableCheckIncomplete = true;

                //using (FileStream stream = new FileStream(args[1], FileMode.Open, FileAccess.Read))
                //using (var reader = ReaderFactory.Open(stream,opt))
                //{
                //    reader.OpenEntryStream();
                //    while (reader.MoveToNextEntry())
                //    {

                //        Console.WriteLine(reader.Entry.Key);
                //        //reader.WriteEntryToDirectory(@"C:\temp", new ExtractionOptions()
                //        //{
                //        //    ExtractFullPath = true,
                //        //    Overwrite = true
                //        //});

                //    }
                //}
            }
            else if (IsIsoFile(args[1]))
            {
                string ext = Path.GetExtension(args[1].ToLower());

                List<string> lFiles = null;
                if (ext == ".iso" || ext == ".c2d")
                {
                    using (FileStream isoStream = File.Open(args[1], FileMode.Open, FileAccess.Read))
                    {
                        CDReader cd = new CDReader(isoStream, false);

                        lFiles = cd.GetFiles(cd.Root.Name, "*.*", SearchOption.AllDirectories).ToList<string>();
                        foreach (string entry in lFiles)
                        {
                            Console.WriteLine(entry);
                        }
                    }

                }
                else if (ext == ".vdi")
                {
                    //using (FileStream inStream = new FileStream(args[1], FileMode.Open, FileAccess.Read, FileShare.None))
                    //{
                    Console.WriteLine("Reading .vdi...");
                    var disk = new DiscUtils.Vdi.Disk(args[1], FileAccess.Read);
                    DiscUtils.FileSystems.SetupHelper.SetupFileSystems();

                    var manager = new VolumeManager(disk);
                    var logicalVolumes = manager.GetLogicalVolumes();
                    foreach (var volume in logicalVolumes)
                    {
                        Console.WriteLine("Volume " + volume.Identity);
                        var fsInfos = FileSystemManager.DetectFileSystems(volume);

                        foreach (var fsInfo in fsInfos)
                        {
                            Console.WriteLine(fsInfo.Name + ": " + fsInfo.Description);

                            using var fs = fsInfo.Open(disk.Content);
                            List<string> array2 = fs.GetFiles(fs.Root.Name, "*.*", SearchOption.AllDirectories).ToList<string>();
                            foreach (string entry in array2)
                            {
                                Console.WriteLine(entry);
                            }
                            //foreach (var file in fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories))
                            //{
                            //    Console.WriteLine(file);
                            //}
                        }
                    }
                    //}

                }
                else if (ext == ".vhd")
                {
                    Console.WriteLine("Reading .vhd...");
                    var disk = new DiscUtils.Vhd.Disk(args[1], FileAccess.Read);
                    DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
                    var manager = new VolumeManager(disk);
                    var logicalVolumes = manager.GetLogicalVolumes();
                    foreach (var volume in logicalVolumes)
                    {
                        Console.WriteLine("Volume " + volume.Identity);
                        var fsInfos = FileSystemManager.DetectFileSystems(volume);

                        Console.WriteLine(">> " + fsInfos.ToList().Count().ToString());
                        foreach (var fsInfo in fsInfos)
                        {
                            Console.WriteLine(fsInfo.Name + ": " + fsInfo.Description);

                            using var fs = fsInfo.Open(volume);
                            List<string> array2 = fs.GetDirectories(fs.Root.Name).ToList<string>();
                            foreach (string dirEntry in array2)
                            {
                                foreach (string entry in fs.GetFiles(dirEntry).ToList<string>())
                                {
                                    Console.WriteLine(entry);
                                }

                            }
                            //Console.WriteLine(fs.GetFiles(fs.Root.FullName).Length.ToString());
                            //foreach (var file in fs.GetFiles(fs.Root.FullName))
                            //{
                            //    Console.WriteLine(file);
                            //}
                        }
                    }
                }
                else if (ext == ".vmdk")
                {
                    Console.WriteLine("Reading .vmdk...");
                    var disk = new DiscUtils.Vmdk.Disk(args[1], FileAccess.Read);
                    DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
                    var manager = new VolumeManager(disk);
                    var logicalVolumes = manager.GetLogicalVolumes();
                    foreach (var volume in logicalVolumes)
                    {
                        Console.WriteLine("Volume " + volume.Identity);
                        var fsInfos = FileSystemManager.DetectFileSystems(volume);
                        foreach (var fsInfo in fsInfos)
                        {
                            Console.WriteLine(fsInfo.Name + ": " + fsInfo.Description);

                            using var fs = fsInfo.Open(volume);
                            List<string> array2 = fs.GetFiles(fs.Root.Name, "*.*", SearchOption.AllDirectories).ToList<string>();
                            foreach (string entry in array2)
                            {
                                Console.WriteLine(entry);
                            }
                            //Console.WriteLine(fs.GetFiles(fs.Root.FullName).Length.ToString());
                            //foreach (var file in fs.GetFiles(fs.Root.FullName))
                            //{
                            //    Console.WriteLine(file);
                            //}
                        }
                    }
                }
                else if (ext == ".bh")
                {
                    var path = args[1];
                    var extractor = new Extractor();
                    foreach (var file in extractor.Extract(path))
                    {
                        Console.WriteLine(file.FullPath);
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable to read the file " + args[1] + ".");
                Console.ResetColor();
            }
            return 0;
        }


        private static int ListCrx(string fName)
        {
            string oName = Path.Combine(Path.GetTempPath(), "temp.zip");
            using (FileStream inStream = new FileStream(fName, FileMode.Open, FileAccess.Read))
            using (FileStream outStream = new FileStream(oName, FileMode.Create, FileAccess.Write))
            {
                MemoryStream ms = new MemoryStream();
                inStream.CopyTo(ms);
                inStream.Position = 0;
                byte[] zipSig = { 0x50, 0x4B, 0x03, 0x04 };
                List<long> zPos = FindPosition(inStream, zipSig, true);
                long zipOffset = zPos[0];
                ms.Position = zipOffset;
                ms.CopyTo(outStream);

                Console.WriteLine("Done");
                ZipFile zip = new ZipFile(oName);
                int i = 0;

                foreach (ZipEntry zEntry in zip)
                {
                    string cEntry = zEntry.FileName.Replace('/', Path.DirectorySeparatorChar);
                    if (IsVerbose)
                    {
                        if (!zEntry.IsDirectory)
                        {
                            Console.WriteLine(cEntry);
                            Console.WriteLine(FormatSize(zEntry.CompressedSize) + " | " + FormatSize(zEntry.UncompressedSize) + " | " +
                            zEntry.LastModified.ToString() + " | " + zEntry.Crc.ToString("X") + " | " + (zEntry.UsesEncryption ? "PWD" : "---") + " | " +
                            zEntry.CompressionMethod.ToString());
                            Console.WriteLine("");

                            /* Console.WriteLine("----------------------------------------------------------------------------");
                            Console.WriteLine("(" + i + "): " + cEntry + " - " + (zEntry.IsDirectory ? "Folder" : "File"));
                            Console.WriteLine("-----> Orig. Size: " + FormatSize(zEntry.UncompressedSize));
                            Console.WriteLine("-----> Packed Size: " + FormatSize(zEntry.CompressedSize));
                            Console.WriteLine("-----> Last modified: " + zEntry.LastModified.ToString());
                            Console.WriteLine("-----> CRC32: " + zEntry.Crc.ToString("X"));
                            Console.WriteLine("-----> Protected: " + (zEntry.UsesEncryption ? "True" : "False"));
                            Console.WriteLine("-----> Compression Method: " + zEntry.CompressionMethod.ToString());
                            Console.WriteLine("----------------------------------------------------------------------------");
                            Console.WriteLine(""); */
                            i++;
                        }
                        else
                        {
                            Console.WriteLine(cEntry);
                            Console.WriteLine("0 | 0 | " +
                            zEntry.LastModified.ToString() + " | 00000000 | " + (zEntry.UsesEncryption ? "PWD" : "---") + " | " +
                            zEntry.CompressionMethod.ToString());
                            Console.WriteLine("");
                            /* Console.WriteLine("----------------------------------------------------------------------------");
                            Console.WriteLine("(" + i + "): " + cEntry + " - " + (zEntry.IsDirectory ? "Folder" : "File"));
                            Console.WriteLine("----------------------------------------------------------------------------");
                            Console.WriteLine(""); */
                            i++;
                        }

                    }
                    else
                    {
                        Console.WriteLine("(" + i + "): " + cEntry);
                        i++;
                    }
                }
                ms.Dispose();

            }


            return 0;
        }

        private static bool IsTarFile(string fName)
        {
            if (fName.ToLower().EndsWith(".tar") ||
                fName.ToLower().EndsWith(".tgz") ||
                fName.ToLower().EndsWith(".txz") ||
                fName.ToLower().EndsWith(".tar.bz2") ||
                fName.ToLower().EndsWith(".tar.z") ||
                fName.ToLower().EndsWith(".tar.gz") ||
                fName.ToLower().EndsWith(".tar.xz")
                )
                return true;
            else
                return false;
        }
        private static bool Is7zFile(string fName)
        {
            return SevenZipLike.Contains(Path.GetExtension(fName.ToLower()));
        }
        private static bool IsIsoFile(string fName)
        {
            return IsoLike.Contains(Path.GetExtension(fName.ToLower()));
        }
        private static int ListTar(string fName)
        {
            using (Stream stream = File.OpenRead(fName))
            using (var reader = ReaderFactory.Open(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    Console.WriteLine(reader.Entry.Key);

                }
            }

            return 0;
        }



        private static int ListZip(string fName)
        {
            Console.WriteLine("ListZip begins for " + fName);
            // try
            // {
            ZipFile zip = new ZipFile(fName);
            // } catch (Exception e)
            // {
            //     Console.WriteLine("Exception " + e);
            // }
            zip.ZipError += (s, e) =>
            {
                Console.WriteLine("Zip Error");
            };
            Console.WriteLine("Archive " + Path.GetFileName(fName) + " has " + zip.Count.ToString() + " files.");
            int i = 1;
            foreach (ZipEntry zEntry in zip)
            {
                string cEntry = zEntry.FileName.Replace('/', Path.DirectorySeparatorChar);
                if (IsVerbose)
                {
                    if (!zEntry.IsDirectory)
                    {
                        Console.WriteLine(cEntry);
                        Console.WriteLine(FormatSize(zEntry.CompressedSize) + " | " + FormatSize(zEntry.UncompressedSize) + " | " +
                        zEntry.LastModified.ToString() + " | " + zEntry.Crc.ToString("X") + " | " + (zEntry.UsesEncryption ? "PWD" : "---") + " | " +
                        zEntry.CompressionMethod.ToString());
                        Console.WriteLine("");

                        /* Console.WriteLine("----------------------------------------------------------------------------");
                        Console.WriteLine("(" + i + "): " + cEntry + " - " + (zEntry.IsDirectory ? "Folder" : "File"));
                        Console.WriteLine("-----> Orig. Size: " + FormatSize(zEntry.UncompressedSize));
                        Console.WriteLine("-----> Packed Size: " + FormatSize(zEntry.CompressedSize));
                        Console.WriteLine("-----> Last modified: " + zEntry.LastModified.ToString());
                        Console.WriteLine("-----> CRC32: " + zEntry.Crc.ToString("X"));
                        Console.WriteLine("-----> Protected: " + (zEntry.UsesEncryption ? "True" : "False"));
                        Console.WriteLine("-----> Compression Method: " + zEntry.CompressionMethod.ToString());
                        Console.WriteLine("----------------------------------------------------------------------------");
                        Console.WriteLine(""); */
                        i++;
                    }
                    else
                    {
                        Console.WriteLine(cEntry);
                        Console.WriteLine("0 | 0 | " +
                        zEntry.LastModified.ToString() + " | 00000000 | " + (zEntry.UsesEncryption ? "PWD" : "---") + " | " +
                        zEntry.CompressionMethod.ToString());
                        Console.WriteLine("");
                        /* Console.WriteLine("----------------------------------------------------------------------------");
                        Console.WriteLine("(" + i + "): " + cEntry + " - " + (zEntry.IsDirectory ? "Folder" : "File"));
                        Console.WriteLine("----------------------------------------------------------------------------");
                        Console.WriteLine(""); */
                        i++;
                    }

                }
                else
                {
                    Console.WriteLine("(" + i + "): " + cEntry);
                    i++;
                }
            }
            return 0;
        }


        // private static async Task<ExitCode> ScanForBomb(string[] args)
        // {
        //     /* Scoring system:
        //         Has more than 4 nested but less than 16? +5
        //         Has more than 16 or more nested zips? +11
        //         Do all nested zip have same CRC? +11
        //         Do all nested zip have same original size? +11
        //         Has more than 2 depth levels but less then 6? +15
        //         Has 6 or more depth levels? +33 --> 42.zip (the most famous) has 6 levels
        //         Last file size is larger than 1 MB but smaller than 1 GB? +15
        //         Last file size is larger or equal to 1 GB? +33 
        //     */
        //     ZipFile zip = new ZipFile(args[1]);
        //     string zipFilePath = args[1];
        //     using (ZipFile zipFile = ZipFile.Read(zipFilePath))
        //     {

        //         var uncompressedSizeCounts = new Dictionary<long, int>();
        //         var compressionRatioCounts = new Dictionary<double, int>();
        //         var crcCounts = new Dictionary<int, int>();

        //         int nestedZip = 0;
        //         int sameCrc = 0;
        //         int sameSize = 0;
        //         int sameRatio = 0;
        //         bool IsNested = false;
        //         long GlobalUnpackedSize = 0;
        //         // long sizeCheck = 0;
        //         if (IsVerbose)
        //         {
        //             FileInfo fInfo = new FileInfo(args[1]);
        //             Console.WriteLine("Reading " + args[1] + " (" + FormatSize(fInfo.Length) + ")...");
        //             Console.WriteLine("------------------------------------");
        //         }
        //         foreach (ZipEntry entry in zipFile)
        //         {

        //             GlobalUnpackedSize += entry.UncompressedSize;
        //             long uncompressedSize = entry.UncompressedSize;
        //             double compressionRatio = entry.CompressionRatio;
        //             int crc = entry.Crc;
        //             if (Path.GetExtension(entry.FileName).Equals(".zip", StringComparison.CurrentCultureIgnoreCase))
        //                 nestedZip++;
        //             if (uncompressedSizeCounts.ContainsKey(uncompressedSize))
        //             {
        //                 uncompressedSizeCounts[uncompressedSize]++;
        //             }
        //             else
        //             {
        //                 uncompressedSizeCounts[uncompressedSize] = 1;
        //             }

        //             if (compressionRatioCounts.ContainsKey(compressionRatio))
        //             {
        //                 compressionRatioCounts[compressionRatio]++;
        //             }
        //             else
        //             {
        //                 compressionRatioCounts[compressionRatio] = 1;
        //             }

        //             if (crcCounts.ContainsKey(crc))
        //             {
        //                 crcCounts[crc]++;
        //             }
        //             else
        //             {
        //                 crcCounts[crc] = 1;
        //             }

        //             if (IsVerbose)
        //             {
        //                 string eRatio = entry.CompressionRatio.ToString("0.00") + "%";
        //                 Console.WriteLine(entry.FileName + "\t" + entry.Crc.ToString("X8") + "\t" + FormatSizeEx((double)entry.UncompressedSize) + "\t" + eRatio);
        //             }

        //         }

        //         foreach (var pair in uncompressedSizeCounts)
        //         {
        //             if (pair.Value > 1)
        //             {
        //                 //Console.WriteLine($"Uncompressed size: {pair.Key}, Count: {pair.Value}");
        //                 sameSize += pair.Value;
        //             }
        //         }

        //         foreach (var pair in compressionRatioCounts)
        //         {
        //             if (pair.Value > 1)
        //             {
        //                 //Console.WriteLine($"Compression ratio: {pair.Key}, Count: {pair.Value}");
        //                 sameRatio += pair.Value;
        //             }
        //         }

        //         foreach (var pair in crcCounts)
        //         {
        //             if (pair.Value > 1)
        //             {
        //                 //Console.WriteLine($"CRC: {pair.Key}, Count: {pair.Value}");
        //                 sameCrc += pair.Value;
        //             }
        //         }

        //         Console.WriteLine("");
        //         int DepthLevel = 1;
        //         int DepthCount = 0;
        //         ulong PayloadSize = 0;
        //         foreach (ZipEntry fEntry in zipFile)
        //         {
        //             if (fEntry.IsDirectory)
        //                 continue;
        //             if (fEntry.FileName.EndsWith(".zip"))
        //             {

        //                 if (IsVerbose)
        //                 {
        //                     Console.WriteLine("");
        //                     Console.WriteLine("Let's check how deep is this archive...");
        //                     Console.WriteLine("---------------------------------------");
        //                 }
        //                 if (fEntry.UsesEncryption)
        //                 {
        //                     Console.Write("This archive needs a password (or CTRL-C to quit): ");
        //                     string pswd = ReadPassword();
        //                     fEntry.Password = pswd;
        //                 }
        //                 if (IsVerbose)
        //                 {
        //                     Console.WriteLine(Path.GetFileName(args[1]) + " - Archive level");
        //                 }
        //                 ScanNestedZip(zipFilePath,fEntry, DepthLevel, out DepthCount, out PayloadSize);
        //                 break; // we just need the first zip to start scanning its nested zips
        //             }

        //         }

        //         if (PayloadSize == -1)
        //         {
        //             Console.WriteLine("This archive cannot be scanned.");
        //             Console.WriteLine("Please, obtain the correct password and retry.");
        //             return ExitCode.ExtractionError;
        //         }

        //         if (PayloadSize == -2)
        //         {
        //             Console.WriteLine("Depth analysis stopped at level " + DepthCount + " for safety reasons.");
        //             Console.WriteLine("This archive looks like a 'quine' and it is a zipbomb for sure.");
        //             Console.WriteLine("Score: 99%");
        //             return ExitCode.Success;
        //         }
        //         IsNested = (DepthCount >= 2);
        //         int score = 0;
        //         if (IsNested)
        //         {
        //             if (nestedZip >= 4 && nestedZip < 16)
        //                 score += 5;
        //             else if (nestedZip >= 16)
        //                 score += 11;
        //             if (nestedZip == sameCrc)
        //                 score += 11;
        //             if (nestedZip == sameSize)
        //                 score += 11;
        //             //Console.WriteLine("Curr score: "+score);    
        //             if (DepthCount >= 2 && DepthCount < 6)
        //                 score += 15;
        //             else if (DepthCount >= 6)
        //             {
        //                 score += 33;
        //                 //    Console.WriteLine("Curr score: "+score);
        //             }
        //             if (PayloadSize >= 1048576 && PayloadSize < 1073741824)
        //                 score += 15;
        //             else if (PayloadSize >= 1073741824)
        //             {
        //                 score += 33;
        //                 //    Console.WriteLine("Curr score: "+score);
        //             }
        //         }
        //         else
        //         {
        //             if (sameCrc >= 4 && sameCrc < 8)
        //                 score += 5;
        //             else if (sameCrc >= 8)
        //                 score += 10;
        //             if (sameSize >= 4 && sameSize < 8)
        //                 score += 5;
        //             else if (sameSize >= 8)
        //                 score += 10;
        //             if (sameRatio >= 4 && sameRatio < 8)
        //                 score += 5;
        //             else if (sameRatio >= 8)
        //                 score += 10;
        //             if (sameCrc == sameRatio && sameRatio == sameSize && sameCrc == sameSize)
        //                 score += 15;
        //             if (GlobalUnpackedSize >= 4294967296 && GlobalUnpackedSize < 10737418240)
        //                 score += 15;
        //             else if (GlobalUnpackedSize >= 10737418240 && GlobalUnpackedSize < 34359738368)
        //                 score += 35;
        //             else if (GlobalUnpackedSize >= 34359738368)
        //                 score += 55;
        //         }
        //         if (IsVerbose)
        //         {
        //             Console.WriteLine("");
        //             Console.WriteLine("Summary");
        //             Console.WriteLine("---------------------------------------\n");
        //             if (nestedZip > 0)
        //                 Console.WriteLine("Nested Zips at top level: " + nestedZip);
        //             Console.WriteLine("Files with same CRC: " + sameCrc);
        //             Console.WriteLine("Files with same uncompressed file size: " + sameSize);
        //             Console.WriteLine("Files with same compression ratio: " + sameRatio);
        //             if (IsNested)
        //             {
        //                 Console.WriteLine("This archive looks like it has multiple levels.");
        //                 Console.WriteLine("Depth Levels: " + DepthCount);
        //                 Console.WriteLine("Payload size: " + PayloadSize + " bytes (" + FormatSizeEx((double)PayloadSize) + ")");
        //                 double GlobalPayload = Math.Pow(nestedZip, DepthCount) * PayloadSize;
        //                 Console.WriteLine("Global attack size: " + GlobalPayload + " bytes (" + FormatSizeEx(GlobalPayload) + ")");
        //                 Console.WriteLine("or...");
        //                 Console.WriteLine("(" + nestedZip + "^" + DepthCount + ")*" + PayloadSize + " = " + FormatSizeEx(GlobalPayload));
        //             }
        //             else
        //             {
        //                 Console.WriteLine("This archive has just files at the top level.");
        //                 Console.WriteLine("When fully decompressed, the archive will occupy " + FormatSizeEx((double)GlobalUnpackedSize) + " of storage.");
        //             }



        //             Console.WriteLine("");
        //         }
        //         Console.WriteLine("Score: " + score + "%");
        //         if (score < 40)
        //             Console.WriteLine("The file " + args[1] + " does not look like a zipbomb.");
        //         else if (score >= 40 && score < 60)
        //             Console.WriteLine("The file " + args[1] + " is highly suspected to be a zipbomb.");
        //         else if (score >= 60)
        //             Console.WriteLine("The file " + args[1] + " looks like a zipbomb.");



        //         return ExitCode.Success;
        //     }
        // }
        // private static long ScanNestedZip(string arcFile, ZipEntry entry, int DepLevel, out int DepCount, out long KillFileSize, string pswd = "")
        // {
        //     // 1. Extract only the first nested zip, then extract the first of its subnested and so on, until
        //     // we reach the "killer file". Let's get its size and count how many levels we have traversed.

        //     Console.WriteLine(entry.CompressionMethod.ToString()+" - "+entry.FileName);

        //     //Console.WriteLine(">> "+entry.FileName+" | "+arcFile);
        //     DepCount = DepLevel;
        //     MemoryStream nestedZipStream;
        //     if ((int)entry.CompressionMethod>12){

        //     }
        //     using (nestedZipStream = new MemoryStream())
        //     {
        //         try
        //         {
        //             entry.Extract(nestedZipStream);
        //         }
        //         catch (Exception ex)
        //         {
        //             Console.WriteLine(ex.Message);
        //             DepCount = DepLevel;
        //             KillFileSize = -1;
        //             return -1;
        //             /*
        //             if (ex.Message.ToLower().Contains("lzma")){
        //                 string LibPath = string.Empty;
        //                 if (IsLinux())
        //                 {
        //                     Console.WriteLine("We are in linux");
        //                     LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "linux", "7z.so");
        //                 }
        //                 else
        //                 {
        //                     Console.WriteLine("This is WIndows");
        //                     if (Environment.Is64BitProcess)
        //                         LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "64bit", "7z.dll");
        //                     else
        //                         LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "32bit", "7z.dll");
        //                 }
        //                 SevenZip.SevenZipBase.SetLibraryPath(LibPath);
        //                 using (SevenZipExtractor szip = new SevenZipExtractor(arcFile))
        //                 {
        //                     Console.WriteLine("szip trying to extract");
        //                     foreach(ArchiveFileInfo zEntry in szip.ArchiveFileData){
        //                        Console.WriteLine("## "+zEntry.FileName+" # "+entry.FileName+" ##");
        //                         if (zEntry.FileName!=entry.FileName)
        //                             continue;
        //                         else {
        //                             szip.ExtractFile(zEntry.FileName, nestedZipStream);
        //                             Console.WriteLine("szip extracted "+zEntry.FileName);
        //                         }
        //                     }
        //                 }
        //             } else {
        //                 DepCount = DepLevel;
        //                 KillFileSize = -1;
        //                 return -1;
        //             }
        //             */

        //         }

        //         nestedZipStream.Position = 0;
        //         KillFileSize = 0;
        //         string ArrChar = "\u2500";
        //         if (IsVerbose)
        //         {
        //             string msgLevel = "\u2514";
        //             for (int x = 1; x <= DepLevel; x++)
        //             {
        //                 msgLevel += ArrChar;
        //             }
        //             Console.WriteLine(msgLevel + " Level " + DepLevel);
        //         }
        //         // At level 50 we will skip extraction
        //         if (DepLevel < 50)
        //         {
        //             //Console.WriteLine("Sub "+nestedZipStream.Length.ToString());
        //             using (ZipFile zipFile = ZipFile.Read(nestedZipStream))
        //             {
        //                 foreach (ZipEntry nEntry in zipFile)
        //                 {
        //                     //Console.WriteLine("Presub -> "+nEntry.FileName);
        //                     if (nEntry.IsDirectory)
        //                         continue;
        //                     if (nEntry.FileName.EndsWith(".zip"))
        //                     {
        //                         //Console.WriteLine("sub -> "+nEntry.FileName);
        //                         DepLevel++;
        //                         ScanNestedZip(arcFile, nEntry, DepLevel, out DepCount, out KillFileSize);
        //                         break;
        //                     }
        //                     else
        //                     {
        //                         KillFileSize += nEntry.UncompressedSize;
        //                         if (IsVerbose)
        //                         {
        //                             string msgLevel = "\u2514";
        //                             for (int x = 1; x <= (DepLevel + 4); x++)
        //                             {
        //                                 msgLevel += ArrChar;
        //                             }
        //                             Console.WriteLine(msgLevel + " PAYLOAD =>  File: " + nEntry.FileName + " (" + FormatSizeEx((double)KillFileSize) + ")");
        //                         }
        //                     }
        //                 }
        //             }
        //         }
        //         else
        //         {
        //             KillFileSize = -2;
        //         }
        //         return KillFileSize;

        //     }
        // }

        private static async Task<ExitCode> ScanForBomb2(string[] args)
        {
            /* Scoring system:
                Has more than 4 nested but less than 16? +5
                Has more than 16 or more nested zips? +11
                Do all nested zip have same CRC? +11
                Do all nested zip have same original size? +11
                Has more than 2 depth levels but less then 6? +15
                Has 6 or more depth levels? +33 --> 42.zip (the most famous) has 6 levels
                Last file size is larger than 1 MB but smaller than 1 GB? +15
                Last file size is larger or equal to 1 GB? +33 
            */

            string zipFilePath = args[1];
            using (ArchiveFile zipFile = new ArchiveFile(zipFilePath, FileUtils.GetSevenZipLibs()))
            {
                var uncompressedSizeCounts = new Dictionary<ulong, int>();
                var compressionRatioCounts = new Dictionary<double, int>();
                var crcCounts = new Dictionary<uint, int>();

                int nestedZip = 0;
                int sameCrc = 0;
                int sameSize = 0;
                int sameRatio = 0;
                bool IsNested = false;
                ulong GlobalUnpackedSize = 0;
                // long sizeCheck = 0;
                if (IsVerbose)
                {
                    FileInfo fInfo = new FileInfo(args[1]);
                    Console.WriteLine("\nReading contents of " + args[1] + " (" + FormatSize(fInfo.Length) + ")...");
                    //Console.WriteLine("------------------------------------");
                }

                ConsoleTable table = null;
                if (IsVerbose)
                {
                    ConsoleTableOptions tOptions = new ConsoleTableOptions();
                    tOptions.NumberAlignment = Alignment.Right;
                    table = new ConsoleTable("File", "Packed Size", "Size", "Ratio", "CRC-32");
                }
                foreach (SevenZipExtractor.Entry entry in zipFile.Entries)
                {
                    if (entry.IsFolder)
                        continue;

                    GlobalUnpackedSize += entry.Size;
                    ulong uncompressedSize = entry.Size;

                    double compressionRatio = Math.Abs((float)entry.Size - (float)entry.PackedSize) / (double)Math.Max(entry.PackedSize, entry.Size);
                    //Console.WriteLine(entry.Size+" . "+ compressionRatio.ToString("P2"));
                    uint crc = entry.CRC;
                    if (Path.GetExtension(entry.FileName).Equals(".zip", StringComparison.CurrentCultureIgnoreCase))
                        nestedZip++;
                    if (uncompressedSizeCounts.ContainsKey(uncompressedSize))
                    {
                        uncompressedSizeCounts[uncompressedSize]++;
                    }
                    else
                    {
                        uncompressedSizeCounts[uncompressedSize] = 1;
                    }

                    if (compressionRatioCounts.ContainsKey(compressionRatio))
                    {
                        compressionRatioCounts[compressionRatio]++;
                    }
                    else
                    {
                        compressionRatioCounts[compressionRatio] = 1;
                    }

                    if (crcCounts.ContainsKey(crc))
                    {
                        crcCounts[crc]++;
                    }
                    else
                    {
                        crcCounts[crc] = 1;
                    }

                    if (IsVerbose)
                    {
                        table.AddRow(entry.FileName, entry.PackedSize.ToString("N0"), entry.Size.ToString("N0"), compressionRatio.ToString("P0"), entry.CRC.ToString("X8"));

                        // string eRatio = compressionRatio.ToString("0.00") + "%";
                        // string line = string.Format("{0,10}{1,5:N0}{2,15:N0}{3,5}%", entry.FileName, entry.PackedSize, entry.Size, compressionRatio);
                        // Console.WriteLine(line);
                        // Console.WriteLine(entry.FileName + "\t" + entry.CRC.ToString("X8") + "\t" + FormatSizeEx((double)entry.Size) + "\t" + eRatio);
                    }

                }
                table.Write(Format.Minimal);

                foreach (var pair in uncompressedSizeCounts)
                {
                    if (pair.Value > 1)
                    {
                        // Console.WriteLine($"Uncompressed size: {pair.Key}, Count: {pair.Value}");
                        sameSize += pair.Value;
                    }
                }

                foreach (var pair in compressionRatioCounts)
                {
                    if (pair.Value > 1)
                    {
                        // Console.WriteLine($"Compression ratio: {pair.Key}, Count: {pair.Value}");
                        sameRatio += pair.Value;
                    }
                }

                foreach (var pair in crcCounts)
                {
                    if (pair.Value > 1)
                    {
                        // Console.WriteLine($"CRC: {pair.Key}, Count: {pair.Value}");
                        sameCrc += pair.Value;
                    }
                }

                Console.WriteLine("");
                int DepthLevel = 1;
                int DepthCount = 0;
                ulong PayloadSize = 0;
                int ResCode = 0;
                foreach (SevenZipExtractor.Entry fEntry in zipFile.Entries)
                {
                    if (fEntry.IsFolder)
                        continue;
                    if (fEntry.FileName.EndsWith(".zip"))
                    {

                        if (IsVerbose)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("Let's check how deep is this archive...");
                            Console.WriteLine("---------------------------------------");
                        }
                        if (fEntry.IsEncrypted)
                        {
                            Console.Write("This archive needs a password (or CTRL-C to quit): ");
                            string pswd = ReadPassword();
                            //fEntry. = pswd;
                        }
                        if (IsVerbose)
                        {
                            Console.WriteLine(Path.GetFileName(args[1]) + " - Archive level");
                        }
                        ScanNestedZip2(zipFilePath, fEntry, DepthLevel, out DepthCount, out PayloadSize, out ResCode);
                        break; // we just need the first zip to start scanning its nested zips
                    }

                }

                if (ResCode == -1)
                {
                    Console.WriteLine("This archive cannot be scanned.");
                    Console.WriteLine("Please, obtain the correct password and retry.");
                    return ExitCode.ExtractionError;
                }

                if (ResCode == -2)
                {
                    Console.WriteLine("Depth analysis stopped at level " + DepthCount + " for safety reasons.");
                    Console.WriteLine("This archive looks like a 'quine' and it is a zipbomb for sure.");
                    Console.WriteLine("Score: 99%");
                    return ExitCode.Success;
                }
                IsNested = (DepthCount >= 2);
                int score = 0;
                if (IsNested)
                {
                    if (nestedZip >= 4 && nestedZip < 16)
                        score += 5;
                    else if (nestedZip >= 16)
                        score += 11;
                    if (nestedZip == sameCrc)
                        score += 11;
                    if (nestedZip == sameSize)
                        score += 11;
                    //Console.WriteLine("Curr score: "+score);    
                    if (DepthCount >= 2 && DepthCount < 6)
                        score += 15;
                    else if (DepthCount >= 6)
                    {
                        score += 33;
                        //    Console.WriteLine("Curr score: "+score);
                    }
                    if (PayloadSize >= 1048576 && PayloadSize < 1073741824)
                        score += 15;
                    else if (PayloadSize >= 1073741824)
                    {
                        score += 33;
                        //    Console.WriteLine("Curr score: "+score);
                    }
                }
                else
                {
                    if (sameCrc >= 4 && sameCrc < 8)
                        score += 5;
                    else if (sameCrc >= 8)
                        score += 10;
                    if (sameSize >= 4 && sameSize < 8)
                        score += 5;
                    else if (sameSize >= 8)
                        score += 10;
                    if (sameRatio >= 4 && sameRatio < 8)
                        score += 5;
                    else if (sameRatio >= 8)
                        score += 10;
                    if (sameCrc == sameRatio && sameRatio == sameSize && sameCrc == sameSize)
                        score += 15;
                    if (GlobalUnpackedSize >= 4294967296 && GlobalUnpackedSize < 10737418240)
                        score += 15;
                    else if (GlobalUnpackedSize >= 10737418240 && GlobalUnpackedSize < 34359738368)
                        score += 35;
                    else if (GlobalUnpackedSize >= 34359738368)
                        score += 55;
                }
                if (IsVerbose)
                {
                    Console.WriteLine("");
                    Console.WriteLine("Summary");
                    Console.WriteLine("---------------------------------------\n");
                    if (nestedZip > 0)
                        Console.WriteLine("Nested Zips at top level: " + nestedZip.ToString("N0"));
                    Console.WriteLine("Files with same CRC: " + sameCrc.ToString("N0"));
                    Console.WriteLine("Files with same uncompressed file size: " + sameSize);
                    Console.WriteLine("Files with same compression ratio: " + sameRatio);
                    if (IsNested)
                    {
                        Console.WriteLine("This archive looks like it has multiple levels.");
                        Console.WriteLine("Depth Levels: " + DepthCount.ToString("N0"));
                        Console.WriteLine("Payload size: " + PayloadSize.ToString("N0") + " bytes (" + FormatSizeEx((double)PayloadSize, true) + ")");
                        double GlobalPayload = Math.Pow(nestedZip, DepthCount) * PayloadSize;
                        Console.WriteLine("Global attack size: " + GlobalPayload.ToString("N0") + " bytes (" + FormatSizeEx(GlobalPayload, true) + ")");
                        Console.WriteLine("or...");
                        Console.WriteLine("(" + nestedZip.ToString("N0") + "^" + DepthCount.ToString("N0") + ")*" + PayloadSize.ToString("N0") + " = " + FormatSizeEx(GlobalPayload, true));
                    }
                    else
                    {
                        Console.WriteLine("This archive has just files at the top level.");
                        Console.WriteLine("When fully decompressed, the archive will occupy " + FormatSizeEx((double)GlobalUnpackedSize, true) + " of storage.");
                    }



                    Console.WriteLine("");
                }
                Console.WriteLine("Score: " + score + "%");
                if (score < 40)
                    Console.WriteLine("The file " + args[1] + " does not look like a zipbomb.");
                else if (score >= 40 && score < 60)
                    Console.WriteLine("The file " + args[1] + " is highly suspected to be a zipbomb.");
                else if (score >= 60)
                    Console.WriteLine("The file " + args[1] + " looks like a zipbomb.");



                return ExitCode.Success;
            }
        }

        private static ulong ScanNestedZip2(string arcFile, SevenZipExtractor.Entry entry, int DepLevel, out int DepCount, out ulong KillFileSize, out int ResCode, string pswd = "")
        {
            // 1. Extract only the first nested zip, then extract the first of its subnested and so on, until
            // we reach the "killer file". Let's get its size and count how many levels we have traversed.

            DepCount = DepLevel;
            MemoryStream nestedZipStream;

            using (nestedZipStream = new MemoryStream())
            {
                try
                {
                    entry.Extract(nestedZipStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    DepCount = DepLevel;
                    KillFileSize = 0;
                    ResCode = -1;
                    return 0;
                    /*
                    if (ex.Message.ToLower().Contains("lzma")){
                        string LibPath = string.Empty;
                        if (IsLinux())
                        {
                            Console.WriteLine("We are in linux");
                            LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "linux", "7z.so");
                        }
                        else
                        {
                            Console.WriteLine("This is WIndows");
                            if (Environment.Is64BitProcess)
                                LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "64bit", "7z.dll");
                            else
                                LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "32bit", "7z.dll");
                        }
                        SevenZip.SevenZipBase.SetLibraryPath(LibPath);
                        using (SevenZipExtractor szip = new SevenZipExtractor(arcFile))
                        {
                            Console.WriteLine("szip trying to extract");
                            foreach(ArchiveFileInfo zEntry in szip.ArchiveFileData){
                               Console.WriteLine("## "+zEntry.FileName+" # "+entry.FileName+" ##");
                                if (zEntry.FileName!=entry.FileName)
                                    continue;
                                else {
                                    szip.ExtractFile(zEntry.FileName, nestedZipStream);
                                    Console.WriteLine("szip extracted "+zEntry.FileName);
                                }
                            }
                        }
                    } else {
                        DepCount = DepLevel;
                        KillFileSize = -1;
                        return -1;
                    }
                    */

                }

                nestedZipStream.Position = 0;
                KillFileSize = 0;
                ResCode = 0;
                string ArrChar = "\u2500";
                if (IsVerbose)
                {
                    string msgLevel = "\u2514";
                    for (int x = 1; x <= DepLevel; x++)
                    {
                        msgLevel += ArrChar;
                    }
                    Console.WriteLine(msgLevel + " Level " + DepLevel);
                }
                // At level 50 we will skip extraction
                if (DepLevel < 50)
                {
                    nestedZipStream.Position = 0;
                    //Console.WriteLine("Sub "+nestedZipStream.Length.ToString());
                    // SevenZipFormat szFormat = (SevenZipFormat)FileUtils.GetSevenZipFormat(nestedZipStream);
                    // Console.WriteLine(szFormat);
                    using (ArchiveFile zipFile = new ArchiveFile(nestedZipStream, SevenZipFormat.Zip, FileUtils.GetSevenZipLibs()))
                    {
                        foreach (SevenZipExtractor.Entry nEntry in zipFile.Entries)
                        {
                            //Console.WriteLine("Presub -> "+nEntry.FileName);
                            if (nEntry.IsFolder)
                                continue;
                            if (nEntry.FileName.EndsWith(".zip"))
                            {
                                //Console.WriteLine("sub -> "+nEntry.FileName);
                                DepLevel++;
                                ResCode = 0;
                                ScanNestedZip2(arcFile, nEntry, DepLevel, out DepCount, out KillFileSize, out ResCode);
                                break;
                            }
                            else
                            {
                                KillFileSize += nEntry.Size;
                                if (IsVerbose)
                                {
                                    string msgLevel = "\u2514";
                                    for (int x = 1; x <= (DepLevel + 4); x++)
                                    {
                                        msgLevel += ArrChar;
                                    }
                                    Console.WriteLine(msgLevel + " PAYLOAD =>  File: " + nEntry.FileName + ", " + FormatSizeEx(nEntry.Size, true) + " (Total: " + FormatSizeEx((double)KillFileSize, true) + ")");
                                }
                            }
                        }
                    }
                }
                else
                {
                    ResCode = -2;
                    KillFileSize = 0;
                }
                return KillFileSize;

            }
        }


        // private static async Task<long> ScanNestedZip_old(ZipEntry entry, long ZipFileSize, int Depth, int MaxDepth = 4)
        // {
        //        Console.WriteLine("Size: " + NestedGlobalSize + "\t" + FormatSize(NestedGlobalSize));
        //     int nestedInnerZipCount = 0;
        //     int sameNestedCrcCount = 1;
        //     string crcNestedCheck = "00000000";
        //     long currSize = 0;

        //     using (MemoryStream nestedZipStream = new MemoryStream())
        //     {
        //         entry.Extract(nestedZipStream);
        //         nestedZipStream.Position = 0;
        //         using (ZipFile zipFile = ZipFile.Read(nestedZipStream))
        //         {
        //             foreach (ZipEntry nEntry in zipFile)
        //             {
        //                 if (nEntry.IsDirectory)
        //                     continue;
        //                 if (nEntry.FileName.EndsWith(".zip"))
        //                 {
        //                     if (crcNestedCheck == "00000000")
        //                         crcNestedCheck = nEntry.Crc.ToString("X8");
        //                     else
        //                     {
        //                         if (crcNestedCheck == nEntry.Crc.ToString("X8"))
        //                         {
        //                             sameNestedCrcCount++;
        //                         }
        //                     }
        //                     nestedInnerZipCount++;
        //                     NestedGlobalSize += nEntry.UncompressedSize;
        //                     //           Console.WriteLine(nEntry.FileName + "\t" + nEntry.Crc.ToString("X8") + "\t" + nEntry.UncompressedSize.ToString());                            
        //                     //if (Depth < MaxDepth)
        //                     //{
        //                     Depth++;
        //                     Console.WriteLine("\t" + entry.FileName + ">" + nEntry.FileName);
        //                     ScanNestedZip(nEntry, Depth, MaxDepth);
        //                     //}
        //                     //break;
        //                 }
        //                 else
        //                 {
        //                     NestedGlobalSize += nEntry.UncompressedSize;
        //                     Console.WriteLine("##> " + nEntry.FileName);
        //                     break;
        //                 }
        //             }
        //         }
        //         //Console.WriteLine(currSize+" - "+FormatSize(currSize));
        //         return currSize;
        //     }
        // }

        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                // Ignore any non-character keys
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Remove(password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();

            return password;
        }
        static ExitCode HashArchive(string[] args)
        {
            if (args.Length < 2)
            {
                WriteLocalizedError("HashMissingFile", "No file was specified.");
                return ExitCode.FileDoesNotExists;
            }

            string filePath = Environment.ExpandEnvironmentVariables(args[1]);
            if (!File.Exists(filePath))
            {
                WriteLocalizedError("HashFileNotFound", "File not found: {0}", filePath);
                return ExitCode.FileDoesNotExists;
            }

            var algorithms = new Dictionary<string, (string Name, Func<string, string> Calculate)>(StringComparer.OrdinalIgnoreCase)
            {
                ["md5"] = ("MD5", CryptoUtils.MD5FileHash),
                ["sha1"] = ("SHA-1", CryptoUtils.Sha1FileHash),
                ["sha224"] = ("SHA-224", CryptoUtils.Sha224FileHash),
                ["sha256"] = ("SHA-256", CryptoUtils.Sha256FileHash),
                ["sha384"] = ("SHA-384", CryptoUtils.Sha384FileHash),
                ["sha512"] = ("SHA-512", CryptoUtils.Sha512FileHash),
                ["sha3224"] = ("SHA-3/224", path => CryptoUtils.Sha3FileHash(path, 224)),
                ["sha3256"] = ("SHA-3/256", path => CryptoUtils.Sha3FileHash(path, 256)),
                ["sha3512"] = ("SHA-3/512", path => CryptoUtils.Sha3FileHash(path, 512)),
                ["ripemd128"] = ("RIPEMD-128", path => CryptoUtils.RipeMDFileHash(path, 128)),
                ["ripemd160"] = ("RIPEMD-160", path => CryptoUtils.RipeMDFileHash(path, 160)),
                ["ripemd256"] = ("RIPEMD-256", path => CryptoUtils.RipeMDFileHash(path, 256)),
                ["ripemd320"] = ("RIPEMD-320", path => CryptoUtils.RipeMDFileHash(path, 320)),
                ["tiger"] = ("Tiger", CryptoUtils.TigerFileHash),
                ["whirlpool"] = ("Whirlpool", CryptoUtils.WhirlpoolFileHash)
            };

            var selectedAlgorithms = new List<(string Name, Func<string, string> Calculate)>();
            var invalidAlgorithms = new List<string>();
            bool calculateAll = false;

            for (int i = 2; i < args.Length; i++)
            {
                string argument = args[i];
                if (!argument.StartsWith("--", StringComparison.Ordinal))
                    continue;

                string option = argument[2..];
                if (IsGlobalOption(option))
                    continue;

                string normalized = option.Replace("-", "").Replace("/", "").ToLowerInvariant();
                if (normalized == "all")
                {
                    calculateAll = true;
                    continue;
                }

                if (algorithms.TryGetValue(normalized, out var algorithm))
                    selectedAlgorithms.Add(algorithm);
                else
                    invalidAlgorithms.Add(argument);
            }

            if (invalidAlgorithms.Count > 0)
            {
                WriteLocalizedError("HashUnknownAlgorithm", "Unknown hashing algorithm: {0}", string.Join(", ", invalidAlgorithms));
                return ExitCode.UnknownError;
            }

            if (calculateAll)
                selectedAlgorithms = algorithms.Values.ToList();

            selectedAlgorithms = selectedAlgorithms
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (selectedAlgorithms.Count == 0)
            {
                WriteLocalizedError("HashAlgorithmRequired", "Specify at least one hashing algorithm. Use 'zg -help -hash' for help.");
                return ExitCode.UnknownError;
            }

            try
            {
                foreach (var algorithm in selectedAlgorithms)
                {
                    string hash;
                    if (IsSilent || !DoShowProgress)
                    {
                        hash = algorithm.Calculate(filePath);
                    }
                    else
                    {
                        string message = GetLocalizedString("HashCalculating", "Calculating {0} hash of {1}", algorithm.Name, filePath);
                        Console.Write(message);
                        try
                        {
                            Task<string> calculation = Task.Run(() => algorithm.Calculate(filePath));
                            while (!calculation.IsCompleted)
                            {
                                System.Threading.Thread.Sleep(500);
                                if (!calculation.IsCompleted)
                                    Console.Write('.');
                            }
                            hash = calculation.GetAwaiter().GetResult();
                        }
                        finally
                        {
                            Console.WriteLine();
                        }
                    }
                    if (!IsSilent)
                        Console.WriteLine(GetLocalizedString("HashResult", "{0} hash of {1}:", algorithm.Name, filePath));
                    Console.WriteLine(hash);
                    if (selectedAlgorithms.Count > 1)
                        Console.WriteLine();
                }

                return ExitCode.Success;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLocalizedError("HashAccessDenied", "Unable to read the file: {0}", ex.Message);
                return ExitCode.IOError;
            }
            catch (IOException ex)
            {
                WriteLocalizedError("HashReadError", "Error while reading the file: {0}", ex.Message);
                return ExitCode.IOError;
            }
        }

        private static ZipBombGuard.Result CheckZipBomb(string path, bool listing = false)
        {
            var result = ZipBombGuard.Inspect(path);
            if (result.ExitCode != 0)
            {
                // Security diagnostics are never suppressed and leave stdout usable by scripts.
                Console.Error.WriteLine(GetLocalizedString("ZipBombStatus",
                    "ZIP security: {0}; score={1}%; risk-code={2}; {3}",
                    path, result.Score, result.ExitCode,
                    result.Score >= 60 ? GetLocalizedString("ZipBombDanger", "HIGH RISK.") :
                    result.Incomplete ? GetLocalizedString("ZipBombUnverified", "Inspection incomplete.") :
                    GetLocalizedString("ZipBombWarning", "WARNING: suspicious archive.")));
                if (result.Blocked && !listing)
                    Console.Error.WriteLine(GetLocalizedString("ZipBombBlocked", "Operation blocked by mandatory ZIP security protection."));
            }
            return result;
        }

        private static bool IsGlobalOption(string option)
        {
            string name = option.Split('=', 2)[0].ToLowerInvariant();
            return name is "w" or "wait" or "s" or "silent" or "v" or "verbose" or
                "o" or "open" or "p" or "progressoff" or "noprogress" or "y" or
                "always" or "yes" or "donotask" or "simulate" or "t" or "temp";
        }

        private static string GetLocalizedString(string resourceName, string fallback, params object[] args)
        {
            string format = Resources.ResourceManager.GetString(resourceName, CultureInfo.CurrentUICulture) ?? fallback;
            return args.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, args);
        }

        private static void WriteLocalizedError(string resourceName, string fallback, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(GetLocalizedString(resourceName, fallback, args));
            Console.ResetColor();
        }

        private static ExitCode ReportDeprecatedCommand(string command)
        {
            if (!IsSilent)
                WriteLocalizedError("DeprecatedCommand", "The {0} command is deprecated and is no longer available.", command);
            return ExitCode.DeprecatedCommand;
        }

        private static bool CompareAttributes(FileAttributes fAttrs, FileAttributes nCriteria)
        {
            bool CanProceed = false;

            bool DoArchive = ((nCriteria & FileAttributes.Archive) == FileAttributes.Archive);
            bool DoFolder = ((nCriteria & FileAttributes.Directory) == FileAttributes.Directory);
            bool DoSystem = ((nCriteria & FileAttributes.System) == FileAttributes.System);
            bool DoReadOnly = ((nCriteria & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            bool DoHidden = ((nCriteria & FileAttributes.Hidden) == FileAttributes.Hidden);

            bool isArchive = ((fAttrs & FileAttributes.Archive) == FileAttributes.Archive);
            bool isFolder = ((fAttrs & FileAttributes.Directory) == FileAttributes.Directory);
            bool isSystem = ((fAttrs & FileAttributes.System) == FileAttributes.System);
            bool isReadOnly = ((fAttrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            bool isHidden = ((fAttrs & FileAttributes.Hidden) == FileAttributes.Hidden);
            if (DoArchive && isArchive)
                CanProceed = true;
            if (DoFolder && isFolder)
                CanProceed = true;
            else if (DoSystem && isSystem)
                CanProceed = true;
            else if (DoHidden && isHidden)
                CanProceed = true;
            else if (DoReadOnly && isReadOnly)
                CanProceed = true;
            return CanProceed;
        }
        private static void PrintStartupHelp()
        {
            Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"ZipGenius X Console - {version}");
            ShowManual("startup");
        }

        private static void PrintHeader(bool showhelp = false)
        {
            Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string title = $"ZipGenius X - Command Line Module v.{version}";
            string copyright = $"Copyright (c){DateTime.Now.Year} The ZipGenius Team - https://www.zipgenius.it";
            int contentWidth = Math.Max(title.Length, copyright.Length);
            if (IsWindows())
            {
                string border = new string('#', contentWidth + 4);
                Console.WriteLine(border);
                Console.WriteLine($"# {title.PadRight(contentWidth)} #");
                Console.WriteLine($"# {copyright.PadRight(contentWidth)} #");
                Console.WriteLine(border);
            }
            if (showhelp)
            {
                ShowManual("generic");
            }
        }

        #region Actions on archive

        private sealed class ExtractCommandOptions
        {
            public string ArchivePath { get; set; } = string.Empty;
            public string DestinationPath { get; set; } = string.Empty;
            public bool Recursive { get; set; } = true;
            public bool RestorePaths { get; set; } = true;
            public bool CreateArchiveSubfolder { get; set; }
            public int OverwriteMode { get; set; } = 2;
            public string Password { get; set; } = string.Empty;
            public string AttributeFilter { get; set; } = "X";
            public List<string> Inclusions { get; } = new();
            public List<string> Exclusions { get; } = new();
        }

        public static ExitCode ExtractFiles(string[] args)
        {
            try
            {
                ExtractCommandOptions options = ParseExtractOptions(args);
                if (!File.Exists(options.ArchivePath))
                {
                    WriteLocalizedError("ExtractArchiveNotFound", "Archive not found: {0}", options.ArchivePath);
                    return ExitCode.FileDoesNotExists;
                }

                var safety = CheckZipBomb(options.ArchivePath);
                if (safety.Blocked)
                    return (ExitCode)safety.ExitCode;

                if (options.CreateArchiveSubfolder)
                    options.DestinationPath = Path.Combine(options.DestinationPath, GetArchiveBaseName(options.ArchivePath));

                Directory.CreateDirectory(options.DestinationPath);
                if (Path.GetExtension(options.ArchivePath).Equals(".sqx", StringComparison.OrdinalIgnoreCase))
                    return ExtractSqxFilesWithPasswordRetries(options);

                return IsWindows()
                    ? ExtractSevenZipFilesWithPasswordRetries(options)
                    : ExtractManagedFilesWithPasswordRetries(options);
            }
            catch (OperationCanceledException)
            {
                WriteLocalizedError("ExtractCanceled", "Extraction canceled by the user.");
                return ExitCode.CanceledByUser;
            }
            catch (ArgumentException ex)
            {
                WriteLocalizedError("ExtractInvalidArguments", "Invalid extraction arguments: {0}", ex.Message);
                return ExitCode.ExtractionError;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLocalizedError("ExtractAccessDenied", "Access denied during extraction: {0}", ex.Message);
                return ExitCode.IOError;
            }
            catch (IOException ex)
            {
                WriteLocalizedError("ExtractIoError", "I/O error during extraction: {0}", ex.Message);
                return ExitCode.IOError;
            }
            catch (Exception ex)
            {
                WriteLocalizedError("ExtractFailed", "Extraction failed: {0}", ex.Message);
                return ExitCode.ExtractionError;
            }
        }

        private static ExitCode ExtractManagedFilesWithPasswordRetries(ExtractCommandOptions options)
        {
            int passwordAttempts = 0;
            bool encryptedEntries = false;
            while (true)
            {
                string stagingRoot = Path.Combine(TempPath, "zgshell-extract-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(stagingRoot);
                    using IArchive archive = TarArchiveEditor.IsTarArchivePath(options.ArchivePath)
                        ? TarArchiveEditor.OpenTarArchive(options.ArchivePath)
                        : ArchiveFactory.Open(options.ArchivePath, new ReaderOptions { Password = options.Password });
                    var entries = archive.Entries.Where(entry =>
                        ShouldExtractStagedFile(entry.Key ?? string.Empty,
                            entry.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal, options) &&
                        (!entry.IsDirectory || options.Recursive)).ToList();
                    if (entries.Count == 0)
                        return ExitCode.ZeroItemsArchive;
                    encryptedEntries = entries.Any(entry => entry.IsEncrypted);
                    if (encryptedEntries && string.IsNullOrEmpty(options.Password) && CanPromptForExtractionPassword())
                    {
                        options.Password = ReadExtractionPassword(options.ArchivePath, ++passwordAttempts);
                        continue;
                    }
                    using (ProgressBar? progress = CreateAddProgress(100, "Extracting archive..."))
                    {
                        byte[] buffer = new byte[81920];
                        for (int index = 0; index < entries.Count; index++)
                        {
                            var entry = entries[index];
                            string name = entry.Key ?? string.Empty;
                            string path = GetSafeExtractionPath(stagingRoot, GetExtractedEntryPath(name, options.RestorePaths));
                            if (entry.IsDirectory)
                            {
                                if (options.RestorePaths) Directory.CreateDirectory(path);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                                using (Stream input = entry.OpenEntryStream())
                                using (FileStream output = File.Create(path))
                                {
                                    long copied = 0;
                                    int count;
                                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        output.Write(buffer, 0, count);
                                        copied += count;
                                        double fraction = entry.Size > 0 ? Math.Min(1, (double)copied / entry.Size) : 0;
                                        progress?.Tick((int)((index + fraction) * 100 / entries.Count), name);
                                    }
                                }
                                if (entry.LastModifiedTime.HasValue)
                                    File.SetLastWriteTime(path, entry.LastModifiedTime.Value);
                            }
                            progress?.Tick((index + 1) * 100 / entries.Count, name);
                        }
                    }
                    int extractedFiles = CommitStagedExtraction(stagingRoot, options);
                    if (!IsSilent)
                        Console.WriteLine(GetLocalizedString("ExtractCompleted", "Extracted {0} file(s) to {1}", extractedFiles, options.DestinationPath));
                    return ExitCode.Success;
                }
                catch (Exception ex) when (IsLikelyPasswordFailure(ex, encryptedEntries) && CanPromptForExtractionPassword() && passwordAttempts < 3)
                {
                    options.Password = ReadExtractionPassword(options.ArchivePath, ++passwordAttempts);
                }
                finally
                {
                    DeleteExtractionStagingFolder(stagingRoot);
                }
            }
        }

        private static ExitCode ExtractSevenZipFilesWithPasswordRetries(ExtractCommandOptions options)
        {
            int passwordAttempts = 0;
            bool encryptedEntries = false;
            while (true)
            {
                string stagingRoot = Path.Combine(TempPath, "zgshell-extract-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(stagingRoot);
                    SevenZip.SevenZipBase.SetLibraryPath(FileUtils.GetSevenZipLibs());
                    using SevenZip.SevenZipExtractor archive = string.IsNullOrEmpty(options.Password)
                        ? new SevenZip.SevenZipExtractor(options.ArchivePath)
                        : new SevenZip.SevenZipExtractor(options.ArchivePath, options.Password);
                    List<SevenZip.ArchiveFileInfo> entries = archive.ArchiveFileData
                        .Where(entry => ShouldExtractEntry(entry, options))
                        .ToList();

                    if (entries.Count == 0)
                    {
                        WriteLocalizedError("ExtractNoMatchingFiles", "No archive entries match the requested filters.");
                        return ExitCode.ZeroItemsArchive;
                    }

                    encryptedEntries = entries.Any(entry => entry.Encrypted);
                    if (encryptedEntries && string.IsNullOrEmpty(options.Password) && CanPromptForExtractionPassword())
                    {
                        options.Password = ReadExtractionPassword(options.ArchivePath, ++passwordAttempts);
                        continue;
                    }

                    using (ProgressBar? progress = CreateAddProgress(100, "Extracting archive..."))
                    {
                        int currentIndex = 0;
                        archive.Extracting += (_, eventArgs) => progress?.Tick(
                            (int)((currentIndex + eventArgs.PercentDone / 100.0) * 100 / entries.Count),
                            entries[currentIndex].FileName);
                        for (int index = 0; index < entries.Count; index++)
                        {
                            currentIndex = index;
                            SevenZip.ArchiveFileInfo entry = entries[index];
                            string relativePath = GetExtractedEntryPath(entry.FileName, options.RestorePaths);
                            string stagingPath = GetSafeExtractionPath(stagingRoot, relativePath);
                            if (entry.IsDirectory)
                            {
                                if (options.RestorePaths)
                                    Directory.CreateDirectory(stagingPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
                                using FileStream output = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None);
                                archive.ExtractFile(entry.Index, output);
                                RestoreExtractedMetadata(stagingPath, entry);
                            }
                            progress?.Tick((index + 1) * 100 / entries.Count, entry.FileName);
                        }
                    }

                    int extractedFiles = CommitStagedExtraction(stagingRoot, options);
                    if (!IsSilent)
                        Console.WriteLine(GetLocalizedString("ExtractCompleted", "Extracted {0} file(s) to {1}", extractedFiles, options.DestinationPath));
                    if (DoOpenFolder && IsWindows())
                        Process.Start(new ProcessStartInfo(options.DestinationPath) { UseShellExecute = true });
                    return ExitCode.Success;
                }
                catch (Exception ex) when (IsLikelyPasswordFailure(ex, encryptedEntries) && CanPromptForExtractionPassword() && passwordAttempts < 3)
                {
                    options.Password = ReadExtractionPassword(options.ArchivePath, ++passwordAttempts);
                }
                catch (Exception ex) when (IsLikelyPasswordFailure(ex, encryptedEntries) && CanPromptForExtractionPassword())
                {
                    throw new InvalidOperationException(
                        GetLocalizedString("ExtractPasswordAttemptsExceeded", "The maximum number of password attempts was exceeded."), ex);
                }
                finally
                {
                    DeleteExtractionStagingFolder(stagingRoot);
                }
            }
        }

        private static int CommitStagedExtraction(string stagingRoot, ExtractCommandOptions options)
        {
            foreach (string directory in Directory.EnumerateDirectories(stagingRoot, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path.Length))
            {
                string relativePath = Path.GetRelativePath(stagingRoot, directory);
                Directory.CreateDirectory(GetSafeExtractionPath(options.DestinationPath, relativePath));
            }

            bool overwriteAll = options.OverwriteMode == 0 || DoAlwaysConfirm;
            bool skipAll = options.OverwriteMode == 1;
            int extractedFiles = 0;
            foreach (string sourcePath in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(stagingRoot, sourcePath);
                string destinationPath = GetSafeExtractionPath(options.DestinationPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                if (File.Exists(destinationPath) && !ResolveExtractionOverwrite(destinationPath, ref overwriteAll, ref skipAll))
                    continue;

                File.Copy(sourcePath, destinationPath, true);
                File.SetLastWriteTime(destinationPath, File.GetLastWriteTime(sourcePath));
                if (IsWindows())
                    File.SetAttributes(destinationPath, File.GetAttributes(sourcePath));
                extractedFiles++;
                if (IsVerbose && !IsSilent)
                    Console.WriteLine(relativePath);
            }
            return extractedFiles;
        }

        private static bool CanPromptForExtractionPassword()
        {
            return !IsSilent && !Console.IsInputRedirected;
        }

        private static string ReadExtractionPassword(string archivePath, int attempt)
        {
            if (attempt > 3)
                throw new InvalidOperationException(GetLocalizedString("ExtractPasswordAttemptsExceeded", "The maximum number of password attempts was exceeded."));

            Console.Write(GetLocalizedString("ExtractPasswordPrompt", "Password required for {0} (attempt {1}/3, Esc to cancel): ", Path.GetFileName(archivePath), attempt));
            StringBuilder password = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    if (password.Length == 0)
                        continue;
                    return password.ToString();
                }
                if (key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    throw new OperationCanceledException();
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                        password.Length--;
                    continue;
                }
                if (!char.IsControl(key.KeyChar))
                    password.Append(key.KeyChar);
            }
        }

        private static bool IsLikelyPasswordFailure(Exception exception, bool encryptedEntries = false)
        {
            // Inspect messages only: stack frames include this password-retry method's name.
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                string message = current.Message;
                if (message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                    (encryptedEntries && (message.Contains("data error", StringComparison.OrdinalIgnoreCase) ||
                                          message.Contains("CRC", StringComparison.OrdinalIgnoreCase))))
                    return true;
            }
            return false;
        }

        private static void DeleteExtractionStagingFolder(string stagingRoot)
        {
            if (!Directory.Exists(stagingRoot))
                return;
            try
            {
                foreach (string file in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(stagingRoot, true);
            }
            catch when (!IsVerbose)
            {
            }
        }

        private static ExtractCommandOptions ParseExtractOptions(string[] args)
        {
            if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]) ||
                args[2].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(GetLocalizedString("ExtractSyntax", "Syntax: zg -extract archive destination [options]"));

            ExtractCommandOptions options = new ExtractCommandOptions
            {
                ArchivePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1])),
                DestinationPath = ResolveExtractionDestination(args[2])
            };

            for (int index = 3; index < args.Length; index++)
                ParseExtractArgument(args[index], options, true);

            return options;
        }

        private static void ParseExtractArgument(string argument, ExtractCommandOptions options, bool allowListFile)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return;
            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (!IsGlobalOption(argument.Substring(2)))
                    throw new ArgumentException($"Unknown option: {argument}");
                return;
            }
            if (argument[0] == '+')
            {
                AddExtractPattern(options.Inclusions, argument.Substring(1));
                return;
            }
            if (argument[0] == '-')
            {
                AddExtractPattern(options.Exclusions, argument.Substring(1));
                return;
            }
            if (argument[0] == '@')
            {
                if (!allowListFile)
                    throw new ArgumentException(GetLocalizedString("ExtractNestedList", "Nested @ list files are not supported."));
                string listPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(argument.Substring(1).Trim('"')));
                if (!File.Exists(listPath))
                    throw new ArgumentException($"List file not found: {listPath}");
                foreach (string line in File.ReadLines(listPath))
                {
                    string item = line.Trim();
                    if (item.Length > 0 && !item.StartsWith('#'))
                        ParseExtractArgument(item, options, false);
                }
                return;
            }

            string option = argument.ToUpperInvariant();
            if (option is "R0" or "R1") options.Recursive = option == "R1";
            else if (option is "F0" or "F1") options.RestorePaths = option == "F1";
            else if (option is "N0" or "N1") options.CreateArchiveSubfolder = option == "N1";
            else if (option is "O0" or "O1" or "O2") options.OverwriteMode = option[1] - '0';
            else if (option is "K0" or "K1") DoShowProgress = option == "K1";
            else if (option.StartsWith('P'))
            {
                string password = argument.Length > 1 ? argument.Substring(1) : string.Empty;
                options.Password = password == "0" ? string.Empty : password;
            }
            else if (option.StartsWith('D')) options.AttributeFilter = ValidateExtractionAttributeFilter(option.Substring(1));
            else throw new ArgumentException($"Unknown option: {argument}");
        }

        private static void AddExtractPattern(List<string> patterns, string pattern)
        {
            string value = pattern.Trim().Trim('"').Replace('\\', '/').TrimStart('/');
            if (value.Length == 0)
                throw new ArgumentException(GetLocalizedString("ExtractEmptyPattern", "An inclusion or exclusion pattern is empty."));
            patterns.Add(value);
        }

        private static string ValidateExtractionAttributeFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                throw new ArgumentException(GetLocalizedString("ExtractNeedAttribute", "The D option requires at least one attribute."));
            if (filter.Any(value => "AHRSZX".IndexOf(value) < 0))
                throw new ArgumentException($"Invalid attribute filter: D{filter}");
            return filter.Contains('X') ? "X" : filter;
        }

        private static bool ShouldExtractEntry(SevenZip.ArchiveFileInfo entry, ExtractCommandOptions options)
        {
            string path = NormalizeExtractionEntryPath(entry.FileName);
            if (path.Length == 0)
                return false;
            if (!options.Recursive && entry.IsDirectory)
                return false;
            if (!options.Recursive && path.Contains('/'))
                return false;
            if (options.Inclusions.Count > 0 && !options.Inclusions.Any(pattern => ExtractionPatternMatches(path, pattern)))
                return false;
            if (options.Exclusions.Any(pattern => ExtractionPatternMatches(path, pattern)))
                return false;
            return entry.IsDirectory || ExtractionAttributesMatch(entry.Attributes, options.AttributeFilter);
        }

        private static ExitCode ExtractSqxFilesWithPasswordRetries(ExtractCommandOptions options)
        {
            int passwordAttempts = 0;
            while (true)
            {
                try
                {
                    return ExtractSqxFiles(options);
                }
                catch (Exception ex) when (IsLikelyPasswordFailure(ex) && CanPromptForExtractionPassword() && passwordAttempts < 3)
                {
                    options.Password = ReadExtractionPassword(options.ArchivePath, ++passwordAttempts);
                }
                catch (Exception ex) when (IsLikelyPasswordFailure(ex) && CanPromptForExtractionPassword())
                {
                    throw new InvalidOperationException(
                        GetLocalizedString("ExtractPasswordAttemptsExceeded", "The maximum number of password attempts was exceeded."), ex);
                }
            }
        }

        private static ExitCode ExtractSqxFiles(ExtractCommandOptions options)
        {
            string stagingRoot = Path.Combine(TempPath, "zgshell-extract-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(stagingRoot);
                SqxNative.ExtractArchive(options.ArchivePath, stagingRoot, options.Password);
                List<string> files = Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories)
                    .Where(file => ShouldExtractStagedFile(Path.GetRelativePath(stagingRoot, file), File.GetAttributes(file), options))
                    .ToList();
                if (files.Count == 0)
                {
                    WriteLocalizedError("ExtractNoMatchingFiles", "No archive entries match the requested filters.");
                    return ExitCode.ZeroItemsArchive;
                }

                bool overwriteAll = options.OverwriteMode == 0 || DoAlwaysConfirm;
                bool skipAll = options.OverwriteMode == 1;
                int extractedFiles = 0;
                using ProgressBar? progress = CreateAddProgress(files.Count, "Extracting SQX archive...");
                for (int index = 0; index < files.Count; index++)
                {
                    string sourcePath = files[index];
                    string archivePath = Path.GetRelativePath(stagingRoot, sourcePath).Replace('\\', '/');
                    string relativePath = GetExtractedEntryPath(archivePath, options.RestorePaths);
                    string destinationPath = GetSafeExtractionPath(options.DestinationPath, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    if (!File.Exists(destinationPath) || ResolveExtractionOverwrite(destinationPath, ref overwriteAll, ref skipAll))
                    {
                        File.Copy(sourcePath, destinationPath, true);
                        File.SetLastWriteTime(destinationPath, File.GetLastWriteTime(sourcePath));
                        if (IsWindows())
                            File.SetAttributes(destinationPath, File.GetAttributes(sourcePath));
                        extractedFiles++;
                    }
                    progress?.Tick(index + 1, archivePath);
                }

                if (!IsSilent)
                    Console.WriteLine(GetLocalizedString("ExtractCompleted", "Extracted {0} file(s) to {1}", extractedFiles, options.DestinationPath));
                if (DoOpenFolder && IsWindows())
                    Process.Start(new ProcessStartInfo(options.DestinationPath) { UseShellExecute = true });
                return ExitCode.Success;
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, true);
            }
        }

        private static bool ShouldExtractStagedFile(string relativePath, FileAttributes attributes, ExtractCommandOptions options)
        {
            string path = NormalizeExtractionEntryPath(relativePath);
            if (!options.Recursive && path.Contains('/'))
                return false;
            if (options.Inclusions.Count > 0 && !options.Inclusions.Any(pattern => ExtractionPatternMatches(path, pattern)))
                return false;
            if (options.Exclusions.Any(pattern => ExtractionPatternMatches(path, pattern)))
                return false;
            return ExtractionAttributesMatch((uint)attributes, options.AttributeFilter);
        }

        private static bool ExtractionPatternMatches(string entryPath, string pattern)
        {
            string normalizedPattern = pattern.Replace('\\', '/').TrimStart('/');
            string expression = "^" + Regex.Escape(normalizedPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            if (Regex.IsMatch(entryPath, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return true;
            return !normalizedPattern.Contains('/') && Regex.IsMatch(Path.GetFileName(entryPath), expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool ExtractionAttributesMatch(uint rawAttributes, string filter)
        {
            if (filter == "X")
                return true;
            FileAttributes attributes = (FileAttributes)rawAttributes;
            bool hasKnownAttributes = (attributes & (FileAttributes.Archive | FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System)) != 0;
            return (filter.Contains('A') && attributes.HasFlag(FileAttributes.Archive)) ||
                   (filter.Contains('H') && attributes.HasFlag(FileAttributes.Hidden)) ||
                   (filter.Contains('R') && attributes.HasFlag(FileAttributes.ReadOnly)) ||
                   (filter.Contains('S') && attributes.HasFlag(FileAttributes.System)) ||
                   (filter.Contains('Z') && !hasKnownAttributes);
        }

        private static string NormalizeExtractionEntryPath(string entryPath)
        {
            return (entryPath ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static string GetExtractedEntryPath(string entryPath, bool restorePaths)
        {
            string normalized = NormalizeExtractionEntryPath(entryPath);
            string result = restorePaths ? normalized : Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidDataException($"Invalid archive entry path: {entryPath}");
            return result.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string GetSafeExtractionPath(string destinationRoot, string relativePath)
        {
            string root = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            string rootPrefix = root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe archive entry path: {relativePath}");
            return candidate;
        }

        private static bool ResolveExtractionOverwrite(string destinationPath, ref bool overwriteAll, ref bool skipAll)
        {
            if (overwriteAll) return true;
            if (skipAll) return false;
            if (IsSilent || Console.IsInputRedirected) return false;

            while (true)
            {
                Console.Write($"File exists: {destinationPath}. [Y]es/[N]o/[A]ll/[S]kip all: ");
                string response = (Console.ReadLine() ?? string.Empty).Trim().ToUpperInvariant();
                if (response is "Y" or "YES") return true;
                if (response is "N" or "NO" or "") return false;
                if (response is "A" or "ALL") { overwriteAll = true; return true; }
                if (response is "S" or "SKIP") { skipAll = true; return false; }
            }
        }

        private static void RestoreExtractedMetadata(string destinationPath, SevenZip.ArchiveFileInfo entry)
        {
            try
            {
                if (entry.LastWriteTime > DateTime.MinValue)
                    File.SetLastWriteTime(destinationPath, entry.LastWriteTime);
                if (IsWindows())
                {
                    FileAttributes attributes = (FileAttributes)entry.Attributes;
                    FileAttributes supported = attributes & (FileAttributes.Archive | FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System);
                    File.SetAttributes(destinationPath, supported == 0 ? FileAttributes.Normal : supported);
                }
            }
            catch when (!IsVerbose)
            {
            }
        }

        private static string ResolveExtractionDestination(string value)
        {
            string expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            if (expanded.Equals("-curr", StringComparison.OrdinalIgnoreCase))
                return Directory.GetCurrentDirectory();

            Dictionary<string, string> placeholders = new(StringComparer.OrdinalIgnoreCase)
            {
                ["[CURR]"] = Directory.GetCurrentDirectory(),
                ["[DOCS]"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ["[MUSIC]"] = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                ["[PICS]"] = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                ["[VIDEO]"] = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                ["[FAV]"] = Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                ["[SYS]"] = Environment.GetFolderPath(Environment.SpecialFolder.System),
                ["[WIN]"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                ["[TEMP]"] = Path.GetTempPath(),
                ["[DESKTOP]"] = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            foreach ((string placeholder, string path) in placeholders)
                expanded = expanded.Replace(placeholder, path, StringComparison.OrdinalIgnoreCase);
            if (Regex.IsMatch(expanded, @"\[[A-Z]+\]", RegexOptions.IgnoreCase))
                throw new ArgumentException($"Unsupported destination placeholder: {expanded}");
            return Path.GetFullPath(expanded);
        }

        private static string GetArchiveBaseName(string archivePath)
        {
            string name = Path.GetFileNameWithoutExtension(archivePath);
            if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);
            return name;
        }
        #endregion

        #region Utilities
        private static void ExportDictToJson(Dictionary<string, string> dict, string OutJsonFile, string arcFile)
        {
            Console.WriteLine("Export to JSON");
            FileInfo fi = new FileInfo(arcFile);
            string fSize = FormatSize(fi.Length);
            string fDate = fi.LastWriteTime.ToUniversalTime().ToString("O");
            string outJson = "\t\"archive\": \"" + Path.GetFileName(arcFile) + "\"," + Environment.NewLine +
                             "\t\"lastwrite\": \"" + fDate + "\"," + Environment.NewLine +
                             "\t\"size\": \"" + fSize + "\"," + Environment.NewLine +
                             "\t\"entries\": [" + Environment.NewLine +
                             "{0}" + Environment.NewLine +
                             "\t]";
            string outString = string.Empty;
            int i = 0;
            foreach (var entry in dict)
            {
                outString += "\t\t{" + Environment.NewLine +
                             "\t\t\t\"file\": \"" + entry.Key + "\"," + Environment.NewLine +
                             "\t\t\t\"sha-256\": \"" + entry.Value + "\"" + Environment.NewLine +
                             "\t\t}";
                i++;
                if (i < dict.Count())
                    outString += ", " + Environment.NewLine;

            }

            //string outString = JsonConvert.SerializeObject(dict, Newtonsoft.Json.Formatting.Indented, JsonSerializerSettings);
            if (!Directory.Exists(Path.GetDirectoryName(OutJsonFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(OutJsonFile));
            string Jresult = "{" + Environment.NewLine + string.Format(outJson, outString) + Environment.NewLine + "}";
            File.WriteAllText(OutJsonFile, Jresult, Encoding.UTF8);
        }

        private static void ExportDictToXml(Dictionary<string, string> dict, string OutJsonFile, string arcFile)
        {

            FileInfo fi = new FileInfo(arcFile);
            string fSize = FormatSize(fi.Length);
            string fDate = fi.LastWriteTime.ToUniversalTime().ToString("O");

            string xmlResult = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine;
            xmlResult += "<info>" + Environment.NewLine +
                         "\t<archive>" + Environment.NewLine +
                         "\t\t" + Path.GetFileName(arcFile) + Environment.NewLine +
                         "\t</archive>" + Environment.NewLine +
                         "\t<size>" + Environment.NewLine +
                         "\t\t" + fSize + Environment.NewLine +
                         "\t</size>" + Environment.NewLine +
                         "\t<lastwrite>" + Environment.NewLine +
                         "\t\t" + fDate + Environment.NewLine +
                         "\t</lastwrite>" + Environment.NewLine +
                         "\t<entries>" + Environment.NewLine +
                         "{0}" +
                         "\t</entries>" + Environment.NewLine +
                         "</info>";



            string outString = string.Empty;
            int i = 0;
            foreach (var entry in dict)
            {
                outString += "\t\t<file>" + Environment.NewLine +
                             "\t\t\t<name>" + Environment.NewLine +
                             "\t\t\t\t" + entry.Key + Environment.NewLine +
                             "\t\t\t</name>" + Environment.NewLine +
                             "\t\t\t<sha-256>" + Environment.NewLine +
                             "\t\t\t\t" + entry.Value + Environment.NewLine +
                             "\t\t\t</sha-256>" + Environment.NewLine +
                             "\t\t</file>";
                i++;
                if (i < dict.Count())
                    outString += Environment.NewLine;
            }

            //string outString = JsonConvert.SerializeObject(dict, Newtonsoft.Json.Formatting.Indented, JsonSerializerSettings);
            if (!Directory.Exists(Path.GetDirectoryName(OutJsonFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(OutJsonFile));
            string Xresult = string.Format(xmlResult, outString);
            File.WriteAllText(OutJsonFile, Xresult, Encoding.UTF8);
        }

        private static void ExportDictToHtml(Dictionary<string, string> dict, string OutHtmlFile, string arcFile)
        {

            FileInfo fi = new FileInfo(arcFile);
            string fSize = FormatSize(fi.Length);
            string fDate = fi.LastWriteTime.ToUniversalTime().ToString("O");


            string hRows = string.Empty;
            int i = 0;
            foreach (var entry in dict)
            {
                hRows += string.Format(Resources.hash_rowtemplate, entry.Key, entry.Value)
                    .Replace("id=\"rowHash\"", "id=\"rowHash" + i.ToString() + "\"")
                    .Replace("elID", "'rowHash" + i.ToString() + "'") + Environment.NewLine;
                i++;
            }
            string outString = Resources.hash_htmltemplate.Replace("{0}", Path.GetFileName(arcFile))
                .Replace("{1}", fSize)
                .Replace("{2}", fDate)
                .Replace("{3}", dict.Count.ToString())
                .Replace("{4}", "SHA-256")
                .Replace("{5}", hRows);
            Console.WriteLine("Exporting to HTML " + OutHtmlFile);

            //string outString = JsonConvert.SerializeObject(dict, Newtonsoft.Json.Formatting.Indented, JsonSerializerSettings);
            if (!Directory.Exists(Path.GetDirectoryName(OutHtmlFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(OutHtmlFile));

            File.WriteAllText(OutHtmlFile, outString, Encoding.UTF8);
        }

        public static bool IsWindows()
        {
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        }
        public static bool IsMac()
        {
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
        }


        public static string CheckSignature(string fName)
        {
            Console.WriteLine(">> " + fName);
            string result = string.Empty;

            using (FileStream inStream = new FileStream(fName, FileMode.Open, FileAccess.Read))
            {

                // Check if PK (4 bytes)
                byte[] buf = new byte[4];
                inStream.Read(buf, 0, buf.Length);
                string sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "50 4B 03 04" ||
                    sBuf == "50 4B 05 06" ||
                    sBuf == "50 4B 07 08")
                    return "PK";
                Array.Clear(buf);

                // Check if LZIP (4 bytes)
                buf = new byte[4];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "4C 5A 49 50")
                    return "LZIP";
                Array.Clear(buf);

                // Check if CAB (4 bytes)
                buf = new byte[4];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "4D 53 43 46" ||
                    sBuf == "49 53 63 28")
                    return "CAB";
                Array.Clear(buf);

                // Check if 7z
                buf = new byte[6];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "37 7A BC AF 27 1C")
                    return "7z";

                // Check if XZ
                buf = new byte[6];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "FD 37 7A 58 5A 00")
                    return "XZ";
                Array.Clear(buf);

                // Check if TAR
                buf = new byte[8];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "75 73 74 61 72 00 30 30" ||
                    sBuf == "75 73 74 61 72 20 20 00")
                    return "TAR";
                Array.Clear(buf);

                // Check if RAR
                buf = new byte[7];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "52 61 72 21 1A 07 00")
                    return "RAR";
                Array.Clear(buf);

                // Check if RAR
                buf = new byte[8];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "52 61 72 21 1A 07 01 00")
                    return "RAR5";
                Array.Clear(buf);

                // Check if VHDX
                buf = new byte[8];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "76 68 64 78 66 69 6C 65")
                    return "VHDX";
                Array.Clear(buf);

                // Check if VHD
                buf = new byte[9];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "63 6F 6E 6E 65 63 74 69 78")
                    return "VHD";
                Array.Clear(buf);

                // Check if VMDK
                buf = new byte[3];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "4B 44 4D")
                    return "VMDK";
                Array.Clear(buf);

                // Check if ARJ
                buf = new byte[2];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "60 EA")
                    return "ARJ";
                Array.Clear(buf);

                // Check if DEB
                buf = new byte[8];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "21 3C 61 72 63 68 3E 0A")
                    return "DEB";
                Array.Clear(buf);

                // Check if CRX
                buf = new byte[4];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "43 72 32 34")
                    return "DEB";
                Array.Clear(buf);

                // Check if GZIP
                buf = new byte[2];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "1F 8B")
                    return "GZIP";
                Array.Clear(buf);

                // Check if ISO
                buf = new byte[5];
                inStream.Position = 0;
                inStream.Read(buf, 0, buf.Length);
                sBuf = BitConverter.ToString(buf).Replace("-", " ");
                Console.WriteLine(sBuf);
                if (sBuf == "43 44 30 30 31")
                    return "ISO";
                Array.Clear(buf);
            }

            return "-NULL-";
        }
        #endregion
    }
}
