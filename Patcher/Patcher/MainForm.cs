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

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WPinternals;

namespace Patcher
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            cmbCodeType.SelectedItem = "Thumb2";
            LoadPaths();
            CenterToScreen();
        }

        private void cmdCompile_Click(object sender, EventArgs e)
        {
            string VirtualOffsetString = txtVirtualOffset.Text.Trim();
            if (VirtualOffsetString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                VirtualOffsetString = VirtualOffsetString[2..];

            CodeType CodeType = Patcher.CodeType.Thumb2;
            byte[] CompiledCode = null;
            if (UInt32.TryParse(VirtualOffsetString, System.Globalization.NumberStyles.HexNumber, null, out uint VirtualOffset))
            {
                CodeType = (CodeType)Enum.Parse(typeof(Patcher.CodeType), cmbCodeType.SelectedItem.ToString());
                CompiledCode = ArmCompiler.Compile(txtVisualStudioPath.Text, VirtualOffset, CodeType, txtAssemblyCode.Text);
            }
            if ((VirtualOffset != 0) && (CompiledCode == null))
                txtCompiledOpcodes.Text = ArmCompiler.Output;
            else if (CompiledCode != null)
                txtCompiledOpcodes.Text = Converter.ConvertHexToString(CompiledCode, " ");
        }

        private void LoadPaths()
        {
            RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\Patcher", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Patcher");

            txtVisualStudioPath.Text = (string)Key.GetValue("VisualStudioPath", "");
            if (txtVisualStudioPath.Text.Length == 0)
                txtVisualStudioPath.Text = FindVisualStudioPath();

            txtPatchDefinitionsFile.Text = (string)Key.GetValue("PatchDefinitionsFilePath", "");
            cmbPatchDefinitionName.Text = (string)Key.GetValue("PatchDefinitionName", "");
            cmbTargetVersion.Text = (string)Key.GetValue("TargetVersion", "");
            cmbTargetPath.Text = (string)Key.GetValue("TargetFilePath", "");
            txtInputFile.Text = (string)Key.GetValue("InputFilePath", "");
            txtOutputFile.Text = (string)Key.GetValue("OutputFilePath", "");

            LoadPatchDefinitions();
        }

        public static string[] FindMSVCBinaryPaths(string s)
        {
            string LegacyPath = Path.Combine(s, @"VC\bin");
            if (Directory.Exists(LegacyPath))
            {
                return new string[] { LegacyPath };
            }

            if (Directory.Exists(Path.Combine(s, @"VC\Tools\MSVC")))
            {
                IEnumerable<string> MSVCs = Directory.EnumerateDirectories(Path.Combine(s, @"VC\Tools\MSVC"));
                IEnumerable<string> Bins = MSVCs.Select(s => Path.Combine(s, "bin")).Where(s => Directory.Exists(s));
                return Bins.ToArray();
            }

            return Array.Empty<string>();
        }

        public static string FindArmAsmPath(string s)
        {
            foreach (string MSVCBin in FindMSVCBinaryPaths(s))
            {
                string path1 = Path.Combine(MSVCBin, "x86_arm");
                string path2 = Path.Combine(MSVCBin, @"Hostx86\arm");

                if (File.Exists(Path.Combine(path1, "armasm.exe")))
                {
                    return path1;
                }

                if (File.Exists(Path.Combine(path2, "armasm.exe")))
                {
                    return path2;
                }
            }

            return "";
        }

        private static string FindVisualStudioPath()
        {
            IEnumerable<string> MainX86VSDirectories = Directory.EnumerateDirectories(@"C:\Program Files (x86)\", "Microsoft Visual Studio*");
            IEnumerable<string> MainX64VSDirectories = Directory.EnumerateDirectories(@"C:\Program Files\", "Microsoft Visual Studio*");

            IEnumerable<string> MainVSDirectories = MainX86VSDirectories.Union(MainX64VSDirectories);

            IEnumerable<string> SubMainVSDirectories = MainVSDirectories.SelectMany(s => Directory.EnumerateDirectories(s));
            IEnumerable<string> SubSubMainVSDirectories = SubMainVSDirectories.SelectMany(s => Directory.EnumerateDirectories(s));
            IEnumerable<string> Directories = MainVSDirectories.Union(SubMainVSDirectories).Union(SubSubMainVSDirectories);

            string attempt1 = Directories.Where(s => FindArmAsmPath(s) != "").OrderByDescending(s => File.GetCreationTime(Path.Combine(s, @"VC\bin\x86_arm\armasm.exe"))).FirstOrDefault() ?? "";

            if (attempt1 != "")
                return attempt1;

            return Directories.Where(s => Directory.Exists(Path.Combine(s, @"VC\Tools\MSVC"))).Select(s => Path.Combine(s, @"VC\Tools\MSVC")).SelectMany(s => Directory.EnumerateDirectories(s)).Where(s => File.Exists(Path.Combine(s, @"bin\Hostx86\arm\armasm.exe"))).OrderByDescending(s => File.GetCreationTime(Path.Combine(s, @"bin\Hostx86\arm\armasm.exe"))).FirstOrDefault() ?? "";
        }

        private void StorePaths()
        {
            RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\Patcher", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Patcher");

            string VisualStudioPath = txtVisualStudioPath.Text.Trim();
            if (VisualStudioPath.Length == 0)
            {
                if (Key.GetValue("VisualStudioPath") != null)
                    Key.DeleteValue("VisualStudioPath");
            }
            else
            {
                Key.SetValue("VisualStudioPath", VisualStudioPath);
            }

            string PatchDefinitionsFilePath = txtPatchDefinitionsFile.Text.Trim();
            if (PatchDefinitionsFilePath.Length == 0)
            {
                if (Key.GetValue("PatchDefinitionsFilePath") != null)
                    Key.DeleteValue("PatchDefinitionsFilePath");
            }
            else
            {
                Key.SetValue("PatchDefinitionsFilePath", PatchDefinitionsFilePath);
            }

            string PatchDefinitionName = cmbPatchDefinitionName.Text.Trim();
            if (PatchDefinitionName.Length == 0)
            {
                if (Key.GetValue("PatchDefinitionName") != null)
                    Key.DeleteValue("PatchDefinitionName");
            }
            else
            {
                Key.SetValue("PatchDefinitionName", PatchDefinitionName);
            }

            string TargetVersion = cmbTargetVersion.Text.Trim();
            if (TargetVersion.Length == 0)
            {
                if (Key.GetValue("TargetVersion") != null)
                    Key.DeleteValue("TargetVersion");
            }
            else
            {
                Key.SetValue("TargetVersion", TargetVersion);
            }

            string TargetFilePath = cmbTargetPath.Text.Trim();
            if (TargetFilePath.Length == 0)
            {
                if (Key.GetValue("TargetFilePath") != null)
                    Key.DeleteValue("TargetFilePath");
            }
            else
            {
                Key.SetValue("TargetFilePath", TargetFilePath);
            }

            string InputFilePath = txtInputFile.Text.Trim();
            if (InputFilePath.Length == 0)
            {
                if (Key.GetValue("InputFilePath") != null)
                    Key.DeleteValue("InputFilePath");
            }
            else
            {
                Key.SetValue("InputFilePath", InputFilePath);
            }

            string OutputFilePath = txtOutputFile.Text.Trim();
            if (OutputFilePath.Length == 0)
            {
                if (Key.GetValue("OutputFilePath") != null)
                    Key.DeleteValue("OutputFilePath");
            }
            else
            {
                Key.SetValue("OutputFilePath", OutputFilePath);
            }
        }

        private bool LoadingPatchDefinitions = false;

        private void LoadPatchDefinitions()
        {
            if (LoadingPatchDefinitions)
                return;
            LoadingPatchDefinitions = true;

            string PatchDefinitionName = cmbPatchDefinitionName.Text;
            string TargetVersion = cmbTargetVersion.Text;
            string TargetPath = cmbTargetPath.Text;

            cmbPatchDefinitionName.SelectedIndex = -1;
            cmbTargetVersion.SelectedIndex = -1;
            cmbTargetPath.SelectedIndex = -1;

            cmbPatchDefinitionName.Items.Clear();
            cmbTargetVersion.Items.Clear();
            cmbTargetPath.Items.Clear();

            cmbPatchDefinitionName.Text = PatchDefinitionName;
            cmbTargetVersion.Text = TargetVersion;
            cmbTargetPath.Text = TargetPath;

            try
            {
                string Definitions = File.ReadAllText(txtPatchDefinitionsFile.Text);
                PatchEngine Engine = new(Definitions);
                Engine.PatchDefinitions.Where(d => !string.IsNullOrEmpty(d.Name)).Select(d => d.Name).Distinct().ToList().ForEach(n => cmbPatchDefinitionName.Items.Add(n));
                PatchDefinition Definition = null;
                if (cmbPatchDefinitionName.Text.Trim().Length > 0)
                    Definition = Engine.PatchDefinitions.Find(d => string.Equals(d.Name, cmbPatchDefinitionName.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
                if (Definition != null)
                {
                    Definition.TargetVersions.Where(v => !string.IsNullOrEmpty(v.Description)).Select(v => v.Description).Distinct().ToList().ForEach(d => cmbTargetVersion.Items.Add(d));
                    TargetVersion Version = null;
                    if (cmbTargetVersion.Text.Trim().Length > 0)
                        Version = Definition.TargetVersions.Find(v => string.Equals(v.Description, cmbTargetVersion.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
                    if (Version != null)
                    {
                        Version.TargetFiles.Where(f => !string.IsNullOrEmpty(f.Path)).Select(f => Path.GetDirectoryName(f.Path)).Distinct().ToList().ForEach(f => cmbTargetPath.Items.Add(f));
                    }
                }
            }
            catch { }

            LoadingPatchDefinitions = false;
        }

        private void cmdVisualStudioPath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog.SelectedPath = txtVisualStudioPath.Text;
            FolderBrowserDialog.Description = "Select path to Visual Studio with ARM32 SDK";
            System.Windows.Forms.DialogResult Result = FolderBrowserDialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
                txtVisualStudioPath.Text = FolderBrowserDialog.SelectedPath;
        }

        private void cmdPatchDefinitionsFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog.CheckFileExists = false;
            OpenFileDialog.DefaultExt = "xml";
            try
            {
                OpenFileDialog.FileName = Path.GetFileName(txtPatchDefinitionsFile.Text);
                OpenFileDialog.InitialDirectory = Path.GetDirectoryName(txtPatchDefinitionsFile.Text);
            }
            catch { }
            OpenFileDialog.Multiselect = false;
            OpenFileDialog.Title = "Open patch-definitions file";
            System.Windows.Forms.DialogResult Result = OpenFileDialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                txtPatchDefinitionsFile.Text = OpenFileDialog.FileName;
                WindowsFormsSynchronizationContext.Current.Post(s => LoadPatchDefinitions(), null);
            }
        }

        private void txtPatchDefinitionsFile_Leave(object sender, EventArgs e)
        {
            WindowsFormsSynchronizationContext.Current.Post(s => LoadPatchDefinitions(), null);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            StorePaths();
        }

        private void cmbPatchDefinitionName_SelectedValueChanged(object sender, EventArgs e)
        {
            WindowsFormsSynchronizationContext.Current.Post(s => LoadPatchDefinitions(), null);
        }

        private void cmbPatchDefinitionName_Leave(object sender, EventArgs e)
        {
            WindowsFormsSynchronizationContext.Current.Post(s => LoadPatchDefinitions(), null);
        }

        private void cmbTargetVersion_SelectedValueChanged(object sender, EventArgs e)
        {
            WindowsFormsSynchronizationContext.Current.Post(s => LoadPatchDefinitions(), null);
        }

        private void cmbTargetVersion_Leave(object sender, EventArgs e)
        {
            WindowsFormsSynchronizationContext.Current.Post(s => LoadPatchDefinitions(), null);
        }

        private void cmdInputFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog.CheckFileExists = true;
            OpenFileDialog.DefaultExt = "";
            try
            {
                OpenFileDialog.FileName = Path.GetFileName(txtInputFile.Text);
                OpenFileDialog.InitialDirectory = Path.GetDirectoryName(txtInputFile.Text);
            }
            catch { }
            OpenFileDialog.Multiselect = false;
            OpenFileDialog.Title = "Open input file";
            System.Windows.Forms.DialogResult Result = OpenFileDialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                txtInputFile.Text = OpenFileDialog.FileName;
                txtOutputFile.Text = "";
            }
        }

        private void cmdOutputFile_Click(object sender, EventArgs e)
        {
            SaveFileDialog.CheckFileExists = false;
            SaveFileDialog.DefaultExt = "";
            try
            {
                SaveFileDialog.FileName = Path.GetFileName(txtInputFile.Text);
                SaveFileDialog.InitialDirectory = Path.GetDirectoryName(txtOutputFile.Text);
            }
            catch { }
            SaveFileDialog.Title = "Open input file";
            System.Windows.Forms.DialogResult Result = SaveFileDialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
                txtOutputFile.Text = SaveFileDialog.FileName;
        }

        private void cmdPatch_Click(object sender, EventArgs e)
        {
            string VirtualOffsetString = txtVirtualOffset.Text.Trim();
            if (VirtualOffsetString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                VirtualOffsetString = VirtualOffsetString[2..];
            CodeType CodeType = Patcher.CodeType.Thumb2;
            byte[] CompiledCode = null;
            if (UInt32.TryParse(VirtualOffsetString, System.Globalization.NumberStyles.HexNumber, null, out uint VirtualAddress))
            {
                CodeType = (CodeType)Enum.Parse(typeof(Patcher.CodeType), cmbCodeType.SelectedItem.ToString());
                CompiledCode = ArmCompiler.Compile(txtVisualStudioPath.Text, VirtualAddress, CodeType, txtAssemblyCode.Text);
            }
            if ((VirtualAddress != 0) && (CompiledCode == null))
            {
                txtCompiledOpcodes.Text = ArmCompiler.Output;
            }
            else
            {
                if (CompiledCode != null)
                    txtCompiledOpcodes.Text = Converter.ConvertHexToString(CompiledCode, " ");

                string TargetFilePath = Path.Combine(cmbTargetPath.Text, Path.GetFileName(txtInputFile.Text));
                if (TargetFilePath.StartsWith(@"\"))
                    TargetFilePath = TargetFilePath[1..];

                try
                {
                    MainPatcher.AddPatch(txtInputFile.Text, (txtOutputFile.Text.Trim().Length > 0) ? txtOutputFile.Text : null, cmbPatchDefinitionName.Text, cmbTargetVersion.Text, TargetFilePath, txtVisualStudioPath.Text, VirtualAddress, CodeType, txtAssemblyCode.Text, txtPatchDefinitionsFile.Text);
                }
                catch (ArgumentOutOfRangeException)
                {
                    txtCompiledOpcodes.Text = "BAD VIRTUAL OFFSET";
                }
                catch
                {
                    txtCompiledOpcodes.Text = "UNKNOWN ERROR";
                }

                txtVirtualOffset.Focus();
                txtVirtualOffset.SelectAll();
            }
        }
    }
}
