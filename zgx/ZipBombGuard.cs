using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Zip;

namespace zg;

// Pure CLI counterpart of the GUI scoring and bounded nested-ZIP scanner.
// No preferences or command-line switches can disable this guard.
internal static class ZipBombGuard
{
    private static readonly string[] ZipExtensions =
    [
        ".zip", ".xpi", ".apk", ".odt", ".ods", ".odp", ".odg", ".otg", ".otp", ".odm", ".odf", ".odb", ".ots", ".ott", ".oxt",
        ".docx", ".xlsx", ".pptx", ".cbz", ".mskin", ".wal", ".dsfz", ".wmz", ".pcv", ".bsz", ".wsz", ".pak", ".pk3",
        ".eopkg", ".vsix", ".msixbundle", ".msix", ".jar", ".war", ".ear", ".appx"
    ];
    internal sealed record Result(int Score, bool Incomplete, bool Recursive = false)
    {
        public int ExitCode => Score >= 60 ? 42 : Incomplete ? 43 : Score >= 40 ? 41 : 0;
        public bool Blocked => Score >= 60 || Incomplete;
    }

    internal static Result Inspect(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[4];
            int count = stream.Read(signature);
            bool zipSignature = count == 4 && signature[0] == 0x50 && signature[1] == 0x4b &&
                ((signature[2] == 3 && signature[3] == 4) ||
                 (signature[2] == 5 && signature[3] == 6) || (signature[2] == 7 && signature[3] == 8));
            // Also find renamed/self-extracting ZIPs by their end-of-directory record.
            if (!zipSignature && stream.Length >= 22)
            {
                byte[] tail = new byte[(int)Math.Min(65557, stream.Length)];
                stream.Position = stream.Length - tail.Length;
                stream.ReadExactly(tail);
                for (int offset = tail.Length - 22; offset >= 0; offset--)
                    if (BitConverter.ToUInt32(tail, offset) == 0x06054b50 &&
                        offset + 22 + BitConverter.ToUInt16(tail, offset + 20) == tail.Length)
                    {
                        zipSignature = true;
                        break;
                    }
            }
            if (!zipSignature && !ZipExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                return new Result(0, false);
            return Scan(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ZipException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return new Result(20, true);
        }
    }

    private static Result Scan(string path)
    {
            var deadline = Stopwatch.StartNew();
            using (var input = File.OpenRead(path))
            {
                if (input.Length <= MaximumNestedZipBytesToInspect)
                {
                    using var data = new MemoryStream();
                    input.CopyTo(data);
                    if (IsSelfReplicatingZip(data) || TryGetOverlappingZipPayload(data, out _))
                        return new Result(99, false);
                }
            }
            int score = 0;

            using (ZipFile zip = new ZipFile(path))
            {
                var uncompressedSizeCounts = new Dictionary<long, int>();
                var compressionRatioCounts = new Dictionary<double, int>();
                var crcCounts = new Dictionary<long, long>();

                int nestedZip = 0;
                long sameCrc = 0;
                int sameSize = 0;
                int sameRatio = 0;
                bool IsNested = false;
                long GlobalUnpackedSize = 0;
                bool totalSizeOverflow = false;
                int nonEmptyFileCount = 0;
                int extremelyCompressedFileCount = 0;
                int fileCount = 0;

                foreach (ZipEntry entry in zip)
                {
                    if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                        return new Result(20, true);
                    if (entry.IsDirectory)
                        continue;
                    fileCount++;
                    if (entry.Size < 0 || entry.CompressedSize < 0)
                        return new Result(20, true);

                    try
                    {
                        GlobalUnpackedSize = checked(GlobalUnpackedSize + entry.Size);
                    }
                    catch (OverflowException)
                    {
                        GlobalUnpackedSize = long.MaxValue;
                        totalSizeOverflow = true;
                    }
                    long uncompressedSize = entry.Size;
                    double compressionRatio = GetRatio(entry.CompressedSize, entry.Size);
                    long crc = entry.Crc;
                    if (uncompressedSize > 0)
                    {
                        nonEmptyFileCount++;
                        if (entry.CompressedSize > 0 && uncompressedSize / entry.CompressedSize >= 1000)
                            extremelyCompressedFileCount++;
                    }
                    if (Path.GetExtension(entry.Name).Equals(".zip", StringComparison.CurrentCultureIgnoreCase))
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
                }

                foreach (var pair in uncompressedSizeCounts)
                {
                    if (pair.Value > 1)
                    {
                        sameSize += pair.Value;
                    }
                }

                foreach (var pair in compressionRatioCounts)
                {
                    if (pair.Value > 1)
                    {
                        sameRatio += pair.Value;
                    }
                }

                foreach (var pair in crcCounts)
                {
                    if (pair.Value > 1)
                    {
                        sameCrc += pair.Value;
                    }
                }

                // Compare distinct values, not totals of repeated entries: two
                // different groups of eight must not look like sixteen identical ZIPs.
                bool repeatedNestedZipPayload = IsRepeatedNestedZipPayload(
                    fileCount, nestedZip, nonEmptyFileCount,
                    uncompressedSizeCounts.Count, compressionRatioCounts.Count, crcCounts.Count);

                int DepthCount = 0;
                BigInteger PayloadSize = BigInteger.Zero;
                bool nestedScanIncomplete = false;
                bool uninspectedLargeHighlyCompressedNestedZip = false;
                bool recursiveZipQuine = false;
                NestedZipScanContext nestedScanContext = new NestedZipScanContext();
                foreach (ZipEntry fEntry in zip)
                {
                    if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                        return new Result(Math.Max(20, score), true);
                    // The outer headers already establish this high-risk pattern.
                    // Do not expand its nested payload just to estimate its size.
                    if (repeatedNestedZipPayload)
                        break;
                    if (fEntry.IsDirectory)
                        continue;
                    if (fEntry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {   

                        //    string pswd = ReadPassword();
                        //    zip.Password = pswd
                        
                        ScanNestedZip(zip, fEntry, 1, nestedScanContext, out int nestedDepth, out BigInteger nestedPayload);
                        var nestedResult = (Depth: nestedDepth, Payload: nestedPayload);
                        DepthCount = Math.Max(DepthCount, nestedResult.Depth);
                        if (nestedResult.Payload == -2)
                        {
                            recursiveZipQuine = true;
                            break;
                        }

                        if (nestedResult.Payload < 0)
                        {
                            nestedScanIncomplete = true;
                            uninspectedLargeHighlyCompressedNestedZip |= fEntry.Size >= 64L * 1024 * 1024 &&
                                GetRatio(fEntry.CompressedSize, fEntry.Size) >= 10;
                            continue;
                        }

                        PayloadSize += nestedResult.Payload;
                    }

                }
                if (recursiveZipQuine) return new Result(99, false, true);

                IsNested = (DepthCount >= 2);
                
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
                    if (DepthCount >= 2 && DepthCount < 6)
                        score += 15;
                    else if (DepthCount >= 6)
                    {
                        score += 33;
                    }
                    if (PayloadSize >= 1048576 && PayloadSize < 1073741824)
                        score += 15;
                    else if (PayloadSize >= 1073741824)
                    {
                        score += 33;
                    }
                }
                else
                {
                    score += ScoreFlatZipBombRisk(sameCrc, sameSize, sameRatio, GlobalUnpackedSize);
                }

                if (PayloadSize >= 34359738368)
                    score = Math.Max(score, 80);
                else if (PayloadSize >= 10737418240 || totalSizeOverflow)
                    score = Math.Max(score, 60);
                else if (PayloadSize >= 4294967296)
                    score = Math.Max(score, 40);

                // A flat archive can be a zipbomb too. Do not require nested ZIPs
                // when every non-empty entry expands by at least three orders of
                // magnitude and the resulting payload is already material.
                bool allFilesExtremelyCompressed = nonEmptyFileCount > 0 &&
                    nonEmptyFileCount == extremelyCompressedFileCount &&
                    GlobalUnpackedSize >= 1024L * 1024 * 1024;
                if (allFilesExtremelyCompressed)
                    score = Math.Max(score, 60);

                // Do not classify a large, extremely compressed nested archive as safe
                // merely because the bounded inspection could not finish.
                if (uninspectedLargeHighlyCompressedNestedZip)
                    score = Math.Max(score, 40);

                bool nestedArchivesNotEstimated = nestedScanIncomplete && score < 40;
                if (nestedArchivesNotEstimated)
                    score = 20;

                if (repeatedNestedZipPayload)
                    score = 99;


                return new Result(Math.Min(99, score), nestedScanIncomplete || totalSizeOverflow);
            }
        }
        private static double GetRatio(long packed, long original) =>
            original > 0 && packed > 0 ? Math.Abs(Math.Round((1 - (double)packed / original) * 100)) : 0;
        internal static bool IsRepeatedNestedZipPayload(int fileCount, int nestedZipCount,
            int nonEmptyFileCount, int distinctSizes, int distinctRatios, int distinctCrcs)
        {
            // CRC zero is a valid repeated value; zero alone is not evidence.
            return fileCount >= 4 && nestedZipCount == fileCount && nonEmptyFileCount == fileCount &&
                distinctSizes == 1 && distinctRatios == 1 && distinctCrcs == 1;
        }

        // Repetition strengthens the evidence; archive size or a common compression
        // ratio alone is normal for large installers and media archives.
        internal static int ScoreFlatZipBombRisk(long sameCrc, int sameSize, int sameRatio, long unpackedSize)
        {
            int score = 0;
            bool hasRepeatedContent = sameCrc >= 4 || sameSize >= 4;
            if (sameCrc >= 4)
                score += sameCrc < 8 ? 5 : 10;
            if (sameSize >= 4)
                score += sameSize < 8 ? 5 : 10;
            if (hasRepeatedContent && sameRatio >= 4)
                score += sameRatio < 8 ? 5 : 10;
            if (hasRepeatedContent && sameCrc == sameRatio && sameRatio == sameSize)
                score += 15;

            if (unpackedSize >= 34359738368L)
                score += hasRepeatedContent ? 80 : 20;
            else if (unpackedSize >= 10737418240L)
                score += hasRepeatedContent ? 60 : 15;
            else if (unpackedSize >= 4294967296L)
                score += hasRepeatedContent ? 40 : 10;
            return score;
        }

        private const int MaximumNestedZipDepth = 50;
        // A nested archive can legitimately exceed 64 MiB while still being small enough
        // to inspect safely. Keep a hard cap, but avoid treating that former threshold
        // as a low-risk result by itself.
        private const long MaximumNestedZipBytesToInspect = 128L * 1024 * 1024;

        private sealed class NestedZipScanContext
        {
            private const int MaximumArchiveNodes = 512;
            private const long MaximumTotalBytesToInspect = 256L * 1024 * 1024;
            private static readonly TimeSpan MaximumScanDuration = TimeSpan.FromSeconds(5);

            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            private int archiveNodes;
            private long inspectedBytes;

            public Dictionary<string, (BigInteger Payload, int AdditionalDepth)> Cache { get; } = new Dictionary<string, (BigInteger Payload, int AdditionalDepth)>(StringComparer.Ordinal);

            public bool TryRegisterArchive(long archiveSize)
            {
                archiveNodes++;
                if (archiveNodes > MaximumArchiveNodes || stopwatch.Elapsed > MaximumScanDuration)
                    return false;

                try
                {
                    inspectedBytes = checked(inspectedBytes + archiveSize);
                }
                catch (OverflowException)
                {
                    return false;
                }

                return inspectedBytes <= MaximumTotalBytesToInspect;
            }

            public bool CanContinueReading(long bytesReadForCurrentArchive)
            {
                return stopwatch.Elapsed <= MaximumScanDuration &&
                    bytesReadForCurrentArchive <= MaximumTotalBytesToInspect - inspectedBytes;
            }
        }

        private static BigInteger ScanNestedZip(ZipFile sourceZip, ZipEntry entry, int depth, NestedZipScanContext context, out int depthCount, out BigInteger payloadSize)
        {
            return ScanNestedZip(sourceZip, entry, depth, context, new HashSet<string>(StringComparer.Ordinal), out depthCount, out payloadSize);
        }

        private static BigInteger ScanNestedZip(ZipFile sourceZip, ZipEntry entry, int depth, NestedZipScanContext context, HashSet<string> ancestorFingerprints, out int depthCount, out BigInteger payloadSize)
        {
            depthCount = depth;
            payloadSize = BigInteger.Zero;

            if (depth >= MaximumNestedZipDepth || entry.IsCrypted || entry.Size > MaximumNestedZipBytesToInspect)
            {
                payloadSize = -1;
                return -1;
            }

            try
            {
                using Stream input = sourceZip.GetInputStream(entry);
                using MemoryStream nestedZipStream = new MemoryStream();
                byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
                try
                {
                    int bytesRead;
                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (nestedZipStream.Length > MaximumNestedZipBytesToInspect - bytesRead ||
                            !context.CanContinueReading(nestedZipStream.Length + bytesRead))
                        {
                            payloadSize = -1;
                            return -1;
                        }

                        nestedZipStream.Write(buffer, 0, bytesRead);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                string archiveFingerprint = Convert.ToHexString(SHA256.HashData(nestedZipStream.ToArray()));
                if (ancestorFingerprints.Contains(archiveFingerprint))
                {
                    payloadSize = -2;
                    return -2;
                }

                if (context.Cache.TryGetValue(archiveFingerprint, out var cachedResult))
                {
                    depthCount = depth + cachedResult.AdditionalDepth;
                    payloadSize = cachedResult.Payload;
                    return payloadSize;
                }

                if (!context.TryRegisterArchive(nestedZipStream.Length))
                {
                    payloadSize = -1;
                    return -1;
                }

                ancestorFingerprints.Add(archiveFingerprint);

                nestedZipStream.Position = 0;
                if (TryGetOverlappingZipPayload(nestedZipStream, out BigInteger overlappingPayload))
                {
                    payloadSize = overlappingPayload;
                    context.Cache[archiveFingerprint] = (payloadSize, 0);
                    return payloadSize;
                }

                nestedZipStream.Position = 0;
                if (IsSelfReplicatingZip(nestedZipStream))
                {
                    payloadSize = -2;
                    return -2;
                }

                nestedZipStream.Position = 0;
                using ZipFile nestedZip = new ZipFile(nestedZipStream);
                foreach (ZipEntry nestedEntry in nestedZip)
                {
                    if (nestedEntry.IsDirectory || nestedEntry.Size < 0)
                        continue;

                    if (nestedEntry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        int childDepth;
                        BigInteger childPayload;
                        ScanNestedZip(
                            nestedZip,
                            nestedEntry,
                            depth + 1,
                            context,
                            new HashSet<string>(ancestorFingerprints, StringComparer.Ordinal),
                            out childDepth,
                            out childPayload);
                        depthCount = Math.Max(depthCount, childDepth);
                        if (childPayload == -2)
                        {
                            payloadSize = -2;
                            return -2;
                        }

                        if (childPayload < 0)
                        {
                            payloadSize = -1;
                            return -1;
                        }

                        payloadSize += childPayload;
                    }
                    else
                    {
                        payloadSize = checked(payloadSize + nestedEntry.Size);
                    }
                }

                context.Cache[archiveFingerprint] = (payloadSize, depthCount - depth);
                return payloadSize;
            }
            catch (Exception)
            {
                payloadSize = -1;
                return -1;
            }
        }
        private static bool IsSelfReplicatingZip(MemoryStream archiveStream)
        {
            byte[] data = archiveStream.ToArray();
            if (data.Length < 22)
                return false;

            int eocdOffset = -1;
            for (int offset = data.Length - 22; offset >= Math.Max(0, data.Length - 65557); offset--)
            {
                if (BitConverter.ToUInt32(data, offset) == 0x06054b50)
                {
                    eocdOffset = offset;
                    break;
                }
            }

            if (eocdOffset < 0 || eocdOffset + 22 > data.Length)
                return false;

            int entryCount = BitConverter.ToUInt16(data, eocdOffset + 10);
            uint centralDirectoryOffset = BitConverter.ToUInt32(data, eocdOffset + 16);
            if (entryCount < 2 || centralDirectoryOffset >= data.Length)
                return false;

            int centralOffset = (int)centralDirectoryOffset;
            int selfReplicatingEntries = 0;
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                if (centralOffset < 0 || centralOffset + 46 > data.Length || BitConverter.ToUInt32(data, centralOffset) != 0x02014b50)
                    return false;

                ushort nameLength = BitConverter.ToUInt16(data, centralOffset + 28);
                ushort extraLength = BitConverter.ToUInt16(data, centralOffset + 30);
                ushort commentLength = BitConverter.ToUInt16(data, centralOffset + 32);
                uint localHeaderOffset = BitConverter.ToUInt32(data, centralOffset + 42);
                if (localHeaderOffset > data.Length - 30 || BitConverter.ToUInt32(data, (int)localHeaderOffset) != 0x04034b50)
                    return false;

                uint localUncompressedSize = BitConverter.ToUInt32(data, (int)localHeaderOffset + 22);
                if (localUncompressedSize == data.Length)
                    selfReplicatingEntries++;

                centralOffset += 46 + nameLength + extraLength + commentLength;
            }

            return selfReplicatingEntries >= 2;
        }

        private static bool TryGetOverlappingZipPayload(MemoryStream archiveStream, out BigInteger payloadSize)
        {
            payloadSize = BigInteger.Zero;
            byte[] data = archiveStream.ToArray();
            if (data.Length < 22)
                return false;

            int eocdOffset = -1;
            for (int offset = data.Length - 22; offset >= Math.Max(0, data.Length - 65557); offset--)
            {
                if (BitConverter.ToUInt32(data, offset) == 0x06054b50)
                {
                    eocdOffset = offset;
                    break;
                }
            }

            if (eocdOffset < 0)
                return false;

            int entryCount = BitConverter.ToUInt16(data, eocdOffset + 10);
            int centralOffset = (int)BitConverter.ToUInt32(data, eocdOffset + 16);
            int centralDirectoryOffset = centralOffset;
            int overlappingEntries = 0;
            Dictionary<uint, int> invalidLocalOffsets = new Dictionary<uint, int>();
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                if (centralOffset < 0 || centralOffset + 46 > data.Length || BitConverter.ToUInt32(data, centralOffset) != 0x02014b50)
                    return false;

                uint centralUncompressedSize = BitConverter.ToUInt32(data, centralOffset + 24);
                ushort nameLength = BitConverter.ToUInt16(data, centralOffset + 28);
                ushort extraLength = BitConverter.ToUInt16(data, centralOffset + 30);
                ushort commentLength = BitConverter.ToUInt16(data, centralOffset + 32);
                uint localHeaderOffset = BitConverter.ToUInt32(data, centralOffset + 42);
                if (localHeaderOffset > data.Length - 30 || BitConverter.ToUInt32(data, (int)localHeaderOffset) != 0x04034b50)
                {
                    invalidLocalOffsets.TryGetValue(localHeaderOffset, out int referenceCount);
                    invalidLocalOffsets[localHeaderOffset] = referenceCount + 1;
                    centralOffset += 46 + nameLength + extraLength + commentLength;
                    continue;
                }

                ushort localNameLength = BitConverter.ToUInt16(data, (int)localHeaderOffset + 26);
                ushort localExtraLength = BitConverter.ToUInt16(data, (int)localHeaderOffset + 28);
                int extraOffset = (int)localHeaderOffset + 30 + localNameLength;
                ulong localUncompressedSize = BitConverter.ToUInt32(data, (int)localHeaderOffset + 22);
                int extraEnd = extraOffset + localExtraLength;
                while (extraOffset + 4 <= extraEnd && extraEnd <= data.Length)
                {
                    ushort extraId = BitConverter.ToUInt16(data, extraOffset);
                    ushort extraDataLength = BitConverter.ToUInt16(data, extraOffset + 2);
                    if (extraOffset + 4 + extraDataLength > extraEnd)
                        return false;

                    if (extraId == 0x0001 && extraDataLength >= 8)
                    {
                        localUncompressedSize = BitConverter.ToUInt64(data, extraOffset + 4);
                        break;
                    }

                    extraOffset += 4 + extraDataLength;
                }

                if (localUncompressedSize > centralUncompressedSize)
                {
                    overlappingEntries++;
                    payloadSize += new BigInteger(localUncompressedSize);
                }

                centralOffset += 46 + nameLength + extraLength + commentLength;
            }

            if (overlappingEntries >= 2)
                return true;

            // Some deliberately malformed bombs use the same invalid central-directory
            // offset for thousands of entries while retaining the real local header in
            // the archive data.  Locate that header without inflating any entry.
            int repeatedInvalidReferences = invalidLocalOffsets.Count == 0 ? 0 : invalidLocalOffsets.Values.Max();
            if (repeatedInvalidReferences < 2)
                return false;

            int localDataEnd = centralDirectoryOffset;
            for (int localHeaderOffset = 0; localHeaderOffset <= localDataEnd - 30; localHeaderOffset++)
            {
                if (BitConverter.ToUInt32(data, localHeaderOffset) != 0x04034b50 ||
                    !TryReadLocalZipUncompressedSize(data, localHeaderOffset, out ulong localUncompressedSize) ||
                    localUncompressedSize == 0)
                    continue;

                payloadSize = new BigInteger(localUncompressedSize) * repeatedInvalidReferences;
                return true;
            }

            return false;
        }

