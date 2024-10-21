namespace Patcher
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.txtVisualStudioPath = new System.Windows.Forms.TextBox();
            this.FolderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.SaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.cmdVisualStudioPath = new System.Windows.Forms.Button();
            this.cmdInputFile = new System.Windows.Forms.Button();
            this.txtInputFile = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cmdOutputFile = new System.Windows.Forms.Button();
            this.txtOutputFile = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cmdPatchDefinitionsFile = new System.Windows.Forms.Button();
            this.txtPatchDefinitionsFile = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.cmbPatchDefinitionName = new System.Windows.Forms.ComboBox();
            this.cmbTargetVersion = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.cmbTargetPath = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.txtAssemblyCode = new System.Windows.Forms.TextBox();
            this.txtCompiledOpcodes = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.txtVirtualOffset = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.cmbCodeType = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.cmdCompile = new System.Windows.Forms.Button();
            this.cmdPatch = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(191, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Path to Visual Studio with ARM32 SDK";
            // 
            // txtVisualStudioPath
            // 
            this.txtVisualStudioPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtVisualStudioPath.Location = new System.Drawing.Point(18, 29);
            this.txtVisualStudioPath.Name = "txtVisualStudioPath";
            this.txtVisualStudioPath.Size = new System.Drawing.Size(665, 20);
            this.txtVisualStudioPath.TabIndex = 1;
            // 
            // OpenFileDialog
            // 
            this.OpenFileDialog.FileName = "openFileDialog1";
            // 
            // cmdVisualStudioPath
            // 
            this.cmdVisualStudioPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdVisualStudioPath.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdVisualStudioPath.Location = new System.Drawing.Point(689, 28);
            this.cmdVisualStudioPath.Name = "cmdVisualStudioPath";
            this.cmdVisualStudioPath.Size = new System.Drawing.Size(35, 22);
            this.cmdVisualStudioPath.TabIndex = 2;
            this.cmdVisualStudioPath.Text = "...";
            this.cmdVisualStudioPath.UseVisualStyleBackColor = true;
            this.cmdVisualStudioPath.Click += new System.EventHandler(this.cmdVisualStudioPath_Click);
            // 
            // cmdInputFile
            // 
            this.cmdInputFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdInputFile.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdInputFile.Location = new System.Drawing.Point(689, 301);
            this.cmdInputFile.Name = "cmdInputFile";
            this.cmdInputFile.Size = new System.Drawing.Size(35, 22);
            this.cmdInputFile.TabIndex = 14;
            this.cmdInputFile.Text = "...";
            this.cmdInputFile.UseVisualStyleBackColor = true;
            this.cmdInputFile.Click += new System.EventHandler(this.cmdInputFile_Click);
            // 
            // txtInputFile
            // 
            this.txtInputFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInputFile.Location = new System.Drawing.Point(18, 302);
            this.txtInputFile.Name = "txtInputFile";
            this.txtInputFile.Size = new System.Drawing.Size(665, 20);
            this.txtInputFile.TabIndex = 13;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 286);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "Input file";
            // 
            // cmdOutputFile
            // 
            this.cmdOutputFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOutputFile.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdOutputFile.Location = new System.Drawing.Point(689, 349);
            this.cmdOutputFile.Name = "cmdOutputFile";
            this.cmdOutputFile.Size = new System.Drawing.Size(35, 22);
            this.cmdOutputFile.TabIndex = 17;
            this.cmdOutputFile.Text = "...";
            this.cmdOutputFile.UseVisualStyleBackColor = true;
            this.cmdOutputFile.Click += new System.EventHandler(this.cmdOutputFile_Click);
            // 
            // txtOutputFile
            // 
            this.txtOutputFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOutputFile.Location = new System.Drawing.Point(18, 350);
            this.txtOutputFile.Name = "txtOutputFile";
            this.txtOutputFile.Size = new System.Drawing.Size(665, 20);
            this.txtOutputFile.TabIndex = 16;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 334);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(101, 13);
            this.label3.TabIndex = 15;
            this.label3.Text = "Output file (optional)";
            // 
            // cmdPatchDefinitionsFile
            // 
            this.cmdPatchDefinitionsFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdPatchDefinitionsFile.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdPatchDefinitionsFile.Location = new System.Drawing.Point(689, 91);
            this.cmdPatchDefinitionsFile.Name = "cmdPatchDefinitionsFile";
            this.cmdPatchDefinitionsFile.Size = new System.Drawing.Size(35, 22);
            this.cmdPatchDefinitionsFile.TabIndex = 5;
            this.cmdPatchDefinitionsFile.Text = "...";
            this.cmdPatchDefinitionsFile.UseVisualStyleBackColor = true;
            this.cmdPatchDefinitionsFile.Click += new System.EventHandler(this.cmdPatchDefinitionsFile_Click);
            // 
            // txtPatchDefinitionsFile
            // 
            this.txtPatchDefinitionsFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtPatchDefinitionsFile.Location = new System.Drawing.Point(18, 92);
            this.txtPatchDefinitionsFile.Name = "txtPatchDefinitionsFile";
            this.txtPatchDefinitionsFile.Size = new System.Drawing.Size(665, 20);
            this.txtPatchDefinitionsFile.TabIndex = 4;
            this.txtPatchDefinitionsFile.Leave += new System.EventHandler(this.txtPatchDefinitionsFile_Leave);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 76);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(117, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Patch defintions xml-file";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 124);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(107, 13);
            this.label5.TabIndex = 6;
            this.label5.Text = "Patch defintion name";
            // 
            // cmbPatchDefinitionName
            // 
            this.cmbPatchDefinitionName.FormattingEnabled = true;
            this.cmbPatchDefinitionName.Location = new System.Drawing.Point(18, 140);
            this.cmbPatchDefinitionName.Name = "cmbPatchDefinitionName";
            this.cmbPatchDefinitionName.Size = new System.Drawing.Size(302, 21);
            this.cmbPatchDefinitionName.TabIndex = 7;
            this.cmbPatchDefinitionName.SelectedValueChanged += new System.EventHandler(this.cmbPatchDefinitionName_SelectedValueChanged);
            this.cmbPatchDefinitionName.Leave += new System.EventHandler(this.cmbPatchDefinitionName_Leave);
            // 
            // cmbTargetVersion
            // 
            this.cmbTargetVersion.FormattingEnabled = true;
            this.cmbTargetVersion.Location = new System.Drawing.Point(18, 189);
            this.cmbTargetVersion.Name = "cmbTargetVersion";
            this.cmbTargetVersion.Size = new System.Drawing.Size(302, 21);
            this.cmbTargetVersion.TabIndex = 9;
            this.cmbTargetVersion.SelectedValueChanged += new System.EventHandler(this.cmbTargetVersion_SelectedValueChanged);
            this.cmbTargetVersion.Leave += new System.EventHandler(this.cmbTargetVersion_Leave);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 173);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(129, 13);
            this.label6.TabIndex = 8;
            this.label6.Text = "Target version description";
            // 
            // cmbTargetPath
            // 
            this.cmbTargetPath.FormattingEnabled = true;
            this.cmbTargetPath.Location = new System.Drawing.Point(18, 238);
            this.cmbTargetPath.Name = "cmbTargetPath";
            this.cmbTargetPath.Size = new System.Drawing.Size(302, 21);
            this.cmbTargetPath.TabIndex = 11;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(15, 222);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(269, 13);
            this.label7.TabIndex = 10;
            this.label7.Text = "Folder for target file relative to Patch Defintion rootfolder";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(15, 497);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(365, 13);
            this.label8.TabIndex = 22;
            this.label8.Text = "ARM32 Patch / Shell assembly code (labels need to be followed by a colon)";
            // 
            // txtAssemblyCode
            // 
            this.txtAssemblyCode.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAssemblyCode.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtAssemblyCode.Location = new System.Drawing.Point(18, 513);
            this.txtAssemblyCode.Multiline = true;
            this.txtAssemblyCode.Name = "txtAssemblyCode";
            this.txtAssemblyCode.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtAssemblyCode.Size = new System.Drawing.Size(706, 161);
            this.txtAssemblyCode.TabIndex = 23;
            // 
            // txtCompiledOpcodes
            // 
            this.txtCompiledOpcodes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCompiledOpcodes.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCompiledOpcodes.Location = new System.Drawing.Point(18, 713);
            this.txtCompiledOpcodes.Multiline = true;
            this.txtCompiledOpcodes.Name = "txtCompiledOpcodes";
            this.txtCompiledOpcodes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtCompiledOpcodes.Size = new System.Drawing.Size(706, 59);
            this.txtCompiledOpcodes.TabIndex = 25;
            // 
            // label9
            // 
            this.label9.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(15, 697);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(94, 13);
            this.label9.TabIndex = 24;
            this.label9.Text = "Compiled opcodes";
            // 
            // txtVirtualOffset
            // 
            this.txtVirtualOffset.Location = new System.Drawing.Point(18, 416);
            this.txtVirtualOffset.Name = "txtVirtualOffset";
            this.txtVirtualOffset.Size = new System.Drawing.Size(302, 20);
            this.txtVirtualOffset.TabIndex = 19;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(15, 400);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(309, 13);
            this.label10.TabIndex = 18;
            this.label10.Text = "Virtual offset (hex) (leave empty for only recalculating checksum)";
            // 
            // cmbCodeType
            // 
            this.cmbCodeType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCodeType.FormattingEnabled = true;
            this.cmbCodeType.Items.AddRange(new object[] {
            "ARM",
            "Thumb",
            "Thumb2"});
            this.cmbCodeType.Location = new System.Drawing.Point(18, 464);
            this.cmbCodeType.Name = "cmbCodeType";
            this.cmbCodeType.Size = new System.Drawing.Size(302, 21);
            this.cmbCodeType.TabIndex = 21;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(15, 448);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(55, 13);
            this.label11.TabIndex = 20;
            this.label11.Text = "Code type";
            // 
            // cmdCompile
            // 
            this.cmdCompile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCompile.Location = new System.Drawing.Point(470, 792);
            this.cmdCompile.Name = "cmdCompile";
            this.cmdCompile.Size = new System.Drawing.Size(120, 33);
            this.cmdCompile.TabIndex = 26;
            this.cmdCompile.Text = "Compile";
            this.cmdCompile.UseVisualStyleBackColor = true;
            this.cmdCompile.Click += new System.EventHandler(this.cmdCompile_Click);
            // 
            // cmdPatch
            // 
            this.cmdPatch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdPatch.Location = new System.Drawing.Point(604, 792);
            this.cmdPatch.Name = "cmdPatch";
            this.cmdPatch.Size = new System.Drawing.Size(120, 33);
            this.cmdPatch.TabIndex = 27;
            this.cmdPatch.Text = "Patch";
            this.cmdPatch.UseVisualStyleBackColor = true;
            this.cmdPatch.Click += new System.EventHandler(this.cmdPatch_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(742, 840);
            this.Controls.Add(this.cmdPatch);
            this.Controls.Add(this.cmdCompile);
            this.Controls.Add(this.cmbCodeType);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.txtVirtualOffset);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.txtCompiledOpcodes);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.txtAssemblyCode);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.cmbTargetPath);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.cmbTargetVersion);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.cmbPatchDefinitionName);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.cmdPatchDefinitionsFile);
            this.Controls.Add(this.txtPatchDefinitionsFile);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cmdOutputFile);
            this.Controls.Add(this.txtOutputFile);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cmdInputFile);
            this.Controls.Add(this.txtInputFile);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cmdVisualStudioPath);
            this.Controls.Add(this.txtVisualStudioPath);
            this.Controls.Add(this.label1);
            this.Name = "MainForm";
            this.Text = "ARM patcher by Rene Lergner";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtVisualStudioPath;
        private System.Windows.Forms.FolderBrowserDialog FolderBrowserDialog;
        private System.Windows.Forms.OpenFileDialog OpenFileDialog;
        private System.Windows.Forms.SaveFileDialog SaveFileDialog;
        private System.Windows.Forms.Button cmdVisualStudioPath;
        private System.Windows.Forms.Button cmdInputFile;
        private System.Windows.Forms.TextBox txtInputFile;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button cmdOutputFile;
        private System.Windows.Forms.TextBox txtOutputFile;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button cmdPatchDefinitionsFile;
        private System.Windows.Forms.TextBox txtPatchDefinitionsFile;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox cmbPatchDefinitionName;
        private System.Windows.Forms.ComboBox cmbTargetVersion;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox cmbTargetPath;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox txtAssemblyCode;
        private System.Windows.Forms.TextBox txtCompiledOpcodes;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox txtVirtualOffset;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox cmbCodeType;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Button cmdCompile;
        private System.Windows.Forms.Button cmdPatch;
    }
}

