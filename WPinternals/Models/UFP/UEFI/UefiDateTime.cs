using System;

namespace UnifiedFlashingPlatform.UEFI
{
    public struct UefiDateTime
    {
        public ushort year;
        public byte month;
        public byte day;
        public byte hour;
        public byte minute;
        public byte second;
        public uint nanosecond;
        public short timezone;
        public byte daylight;

        public static UefiDateTime ReadFromBuffer(byte[] Buffer, int offset)
        {
            ushort year = BitConverter.ToUInt16(Buffer, offset);
            byte month = Buffer[offset + 2];
            byte day = Buffer[offset + 3];
            byte hour = Buffer[offset + 4];
            byte minute = Buffer[offset + 5];
            byte second = Buffer[offset + 6];
            uint nanosecond = BitConverter.ToUInt32(Buffer, offset + 8);
            short timezone = BitConverter.ToInt16(Buffer, offset + 12);
            byte daylight = Buffer[offset + 14];

            return new UefiDateTime()
            {
                year = year,
                month = month,
                day = day,
                hour = hour,
                minute = minute,
                second = second,
                nanosecond = nanosecond,
                timezone = timezone,
                daylight = daylight
            };
        }

        public readonly DateTime ToDateTime()
        {
            DateTime dateTime;

            byte month = this.month == 0 ? (byte)1 : this.month;
            byte day = this.day == 0 ? (byte)1 : this.day;

            if (timezone == 2047)
            {
                dateTime = new DateTime(year, month, day, hour, minute, second, Convert.ToInt32(nanosecond / 1000000U), DateTimeKind.Local);
            }
            else
            {
                dateTime = new DateTime(year, month, day, hour, minute, second, Convert.ToInt32(nanosecond / 1000000U), DateTimeKind.Utc);
            }

            return dateTime;
        }
    }
}