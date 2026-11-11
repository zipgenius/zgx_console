using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Zgx.Sfx;

public static partial class Package
{
    public static Session OpenSelf(string? executable = null)
    {
        using var file = File.OpenRead(executable ?? Environment.ProcessPath ?? throw new IOException("Executable path unavailable."));
        // macOS codesign may append its signature after our trailer. Search only a bounded tail.
        byte[] tail = new byte[(int)Math.Min(file.Length, 4 * 1024 * 1024)];
        long tailStart = file.Length - tail.Length;
        file.Position = tailStart;
        file.ReadExactly(tail);
        for (int i = tail.Length - FooterSize; i >= 0; i--)
        {
            if (!tail.AsSpan(i, 8).SequenceEqual(Magic)) continue;
            ReadOnlySpan<byte> footer = tail.AsSpan(i, FooterSize);
            long offset = BinaryPrimitives.ReadInt64LittleEndian(footer[8..]);
            long length = BinaryPrimitives.ReadInt64LittleEndian(footer[16..]);
            long jsonLength = BinaryPrimitives.ReadInt64LittleEndian(footer[24..]);
            long footerPosition = tailStart + i;
            if (BinaryPrimitives.ReadInt64LittleEndian(footer[32..]) != 1 || offset < 1 || length < 1 ||
                jsonLength is < 2 or > 8 * 1024 * 1024 || offset > footerPosition ||
                length > footerPosition - offset || offset + length != footerPosition - jsonLength)
                continue;
            file.Position = offset + length;
            byte[] json = new byte[(int)jsonLength];
            file.ReadExactly(json);
            Options options = JsonSerializer.Deserialize<Options>(json) ?? throw new InvalidDataException("Invalid SFX options.");
            Validate(options);
            string work = Path.Combine(Path.GetTempPath(), "zgx-sfx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(work, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            string payload = Path.Combine(work, Path.GetFileName(options.ArchiveName));
            try
            {
                file.Position = offset;
                using (var output = File.Create(payload))
                {
                    byte[] buffer = new byte[81920];
                    long remaining = length;
                    while (remaining > 0)
                    {
                        int count = file.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
                        if (count == 0) throw new EndOfStreamException();
                        output.Write(buffer, 0, count);
                        remaining -= count;
                    }
                }
                using var input = File.OpenRead(payload);
                if (!Convert.ToHexString(SHA256.HashData(input)).Equals(options.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Damaged SFX archive (checksum mismatch).");
                return new Session(options, work, payload);
            }
            catch { Directory.Delete(work, true); throw; }
        }
        throw new InvalidDataException("This executable does not contain a valid SFX archive.");
    }

}
