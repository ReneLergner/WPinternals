using System;

namespace WPinternalsSDK
{
    public class File : FileSystemEntry
    {
        public File(string Path, FileAttributes Attributes) : base(Path, Attributes)
        {
        }

        public ulong Size
        {
            get
            {
                if (!this.sizeQueried)
                {
                    this.size = FileSystem.GetFileSize(base.Path);
                }
                return this.size;
            }
            internal set
            {
                this.size = value;
                this.sizeQueried = true;
            }
        }

        public override string ToString()
        {
            return base.Path;
        }

        private bool sizeQueried;

        private ulong size;
    }
}
