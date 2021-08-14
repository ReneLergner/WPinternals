/*  WinUSBNet library
 *  (C) 2010 Thomas Bleeker (www.madwizard.org)
 *
 *  Licensed under the MIT license, see license.txt or:
 *  http://www.opensource.org/licenses/mit-license.php
 */

using System;
using System.Windows;
using System.Windows.Interop;

namespace MadWizard.WinUSBNet
{
    internal class DeviceNotifyHook : IDisposable
    {
        // http://msdn.microsoft.com/en-us/library/system.windows.forms.nativewindow.aspx

        // TODO: disposed exception when disposed

        private readonly USBNotifier _notifier;
        private Guid _guid;
        private readonly IntPtr _notifyHandle;

        public DeviceNotifyHook(USBNotifier notifier, Guid guid)
        {
            // _parent = parent;
            _guid = guid;
            // _parent.HandleCreated += new EventHandler(this.OnHandleCreated);
            // _parent.HandleDestroyed += new EventHandler(this.OnHandleDestroyed);
            _notifier = notifier;

            IntPtr hWnd = IntPtr.Zero;
            if (Application.Current.MainWindow != null)
            {
                hWnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            }

            if (hWnd == IntPtr.Zero)
            {
                throw new USBException("Main window not loaded yet. Cannot connect with USB yet.");
            }

            API.DeviceManagement.RegisterForDeviceNotifications(hWnd, _guid, ref _notifyHandle);

            HwndSource source = PresentationSource.FromVisual(Application.Current.MainWindow) as HwndSource;
            source.AddHook(WndProc);
        }

        ~DeviceNotifyHook()
        {
            Dispose(false);
        }

        /*
        // Listen for the control's window creation and then hook into it.
        private void OnHandleCreated(object sender, EventArgs e)
        {
            try
            {
                // Window is now created, assign handle to NativeWindow.
                IntPtr handle = ((Control)sender).Handle;
                RegisterNotify(handle);
            }
            catch (API.APIException ex)
            {
                throw new USBException("Failed to register new window handle for device notification.", ex);
            }
        }

        private void OnHandleDestroyed(object sender, EventArgs e)
        {
            try
            {
                // Window was destroyed, release hook.
                StopNotify();
            }
            catch (API.APIException ex)
            {
                throw new USBException("Failed to unregister destroyed window handle for device notification.", ex);
            }
        }

        private void RegisterNotify(IntPtr handle)
        {
            AssignHandle(handle);

            if (_notifyHandle != IntPtr.Zero)
            {
                API.DeviceManagement.StopDeviceDeviceNotifications(_notifyHandle);
                _notifyHandle = IntPtr.Zero;
            }
            API.DeviceManagement.RegisterForDeviceNotifications(handle, _guid, ref _notifyHandle);
        }

        private void StopNotify()
        {
            //ReleaseHandle();
            if (_notifyHandle != IntPtr.Zero)
            {
                API.DeviceManagement.StopDeviceDeviceNotifications(_notifyHandle);
                _notifyHandle = IntPtr.Zero;
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Listen for operating system messages

            switch (m.Msg)
            {
                case API.DeviceManagement.WM_DEVICECHANGE:
                    _notifier.HandleDeviceChange(m);
                    break;
                case WM_NCDESTROY:
                    // Note: when a control is used, OnHandleDestroyed will be called and the
                    // handle is already released from NativeWindow. In that case, this
                    // WM_NCDESTROY message will not be caught here. This is no problem since
                    // StopNotify is already called. Even if it does, calling it twice does not cause
                    // problems.
                    // When a window handle is used instead of a Control the OnHandle events will not
                    // fire and this handler is necessary to release the handle and stop notifications
                    // when the window is destroyed.
                    StopNotify();
                    break;
            }
            base.WndProc(ref m);
        }
        */

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Listen for operating system messages

            return msg switch
            {
                API.DeviceManagement.WM_DEVICECHANGE => (IntPtr)_notifier.HandleDeviceChange(msg, wParam, lParam),
                _ => IntPtr.Zero,
            };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // clean managed resources

                // do not clean the notifier here. the notifier owns and will dispose this object.
            }
            if (_notifyHandle != IntPtr.Zero)
            {
                API.DeviceManagement.StopDeviceDeviceNotifications(_notifyHandle);

                try
                {
                    /*
                    if ((Application.Current != null) && (Application.Current.MainWindow != null))
                    {
                        HwndSource source = PresentationSource.FromVisual(Application.Current.MainWindow) as HwndSource;
                        source.RemoveHook(WndProc);
                    }
                    */

                    void RemoveHookAction()
                    {
                        if (Application.Current.MainWindow != null)
                        {
                            HwndSource source = PresentationSource.FromVisual(Application.Current.MainWindow) as HwndSource;
                            source.RemoveHook(WndProc);
                        }
                    }

                    if (Application.Current != null)
                    {
                        if (Application.Current.Dispatcher.Thread.ManagedThreadId == Environment.CurrentManagedThreadId)
                        {
                            RemoveHookAction();
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(RemoveHookAction);
                        }
                    }
                }
                catch { }
            }
        }
    }
}
