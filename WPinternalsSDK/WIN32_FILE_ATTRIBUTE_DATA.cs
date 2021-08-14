using System;

namespace WPinternalsSDK
{
    internal struct WIN32_FILE_ATTRIBUTE_DATA
    {
        public FileAttributes dwFileAttributes;

        public FILETIME ftCreationTime;

        public FILETIME ftLastAccessTime;

        public FILETIME ftLastWriteTime;

        public uint nFileSizeHigh;

        public uint nFileSizeLow;
    }
}
