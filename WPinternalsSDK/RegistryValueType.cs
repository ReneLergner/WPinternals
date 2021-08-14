using System;

namespace WPinternalsSDK
{
    public enum RegistryValueType
    {
        String = 1,
        ExpandString,
        Binary,
        DWord,
        MultiString = 7,
        QWord = 11,
        Unknown = -1,
        None
    }
}
