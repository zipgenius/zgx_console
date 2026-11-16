using System.Runtime.InteropServices;

namespace zg;

// GCab's C API uses GLib objects and GError; all objects are released here.
internal static class GCabArchive
{
    private const string Gcab = "libgcab-1.0.so.0";
    private const string Gio = "libgio-2.0.so.0";
    private const string Gobject = "libgobject-2.0.so.0";
    private const string Glib = "libglib-2.0.so.0";

    internal sealed record Entry(string Name, long Size, DateTime Modified);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FileCallback(IntPtr file, IntPtr context);

    internal static List<Entry> List(string archivePath) => WithArchive(archivePath, (cab, entries) => entries);

    internal static void Extract(string archivePath, string destination, Action<IReadOnlyList<Entry>> validate, ISet<string>? selected = null)
    {
        WithArchive(archivePath, (cab, entries) =>
        {
            validate(entries);
            IntPtr directory = g_file_new_for_path(destination);
            Exception? callbackFailure = null;
            FileCallback? callback = selected == null ? null : (file, _) =>
            {
                try
                {
                    string name = (Marshal.PtrToStringUTF8(gcab_file_get_name(file)) ?? "").Replace('\\', '/');
                    return selected.Contains(name) ? 1 : 0;
                }
                catch (Exception ex) { callbackFailure = ex; return 0; }
            };
            try
            {
                Check(gcab_cabinet_extract_simple(cab, directory, callback, IntPtr.Zero, IntPtr.Zero, out IntPtr error), error);
                GC.KeepAlive(callback);
                if (callbackFailure != null) throw new IOException("Could not filter CAB entries.", callbackFailure);
            }
            finally { Unref(directory); }
            return 0;
        });
    }

    internal static void Write(string stagingRoot, string outputPath, bool compress)
    {
        IntPtr cab = IntPtr.Zero, folder = IntPtr.Zero, outputFile = IntPtr.Zero, stream = IntPtr.Zero;
        try
        {
            cab = gcab_cabinet_new();
            folder = gcab_folder_new(compress ? 1 : 0);
            if (cab == IntPtr.Zero || folder == IntPtr.Zero) throw new OutOfMemoryException();
            Check(gcab_cabinet_add_folder(cab, folder, out IntPtr error), error);
            foreach (string source in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(stagingRoot, source);
                if (relative.Contains('\\') || relative.Contains(':'))
                    throw new InvalidDataException("CAB entry name contains unsupported characters: " + relative);
                string name = relative.Replace(Path.DirectorySeparatorChar, '\\');
                IntPtr file = g_file_new_for_path(source);
                IntPtr entry = IntPtr.Zero;
                try
                {
                    entry = gcab_file_new_with_file(name, file);
                    if (file == IntPtr.Zero || entry == IntPtr.Zero) throw new OutOfMemoryException();
                    Check(gcab_folder_add_file(folder, entry, 0, IntPtr.Zero, out error), error);
                }
                finally { Unref(entry); Unref(file); }
            }
            outputFile = g_file_new_for_path(outputPath);
            stream = g_file_create(outputFile, 1, IntPtr.Zero, out error);
            CheckPointer(stream, error);
            Check(gcab_cabinet_write_simple(cab, stream, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out error), error);
            Check(g_output_stream_close(stream, IntPtr.Zero, out error), error);
        }
        catch (DllNotFoundException ex) { throw MissingLibrary(ex); }
        finally { Unref(stream); Unref(outputFile); Unref(folder); Unref(cab); }
    }

