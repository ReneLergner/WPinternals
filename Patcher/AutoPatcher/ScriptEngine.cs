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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WPinternals;

namespace Patcher
{
    public static class ScriptEngine
    {
        private static List<CodeLine> ScriptCode;
        private static int Pointer;
        private static Action<string> WriteLog;
        private static string InputFolderPath;
        private static string OutputFolderPath;
        private static string BackupFolderPath;
        private static string RelativePath;
        private static string RelativeOutputPath;
        private static string PathToVisualStudio;
        private static string PatchDefinitionName;
        private static string PatchDefinitionVersion;
        private static AnalyzedFile AnalyzedFile;
        private static byte[] FileBuffer;
        private static string FilePath;
        private static TargetFile FilePatchCollection;
        private static PatchEngine PatchEngine;
        private static UInt32 CurrentVirtualAddressTarget;
        private static bool FindSuccess;
        private static List<FunctionDescriptor> Labels;
        private static List<UInt32> JumpHistory;

        private static readonly char[] Operators = new char[] { '!', '@', '#', '$', '%', '^', '&', '*', '-', '+', '=', '|', '/', '?', '<', '>' };
        private static readonly char[] Separators = new char[] { ',' };
        private static readonly char[] Brackets = new char[] { '[', ']', '(', ')', '{', '}' };

        internal static void ExecuteScript(string PathToVisualStudio, string ScriptFilePath, string InputFolderPath, PatchEngine PatchEngine = null, string OutputFolderPath = null, string BackupFolderPath = null, Action<string> WriteLog = null)
        {
            try
            {
                ScriptEngine.WriteLog = WriteLog ?? ((s) => { });

                ScriptCode = new List<CodeLine>();
                JumpHistory = new List<UInt32>();
                Pointer = 0;
                PatchDefinitionName = null;
                PatchDefinitionVersion = null;
                AnalyzedFile = null;

                ScriptEngine.InputFolderPath = InputFolderPath;
                ScriptEngine.OutputFolderPath = OutputFolderPath?.Length == 0 ? null : OutputFolderPath;
                ScriptEngine.BackupFolderPath = BackupFolderPath?.Length == 0 ? null : BackupFolderPath;
                ScriptEngine.PathToVisualStudio = PathToVisualStudio;
                ScriptEngine.PatchEngine = PatchEngine;

                string[] ScriptCodeLines = File.ReadAllText(ScriptFilePath).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                CodeLine CurrentLine = new();
                bool InCode = false;
                for (int i = 0; i < ScriptCodeLines.Length; i++)
                {
                    string Line = RemoveComment(ScriptCodeLines[i]).Trim(new char[] { ' ', '\t' });
                    if (Line.Length > 0)
                    {
                        string Command = Line.Split(new char[] { ' ', '\t' })[0];
                        if (string.Equals(Command, "PatchCode", StringComparison.CurrentCultureIgnoreCase))
                        {
                            InCode = true;
                            CurrentLine.Code = Line;
                        }
                        else if ((string.Equals(Command, "EndCode", StringComparison.CurrentCultureIgnoreCase)) || (string.Equals(Command, "EndPatch", StringComparison.CurrentCultureIgnoreCase)))
                        {
                            InCode = false;
                            ScriptCode.Add(CurrentLine);
                            CurrentLine = new CodeLine();
                        }
                        else if (InCode)
                        {
                            CurrentLine.PatchCode += Line + Environment.NewLine;
                        }
                        else if (Line.EndsWith(":"))
                        {
                            if (CurrentLine.Label?.Length > 0)
                                throw new ScriptParserException("Two labels at the same location");

                            string Label = Line.TrimEnd(new char[] { ' ', ':' });
                            if (Label.Contains(' '))
                                throw new ScriptParserException("No spaces allowed in label");

                            CurrentLine.Label = Label;
                        }
                        else
                        {
                            CurrentLine.Code = Line;
                            ScriptCode.Add(CurrentLine);
                            CurrentLine = new CodeLine();
                        }
                    }
                }

                do
                {
                    CurrentLine = ScriptCode[Pointer];
                    Pointer++;
                    ExecuteCode(CurrentLine);
                }
                while (Pointer < ScriptCode.Count);

                CloseFile();

                WriteLog("Script finished!");
            }
            catch (ScriptParserException Ex)
            {
                WriteLog("Script parser error: " + Ex.Message);
            }
            catch (Exception Ex)
            {
                WriteLog("Script execution error: " + Ex.Message);
            }
        }

        private static string RemoveComment(string Line)
        {
            int q;
            bool InString;
            int p;
            do
            {
                InString = false;
                p = Line.IndexOf("//");
                if (p >= 0)
                {
                    q = 0;
                    do
                    {
                        q = Line.IndexOf("\"", q);
                        if ((q >= 0) && (q < p))
                        {
                            if ((q == 0) || (Line[q - 1] != '\\'))
                                InString = !InString;
                            q++;
                        }
                    }
                    while ((q >= 0) && (q < p));

                    if (!InString)
                    {
                        return Line.Substring(0, p);
                    }

                    p++;
                }
            }
            while (p >= 0);

            return Line;
        }

        private static void ExecuteCode(CodeLine Line)
        {
            List<Token> Tokens = Tokenizer(Line.Code);
            ParseTokens(Tokens, out string Command, out List<Tuple<string, string, TokenType>> Params);

            // Invoke method
            MethodInfo Method = Array.Find(typeof(ScriptEngine).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static), m => string.Equals(m.Name, Command, StringComparison.CurrentCultureIgnoreCase));

            if (Method == null)
                throw new ScriptParserException("Unrecognized command: " + Command);

            ParameterInfo[] ParamInfos = Method.GetParameters();
            object[] ParamObjects = new object[ParamInfos.Length];
            for (int i = 0; i < ParamInfos.Length; i++)
            {
                if (ParamInfos[i].HasDefaultValue)
                    ParamObjects[i] = ParamInfos[i].DefaultValue;
                else ParamObjects[i] = ParamInfos[i].ParameterType.IsValueType ? Activator.CreateInstance(ParamInfos[i].ParameterType) : null;
            }
            for (int i = 0; i < Params.Count; i++)
            {
                int ParamIndex;
                if (Params[i].Item1 == null)
                {
                    ParamIndex = i < ParamObjects.Length ? i : throw new ScriptParserException("Wrong number of parameters for command: " + Command);
                }
                else
                {
                    ParameterInfo ParamInfo = Array.Find(ParamInfos, p => string.Equals(p.Name, Params[i].Item1, StringComparison.CurrentCultureIgnoreCase));
                    ParamIndex = ParamInfo != null
                        ? ParamInfo.Position
                        : throw new ScriptParserException("Unrecognized parameters " + Params[i].Item1 + " for command: " + Command);
                }

                if ((Params[i].Item3 == TokenType.Text) && (ParamInfos[ParamIndex].ParameterType == typeof(string)))
                {
                    ParamObjects[ParamIndex] = Params[i].Item2;
                }
                else if ((Params[i].Item3 == TokenType.Number) && ((ParamInfos[ParamIndex].ParameterType == typeof(int)) || (ParamInfos[ParamIndex].ParameterType == typeof(uint))))
                {
                    if (Params[i].Item2.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        // Hex
                        UInt32 Value = Convert.ToUInt32(Params[i].Item2[2..], 16);
                        ParamObjects[ParamIndex] = ParamInfos[ParamIndex].ParameterType == typeof(uint) ? Value : (object)(int)Value;
                    }
                    else
                    {
                        if (Params[i].Item2.StartsWith("-"))
                        {
                            Int32 IntValue = Int32.Parse(Params[i].Item2);
                            ParamObjects[ParamIndex] = ParamInfos[ParamIndex].ParameterType == typeof(uint) ? (uint)IntValue : (object)IntValue;
                        }
                        else
                        {
                            UInt32 UIntValue = UInt32.Parse(Params[i].Item2);
                            ParamObjects[ParamIndex] = ParamInfos[ParamIndex].ParameterType == typeof(uint) ? UIntValue : (object)(int)UIntValue;
                        }
                    }
                }
                else
                {
                    throw new ScriptParserException("Wrong parametertype for parameter " + ParamInfos[ParamIndex].Name + " of command " + Command);
                }
            }

