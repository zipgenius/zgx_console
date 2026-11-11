using System;
using System.IO;

namespace zg
{
    internal static class SqxCompressor
    {
        public static void CreateArchive(string archivePath, string sourceFolder, int compressionRate, ulong volumeSize = 0, bool solidCompression = true, string password = "")
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                return;

            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
                return;

            SqxNative.CreateArchive(archivePath, sourceFolder, compressionRate, volumeSize, solidCompression, password);
        }
    }
}
