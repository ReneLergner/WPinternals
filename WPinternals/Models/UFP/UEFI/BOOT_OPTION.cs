using System;
using System.Drawing;
using System.Text;
using WPinternals.HelperClasses;

namespace UnifiedFlashingPlatform.UEFI
{
    public struct BOOT_OPTION
    {
        public byte BootOrderIndex;
        public ushort BootOption;
        public LoadOptionAttribute Attributes;
        public string Description;
        public string DevicePath;
        public string CommandLine;

        public ushort DescriptionStringOffset;
        public ushort DevicePathStringOffset;
        public ushort CommandLineStringOffset;
        public ushort TotalSize;

        public static BOOT_OPTION ReadFromBuffer(byte[] Buffer, int offset)
        {
            byte BootOrderIndex = Buffer[offset];
            var Attributes = (LoadOptionAttribute)BigEndian.ToUInt32(Buffer, offset + 1);
            ushort BootOption = BigEndian.ToUInt16(Buffer, offset + 5);
            // Latest
            ushort DescriptionStringOffset = BigEndian.ToUInt16(Buffer, offset + 7);
            ushort DevicePathStringOffset = BigEndian.ToUInt16(Buffer, offset + 9);
            ushort CommandLineStringOffset = BigEndian.ToUInt16(Buffer, offset + 11);
            ushort TotalSize = BigEndian.ToUInt16(Buffer, offset + 13);

            string Description = Encoding.Unicode.GetString(Buffer, offset + DescriptionStringOffset, DevicePathStringOffset - DescriptionStringOffset);
            string DevicePath;
            string CommandLine = "";

            if (CommandLineStringOffset == 0)
            {
                DevicePath = Encoding.Unicode.GetString(Buffer, offset + DevicePathStringOffset, TotalSize - DevicePathStringOffset);
            }
            else
            {
                DevicePath = Encoding.Unicode.GetString(Buffer, offset + DevicePathStringOffset, CommandLineStringOffset - DevicePathStringOffset);
                CommandLine = Encoding.Unicode.GetString(Buffer, offset + CommandLineStringOffset, TotalSize - CommandLineStringOffset);
            }

            // Older
            /*ushort DescriptionStringLength = BigEndian.ToUInt16(Buffer, offset + 7);
            ushort DevicePathStringLength = BigEndian.ToUInt16(Buffer, offset + 9);

            string Description = Encoding.Unicode.GetString(Buffer, offset + 11, DescriptionStringLength);
            string DevicePath = Encoding.Unicode.GetString(Buffer, offset + 11 + DescriptionStringLength, DevicePathStringLength);
            string CommandLine = "";

            ushort DescriptionStringOffset = 11;
            ushort DevicePathStringOffset = (ushort)(11 + DescriptionStringLength);
            ushort CommandLineStringOffset = 0;
            ushort TotalSize = (ushort)(11 + DescriptionStringLength + DevicePathStringLength);*/

            return new BOOT_OPTION()
            {
                BootOrderIndex = BootOrderIndex,
                Attributes = Attributes,
                BootOption = BootOption,
                Description = Description,
                DevicePath = DevicePath,
                CommandLine = CommandLine,
                DescriptionStringOffset = DescriptionStringOffset,
                DevicePathStringOffset = DevicePathStringOffset,
                CommandLineStringOffset = CommandLineStringOffset,
                TotalSize = TotalSize
            };
        }

        public override readonly string ToString()
        {
            return "BootOrderIndex: " + BootOrderIndex +
                " - Attributes: " + Attributes +
                " - BootOption: " + BootOption +
                " - Description: " + Description +
                " - DevicePath: " + DevicePath +
                " - CommandLine: " + CommandLine;
        }
    }
}
