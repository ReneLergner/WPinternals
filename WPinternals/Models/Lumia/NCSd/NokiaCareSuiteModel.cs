﻿// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WPinternals.HelperClasses;

namespace WPinternals.Models.Lumia.NCSd
{
    internal class NokiaCareSuiteModel : NokiaPhoneModel
    {
        private int MessageId = 0;

        public NokiaCareSuiteModel(string DevicePath) : base(DevicePath)
        {

        }

        private JsonElement? ExecuteJsonMethodAsJsonToken(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            byte[] Buffer;
            int Length;

            lock (UsbLock)
            {
                const string jsonrpc = "2.0";
                int id = MessageId++;
                string method = JsonMethod;
                Dictionary<string, object> @params = [];
                if (Params != null)
                {
                    foreach (KeyValuePair<string, object> Param in Params)
                    {
                        if (Param.Value is byte[] v)
                        {
                            @params.Add(Param.Key, v.Select(b => (int)b).ToArray()); // convert to int-array
                        }
                        else
                        {
                            @params.Add(Param.Key, Param.Value);
                        }
                    }
                }
                @params.Add("MessageVersion", 0);
                string Request = JsonSerializer.Serialize(new
                {
                    jsonrpc,
                    id,
                    method,
                    @params
                });
                Device.OutputPipe.Write(System.Text.Encoding.ASCII.GetBytes(Request));

                Buffer = new byte[0x10000];
                Length = Device.InputPipe.Read(Buffer);
            }

            string ResultString = System.Text.Encoding.ASCII.GetString(Buffer, 0, Length);
            JsonDocument ResultMessage = JsonDocument.Parse(ResultString);

            try
            {
                bool result = ResultMessage.RootElement.TryGetProperty("result", out JsonElement ResultToken);
                if (!result || ResultElement == null)
                {
                    return null;
                }

                return ResultToken.GetProperty(ResultElement);
            }
            catch (Exception ex)
            {
                LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                LogFile.Log(ex.Message, LogType.FileAndConsole);
                LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                return null;
            }
        }

        public void ExecuteJsonMethod(string JsonMethod, Dictionary<string, object> Params)
        {
            _ = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, null);
        }

        public string ExecuteJsonMethodAsString(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, ResultElement);
            if (Token == null)
            {
                return null;
            }

