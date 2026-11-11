using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Lzw;
using ICSharpCode.SharpZipLib.Tar;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zg
{
    public static class TarArchiveEditor
    {
        public static bool IsTarArchivePath(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                return false;

            string fileName = Path.GetFileName(archivePath);
            return fileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tbz", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tar.zx", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".txz", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".tar.z", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".taz", StringComparison.OrdinalIgnoreCase);
        }

        public static IArchive OpenTarArchive(string archivePath)
        {
            string fileName = Path.GetFileName(archivePath);
            Stream inputStream = File.OpenRead(archivePath);
            Stream tarStream = inputStream;
            FileStream seekableTarStream = null;

            try
            {
                if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
                {
                    tarStream = new SharpCompress.Compressors.Deflate.GZipStream(inputStream, SharpCompress.Compressors.CompressionMode.Decompress);
                }
                else if (fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".tbz", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase))
                {
                    tarStream = new BZip2InputStream(inputStream);
                }
                else if (fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".tar.zx", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".txz", StringComparison.OrdinalIgnoreCase))
                {
                    tarStream = new XZStream(inputStream);
                }
                else if (IsTarZArchivePath(archivePath))
                {
                    tarStream = new LzwInputStream(inputStream)
                    {
                        IsStreamOwner = true
                    };
                    inputStream = null;
                }

                if (!tarStream.CanSeek)
                {
                    seekableTarStream = new FileStream(
                        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tar"),
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        81920,
                        FileOptions.DeleteOnClose);

                    tarStream.CopyTo(seekableTarStream);
                    seekableTarStream.Position = 0;
                    tarStream.Dispose();
                    tarStream = seekableTarStream;
                    seekableTarStream = null;
                    inputStream = null;
                }

                IArchive archive = ArchiveFactory.OpenArchive(tarStream, new ReaderOptions());
                inputStream = null;
                return archive;
            }
            catch
            {
                seekableTarStream?.Dispose();
                if (!ReferenceEquals(tarStream, inputStream))
                    tarStream?.Dispose();
                inputStream?.Dispose();
                return null;
            }
        }

        public static void DeleteEntries(string archivePath, IEnumerable<string> selectedArchivePaths)
        {
            HashSet<string> selectedPaths = selectedArchivePaths
                .Select(NormalizeArchivePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            RewriteArchive(archivePath, selectedPaths, null, null);
        }

        public static void AddOrUpdateEntries(string archivePath, IDictionary<string, string> files, IDictionary<string, string> directories = null)
            => AddOrUpdateEntries(archivePath, files, directories, null);

        public static void AddOrUpdateEntries(
            string archivePath,
            IDictionary<string, string> files,
            IDictionary<string, string> directories,
            Action<string, long, long> progressCallback)
        {
            Dictionary<string, string> normalizedFiles = (files ?? new Dictionary<string, string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && File.Exists(entry.Value))
                .ToDictionary(entry => NormalizeArchivePath(entry.Key), entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> normalizedDirectories = (directories ?? new Dictionary<string, string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => NormalizeArchivePath(entry.Key).TrimEnd('/') + "/", entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            HashSet<string> replacementPaths = normalizedFiles.Keys
                .Concat(normalizedDirectories.Keys.Select(path => path.TrimEnd('/')))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            RewriteArchive(archivePath, replacementPaths, progressCallback, tar =>
            {
                foreach (KeyValuePair<string, string> directory in normalizedDirectories.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                    WriteDirectoryEntry(tar, directory.Key, Directory.Exists(directory.Value) ? Directory.GetLastWriteTime(directory.Value) : DateTime.Now, progressCallback);

                foreach (KeyValuePair<string, string> file in normalizedFiles.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                    WriteFileEntry(tar, file.Key, file.Value, progressCallback);
            });
        }

        private static void RewriteArchive(
            string archivePath,
            HashSet<string> excludedPaths,
            Action<string, long, long> progressCallback,
            Action<TarOutputStream> writeAdditionalEntries)
        {
            string archiveFolder = Path.GetDirectoryName(archivePath) ?? string.Empty;
            string sourceTarPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.source.tar");
            string updatedTarPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.updated.tar");
            string tempArchivePath = Path.Combine(archiveFolder, $"{Path.GetFileName(archivePath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                if (File.Exists(archivePath) && new FileInfo(archivePath).Length > 0)
                    ExtractArchiveToTar(archivePath, sourceTarPath);
                else
                    CreateEmptyTar(sourceTarPath);

                using (FileStream tarFile = File.Create(updatedTarPath))
                using (TarOutputStream tar = new TarOutputStream(tarFile, System.Text.Encoding.UTF8))
                {
                    tar.IsStreamOwner = false;
                    using IArchive sourceArchive = ArchiveFactory.OpenArchive(File.OpenRead(sourceTarPath), new ReaderOptions());
                    foreach (IArchiveEntry sourceEntry in sourceArchive.Entries)
                    {
                        string entryPath = NormalizeArchivePath(sourceEntry.Key);
                        if (string.IsNullOrWhiteSpace(entryPath) || IsSelectedArchivePath(entryPath, excludedPaths))
                            continue;

                        if (sourceEntry.IsDirectory)
                            WriteDirectoryEntry(tar, entryPath.TrimEnd('/') + "/", sourceEntry.LastModifiedTime ?? DateTime.Now, progressCallback);
                        else
                            WriteArchiveEntry(tar, sourceEntry, entryPath, progressCallback);
                    }

                    writeAdditionalEntries?.Invoke(tar);
                    tar.Finish();
                }

                CompressTarToArchive(updatedTarPath, tempArchivePath, archivePath);
                File.Copy(tempArchivePath, archivePath, true);
            }
            finally
            {
                TryDelete(sourceTarPath);
                TryDelete(updatedTarPath);
                TryDelete(tempArchivePath);
            }
        }

        private static void ExtractArchiveToTar(string archivePath, string tarPath)
        {
            string fileName = Path.GetFileName(archivePath);
            if (fileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(archivePath, tarPath, true);
                return;
            }

            using FileStream input = File.OpenRead(archivePath);
            using FileStream output = File.Create(tarPath);
            Stream sourceStream = input;

            try
            {
                if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
                {
                    sourceStream = new SharpCompress.Compressors.Deflate.GZipStream(input, SharpCompress.Compressors.CompressionMode.Decompress);
                }
                else if (fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".tbz", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase))
                {
                    sourceStream = new BZip2InputStream(input);
                }
                else if (fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".tar.zx", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".txz", StringComparison.OrdinalIgnoreCase))
                {
                    sourceStream = new XZStream(input);
                }
                else if (IsTarZArchivePath(archivePath))
                {
                    sourceStream = new LzwInputStream(input)
                    {
                        IsStreamOwner = false
                    };
                }

                sourceStream.CopyTo(output);
            }
            finally
            {
                if (!ReferenceEquals(sourceStream, input))
                    sourceStream.Dispose();
            }
        }

        private static void CreateEmptyTar(string tarPath)
        {
            using FileStream tarFile = File.Create(tarPath);
            using TarOutputStream tar = new TarOutputStream(tarFile, System.Text.Encoding.UTF8);
            tar.Finish();
        }

        private static void CompressTarToArchive(string tarPath, string tempArchivePath, string originalArchivePath)
        {
            string fileName = Path.GetFileName(originalArchivePath);
            if (fileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(tarPath, tempArchivePath, true);
                return;
            }

            if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                using FileStream input = File.OpenRead(tarPath);
                using FileStream output = File.Create(tempArchivePath);
                using GZipOutputStream gzip = new GZipOutputStream(output);
                gzip.SetLevel(6);
                input.CopyTo(gzip);
                gzip.Finish();
                return;
            }

            if (fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".tbz", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase))
            {
                using FileStream input = File.OpenRead(tarPath);
                using FileStream output = File.Create(tempArchivePath);
                using BZip2OutputStream bzip2 = new BZip2OutputStream(output, 6);
                input.CopyTo(bzip2);
                return;
            }

            if (IsTarZArchivePath(originalArchivePath))
            {
                WriteUnixCompressArchive(tarPath, tempArchivePath);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                LibArchiveWriter.Compress(tempArchivePath, new Dictionary<string, string> { [Path.GetFileName(tarPath)] = tarPath },
                    "xz", 5, string.Empty, 0, null);
                return;
            }
            SevenZipBase.SetLibraryPath(FileUtils.GetSevenZipLibs());
            SevenZipCompressor compressor = new SevenZipCompressor
            {
                CompressionLevel = SevenZip.CompressionLevel.Normal,
                CompressionMode = SevenZip.CompressionMode.Create,
                ArchiveFormat = GetSevenZipSingleStreamFormat(originalArchivePath)
            };
            compressor.CompressFiles(tempArchivePath, tarPath);
        }

        private static OutArchiveFormat GetSevenZipSingleStreamFormat(string archivePath)
        {
            string fileName = Path.GetFileName(archivePath);
            if (fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".tar.zx", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".txz", StringComparison.OrdinalIgnoreCase))
                return OutArchiveFormat.XZ;

            return OutArchiveFormat.Tar;
        }

        public static void WriteUnixCompressArchive(string sourcePath, string archivePath)
        {
            const int maxBits = 16;

            using FileStream input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream output = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);

            output.WriteByte(0x1F);
            output.WriteByte(0x9D);
            output.WriteByte(0x80 | maxBits);

            int codeWidth = 9;
            ulong bitBuffer = 0;
            int bitCount = 0;
            int decoderFreeEntry = 256;
            bool wroteFirstCode = false;

            void WriteCode(int code)
            {
                bitBuffer |= ((ulong)code) << bitCount;
                bitCount += codeWidth;

                while (bitCount >= 8)
                {
                    output.WriteByte((byte)(bitBuffer & 0xFF));
                    bitBuffer >>= 8;
                    bitCount -= 8;
                }
            }

            void FlushBits()
            {
                if (bitCount > 0)
                {
                    output.WriteByte((byte)(bitBuffer & 0xFF));
                    bitBuffer = 0;
                    bitCount = 0;
                }
            }

            int value;
            while ((value = input.ReadByte()) != -1)
            {
                WriteCode(value);

                if (wroteFirstCode)
                    decoderFreeEntry++;
                else
                {
                    decoderFreeEntry = 257;
                    wroteFirstCode = true;
                }

                if (decoderFreeEntry > (1 << codeWidth) - 1 && codeWidth < maxBits)
                {
                    FlushBits();
                    codeWidth++;
                }
            }

            FlushBits();
        }

        private static void WriteArchiveEntry(TarOutputStream tar, IArchiveEntry sourceEntry, string entryPath, Action<string, long, long> progressCallback = null)
        {
            TarEntry targetEntry = TarEntry.CreateTarEntry(entryPath);
            targetEntry.Size = sourceEntry.Size;
            targetEntry.ModTime = sourceEntry.LastModifiedTime ?? DateTime.Now;
            tar.PutNextEntry(targetEntry);
            using Stream sourceStream = sourceEntry.OpenEntryStream();
            CopyStreamWithProgress(sourceStream, tar, entryPath, progressCallback);
            tar.CloseEntry();
        }

        private static void WriteFileEntry(TarOutputStream tar, string entryPath, string sourcePath, Action<string, long, long> progressCallback = null)
        {
            FileInfo fileInfo = new FileInfo(sourcePath);
            TarEntry entry = TarEntry.CreateTarEntry(entryPath);
            entry.Size = fileInfo.Length;
            entry.ModTime = fileInfo.LastWriteTime;
            tar.PutNextEntry(entry);
            using FileStream sourceStream = File.OpenRead(sourcePath);
            CopyStreamWithProgress(sourceStream, tar, entryPath, progressCallback);
            tar.CloseEntry();
        }

        private static void WriteDirectoryEntry(TarOutputStream tar, string entryPath, DateTime modifiedTime, Action<string, long, long> progressCallback = null)
        {
            string normalizedPath = NormalizeArchivePath(entryPath).TrimEnd('/') + "/";
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return;

            TarEntry entry = TarEntry.CreateTarEntry(normalizedPath);
            entry.ModTime = modifiedTime;
            tar.PutNextEntry(entry);
            tar.CloseEntry();
            progressCallback?.Invoke(normalizedPath, 1, 1);
        }

        private static void CopyStreamWithProgress(Stream source, Stream destination, string entryPath, Action<string, long, long> progressCallback)
        {
            if (progressCallback == null)
            {
                source.CopyTo(destination);
                return;
            }

            byte[] buffer = new byte[81920];
            long total = source.CanSeek ? source.Length : 0;
            long readTotal = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, read);
                readTotal += read;
                progressCallback(entryPath, readTotal, total);
            }

            progressCallback(entryPath, total > 0 ? total : readTotal, total > 0 ? total : readTotal);
        }

        private static bool IsSelectedArchivePath(string entryName, IEnumerable<string> selectedArchivePaths)
        {
            string normalizedEntryName = NormalizeArchivePath(entryName).TrimEnd('/');
            foreach (string selectedPath in selectedArchivePaths)
            {
                string normalizedSelectedPath = NormalizeArchivePath(selectedPath).TrimEnd('/');
                if (normalizedEntryName.Equals(normalizedSelectedPath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedEntryName.StartsWith(normalizedSelectedPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTarZArchivePath(string archivePath)
        {
            string fileName = Path.GetFileName(archivePath);
            return fileName.EndsWith(".tar.z", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".taz", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeArchivePath(string path)
        {
            return (path ?? string.Empty)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Trim('/');
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
