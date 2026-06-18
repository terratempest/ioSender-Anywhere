using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CNC.Controls.Avalonia.Services;
using CNC.Converters;

namespace ioSender.Services;

public static class ProgramOpenFilePicker
{
    public static Task<string?> PickAsync(Window owner, IStorageProvider storage)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(storage);

#if IOSENDER_WINDOWS
        if (OperatingSystem.IsWindows())
            return Task.FromResult(PickWithWin32Dialog(owner));
#endif
        return PickWithAvaloniaAsync(storage);
    }

    public static IReadOnlyList<FilePickerFileType> BuildAvaloniaFilters()
    {
        var filters = new List<FilePickerFileType>(GCodeFilePicker.FileTypes);
        var converterPatterns = GCodeConverterRegistry.OpenPatterns
            .GroupBy(p => p.Description)
            .Select(g => new FilePickerFileType(g.Key)
            {
                Patterns = g.Select(p => "*." + p.Extension).ToList()
            });
        filters.AddRange(converterPatterns);
        return filters;
    }

    public static string BuildWin32Filter()
    {
        var filters = new List<(string Name, string Pattern)>
        {
            ("G-code", "*.nc;*.ngc;*.gcode;*.tap;*.cnc;*.txt")
        };

        filters.AddRange(GCodeConverterRegistry.OpenPatterns
            .GroupBy(p => p.Description)
            .Select(g => (g.Key, string.Join(';', g.Select(p => "*." + p.Extension)))));

        filters.Add(("All files", "*.*"));
        return string.Concat(filters.Select(f => f.Name + '\0' + f.Pattern + '\0')) + '\0';
    }

    static async Task<string?> PickWithAvaloniaAsync(IStorageProvider storage)
    {
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open file",
            AllowMultiple = false,
            FileTypeFilter = BuildAvaloniaFilters()
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

#if IOSENDER_WINDOWS
    static string? PickWithWin32Dialog(Window owner)
    {
        var fileBuffer = new string('\0', 32768);
        var ofn = new OpenFileName
        {
            lStructSize = Marshal.SizeOf<OpenFileName>(),
            hwndOwner = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero,
            lpstrFilter = BuildWin32Filter(),
            nFilterIndex = 1,
            lpstrFile = fileBuffer,
            nMaxFile = fileBuffer.Length,
            lpstrTitle = "Open file",
            Flags = OpenFileNameFlags.Explorer
                | OpenFileNameFlags.FileMustExist
                | OpenFileNameFlags.PathMustExist
                | OpenFileNameFlags.HideReadOnly
                | OpenFileNameFlags.NoChangeDir
        };

        if (GetOpenFileName(ref ofn))
            return ofn.lpstrFile.Split('\0', 2)[0];

        var error = CommDlgExtendedError();
        if (error == 0)
            return null;

        throw new Win32Exception(error, $"Open file dialog failed: 0x{error:X}");
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileName(ref OpenFileName openFileName);

    [DllImport("comdlg32.dll")]
    static extern int CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public OpenFileNameFlags Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }

    [Flags]
    enum OpenFileNameFlags
    {
        HideReadOnly = 0x00000004,
        NoChangeDir = 0x00000008,
        PathMustExist = 0x00000800,
        FileMustExist = 0x00001000,
        Explorer = 0x00080000,
    }
#endif
}