    private static T WithArchive<T>(string path, Func<IntPtr, List<Entry>, T> action)
    {
        // A split cabinet needs all of its parts; do not silently list or extract one part.
        using (var input = File.OpenRead(path))
        {
            Span<byte> header = stackalloc byte[36]; input.ReadExactly(header);
            if (!header[..4].SequenceEqual("MSCF"u8)) throw new InvalidDataException("Invalid CAB header.");
            if ((System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[30..]) & 3) != 0)
                throw new NotSupportedException("Split CAB archives are not supported by the Linux CAB reader.");
        }
        IntPtr cab = IntPtr.Zero, file = IntPtr.Zero, stream = IntPtr.Zero;
        try
        {
            cab = gcab_cabinet_new();
            file = g_file_new_for_path(path);
            stream = g_file_read(file, IntPtr.Zero, out IntPtr error);
            CheckPointer(stream, error);
            Check(gcab_cabinet_load(cab, stream, IntPtr.Zero, out error), error);
            var entries = new List<Entry>();
            IntPtr array = gcab_cabinet_get_folders(cab);
            if (array == IntPtr.Zero) throw new InvalidDataException("CAB folder table is missing.");
            IntPtr data = Marshal.ReadIntPtr(array);
            int count = Marshal.ReadInt32(array, IntPtr.Size);
            for (int i = 0; i < count; i++)
            {
                IntPtr folder = Marshal.ReadIntPtr(data, i * IntPtr.Size);
                IntPtr list = gcab_folder_get_files(folder);
                try
                {
                    for (IntPtr node = list; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
                    {
                        IntPtr entry = Marshal.ReadIntPtr(node);
                        string name = Marshal.PtrToStringUTF8(gcab_file_get_name(entry)) ?? throw new InvalidDataException("CAB entry has no name.");
                        IntPtr date = gcab_file_get_date_time(entry);
                        DateTime modified = date == IntPtr.Zero ? DateTime.MinValue : DateTimeOffset.FromUnixTimeSeconds(g_date_time_to_unix(date)).LocalDateTime;
                        entries.Add(new Entry(name.Replace('\\', '/'), gcab_file_get_size(entry), modified));
                    }
                }
                finally { if (list != IntPtr.Zero) g_slist_free(list); }
            }
            return action(cab, entries);
        }
        catch (DllNotFoundException ex) { throw MissingLibrary(ex); }
        finally { Unref(cab); Unref(stream); Unref(file); }
    }

    private static NotSupportedException MissingLibrary(Exception ex) => new("CAB support requires libgcab-1.0.so.0. Install libgcab-1.0-0 on Debian/Ubuntu, libgcab1 on Fedora, or gcab on Arch.", ex);
    private static void Unref(IntPtr value) { if (value != IntPtr.Zero) g_object_unref(value); }
    private static void CheckPointer(IntPtr value, IntPtr error) { if (value == IntPtr.Zero) Throw(error); if (error != IntPtr.Zero) g_error_free(error); }
    private static void Check(int result, IntPtr error) { if (result == 0) Throw(error); if (error != IntPtr.Zero) g_error_free(error); }
    private static void Throw(IntPtr error)
    {
        string message = error == IntPtr.Zero ? "Unknown libgcab error" : Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, 8)) ?? "Unknown libgcab error";
        if (error != IntPtr.Zero) g_error_free(error);
        throw new IOException("libgcab: " + message);
    }

    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_cabinet_new();
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_folder_new(int compression);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern int gcab_cabinet_add_folder(IntPtr cab, IntPtr folder, out IntPtr error);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern int gcab_folder_add_file(IntPtr folder, IntPtr file, int recurse, IntPtr cancellable, out IntPtr error);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_file_new_with_file([MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr file);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern int gcab_cabinet_write_simple(IntPtr cab, IntPtr stream, IntPtr callback, IntPtr context, IntPtr cancellable, out IntPtr error);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern int gcab_cabinet_load(IntPtr cab, IntPtr stream, IntPtr cancellable, out IntPtr error);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_cabinet_get_folders(IntPtr cab);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_folder_get_files(IntPtr folder);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_file_get_name(IntPtr file);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern uint gcab_file_get_size(IntPtr file);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr gcab_file_get_date_time(IntPtr file);
    [DllImport(Gcab, CallingConvention = CallingConvention.Cdecl)] private static extern int gcab_cabinet_extract_simple(IntPtr cab, IntPtr path, FileCallback? callback, IntPtr context, IntPtr cancellable, out IntPtr error);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr g_file_new_for_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr g_file_read(IntPtr file, IntPtr cancellable, out IntPtr error);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr g_file_create(IntPtr file, int flags, IntPtr cancellable, out IntPtr error);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] private static extern int g_output_stream_close(IntPtr stream, IntPtr cancellable, out IntPtr error);
    [DllImport(Gobject, CallingConvention = CallingConvention.Cdecl)] private static extern void g_object_unref(IntPtr value);
    [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] private static extern void g_error_free(IntPtr error);
    [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] private static extern void g_slist_free(IntPtr list);
    [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] private static extern long g_date_time_to_unix(IntPtr date);
}
