using System;
using System.Runtime.InteropServices;

namespace Monopoly.Setup
{
    // Современное окно Windows «Выбор папки» (IFileOpenDialog). В .NET Framework для WPF своего такого нет,
    // а старое дерево папок из WinForms неудобное.
    internal static class FolderPicker
    {
        private const uint PickFolders = 0x20, ForceFileSystem = 0x40;
        private const uint FileSystemPath = 0x80058000;
        private const int Cancelled = unchecked((int)0x800704C7);

        // null — отменили.
        public static string? Pick(IntPtr owner, string title, string? startFolder)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            try
            {
                dialog.SetOptions(PickFolders | ForceFileSystem);
                dialog.SetTitle(title);
                if (startFolder is not null && System.IO.Directory.Exists(startFolder)
                    && SHCreateItemFromParsingName(startFolder, IntPtr.Zero, typeof(IShellItem).GUID, out var start) == 0)
                {
                    dialog.SetFolder(start);
                }
                int result = dialog.Show(owner);
                if (result == Cancelled)
                    return null;
                Marshal.ThrowExceptionForHR(result);
                dialog.GetResult(out var item);
                item.GetDisplayName(FileSystemPath, out string path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        // Порядок методов — как в IFileDialog; нужны только первые, до GetResult.
        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr specs);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint type, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        }
    }
}
