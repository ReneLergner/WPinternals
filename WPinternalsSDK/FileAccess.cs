using System;

namespace WPinternalsSDK
{
    [Flags]
    internal enum FileAccess : uint
    {
        AccessSystemSecurity = 16777216U,
        MaximumAllowed = 33554432U,
        Delete = 65536U,
        ReadControl = 131072U,
        WriteDAC = 262144U,
        WriteOwner = 524288U,
        Synchronize = 1048576U,
        StandardRightsRequired = 983040U,
        StandardRightsRead = 131072U,
        StandardRightsWrite = 131072U,
        StandardRightsExecute = 131072U,
        StandardRightsAll = 2031616U,
        SpecificRightsAll = 65535U,
        ReadData = 1U,
        ListDirectory = 1U,
        WriteData = 2U,
        AddFile = 2U,
        AppendData = 4U,
        AddSubdirectory = 4U,
        CreatePipeInstance = 4U,
        ReadEa = 8U,
        WriteEa = 16U,
        Execute = 32U,
        Traverse = 32U,
        DeleteChild = 64U,
        ReadAttributes = 128U,
        WriteAttributes = 256U,
        GenericRead = 2147483648U,
        GenericWrite = 1073741824U,
        GenericExecute = 536870912U,
        GenericAll = 268435456U
    }
}
