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
using System.Collections.Generic;

namespace WPinternals
{
    internal static class Terminal
    {
        public static TerminalResponse Parse(byte[] Buffer, int Offset)
        {
            TerminalResponse Response = new();

            // Get root node
            if (Buffer.Length >= (Offset + 8))
            {
                int NodeNumber = BitConverter.ToInt32(Buffer, Offset);
                int NodeSize = BitConverter.ToInt32(Buffer, Offset + 4);
                int End = NodeSize + Offset + 8;
                int Index = Offset + 8;
                if ((NodeNumber == 0x10000) && (End <= Buffer.Length))
                {
                    // Get subnodes
                    while (Index < End)
                    {
                        NodeNumber = BitConverter.ToInt32(Buffer, Index);
                        NodeSize = BitConverter.ToInt32(Buffer, Index + 4);
                        byte[] Raw = new byte[NodeSize];
                        Array.Copy(Buffer, Index + 8, Raw, 0, NodeSize);
                        Response.RawEntries.Add(NodeNumber, Raw);
                        Index += NodeSize + 8;
                    }
                }
            }

            // Parse subnodes
            Response.RawEntries.TryGetValue(3, out Response.PublicId);
            Response.RawEntries.TryGetValue(7, out Response.RootKeyHash);

            return Response;
        }
    }

    internal class TerminalResponse
    {
        public Dictionary<int, byte[]> RawEntries = new();
        public byte[] PublicId = null;
        public byte[] RootKeyHash = null;
    }
}