        private static bool TryReadLocalZipUncompressedSize(byte[] data, int localHeaderOffset, out ulong uncompressedSize)
        {
            uncompressedSize = 0;
            if (localHeaderOffset < 0 || localHeaderOffset + 30 > data.Length ||
                BitConverter.ToUInt32(data, localHeaderOffset) != 0x04034b50)
                return false;

            ushort nameLength = BitConverter.ToUInt16(data, localHeaderOffset + 26);
            ushort extraLength = BitConverter.ToUInt16(data, localHeaderOffset + 28);
            int extraOffset = localHeaderOffset + 30 + nameLength;
            int extraEnd = extraOffset + extraLength;
            if (extraOffset < 0 || extraEnd < extraOffset || extraEnd > data.Length)
                return false;

            uncompressedSize = BitConverter.ToUInt32(data, localHeaderOffset + 22);
            while (extraOffset + 4 <= extraEnd)
            {
                ushort extraId = BitConverter.ToUInt16(data, extraOffset);
                ushort extraDataLength = BitConverter.ToUInt16(data, extraOffset + 2);
                if (extraOffset + 4 + extraDataLength > extraEnd)
                    return false;

                if (extraId == 0x0001 && extraDataLength >= 8)
                {
                    uncompressedSize = BitConverter.ToUInt64(data, extraOffset + 4);
                    break;
                }

                extraOffset += 4 + extraDataLength;
            }

            return true;
        }




}

