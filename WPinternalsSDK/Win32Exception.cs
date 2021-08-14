using System;

namespace WPinternalsSDK
{
    public class Win32Exception : Exception
    {
        public Win32Exception(int HResult)
        {
            this.HResult = HResult;
        }

        public Win32Exception(int HResult, string Message) : base(Message)
        {
            this.HResult = HResult;
        }

        public new int HResult
        {
            get
            {
                return this._HResult;
            }
            set
            {
                this._HResult = value;
            }
        }

        private int _HResult;
    }
}
