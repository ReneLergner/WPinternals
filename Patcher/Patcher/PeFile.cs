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

// Explanation of PE header here:
// https://msdn.microsoft.com/en-us/library/ms809762.aspx

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using WPinternals;

namespace Patcher
{
    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_DOS_HEADER
    {
        public UInt16 e_magic;
        public UInt16 e_cblp;
        public UInt16 e_cp;
        public UInt16 e_crlc;
        public UInt16 e_cparhdr;
        public UInt16 e_minalloc;
        public UInt16 e_maxalloc;
        public UInt16 e_ss;
        public UInt16 e_sp;
        public UInt16 e_csum;
        public UInt16 e_ip;
        public UInt16 e_cs;
        public UInt16 e_lfarlc;
        public UInt16 e_ovno;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public UInt16[] e_res1;
        public UInt16 e_oemid;
        public UInt16 e_oeminfo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public UInt16[] e_res2;
        public UInt32 e_lfanew;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_NT_HEADERS
    {
        public UInt32 Signature;
        public IMAGE_FILE_HEADER FileHeader;
        public IMAGE_OPTIONAL_HEADER32 OptionalHeader32;
        public IMAGE_OPTIONAL_HEADER64 OptionalHeader64;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_FILE_HEADER
    {
        public UInt16 Machine;
        public UInt16 NumberOfSections;
        public UInt32 TimeDateStamp;
        public UInt32 PointerToSymbolTable;
        public UInt32 NumberOfSymbols;
        public UInt16 SizeOfOptionalHeader;
        public UInt16 Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_OPTIONAL_HEADER32
    {
        public UInt16 Magic;
        public Byte MajorLinkerVersion;
        public Byte MinorLinkerVersion;
        public UInt32 SizeOfCode;
        public UInt32 SizeOfInitializedData;
        public UInt32 SizeOfUninitializedData;
        public UInt32 AddressOfEntryPoint;
        public UInt32 BaseOfCode;
        public UInt32 BaseOfData;
        public UInt32 ImageBase;
        public UInt32 SectionAlignment;
        public UInt32 FileAlignment;
        public UInt16 MajorOperatingSystemVersion;
        public UInt16 MinorOperatingSystemVersion;
        public UInt16 MajorImageVersion;
        public UInt16 MinorImageVersion;
        public UInt16 MajorSubsystemVersion;
        public UInt16 MinorSubsystemVersion;
        public UInt32 Win32VersionValue;
        public UInt32 SizeOfImage;
        public UInt32 SizeOfHeaders;
        public UInt32 CheckSum;
        public UInt16 Subsystem;
        public UInt16 DllCharacteristics;
        public UInt32 SizeOfStackReserve;
        public UInt32 SizeOfStackCommit;
        public UInt32 SizeOfHeapReserve;
        public UInt32 SizeOfHeapCommit;
        public UInt32 LoaderFlags;
        public UInt32 NumberOfRvaAndSizes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public IMAGE_DATA_DIRECTORY[] DataDirectory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_OPTIONAL_HEADER64
    {
        public UInt16 Magic;
        public Byte MajorLinkerVersion;
        public Byte MinorLinkerVersion;
        public UInt32 SizeOfCode;
        public UInt32 SizeOfInitializedData;
        public UInt32 SizeOfUninitializedData;
        public UInt32 AddressOfEntryPoint;
        public UInt32 BaseOfCode;
        public UInt64 ImageBase;
        public UInt32 SectionAlignment;
        public UInt32 FileAlignment;
        public UInt16 MajorOperatingSystemVersion;
        public UInt16 MinorOperatingSystemVersion;
        public UInt16 MajorImageVersion;
        public UInt16 MinorImageVersion;
        public UInt16 MajorSubsystemVersion;
        public UInt16 MinorSubsystemVersion;
        public UInt32 Win32VersionValue;
        public UInt32 SizeOfImage;
        public UInt32 SizeOfHeaders;
        public UInt32 CheckSum;
        public UInt16 Subsystem;
        public UInt16 DllCharacteristics;
        public UInt64 SizeOfStackReserve;
        public UInt64 SizeOfStackCommit;
        public UInt64 SizeOfHeapReserve;
        public UInt64 SizeOfHeapCommit;
        public UInt32 LoaderFlags;
        public UInt32 NumberOfRvaAndSizes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public IMAGE_DATA_DIRECTORY[] DataDirectory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_DATA_DIRECTORY
    {
        public UInt32 VirtualAddress;
        public UInt32 Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_SECTION_HEADER
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public string Name;
        public Misc Misc;
        public UInt32 VirtualAddress;
        public UInt32 SizeOfRawData;
        public UInt32 PointerToRawData;
        public UInt32 PointerToRelocations;
        public UInt32 PointerToLinenumbers;
        public UInt16 NumberOfRelocations;
        public UInt16 NumberOfLinenumbers;
        public UInt32 Characteristics;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Misc
    {
        [FieldOffset(0)]
        public UInt32 PhysicalAddress;
        [FieldOffset(0)]
        public UInt32 VirtualSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_EXPORT_DIRECTORY
    {
        public UInt32 Characteristics;
        public UInt32 TimeDateStamp;
        public UInt16 MajorVersion;
        public UInt16 MinorVersion;
        public UInt32 Name;
        public UInt32 Base;
        public UInt32 NumberOfFunctions;
        public UInt32 NumberOfNames;
        /// <summary>
        /// RVA from base of image
        /// </summary>
        public UInt32 AddressOfFunctions;
        /// <summary>
        /// RVA from base of image
        /// </summary>
        public UInt32 AddressOfNames;
        /// <summary>
        /// RVA from base of image
        /// </summary>
        public UInt32 AddressOfNameOrdinals;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_IMPORT_DESCRIPTOR
    {
        #region union
        /// <summary>
        /// CSharp doesnt really support unions, but they can be emulated by a field offset 0
        /// </summary>
        [FieldOffset(0)]
        public uint Characteristics;            // 0 for terminating null import descriptor
        /// <summary>
        /// RVA to original unbound IAT (PIMAGE_THUNK_DATA)
        /// </summary>
        [FieldOffset(0)]
        public uint OriginalFirstThunk;
        #endregion

        [FieldOffset(4)]
        public uint TimeDateStamp;
        [FieldOffset(8)]
        public uint ForwarderChain;
        [FieldOffset(12)]
        public uint Name;
        [FieldOffset(16)]
        public uint FirstThunk;
    }

    public struct RUNTIME_FUNCTION_64
    {
        public UInt64 RVAofBeginAddress;
        public UInt64 RVAofEndAddress;
        public UInt64 RVAofUnwindData;
    }

    public struct RUNTIME_FUNCTION_32
    {
        public UInt32 RVAofBeginAddress;
        public UInt32 RVAofUnwindData;
    }

    public static class Constants
    {
        public static class SectionFlags
        {
            public const UInt32 IMAGE_SCN_CNT_CODE = 0x00000020;
        }
    }

    internal enum ResourceType
    {
        /// <summary>
        /// Accelerator table.
        /// </summary>
        RT_ACCELERATOR = 9,
        /// <summary>
        /// Animated cursor.
        /// </summary>
        RT_ANICURSOR = 21,
        /// <summary>
        /// Animated icon.
        /// </summary>
        RT_ANIICON = 22,
        /// <summary>
        /// Bitmap resource.
        /// </summary>
        RT_BITMAP = 2,
        /// <summary>
        /// Hardware-dependent cursor resource.
        /// </summary>
        RT_CURSOR = 1,
        /// <summary>
        /// Dialog box.
        /// </summary>
        RT_DIALOG = 5,
        /// <summary>
        /// Allows
        /// </summary>
        RT_DLGINCLUDE = 17,
        /// <summary>
        /// Font resource.
        /// </summary>
        RT_FONT = 8,
        /// <summary>
        /// Font directory resource.
        /// </summary>
        RT_FONTDIR = 7,
        /// <summary>
        /// Hardware-independent cursor resource.
        /// </summary>
        RT_GROUP_CURSOR = RT_CURSOR + 11,
        /// <summary>
        /// Hardware-independent icon resource.
        /// </summary>
        RT_GROUP_ICON = RT_ICON + 11,
        /// <summary>
        /// HTML resource.
        /// </summary>
        RT_HTML = 23,
        /// <summary>
        /// Hardware-dependent icon resource.
        /// </summary>
        RT_ICON = 3,
        /// <summary>
        /// Side-by-Side Assembly Manifest.
        /// </summary>
        RT_MANIFEST = 24,
        /// <summary>
        /// Menu resource.
        /// </summary>
        RT_MENU = 4,
        /// <summary>
        /// Message-table entry.
        /// </summary>
        RT_MESSAGETABLE = 11,
        /// <summary>
        /// Plug and Play resource.
        /// </summary>
        RT_PLUGPLAY = 19,
        /// <summary>
        /// Application-defined resource (raw data).
        /// </summary>
        RT_RCDATA = 10,
        /// <summary>
        /// String-table entry.
        /// </summary>
        RT_STRING = 6,
        /// <summary>
        /// Version resource.
        /// </summary>
        RT_VERSION = 16,
        /// <summary>
        /// VXD
        /// </summary>
        RT_VXD = 20,
        RT_DLGINIT = 240,
        RT_TOOLBAR = 241
    };

    #endregion

    public class PeFile
    {
        #region Fields

        public readonly IMAGE_DOS_HEADER DosHeader;
        public IMAGE_NT_HEADERS NtHeaders;
        private readonly IList<IMAGE_SECTION_HEADER> _sectionHeaders = new List<IMAGE_SECTION_HEADER>();

        public List<Section> Sections = new();
        public List<FunctionDescriptor> Exports = new();
        public List<FunctionDescriptor> Imports = new();
        public List<FunctionDescriptor> RuntimeFunctions = new();
        public byte[] Buffer;
        public UInt64 ImageBase;
        public UInt64 EntryPoint;
        public UInt64 ExportDirectoryVirtualOffset;
        public UInt64 ImportDirectoryVirtualOffset;
        public UInt64 RuntimeDirectoryVirtualOffset;
        public UInt32 RuntimeDirectorySize;

        #endregion

        public PeFile(string Path): this(File.ReadAllBytes(Path))
        {
        }

        public PeFile(byte[] Buffer)
        {
            int P = 0;

            this.Buffer = Buffer;

            // Read MS-DOS header section
            DosHeader = MarshalBytesTo<IMAGE_DOS_HEADER>(Buffer, P);

            // MS-DOS magic number should read 'MZ'
            if (DosHeader.e_magic != 0x5a4d)
            {
                throw new InvalidOperationException("File is not a portable executable.");
            }

            // Read NT Headers
            P = (int)DosHeader.e_lfanew;
            NtHeaders.Signature = MarshalBytesTo<UInt32>(Buffer, P);

            // Make sure we have 'PE' in the pe signature
            if (NtHeaders.Signature != 0x4550)
            {
                throw new InvalidOperationException("Invalid portable executable signature in NT header.");
            }

            P += sizeof(UInt32);
            NtHeaders.FileHeader = MarshalBytesTo<IMAGE_FILE_HEADER>(Buffer, P);

            // Read optional headers
            P += Marshal.SizeOf(typeof(IMAGE_FILE_HEADER));
            if (Is32bitAssembly())
            {
                Load32bitOptionalHeaders(Buffer, P);
                ImageBase = NtHeaders.OptionalHeader32.ImageBase;
                EntryPoint = NtHeaders.OptionalHeader32.AddressOfEntryPoint;
                ExportDirectoryVirtualOffset = NtHeaders.OptionalHeader32.DataDirectory[0].VirtualAddress;
                ImportDirectoryVirtualOffset = NtHeaders.OptionalHeader32.DataDirectory[1].VirtualAddress;
                RuntimeDirectoryVirtualOffset = NtHeaders.OptionalHeader32.DataDirectory[3].VirtualAddress;
                RuntimeDirectorySize = NtHeaders.OptionalHeader32.DataDirectory[3].Size;
            }
            else
            {
                Load64bitOptionalHeaders(Buffer, P);
                ImageBase = NtHeaders.OptionalHeader64.ImageBase;
                EntryPoint = NtHeaders.OptionalHeader64.AddressOfEntryPoint;
                ExportDirectoryVirtualOffset = NtHeaders.OptionalHeader64.DataDirectory[0].VirtualAddress;
                ImportDirectoryVirtualOffset = NtHeaders.OptionalHeader64.DataDirectory[1].VirtualAddress;
                RuntimeDirectoryVirtualOffset = NtHeaders.OptionalHeader64.DataDirectory[3].VirtualAddress;
                RuntimeDirectorySize = NtHeaders.OptionalHeader64.DataDirectory[3].Size;
            }

            // Read Sections
            _sectionHeaders.ToList().ForEach(s =>
            {
                byte[] RawCode = new byte[s.SizeOfRawData];
                System.Buffer.BlockCopy(Buffer, (int)s.PointerToRawData, RawCode, 0, (int)s.SizeOfRawData);

                Sections.Add(new Section { Header = s, Buffer = RawCode, VirtualAddress = s.VirtualAddress + (UInt32)ImageBase, VirtualSize = s.Misc.VirtualSize, IsCode = (s.Characteristics & (uint)Constants.SectionFlags.IMAGE_SCN_CNT_CODE) != 0 });
            });

            // Read Exports
            // TODO: Proper support for 64-bit files
            if (ExportDirectoryVirtualOffset != 0)
            {
                IMAGE_EXPORT_DIRECTORY ExportDirectory = MarshalBytesTo<IMAGE_EXPORT_DIRECTORY>(Buffer, (int)ConvertVirtualOffsetToRawOffset((uint)ExportDirectoryVirtualOffset));
                if (ExportDirectory.AddressOfNames != 0)
                {
                    Section ExportsSection = GetSectionForVirtualAddress((uint)(ImageBase + ExportDirectory.AddressOfNames));
                    UInt32 OffsetNames = (UInt32)(ImageBase + ExportDirectory.AddressOfNames - ExportsSection.VirtualAddress);
                    UInt32 OffsetOrdinals = (UInt32)(ImageBase + ExportDirectory.AddressOfNameOrdinals - ExportsSection.VirtualAddress);
                    UInt32 OffsetFunctions = (UInt32)(ImageBase + ExportDirectory.AddressOfFunctions - ExportsSection.VirtualAddress);
                    string[] ExportNames = new string[ExportDirectory.NumberOfNames];
                    UInt16[] Ordinals = new UInt16[ExportDirectory.NumberOfNames];
                    UInt32[] VirtualAddresses = new UInt32[ExportDirectory.NumberOfFunctions];
                    for (int i = 0; i < ExportDirectory.NumberOfNames; i++)
                    {
                        UInt32 NamesRVA = ByteOperations.ReadUInt32(ExportsSection.Buffer, (UInt32)(OffsetNames + (i * sizeof(UInt32))));
                        UInt32 NameOffset = (UInt32)(NamesRVA + ImageBase - ExportsSection.VirtualAddress);
                        ExportNames[i] = ByteOperations.ReadAsciiString(ExportsSection.Buffer, NameOffset);

                        Ordinals[i] = ByteOperations.ReadUInt16(ExportsSection.Buffer, (UInt32)(OffsetOrdinals + (i * sizeof(UInt16))));
                    }
                    for (int i = 0; i < ExportDirectory.NumberOfFunctions; i++)
                    {
                        VirtualAddresses[i] = ByteOperations.ReadUInt32(ExportsSection.Buffer, (UInt32)(OffsetFunctions + (i * sizeof(UInt32))));
                        VirtualAddresses[i] -= VirtualAddresses[i] % 2; // Round down for Thumb2
                    }
                    for (int i = 0; i < ExportDirectory.NumberOfNames; i++)
                    {
                        Exports.Add(new FunctionDescriptor() { Name = ExportNames[i], VirtualAddress = (UInt32)(ImageBase + VirtualAddresses[Ordinals[i]]) });
                    }
                }
            }

            // Read Imports
            // TODO: Proper support for 64-bit files
            if (ImportDirectoryVirtualOffset != 0)
            {
                Section ImportsSection = GetSectionForVirtualAddress((uint)(ImageBase + ImportDirectoryVirtualOffset));
                IMAGE_IMPORT_DESCRIPTOR ImportDirectory;
                do
                {
                    ImportDirectory = MarshalBytesTo<IMAGE_IMPORT_DESCRIPTOR>(ImportsSection.Buffer, (int)(ImportDirectoryVirtualOffset - (ImportsSection.VirtualAddress - ImageBase)));
                    if (ImportDirectory.OriginalFirstThunk != 0)
                    {
                        // ImportDirectory.OriginalFirstThunk is the VirtualOffset to an array of VirtualOffsets. They point to a struct with a word-value, followed by a zero-terminated ascii-string, which is the name of the import.
                        // ImportDirectory.FirstThunk points to an array pointers which is the actual import table.
                        UInt32 NameArrayOffset = ImportDirectory.OriginalFirstThunk - (ImportsSection.VirtualAddress - (UInt32)ImageBase);
                        UInt32 NameOffset;
                        int i = 0;
                        do
                        {
                            NameOffset = ByteOperations.ReadUInt32(ImportsSection.Buffer, NameArrayOffset);
                            if ((NameOffset < (ImportsSection.VirtualAddress - ImageBase)) || (NameOffset >= (ImportsSection.VirtualAddress + ImportsSection.VirtualSize - ImageBase)))
                                NameOffset = 0; // ImportDirectory.OriginalFirstThunk seems to contain Characteristics, not an offset to an array.
                            NameArrayOffset += sizeof(UInt32);
                            if (NameOffset != 0)
                            {
                                string Name = ByteOperations.ReadAsciiString(ImportsSection.Buffer, NameOffset + 2 - (ImportsSection.VirtualAddress - (UInt32)ImageBase));
                                Imports.Add(new FunctionDescriptor() { Name = Name, VirtualAddress = ImportDirectory.FirstThunk + (UInt32)ImageBase + (UInt32)(i * sizeof(UInt32)) });
                                i++;
                            }
                        }
                        while (NameOffset != 0);

                        ImportDirectoryVirtualOffset += (UInt64)Marshal.SizeOf(typeof(IMAGE_IMPORT_DESCRIPTOR));
                    }
                }
                while (ImportDirectory.OriginalFirstThunk != 0);
            }

            // Read Runtime functions
            // TODO: Proper support for 64-bit files
            if (RuntimeDirectoryVirtualOffset != 0)
            {
                Section RuntimeSection = GetSectionForVirtualAddress((uint)(ImageBase + RuntimeDirectoryVirtualOffset));
                RUNTIME_FUNCTION_32 RuntimeFunction;
                for (int i = 0; i < (RuntimeDirectorySize / Marshal.SizeOf(typeof(RUNTIME_FUNCTION_32))); i++)
                {
                    RuntimeFunction = MarshalBytesTo<RUNTIME_FUNCTION_32>(RuntimeSection.Buffer, (int)(RuntimeDirectoryVirtualOffset - (RuntimeSection.VirtualAddress - ImageBase)) + (i * Marshal.SizeOf(typeof(RUNTIME_FUNCTION_32))));
                    RuntimeFunctions.Add(new FunctionDescriptor() { Name = null, VirtualAddress = (UInt32)(RuntimeFunction.RVAofBeginAddress + ImageBase) });
                }
            }
        }

        public UInt32 GetChecksumOffset()
        {
            return ByteOperations.ReadUInt32(Buffer, 0x3C) + +0x58;
        }

        internal UInt32 CalculateChecksum()
        {
            UInt32 Checksum = 0;
            UInt32 Hi;

            // Clear file checksum
            // ByteOperations.WriteUInt32(PEFile, GetChecksumOffset(), 0);
            UInt32 ChecksumOffset = GetChecksumOffset();

            for (UInt32 i = 0; i < ((UInt32)Buffer.Length & 0xfffffffe); i += 2)
            {
                if ((i < ChecksumOffset) || (i >= (ChecksumOffset + 4)))
                    Checksum += ByteOperations.ReadUInt16(Buffer, i);

                Hi = Checksum >> 16;
                if (Hi != 0)
                {
                    Checksum = Hi + (Checksum & 0xFFFF);
                }
            }
            if ((Buffer.Length % 2) != 0)
            {
                Checksum += (UInt32)ByteOperations.ReadUInt8(Buffer, (UInt32)Buffer.Length - 1);
                Hi = Checksum >> 16;
                if (Hi != 0)
                {
                    Checksum = Hi + (Checksum & 0xFFFF);
                }
            }
            Checksum += (UInt32)Buffer.Length;

            // Write file checksum
            // ByteOperations.WriteUInt32(Buffer, GetChecksumOffset(), Checksum);

            return Checksum;
        }

        public IMAGE_DOS_HEADER GetDOSHeader()
        {
            return DosHeader;
        }

        public UInt32 GetPESignature()
        {
            return NtHeaders.Signature;
        }

        public IMAGE_FILE_HEADER GetFileHeader()
        {
            return NtHeaders.FileHeader;
        }

        public IMAGE_OPTIONAL_HEADER32 GetOptionalHeaders32()
        {
            return NtHeaders.OptionalHeader32;
        }

        public IMAGE_OPTIONAL_HEADER64 GetOptionalHeaders64()
        {
            return NtHeaders.OptionalHeader64;
        }

        public IList<IMAGE_SECTION_HEADER> GetSectionHeaders()
        {
            return _sectionHeaders;
        }

        public bool Is32bitAssembly()
        {
            return (NtHeaders.FileHeader.Characteristics & 0x0100) == 0x0100;
        }

        private void Load64bitOptionalHeaders(byte[] Buffer, int Offset)
        {
            NtHeaders.OptionalHeader64 = MarshalBytesTo<IMAGE_OPTIONAL_HEADER64>(Buffer, Offset);

            // Should have 10 data directories
            if (NtHeaders.OptionalHeader64.NumberOfRvaAndSizes != 0x10)
            {
                throw new InvalidOperationException("Invalid number of data directories in NT header");
            }
            Offset += Marshal.SizeOf(typeof(IMAGE_OPTIONAL_HEADER64));

            for (int i = 0; i < NtHeaders.FileHeader.NumberOfSections; i++)
            {
                _sectionHeaders.Add(MarshalBytesTo<IMAGE_SECTION_HEADER>(Buffer, Offset));
                Offset += Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER));
            }
        }

        private void Load32bitOptionalHeaders(byte[] Buffer, int Offset)
        {
            NtHeaders.OptionalHeader32 = MarshalBytesTo<IMAGE_OPTIONAL_HEADER32>(Buffer, Offset);

            // Should have 10 data directories
            if (NtHeaders.OptionalHeader32.NumberOfRvaAndSizes != 0x10)
            {
                throw new InvalidOperationException("Invalid number of data directories in NT header");
            }
            Offset += Marshal.SizeOf(typeof(IMAGE_OPTIONAL_HEADER32));

            for (int i = 0; i < NtHeaders.FileHeader.NumberOfSections; i++)
            {
                _sectionHeaders.Add(MarshalBytesTo<IMAGE_SECTION_HEADER>(Buffer, Offset));
                Offset += Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER));
            }
        }

        public UInt32 ConvertVirtualOffsetToRawOffset(UInt32 VirtualOffset)
        {
            // TODO: Add 64-bit support
            if (VirtualOffset < (Sections.OrderBy(s => s.VirtualAddress).First().VirtualAddress - GetOptionalHeaders32().ImageBase))
                return VirtualOffset;

            IMAGE_SECTION_HEADER? SectionHeaderSelection = _sectionHeaders.FirstOrDefault(h => (h.VirtualAddress <= VirtualOffset) && ((h.VirtualAddress + h.SizeOfRawData) > VirtualOffset));
            if (SectionHeaderSelection == null)
                throw new ArgumentOutOfRangeException();

            IMAGE_SECTION_HEADER SectionHeader = (IMAGE_SECTION_HEADER)SectionHeaderSelection;

            if (string.IsNullOrEmpty(SectionHeader.Name) || (SectionHeader.SizeOfRawData == 0))
                throw new ArgumentOutOfRangeException();

            return SectionHeader.PointerToRawData + (VirtualOffset - SectionHeader.VirtualAddress);
        }

        public UInt32 ConvertVirtualAddressToRawOffset(UInt32 VirtualAddress)
        {
            return ConvertVirtualOffsetToRawOffset((UInt32)(VirtualAddress - ImageBase));
        }

        internal uint ConvertRawOffsetToVirtualAddress(uint RawOffset)
        {
            // TODO: Add 64-bit support
            if (RawOffset < Sections.OrderBy(s => s.VirtualAddress).First().Header.PointerToRawData)
                return RawOffset + GetOptionalHeaders32().ImageBase;

            IMAGE_SECTION_HEADER? SectionHeaderSelection = _sectionHeaders.FirstOrDefault(h => (h.PointerToRawData <= RawOffset) && ((h.PointerToRawData + h.SizeOfRawData) > RawOffset));
            if (SectionHeaderSelection == null)
                throw new ArgumentOutOfRangeException();

            IMAGE_SECTION_HEADER SectionHeader = (IMAGE_SECTION_HEADER)SectionHeaderSelection;

            if (string.IsNullOrEmpty(SectionHeader.Name) || (SectionHeader.SizeOfRawData == 0))
                throw new ArgumentOutOfRangeException();

            return RawOffset - SectionHeader.PointerToRawData + SectionHeader.VirtualAddress + GetOptionalHeaders32().ImageBase;
        }

        public Section GetSectionForVirtualAddress(UInt32 VirtualAddress)
        {
            return Sections.Find(s => (VirtualAddress >= s.VirtualAddress) && (VirtualAddress < (s.VirtualAddress + s.VirtualSize)));
        }

        private static T MarshalBytesTo<T>(BinaryReader reader)
        {
            // Unmanaged data
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

            // Create a pointer to the unmanaged data pinned in memory to be accessed by unmanaged code
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            // Use our previously created pointer to unmanaged data and marshal to the specified type
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

            // Deallocate pointer
            handle.Free();

            return theStructure;
        }

        private static T MarshalBytesTo<T>(byte[] Binary, int Offset)
        {
            // Unmanaged data
            byte[] bytes = new byte[Marshal.SizeOf(typeof(T))];
            System.Buffer.BlockCopy(Binary, Offset, bytes, 0, Marshal.SizeOf(typeof(T)));

            // Create a pointer to the unmanaged data pinned in memory to be accessed by unmanaged code
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            // Use our previously created pointer to unmanaged data and marshal to the specified type
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

            // Deallocate pointer
            handle.Free();

            return theStructure;
        }

        internal byte[] GetResource(int[] Index)
        {
            UInt32 PEPointer = ByteOperations.ReadUInt32(Buffer, 0x3C);
            UInt16 OptionalHeaderSize = ByteOperations.ReadUInt16(Buffer, PEPointer + 0x14);
            UInt32 SectionTablePointer = PEPointer + 0x18 + OptionalHeaderSize;
            UInt16 SectionCount = ByteOperations.ReadUInt16(Buffer, PEPointer + 0x06);
            UInt32? ResourceSectionEntryPointer = null;
            for (int i = 0; i < SectionCount; i++)
            {
                string SectionName = ByteOperations.ReadAsciiString(Buffer, (UInt32)(SectionTablePointer + (i * 0x28)), 8);
                int e = SectionName.IndexOf('\0');
                if (e >= 0)
                    SectionName = SectionName.Substring(0, e);
                if (SectionName == ".rsrc")
                {
                    ResourceSectionEntryPointer = (UInt32)(SectionTablePointer + (i * 0x28));
                    break;
                }
            }
            if (ResourceSectionEntryPointer == null)
                throw new Exception("Resource-section not found");
            UInt32 ResourceRawSize = ByteOperations.ReadUInt32(Buffer, (UInt32)ResourceSectionEntryPointer + 0x10);
            UInt32 ResourceRawPointer = ByteOperations.ReadUInt32(Buffer, (UInt32)ResourceSectionEntryPointer + 0x14);
            UInt32 ResourceVirtualPointer = ByteOperations.ReadUInt32(Buffer, (UInt32)ResourceSectionEntryPointer + 0x0C);

            UInt32 p = ResourceRawPointer;
            for (int i = 0; i < Index.Length; i++)
            {
                UInt16 ResourceNamedEntryCount = ByteOperations.ReadUInt16(Buffer, p + 0x0c);
                UInt16 ResourceIdEntryCount = ByteOperations.ReadUInt16(Buffer, p + 0x0e);
                for (int j = ResourceNamedEntryCount; j < ResourceNamedEntryCount + ResourceIdEntryCount; j++)
                {
                    UInt32 ResourceID = ByteOperations.ReadUInt32(Buffer, (UInt32)(p + 0x10 + (j * 8)));
                    UInt32 NextPointer = ByteOperations.ReadUInt32(Buffer, (UInt32)(p + 0x10 + (j * 8) + 4));
                    if (ResourceID == (UInt32)Index[i])
                    {
                        // Check high bit
                        if ((NextPointer & 0x80000000) == 0 != (i == (Index.Length - 1)))
                            throw new Exception("Bad resource path");

                        p = ResourceRawPointer + (NextPointer & 0x7fffffff);
                        break;
                    }
                }
            }

            UInt32 ResourceValuePointer = ByteOperations.ReadUInt32(Buffer, p) - ResourceVirtualPointer + ResourceRawPointer;
            UInt32 ResourceValueSize = ByteOperations.ReadUInt32(Buffer, p + 4);

            byte[] ResourceValue = new byte[ResourceValueSize];
            Array.Copy(Buffer, ResourceValuePointer, ResourceValue, 0, ResourceValueSize);

            return ResourceValue;
        }

        internal Version GetFileVersion()
        {
            byte[] version = GetResource(new int[] { (int)ResourceType.RT_VERSION, 1, 1033 });

            // RT_VERSION format:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms647001(v=vs.85).aspx
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646997(v=vs.85).aspx
            const UInt32 FixedFileInfoPointer = 0x28;
            UInt16 Major = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0A);
            UInt16 Minor = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x08);
            UInt16 Build = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0E);
            UInt16 Revision = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0C);

            return new Version(Major, Minor, Build, Revision);
        }

        internal Version GetProductVersion()
        {
            byte[] version = GetResource(new int[] { (int)ResourceType.RT_VERSION, 1, 1033 });

            // RT_VERSION format:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms647001(v=vs.85).aspx
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646997(v=vs.85).aspx
            const UInt32 FixedFileInfoPointer = 0x28;
            UInt16 Major = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x12);
            UInt16 Minor = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x10);
            UInt16 Build = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x16);
            UInt16 Revision = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x14);

            return new Version(Major, Minor, Build, Revision);
        }
    }

    public class Section
    {
        public IMAGE_SECTION_HEADER Header;
        public byte[] Buffer;
        public UInt32 VirtualAddress;
        public UInt32 VirtualSize;
        public bool IsCode;
    }

    public class FunctionDescriptor
    {
        public string Name;
        public UInt32 VirtualAddress;
    }
}
