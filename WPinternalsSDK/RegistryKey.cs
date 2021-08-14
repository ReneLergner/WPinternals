using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WPinternalsSDK
{
    public class RegistryKey : RegistryItem
    {
        public RegistryKey(RegistryHive Root, string Path)
        {
            this.root = Root;
            this.path = Path;
        }

        public RegistryHive Root
        {
            get
            {
                return this.root;
            }
        }

        public string Path
        {
            get
            {
                return this.path;
            }
        }

        public string Name
        {
            get
            {
                string text = this.path;
                int num = text.LastIndexOf('\\');
                if (num >= 0)
                {
                    text = text.Substring(num + 1);
                }
                return text;
            }
        }

        public List<RegistryItem> GetSubItems()
        {
            this.QueryKey();
            return this.SubItems;
        }

        private void QueryKey()
        {
            if (this.SubItems == null)
            {
                this.SubItems = new List<RegistryItem>();
                this.SubKeys = new List<RegistryKey>();
                this.Values = new List<RegistryValue>();
                this.subKeyCount = 0;
                this.valueCount = 0;
                Dictionary<string, RegistryKey> dictionary = new Dictionary<string, RegistryKey>();
                Dictionary<string, RegistryValue> dictionary2 = new Dictionary<string, RegistryValue>();
                UIntPtr uintPtr = UIntPtr.Zero;
                int num;
                if (this.Path != null && this.Path.Length > 0)
                {
                    num = Win32.RegOpenKeyEx((UIntPtr)((uint)this.Root), this.Path, (RegOpenKeyOption)0, RegSAM.AllAccess, out uintPtr);
                    if (num != 0)
                    {
                        throw new Win32Exception(num, "QueryKey failed");
                    }
                }
                else
                {
                    uintPtr = (UIntPtr)((uint)this.Root);
                }
                bool flag = false;
                uint num2 = 0U;
                StringBuilder stringBuilder = new StringBuilder(260);
                do
                {
                    uint num3 = 260U;
                    long num4;
                    num = Win32.RegEnumKeyEx(uintPtr, num2, stringBuilder, ref num3, UIntPtr.Zero, UIntPtr.Zero, UIntPtr.Zero, out num4);
                    if (num == 0)
                    {
                        RegistryKey value = new RegistryKey(this.root, ((this.path == "") ? "" : (this.path + "\\")) + stringBuilder.ToString());
                        dictionary.Add(stringBuilder.ToString(), value);
                        this.subKeyCount++;
                        num2 += 1U;
                    }
                    else if (num == 259)
                    {
                        num = 0;
                        flag = true;
                    }
                }
                while (!flag && num == 0);
                flag = false;
                num2 = 0U;
                do
                {
                    uint num3 = 260U;
                    uint num5 = 0U;
                    byte[] array = null;
                    RegistryValueType registryValueType;
                    Win32.RegEnumValue(uintPtr, num2, stringBuilder, ref num3, UIntPtr.Zero, out registryValueType, array, ref num5);
                    if (num5 > 0U)
                    {
                        array = new byte[num5];
                    }
                    num3 = 260U;
                    num = Win32.RegEnumValue(uintPtr, num2, stringBuilder, ref num3, UIntPtr.Zero, out registryValueType, array, ref num5);
                    object value2 = null;
                    if (num == 0)
                    {
                        switch (registryValueType)
                        {
                            case RegistryValueType.String:
                                value2 = Encoding.Unicode.GetString(array, 0, (int)(num5 - 2U));
                                break;
                            case RegistryValueType.ExpandString:
                            case (RegistryValueType)5:
                            case (RegistryValueType)6:
                                break;
                            case RegistryValueType.Binary:
                                value2 = array;
                                break;
                            case RegistryValueType.DWord:
                                value2 = BitConverter.ToInt32(array, 0);
                                break;
                            case RegistryValueType.MultiString:
                                value2 = Encoding.Unicode.GetString(array, 0, (int)(num5 - 4U)).Split(new char[1]);
                                break;
                            default:
                                if (registryValueType == RegistryValueType.QWord)
                                {
                                    value2 = BitConverter.ToInt64(array, 0);
                                }
                                break;
                        }
                        RegistryValue value3 = new RegistryValue(this.root, this.path, stringBuilder.ToString(), registryValueType, value2);
                        dictionary2.Add(stringBuilder.ToString(), value3);
                        this.valueCount++;
                        num2 += 1U;
                    }
                    else if (num == 259)
                    {
                        num = 0;
                        flag = true;
                    }
                }
                while (!flag && num == 0);
                if (this.Path != null && this.Path.Length > 0)
                {
                    Win32.RegCloseKey(uintPtr);
                }
                if (num != 0)
                {
                    throw new Win32Exception(num, "QueryKey failed");
                }
                foreach (KeyValuePair<string, RegistryKey> keyValuePair in from entry in dictionary
                                                                           orderby entry.Key
                                                                           select entry)
                {
                    this.SubItems.Add(keyValuePair.Value);
                    this.SubKeys.Add(keyValuePair.Value);
                }
                foreach (KeyValuePair<string, RegistryValue> keyValuePair2 in from entry in dictionary2
                                                                              orderby entry.Key
                                                                              select entry)
                {
                    this.SubItems.Add(keyValuePair2.Value);
                    this.Values.Add(keyValuePair2.Value);
                }
            }
        }

        public RegistryKey GetParent()
        {
            if (this.path == "")
            {
                return null;
            }
            int num = this.path.LastIndexOf('\\');
            if (num < 0)
            {
                return new RegistryKey(this.root, "");
            }
            return new RegistryKey(this.root, this.path.Substring(0, num));
        }

        public void RefreshKey()
        {
            this.SubItems = null;
            this.subKeyCount = -1;
            this.valueCount = -1;
        }

        public int GetSubKeyCount()
        {
            this.QueryKey();
            return this.subKeyCount;
        }

        public int GetValueCount()
        {
            this.QueryKey();
            return this.valueCount;
        }

        public RegistryKey GetSubKey(string Key)
        {
            return new RegistryKey(this.root, this.path + ((this.path == "") ? "" : "\\") + Key);
        }

        public override string ToString()
        {
            return "Subkey: " + this.Name;
        }

        private static RegistryValueType GetValueKindFromObject(object Value)
        {
            RegistryValueType result = RegistryValueType.Unknown;
            if (Value is int || Value is uint)
            {
                result = RegistryValueType.DWord;
            }
            if (Value is string)
            {
                result = RegistryValueType.String;
            }
            if (Value is byte[])
            {
                result = RegistryValueType.Binary;
            }
            if (Value is string[])
            {
                result = RegistryValueType.MultiString;
            }
            return result;
        }

        private readonly string path;

        private readonly RegistryHive root;

        private List<RegistryItem> SubItems;

        private List<RegistryKey> SubKeys;

        private List<RegistryValue> Values;

        private int subKeyCount = -1;

        private int valueCount = -1;
    }
}
