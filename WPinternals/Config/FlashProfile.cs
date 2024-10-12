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

using System;
using System.Xml.Serialization;

namespace WPinternals.Config
{
    public class FlashProfile
    {
        public string Type;
        public string PlatformID;
        public string ProductCode;
        public string PhoneFirmware;
        public string FfuFirmware;

        [XmlIgnore]
        internal uint FillSize;
        [XmlIgnore]
        internal uint HeaderSize;
        [XmlIgnore]
        internal bool AssumeImageHeaderFallsInGap;
        [XmlIgnore]
        internal bool AllocateAsyncBuffersOnPhone;

        [XmlElement(ElementName = "Profile")]
        public string ProfileAsString
        {
            get
            {
                byte[] ValueBuffer = new byte[10];
                ValueBuffer[0] = 1; // Profile version
                ByteOperations.WriteUInt32(ValueBuffer, 1, FillSize);
                ByteOperations.WriteUInt32(ValueBuffer, 5, HeaderSize);
                if (AssumeImageHeaderFallsInGap)
                {
                    ValueBuffer[9] |= 1;
                }

                if (AllocateAsyncBuffersOnPhone)
                {
                    ValueBuffer[9] |= 2;
                }

                return Convert.ToBase64String(ValueBuffer);
            }
            set
            {
                byte[] ValueBuffer = Convert.FromBase64String(value);
                byte Version = ValueBuffer[0];
                FillSize = ByteOperations.ReadUInt32(ValueBuffer, 1);
                HeaderSize = ByteOperations.ReadUInt32(ValueBuffer, 5);
                AssumeImageHeaderFallsInGap = (ValueBuffer[9] & 1) != 0;
                AllocateAsyncBuffersOnPhone = (ValueBuffer[9] & 2) != 0;
            }
        }
    }
}
