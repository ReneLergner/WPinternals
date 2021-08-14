/*  WinUSBNet library
 *  (C) 2010 Thomas Bleeker (www.madwizard.org)
 *
 *  Licensed under the MIT license, see license.txt or:
 *  http://www.opensource.org/licenses/mit-license.php
 */

using System;

namespace MadWizard.WinUSBNet
{
    /// <summary>
    /// Delegate for event handler methods handing USB events
    /// </summary>
    /// <param name="sender">The source of the event</param>
    /// <param name="e">Details of the event</param>
    public delegate void USBEventHandler(object sender, USBEvent e);

    /// <summary>
    /// Event type enumeration for WinUSB events
    /// </summary>
    public enum USBEventType
    {
        /// <summary>
        /// A device has been connected to the system
        /// </summary>
        DeviceArrival,

        /// <summary>
        /// A device has been disconnected from the system
        /// </summary>
        DeviceRemoval,
    }

    /// <summary>
    /// Contains the details of a USB event
    /// </summary>
    public class USBEvent : EventArgs
    {
        /// <summary>
        /// WinUSB interface GUID of the device as specified in the WinUSBNotifier
        /// </summary>
        public Guid Guid;

        /// <summary>
        /// Device pathname that identifies the device
        /// </summary>
        public string DevicePath;

        /// <summary>
        /// Type of event that occurred
        /// </summary>
        public USBEventType Type;

        internal USBEvent(USBEventType type, Guid guid, string devicePath)
        {
            this.Guid = guid;
            this.DevicePath = devicePath;
            this.Type = type;
        }
    }

    /// <summary>
    /// Helper class to receive notifications on USB device changes such as
    /// connecting or removing a device.
    /// </summary>
    public class USBNotifier : IDisposable
    {
        private readonly DeviceNotifyHook _hook;
        private Guid _guid;
        private readonly WPinternals.AsyncAutoResetEvent NodeChangeEvent = new(false);

        /// <summary>
        /// Event triggered when a new USB device that matches the USBNotifier's GUID is connected
        /// </summary>
        private event EventHandler<USBEvent> _Arrival;
        public event EventHandler<USBEvent> Arrival
        {
            // Heathcliff74 - Also notify currently connected USB devices
            add
            {
                _Arrival -= value;
                _Arrival += value;
                foreach (USBDeviceInfo Device in USBDevice.GetDevices(Guid))
                {
                    _Arrival(this, new USBEvent(USBEventType.DeviceArrival, Guid, Device.DevicePath));
                }
            }
            remove
            {
                _Arrival -= value;
            }
        }

        /// <summary>
        /// Event triggered when a new USB device that matches the USBNotifier's GUID  is disconnected
        /// </summary>
        public event EventHandler<USBEvent> Removal;

        /// <summary>
        /// The interface GUID of devices this USBNotifier will watch
        /// </summary>
        public Guid Guid
        {
            get
            {
                return _guid;
            }
        }

        /// <summary>
        /// Constructs a new USBNotifier that will watch for events on
        /// devices matching the given interface GUID. A Windows Forms control
        /// is needed since the notifier relies on window messages.
        /// </summary>
        /// <param name="control">A control that will be used internally for device notification messages.
        /// You can use a Form object for example.</param>
        /// <param name="guidString">The interface GUID string of the devices to watch.</param>
        public USBNotifier(string guidString) :
            this(new Guid(guidString))
        {
            // Handled in other constructor
        }

        /// <summary>
        /// Constructs a new USBNotifier that will watch for events on
        /// devices matching the given interface GUID. A Windows Forms control
        /// is needed since the notifier relies on window messages.
        /// </summary>
        /// <param name="control">A control that will be used internally for device notification messages.
        /// You can use a Form object for example.</param>
        /// <param name="guid">The interface GUID of the devices to watch.</param>
        public USBNotifier(Guid guid)
        {
            _guid = guid;
            _hook = new DeviceNotifyHook(this, _guid);
        }

        /// <summary>
        /// Triggers the arrival event
        /// </summary>
        /// <param name="devicePath">Device pathname of the device that has been connected</param>
        protected void OnArrival(string devicePath)
        {
            _Arrival?.Invoke(this, new USBEvent(USBEventType.DeviceArrival, _guid, devicePath));
        }
        /// <summary>
        /// Triggers the removal event
        /// </summary>
        /// <param name="devicePath">Device pathname of the device that has been connected</param>
        protected void OnRemoval(string devicePath)
        {
            Removal?.Invoke(this, new USBEvent(USBEventType.DeviceRemoval, _guid, devicePath));
        }

        internal int HandleDeviceChange(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg != API.DeviceManagement.WM_DEVICECHANGE)
            {
                throw new USBException("Invalid device change message."); // should not happen
            }

            //switch ((int)wParam)
            //{
            //    case API.DeviceManagement.DBT_DEVICEARRIVAL:
            //        WPinternals.LogFile.Log(Guid.ToString() + " - DBT_DEVICEARRIVAL", WPinternals.LogType.FileOnly);
            //        break;
            //    case API.DeviceManagement.DBT_DEVICEREMOVECOMPLETE:
            //        WPinternals.LogFile.Log(Guid.ToString() + " - DBT_DEVICEREMOVECOMPLETE", WPinternals.LogType.FileOnly);
            //        break;
            //    case API.DeviceManagement.DBT_DEVNODES_CHANGED:
            //        WPinternals.LogFile.Log(Guid.ToString() + " - DBT_DEVNODES_CHANGED", WPinternals.LogType.FileOnly);
            //        break;
            //    case API.DeviceManagement.DBT_QUERYCHANGECONFIG:
            //        WPinternals.LogFile.Log(Guid.ToString() + " - DBT_QUERYCHANGECONFIG", WPinternals.LogType.FileOnly);
            //        break;
            //    default:
            //        WPinternals.LogFile.Log(Guid.ToString() + " - wParam: 0x" + ((int)wParam).ToString("X8"), WPinternals.LogType.FileOnly);
            //        break;
            //}

            if ((int)wParam == API.DeviceManagement.DBT_DEVICEARRIVAL)
            {
                string devName = API.DeviceManagement.GetNotifyMessageDeviceName(lParam, _guid);
                if (devName != null)
                {
                    OnArrival(devName);
                }
            }

            if ((int)wParam == API.DeviceManagement.DBT_DEVICEREMOVECOMPLETE)
            {
                string devName = API.DeviceManagement.GetNotifyMessageDeviceName(lParam, _guid);
                if (devName != null)
                {
                    OnRemoval(devName);
                }
            }

            if ((int)wParam == API.DeviceManagement.DBT_DEVNODES_CHANGED)
            {
                NodeChangeEvent.Set();
            }

            if ((int)wParam == API.DeviceManagement.DBT_QUERYCHANGECONFIG)
            {
                return 1; // Give permission
            }

            return 0;
        }

        public async System.Threading.Tasks.Task WaitForNextNodeChange()
        {
            await NodeChangeEvent.WaitAsync(System.Threading.Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Disposes the USBNotifier object and frees all resources.
        /// Call this method when the object is no longer needed.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object's resources.
        /// </summary>
        /// <param name="disposing">True when dispose is called manually, false when called by the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hook.Dispose();
            }
        }
    }
}
