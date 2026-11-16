using System.Runtime.InteropServices;

namespace zg;

// Linux wimlib C API. The handle remains read-only until the new WIM is
// completely written to a separate file by wimlib_write().
internal static class WimLibWriter
{
    private const string Library = "libwim.so.15";
    private const int AllImages = -1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IterateCallback(IntPtr entry, IntPtr context);

    internal static HashSet<string> ExistingPaths(string path)
    {
        return Entries(path).Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal static List<(string Name, bool IsDirectory, bool IsReparsePoint)> Entries(string path)
    {
        IntPtr wim = Open(path);
        try
        {
            CheckSingleImage(wim);
            var paths = new List<(string, bool, bool)>();
            Exception? failure = null;
            IterateCallback callback = (entry, _) =>
            {
                try
                {
                    var name = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(entry, IntPtr.Size * 2));
                    if (!string.IsNullOrEmpty(name) && name != "/" && name != "\\")
                    {
                        uint attributes = unchecked((uint)Marshal.ReadInt32(entry, IntPtr.Size * 6));
                        paths.Add((name.Replace('\\', '/').Trim('/'), (attributes & 0x10) != 0, (attributes & 0x400) != 0));
                    }
                    return 0;
                }
                catch (Exception ex) { failure = ex; return 1; }
            };
            int result = wimlib_iterate_dir_tree(wim, 1, "/", 1, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (failure != null) throw new IOException("Unable to enumerate WIM entries.", failure);
            Check(result);
            return paths;
        }
        finally { wimlib_free(wim); }
    }

    internal static void Verify(string path)
    {
        IntPtr wim = Open(path, 1); // Verify the integrity table when present.
        try { Check(wimlib_verify_wim(wim, 0)); }
        finally { wimlib_free(wim); }
    }

    internal static void Extract(string path, string destination)
    {
        IntPtr wim = Open(path);
        try { CheckSingleImage(wim); Check(wimlib_extract_image(wim, 1, destination, 0)); }
        finally { wimlib_free(wim); }
    }

    internal static void Write(string stagingRoot, string destination, string? existing, int compressionLevel)
    {
        IntPtr wim = IntPtr.Zero;
        try
        {
            if (existing == null)
            {
                // NONE, XPRESS and LZX use ordinary, Microsoft-compatible WIMs.
                int compression = compressionLevel == 0 ? 0 : compressionLevel == 1 ? 1 : 2;
                Check(wimlib_create_new_wim(compression, out wim));
                Check(wimlib_add_image(wim, stagingRoot, "ZipGenius", IntPtr.Zero, 0));
            }
            else
            {
                wim = Open(existing);
                CheckSingleImage(wim);
                Check(wimlib_add_tree(wim, 1, stagingRoot, "/", 0));
            }
            Check(wimlib_write(wim, destination, AllImages, 1, 0));
        }
        catch (DllNotFoundException ex)
        {
            throw new NotSupportedException("WIM support requires the wimlib runtime (libwim.so.15). Install libwim15t64 or libwim15 on Debian/Ubuntu, or wimlib on Fedora/Arch.", ex);
        }
        finally { if (wim != IntPtr.Zero) wimlib_free(wim); }
    }

    private static IntPtr Open(string path, int flags = 0)
    {
        try { Check(wimlib_open_wim(path, flags, out IntPtr wim)); return wim; }
        catch (DllNotFoundException ex)
        { throw new NotSupportedException("WIM support requires the wimlib runtime (libwim.so.15). Install libwim15t64 or libwim15 on Debian/Ubuntu, or wimlib on Fedora/Arch.", ex); }
    }

    private static void CheckSingleImage(IntPtr wim)
    {
        // wimlib_wim_info starts with a 16-byte GUID and uint32 image_count.
        IntPtr info = Marshal.AllocHGlobal(256);
        try
        {
            Check(wimlib_get_wim_info(wim, info));
            if (Marshal.ReadInt32(info, 16) != 1)
                throw new NotSupportedException("Updating a WIM with multiple images is not supported; the original was not changed.");
        }
        finally { Marshal.FreeHGlobal(info); }
    }

    private static void Check(int code)
    {
        if (code != 0) throw new IOException($"wimlib: {Marshal.PtrToStringUTF8(wimlib_get_error_string(code)) ?? "unknown error"} (code {code}).");
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_create_new_wim(int compression, out IntPtr wim);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_open_wim([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, out IntPtr wim);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_add_image(IntPtr wim, [MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr config, int flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_add_tree(IntPtr wim, int image, [MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string target, int flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_iterate_dir_tree(IntPtr wim, int image, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, IterateCallback callback, IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_get_wim_info(IntPtr wim, IntPtr info);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_write(IntPtr wim, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int image, int flags, uint threads);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_verify_wim(IntPtr wim, int flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int wimlib_extract_image(IntPtr wim, int image, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination, int flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr wimlib_get_error_string(int code);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void wimlib_free(IntPtr wim);
}
