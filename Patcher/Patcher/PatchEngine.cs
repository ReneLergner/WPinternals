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

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace WPinternals
{
    internal class PatchEngine
    {
        internal List<PatchDefinition> PatchDefinitions = new();
        internal readonly List<TargetRedirection> TargetRedirections = new();

        internal PatchEngine() { }

        internal PatchEngine(string PatchDefinitionsXmlString)
        {
            XmlSerializer x = new(PatchDefinitions.GetType(), null, Array.Empty<Type>(), new XmlRootAttribute("PatchDefinitions"), "");
            MemoryStream s = new(System.Text.Encoding.ASCII.GetBytes(PatchDefinitionsXmlString));
            PatchDefinitions = (List<PatchDefinition>)x.Deserialize(s);
        }

        internal void WriteDefinitions(string FilePath)
        {
            XmlSerializer x = new(PatchDefinitions.GetType(), null, Array.Empty<Type>(), new XmlRootAttribute("PatchDefinitions"), "");

            XmlSerializerNamespaces ns = new();
            ns.Add("", "");

            System.IO.StreamWriter FileWriter = new(FilePath);
            XmlWriter XmlWriter = XmlWriter.Create(FileWriter, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true, NewLineHandling = NewLineHandling.Entitize });

            FileWriter.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            FileWriter.WriteLine("");
            FileWriter.WriteLine("<!--");
            FileWriter.WriteLine("Copyright(c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda");
            FileWriter.WriteLine("");
            FileWriter.WriteLine("Permission is hereby granted, free of charge, to any person obtaining a");
            FileWriter.WriteLine("copy of this software and associated documentation files(the \"Software\"),");
            FileWriter.WriteLine("to deal in the Software without restriction, including without limitation");
            FileWriter.WriteLine("the rights to use, copy, modify, merge, publish, distribute, sublicense,");
            FileWriter.WriteLine("and / or sell copies of the Software, and to permit persons to whom the");
            FileWriter.WriteLine("Software is furnished to do so, subject to the following conditions:");
            FileWriter.WriteLine("");
            FileWriter.WriteLine("The above copyright notice and this permission notice shall be included in");
            FileWriter.WriteLine("all copies or substantial portions of the Software.");
            FileWriter.WriteLine("");
            FileWriter.WriteLine("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR");
            FileWriter.WriteLine("IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,");
            FileWriter.WriteLine("FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE");
            FileWriter.WriteLine("AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER");
            FileWriter.WriteLine("LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING");
            FileWriter.WriteLine("FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER");
            FileWriter.WriteLine("DEALINGS IN THE SOFTWARE.");
            FileWriter.WriteLine("-->");
            FileWriter.WriteLine("");

            x.Serialize(XmlWriter, PatchDefinitions, ns);

            FileWriter.Close();
        }

        private string _TargetPath = null;
        internal string TargetPath
        {
            get
            {
                return _TargetPath;
            }
            set
            {
                _TargetPath = value.TrimEnd(new char[] { '\\' });
            }
        }
    }

    internal class TargetRedirection
    {
        private string _RelativePath;
        private string _TargetPath;

        internal TargetRedirection(string RelativePath, string TargetPath)
        {
            this.RelativePath = RelativePath;
            this.TargetPath = TargetPath;
        }

        internal string RelativePath
        {
            get
            {
                return _RelativePath;
            }
            set
            {
                _RelativePath = value.TrimStart(new char[] { '\\' }).TrimEnd(new char[] { '\\' });
            }
        }

        internal string TargetPath
        {
            get
            {
                return _TargetPath;
            }
            set
            {
                _TargetPath = value.TrimEnd(new char[] { '\\' });
            }
        }
    }

    /// <summary>
    /// Must be public to be serializable
    /// </summary>
    public class PatchDefinition
    {
        [XmlAttribute]
        public string Name;

        public List<TargetVersion> TargetVersions = new();
    }

    /// <summary>
    /// Must be public to be serializable
    /// </summary>
    public class TargetVersion
    {
        [XmlAttribute]
        public string Description;

        public List<TargetFile> TargetFiles = new();
    }

    /// <summary>
    /// Must be public to be serializable
    /// </summary>
    public class TargetFile
    {
        private string _Path;
        [XmlAttribute]
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                _Path = value.TrimStart(new char[] { '\\' });
            }
        }

        [XmlIgnore]
        public byte[] HashOriginal;
        [XmlAttribute("HashOriginal")]
        public string HashOriginalAsString
        {
            get
            {
                return Converter.ConvertHexToString(HashOriginal, "");
            }
            set
            {
                HashOriginal = Converter.ConvertStringToHex(value);
            }
        }

        [XmlIgnore]
        public byte[] HashPatched;
        [XmlAttribute("HashPatched")]
        public string HashPatchedAsString
        {
            get
            {
                return Converter.ConvertHexToString(HashPatched, "");
            }
            set
            {
                HashPatched = Converter.ConvertStringToHex(value);
            }
        }

        public List<Patch> Patches = new();
        public List<TargetFile> Obsolete = new();
    }

    /// <summary>
    /// Must be public to be serializable
    /// </summary>
    public class Patch
    {
        [XmlIgnore]
        public UInt32 Address;
        [XmlAttribute("Address")]
        public string AddressAsString
        {
            get
            {
                return "0x" + Address.ToString("X8");
            }
            set
            {
                string NewValue = value;
                if (NewValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    NewValue = NewValue[2..];
                Address = Convert.ToUInt32(NewValue, 16);
            }
        }

        [XmlIgnore]
        public byte[] OriginalBytes;
        [XmlAttribute("OriginalBytes")]
        public string OriginalBytesAsString
        {
            get
            {
                return Converter.ConvertHexToString(OriginalBytes, "");
            }
            set
            {
                OriginalBytes = Converter.ConvertStringToHex(value);
            }
        }

        [XmlIgnore]
        public byte[] PatchedBytes;
        [XmlAttribute("PatchedBytes")]
        public string PatchedBytesAsString
        {
            get
            {
                return Converter.ConvertHexToString(PatchedBytes, "");
            }
            set
            {
                PatchedBytes = Converter.ConvertStringToHex(value);
            }
        }
    }
}
