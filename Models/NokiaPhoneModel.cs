// Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WPinternals
{
    internal class NokiaPhoneModel: IDisposable
    {
        protected bool Disposed = false;
        private USBDevice Device = null;
        private int MessageId = 0;
        private object UsbLock = new object();

        public NokiaPhoneModel(string DevicePath)
        {
            // Mass Storage device is not WinUSB
            try
            {
                Device = new USBDevice(DevicePath);
            }
            catch { }
        }

        private JToken ExecuteJsonMethodAsJsonToken(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            byte[] Buffer;
            int Length;

            lock (UsbLock)
            {
                string jsonrpc = "2.0";
                int id = MessageId++;
                string method = JsonMethod;
                Dictionary<string, object> @params = new Dictionary<string, object>();
                if (Params != null)
                {
                    foreach (KeyValuePair<string, object> Param in Params)
                    {
                        if (Param.Value is byte[])
                            @params.Add(Param.Key, ((byte[])Param.Value).Select(b => (int)b).ToArray()); // convert to int-array
                        else
                            @params.Add(Param.Key, Param.Value);
                    }
                }
                @params.Add("MessageVersion", 0);
                string Request = JsonConvert.SerializeObject(new { jsonrpc, id, method, @params });
                Device.OutputPipe.Write(System.Text.Encoding.ASCII.GetBytes(Request));

                Buffer = new byte[0x10000];
                Length = Device.InputPipe.Read(Buffer);
            }

            Newtonsoft.Json.Linq.JObject ResultMessage = Newtonsoft.Json.Linq.JObject.Parse(System.Text.ASCIIEncoding.ASCII.GetString(Buffer, 0, Length));

            JToken ResultToken = ResultMessage.Root.SelectToken("result");
            if ((ResultToken == null) || (ResultElement == null)) return null;
            return ResultToken.SelectToken(ResultElement);
        }

        public void ExecuteJsonMethod(string JsonMethod, Dictionary<string, object> Params)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, null);
        }

        public string ExecuteJsonMethodAsString(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, ResultElement);
            if (Token == null) return null;
            return Token.Value<string>();
        }

        public string ExecuteJsonMethodAsString(string JsonMethod, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null) return null;
            return Token.Value<string>();
        }

        public int ExecuteJsonMethodAsInteger(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, ResultElement);
            if (Token == null) return 0;
            return Token.Value<int>();
        }

        public int ExecuteJsonMethodAsInteger(string JsonMethod, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null) return 0;
            return Token.Value<int>();
        }

        public byte[] ExecuteJsonMethodAsBytes(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, ResultElement);
            if (Token == null) return null;
            return Token.Values<byte>().ToArray();
        }

        public byte[] ExecuteJsonMethodAsBytes(string JsonMethod, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null) return null;
            return Token.Values<byte>().ToArray();
        }

        public bool? ExecuteJsonMethodAsBoolean(string JsonMethod, string ResultElement)
        {
            JToken Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null) return null;
            return Token.Value<bool>();
        }

        public void ExecuteJsonMethodAsync(string JsonMethod, Dictionary<string, object> Params)
        {
            lock (UsbLock)
            {
                string jsonrpc = "2.0";
                int id = MessageId++;
                string method = JsonMethod;
                Dictionary<string, object> @params = new Dictionary<string, object>();
                if (Params != null)
                {
                    foreach (KeyValuePair<string, object> Param in Params)
                    {
                        if (Param.Value is byte[])
                            @params.Add(Param.Key, ((byte[])Param.Value).Select(b => (int)b).ToArray()); // convert to int-array
                        else
                            @params.Add(Param.Key, Param.Value);
                    }
                }
                @params.Add("MessageVersion", 0);
                string Request = JsonConvert.SerializeObject(new { jsonrpc, id, method, @params });

                byte[] OutBuffer = System.Text.Encoding.ASCII.GetBytes(Request);
                Device.OutputPipe.BeginWrite(OutBuffer, 0, OutBuffer.Length, (AsyncResultWrite) =>
                {
                    Device.OutputPipe.EndWrite(AsyncResultWrite);
                }, null);
            }
        }

        public void ExecuteJsonMethodAsync(string JsonMethod)
        {
            ExecuteJsonMethod(JsonMethod, null);
        }

        public delegate void JsonMethodCallbackString(object State, string Result);

        public void ExecuteJsonMethodAsStringAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackString Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Value<string>()); });
        }

        public void ExecuteJsonMethodAsStringAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackString Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Value<string>()); });
        }

        public delegate void JsonMethodCallbackBoolean(object State, bool Result);

        public void ExecuteJsonMethodAsBooleanAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackBoolean Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Value<bool>()); });
        }

        public void ExecuteJsonMethodAsBooleanAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackBoolean Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Value<bool>()); });
        }

        public delegate void JsonMethodCallbackBytes(object State, byte[] Result);

        public void ExecuteJsonMethodAsBytesAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackBytes Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Values<byte>().ToArray()); });
        }

        public void ExecuteJsonMethodAsBytesAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackBytes Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Values<byte>().ToArray()); });
        }

        public delegate void JsonMethodCallbackInteger(object State, int Result);

        public void ExecuteJsonMethodAsIntegerAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackInteger Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Value<int>()); });
        }

        public void ExecuteJsonMethodAsIntegerAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackInteger Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => { Callback(ReturnState, Token.Value<int>()); });
        }

        public delegate void JsonMethodCallbackToken(object State, JToken Result);

        public void ExecuteJsonMethodAsTokenAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackToken Callback)
        {
            byte[] Buffer;
            int Length;

            lock (UsbLock)
            {
                string jsonrpc = "2.0";
                int id = MessageId++;
                string method = JsonMethod;
                Dictionary<string, object> @params = new Dictionary<string, object>();
                if (Params != null)
                {
                    foreach (KeyValuePair<string, object> Param in Params)
                    {
                        if (Param.Value is byte[])
                            @params.Add(Param.Key, ((byte[])Param.Value).Select(b => (int)b).ToArray()); // convert to int-array
                        else
                            @params.Add(Param.Key, Param.Value);
                    }
                }
                @params.Add("MessageVersion", 0);
                string Request = JsonConvert.SerializeObject(new { jsonrpc, id, method, @params });

                byte[] OutBuffer = System.Text.Encoding.ASCII.GetBytes(Request);
                Device.OutputPipe.BeginWrite(OutBuffer, 0, OutBuffer.Length, (AsyncResultWrite) =>
                    {
                        Device.OutputPipe.EndWrite(AsyncResultWrite);
                        Buffer = new byte[0x10000];
                        Device.InputPipe.BeginRead(Buffer, 0, 0x10000, (AsyncResultRead) =>
                            {
                                Length = Device.InputPipe.EndRead(AsyncResultRead);

                                Newtonsoft.Json.Linq.JObject ResultMessage = Newtonsoft.Json.Linq.JObject.Parse(System.Text.ASCIIEncoding.ASCII.GetString(Buffer, 0, Length));

                                JToken ResultToken = ResultMessage.Root.SelectToken("result");
                                if ((ResultToken == null) || (ResultElement == null)) Callback(AsyncResultRead.AsyncState, null);
                                Callback(AsyncResultRead.AsyncState, ResultToken.SelectToken(ResultElement));
                            }, AsyncResultWrite.AsyncState);
                    }, State);
            }
        }

        public void ExecuteJsonMethodAsTokenAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackToken Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, Callback);
        }

        public byte[] ExecuteRawMethod(byte[] RawMethod)
        {
            return ExecuteRawMethod(RawMethod, RawMethod.Length);
        }

        public byte[] ExecuteRawMethod(byte[] RawMethod, int Length)
        {
            byte[] Buffer = new byte[0x8000]; // Should be at least 0x4408 for receiving the GPT packet.
            byte[] Result = null;
            lock (UsbLock)
            {
                Device.OutputPipe.Write(RawMethod, 0, Length);
                try
                {
                    int OutputLength = Device.InputPipe.Read(Buffer);
                    Result = new byte[OutputLength];
                    System.Buffer.BlockCopy(Buffer, 0, Result, 0, OutputLength);
                }
                catch { } // Reboot command looses connection
            }
            return Result;
        }

        public void ExecuteRawVoidMethod(byte[] RawMethod)
        {
            ExecuteRawVoidMethod(RawMethod, RawMethod.Length);
        }

        public void ExecuteRawVoidMethod(byte[] RawMethod, int Length)
        {
            lock (UsbLock)
            {
                Device.OutputPipe.Write(RawMethod, 0, Length);
            }
        }

        public void ResetDevice()
        {
            try
            {
                foreach (var pipe in Device.Pipes)
                {
                    pipe.Abort();
                    pipe.Reset();
                }
            }
            catch { }
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
                return;

            if (disposing)
            {
                if (Device != null)
                    Device.Dispose();
            }

            // Clean unmanaged resources here.
            // (none currently)

            Disposed = true;
        }
    }
}
