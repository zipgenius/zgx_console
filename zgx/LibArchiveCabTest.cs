using System.Runtime.InteropServices;

namespace zg;

// Verify CAB data by streaming it to memory, never creating extracted files.
internal static class LibArchiveCabTest
{
    private const string Library = "libarchive.so.13";
    internal static int Verify(string path)
    {
        IntPtr archive = IntPtr.Zero;
        IntPtr locale = IntPtr.Zero, previousLocale = IntPtr.Zero;
        try
        {
            locale = newlocale(1, "C.UTF-8", IntPtr.Zero);
            if (locale == IntPtr.Zero) throw new NotSupportedException("CAB testing requires the C.UTF-8 locale.");
            previousLocale = uselocale(locale);
            if (previousLocale == IntPtr.Zero) throw new IOException("Could not activate a UTF-8 locale for CAB testing.");
            archive = archive_read_new();
            if (archive == IntPtr.Zero) throw new OutOfMemoryException();
            Check(archive, archive_read_support_filter_all(archive));
            Check(archive, archive_read_support_format_cab(archive));
            Check(archive, archive_read_open_filename(archive, path, (nuint)10240));
            byte[] buffer = new byte[81920];
            int count = 0;
            while (true)
            {
                int status = archive_read_next_header(archive, out IntPtr entry);
                if (status == 1) break; // ARCHIVE_EOF
                Check(archive, status);
                if (entry == IntPtr.Zero) throw new InvalidDataException("Missing CAB entry metadata.");
                long expected = archive_entry_size(entry), total = 0;
                while (true)
                {
                    nint read = archive_read_data(archive, buffer, (nuint)buffer.Length);
                    if (read == 0) break;
                    if (read < 0) throw Error(archive);
                    total = checked(total + (long)read);
                }
                if (expected >= 0 && total != expected) throw new InvalidDataException("CAB entry size does not match.");
                count++;
            }
            return count;
        }
        catch (DllNotFoundException ex) { throw new NotSupportedException("CAB testing requires libarchive.so.13.", ex); }
        finally
        {
            if (archive != IntPtr.Zero) archive_read_free(archive);
            if (previousLocale != IntPtr.Zero) uselocale(previousLocale);
            if (locale != IntPtr.Zero) freelocale(locale);
        }
    }
    private static void Check(IntPtr archive, int result) { if (result < 0) throw Error(archive); }
    private static InvalidDataException Error(IntPtr archive) => new(Marshal.PtrToStringUTF8(archive_error_string(archive)) ?? "CAB reading failed.");
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr archive_read_new();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_read_support_filter_all(IntPtr archive);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_read_support_format_cab(IntPtr archive);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_read_open_filename(IntPtr archive, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, nuint blockSize);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_read_next_header(IntPtr archive, out IntPtr entry);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint archive_read_data(IntPtr archive, byte[] buffer, nuint size);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern long archive_entry_size(IntPtr entry);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr archive_error_string(IntPtr archive);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_read_free(IntPtr archive);
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr newlocale(int mask, string locale, IntPtr existing);
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr uselocale(IntPtr locale);
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)] private static extern void freelocale(IntPtr locale);
}
