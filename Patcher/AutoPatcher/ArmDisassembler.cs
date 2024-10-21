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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gee.External.Capstone;
using Gee.External.Capstone.Arm;
//using PeNet;

namespace Patcher
{
    public static class ArmDisassembler
    {
        public static AnalyzedFile Analyze(string FilePath, string AsmPath = null)
        {
            PeFile File = new(FilePath);

            SortedList<UInt32, ArmInstruction> AnalyzedCode = new(0x1000000); // Default capacity of 0x100000 was not enough for analyzing ntoskrnl.exe

            if ((AsmPath != null) && System.IO.File.Exists(AsmPath))
            {
                using StreamReader Reader = new(AsmPath);
                while (Reader.Peek() >= 0)
                {
                    ArmInstruction Instruction = new(Reader.ReadLine());
                    AnalyzedCode.Add(Instruction.Address, Instruction);
                }
            }
            else
            {
                CapstoneArmDisassembler Disassembler = CapstoneDisassembler.CreateArmDisassembler(ArmDisassembleMode.Thumb);

                // Initially use a Dictionary and sort it afterwards. For analyzing ntoskrnl.exe this is about 60 times faster than using a SortedList from the start.
                // Default capacity of 0x100000 was not enough for analyzing ntoskrnl.exe
                Dictionary<UInt32, ArmInstruction> TempCode = new(0x1000000);

                // Analyze from entrypoint
                Analyze(Disassembler, File.Sections, TempCode, (UInt32)(File.ImageBase + File.EntryPoint));

                // Analyze from exports
                foreach (FunctionDescriptor Function in File.Exports)
                    Analyze(Disassembler, File.Sections, TempCode, (UInt32)Function.VirtualAddress);

                // Analyze from imports
                foreach (FunctionDescriptor Function in File.Imports)
                    Analyze(Disassembler, File.Sections, TempCode, (UInt32)Function.VirtualAddress);

                // Analyze from runtime-functions
                foreach (FunctionDescriptor Function in File.RuntimeFunctions)
                    Analyze(Disassembler, File.Sections, TempCode, (UInt32)Function.VirtualAddress);

                // Sort the instructions.
                // SortedList is used, because it can be indexed by value (not only by key).
                List<UInt32> Keys = TempCode.Keys.ToList();
                Keys.Sort();
                foreach (UInt32 Key in Keys)
                    AnalyzedCode.Add(Key, TempCode[Key]);

                if (AsmPath != null)
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AsmPath));

