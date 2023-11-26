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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
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
            XmlSerializer x = new(PatchDefinitions.GetType(), null, [], new XmlRootAttribute("PatchDefinitions"), "");
            MemoryStream s = new(System.Text.Encoding.ASCII.GetBytes(PatchDefinitionsXmlString));
            PatchDefinitions = (List<PatchDefinition>)x.Deserialize(s);
        }

        internal void WriteDefinitions(string FilePath)
        {
            XmlSerializer x = new(PatchDefinitions.GetType(), null, [], new XmlRootAttribute("PatchDefinitions"), "");

            XmlSerializerNamespaces ns = new();
            ns.Add("", "");

            StreamWriter FileWriter = new(FilePath);
            XmlWriter XmlWriter = XmlWriter.Create(FileWriter, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true, NewLineHandling = NewLineHandling.Entitize });

            FileWriter.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            FileWriter.WriteLine("");
            FileWriter.WriteLine("<!--");
            FileWriter.WriteLine("Copyright(c) 2018, Rene Lergner - @Heathcliff74xda");
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
                _TargetPath = value.TrimEnd(['\\']);
            }
        }

        private DiscUtils.DiscFileSystem _TargetImage = null;
        internal DiscUtils.DiscFileSystem TargetImage
        {
            get
            {
                return _TargetImage;
            }
            set
            {
                _TargetImage = value;
                _TargetPath = "";
            }
        }

        internal bool Patch(string PatchDefinition)
        {
            bool Result = false;
            List<FilePatcher> LoadedFiles = new();

            LogFile.Log("Attempt patch: " + PatchDefinition);

            // Find a matching TargetVersion
            PatchDefinition Definition = PatchDefinitions.Single(d => string.Equals(d.Name, PatchDefinition, StringComparison.CurrentCultureIgnoreCase));
            TargetVersion MatchedVersion = null;
            int VersionIndex = 0;
            foreach (TargetVersion CurrentVersion in Definition.TargetVersions)
            {
                bool Match = true;
                int FileIndex = 0;

                foreach (TargetFile CurrentTargetFile in CurrentVersion.TargetFiles)
                {
                    // Determine target path
                    string TargetPath = null;
                    foreach (TargetRedirection CurrentRedirection in TargetRedirections)
                    {
                        if (CurrentTargetFile.Path.StartsWith(CurrentRedirection.RelativePath, StringComparison.OrdinalIgnoreCase))
                        {
                            TargetPath = Path.Combine(CurrentRedirection.TargetPath + "\\", CurrentTargetFile.Path);
                            break;
                        }
                    }
                    if (TargetPath == null)
                    {
                        TargetPath = Path.Combine(this.TargetPath + "\\", CurrentTargetFile.Path);
                    }

                    // Lookup file
                    FilePatcher CurrentFile = LoadedFiles.SingleOrDefault(f => string.Equals(f.FilePath, TargetPath, StringComparison.CurrentCultureIgnoreCase));
                    if (CurrentFile == null)
                    {
                        CurrentFile = (TargetImage != null) && (!TargetPath.Contains(':'))
                            ? new FilePatcher(TargetPath, TargetImage.OpenFile(TargetPath, FileMode.Open, FileAccess.ReadWrite))
                            : new FilePatcher(TargetPath);

                        LoadedFiles.Add(CurrentFile);
                    }

                    // Compare hash
                    if (!StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashOriginal) &&
                        !StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashPatched))
                    {
                        Match = false;

                        foreach (TargetFile CurrentObsoleteFile in CurrentTargetFile.Obsolete)
                        {
                            if (StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentObsoleteFile.HashPatched))
                            {
                                Match = true; // Found match after all. File is patched with an obsolete version of this patch.
                                break;
                            }
                        }

                        if (!Match)
                        {
                            LogFile.Log("Pattern: " + VersionIndex.ToString() + ", " + FileIndex.ToString());
                            break;
                        }
                    }

                    FileIndex++;
                }

                if (Match)
                {
                    MatchedVersion = CurrentVersion;
                    break;
                }

                VersionIndex++;
            }

            if (MatchedVersion != null)
            {
                LogFile.Log("Apply: " + MatchedVersion.Description);

                foreach (TargetFile CurrentTargetFile in MatchedVersion.TargetFiles)
                {
                    FilePatcher CurrentFile = LoadedFiles.SingleOrDefault(f => string.Equals(f.FilePath, Path.Combine(TargetPath + "\\", CurrentTargetFile.Path), StringComparison.CurrentCultureIgnoreCase));

                    if (!StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashPatched))
                    {
                        CurrentFile.StartPatching();

                        if (!StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashOriginal))
                        {
                            // File is already patched, but with an older version of this patch.
                            // First unpatch back to original.

                            foreach (TargetFile CurrentObsoleteFile in CurrentTargetFile.Obsolete)
                            {
                                if (StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentObsoleteFile.HashPatched))
                                {
                                    foreach (Patch CurrentPatch in CurrentObsoleteFile.Patches)
                                    {
                                        CurrentFile.ApplyPatch(CurrentPatch.Address, CurrentPatch.OriginalBytes);
                                    }
                                    break;
                                }
                            }
                        }

                        foreach (Patch CurrentPatch in CurrentTargetFile.Patches)
                        {
                            CurrentFile.ApplyPatch(CurrentPatch.Address, CurrentPatch.PatchedBytes);
                        }

                        CurrentFile.FinishPatching();
                    }
                }

                Result = true;
            }

            return Result;
        }

        internal void Restore(string PatchDefinition)
        {
            List<FilePatcher> LoadedFiles = new();

            try
            {
                // Find a matching TargetVersion
                PatchDefinition Definition = PatchDefinitions.Single(d => string.Equals(d.Name, PatchDefinition, StringComparison.CurrentCultureIgnoreCase));
                TargetVersion MatchedVersion = null;
                foreach (TargetVersion CurrentVersion in Definition.TargetVersions)
                {
                    bool Match = true;

                    foreach (TargetFile CurrentTargetFile in CurrentVersion.TargetFiles)
                    {
                        // Determine target path
                        string TargetPath = null;
                        foreach (TargetRedirection CurrentRedirection in TargetRedirections)
                        {
                            if (CurrentTargetFile.Path.StartsWith(CurrentRedirection.RelativePath, StringComparison.OrdinalIgnoreCase))
                            {
                                TargetPath = Path.Combine(CurrentRedirection.TargetPath, CurrentTargetFile.Path);
                                break;
                            }
                        }
                        if (TargetPath == null)
                        {
                            TargetPath = Path.Combine(this.TargetPath, CurrentTargetFile.Path);
                        }

                        // Lookup file
                        FilePatcher CurrentFile = LoadedFiles.SingleOrDefault(f => string.Equals(f.FilePath, TargetPath, StringComparison.CurrentCultureIgnoreCase));
                        if (CurrentFile == null)
                        {
                            CurrentFile = new FilePatcher(TargetPath);
                            LoadedFiles.Add(CurrentFile);
                        }

                        // Compare hash
                        if (!StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashOriginal) &&
                            !StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashPatched))
                        {
                            Match = false;

                            foreach (TargetFile CurrentObsoleteFile in CurrentTargetFile.Obsolete)
                            {
                                if (StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentObsoleteFile.HashPatched))
                                {
                                    Match = true; // Found match after all. File is patched with an obsolete version of this patch.
                                    break;
                                }
                            }

                            if (!Match)
                            {
                                break;
                            }
                        }
                    }

                    if (Match)
                    {
                        MatchedVersion = CurrentVersion;
                        break;
                    }
                }

                if (MatchedVersion != null)
                {
                    foreach (TargetFile CurrentTargetFile in MatchedVersion.TargetFiles)
                    {
                        FilePatcher CurrentFile = LoadedFiles.SingleOrDefault(f => string.Equals(f.FilePath, Path.Combine(TargetPath, CurrentTargetFile.Path), StringComparison.CurrentCultureIgnoreCase));

                        if (!StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashOriginal))
                        {
                            CurrentFile.StartPatching();

                            if (StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentTargetFile.HashPatched))
                            {
                                foreach (Patch CurrentPatch in CurrentTargetFile.Patches)
                                {
                                    CurrentFile.ApplyPatch(CurrentPatch.Address, CurrentPatch.OriginalBytes);
                                }
                            }
                            else
                            {
                                foreach (TargetFile CurrentObsoleteFile in CurrentTargetFile.Obsolete)
                                {
                                    if (StructuralComparisons.StructuralEqualityComparer.Equals(CurrentFile.Hash, CurrentObsoleteFile.HashPatched))
                                    {
                                        foreach (Patch CurrentPatch in CurrentObsoleteFile.Patches)
                                        {
                                            CurrentFile.ApplyPatch(CurrentPatch.Address, CurrentPatch.OriginalBytes);
                                        }
                                        break;
                                    }
                                }
                            }

                            CurrentFile.FinishPatching();
                        }
                    }
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
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
                _RelativePath = value.TrimStart(['\\']).TrimEnd(['\\']);
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
                _TargetPath = value.TrimEnd(['\\']);
            }
        }
    }

    internal class FilePatcher
    {
        internal byte[] Hash = null;
        internal string FilePath;
        internal FileSecurity OriginalACL;
        internal Privilege TakeOwnershipPrivilege;
        internal Privilege RestorePrivilege;
        internal Stream Stream = null;

        internal FilePatcher(string FilePath)
        {
            this.FilePath = FilePath;
            using FileStream stream = File.OpenRead(FilePath);
            SHA1Managed sha = new();
            Hash = sha.ComputeHash(stream);
        }

        internal FilePatcher(string FilePath, Stream FileStream)
        {
            if (!FileStream.CanSeek || !FileStream.CanWrite)
            {
                throw new WPinternalsException("Incorrect filestream", "The provided file stream for patching does not support seeking and/or writing.");
            }

            this.FilePath = FilePath;
            this.Stream = FileStream;
            FileStream.Position = 0;
            SHA1Managed sha = new();
            Hash = sha.ComputeHash(FileStream);
            FileStream.Position = 0;
        }

        internal void StartPatching()
        {
            if (FilePath.Contains(':'))
            {
                FileInfo fileInfo = new(FilePath);

                // Enable Take Ownership AND Restore ownership to original owner
                // Take Ownership Privilge is not enough.
                // We need Restore Privilege.
                RestorePrivilege = new Privilege(Privilege.Restore);
                RestorePrivilege.Enable();

                if ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor <= 1))
                {
                    // On Vista or 7
                    TakeOwnershipPrivilege = new Privilege(Privilege.TakeOwnership);
                    TakeOwnershipPrivilege.Enable();
                }

                // Backup original owner and ACL
                OriginalACL = fileInfo.GetAccessControl();

                // And take the original security to create new security rules.
                FileSecurity NewACL = fileInfo.GetAccessControl();

                // Take ownership
                NewACL.SetOwner(WindowsIdentity.GetCurrent().User);
                fileInfo.SetAccessControl(NewACL);

                // And create a new access rule
                NewACL.SetAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, AccessControlType.Allow));
                fileInfo.SetAccessControl(NewACL);

                // Open the file for patching
                Stream = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite);
            }
        }

        internal void ApplyPatch(UInt32 Offset, byte[] Bytes)
        {
            Stream.Position = Offset;
            Stream.Write(Bytes, 0, Bytes.Length);
        }

        internal void FinishPatching()
        {
            // Close file
            Stream.Close();

            if (FilePath.Contains(':'))
            {
                FileInfo fileInfo = new(FilePath);

                // Restore original owner and access rules.
                // The OriginalACL cannot be reused directly.
                FileSecurity NewACL = fileInfo.GetAccessControl();
                NewACL.SetSecurityDescriptorBinaryForm(OriginalACL.GetSecurityDescriptorBinaryForm());
                fileInfo.SetAccessControl(NewACL);

                // Revert to self
                RestorePrivilege.Revert();
                RestorePrivilege.Disable();

                if ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor <= 1))
                {
                    // On Vista or 7
                    TakeOwnershipPrivilege.Revert();
                    TakeOwnershipPrivilege.Disable();
                }
            }
        }
    }

    public class PatchDefinition // Must be public to be serializable
    {
        [XmlAttribute]
        public string Name;

        public List<TargetVersion> TargetVersions = new();
    }

    public class TargetVersion // Must be public to be serializable
    {
        [XmlAttribute]
        public string Description;

        public List<TargetFile> TargetFiles = new();
    }

    public class TargetFile // Must be public to be serializable
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
                _Path = value.TrimStart(['\\']);
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

    public class Patch // Must be public to be serializable
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
                {
                    NewValue = NewValue[2..];
                }

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
