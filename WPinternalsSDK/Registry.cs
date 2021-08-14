using System;
using System.Text;

namespace WPinternalsSDK
{
    public static class Registry
    {
        public static RegistryKey GetKey(RegistryHive Root, string Key)
        {
            return new RegistryKey(Root, Key);
        }

        public static int GetDWordValue(RegistryHive Root, string Key, string ValueName)
        {
            return (int)Registry.GetValue(Root, Key, ValueName);
        }

        public static void SetDWordValue(RegistryHive Root, string Key, string ValueName, int Value)
        {
            Registry.SetValue(Root, Key, ValueName, Value);
        }

        public static string GetStringValue(RegistryHive Root, string Key, string ValueName)
        {
            return (string)Registry.GetValue(Root, Key, ValueName);
        }

        public static void SetStringValue(RegistryHive Root, string Key, string ValueName, string Value)
        {
            Registry.SetValue(Root, Key, ValueName, Value);
        }

        public static string[] GetMultiStringValue(RegistryHive Root, string Key, string ValueName)
        {
            return (string[])Registry.GetValue(Root, Key, ValueName);
        }

        public static void SetMultiStringValue(RegistryHive Root, string Key, string ValueName, string[] Value)
        {
            Registry.SetValue(Root, Key, ValueName, Value);
        }

        public static byte[] GetBinaryValue(RegistryHive Root, string Key, string ValueName)
        {
            return (byte[])Registry.GetValue(Root, Key, ValueName);
        }

        public static void SetBinaryValue(RegistryHive Root, string Key, string ValueName, byte[] Value)
        {
            Registry.SetValue(Root, Key, ValueName, Value);
        }

        public static void DeleteKey(RegistryHive Root, string Key)
        {
            foreach (RegistryItem registryItem in Registry.GetKey(Root, Key).GetSubItems())
            {
                if (registryItem is RegistryKey)
                {
                    Registry.DeleteKey(Root, ((RegistryKey)registryItem).Path);
                }
            }
            int num = Key.LastIndexOf('\\');
            int num2;
            if (num < 0)
            {
                num2 = Win32.RegDeleteKeyEx((UIntPtr)((uint)Root), Key, 0U, 0U);
            }
            else
            {
                string lpSubKey = Key.Substring(0, num);
                string lpSubKey2 = Key.Substring(num + 1);
                UIntPtr hKey;
                num2 = Win32.RegOpenKeyEx((UIntPtr)((uint)Root), lpSubKey, (RegOpenKeyOption)0, RegSAM.AllAccess, out hKey);
                if (num2 != 0)
                {
                    throw new Win32Exception(num2, "DeleteKey failed");
                }
                num2 = Win32.RegDeleteKeyEx(hKey, lpSubKey2, 0U, 0U);
                Win32.RegCloseKey(hKey);
            }
            if (num2 != 0)
            {
                throw new Win32Exception(num2, "DeleteKey failed");
            }
        }

        public static void CreateKey(RegistryHive Root, string Key)
        {
            UIntPtr zero = UIntPtr.Zero;
            RegResult regResult;
            int num = Win32.RegCreateKeyEx((UIntPtr)((uint)Root), Key, 0, null, RegCreateKeyOption.NonVolatile, RegSAM.AllAccess, UIntPtr.Zero, out zero, out regResult);
            if (num != 0)
            {
                throw new Win32Exception(num, "CreateKey failed");
            }
            if (zero != UIntPtr.Zero)
            {
                Win32.RegCloseKey(zero);
            }
        }

        public static void DeleteValue(RegistryHive Root, string Key, string ValueName)
        {
            UIntPtr hKey = UIntPtr.Zero;
            int num;
            if (Key != null && Key.Length > 0)
            {
                num = Win32.RegOpenKeyEx((UIntPtr)((uint)Root), Key, (RegOpenKeyOption)0, RegSAM.AllAccess, out hKey);
                if (num != 0)
                {
                    throw new Win32Exception(num, "DeleteValue failed");
                }
            }
            else
            {
                hKey = (UIntPtr)((uint)Root);
            }
            num = Win32.RegDeleteValue(hKey, ValueName);
            if (Key != null && Key.Length > 0)
            {
                Win32.RegCloseKey(hKey);
            }
            if (num != 0)
            {
                throw new Win32Exception(num, "DeleteValue failed");
            }
        }

