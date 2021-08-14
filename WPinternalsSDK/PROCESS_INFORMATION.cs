using System;

namespace WPinternalsSDK
{
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;

        public IntPtr hThread;

        public int dwProcessId;

        public int dwThreadId;
    }
}
