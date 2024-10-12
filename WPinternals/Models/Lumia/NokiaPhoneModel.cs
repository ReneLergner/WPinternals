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

using MadWizard.WinUSBNet;
using System;
using WPinternals.HelperClasses;

namespace WPinternals.Models.Lumia
{
    internal class NokiaPhoneModel : IDisposable
    {
        protected bool Disposed = false;
        internal readonly USBDevice Device = null;
        internal readonly object UsbLock = new();

        public NokiaPhoneModel(string DevicePath)
        {
            // Mass Storage device is not WinUSB
            try
            {
                Device = new USBDevice(DevicePath);
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }
        }

        public void ResetDevice()
        {
            try
            {
                foreach (USBPipe pipe in Device.Pipes)
                {
                    pipe.Abort();
                    pipe.Reset();
                }
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }
        }

        /// <summary>
        /// Disposes the UsbDevice including all unmanaged WinUSB handles. This function
        /// should be called when the UsbDevice object is no longer in use, otherwise
        /// unmanaged handles will remain open until the garbage collector finalizes the
        /// object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer for the UsbDevice. Disposes all unmanaged handles.
        /// </summary>
        ~NokiaPhoneModel()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the object
        /// </summary>
        /// <param name="disposing">Indicates wether Dispose was called manually (true) or by
        /// the garbage collector (false) via the destructor.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            if (disposing)
            {
                Device?.Dispose();
            }

            // Clean unmanaged resources here.
            // (none currently)

            Disposed = true;
        }
    }
}
