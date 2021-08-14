using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WPinternalsSDK
{
    internal class Win32
    {
        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegDeleteKeyEx(UIntPtr hKey, string lpSubKey, uint samDesired, uint Reserved);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegCreateKeyEx(UIntPtr hKey, string lpSubKey, int Reserved, string lpClass, RegCreateKeyOption dwOptions, RegSAM samDesired, UIntPtr lpSecurityAttributes, out UIntPtr phkResult, out RegResult lpdwDisposition);

        [DllImport("KERNELBASE.DLL")]
        internal static extern int RegCloseKey(UIntPtr hKey);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegDeleteValue(UIntPtr hKey, string lpValueName);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegOpenKeyEx(UIntPtr hKey, string lpSubKey, RegOpenKeyOption dwOptions, RegSAM samDesired, out UIntPtr phkResult);

        [DllImport("Powrprof.dll", SetLastError = true)]
        internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("ShellChromeAPI.dll")]
        internal static extern int Shell_RequestShutdown(int ShutDownType);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, UIntPtr lpProcessAttributes, UIntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("KERNELBASE.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegQueryValueEx(UIntPtr hKey, string lpValueName, int lpReserved, ref RegistryValueType lpType, byte[] lpData, ref int lpcbData);

        [DllImport("SSPICLI.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool LogonUserExEx(string Username, string Domain, string Password, uint LogonType, uint LogonProvider, UIntPtr TokenGroups, out IntPtr hToken, out UIntPtr pLogonSid, out UIntPtr pBuffer, out uint BufferLength, UIntPtr Reserved);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegSetKeyValue(UIntPtr hKey, string lpSubKey, string lpValueName, RegistryValueType dwType, byte[] lpData, int cbData);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegEnumKeyEx(UIntPtr hkey, uint index, StringBuilder lpName, ref uint lpcbName, UIntPtr reserved, UIntPtr lpClass, UIntPtr lpcbClass, out long lpftLastWriteTime);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
        internal static extern int RegEnumValue(UIntPtr hKey, uint dwIndex, StringBuilder lpValueName, ref uint lpcValueName, UIntPtr lpReserved, out RegistryValueType lpType, byte[] lpData, ref uint lpcbData);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileAttributesEx(string lpFileName, GET_FILEEX_INFO_LEVELS fInfoLevelId, out WIN32_FILE_ATTRIBUTE_DATA fileData);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern FileAttributes GetFileAttributes(string lpFileName);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFile([MarshalAs(UnmanagedType.LPWStr)] string filename, [MarshalAs(UnmanagedType.U4)] FileAccess access, [MarshalAs(UnmanagedType.U4)] ShareMode share, IntPtr securityAttributes, [MarshalAs(UnmanagedType.U4)] CreationDisposition creationDisposition, [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes, IntPtr templateFile);

        [DllImport("KERNELBASE.DLL", SetLastError = true)]
        internal static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, UIntPtr lpOverlapped);

        [DllImport("KERNELBASE.DLL", SetLastError = true)]
        internal static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, [In] UIntPtr lpOverlapped);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, MoveFileFlags Flags);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool RemoveDirectory(string lpPathName);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DeleteFile(string lpFileName);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint SetFilePointer([In] IntPtr hFile, [In] int lDistanceToMove, out int lpDistanceToMoveHigh, [In] MoveMethod dwMoveMethod);

        [DllImport("KERNELBASE.DLL", SetLastError = true)]
        internal static extern bool SetEndOfFile(IntPtr hFile);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("KERNELBASE.DLL", SetLastError = true)]
        internal static extern bool FindClose(IntPtr hFindFile);

        internal const uint INVALID_SET_FILE_POINTER = 4294967295U;

        internal const int ERROR_NO_MORE_FILES = 18;
    }
}
