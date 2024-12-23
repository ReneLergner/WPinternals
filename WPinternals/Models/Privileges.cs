﻿// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
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

using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Threading;
using WPinternals.HelperClasses;

namespace WPinternals
{
    using Luid = NativeMethods.LUID;
    using PrivilegeNotHeldException = System.Security.AccessControl.PrivilegeNotHeldException;
    using Win32Exception = System.ComponentModel.Win32Exception;

    internal delegate void PrivilegedCallback(object state);

    internal sealed class Privilege
    {
        #region Private static members
        private static readonly LocalDataStoreSlot tlsSlot = Thread.AllocateDataSlot();
        private static readonly HybridDictionary privileges = [];
        private static readonly HybridDictionary luids = [];
        private static readonly ReaderWriterLock privilegeLock = new();
        #endregion

        #region Private members
        private bool needToRevert = false;
        private bool initialState = false;
        private bool stateWasChanged = false;
        private Luid luid;
        private readonly Thread currentThread = Thread.CurrentThread;
        private TlsContents tlsContents = null;
        #endregion

        #region Privilege names
        public const string CreateToken = "SeCreateTokenPrivilege";
        public const string AssignPrimaryToken = "SeAssignPrimaryTokenPrivilege";
        public const string LockMemory = "SeLockMemoryPrivilege";
        public const string IncreaseQuota = "SeIncreaseQuotaPrivilege";
        public const string UnsolicitedInput = "SeUnsolicitedInputPrivilege";
        public const string MachineAccount = "SeMachineAccountPrivilege";
        public const string TrustedComputingBase = "SeTcbPrivilege";
        public const string Security = "SeSecurityPrivilege";
        public const string TakeOwnership = "SeTakeOwnershipPrivilege";
        public const string LoadDriver = "SeLoadDriverPrivilege";
        public const string SystemProfile = "SeSystemProfilePrivilege";
        public const string SystemTime = "SeSystemtimePrivilege";
        public const string ProfileSingleProcess = "SeProfileSingleProcessPrivilege";
        public const string IncreaseBasePriority = "SeIncreaseBasePriorityPrivilege";
        public const string CreatePageFile = "SeCreatePagefilePrivilege";
        public const string CreatePermanent = "SeCreatePermanentPrivilege";
        public const string Backup = "SeBackupPrivilege";
        public const string Restore = "SeRestorePrivilege";
        public const string Shutdown = "SeShutdownPrivilege";
        public const string Debug = "SeDebugPrivilege";
        public const string Audit = "SeAuditPrivilege";
        public const string SystemEnvironment = "SeSystemEnvironmentPrivilege";
        public const string ChangeNotify = "SeChangeNotifyPrivilege";
        public const string RemoteShutdown = "SeRemoteShutdownPrivilege";
        public const string Undock = "SeUndockPrivilege";
        public const string SyncAgent = "SeSyncAgentPrivilege";
        public const string EnableDelegation = "SeEnableDelegationPrivilege";
        public const string ManageVolume = "SeManageVolumePrivilege";
        public const string Impersonate = "SeImpersonatePrivilege";
        public const string CreateGlobal = "SeCreateGlobalPrivilege";
        public const string TrustedCredentialManagerAccess = "SeTrustedCredManAccessPrivilege";
        public const string ReserveProcessor = "SeReserveProcessorPrivilege";
        #endregion

        #region LUID caching logic

        //
        // This routine is a wrapper around a hashtable containing mappings
        // of privilege names to luids
        //