            if (Method.Name == "PatchCode")
                ParamObjects[^1] = Line.PatchCode;

            try
            {
                Method.Invoke(null, ParamObjects);
            }
            catch (ScriptExecutionException)
            {
                throw;
            }
            catch (Exception Ex)
            {
                Exception Ex2 = Ex;
                if (Ex2.InnerException != null)
                    Ex2 = Ex2.InnerException;
                if (Ex2 is ScriptExecutionException)
                    throw Ex2;
                throw new ScriptExecutionException(Ex2.Message, Ex2);
            }
        }

        private static List<Token> Tokenizer(string Line)
        {
            // Name: Test_123
            // Text: Test "Dit is een \"test\"..."
            // Number: -12 0xA1 +334
            // Operator: !@#$%^&*-+=|/?<> (combination of multiple characters possible)
            // Separator: ,
            // Bracket: []{}()

            List<Token> Tokens = new();
            int p = 0;

            while (p < Line.Length)
            {
                if (char.IsWhiteSpace(Line[p]))
                {
                    p++;
                    continue;
                }

                if (Line[p] == '\"')
                {
                    int q = Line.IndexOf('\"', p + 1);
                    if (q == -1)
                    {
                        Tokens.Add(new Token() { Type = TokenType.Text, Text = Line[(p + 1)..] });
                        p = Line.Length;
                    }
                    else
                    {
                        Tokens.Add(new Token() { Type = TokenType.Text, Text = Line.Substring(p + 1, q - p - 1) });
                        p = q + 1;
                    }
                    continue;
                }

                if (char.IsLetter(Line[p]) || (Line[p] == '_'))
                {
                    int q = p + 1;
                    while (q < Line.Length)
                    {
                        if (char.IsLetterOrDigit(Line[q]) || (Line[q] == '_') || (Line[q] == '.'))
                            q++;
                        else
                            break;
                    }

                    if ((q == Line.Length) || char.IsWhiteSpace(Line[q]) || Separators.Contains(Line[q]) || Brackets.Contains(Line[q]) || Operators.Contains(Line[q]))
                    {
                        Tokens.Add(new Token() { Type = TokenType.Text, Text = Line[p..q] });
                        p = q;
                        continue;
                    }
                }

                // Int
                if ((((Line[p] == '+') || (Line[p] == '-')) && (p < Line.Length) && char.IsNumber(Line[p + 1])) || char.IsNumber(Line[p]))
                {
                    int q = p + 1;
                    while (q < Line.Length)
                    {
                        if (char.IsDigit(Line[q]))
                            q++;
                        else
                            break;
                    }

                    if ((q == Line.Length) || char.IsWhiteSpace(Line[q]) || Separators.Contains(Line[q]) || Brackets.Contains(Line[q]) || Operators.Contains(Line[q]))
                    {
                        Tokens.Add(new Token() { Type = TokenType.Number, Text = Line[p..q], Value = (UInt32)Int32.Parse(Line[p..q]) });
                        p = q;
                        continue;
                    }
                }

                // Hex
                if (((Line.Length - p) >= 3) && (string.Equals(Line.Substring(p, 2), "0x", StringComparison.CurrentCultureIgnoreCase)))
                {
                    int q = p + 2;
                    while (q < Line.Length)
                    {
                        char CurrentChar = Line[q];
                        if (char.IsDigit(CurrentChar) || ((CurrentChar >= 'a') && (CurrentChar <= 'f')) || ((CurrentChar >= 'A') && (CurrentChar <= 'F')))
                            q++;
                        else
                            break;
                    }

                    if ((q == Line.Length) || char.IsWhiteSpace(Line[q]) || Separators.Contains(Line[q]) || Brackets.Contains(Line[q]) || Operators.Contains(Line[q]))
                    {
                        Tokens.Add(new Token() { Type = TokenType.Number, Text = Line[p..q], Value = UInt32.Parse(Line.Substring(p + 2, q - p - 2), System.Globalization.NumberStyles.HexNumber) });
                        p = q;
                        continue;
                    }
                }

                // Operators
                if (Operators.Contains(Line[p]))
                {
                    int q = p + 1;
                    while (q < Line.Length)
                    {
                        if (Operators.Contains(Line[q]))
                            q++;
                        else
                            break;
                    }

                    Tokens.Add(new Token() { Type = TokenType.Operator, Text = Line[p..q] });
                    p = q;
                    continue;
                }

                // Brackets
                if (Brackets.Contains(Line[p]))
                {
                    Tokens.Add(new Token() { Type = TokenType.Bracket, Text = Line.Substring(p, 1) });
                    p++;
                    continue;
                }

                // Separators
                if (Separators.Contains(Line[p]))
                {
                    Tokens.Add(new Token() { Type = TokenType.Separator, Text = Line.Substring(p, 1) });
                    p++;
                    continue;
                }

                throw new ScriptParserException("Syntax error in line: " + Line);
            }

            return Tokens;
        }

        private static List<Token> ArmThumbTokenizer(string Line)
        {
            List<Token> Tokens;
            try
            {
                Tokens = Tokenizer(Line.ToLower().Replace("#", ""));
            }
            catch
            {
                Tokens = new List<Token>();
            }

            for (int i = 0; i < (Tokens.Count - 1); i++)
            {
                if ((Tokens[i].Text == "r") && (Tokens[i + 1].Text == "?"))
                {
                    Tokens[i].Text = "r?";
                    Tokens.RemoveAt(i + 1);
                }
            }

            return Tokens;
        }