                    using StreamWriter Writer = new(AsmPath, false);
                    for (int i = 0; i < AnalyzedCode.Count; i++)
                    {
                        Writer.WriteLine(AnalyzedCode.Values[i].ToString());
                    }
                }
            }

            return new AnalyzedFile() { File = File, Code = AnalyzedCode };
        }

        public static void Analyze(CapstoneArmDisassembler Disassembler, List<Section> Sections, Dictionary<UInt32, ArmInstruction> AnalyzedCode, UInt32 VirtualAddress)
        {
            VirtualAddress -= (VirtualAddress % 2);
            List<UInt32> AddressesToAnalyze = new();
            AddressesToAnalyze.Add(VirtualAddress);
            Section CurrentSection = null;

            while (AddressesToAnalyze.Count > 0)
            {
                UInt32 CurrentAddress = AddressesToAnalyze[0];
                if ((CurrentSection == null) || (CurrentAddress < CurrentSection.VirtualAddress) || (CurrentAddress > (CurrentSection.VirtualAddress + CurrentSection.VirtualSize)))
                {
                    CurrentSection = Sections.Find(s => (CurrentAddress >= s.VirtualAddress) && (CurrentAddress < (s.VirtualAddress + s.VirtualSize)) && s.IsCode);
                    if (CurrentSection == null)
                    {
                        // throw new Exception("Address 0x" + CurrentAddress.ToString("X8") + " is not inside boundaries of code-sections");
                        // Probably jumped to this address because data was disassembled as if it were code. Ignore this.
                        // return;
                        AddressesToAnalyze.RemoveAt(0);
                        continue;
                    }
                }

                if (AnalyzedCode.ContainsKey(CurrentAddress))
                {
                    // return;
                    AddressesToAnalyze.RemoveAt(0);
                    continue;
                }

                Gee.External.Capstone.Arm.ArmInstruction[] NewInstructions = Disassembler.Disassemble(CurrentSection.Buffer.Skip((int)CurrentAddress - (int)CurrentSection.VirtualAddress).ToArray(), CurrentAddress);
                if (NewInstructions.Any())
                {
                    UInt32 StartAddress = (UInt32)NewInstructions.First().Address;
                    UInt32 EndAddress = (UInt32)NewInstructions.Last().Address;

                    ArmInstruction PreviousInstruction = null;
                    foreach (Gee.External.Capstone.Arm.ArmInstruction DisassemblerInstruction in NewInstructions)
                    {
                        // ArmInstruction Instruction = new ArmInstruction(DisassemblerInstruction);
                        ArmInstruction Instruction = new()
                        {
                            Address = (UInt32)DisassemblerInstruction.Address,
                            Bytes = DisassemblerInstruction.Bytes,
                            Mnemonic = DisassemblerInstruction.Mnemonic,
                            Operand = DisassemblerInstruction.Operand.Replace("sb", "r9").Replace("sl", "r10").Replace("fp", "r11").Replace("ip", "r12")
                        };

                        if (AnalyzedCode.ContainsKey((UInt32)Instruction.Address))
                            break;

                        // Merge movw + movt into one command
                        // movw r3, #0x6010 + movt r3, #0x1000 = mov r3, #0x10006010
                        UInt32 HighPart, LowPart;
                        string HighString, LowString;
                        if ((PreviousInstruction?.Mnemonic == "movt") && (Instruction.Mnemonic == "movw") && (PreviousInstruction.Operand.Split(new char[] { ',' })[0] == Instruction.Operand.Split(new char[] { ',' })[0]))
                        {
                            byte[] Combined = new byte[8];
                            System.Buffer.BlockCopy(PreviousInstruction.Bytes, 0, Combined, 0, 4);
                            System.Buffer.BlockCopy(Instruction.Bytes, 0, Combined, 4, 4);
                            PreviousInstruction.Bytes = Combined;
                            PreviousInstruction.Mnemonic = "mov";

                            HighString = PreviousInstruction.Operand[(PreviousInstruction.Operand.IndexOf('#') + 1)..];
                            HighPart = (HighString.Length >= 2) && (HighString.Substring(0, 2) == "0x")
                                ? UInt32.Parse(HighString[2..], System.Globalization.NumberStyles.HexNumber)
                                : UInt32.Parse(HighString);
                            LowString = Instruction.Operand[(Instruction.Operand.IndexOf('#') + 1)..];
                            LowPart = (LowString.Length >= 2) && (LowString.Substring(0, 2) == "0x")
                                ? UInt32.Parse(LowString[2..], System.Globalization.NumberStyles.HexNumber)
                                : UInt32.Parse(LowString);
                            PreviousInstruction.Operand = string.Concat(PreviousInstruction.Operand.AsSpan(0, PreviousInstruction.Operand.IndexOf('#') + 1), "0x", ((HighPart << 16) + LowPart).ToString("X8"));
                            continue;
                        }
                        if ((PreviousInstruction?.Mnemonic == "movw") && (Instruction.Mnemonic == "movt") && (PreviousInstruction.Operand.Split(new char[] { ',' })[0] == Instruction.Operand.Split(new char[] { ',' })[0]))
                        {
                            byte[] Combined = new byte[8];
                            System.Buffer.BlockCopy(PreviousInstruction.Bytes, 0, Combined, 0, 4);
                            System.Buffer.BlockCopy(Instruction.Bytes, 0, Combined, 4, 4);
                            PreviousInstruction.Bytes = Combined;
                            PreviousInstruction.Mnemonic = "mov";

                            HighString = Instruction.Operand[(Instruction.Operand.IndexOf('#') + 1)..];
                            HighPart = (HighString.Length >= 2) && (HighString.Substring(0, 2) == "0x")
                                ? UInt32.Parse(HighString[2..], System.Globalization.NumberStyles.HexNumber)
                                : UInt32.Parse(HighString);
                            LowString = PreviousInstruction.Operand[(PreviousInstruction.Operand.IndexOf('#') + 1)..];
                            LowPart = (LowString.Length >= 2) && (LowString.Substring(0, 2) == "0x")
                                ? UInt32.Parse(LowString[2..], System.Globalization.NumberStyles.HexNumber)
                                : UInt32.Parse(LowString);
                            PreviousInstruction.Operand = string.Concat(PreviousInstruction.Operand.AsSpan(0, PreviousInstruction.Operand.IndexOf('#') + 1), "0x", ((HighPart << 16) + LowPart).ToString("X8"));
                            continue;
                        }

                        AnalyzedCode.Add((UInt32)Instruction.Address, Instruction);

                        int IndexOfIndirectConstant = Instruction.Operand.IndexOf("[pc, #0x");
                        if (IndexOfIndirectConstant >= 0)
                        {
                            int IndexOfEnd = Instruction.Operand.IndexOf("]", IndexOfIndirectConstant);
                            string PCOffsetString = Instruction.Operand.Substring(IndexOfIndirectConstant + 8, IndexOfEnd - IndexOfIndirectConstant - 8);
                            UInt32 PCOffset = UInt32.Parse(PCOffsetString, System.Globalization.NumberStyles.HexNumber);
                            UInt32 PC = (UInt32)Instruction.Address + 4;
                            UInt32 PCforIndirect = PC - (PC % 4);
                            UInt32 VirtualAddressOfIndirectConstant = PCforIndirect + PCOffset;

                            // If the address is outside the range of the section, then this is probably data which is compiled as code.
                            // In this case we will ignore this and not do this part of the analysis.
                            if ((VirtualAddressOfIndirectConstant >= CurrentSection.VirtualAddress) && (VirtualAddressOfIndirectConstant < (CurrentSection.VirtualAddress + CurrentSection.VirtualSize)))
                            {
                                UInt32 RawOffsetOfIndirectConstant = VirtualAddressOfIndirectConstant - CurrentSection.VirtualAddress;
                                UInt32 IndirectConstant = BitConverter.ToUInt32(CurrentSection.Buffer, (int)RawOffsetOfIndirectConstant);
                                Instruction.Operand = Instruction.Operand.Substring(0, IndexOfIndirectConstant) + "#0x" + IndirectConstant.ToString("x8") + Instruction.Operand[(IndexOfEnd + 1)..];
                            }
                        }

                        if (JumpCommands.Contains(Instruction.Mnemonic))
                        {
                            UInt32 NewAddress = UInt32.Parse(Instruction.Operand[(Instruction.Operand.IndexOf("#0x") + 3)..], System.Globalization.NumberStyles.HexNumber);
                            NewAddress -= (NewAddress % 2);
                            if (((NewAddress < StartAddress) || (NewAddress > EndAddress)) && !AddressesToAnalyze.Any(a => a == NewAddress))
                                AddressesToAnalyze.Add(NewAddress);
                        }

                        PreviousInstruction = Instruction;
                    }
                }

                AddressesToAnalyze.RemoveAt(0);
            }
        }

        public static string[] JumpCommands = new string[]
        {
            "b", "b.w", "bl", "bl.w", "beq", "beq.w", "bne", "bne.w", "bhs", "bhs.w", "blo", "blo.w",
            "bmi", "bmi.w", "bpl", "bpl.w", "bvs", "bvs.w", "bvc", "bvc.w", "bhi", "bhi.w", "bls", "bls.w",
            "bge", "bge.w", "blt", "blt.w", "bgt", "bgt.w", "ble", "ble.w", "bal", "bal.w", "cbnz", "cbz"
        };

        public static string[] ConditionalJumpInstructions = new string[]
        {
            "beq", "beq.w", "bne", "bne.w", "bhs", "bhs.w", "blo", "blo.w", "bmi", "bmi.w",
            "bpl", "bpl.w", "bvs", "bvs.w", "bvc", "bvc.w", "bhi", "bhi.w", "bls", "bls.w",
            "bge", "bge.w", "blt", "blt.w", "bgt", "bgt.w", "ble", "ble.w", "bal", "bal.w", "cbnz", "cbz"
        };

        public static string WriteCode(SortedDictionary<UInt32, ArmInstruction> AnalyzedCode)
        {
            StringBuilder Code = new(1000);

            foreach (var Instruction in AnalyzedCode)
            {
                Code.AppendFormat("{0:X}: \t {1} \t {2}\r\n", Instruction.Value.Address, Instruction.Value.Mnemonic, Instruction.Value.Operand);
            }

            return Code.ToString();
        }
    }

    public class ArmInstruction
    {
        public UInt32 Address;
        public byte[] Bytes;
        public string Mnemonic;
        public string Operand;

        public ArmInstruction()
        {
        }

        public ArmInstruction(string Assembly)
        {
            Address = UInt32.Parse(Assembly.Substring(0, 8), System.Globalization.NumberStyles.HexNumber);
            string Hex = Assembly.Substring(12, 24).Trim();
            Bytes = new byte[(Hex.Length + 1) / 3];
            for (int i = 0; i < Bytes.Length; i++)
                Bytes[i] = byte.Parse(Hex.Substring(i * 3, 2), System.Globalization.NumberStyles.HexNumber);
            Mnemonic = Assembly.Substring(39, 16).Trim();
            Operand = Assembly[55..];
        }

        public override string ToString()
        {
            StringBuilder Result = new();

            Result.Append(Address.ToString("X8")); // 0
            Result.Append("    ");
            for (int i = 0; i < Bytes.Length; i++) // 12
            {
                Result.Append(Bytes[i].ToString("X2"));
                Result.Append(' ');
            }
            Result.Append(new String(' ', (8 - Bytes.Length) * 3));
            Result.Append("   ");
            Result.Append(Mnemonic.PadRight(16)); // 39
            Result.Append(Operand); // 55

            return Result.ToString();
        }
    }

    public class AnalyzedFile
    {
        public PeFile File;
        public SortedList<UInt32, ArmInstruction> Code;
    }
}
