using System;

namespace WPinternalsSDK
{
    public enum CreationDisposition
    {
        CreateNew = 1,
        CreateAlways,
        OpenExisting,
        OpenAlways,
        TruncateExisting,
        OpenForLoader
    }
}