        private static Luid LuidFromPrivilege(string privilege)
        {
            Luid luid;
            luid.LowPart = 0;
            luid.HighPart = 0;

            //
            // Look up the privilege LUID inside the cache
            //

            try
            {
                privilegeLock.AcquireReaderLock(Timeout.Infinite);

                if (luids.Contains(privilege))
                {
                    luid = (Luid)luids[privilege];

                    privilegeLock.ReleaseReaderLock();
                }
                else
                {
                    privilegeLock.ReleaseReaderLock();

                    if (!NativeMethods.LookupPrivilegeValue(null, privilege, ref luid))
                    {
                        int error = Marshal.GetLastWin32Error();

                        if (error == NativeMethods.ERROR_NOT_ENOUGH_MEMORY)
                        {
                            throw new OutOfMemoryException();
                        }
                        else if (error == NativeMethods.ERROR_ACCESS_DENIED)
                        {
                            throw new UnauthorizedAccessException("Caller does not have the rights to look up privilege local unique identifier");
                        }
                        else if (error == NativeMethods.ERROR_NO_SUCH_PRIVILEGE)
                        {
                            throw new ArgumentException(
                                string.Format("{0} is not a valid privilege name", privilege),
                                nameof(privilege));
                        }
                        else
                        {
                            throw new Win32Exception(error);
                        }
                    }

                    privilegeLock.AcquireWriterLock(Timeout.Infinite);
                }
            }
            finally
            {
                if (privilegeLock.IsReaderLockHeld)
                {
                    privilegeLock.ReleaseReaderLock();
                }

                if (privilegeLock.IsWriterLockHeld)
                {
                    if (!luids.Contains(privilege))
                    {
                        luids[privilege] = luid;
                        privileges[luid] = privilege;
                    }

                    privilegeLock.ReleaseWriterLock();
                }
            }

            return luid;
        }
        #endregion

        #region Nested classes
        private sealed class TlsContents : IDisposable
        {
            private bool disposed = false;
            private int referenceCount = 1;
            private SafeTokenHandle threadHandle = new(IntPtr.Zero);

            private static SafeTokenHandle processHandle = new(IntPtr.Zero);
            private static readonly object syncRoot = new();

            #region Constructor and finalizer
            public TlsContents()
            {
                int error = 0;
                int cachingError = 0;
                bool success = true;

                if (processHandle.IsInvalid)
                {
                    lock (syncRoot)
                    {
                        if (processHandle.IsInvalid && !NativeMethods.OpenProcessToken(
                                            NativeMethods.GetCurrentProcess(),
                                            TokenAccessLevels.Duplicate,
                                            ref processHandle))
                        {
                            cachingError = Marshal.GetLastWin32Error();
                            success = false;
                        }
                    }
                }

                try
                {
                    // Open the thread token; if there is no thread token,
                    // copy the process token onto the thread

                    if (!NativeMethods.OpenThreadToken(
                        NativeMethods.GetCurrentThread(),
                        TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges,
                        true,
                        ref this.threadHandle))
                    {
                        if (success)
                        {
                            error = Marshal.GetLastWin32Error();

                            if (error != NativeMethods.ERROR_NO_TOKEN)
                            {
                                success = false;
                            }

                            if (success)
                            {
                                error = 0;

                                if (!NativeMethods.DuplicateTokenEx(
                                    processHandle,
                                    TokenAccessLevels.Impersonate | TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges,
                                    IntPtr.Zero,
                                    SecurityImpersonationLevel.Impersonation,
                                    TokenType.Impersonation,
                                    ref this.threadHandle))
                                {
                                    error = Marshal.GetLastWin32Error();
                                    success = false;
                                }
                            }

                            if (success && !NativeMethods.SetThreadToken(
                                    IntPtr.Zero,
                                    this.threadHandle))
                            {
                                error = Marshal.GetLastWin32Error();
                                success = false;
                            }

                            if (success)
                            {
                                // This thread is now impersonating; it needs to be reverted to its original state

                                this.IsImpersonating = true;
                            }
                        }
                        else
                        {
                            error = cachingError;
                        }
                    }
                    else
                    {
                        success = true;
                    }
                }
                finally
                {
                    if (!success)
                    {
                        Dispose();
                    }
                }

                if (error == NativeMethods.ERROR_NOT_ENOUGH_MEMORY)
                {
                    throw new OutOfMemoryException();
                }
                else if (error == NativeMethods.ERROR_ACCESS_DENIED ||
                    error == NativeMethods.ERROR_CANT_OPEN_ANONYMOUS)
                {
                    throw new UnauthorizedAccessException("The caller does not have the rights to perform the operation");
                }
                else if (error != 0)
                {
                    throw new Win32Exception(error);
                }
            }