        private static void ParseTokens(List<Token> Tokens, out string Command, out List<Tuple<string, string, TokenType>> Params)
        {
            Command = null;
            Params = new List<Tuple<string, string, TokenType>>();

            if ((Tokens[0].Type == TokenType.Text) && (!IsName(Tokens[0].Text)))
                throw new ScriptParserException("Invalid command");

            Command = Tokens[0].Text;
            Tokens.RemoveAt(0);

            if (Tokens.Count > 0)
            {
                if ((Tokens[0].Text == "(") && (Tokens[^1].Text == ")"))
                {
                    Tokens.RemoveAt(0);
                    Tokens.RemoveAt(Tokens.Count - 1);
                }

                // Parse params
                bool GotNamedParam = false;
                while (Tokens.Count > 0)
                {
                    if ((Tokens.Count >= 3) && (Tokens[0].Type == TokenType.Text) && IsName(Tokens[0].Text) && (Tokens[1].Text == "=") && ((Tokens[2].Type == TokenType.Text) || (Tokens[2].Type == TokenType.Number)))
                    {
                        Params.Add(new Tuple<string, string, TokenType>(Tokens[0].Text, Tokens[2].Text, Tokens[2].Type));
                        GotNamedParam = true;
                        Tokens.RemoveRange(0, 3);
                    }
                    else if ((Tokens[0].Type == TokenType.Text) || (Tokens[0].Type == TokenType.Number))
                    {
                        if (GotNamedParam)
                            throw new ScriptParserException("Named parameter cannot preceed an unnamed parameter");

                        Params.Add(new Tuple<string, string, TokenType>(null, Tokens[0].Text, Tokens[0].Type));
                        Tokens.RemoveAt(0);
                    }
                    else
                    {
                        throw new ScriptParserException("Syntax error");
                    }

                    if ((Tokens.Count > 0) && (Tokens[0].Text == ","))
                        Tokens.RemoveAt(0);
                }
            }
        }

        private static bool IsName(string Token)
        {
            if (Token.Length == 0)
                return false;
            for (int i = 0; i < Token.Length; i++)
            {
                if (!(char.IsLetter(Token[i]) || (Token[i] == '_') || (Token[i] == '.') || ((i > 0) && char.IsNumber(Token[i]))))
                    return false;
            }
            return true;
        }

        private static void PatchDefinition(string Name, string VersionFrom, string Version, string RelativePath, string RelativeOutputPath)
        {
            CloseFile();

            PatchDefinitionName = Name;
            ScriptEngine.RelativePath = RelativePath;
            ScriptEngine.RelativeOutputPath = RelativeOutputPath;
            if (Version != null)
            {
                PatchDefinitionVersion = Version;
            }
            else if (VersionFrom != null)
            {
                string FullPath = System.IO.Path.Combine(InputFolderPath, VersionFrom);
                PeFile File = new(FullPath);
                Version ProductVersion = File.GetProductVersion();
                PatchDefinitionVersion = ProductVersion.Major.ToString() + "." + ProductVersion.Minor.ToString() + "." + ProductVersion.Build.ToString() + "." + ProductVersion.Revision.ToString();
            }
            else
            {
                throw new ScriptExecutionException("Patch definition version is mandatory");
            }
            WriteLog("PatchDefinition: " + PatchDefinitionName);
            WriteLog("Version: " + PatchDefinitionVersion);
        }

