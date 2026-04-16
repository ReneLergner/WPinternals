using System;
using System.Linq;
using System.Text;
using UnifiedFlashingPlatform.UEFI;
using WPinternals.HelperClasses;

namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformModel
    {
        public void WriteParam(string Param, byte[] Data)
        {
            byte[] Request = new byte[0x0F + Data.Length];
            const string Header = WriteParamSignature; // NOKXFW

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);
            // 4 empty bytes here
            Buffer.BlockCopy(Data, 0, Request, 15, Data.Length);

            ExecuteRawMethod(Request);
        }

        // TODO: Verify proper functionality
        public void SetUEFIVariable(Guid Guid, string Name, UefiVariableAttributes Attributes, byte[] Data)
        {
            byte[] ParamBuffer = new byte[540 + Data.Length];

            /* 15..30   */ Buffer.BlockCopy(Guid.ToByteArray(), 0, ParamBuffer, 0, 16);
            /* 31..34   */ Buffer.BlockCopy(BigEndian.GetBytes(Math.Min(512, Name.Length * 2), 4), 0, ParamBuffer, 16, 4);
            /* 35..     */ Buffer.BlockCopy(Encoding.Unicode.GetBytes(Name), 0, ParamBuffer, 20, Math.Min(512, Name.Length * 2)); // 256 Max Size for name (unicode)
            /* 547..550 */ Buffer.BlockCopy(BigEndian.GetBytes(Attributes, 4), 0, ParamBuffer, 532, 4);
            /* 551..554 */ Buffer.BlockCopy(BigEndian.GetBytes(Data.Length, 4), 0, ParamBuffer, 536, 4);
            /* 555..    */ Buffer.BlockCopy(Data, 0, ParamBuffer, 540, Data.Length);

            WriteParam(SettingUEFIVariableWriteParamSignature, ParamBuffer);
        }

        public void SetOneTimeBootSequence(ushort BootEntryID)
        {
            //WriteParam(OneTimeBootSequenceWriteParamSignature, BigEndian.GetBytes(BootEntryID, 2));
            WriteParam("UOBU", [.. BigEndian.GetBytes(BootEntryID, 2).Reverse()]);
        }

        public void SetBootOptionAsFirstEntry(ushort BootEntryID)
        {
            //WriteParam(BootOptionAsFirstEntryWriteParamSignature, BigEndian.GetBytes(BootEntryID, 2));
            WriteParam("UBOF", [.. BigEndian.GetBytes(BootEntryID, 2).Reverse()]);
        }

        public void SetBootOptionAsLastEntry(ushort BootEntryID)
        {
            WriteParam(BootOptionAsLastEntryWriteParamSignature, BigEndian.GetBytes(BootEntryID, 2));
        }

        public void SetProgressBar(uint Percentage)
        {
            WriteParam("PBI\0", BigEndian.GetBytes(Percentage, 1));
        }
    }
}
