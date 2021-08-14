using System;
using System.Runtime.InteropServices;

namespace WPinternalsSDK
{
    public static class Environment
    {
        public static bool HasRootAccess()
        {
            bool result = true;
            try
            {
                new Folder("C:\\Windows\\System32\\config").GetSubItems();
            }
            catch
            {
                result = false;
            }
            try
            {
                new RegistryKey((RegistryHive)2147483650U, "Security").GetSubItems();
            }
            catch
            {
                result = false;
            }
            try
            {
                Environment.ShellExecute("C:\\WINDOWS\\SYSTEM32\\INSTALLAGENT.EXE");
            }
            catch
            {
                result = false;
            }
            return result;
        }

        public static void Elevate()
        {
            IntPtr intPtr;
            UIntPtr uintPtr;
            UIntPtr uintPtr2;
            uint num;
            if (!Win32.LogonUserExEx("DefApps", "", "", 2U, 0U, UIntPtr.Zero, out intPtr, out uintPtr, out uintPtr2, out num, UIntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Elevate failed");
            }
        }

        public static void ShellExecute(string Application)
        {
            Environment.ShellExecute(Application, null);
        }

        public static void ShellExecute(string Application, string Arguments)
        {
            PROCESS_INFORMATION process_INFORMATION = default(PROCESS_INFORMATION);
            STARTUPINFO startupinfo = default(STARTUPINFO);
            if (!Win32.CreateProcess(Application, Arguments, UIntPtr.Zero, UIntPtr.Zero, false, 32U, IntPtr.Zero, null, ref startupinfo, out process_INFORMATION))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");
            }
            if (process_INFORMATION.hProcess != IntPtr.Zero && process_INFORMATION.hProcess != (IntPtr)(-1))
            {
                Win32.CloseHandle(process_INFORMATION.hProcess);
            }
            if (process_INFORMATION.hThread != IntPtr.Zero && process_INFORMATION.hThread != (IntPtr)(-1))
            {
                Win32.CloseHandle(process_INFORMATION.hThread);
                return;
            }
        }
    }
}
