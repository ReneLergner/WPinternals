using System;

namespace WPinternalsSDK
{
    public class FileSystemEntry : IComparable<FileSystemEntry>
    {
        public string Path { get; private set; }

        public string Name { get; private set; }

        internal FileSystemEntry(string Path, FileAttributes Attributes)
        {
            string text = Path.TrimEnd(new char[]
            {
                '\\'
            });
            this.Path = text;
            this.Name = text;
            int num = this.Name.LastIndexOf('\\');
            if (num >= 0)
            {
                this.Name = this.Name.Substring(num + 1);
            }
            this.Attributes = Attributes;
        }

        int IComparable<FileSystemEntry>.CompareTo(FileSystemEntry Other)
        {
            if (Other == null)
            {
                throw new ArgumentException();
            }
            if (this.IsFolder && Other.IsFile)
            {
                return -1;
            }
            if (this.IsFile && Other.IsFolder)
            {
                return 1;
            }
            return string.Compare(this.Name, Other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsFolder
        {
            get
            {
                return (this.Attributes & FileAttributes.Directory) > ~FileAttributes.Invalid;
            }
        }

        public bool IsFile
        {
            get
            {
                return (this.Attributes & FileAttributes.Directory) == ~FileAttributes.Invalid;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return (this.Attributes & FileAttributes.Readonly) > ~FileAttributes.Invalid;
            }
        }

        public bool IsHidden
        {
            get
            {
                return (this.Attributes & FileAttributes.Hidden) > ~FileAttributes.Invalid;
            }
        }

        public bool IsSystem
        {
            get
            {
                return (this.Attributes & FileAttributes.System) > ~FileAttributes.Invalid;
            }
        }

        public readonly FileAttributes Attributes;

        public readonly FileSystemType Type = FileSystemType.Unknown;
    }
}
