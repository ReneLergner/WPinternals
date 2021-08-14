using System;

namespace WPinternalsSDK
{
    public class RegistryValue : RegistryItem
    {
        public RegistryValue(RegistryHive Root, string Key, string ValueName, RegistryValueType Type, object Value)
        {
            this.root = Root;
            this.key = Key;
            this.valueName = ValueName;
            this.type = Type;
            this.value = Value;
        }

        public RegistryHive Root
        {
            get
            {
                return this.root;
            }
        }

        public string Key
        {
            get
            {
                return this.key;
            }
        }

        public string ValueName
        {
            get
            {
                return this.valueName;
            }
        }

        public RegistryValueType Type
        {
            get
            {
                return this.type;
            }
        }

        public object Value
        {
            get
            {
                return this.value;
            }
        }

        public override string ToString()
        {
            string text = null;
            RegistryValueType registryValueType = this.type;
            switch (registryValueType)
            {
                case RegistryValueType.String:
                    text = (string)this.value;
                    break;
                case RegistryValueType.ExpandString:
                case (RegistryValueType)5:
                case (RegistryValueType)6:
                    break;
                case RegistryValueType.Binary:
                    text = "<Binary>";
                    break;
                case RegistryValueType.DWord:
                    text = "0x" + ((int)this.value).ToString("X8");
                    break;
                case RegistryValueType.MultiString:
                    text = "<Multistring>";
                    break;
                default:
                    if (registryValueType == RegistryValueType.QWord)
                    {
                        text = "0x" + ((long)this.value).ToString("X16");
                    }
                    break;
            }
            if (text == null)
            {
                return this.ValueName + ": <Unknown>";
            }
            return this.ValueName + ": " + text;
        }

        private readonly RegistryHive root;

        private readonly string key;

        private readonly string valueName;

        private readonly RegistryValueType type = RegistryValueType.Unknown;

        private readonly object value;
    }
}