            ~TlsContents()
            {
                if (!this.disposed)
                {
                    Dispose(false);
                }
            }
            #endregion

            #region IDisposable implementation
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (this.disposed)
                {
                    return;
                }

                if (this.threadHandle != null)
                {
                    this.threadHandle.Dispose();
                    this.threadHandle = null;
                }

                if (this.IsImpersonating)
                {
                    NativeMethods.RevertToSelf();
                }

                this.disposed = true;
            }
            #endregion

            #region Reference-counting
            public void IncrementReferenceCount()
            {
                this.referenceCount++;
            }

            public int DecrementReferenceCount()
            {
                int result = --this.referenceCount;

                if (result == 0)
                {
                    Dispose();
                }

                return result;
            }

            public int ReferenceCountValue
            {
                get { return this.referenceCount; }
            }
            #endregion

            #region Properties
            public SafeTokenHandle ThreadHandle
            {
                get { return this.threadHandle; }
            }

            public bool IsImpersonating { get; } = false;
            #endregion
        }
        #endregion

        #region Constructor
        public Privilege(string privilegeName)
        {
            if (privilegeName == null)
            {
                throw new ArgumentNullException(nameof(privilegeName));
            }

            this.luid = LuidFromPrivilege(privilegeName);
        }
        #endregion

        #region Public methods and properties
        public void Enable()
        {
            this.ToggleState(true);
        }

        public void Disable()
        {
            this.ToggleState(false);
        }

        public void Revert()
        {
            int error = 0;

            // All privilege operations must take place on the same thread

            if (!this.currentThread.Equals(Thread.CurrentThread))
            {
                throw new InvalidOperationException("Operation must take place on the thread that created the object");
            }

            if (!this.NeedToRevert)
            {
                return;
            }

            // This code must be eagerly prepared and non-interruptible.

            try
            {
                // The payload is entirely in the finally block
                // This is how we ensure that the code will not be
                // interrupted by catastrophic exceptions
            }
            finally
            {
                bool success = true;

                try
                {
                    // Only call AdjustTokenPrivileges if we're not going to be reverting to self,
                    // on this Revert, since doing the latter obliterates the thread token anyway

                    if (this.stateWasChanged &&
                        (this.tlsContents.ReferenceCountValue > 1 ||
                        !this.tlsContents.IsImpersonating))
                    {
                        NativeMethods.TOKEN_PRIVILEGE newState = new()
                        {
                            PrivilegeCount = 1
                        };
                        newState.Privilege.Luid = this.luid;
                        newState.Privilege.Attributes = this.initialState ? NativeMethods.SE_PRIVILEGE_ENABLED : NativeMethods.SE_PRIVILEGE_DISABLED;

                        NativeMethods.TOKEN_PRIVILEGE previousState = new();
                        uint previousSize = 0;

                        if (!NativeMethods.AdjustTokenPrivileges(
                                        this.tlsContents.ThreadHandle,
                                        false,
                                        ref newState,
                                        (uint)Marshal.SizeOf(previousState),
                                        ref previousState,
                                        ref previousSize))
                        {
                            error = Marshal.GetLastWin32Error();
                            success = false;
                        }
                    }
                }
                finally
                {
                    if (success)
                    {
                        this.Reset();
                    }
                }
            }

            if (error == NativeMethods.ERROR_NOT_ENOUGH_MEMORY)
            {
                throw new OutOfMemoryException();
            }
            else if (error == NativeMethods.ERROR_ACCESS_DENIED)
            {
                throw new UnauthorizedAccessException("Caller does not have the permission to change the privilege");
            }
            else if (error != 0)
            {
                throw new Win32Exception(error);
            }
        }

        public bool NeedToRevert
        {
            get { return this.needToRevert; }
        }

        public static void RunWithPrivilege(string privilege, bool enabled, PrivilegedCallback callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Privilege p = new(privilege);

            try
            {
                if (enabled)
                {
                    p.Enable();
                }
                else
                {
                    p.Disable();
                }

                callback(state);
            }
            catch (Exception ex)
            {
                LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                LogFile.Log(ex.Message, LogType.FileAndConsole);
                LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                p.Revert();
                throw;
            }
            finally
            {
                p.Revert();
            }
        }
        #endregion