        private static void PatchFile(string Path)
        {
            if (PatchDefinitionName == null)
                throw new ScriptExecutionException("PatchDefinition not defined");

            CloseFile();
            string FullPath = System.IO.Path.Combine(InputFolderPath, RelativePath ?? "", Path);

            string AsmFilePath = BackupFolderPath == null
                ? System.IO.Path.Combine(InputFolderPath, RelativePath ?? "", Path)
                : System.IO.Path.Combine(BackupFolderPath, RelativePath ?? "", Path).Replace("%VERSION%", PatchDefinitionVersion, StringComparison.OrdinalIgnoreCase);
            AsmFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AsmFilePath), System.IO.Path.GetFileNameWithoutExtension(AsmFilePath) + ".asm");

            bool AsmFileExists = File.Exists(AsmFilePath);
            if (AsmFileExists)
                WriteLog("Loading file: " + AsmFilePath);
            else
                WriteLog("Analyzing file: " + FullPath);

            AnalyzedFile = ArmDisassembler.Analyze(FullPath, AsmFilePath);
            FilePath = Path;
            FileBuffer = AnalyzedFile.File.Buffer;

            if (!AsmFileExists)
            {
                WriteLog("Writing file: " + AsmFilePath);
                WriteLog("Analysis done");
            }

            if (BackupFolderPath != null)
            {
                FullPath = System.IO.Path.Combine(BackupFolderPath, RelativePath ?? "", Path).Replace("%VERSION%", PatchDefinitionVersion, StringComparison.OrdinalIgnoreCase);

                WriteLog("Create backup to: " + FullPath);
                File.WriteAllBytes(FullPath, FileBuffer);
            }

            CurrentVirtualAddressTarget = (UInt32)AnalyzedFile.File.ImageBase;

            PatchDefinition PatchDefinition = PatchEngine.PatchDefinitions.Find(d => string.Equals(d.Name, PatchDefinitionName, StringComparison.CurrentCultureIgnoreCase));
            if (PatchDefinition == null)
            {
                PatchDefinition = new PatchDefinition
                {
                    Name = PatchDefinitionName
                };
                PatchEngine.PatchDefinitions.Add(PatchDefinition);
            }
            TargetVersion TargetVersion = PatchDefinition.TargetVersions.Find(v => string.Equals(v.Description, PatchDefinitionVersion, StringComparison.CurrentCultureIgnoreCase));
            if (TargetVersion == null)
            {
                TargetVersion = new TargetVersion
                {
                    Description = PatchDefinitionVersion
                };
                PatchDefinition.TargetVersions.Add(TargetVersion);
            }
            TargetFile TargetFile = TargetVersion.TargetFiles.Find(f => (f.Path != null) && (string.Equals(f.Path.TrimStart(new char[] { '\\' }), Path.TrimStart(new char[] { '\\' }), StringComparison.CurrentCultureIgnoreCase)));
            if (TargetFile != null)
                TargetVersion.TargetFiles.Remove(TargetFile); // Remove any old patches for this file
            TargetFile = new TargetFile();
            TargetVersion.TargetFiles.Add(TargetFile);
            TargetFile.Path = Path.TrimStart(new char[] { '\\' });
            SHA1Managed SHA = new();
            TargetFile.HashOriginal = SHA.ComputeHash(AnalyzedFile.File.Buffer);
            FilePatchCollection = TargetFile;

            Labels = new List<FunctionDescriptor>();
        }

        private static void CloseFile()
        {
            if (AnalyzedFile != null)
            {
                // Update hash in patch definition
                SHA1Managed SHA = new();
                FilePatchCollection.HashPatched = SHA.ComputeHash(FileBuffer);
                WriteLog("New hash for patched file: " + Converter.ConvertHexToString(FilePatchCollection.HashPatched, ""));

                // Write patched file
                if (OutputFolderPath != null)
                {
                    string FullPath = System.IO.Path.Combine(OutputFolderPath, RelativeOutputPath ?? "", RelativePath ?? "", FilePath).Replace("%VERSION%", PatchDefinitionVersion, StringComparison.OrdinalIgnoreCase);

                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FullPath));

                    WriteLog("Writing patched file: " + FullPath);

                    File.WriteAllBytes(FullPath, FileBuffer);
                }

                AnalyzedFile = null;
            }
        }

        private static void PatchAtRawOffset(byte[] Bytes, UInt32 RawOffset)
        {
            // Read original bytes
            byte[] Original = new byte[Bytes.Length];
            System.Buffer.BlockCopy(FileBuffer, (int)RawOffset, Original, 0, Bytes.Length);

            // Patch bytes in buffer
            System.Buffer.BlockCopy(Bytes, 0, FileBuffer, (int)RawOffset, Bytes.Length);

            // Add patch to definitions (original and patched bytes)
            Patch CurrentPatch = FilePatchCollection.Patches.Find(p => p.Address == RawOffset);
            if (CurrentPatch == null)
            {
                CurrentPatch = new Patch
                {
                    Address = RawOffset,
                    OriginalBytes = Original
                };
                FilePatchCollection.Patches.Add(CurrentPatch);
            }
            CurrentPatch.PatchedBytes = Bytes;

            WriteLog("Patched file at raw offset: 0x" + RawOffset.ToString("X8"));
            WriteLog("    Original bytes: " + Converter.ConvertHexToString(Original, " "));
            WriteLog("    Patched bytes:  " + Converter.ConvertHexToString(Bytes, " "));
        }

        private static void PatchAtVirtualAddress(byte[] Bytes, UInt32 VirtualAddress)
        {
            PatchAtRawOffset(Bytes, AnalyzedFile.File.ConvertVirtualAddressToRawOffset(CurrentVirtualAddressTarget));
        }

        private static void FindFirstAscii(string SearchString)
        {
            CurrentVirtualAddressTarget = (UInt32)AnalyzedFile.File.ImageBase;
            WriteLog("Set search start point to virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            FindNextAscii(SearchString);
        }

        private static void FindNextAscii(string SearchString)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for ascii string: " + SearchString);
            UInt32? FindIndex = ByteOperations.FindAscii(FileBuffer, AnalyzedFile.File.ConvertVirtualAddressToRawOffset(CurrentVirtualAddressTarget), SearchString);
            FindSuccess = FindIndex != null;
            if (FindIndex != null)
            {
                CurrentVirtualAddressTarget = AnalyzedFile.File.ConvertRawOffsetToVirtualAddress((UInt32)FindIndex);
                WriteLog("Ascii string found at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            }
            else
            {
                WriteLog("String not found");
            }
        }

        private static void FindAscii(string SearchString)
        {
            FindNextAscii(SearchString);
        }

        private static void FindFirstUnicode(string SearchString)
        {
            CurrentVirtualAddressTarget = (UInt32)AnalyzedFile.File.ImageBase;
            WriteLog("Set search start point to virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            FindNextUnicode(SearchString);
        }

        private static void FindNextUnicode(string SearchString)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for unicode string: " + SearchString);
            UInt32? FindIndex = ByteOperations.FindUnicode(FileBuffer, AnalyzedFile.File.ConvertVirtualAddressToRawOffset(CurrentVirtualAddressTarget), SearchString);
            FindSuccess = FindIndex != null;
            if (FindIndex != null)
            {
                CurrentVirtualAddressTarget = AnalyzedFile.File.ConvertRawOffsetToVirtualAddress((UInt32)FindIndex);
                WriteLog("Unicode string found at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            }
            else
            {
                WriteLog("String not found");
            }
        }

        private static void FindUnicode(string SearchString)
        {
            FindNextUnicode(SearchString);
        }

        private static void FindFirstBytes(string SearchString)
        {
            CurrentVirtualAddressTarget = (UInt32)AnalyzedFile.File.ImageBase;
            WriteLog("Set search start point to virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            FindNextBytes(SearchString);
        }

        private static void FindNextBytes(string SearchString)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for bytes: " + SearchString);
            byte[] Bytes = GetBytesFromString(SearchString);
            UInt32? FindIndex = ByteOperations.FindPattern(FileBuffer, AnalyzedFile.File.ConvertVirtualAddressToRawOffset(CurrentVirtualAddressTarget), null, Bytes, null, null);
            FindSuccess = FindIndex != null;
            if (FindIndex != null)
            {
                CurrentVirtualAddressTarget = AnalyzedFile.File.ConvertRawOffsetToVirtualAddress((UInt32)FindIndex);
                WriteLog("Binary search pattern found at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            }
            else
            {
                WriteLog("Binary search pattern not found");
            }
        }

        private static void FindBytes(string SearchString)
        {
            FindNextBytes(SearchString);
        }

        private static byte[] GetBytesFromString(string Bytes)
        {
            Bytes = Bytes.ToUpper().Replace("0X", "");
            for (int i = Bytes.Length -1 ; i >= 0; i--)
            {
                char CurrentChar = Bytes[i];
                if (((CurrentChar < '0') || (CurrentChar > '9')) && ((CurrentChar < 'A') || (CurrentChar > 'F')))
                    Bytes = Bytes.Substring(0, i) + Bytes[(i + 1)..];
            }
            if ((Bytes.Length % 2) > 0)
                throw new ScriptExecutionException("Not a valid binary search string: " + Bytes);
            byte[] Result = new byte[Bytes.Length / 2];
            for (int i = 0; i < (Bytes.Length / 2); i++)
                Result[i] = byte.Parse(Bytes.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            return Result;
        }

        private static void JumpToReference(int ReferenceIndex, string CodePattern, string R0, string R1, string R2, string R3, string Result)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for reference " + (ReferenceIndex == 0 ? "" : "with index " + ReferenceIndex.ToString() + " ") + "to virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            int FoundIndex = -1;
            for (int i = 0; i < AnalyzedFile.Code.Count; i++)
            {
                if ((AnalyzedFile.Code.Values[i].Operand.Contains("#0x" + CurrentVirtualAddressTarget.ToString("X8"), StringComparison.OrdinalIgnoreCase)) ||
                    (AnalyzedFile.Code.Values[i].Operand.Contains("#0x" + CurrentVirtualAddressTarget.ToString("X"), StringComparison.OrdinalIgnoreCase)))
                {
                    // Reference found. Now check criteria.
                    bool Match = false;

                    if (CodePattern != null)
                    {
                        string[] PatternLines = CodePattern.Split(new char[] { ';' });
                        if (i < (PatternLines.Length - 1))
                            continue;
                        for (int j = 0; j < PatternLines.Length; j++)
                        {
                            int k = i - PatternLines.Length + j + 1;
                            Match = MatchPattern(AnalyzedFile.Code.Values[k], PatternLines[j]);
                            if (!Match)
                                break;
                        }
                        if (!Match)
                            continue;
                    }

                    if (R0 != null)
                    {
                        const string Register = "r0";
                        string Pattern = R0;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R1 != null)
                    {
                        const string Register = "r1";
                        string Pattern = R1;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R2 != null)
                    {
                        const string Register = "r2";
                        string Pattern = R2;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R3 != null)
                    {
                        const string Register = "r3";
                        string Pattern = R3;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (Result != null)
                    {
                        const string Register = "r0";
                        string Pattern = Result;
                        Match = false;
                        int j = i + 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && Tokens.Any(t => t.Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j++;
                        }
                        while (j < AnalyzedFile.Code.Count);
                        if (!Match)
                            continue;
                    }

                    FoundIndex++;
                    if (FoundIndex == ReferenceIndex)
                    {
                        JumpHistory.Add(CurrentVirtualAddressTarget);
                        CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                        WriteLog("Found reference in code at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                        FindSuccess = true;
                        return;
                    }
                }
            }

            // throw new ScriptExecutionException("Reference not found");
            FindSuccess = false;
        }

        private static bool MatchPattern(ArmInstruction Instruction, string Pattern)
        {
            List<Token> PatternTokens = ArmThumbTokenizer(Pattern);
            return MatchPattern(Instruction, PatternTokens);
        }

        private static bool MatchPattern(ArmInstruction Instruction, List<Token> PatternTokens)
        {
            List<Token> InstructionTokens = null;
            try
            {
                InstructionTokens = ArmThumbTokenizer(Instruction.Mnemonic + " " + Instruction.Operand);
            }
            catch { }
            if (InstructionTokens == null)
                return false;

            // Sanity check
            if ((InstructionTokens.Count == 0) || (PatternTokens.Count == 0))
                return false;

            // Complete wildcard pattern
            if (PatternTokens[0].Text == "?")
                return true;

            // instruction must match
            if ((InstructionTokens[0].Text != PatternTokens[0].Text) && (InstructionTokens[0].Text != (PatternTokens[0].Text + ".w")))
                return false;

            if ((PatternTokens.Count == 1) || ((PatternTokens.Count == 2) && (PatternTokens[1].Text == "?")))
                return true;

            for (int i = 1; i < InstructionTokens.Count; i++)
            {
                if (PatternTokens[i].Text == "?")
                    continue;

                if ((PatternTokens[i].Text == "r?") && InstructionTokens[i].Text.StartsWith("r"))
                    continue;

                if (PatternTokens[i].Text == InstructionTokens[i].Text)
                    continue;

                if ((PatternTokens[i].Type == TokenType.Number) && (InstructionTokens[i].Type == TokenType.Number) && (PatternTokens[i].Value == InstructionTokens[i].Value))
                    continue;

                return false;
            }

            return true;
        }

        private static void FindFirstInstructionPattern(string Pattern, int InstructionIndex)
        {
            CurrentVirtualAddressTarget = (UInt32)AnalyzedFile.File.ImageBase;
            WriteLog("Set search start point to virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            FindNextInstructionPattern(Pattern, InstructionIndex);
        }

        private static void FindNextInstructionPattern(string Pattern, int InstructionIndex)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for instruction-pattern");

            List<List<Token>> PatternTokens = new();
            string[] PatternInstructions = Pattern.Split(new char[] { ';' });
            for (int i = 0; i < PatternInstructions.Length; i++)
                PatternTokens.Add(ArmThumbTokenizer(PatternInstructions[i]));

            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) + 1; i < AnalyzedFile.Code.Count; i++)
            {
                bool IsMatch = true;

                for (int j = 0; j < PatternTokens.Count; j++)
                {
                    if (!MatchPattern(AnalyzedFile.Code.Values[i + j], PatternTokens[j]))
                    {
                        IsMatch = false;
                        break;
                    }
                }

                if (IsMatch)
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i + InstructionIndex].Address;
                    WriteLog("Found instruction-pattern at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Instruction-pattern not found");
        }

        private static void FindPreviousInstructionPattern(string Pattern, int InstructionIndex)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for instruction-pattern");

            List<List<Token>> PatternTokens = new();
            string[] PatternInstructions = Pattern.Split(new char[] { ';' });
            for (int i = 0; i < PatternInstructions.Length; i++)
                PatternTokens.Add(ArmThumbTokenizer(PatternInstructions[i]));

            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) - 1; i >= 0; i--)
            {
                bool IsMatch = true;

                for (int j = 0; j < PatternTokens.Count; j++)
                {
                    if (!MatchPattern(AnalyzedFile.Code.Values[i + j], PatternTokens[j]))
                    {
                        IsMatch = false;
                        break;
                    }
                }

                if (IsMatch)
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i + InstructionIndex].Address;
                    WriteLog("Found instruction-pattern at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Instruction-pattern not found");
        }

        private static void FindInstructionPattern(string Pattern, int InstructionIndex)
        {
            FindNextInstructionPattern(Pattern, InstructionIndex);
        }

        private static void FindFirstValue(UInt32 Value)
        {
            CurrentVirtualAddressTarget = (UInt32)AnalyzedFile.File.ImageBase;
            WriteLog("Set search start point to virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            FindNextValue(Value);
        }

        private static void FindPreviousValue(UInt32 Value)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            bool GotValue;
            UInt32 FoundValue = 0;
            WriteLog("Looking for previous value: 0x" + Value.ToString("X8"));
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) - 1; i >= 0; i--)
            {
                GotValue = false;

                int Index = AnalyzedFile.Code.Values[i].Operand.IndexOf("#0x");
                if (Index >= 0)
                    GotValue = UInt32.TryParse(AnalyzedFile.Code.Values[i].Operand[(Index + 3)..].TrimEnd(new char[] { ']' }), System.Globalization.NumberStyles.HexNumber, null, out FoundValue);
                if (!GotValue)
                {
                    Index = AnalyzedFile.Code.Values[i].Operand.IndexOf("#");
                    if (Index >= 0)
                        GotValue = UInt32.TryParse(AnalyzedFile.Code.Values[i].Operand[(Index + 1)..].TrimEnd(new char[] { ']' }), out FoundValue);
                }
                if (GotValue && (FoundValue == Value))
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found value in code at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Value not found");
        }

        private static void FindNextValue(UInt32 Value)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            bool GotValue;
            UInt32 FoundValue = 0;
            WriteLog("Looking for value: 0x" + Value.ToString("X8"));
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) + 1; i < AnalyzedFile.Code.Count; i++)
            {
                GotValue = false;

                int Index = AnalyzedFile.Code.Values[i].Operand.IndexOf("#0x");
                if (Index >= 0)
                    GotValue = UInt32.TryParse(AnalyzedFile.Code.Values[i].Operand[(Index + 3)..].TrimEnd(new char[] { ']' }), System.Globalization.NumberStyles.HexNumber, null, out FoundValue);
                if (!GotValue)
                {
                    Index = AnalyzedFile.Code.Values[i].Operand.IndexOf("#");
                    if (Index >= 0)
                        GotValue = UInt32.TryParse(AnalyzedFile.Code.Values[i].Operand[(Index + 1)..].TrimEnd(new char[] { ']' }), out FoundValue);
                }
                if (GotValue && (FoundValue == Value))
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found value in code at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Value not found");
        }

        private static void FindValue(UInt32 Value)
        {
            FindNextValue(Value);
        }

        private static void FindPreviousConditionalJump()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for previous conditional jump");
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) - 1; i >= 0; i--)
            {
                if (ArmDisassembler.ConditionalJumpInstructions.Contains(AnalyzedFile.Code.Values[i].Mnemonic))
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found conditional jump at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    WriteLog("    " + AnalyzedFile.Code.Values[i].Mnemonic + " " + AnalyzedFile.Code.Values[i].Operand);
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Conditional jump not found");
        }

        private static void FindNextConditionalJump()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for next conditional jump");
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) + 1; i < AnalyzedFile.Code.Count; i++)
            {
                if (ArmDisassembler.ConditionalJumpInstructions.Contains(AnalyzedFile.Code.Values[i].Mnemonic))
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found conditional jump at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    WriteLog("    " + AnalyzedFile.Code.Values[i].Mnemonic + " " + AnalyzedFile.Code.Values[i].Operand);
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Conditional jump not found");
        }

        private static void MakeJumpUnconditional(string Instruction)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            ArmInstruction CurrentInstruction = AnalyzedFile.Code[CurrentVirtualAddressTarget];
            if (ArmDisassembler.ConditionalJumpInstructions.Contains(CurrentInstruction.Mnemonic))
            {
                if ((Instruction == null) || (string.Equals(Instruction, CurrentInstruction.Mnemonic, StringComparison.CurrentCultureIgnoreCase)) || (string.Equals(Instruction + ".w", CurrentInstruction.Mnemonic, StringComparison.CurrentCultureIgnoreCase)))
                {
                    WriteLog("Making instruction unconditional at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    string AddressString = CurrentInstruction.Operand[(CurrentInstruction.Operand.IndexOf("#0x") + 3)..];
                    UInt32 Address = Convert.ToUInt32(AddressString, 16);
                    string NewInstruction = CurrentInstruction.Mnemonic.EndsWith(".w") ? "b.w" : "b";
                    WriteLog("    Original: " + CurrentInstruction.Mnemonic + " " + CurrentInstruction.Operand);
                    WriteLog("    Patch:    " + NewInstruction + " #0x" + AddressString);
                    string NewCode = NewInstruction + " 0x" + AddressString;
                    byte[] CompiledCode = Compile(NewCode);
                    PatchAtVirtualAddress(CompiledCode, CurrentVirtualAddressTarget);
                    CurrentVirtualAddressTarget += (UInt32)CompiledCode.Length;
                }
                else
                {
                    WriteLog("Looking for conditional jump: " + Instruction);
                    WriteLog("Instead this conditional jump was found: " + CurrentInstruction.Mnemonic + " " + CurrentInstruction.Operand);
                    WriteLog("Instead of making the jump unconditional, the jump will be cleared");
                    string NewInstruction = (CurrentInstruction.Bytes.Length == 2) ? "nop" : "nop.w";
                    WriteLog("Patch: " + NewInstruction);
                    byte[] CompiledCode = Compile(NewInstruction);
                    PatchAtVirtualAddress(CompiledCode, CurrentVirtualAddressTarget);
                    CurrentVirtualAddressTarget += (UInt32)CompiledCode.Length;
                }
            }
            else
            {
                throw new ScriptExecutionException("Instruction cannot be made unconditional because it isn't a jump-instruction");
            }
        }

        private static void PatchChecksum()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Calculating new checksum for file");
            UInt32 ChecksumOffset = AnalyzedFile.File.GetChecksumOffset();
            UInt32 Checksum = AnalyzedFile.File.CalculateChecksum();
            byte[] ChecksumBytes = new byte[4];
            ByteOperations.WriteUInt32(ChecksumBytes, 0, Checksum);
            PatchAtRawOffset(ChecksumBytes, ChecksumOffset);
        }

        private static void PatchCode(string CodeType, string AsmCode)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            Patcher.CodeType PatcherCodeType = Patcher.CodeType.Thumb2;
            if (CodeType != null)
            {
                PatcherCodeType = CodeType.ToLower() switch
                {
                    "arm" => Patcher.CodeType.ARM,
                    "thumb" => Patcher.CodeType.Thumb,
                    "thumb2" => Patcher.CodeType.Thumb2,
                    _ => throw new ScriptExecutionException("Invalid Assembly Type"),
                };
            }

            WriteLog("Compiling new code at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            byte[] CompiledCode = Compile(PatcherCodeType, AsmCode);
            PatchAtVirtualAddress(CompiledCode, CurrentVirtualAddressTarget);

            CurrentVirtualAddressTarget += (UInt32)CompiledCode.Length;
            if (!AnalyzedFile.Code.ContainsKey(CurrentVirtualAddressTarget))
                CurrentVirtualAddressTarget += 2;
            if (!AnalyzedFile.Code.ContainsKey(CurrentVirtualAddressTarget))
            {
                for (int i = 0; i < AnalyzedFile.Code.Count; i++)
                {
                    if (AnalyzedFile.Code.Values[i].Address > CurrentVirtualAddressTarget)
                    {
                        CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                        break;
                    }
                }
            }
        }

        private static void JumpToImport(string FunctionName)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            FunctionDescriptor Import = AnalyzedFile.File.Imports.Find(i => string.Equals(i.Name, FunctionName, StringComparison.CurrentCultureIgnoreCase));
            if (Import == null)
            {
                throw new ScriptExecutionException("Import not found: " + FunctionName);
            }
            else
            {
                WriteLog("Import " + FunctionName + " found at: 0x" + Import.VirtualAddress.ToString("X8"));
                JumpHistory.Add(CurrentVirtualAddressTarget);
                CurrentVirtualAddressTarget = Import.VirtualAddress;
            }
        }

        private static void JumpToExport(string FunctionName)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            FunctionDescriptor Export = AnalyzedFile.File.Exports.Find(i => string.Equals(i.Name, FunctionName, StringComparison.CurrentCultureIgnoreCase));
            if (Export == null)
            {
                throw new ScriptExecutionException("Export not found: " + FunctionName);
            }
            else
            {
                WriteLog("Export " + FunctionName + " found at: 0x" + Export.VirtualAddress.ToString("X8"));
                JumpHistory.Add(CurrentVirtualAddressTarget);
                CurrentVirtualAddressTarget = Export.VirtualAddress;
            }
        }

        private static void FindPreviousInstruction(string Instruction)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for previous instruction: " + Instruction);
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) - 1; i >= 0; i--)
            {
                if ((string.Equals(Instruction, AnalyzedFile.Code.Values[i].Mnemonic, StringComparison.CurrentCultureIgnoreCase)) || (string.Equals(Instruction + ".W", AnalyzedFile.Code.Values[i].Mnemonic, StringComparison.CurrentCultureIgnoreCase)))
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found instruction at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Instruction not found");
        }

        private static void FindNextInstruction(string Instruction)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for instruction: " + Instruction);
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) + 1; i < AnalyzedFile.Code.Count; i++)
            {
                if ((string.Equals(Instruction, AnalyzedFile.Code.Values[i].Mnemonic, StringComparison.CurrentCultureIgnoreCase)) || (string.Equals(Instruction + ".W", AnalyzedFile.Code.Values[i].Mnemonic, StringComparison.CurrentCultureIgnoreCase)))
                {
                    FindSuccess = true;
                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found instruction at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    return;
                }
            }

            FindSuccess = false;
            WriteLog("Instruction not found");
        }

        private static void FindInstruction(string Instruction)
        {
            FindNextInstruction(Instruction);
        }

        private static void CreateLabel(string Label)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            if (Labels.Any(l => string.Equals(l.Name, Label, StringComparison.CurrentCultureIgnoreCase)))
                throw new ScriptExecutionException("Label already exists: " + Label);

            Labels.Add(new FunctionDescriptor() { Name = Label, VirtualAddress = CurrentVirtualAddressTarget });
            WriteLog("Label created: " + Label + " = 0x" + CurrentVirtualAddressTarget.ToString("X8"));
        }

        private static string GetLabelsForAsm()
        {
            string Result = "";
            foreach (FunctionDescriptor Label in Labels)
            {
                Result += Label.Name + " EQU 0x" + Label.VirtualAddress.ToString("X8") + Environment.NewLine;
            }
            return Result;
        }

        private static void CompareInstructionGo(string Instruction, string Label)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            string CurrentInstruction = AnalyzedFile.Code[CurrentVirtualAddressTarget].Mnemonic;
            WriteLog("Current instruction: " + CurrentInstruction);
            if ((string.Equals(CurrentInstruction, Instruction, StringComparison.CurrentCultureIgnoreCase)) || (string.Equals(CurrentInstruction, Instruction + ".w", StringComparison.CurrentCultureIgnoreCase)))
                Go(Label);
        }

        private static void ClearInstruction()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            ArmInstruction CurrentInstruction = AnalyzedFile.Code[CurrentVirtualAddressTarget];
            WriteLog("clearing instruction at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
            string Instruction = (CurrentInstruction.Bytes.Length == 2) ? "nop" : "nop.w";
            WriteLog("    Original: " + CurrentInstruction.Mnemonic + " " + CurrentInstruction.Operand);
            WriteLog("    Patch:    " + Instruction);
            byte[] CompiledCode = Compile(Instruction);
            PatchAtVirtualAddress(CompiledCode, CurrentVirtualAddressTarget);
            CurrentVirtualAddressTarget += (UInt32)CompiledCode.Length;
        }

        private static byte[] Compile(string AsmCode)
        {
            return Compile(CodeType.Thumb2, AsmCode);
        }

        private static byte[] Compile(CodeType Type, string AsmCode)
        {
            byte[] Result = ArmCompiler.Compile(PathToVisualStudio, CurrentVirtualAddressTarget, Type, GetLabelsForAsm() + AsmCode);
            if (Result == null)
                throw new ScriptExecutionException("ARM compiler output: " + ArmCompiler.Output);
            else
                return Result;
        }

        private static void Go(string Label)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            CodeLine NewLine = ScriptCode.Find(l => string.Equals(l.Label, Label, StringComparison.CurrentCultureIgnoreCase));
            if (NewLine == null)
            {
                throw new ScriptExecutionException("Label " + Label + " not found");
            }
            else
            {
                Pointer = ScriptCode.IndexOf(NewLine);
                WriteLog("Go to label: " + Label);
            }
        }

        private static void PatchUnicode(string Text)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Patching zero-terminated unicode string: " + Text);
            if (!Text.EndsWith("\0"))
                Text += "\0";
            byte[] Bytes = Encoding.Unicode.GetBytes(Text);
            PatchAtVirtualAddress(Bytes, CurrentVirtualAddressTarget);
            CurrentVirtualAddressTarget += (UInt32)Bytes.Length;
        }

        private static void PatchAscii(string Text)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Patching zero-terminated ascii string: " + Text);
            if (!Text.EndsWith("\0"))
                Text += "\0";
            byte[] Bytes = Encoding.ASCII.GetBytes(Text);
            PatchAtVirtualAddress(Bytes, CurrentVirtualAddressTarget);
            CurrentVirtualAddressTarget += (UInt32)Bytes.Length;
        }

        private static void JumpToTarget()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            ArmInstruction CurrentInstruction = AnalyzedFile.Code[CurrentVirtualAddressTarget];
            string AddressString = CurrentInstruction.Operand[(CurrentInstruction.Operand.IndexOf("#0x") + 3)..];
            if (UInt32.TryParse(AddressString, System.Globalization.NumberStyles.HexNumber, null, out uint Address))
            {
                if (AnalyzedFile.File.GetSectionForVirtualAddress(Address) == null)
                    throw new ScriptExecutionException("Target at virtual address 0x" + Address.ToString("X8") + " is invalid");
                WriteLog("Jumping to target: 0x" + Address.ToString("X8"));
                JumpHistory.Add(CurrentVirtualAddressTarget);
                CurrentVirtualAddressTarget = Address;
            }
            else
            {
                throw new ScriptExecutionException("Could not jump to target: " + CurrentInstruction.Operand);
            }
        }

        private static void JumpToLabel(string Label)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            FunctionDescriptor FoundLabel = Labels.Find(l => string.Equals(l.Name, Label, StringComparison.CurrentCultureIgnoreCase));
            if (FoundLabel == null)
                throw new ScriptExecutionException("Label not found: " + Label);

            WriteLog("Jumping to label: " + Label);
            WriteLog("New virtual address: 0x" + FoundLabel.VirtualAddress.ToString("X8"));
            JumpHistory.Add(CurrentVirtualAddressTarget);
            CurrentVirtualAddressTarget = FoundLabel.VirtualAddress;
        }

        private static void IfFoundGo(string Label)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            if (FindSuccess)
            {
                FunctionDescriptor FoundLabel = Labels.Find(l => string.Equals(l.Name, Label, StringComparison.CurrentCultureIgnoreCase));
                if (FoundLabel == null)
                    throw new ScriptExecutionException("Label not found: " + Label);

                WriteLog("Condition was found, jumping to label: " + Label);
                WriteLog("New virtual address: 0x" + FoundLabel.VirtualAddress.ToString("X8"));
                CurrentVirtualAddressTarget = FoundLabel.VirtualAddress;
            }
        }

        private static void IfNotFoundGo(string Label)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            if (!FindSuccess)
            {
                FunctionDescriptor FoundLabel = Labels.Find(l => string.Equals(l.Name, Label, StringComparison.CurrentCultureIgnoreCase));
                if (FoundLabel == null)
                    throw new ScriptExecutionException("Label not found: " + Label);

                WriteLog("Condition was not found, jumping to label: " + Label);
                WriteLog("New virtual address: 0x" + FoundLabel.VirtualAddress.ToString("X8"));
                CurrentVirtualAddressTarget = FoundLabel.VirtualAddress;
            }
        }

        private static void IfFoundThrowError(string Message)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            if (FindSuccess)
            {
                if (Message == null)
                    throw new ScriptExecutionException("Script execution error");
                else
                    throw new ScriptExecutionException(Message);
            }
        }

        private static void IfNotFoundThrowError(string Message)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            if (!FindSuccess)
            {
                if (Message == null)
                    throw new ScriptExecutionException("Script execution error");
                else
                    throw new ScriptExecutionException(Message);
            }
        }

        private static void JumpBack()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            if (JumpHistory.Count == 0)
                throw new ScriptExecutionException("No jump history, can't jump back");

            CurrentVirtualAddressTarget = JumpHistory.Last();
            JumpHistory.RemoveAt(JumpHistory.Count - 1);
            WriteLog("Jumping back to: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
        }

        private static void FindStore()
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            string Operand = AnalyzedFile.Code[CurrentVirtualAddressTarget].Operand;
            if (Operand.Contains(','))
            {
                string Register = Operand.Substring(0, Operand.IndexOf(',')).Trim();
                if (Register.StartsWith("r"))
                {
                    WriteLog("Looking for instruction where " + Register + " is being stored");

                    for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) + 1; i < AnalyzedFile.Code.Count; i++)
                    {
                        if ((string.Equals("str", AnalyzedFile.Code.Values[i].Mnemonic, StringComparison.CurrentCultureIgnoreCase)) || (string.Equals("str.w", AnalyzedFile.Code.Values[i].Mnemonic, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            Operand = AnalyzedFile.Code.Values[i].Operand;
                            if (Operand.Contains(','))
                            {
                                if (Register == Operand.Substring(0, Operand.IndexOf(',')).Trim())
                                {
                                    FindSuccess = true;
                                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                                    WriteLog("Found instruction at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                                    return;
                                }
                            }
                        }
                    }

                    FindSuccess = false;
                    WriteLog("Instruction not found");
                }
                else
                {
                    throw new ScriptExecutionException("Could not locate register to search for");
                }
            }
            else
            {
                throw new ScriptExecutionException("Could not locate register to search for");
            }
        }

        private static void FindFunctionCall(string R0, string R1, string R2, string R3, string Result)
        {
            FindNextFunctionCall(R0, R1, R2, R3, Result);
        }

        private static void FindNextFunctionCall(string R0, string R1, string R2, string R3, string Result)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for function call");
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) + 1; i < AnalyzedFile.Code.Count; i++)
            {
                if (AnalyzedFile.Code.Values[i].Mnemonic == "bl")
                {
                    // Function call found. Now check criteria.
                    bool Match;

                    if (R0 != null)
                    {
                        const string Register = "r0";
                        string Pattern = R0;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R1 != null)
                    {
                        const string Register = "r1";
                        string Pattern = R1;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R2 != null)
                    {
                        const string Register = "r2";
                        string Pattern = R2;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R3 != null)
                    {
                        const string Register = "r3";
                        string Pattern = R3;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (Result != null)
                    {
                        const string Register = "r0";
                        string Pattern = Result;
                        Match = false;
                        int j = i + 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && Tokens.Any(t => t.Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j++;
                        }
                        while (j < AnalyzedFile.Code.Count);
                        if (!Match)
                            continue;
                    }

                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found function-call in code at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    FindSuccess = true;
                    return;
                }
            }

            // throw new ScriptExecutionException("Reference not found");
            FindSuccess = false;
        }

        private static void FindPreviousFunctionCall(string R0, string R1, string R2, string R3, string Result)
        {
            if (AnalyzedFile == null)
                throw new ScriptExecutionException("PatchFile not defined");

            WriteLog("Looking for function call");
            for (int i = AnalyzedFile.Code.IndexOfKey(CurrentVirtualAddressTarget) - 1; i >= 0; i--)
            {
                if (AnalyzedFile.Code.Values[i].Mnemonic == "bl")
                {
                    // Function call found. Now check criteria.
                    bool Match;

                    if (R0 != null)
                    {
                        const string Register = "r0";
                        string Pattern = R0;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R1 != null)
                    {
                        const string Register = "r1";
                        string Pattern = R1;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R2 != null)
                    {
                        const string Register = "r2";
                        string Pattern = R2;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (R3 != null)
                    {
                        const string Register = "r3";
                        string Pattern = R3;
                        Match = false;
                        int j = i - 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && (Tokens[0].Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j--;
                        }
                        while (j >= 0);
                        if (!Match)
                            continue;
                    }

                    if (Result != null)
                    {
                        const string Register = "r0";
                        string Pattern = Result;
                        Match = false;
                        int j = i + 1;
                        do
                        {
                            List<Token> Tokens = ArmThumbTokenizer(AnalyzedFile.Code.Values[j].Operand);
                            if ((Tokens.Count > 0) && Tokens.Any(t => t.Text == Register))
                            {
                                Match = MatchPattern(AnalyzedFile.Code.Values[j], Pattern);
                                break;
                            }
                            j++;
                        }
                        while (j < AnalyzedFile.Code.Count);
                        if (!Match)
                            continue;
                    }

                    CurrentVirtualAddressTarget = AnalyzedFile.Code.Values[i].Address;
                    WriteLog("Found function-call in code at virtual address: 0x" + CurrentVirtualAddressTarget.ToString("X8"));
                    FindSuccess = true;
                    return;
                }
            }

            // throw new ScriptExecutionException("Reference not found");
            FindSuccess = false;
        }
    }

    public class CodeLine
    {
        public string Label;
        public string Code;
        public string PatchCode;
    }

    public enum TokenType
    {
        Bracket,
        Separator,
        Operator,
        Text,
        Number
    }

    public class Token
    {
        public TokenType Type;
        public string Text;
        public UInt32 Value;
    }

    public class ScriptParserException: Exception
    {
        public ScriptParserException(string Message): base(Message)
        {
        }

        public ScriptParserException() : base()
        {
        }

        public ScriptParserException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class ScriptExecutionException: Exception
    {
        public ScriptExecutionException(string Message) : base(Message)
        {
        }

        public ScriptExecutionException(string Message, Exception InnerException) : base(Message, InnerException)
        {
        }

        public ScriptExecutionException() : base()
        {
        }
    }

    /// <summary>
    /// Extension method by: Oleg Zarevennyi
    /// https://stackoverflow.com/a/45756981
    /// </summary>
    static public class StringExtensions
    {
        public static string Replace(this string str, string oldValue, string @newValue, StringComparison comparisonType)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }
            if (str.Length == 0)
            {
                return str;
            }
            if (oldValue == null)
            {
                throw new ArgumentNullException(nameof(oldValue));
            }
            if (oldValue.Length == 0)
            {
                throw new ArgumentException("String cannot be of zero length.");
            }

            StringBuilder resultStringBuilder = new(str.Length);
            bool isReplacementNullOrEmpty = string.IsNullOrEmpty(@newValue);

            const int valueNotFound = -1;
            int foundAt;
            int startSearchFromIndex = 0;
            while ((foundAt = str.IndexOf(oldValue, startSearchFromIndex, comparisonType)) != valueNotFound)
            {
                int @charsUntilReplacment = foundAt - startSearchFromIndex;
                bool isNothingToAppend = @charsUntilReplacment == 0;
                if (!isNothingToAppend)
                {
                    resultStringBuilder.Append(str, startSearchFromIndex, @charsUntilReplacment);
                }
                if (!isReplacementNullOrEmpty)
                {
                    resultStringBuilder.Append(@newValue);
                }
                startSearchFromIndex = foundAt + oldValue.Length;
                if (startSearchFromIndex == str.Length)
                {
                    return resultStringBuilder.ToString();
                }
            }

            int @charsUntilStringEnd = str.Length - startSearchFromIndex;
            resultStringBuilder.Append(str, startSearchFromIndex, @charsUntilStringEnd);

            return resultStringBuilder.ToString();
        }
    }
}