        public static object GetValue(RegistryHive Root, string Key, string ValueName)
        {
            object obj = null;
            UIntPtr hKey = UIntPtr.Zero;
            int num;
            if (Key != null && Key.Length > 0)
            {
                num = Win32.RegOpenKeyEx((UIntPtr)((uint)Root), Key, (RegOpenKeyOption)0, RegSAM.AllAccess, out hKey);
                if (num != 0)
                {
                    throw new Win32Exception(num, "GetValue failed");
                }
            }
            else
            {
                hKey = (UIntPtr)((uint)Root);
            }
            RegistryValueType registryValueType = RegistryValueType.None;
            int num2 = 0;
            num = Win32.RegQueryValueEx(hKey, ValueName, 0, ref registryValueType, null, ref num2);
            if (num == 0)
            {
                byte[] array = new byte[num2];
                Win32.RegQueryValueEx(hKey, ValueName, 0, ref registryValueType, array, ref num2);
                switch (registryValueType)
                {
                    case RegistryValueType.String:
                        obj = Encoding.Unicode.GetString(array, 0, num2 - 2);
                        break;
                    case RegistryValueType.ExpandString:
                    case (RegistryValueType)5:
                    case (RegistryValueType)6:
                        break;
                    case RegistryValueType.Binary:
                        obj = array;
                        break;
                    case RegistryValueType.DWord:
                        obj = BitConverter.ToInt32(array, 0);
                        break;
                    case RegistryValueType.MultiString:
                        obj = Encoding.Unicode.GetString(array, 0, num2 - 4).Split(new char[1]);
                        break;
                    default:
                        if (registryValueType == RegistryValueType.QWord)
                        {
                            obj = BitConverter.ToInt64(array, 0);
                        }
                        break;
                }
            }
            if (Key != null && Key.Length > 0)
            {
                Win32.RegCloseKey(hKey);
            }
            if (num != 0)
            {
                throw new Win32Exception(num, "GetValue failed");
            }
            if (obj == null)
            {
                throw new NotSupportedException("Registry value cannot be read. Unknown type.");
            }
            return obj;
        }

        public static void SetValue(RegistryHive Root, string Key, string ValueName, object Value)
        {
            byte[] array;
            RegistryValueType registryValueType;
            if (Value is string)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append((string)Value);
                stringBuilder.Append('\0');
                array = Encoding.Unicode.GetBytes(stringBuilder.ToString());
                registryValueType = RegistryValueType.String;
            }
            else if (Value is uint)
            {
                array = BitConverter.GetBytes((uint)Value);
                registryValueType = RegistryValueType.DWord;
            }
            else if (Value is int)
            {
                array = BitConverter.GetBytes((int)Value);
                registryValueType = RegistryValueType.DWord;
            }
            else if (Value is ulong)
            {
                array = BitConverter.GetBytes((ulong)Value);
                registryValueType = RegistryValueType.QWord;
            }
            else if (Value is long)
            {
                array = BitConverter.GetBytes((long)Value);
                registryValueType = RegistryValueType.QWord;
            }
            else if (Value is string[])
            {
                string[] array2 = Value as string[];
                StringBuilder stringBuilder2 = new StringBuilder();
                for (int i = 0; i < array2.Length; i++)
                {
                    stringBuilder2.Append(array2[i]);
                    stringBuilder2.Append('\0');
                }
                stringBuilder2.Append('\0');
                array = Encoding.Unicode.GetBytes(stringBuilder2.ToString());
                registryValueType = RegistryValueType.MultiString;
            }
            else
            {
                if (!(Value is byte[]))
                {
                    throw new NotSupportedException("Registry value cannot be written. Type unknown.");
                }
                array = (byte[])Value;
                registryValueType = RegistryValueType.Binary;
            }
            UIntPtr hKey = (UIntPtr)((uint)Root);
            RegistryValueType dwType = registryValueType;
            byte[] array3 = array;
            int num = Win32.RegSetKeyValue(hKey, Key, ValueName, dwType, array3, array3.Length);
            if (num != 0)
            {
                throw new Win32Exception(num, "SetValue failed");
            }
        }
    }
}
