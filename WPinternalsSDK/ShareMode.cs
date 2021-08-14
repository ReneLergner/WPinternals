using System;

namespace WPinternalsSDK
{
    [Flags]
    internal enum ShareMode : uint
    {
        None = 0U,
        Read = 1U,
        Write = 2U,
        Delete = 4U
    }
}
