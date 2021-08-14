using System;

namespace WPinternalsSDK
{
    [Flags]
    public enum FileAttributes : uint
    {
        Invalid = 4294967295U,
        Readonly = 1U,
        Hidden = 2U,
        System = 4U,
        Directory = 16U,
        Archive = 32U,
        Device = 64U,
        Normal = 128U,
        Temporary = 256U,
        SparseFile = 512U,
        ReparsePoint = 1024U,
        Compressed = 2048U,
        Offline = 4096U,
        NotContentIndexed = 8192U,
        Encrypted = 16384U,
        Virtual = 65536U,
        BackupSemantics = 33554432U,
        DeleteOnClose = 67108864U,
        NoBuffering = 536870912U,
        OpenNoRecall = 1048576U,
        OpenReparsePoint = 2097152U,
        Overlapped = 1073741824U,
        PosixSemantics = 1048576U,
        RandomAccess = 268435456U,
        SessionAware = 8388608U,
        SequentialScan = 134217728U,
        WriteThrough = 2147483648U
    }
}
