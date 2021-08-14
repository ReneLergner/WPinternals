using System;

namespace WPinternalsSDK
{
    [Flags]
    public enum MoveFileFlags
    {
        ReplaceExisting = 1,
        CopyAllowed = 2,
        DelayUntilReboot = 4,
        WriteThrough = 8,
        CreateHardLink = 16,
        FailIfNotTrackable = 32
    }
}
