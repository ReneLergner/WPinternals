using System;

namespace WPinternalsSDK
{
    [Flags]
    public enum RegCreateKeyOption
    {
        NonVolatile = 0,
        Volatile = 1,
        CreateLink = 2,
        BackupRestore = 4,
        OpenLink = 8
    }
}
