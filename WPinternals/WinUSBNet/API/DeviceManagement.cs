/*  WinUSBNet library
 *  (C) 2010 Thomas Bleeker (www.madwizard.org)
 *  
 *  Licensed under the MIT license, see license.txt or:
 *  http://www.opensource.org/licenses/mit-license.php
 */

/* NOTE: Parts of the code in this file are based on the work of Jan Axelson
 * See http://www.lvr.com/winusb.htm for more information
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MadWizard.WinUSBNet.API
{
    ///  <summary>
    ///  Routines for detecting devices and receiving device notifications.
    ///  </summary>
    internal static partial class DeviceManagement
    {
        // Get device name from notification message.
        // Also checks checkGuid with the GUID from the message to check the notification
        // is for a relevant device. Other messages might be received.
        public static string GetNotifyMessageDeviceName(IntPtr pDevBroadcastHeader, Guid checkGuid)
        {
            int stringSize;

            DEV_BROADCAST_DEVICEINTERFACE_1 devBroadcastDeviceInterface = new();
            DEV_BROADCAST_HDR devBroadcastHeader = new();

            // The LParam parameter of Message is a pointer to a DEV_BROADCAST_HDR structure.

            Marshal.PtrToStructure(pDevBroadcastHeader, devBroadcastHeader);

            if (devBroadcastHeader.dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE)
            {
                // The dbch_devicetype parameter indicates that the event applies to a device interface.
                // So the structure in LParam is actually a DEV_BROADCAST_INTERFACE structure, 
                // which begins with a DEV_BROADCAST_HDR.

                // Obtain the number of characters in dbch_name by subtracting the 32 bytes
                // in the strucutre that are not part of dbch_name and dividing by 2 because there are 
                // 2 bytes per character.

                stringSize = Convert.ToInt32((devBroadcastHeader.dbch_size - 32) / 2);

                // The dbcc_name parameter of devBroadcastDeviceInterface contains the device name. 
                // Trim dbcc_name to match the size of the String.         

                devBroadcastDeviceInterface.dbcc_name = new char[stringSize + 1];

                // Marshal data from the unmanaged block pointed to by m.LParam 
                // to the managed object devBroadcastDeviceInterface.

                Marshal.PtrToStructure(pDevBroadcastHeader, devBroadcastDeviceInterface);

                // Check if message is for the GUID
                if (devBroadcastDeviceInterface.dbcc_classguid != checkGuid)
                {
                    return null;
                }

                // Store the device name in a String.
                return (string)(new(devBroadcastDeviceInterface.dbcc_name, 0, stringSize));
            }
            else if (devBroadcastHeader.dbch_devicetype == DBT_DEVTYP_VOLUME)
            {
                DEV_BROADCAST_VOLUME vol = new();
                Marshal.PtrToStructure(pDevBroadcastHeader, vol);
                Int32 Mask = vol.dbcv_unitmask;
                Int32 DriveInt = Convert.ToInt32('A') - 1;
                do
                {
                    Mask /= 2;
                    DriveInt++;
                }
                while (Mask > 0);
                return @"\\.\" + Convert.ToChar(DriveInt) + ":";
            }
            return null;
        }

        private static byte[] GetProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, SPDRP property, out int regType)
        {
            if (!SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, IntPtr.Zero, IntPtr.Zero, 0, out uint requiredSize) && Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
            {
                throw APIException.Win32("Failed to get buffer size for device registry property.");
            }

            byte[] buffer = new byte[requiredSize];

            if (!SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, out regType, buffer, (uint)buffer.Length, out requiredSize))
            {
                throw APIException.Win32("Failed to get device registry property.");
            }

            return buffer;
        }

        //DEFINE_DEVPROPKEY(DEVPKEY_Device_BusReportedDeviceDesc,  0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 4);     // DEVPROP_TYPE_STRING
        //DEFINE_DEVPROPKEY(DEVPKEY_Device_ContainerId,            0x8c7ed206, 0x3f8a, 0x4827, 0xb3, 0xab, 0xae, 0x9e, 0x1f, 0xae, 0xfc, 0x6c, 2);     // DEVPROP_TYPE_GUID
        //DEFINE_DEVPROPKEY(DEVPKEY_Device_FriendlyName,           0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 14);    // DEVPROP_TYPE_STRING
        //DEFINE_DEVPROPKEY(DEVPKEY_DeviceDisplay_Category,        0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 0x5a);  // DEVPROP_TYPE_STRING_LIST
        //DEFINE_DEVPROPKEY(DEVPKEY_Device_LocationInfo,           0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 15);    // DEVPROP_TYPE_STRING
        //DEFINE_DEVPROPKEY(DEVPKEY_Device_Manufacturer,           0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 13);    // DEVPROP_TYPE_STRING
        //DEFINE_DEVPROPKEY(DEVPKEY_Device_SecuritySDS,            0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 26);    // DEVPROP_TYPE_SECURITY_DESCRIPTOR_STRING

        // Heathcliff74
        private static byte[] GetProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY property, out uint propertyType)
        {
            if (!SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref property, out propertyType, null, 0, out int requiredSize, 0) && Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
            {
                throw APIException.Win32("Failed to get buffer size for device registry property.");
            }

            byte[] buffer = new byte[requiredSize];

            if (!SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref property, out propertyType, buffer, buffer.Length, out requiredSize, 0))
            {
                throw APIException.Win32("Failed to get device registry property.");
            }

            return buffer;
        }

        // todo: is the queried data always available, or should we check ERROR_INVALID_DATA?
        private static string GetStringProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, SPDRP property)
        {
            byte[] buffer = GetProperty(deviceInfoSet, deviceInfoData, property, out int regType);
            if (regType != (int)RegTypes.REG_SZ)
            {
                throw new APIException("Invalid registry type returned for device property.");
            }

            // sizof(char), 2 bytes, are removed to leave out the string terminator
            return System.Text.Encoding.Unicode.GetString(buffer, 0, buffer.Length - sizeof(char));
        }

        // Heathcliff74
        // todo: is the queried data always available, or should we check ERROR_INVALID_DATA?
        private static string GetStringProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY property)
        {
            byte[] buffer = GetProperty(deviceInfoSet, deviceInfoData, property, out uint propertyType);
            if (propertyType != 0x00000012) // DEVPROP_TYPE_STRING
            {
                throw new APIException("Invalid registry type returned for device property.");
            }

            // sizof(char), 2 bytes, are removed to leave out the string terminator
            return System.Text.Encoding.Unicode.GetString(buffer, 0, buffer.Length - sizeof(char));
        }

        private static string[] GetMultiStringProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, SPDRP property)
        {
            byte[] buffer = GetProperty(deviceInfoSet, deviceInfoData, property, out int regType);
            if (regType != (int)RegTypes.REG_MULTI_SZ)
            {
                throw new APIException("Invalid registry type returned for device property.");
            }

            string fullString = System.Text.Encoding.Unicode.GetString(buffer);

            return fullString.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        }
        private static DeviceDetails GetDeviceDetails(string devicePath, IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            DeviceDetails details = new();
            details.DevicePath = devicePath;
            details.DeviceDescription = GetStringProperty(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_DEVICEDESC);
            details.Manufacturer = GetStringProperty(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_MFG);

            // Heathcliff74
            details.BusName = "";
            try
            {
                details.BusName = GetStringProperty(deviceInfoSet, deviceInfoData, new DEVPROPKEY(new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), 4));
            }
            catch { }

            string[] hardwareIDs = GetMultiStringProperty(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_HARDWAREID);

            Regex regex = new("^USB\\\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            foreach (string hardwareID in hardwareIDs)
            {
                Match match = regex.Match(hardwareID);
                if (match.Success)
                {
                    details.VID = ushort.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.AllowHexSpecifier);
                    details.PID = ushort.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.AllowHexSpecifier);
                    break;
                }
            }

            // Heathcliff74
            // Not all devices have numeric VID and PID
            // 
            // if (!foundVidPid)
            //     throw new APIException("Failed to find VID and PID for USB device. No hardware ID could be parsed.");

            return details;
        }

        public static DeviceDetails[] FindDevicesFromGuid(Guid guid)
        {
            IntPtr deviceInfoSet = IntPtr.Zero;
            List<DeviceDetails> deviceList = new();
            try
            {
                deviceInfoSet = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero,
                    DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (deviceInfoSet == FileIO.INVALID_HANDLE_VALUE)
                {
                    throw APIException.Win32("Failed to enumerate devices.");
                }

                int memberIndex = 0;
                while (true)
                {
                    // Begin with 0 and increment through the device information set until
                    // no more devices are available.					
                    SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new();

                    // The cbSize element of the deviceInterfaceData structure must be set to
                    // the structure's size in bytes. 
                    // The size is 28 bytes for 32-bit code and 32 bytes for 64-bit code.
                    deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

                    bool success = SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref guid, memberIndex, ref deviceInterfaceData);

                    // Find out if a device information set was retrieved.
                    if (!success)
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        if (lastError == ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        throw APIException.Win32("Failed to get device interface.");
                    }
                    // A device is present.

                    int bufferSize = 0;

                    success = SetupDiGetDeviceInterfaceDetail
                        (deviceInfoSet,
                        ref deviceInterfaceData,
                        IntPtr.Zero,
                        0,
                        ref bufferSize,
                        IntPtr.Zero);

                    if (!success && Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
                    {
                        throw APIException.Win32("Failed to get interface details buffer size.");
                    }

                    IntPtr detailDataBuffer = IntPtr.Zero;
                    try
                    {
                        // Allocate memory for the SP_DEVICE_INTERFACE_DETAIL_DATA structure using the returned buffer size.
                        detailDataBuffer = Marshal.AllocHGlobal(bufferSize);

                        // Store cbSize in the first bytes of the array. The number of bytes varies with 32- and 64-bit systems.

                        Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);

                        // Call SetupDiGetDeviceInterfaceDetail again.
                        // This time, pass a pointer to DetailDataBuffer
                        // and the returned required buffer size.

                        // build a DevInfo Data structure
                        SP_DEVINFO_DATA da = new();
                        da.cbSize = Marshal.SizeOf(da);

                        success = SetupDiGetDeviceInterfaceDetail
                            (deviceInfoSet,
                            ref deviceInterfaceData,
                            detailDataBuffer,
                            bufferSize,
                            ref bufferSize,
                            ref da);

                        if (!success)
                        {
                            throw APIException.Win32("Failed to get device interface details.");
                        }

                        // Skip over cbsize (4 bytes) to get the address of the devicePathName.

                        IntPtr pDevicePathName = new(detailDataBuffer.ToInt64() + 4);
                        string pathName = Marshal.PtrToStringUni(pDevicePathName);

                        // Get the String containing the devicePathName.

                        DeviceDetails details = GetDeviceDetails(pathName, deviceInfoSet, da);

                        deviceList.Add(details);
                    }
                    finally
                    {
                        if (detailDataBuffer != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(detailDataBuffer);
                            detailDataBuffer = IntPtr.Zero;
                        }
                    }
                    memberIndex++;
                }
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero && deviceInfoSet != FileIO.INVALID_HANDLE_VALUE)
                {
                    SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
            return [.. deviceList];
        }

        public static void RegisterForDeviceNotifications(IntPtr controlHandle, Guid classGuid, ref IntPtr deviceNotificationHandle)
        {
            DEV_BROADCAST_DEVICEINTERFACE devBroadcastDeviceInterface = new();
            IntPtr devBroadcastDeviceInterfaceBuffer = IntPtr.Zero;
            try
            {
                int size = Marshal.SizeOf(devBroadcastDeviceInterface);
                devBroadcastDeviceInterface.dbcc_size = size;

                devBroadcastDeviceInterface.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                devBroadcastDeviceInterface.dbcc_reserved = 0;
                devBroadcastDeviceInterface.dbcc_classguid = classGuid;
                devBroadcastDeviceInterfaceBuffer = Marshal.AllocHGlobal(size);

                // Copy the DEV_BROADCAST_DEVICEINTERFACE structure to the buffer.
                // Set fDeleteOld True to prevent memory leaks.
                Marshal.StructureToPtr(devBroadcastDeviceInterface, devBroadcastDeviceInterfaceBuffer, true);

                deviceNotificationHandle = RegisterDeviceNotification(controlHandle, devBroadcastDeviceInterfaceBuffer, DEVICE_NOTIFY_WINDOW_HANDLE);
                if (deviceNotificationHandle == IntPtr.Zero)
                {
                    throw APIException.Win32("Failed to register device notification");
                }

                // Marshal data from the unmanaged block devBroadcastDeviceInterfaceBuffer to
                // the managed object devBroadcastDeviceInterface
                Marshal.PtrToStructure(devBroadcastDeviceInterfaceBuffer, devBroadcastDeviceInterface);
            }
            finally
            {
                // Free the memory allocated previously by AllocHGlobal.
                if (devBroadcastDeviceInterfaceBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(devBroadcastDeviceInterfaceBuffer);
                }
            }
        }

        public static void StopDeviceDeviceNotifications(IntPtr deviceNotificationHandle)
        {
            if (!UnregisterDeviceNotification(deviceNotificationHandle))
            {
                throw APIException.Win32("Failed to unregister device notification");
            }
        }
    }
}
