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
        private PatchEngine PatchEngine = null;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadPaths();
            CenterToScreen();
        }

        private void LoadPaths()
        {
            RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\Patcher", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Patcher");

            txtVisualStudioPath.Text = (string)Key.GetValue("VisualStudioPath", "");
            if (txtVisualStudioPath.Text.Length == 0)
                txtVisualStudioPath.Text = FindVisualStudioPath();

            txtPatchDefinitionsFile.Text = (string)Key.GetValue("PatchDefinitionsFilePath", "");
            txtScriptFile.Text = (string)Key.GetValue("ScriptFilePath", "");
            txtInputFolder.Text = (string)Key.GetValue("InputFolderPath", "");
            txtOutputFolder.Text = (string)Key.GetValue("OutputFolderPath", "");
            txtBackupFolder.Text = (string)Key.GetValue("BackupFolderPath", "");

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

            string ScriptFilePath = txtScriptFile.Text.Trim();
            if (ScriptFilePath.Length == 0)
            {
                if (Key.GetValue("ScriptFilePath") != null)
                    Key.DeleteValue("ScriptFilePath");
            }
            else
            {
                Key.SetValue("ScriptFilePath", ScriptFilePath);
            }

            string InputFolderPath = txtInputFolder.Text.Trim();
            if (InputFolderPath.Length == 0)
            {
                if (Key.GetValue("InputFolderPath") != null)
                    Key.DeleteValue("InputFolderPath");
            }
            else
            {
                Key.SetValue("InputFolderPath", InputFolderPath);
            }

            string OutputFolderPath = txtOutputFolder.Text.Trim();
            if (OutputFolderPath.Length == 0)
            {
                if (Key.GetValue("OutputFolderPath") != null)
                    Key.DeleteValue("OutputFolderPath");
            }
            else
            {
                Key.SetValue("OutputFolderPath", OutputFolderPath);
            }

            string BackupFolderPath = txtBackupFolder.Text.Trim();
            if (BackupFolderPath.Length == 0)
            {
                if (Key.GetValue("BackupFolderPath") != null)
                    Key.DeleteValue("BackupFolderPath");
            }
            else
            {
                Key.SetValue("BackupFolderPath", BackupFolderPath);
            }
        }

        private bool LoadingPatchDefinitions = false;

        private void LoadPatchDefinitions()
        {
            if (LoadingPatchDefinitions)
                return;
            LoadingPatchDefinitions = true;

            try
            {
                string Definitions = File.ReadAllText(txtPatchDefinitionsFile.Text);
                PatchEngine = new PatchEngine(Definitions);
            }
            catch
            {
                PatchEngine = new PatchEngine();
            }

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

        private void cmdInputFolder_Click(object sender, EventArgs e)
        {
            FolderSelectDialog Dialog = new();
            Dialog.Title = "Select input location";
            Dialog.InitialDirectory = txtInputFolder.Text;
            try
            {
                Dialog.InitialDirectory = txtInputFolder.Text;
            }
            catch { }
            bool Result = Dialog.ShowDialog();
            if (Result)
            {
                txtInputFolder.Text = Dialog.FileName;
                txtOutputFolder.Text = "";
            }
        }

        private void cmdOutputFolder_Click(object sender, EventArgs e)
        {
            FolderSelectDialog Dialog = new();
            Dialog.Title = "Select output location";
            Dialog.InitialDirectory = txtOutputFolder.Text;
            try
            {
                Dialog.InitialDirectory = txtOutputFolder.Text;
            }
            catch { }
            bool Result = Dialog.ShowDialog();
            if (Result)
            {
                txtOutputFolder.Text = Dialog.FileName;
            }
        }

        private void cmdScriptFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog.CheckFileExists = true;
            OpenFileDialog.DefaultExt = "pds";
            try
            {
                OpenFileDialog.FileName = Path.GetFileName(txtScriptFile.Text);
                OpenFileDialog.InitialDirectory = Path.GetDirectoryName(txtScriptFile.Text);
            }
            catch { }
            OpenFileDialog.Multiselect = false;
            OpenFileDialog.Title = "Open patch-definition-script-file";
            System.Windows.Forms.DialogResult Result = OpenFileDialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                txtScriptFile.Text = OpenFileDialog.FileName;
            }
        }

        private void cmdCompile_Click(object sender, EventArgs e)
        {
            ClearLog();
            StorePaths();
            ScriptEngine.ExecuteScript(txtVisualStudioPath.Text.Trim(), txtScriptFile.Text.Trim(), txtInputFolder.Text.Trim(), PatchEngine: PatchEngine, WriteLog: WriteLog);
        }

        private void cmdPatch_Click(object sender, EventArgs e)
        {
            ClearLog();
            StorePaths();
            ScriptEngine.ExecuteScript(txtVisualStudioPath.Text.Trim(), txtScriptFile.Text.Trim(), txtInputFolder.Text.Trim(), PatchEngine, txtOutputFolder.Text.Trim(), txtBackupFolder.Text.Trim().Length == 0 ? null : txtBackupFolder.Text.Trim(), WriteLog);

            PatchEngine.WriteDefinitions(txtPatchDefinitionsFile.Text);
            WriteLog("Patch-definitions written to: " + txtPatchDefinitionsFile.Text);
        }

        private void ClearLog()
        {
            txtConsole.Clear();
        }

        private void WriteLog(string Line)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke((MethodInvoker)delegate { WriteLog(Line); });
            }
            else
            {
                txtConsole.AppendText(Line + Environment.NewLine);
                txtConsole.Select(txtConsole.Text.Length, 0);
                txtConsole.ScrollToCaret();
                System.Diagnostics.Debug.WriteLine(Line);
            }
        }

        private void cmdBackupFolder_Click(object sender, EventArgs e)
        {
            FolderSelectDialog Dialog = new();
            Dialog.Title = "Select backup location";
            Dialog.InitialDirectory = txtBackupFolder.Text;
            try
            {
                Dialog.InitialDirectory = txtBackupFolder.Text;
            }
            catch { }
            bool Result = Dialog.ShowDialog();
            if (Result)
            {
                txtBackupFolder.Text = Dialog.FileName;
            }
        }

        private void CapstoneLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/aquynh/capstone/blob/master/LICENSE.TXT");
        }

        private void CapstoneNetLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/9ee1/Capstone.NET/blob/master/LICENSE");
        }
    }
}
