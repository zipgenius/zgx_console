using System.Runtime.InteropServices;

namespace zg;

// libarchive C API: archive.h and archive_entry.h. No child processes are used.
internal static class LibArchiveWriter
{
    private const string Library = "libarchive.so.13";

    internal static void Compress(string destination, IReadOnlyDictionary<string, string> files,
        string format, int level, string password, long volumeSize, Action<int>? progress)
    {
        if (!string.IsNullOrEmpty(password))
            throw new NotSupportedException("Encrypted 7Z creation is not supported by the Linux library. Use ZIP for encrypted output.");
        if (volumeSize != 0)
            throw new NotSupportedException("Split archive creation is not supported by the Linux library.");
        if (File.Exists(destination))
            throw new NotSupportedException("Updating an existing 7Z archive is not supported by the Linux library.");
        IntPtr archive;
        try { archive = archive_write_new(); }
        catch (DllNotFoundException ex)
        {
            throw new NotSupportedException("Creating 7Z/XZ archives requires the libarchive shared library (libarchive.so.13).", ex);
        }
        if (archive == IntPtr.Zero) throw new OutOfMemoryException();
        // .NET does not initialize the C locale. Use a thread-local UTF-8 locale
        // so libarchive can convert Unicode names to the 7Z UTF-16 header.
        IntPtr locale = newlocale(1, "C.UTF-8", IntPtr.Zero);
        if (locale == IntPtr.Zero)
        {
            archive_write_free(archive);
            throw new NotSupportedException("A UTF-8 C locale is required for archive creation.");
        }
        IntPtr previousLocale = uselocale(locale);
        if (previousLocale == IntPtr.Zero)
        {
            freelocale(locale);
            archive_write_free(archive);
            throw new IOException("Could not activate the UTF-8 locale for archive creation.");
        }
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (format == "7z")
            {
                Check(archive, archive_write_set_format_7zip(archive));
                Check(archive, archive_write_set_options(archive, level == 0 ? "compression=copy" : "compression=lzma2,compression-level=" + Math.Clamp(level, 1, 9)));
            }
            else if (format == "xz")
            {
                Check(archive, archive_write_set_format_raw(archive));
                Check(archive, archive_write_add_filter_xz(archive));
            }
            else throw new NotSupportedException($"Linux library cannot create {format} archives.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            Check(archive, archive_write_open_filename(archive, temporary));
            byte[] buffer = new byte[81920];
            int completed = 0;
            foreach (var pair in files)
            {
                bool directory = Directory.Exists(pair.Value);
                IntPtr entry = archive_entry_new();
                if (entry == IntPtr.Zero) throw new OutOfMemoryException();
                try
                {
                    archive_entry_set_pathname_utf8(entry, pair.Key.Replace('\\', '/'));
                    archive_entry_set_filetype(entry, directory ? 0x4000u : 0x8000u);
                    archive_entry_set_perm(entry, directory ? 493u : 420u);
                    long size = directory ? 0 : new FileInfo(pair.Value).Length;
                    archive_entry_set_size(entry, size);
                    archive_entry_set_mtime(entry, (nint)new DateTimeOffset(File.GetLastWriteTimeUtc(pair.Value)).ToUnixTimeSeconds(), 0);
                    Check(archive, archive_write_header(archive, entry));
                    if (!directory)
                    {
                        using FileStream input = File.OpenRead(pair.Value);
                        long copied = 0;
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            nint written = archive_write_data(archive, buffer, (nuint)count);
                            if (written != count) throw new IOException(Error(archive));
                            copied += count;
                            progress?.Invoke((int)((completed + (size > 0 ? (double)copied / size : 1)) * 99 / Math.Max(1, files.Count)));
                        }
                    }
                    Check(archive, archive_write_finish_entry(archive));
                }
                finally { archive_entry_free(entry); }
                completed++;
            }
            Check(archive, archive_write_close(archive));
            File.Move(temporary, destination);
            progress?.Invoke(100);
        }
        finally
        {
            archive_write_free(archive);
            uselocale(previousLocale);
            freelocale(locale);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr newlocale(int mask, string locale, IntPtr existing);
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr uselocale(IntPtr locale);
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)] private static extern void freelocale(IntPtr locale);
    private static string Error(IntPtr archive) => Marshal.PtrToStringUTF8(archive_error_string(archive)) ?? "Archive creation failed.";
    private static void Check(IntPtr archive, int result) { if (result < 0) throw new IOException(Error(archive)); }
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr archive_write_new();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_set_format_7zip(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_set_format_raw(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_add_filter_xz(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_set_options(IntPtr a, [MarshalAs(UnmanagedType.LPUTF8Str)] string options);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_open_filename(IntPtr a, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_header(IntPtr a, IntPtr entry);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint archive_write_data(IntPtr a, byte[] buffer, nuint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_finish_entry(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_close(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int archive_write_free(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr archive_error_string(IntPtr a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr archive_entry_new();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void archive_entry_free(IntPtr e);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void archive_entry_set_pathname_utf8(IntPtr e, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void archive_entry_set_filetype(IntPtr e, uint type);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void archive_entry_set_perm(IntPtr e, uint mode);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void archive_entry_set_size(IntPtr e, long size);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void archive_entry_set_mtime(IntPtr e, nint time, nint nanoseconds);
}