            return Token.Value.GetString();
        }

        public string ExecuteJsonMethodAsString(string JsonMethod, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null)
            {
                return null;
            }

            return Token.Value.GetString();
        }

        public int ExecuteJsonMethodAsInteger(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, ResultElement);
            if (Token == null)
            {
                return 0;
            }

            return Token.Value.GetInt32();
        }

        public int ExecuteJsonMethodAsInteger(string JsonMethod, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null)
            {
                return 0;
            }

            return Token.Value.GetInt32();
        }

        public byte[] ExecuteJsonMethodAsBytes(string JsonMethod, Dictionary<string, object> Params, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, Params, ResultElement);
            if (Token == null)
            {
                return null;
            }

            return Token.Value.EnumerateArray().Select(x => x.GetByte()).ToArray();
        }

        public byte[] ExecuteJsonMethodAsBytes(string JsonMethod, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null)
            {
                return null;
            }

            return Token.Value.EnumerateArray().Select(x => x.GetByte()).ToArray();
        }

        public bool? ExecuteJsonMethodAsBoolean(string JsonMethod, string ResultElement)
        {
            JsonElement? Token = ExecuteJsonMethodAsJsonToken(JsonMethod, null, ResultElement);
            if (Token == null)
            {
                return null;
            }

            if (Token.Value.ValueKind == JsonValueKind.String)
            {
                return Token.Value.GetString().Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }

            return Token.Value.GetBoolean();
        }

        public void ExecuteJsonMethodAsync(string JsonMethod, Dictionary<string, object> Params)
        {
            lock (UsbLock)
            {
                const string jsonrpc = "2.0";
                int id = MessageId++;
                string method = JsonMethod;
                Dictionary<string, object> @params = [];
                if (Params != null)
                {
                    foreach (KeyValuePair<string, object> Param in Params)
                    {
                        if (Param.Value is byte[] v)
                        {
                            @params.Add(Param.Key, v.Select(b => (int)b).ToArray()); // convert to int-array
                        }
                        else
                        {
                            @params.Add(Param.Key, Param.Value);
                        }
                    }
                }
                @params.Add("MessageVersion", 0);
                string Request = JsonSerializer.Serialize(new
                {
                    jsonrpc,
                    id,
                    method,
                    @params
                });

                byte[] OutBuffer = System.Text.Encoding.ASCII.GetBytes(Request);
                Device.OutputPipe.BeginWrite(OutBuffer, 0, OutBuffer.Length, (AsyncResultWrite) => Device.OutputPipe.EndWrite(AsyncResultWrite), null);
            }
        }

        public void ExecuteJsonMethodAsync(string JsonMethod)
        {
            ExecuteJsonMethod(JsonMethod, null);
        }

        public delegate void JsonMethodCallbackString(object State, string Result);

        public void ExecuteJsonMethodAsStringAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackString Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.GetRawText()));
        }

        public void ExecuteJsonMethodAsStringAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackString Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.GetRawText()));
        }

        public delegate void JsonMethodCallbackBoolean(object State, bool Result);

        public void ExecuteJsonMethodAsBooleanAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackBoolean Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.GetBoolean()));
        }

        public void ExecuteJsonMethodAsBooleanAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackBoolean Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.GetBoolean()));
        }

        public delegate void JsonMethodCallbackBytes(object State, byte[] Result);

        public void ExecuteJsonMethodAsBytesAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackBytes Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.EnumerateArray().Select(x => x.GetByte()).ToArray()));
        }

        public void ExecuteJsonMethodAsBytesAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackBytes Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.EnumerateArray().Select(x => x.GetByte()).ToArray()));
        }

        public delegate void JsonMethodCallbackInteger(object State, int Result);

        public void ExecuteJsonMethodAsIntegerAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackInteger Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, Params, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.GetInt32()));
        }

        public void ExecuteJsonMethodAsIntegerAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackInteger Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, (ReturnState, Token) => Callback(ReturnState, Token.Value.GetInt32()));
        }

        public delegate void JsonMethodCallbackToken(object State, JsonElement? Result);

        public void ExecuteJsonMethodAsTokenAsync(string JsonMethod, Dictionary<string, object> Params, string ResultElement, object State, JsonMethodCallbackToken Callback)
        {
            byte[] Buffer;
            int Length;

            lock (UsbLock)
            {
                const string jsonrpc = "2.0";
                int id = MessageId++;
                string method = JsonMethod;
                Dictionary<string, object> @params = [];
                if (Params != null)
                {
                    foreach (KeyValuePair<string, object> Param in Params)
                    {
                        if (Param.Value is byte[] v)
                        {
                            @params.Add(Param.Key, v.Select(b => (int)b).ToArray()); // convert to int-array
                        }
                        else
                        {
                            @params.Add(Param.Key, Param.Value);
                        }
                    }
                }
                @params.Add("MessageVersion", 0);
                string Request = JsonSerializer.Serialize(new
                {
                    jsonrpc,
                    id,
                    method,
                    @params
                });

                byte[] OutBuffer = System.Text.Encoding.ASCII.GetBytes(Request);
                Device.OutputPipe.BeginWrite(OutBuffer, 0, OutBuffer.Length, (AsyncResultWrite) =>
                {
                    Device.OutputPipe.EndWrite(AsyncResultWrite);
                    Buffer = new byte[0x10000];
                    Device.InputPipe.BeginRead(Buffer, 0, 0x10000, (AsyncResultRead) =>
                    {
                        Length = Device.InputPipe.EndRead(AsyncResultRead);

                        JsonDocument ResultMessage = JsonDocument.Parse(System.Text.Encoding.ASCII.GetString(Buffer, 0, Length));

                        JsonElement? ResultToken = ResultMessage.RootElement.GetProperty("result");
                        if (ResultToken == null || ResultElement == null)
                        {
                            Callback(AsyncResultRead.AsyncState, null);
                        }

                        Callback(AsyncResultRead.AsyncState, ResultToken.Value.GetProperty(ResultElement));
                    }, AsyncResultWrite.AsyncState);
                }, State);
            }
        }

        public void ExecuteJsonMethodAsTokenAsync(string JsonMethod, string ResultElement, object State, JsonMethodCallbackToken Callback)
        {
            ExecuteJsonMethodAsTokenAsync(JsonMethod, null, ResultElement, State, Callback);
        }
    }
}
