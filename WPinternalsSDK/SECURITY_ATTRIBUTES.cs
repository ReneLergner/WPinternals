using System;

namespace WPinternalsSDK
{
    internal struct SECURITY_ATTRIBUTES
    {
        public int nLength;

        public IntPtr lpSecurityDescriptor;

        public int bInheritHandle;
    }
}
