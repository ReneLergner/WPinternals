// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace WPinternals
{
    [Flags]
    internal enum TokenAccessLevels
    {
        AssignPrimary = 0x00000001,
        Duplicate = 0x00000002,
        Impersonate = 0x00000004,
        Query = 0x00000008,
        QuerySource = 0x00000010,
        AdjustPrivileges = 0x00000020,
        AdjustGroups = 0x00000040,
        AdjustDefault = 0x00000080,
        AdjustSessionId = 0x00000100,

        Read = 0x00020000 | Query,

        Write = 0x00020000 | AdjustPrivileges | AdjustGroups | AdjustDefault,

        AllAccess = 0x000F0000 |
            AssignPrimary |
            Duplicate |
            Impersonate |
            Query |
            QuerySource |
            AdjustPrivileges |
            AdjustGroups |
            AdjustDefault |
            AdjustSessionId,

        MaximumAllowed = 0x02000000
    }

    internal enum SecurityImpersonationLevel
    {
        Anonymous = 0,
        Identification = 1,
        Impersonation = 2,
        Delegation = 3,
    }

    internal enum TokenType
    {
        Primary = 1,
        Impersonation = 2,
    }

    internal enum EMoveMethod : uint
    {
        Begin = 0,
        Current = 1,
        End = 2
    }

    internal sealed class NativeMethods
    {
        internal const uint SE_PRIVILEGE_DISABLED = 0x00000000;
        internal const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LUID
        {
            internal uint LowPart;
            internal uint HighPart;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LUID_AND_ATTRIBUTES
        {
            internal LUID Luid;
            internal uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct TOKEN_PRIVILEGE
        {
            internal uint PrivilegeCount;
            internal LUID_AND_ATTRIBUTES Privilege;
        }

        internal const string ADVAPI32 = "advapi32.dll";
        internal const string KERNEL32 = "kernel32.dll";

        internal const int ERROR_SUCCESS = 0x0;
        internal const int ERROR_ACCESS_DENIED = 0x5;
        internal const int ERROR_NOT_ENOUGH_MEMORY = 0x8;
        internal const int ERROR_NO_TOKEN = 0x3f0;
        internal const int ERROR_NOT_ALL_ASSIGNED = 0x514;
        internal const int ERROR_NO_SUCH_PRIVILEGE = 0x521;
        internal const int ERROR_CANT_OPEN_ANONYMOUS = 0x543;

        [DllImport(
             KERNEL32,
             SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport(
             ADVAPI32,
             CharSet = CharSet.Unicode,
             SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(
            [In]     SafeTokenHandle TokenHandle,
            [In]     bool DisableAllPrivileges,
            [In]     ref TOKEN_PRIVILEGE NewState,
            [In]     uint BufferLength,
            [In, Out] ref TOKEN_PRIVILEGE PreviousState,
            [In, Out] ref uint ReturnLength);

        [DllImport(
             ADVAPI32,
             CharSet = CharSet.Auto,
             SetLastError = true)]
        internal static extern
        bool RevertToSelf();

        [DllImport(
             ADVAPI32,
             EntryPoint = "LookupPrivilegeValueW",
             CharSet = CharSet.Auto,
             SetLastError = true)]
        internal static extern
        bool LookupPrivilegeValue(
            [In]     string lpSystemName,
            [In]     string lpName,
            [In, Out] ref LUID Luid);

        [DllImport(
             KERNEL32,
             CharSet = CharSet.Auto,
             SetLastError = true)]
        internal static extern
        IntPtr GetCurrentProcess();

        [DllImport(
             KERNEL32,
             CharSet = CharSet.Auto,
             SetLastError = true)]
        internal static extern
            IntPtr GetCurrentThread();

        [DllImport(
             ADVAPI32,
             CharSet = CharSet.Unicode,
             SetLastError = true)]
        internal static extern
        bool OpenProcessToken(
            [In]     IntPtr ProcessToken,
            [In]     TokenAccessLevels DesiredAccess,
            [In, Out] ref SafeTokenHandle TokenHandle);

        [DllImport
             (ADVAPI32,
             CharSet = CharSet.Unicode,
             SetLastError = true)]
        internal static extern
        bool OpenThreadToken(
            [In]     IntPtr ThreadToken,
            [In]     TokenAccessLevels DesiredAccess,
            [In]     bool OpenAsSelf,
            [In, Out] ref SafeTokenHandle TokenHandle);

        [DllImport
            (ADVAPI32,
             CharSet = CharSet.Unicode,
             SetLastError = true)]
        internal static extern
        bool DuplicateTokenEx(
            [In]    SafeTokenHandle ExistingToken,
            [In]    TokenAccessLevels DesiredAccess,
            [In]    IntPtr TokenAttributes,
            [In]    SecurityImpersonationLevel ImpersonationLevel,
            [In]    TokenType TokenType,
            [In, Out] ref SafeTokenHandle NewToken);

        [DllImport
             (ADVAPI32,
             CharSet = CharSet.Unicode,
             SetLastError = true)]
        internal static extern
        bool SetThreadToken(
            [In]    IntPtr Thread,
            [In]    SafeTokenHandle Token);

        internal const uint FILE_SHARE_READ = 0x00000001;
        internal const uint FILE_SHARE_WRITE = 0x00000002;
        internal const uint FILE_SHARE_DELETE = 0x00000004;
        internal const uint OPEN_EXISTING = 3;

        internal const uint GENERIC_READ = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;

        internal const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
        internal const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        internal const uint FILE_READ_ATTRIBUTES = 0x0080;
        internal const uint FILE_WRITE_ATTRIBUTES = 0x0100;
        internal const uint ERROR_INSUFFICIENT_BUFFER = 122;
        internal const uint FILE_BEGIN = 0;
        internal const uint FSCTL_LOCK_VOLUME = 0x00090018;
        internal const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
        internal const uint FSCTL_UNLOCK_VOLUME = 0x00090022;
        internal const uint IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;
        internal const uint IOCTL_STORAGE_LOAD_MEDIA = 0x2D480C;

        internal const Int32 INVALID_HANDLE_VALUE = -1;
        internal const Int32 FILE_ATTRIBUTE_NORMAL = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern uint SetFilePointer(
              [In] IntPtr hFile,
              [In] int lDistanceToMove,
              [In, Out] ref int lpDistanceToMoveHigh,
              [In] EMoveMethod dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

        [DllImport("Kernel32.dll", SetLastError = false, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint IoControlCode,
            [In] object InBuffer,
            uint nInBufferSize,
            [Out] object OutBuffer,
            uint nOutBufferSize,
            ref uint pBytesReturned,
            IntPtr Overlapped
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushFileBuffers(IntPtr hFile);

        static NativeMethods()
        {
        }
    }

    internal sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeTokenHandle() : base(true) { }

        // 0 is an Invalid Handle
        internal SafeTokenHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        internal static SafeTokenHandle InvalidHandle
        {
            get { return new SafeTokenHandle(IntPtr.Zero); }
        }

        [DllImport(NativeMethods.KERNEL32, SetLastError = true),
         SuppressUnmanagedCodeSecurity]
        private static extern bool CloseHandle(IntPtr handle);

        override protected bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }
}
