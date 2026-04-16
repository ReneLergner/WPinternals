using System;
using System.Text;

namespace UnifiedFlashingPlatform.UEFI
{
    public struct FILE_INFO
    {
		public ulong Size;
		public ulong FileSize;
		public ulong PhysicalSize;
		public UefiDateTime CreateTime;
		public UefiDateTime LastAccessTime;
		public UefiDateTime ModificationTime;
		public FileAttribute Attribute;
		public string FileName;

        public static FILE_INFO ReadFromBuffer(byte[] Buffer, int offset)
        {
            ulong size = BitConverter.ToUInt64(Buffer, offset);
            ulong fileSize = BitConverter.ToUInt64(Buffer, offset + 8);
            ulong physicalSize = BitConverter.ToUInt64(Buffer, offset + 16);
            UefiDateTime createTime = UefiDateTime.ReadFromBuffer(Buffer, offset + 24);
            UefiDateTime lastAccessTime = UefiDateTime.ReadFromBuffer(Buffer, offset + 40);
            UefiDateTime modificationTime = UefiDateTime.ReadFromBuffer(Buffer, offset + 56);
            ulong attribute = BitConverter.ToUInt64(Buffer, offset + 72);
            string fileName = Encoding.Unicode.GetString(Buffer, offset + 80, (int)(size - 80));

            return new FILE_INFO()
            {
                Size = size,
                FileSize = fileSize,
                PhysicalSize = physicalSize,
                CreateTime = createTime,
                LastAccessTime = lastAccessTime,
                ModificationTime = modificationTime,
                Attribute = (FileAttribute)attribute,
                FileName = fileName
            };
        }

        public override readonly string ToString()
        {
            return "Size: " + Size +
            "- File Size: " + FileSize +
            "- Physical Size: " + PhysicalSize +
            "- Create Time: " + CreateTime.ToDateTime() +
            "- Last Access Time: " + LastAccessTime.ToDateTime() +
            "- Modification Time: " + ModificationTime.ToDateTime() +
            "- Attribute: " + Attribute +
            "- FileName: " + FileName;
        }
    }
}
