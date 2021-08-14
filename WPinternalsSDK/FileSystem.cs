using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WPinternalsSDK
{
    public static class FileSystem
    {
        public static Folder GetFolder(string Path)
        {
            Folder folder = null;
            string text = Path.TrimEnd(new char[]
            {
                '\\'
            });
            if (FileSystem.UseFolderCache)
            {
                KeyedList<string, Folder> folderCache = FileSystem.FolderCache;
                lock (folderCache)
                {
                    string key = text.ToLower();
                    if (FileSystem.FolderCache.Contains(key))
                    {
                        folder = FileSystem.FolderCache[key];
                        FileSystem.FolderCache.Remove(folder);
                        FileSystem.FolderCache.Insert(0, folder);
                        return folder;
                    }
                    folder = new Folder(text);
                    FileSystem.FolderCache.Insert(0, folder);
                    while (FileSystem.FolderCache.Count > 100)
                    {
                        FileSystem.FolderCache.RemoveAt(100);
                    }
                    return folder;
                }
            }
            folder = new Folder(text);
            return folder;
        }

        public static void ClearFolderCache(string Path)
        {
            string key = Path.ToLower();
            if (FileSystem.FolderCache.Contains(key))
            {
                Folder item = FileSystem.FolderCache[key];
                FileSystem.FolderCache.Remove(item);
            }
        }

        public static byte[] ReadFile(string Path)
        {
            uint num = (uint)FileSystem.GetFileSize(Path);
            byte[] array = new byte[num];
            if (num > 0U)
            {
                IntPtr intPtr = Win32.CreateFile(Path, (FileAccess)2147483648U, ShareMode.Read | ShareMode.Write | ShareMode.Delete, IntPtr.Zero, CreationDisposition.OpenExisting, FileAttributes.Normal, IntPtr.Zero);
                if (intPtr == (IntPtr)(-1))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open file");
                }
                uint num2;
                if (!Win32.ReadFile(intPtr, array, num, out num2, UIntPtr.Zero))
                {
                    Win32.CloseHandle(intPtr);
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to read file");
                }
                Win32.CloseHandle(intPtr);
            }
            return array;
        }

        public static string ReadAsciiFile(string Path)
        {
            byte[] array = FileSystem.ReadFile(Path);
            return Encoding.UTF8.GetString(array, 0, array.Length);
        }

        public static string ReadUnicodeFile(string Path)
        {
            byte[] array = FileSystem.ReadFile(Path);
            return Encoding.Unicode.GetString(array, 0, array.Length);
        }

        public static void WriteFile(string Path, byte[] Buffer)
        {
            IntPtr intPtr = Win32.CreateFile(Path, FileAccess.GenericWrite, ShareMode.None, IntPtr.Zero, CreationDisposition.CreateAlways, FileAttributes.Normal, IntPtr.Zero);
            if (intPtr == (IntPtr)(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open file");
            }
            uint num;
            if (!Win32.WriteFile(intPtr, Buffer, (uint)Buffer.Length, out num, UIntPtr.Zero))
            {
                Win32.CloseHandle(intPtr);
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write file");
            }
            Win32.CloseHandle(intPtr);
        }

        public static void WriteAsciiFile(string Path, string Text)
        {
            FileSystem.WriteFile(Path, Encoding.UTF8.GetBytes(Text));
        }

        public static void WriteUnicodeFile(string Path, string Text)
        {
            FileSystem.WriteFile(Path, Encoding.Unicode.GetBytes(Text));
        }

        public static void CopyFile(string SourceFilePath, string DestinationFilePath)
        {
            if (!Win32.CopyFile(SourceFilePath, DestinationFilePath, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to copy file");
            }
        }

        public static void MoveFile(string SourceFilePath, string DestinationFilePath, MoveFileFlags Flags)
        {
            if (!Win32.MoveFileEx(SourceFilePath, DestinationFilePath, Flags))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to move file");
            }
        }

        public static bool FileExists(string Path)
        {
            return Win32.GetFileAttributes(Path) != (FileAttributes)4294967295U;
        }

        public static void DeleteFolder(string Path)
        {
            foreach (FileSystemEntry fileSystemEntry in FileSystem.GetFolder(Path).GetSubItems())
            {
                if (fileSystemEntry.IsFile)
                {
                    FileSystem.DeleteFile(fileSystemEntry.Path);
                }
                else
                {
                    FileSystem.DeleteFolder(fileSystemEntry.Path);
                }
            }
            if (!Win32.RemoveDirectory(Path))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to delete folder");
            }
        }

        public static void CreateFolder(string Path)
        {
            if (!Win32.CreateDirectory(Path, IntPtr.Zero) && Marshal.GetLastWin32Error() != 183)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create folder");
            }
        }

        public static void DeleteFile(string Path)
        {
            if (!Win32.DeleteFile(Path))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to delete file");
            }
        }

        public static void RenameFile(string OldPath, string NewPath)
        {
            if (!Win32.MoveFileEx(OldPath, NewPath, (MoveFileFlags)0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to rename file");
            }
        }

        public static ulong GetFileSize(string Path)
        {
            WIN32_FILE_ATTRIBUTE_DATA win32_FILE_ATTRIBUTE_DATA;
            if (!Win32.GetFileAttributesEx(Path, GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, out win32_FILE_ATTRIBUTE_DATA))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetFileSize failed");
            }
            return (ulong)win32_FILE_ATTRIBUTE_DATA.nFileSizeHigh << 32 | (ulong)win32_FILE_ATTRIBUTE_DATA.nFileSizeLow;
        }

        public static bool UseFolderCache = false;

        public static int FolderCacheSize = 100;

        private static readonly KeyedList<string, Folder> FolderCache = new KeyedList<string, Folder>((Folder folder) => folder.Path.ToLower());

        public static readonly Folder Root = FileSystem.GetFolder("C:\\");
    }
}
