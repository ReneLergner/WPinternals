using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WPinternalsSDK
{
    public class Folder : FileSystemEntry
    {
        internal Folder(string Path, FileAttributes Attributes) : base(Path, Attributes | FileAttributes.Directory)
        {
        }

        internal Folder(string Path) : base(Path, FileAttributes.Directory)
        {
        }

        public Folder() : base("", FileAttributes.Directory)
        {
        }

        public List<FileSystemEntry> GetSubItems()
        {
            this.QueryFolder();
            return this.SubItems;
        }

        private void QueryFolder()
        {
            object obj = this.queryLockObject;
            lock (obj)
            {
                if (this.SubItems == null)
                {
                    this.SubItems = new List<FileSystemEntry>();
                    this.subFolderCount = 0;
                    this.fileCount = 0;
                    if (base.Path.Length > 0)
                    {
                        string text = base.Path;
                        if (!base.Path.EndsWith("*"))
                        {
                            if (base.Path.EndsWith("\\"))
                            {
                                text += "*";
                            }
                            else
                            {
                                text += "\\*";
                            }
                        }
                        WIN32_FIND_DATA win32_FIND_DATA;
                        IntPtr intPtr = Win32.FindFirstFile(text, out win32_FIND_DATA);
                        if (intPtr == (IntPtr)(-1))
                        {
                            int lastWin32Error = Marshal.GetLastWin32Error();
                            if (lastWin32Error != 18)
                            {
                                throw new Win32Exception(lastWin32Error, "Failed to query folder");
                            }
                        }
                        else
                        {
                            do
                            {
                                string text2 = base.Path;
                                if (!base.Path.EndsWith("\\"))
                                {
                                    text2 += "\\";
                                }
                                text2 += win32_FIND_DATA.cFileName;
                                if ((win32_FIND_DATA.dwFileAttributes & 16U) != 0U)
                                {
                                    if (win32_FIND_DATA.cFileName != "." && win32_FIND_DATA.cFileName != "..")
                                    {
                                        this.SubItems.Add(FileSystem.GetFolder(text2));
                                        this.subFolderCount++;
                                    }
                                }
                                else
                                {
                                    File file = new File(text2, (FileAttributes)win32_FIND_DATA.dwFileAttributes);
                                    file.Size = ((ulong)win32_FIND_DATA.nFileSizeHigh << 32 | (ulong)win32_FIND_DATA.nFileSizeLow);
                                    this.SubItems.Add(file);
                                    this.fileCount++;
                                }
                            }
                            while (Win32.FindNextFile(intPtr, out win32_FIND_DATA));
                            Win32.FindClose(intPtr);
                            this.SubItems.Sort();
                        }
                    }
                }
            }
        }

        public Folder GetParent()
        {
            if (base.Path == "" || base.Path == "\\")
            {
                return null;
            }
            int num = base.Path.LastIndexOf('\\');
            if (num < 0)
            {
                return FileSystem.GetFolder("\\");
            }
            return FileSystem.GetFolder(base.Path.Substring(0, num));
        }

        public void RefreshFolder()
        {
            if (this.SubItems != null)
            {
                object obj = this.queryLockObject;
                lock (obj)
                {
                    this.SubItems = null;
                    this.subFolderCount = -1;
                    this.fileCount = -1;
                }
            }
        }

        public int GetSubFolderCount()
        {
            this.QueryFolder();
            return this.subFolderCount;
        }

        public int GetFileCount()
        {
            this.QueryFolder();
            return this.fileCount;
        }

        public Folder OpenSubFolder(string Name)
        {
            return FileSystem.GetFolder(base.Path + "\\" + Name.TrimStart(new char[]
            {
                '\\'
            }));
        }

        public override string ToString()
        {
            return base.Path;
        }

        private List<FileSystemEntry> SubItems;

        private int subFolderCount = -1;

        private int fileCount = -1;

        private readonly object queryLockObject = new object();

        private readonly object detailLockObject = new object();
    }
}