        #region Private implementation
        private void ToggleState(bool enable)
        {
            int error = 0;

            // All privilege operations must take place on the same thread

            if (!this.currentThread.Equals(Thread.CurrentThread))
            {
                throw new InvalidOperationException("Operation must take place on the thread that created the object");
            }

            // This privilege was already altered and needs to be reverted before it can be altered again

            if (this.NeedToRevert)
            {
                throw new InvalidOperationException("Must revert the privilege prior to attempting this operation");
            }

            // Need to make this block of code non-interruptible so that it would preserve
            // consistency of thread oken state even in the face of catastrophic exceptions

            try
            {
                // The payload is entirely in the finally block
                // This is how we ensure that the code will not be
                // interrupted by catastrophic exceptions
            }
            finally
            {
                try
                {
                    // Retrieve TLS state

                    this.tlsContents = Thread.GetData(tlsSlot) as TlsContents;

                    if (this.tlsContents == null)
                    {
                        this.tlsContents = new TlsContents();
                        Thread.SetData(tlsSlot, this.tlsContents);
                    }
                    else
                    {
                        this.tlsContents.IncrementReferenceCount();
                    }

                    NativeMethods.TOKEN_PRIVILEGE newState = new()
                    {
                        PrivilegeCount = 1
                    };
                    newState.Privilege.Luid = this.luid;
                    newState.Privilege.Attributes = enable ? NativeMethods.SE_PRIVILEGE_ENABLED : NativeMethods.SE_PRIVILEGE_DISABLED;

                    NativeMethods.TOKEN_PRIVILEGE previousState = new();
                    uint previousSize = 0;

                    // Place the new privilege on the thread token and remember the previous state.

                    if (!NativeMethods.AdjustTokenPrivileges(
                                    this.tlsContents.ThreadHandle,
                                    false,
                                    ref newState,
                                    (uint)Marshal.SizeOf(previousState),
                                    ref previousState,
                                    ref previousSize))
                    {
                        error = Marshal.GetLastWin32Error();
                    }
                    else if (NativeMethods.ERROR_NOT_ALL_ASSIGNED == Marshal.GetLastWin32Error())
                    {
                        error = NativeMethods.ERROR_NOT_ALL_ASSIGNED;
                    }
                    else
                    {
                        // This is the initial state that revert will have to go back to

                        this.initialState = (previousState.Privilege.Attributes & NativeMethods.SE_PRIVILEGE_ENABLED) != 0;

                        // Remember whether state has changed at all

                        this.stateWasChanged = this.initialState != enable;

                        // If we had to impersonate, or if the privilege state changed we'll need to revert

                        this.needToRevert = this.tlsContents.IsImpersonating || this.stateWasChanged;
                    }
                }
                finally
                {
                    if (!this.needToRevert)
                    {
                        this.Reset();
                    }
                }
            }

            if (error == NativeMethods.ERROR_NOT_ALL_ASSIGNED)
            {
                throw new PrivilegeNotHeldException(privileges[this.luid] as string);
            }
            if (error == NativeMethods.ERROR_NOT_ENOUGH_MEMORY)
            {
                throw new OutOfMemoryException();
            }
            else if (error == NativeMethods.ERROR_ACCESS_DENIED ||
                error == NativeMethods.ERROR_CANT_OPEN_ANONYMOUS)
            {
                throw new UnauthorizedAccessException("The caller does not have the right to change the privilege");
            }
            else if (error != 0)
            {
                throw new Win32Exception(error);
            }
        }

        private void Reset()
        {
            try
            {
                // Payload is in the finally block
                // as a way to guarantee execution
            }
            finally
            {
                this.stateWasChanged = false;
                this.initialState = false;
                this.needToRevert = false;

                if (this.tlsContents?.DecrementReferenceCount() == 0)
                {
                    this.tlsContents = null;
                    Thread.SetData(tlsSlot, null);
                }
            }
        }
        #endregion
    }
}

