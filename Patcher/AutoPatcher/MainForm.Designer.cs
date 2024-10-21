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
            this.cmdInputFolder = new System.Windows.Forms.Button();
            this.txtInputFolder = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cmdOutputFolder = new System.Windows.Forms.Button();
            this.txtOutputFolder = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cmdPatchDefinitionsFile = new System.Windows.Forms.Button();
            this.txtPatchDefinitionsFile = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtConsole = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.cmdCompile = new System.Windows.Forms.Button();
            this.cmdPatch = new System.Windows.Forms.Button();
            this.cmdScriptFile = new System.Windows.Forms.Button();
            this.txtScriptFile = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.cmdBackupFolder = new System.Windows.Forms.Button();
            this.txtBackupFolder = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.CapstoneLink = new System.Windows.Forms.LinkLabel();
            this.label8 = new System.Windows.Forms.Label();
            this.CapstoneNetLink = new System.Windows.Forms.LinkLabel();
            this.label10 = new System.Windows.Forms.Label();
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
            // cmdInputFolder
            // 
            this.cmdInputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdInputFolder.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdInputFolder.Location = new System.Drawing.Point(689, 224);
            this.cmdInputFolder.Name = "cmdInputFolder";
            this.cmdInputFolder.Size = new System.Drawing.Size(35, 22);
            this.cmdInputFolder.TabIndex = 8;
            this.cmdInputFolder.Text = "...";
            this.cmdInputFolder.UseVisualStyleBackColor = true;
            this.cmdInputFolder.Click += new System.EventHandler(this.cmdInputFolder_Click);
            // 
            // txtInputFolder
            // 
            this.txtInputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInputFolder.Location = new System.Drawing.Point(18, 225);
            this.txtInputFolder.Name = "txtInputFolder";
            this.txtInputFolder.Size = new System.Drawing.Size(665, 20);
            this.txtInputFolder.TabIndex = 7;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 209);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(71, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "Input location";
            // 
            // cmdOutputFolder
            // 
            this.cmdOutputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOutputFolder.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdOutputFolder.Location = new System.Drawing.Point(689, 322);
            this.cmdOutputFolder.Name = "cmdOutputFolder";
            this.cmdOutputFolder.Size = new System.Drawing.Size(35, 22);
            this.cmdOutputFolder.TabIndex = 12;
            this.cmdOutputFolder.Text = "...";
            this.cmdOutputFolder.UseVisualStyleBackColor = true;
            this.cmdOutputFolder.Click += new System.EventHandler(this.cmdOutputFolder_Click);
            // 
            // txtOutputFolder
            // 
            this.txtOutputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOutputFolder.Location = new System.Drawing.Point(18, 323);
            this.txtOutputFolder.Name = "txtOutputFolder";
            this.txtOutputFolder.Size = new System.Drawing.Size(665, 20);
            this.txtOutputFolder.TabIndex = 11;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 307);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(125, 13);
            this.label3.TabIndex = 15;
            this.label3.Text = "Output location (optional)";
            // 
            // cmdPatchDefinitionsFile
            // 
            this.cmdPatchDefinitionsFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdPatchDefinitionsFile.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdPatchDefinitionsFile.Location = new System.Drawing.Point(689, 91);
            this.cmdPatchDefinitionsFile.Name = "cmdPatchDefinitionsFile";
            this.cmdPatchDefinitionsFile.Size = new System.Drawing.Size(35, 22);
            this.cmdPatchDefinitionsFile.TabIndex = 4;
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
            this.txtPatchDefinitionsFile.TabIndex = 3;
            this.txtPatchDefinitionsFile.Leave += new System.EventHandler(this.txtPatchDefinitionsFile_Leave);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 76);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(117, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Patch definitions xml-file";
            // 
            // txtConsole
            // 
            this.txtConsole.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtConsole.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtConsole.Location = new System.Drawing.Point(18, 383);
            this.txtConsole.Multiline = true;
            this.txtConsole.Name = "txtConsole";
            this.txtConsole.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtConsole.Size = new System.Drawing.Size(706, 392);
            this.txtConsole.TabIndex = 13;
            // 
            // label9
            // 
            this.label9.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(15, 367);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(78, 13);
            this.label9.TabIndex = 24;
            this.label9.Text = "Console output";
            // 
            // cmdCompile
            // 
            this.cmdCompile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCompile.Location = new System.Drawing.Point(470, 792);
            this.cmdCompile.Name = "cmdCompile";
            this.cmdCompile.Size = new System.Drawing.Size(120, 33);
            this.cmdCompile.TabIndex = 14;
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
            this.cmdPatch.TabIndex = 15;
            this.cmdPatch.Text = "Patch";
            this.cmdPatch.UseVisualStyleBackColor = true;
            this.cmdPatch.Click += new System.EventHandler(this.cmdPatch_Click);
            // 
            // cmdScriptFile
            // 
            this.cmdScriptFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdScriptFile.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdScriptFile.Location = new System.Drawing.Point(689, 155);
            this.cmdScriptFile.Name = "cmdScriptFile";
            this.cmdScriptFile.Size = new System.Drawing.Size(35, 22);
            this.cmdScriptFile.TabIndex = 6;
            this.cmdScriptFile.Text = "...";
            this.cmdScriptFile.UseVisualStyleBackColor = true;
            this.cmdScriptFile.Click += new System.EventHandler(this.cmdScriptFile_Click);
            // 
            // txtScriptFile
            // 
            this.txtScriptFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtScriptFile.Location = new System.Drawing.Point(18, 156);
            this.txtScriptFile.Name = "txtScriptFile";
            this.txtScriptFile.Size = new System.Drawing.Size(665, 20);
            this.txtScriptFile.TabIndex = 5;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 140);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(50, 13);
            this.label5.TabIndex = 28;
            this.label5.Text = "Script-file";
            // 
            // cmdBackupFolder
            // 
            this.cmdBackupFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdBackupFolder.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdBackupFolder.Location = new System.Drawing.Point(689, 273);
            this.cmdBackupFolder.Name = "cmdBackupFolder";
            this.cmdBackupFolder.Size = new System.Drawing.Size(35, 22);
            this.cmdBackupFolder.TabIndex = 10;
            this.cmdBackupFolder.Text = "...";
            this.cmdBackupFolder.UseVisualStyleBackColor = true;
            this.cmdBackupFolder.Click += new System.EventHandler(this.cmdBackupFolder_Click);
            // 
            // txtBackupFolder
            // 
            this.txtBackupFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtBackupFolder.Location = new System.Drawing.Point(18, 274);
            this.txtBackupFolder.Name = "txtBackupFolder";
            this.txtBackupFolder.Size = new System.Drawing.Size(665, 20);
            this.txtBackupFolder.TabIndex = 9;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 258);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(130, 13);
            this.label6.TabIndex = 31;
            this.label6.Text = "Backup location (optional)";
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(20, 807);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(66, 13);
            this.label7.TabIndex = 32;
            this.label7.Text = "Powered by ";
            // 
            // CapstoneLink
            // 
            this.CapstoneLink.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CapstoneLink.AutoSize = true;
            this.CapstoneLink.Location = new System.Drawing.Point(80, 807);
            this.CapstoneLink.Name = "CapstoneLink";
            this.CapstoneLink.Size = new System.Drawing.Size(52, 13);
            this.CapstoneLink.TabIndex = 33;
            this.CapstoneLink.TabStop = true;
            this.CapstoneLink.Text = "Capstone";
            this.CapstoneLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.CapstoneLink_LinkClicked);
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(129, 807);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(28, 13);
            this.label8.TabIndex = 34;
            this.label8.Text = "and ";
            // 
            // CapstoneNetLink
            // 
            this.CapstoneNetLink.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CapstoneNetLink.AutoSize = true;
            this.CapstoneNetLink.Location = new System.Drawing.Point(151, 807);
            this.CapstoneNetLink.Name = "CapstoneNetLink";
            this.CapstoneNetLink.Size = new System.Drawing.Size(77, 13);
            this.CapstoneNetLink.TabIndex = 35;
            this.CapstoneNetLink.TabStop = true;
            this.CapstoneNetLink.Text = "Capstone.NET";
            this.CapstoneNetLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.CapstoneNetLink_LinkClicked);
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(225, 807);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(42, 13);
            this.label10.TabIndex = 36;
            this.label10.Text = "libraries";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(742, 840);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.CapstoneNetLink);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.CapstoneLink);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.cmdBackupFolder);
            this.Controls.Add(this.txtBackupFolder);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.cmdScriptFile);
            this.Controls.Add(this.txtScriptFile);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.cmdPatch);
            this.Controls.Add(this.cmdCompile);
            this.Controls.Add(this.txtConsole);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.cmdPatchDefinitionsFile);
            this.Controls.Add(this.txtPatchDefinitionsFile);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cmdOutputFolder);
            this.Controls.Add(this.txtOutputFolder);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cmdInputFolder);
            this.Controls.Add(this.txtInputFolder);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cmdVisualStudioPath);
            this.Controls.Add(this.txtVisualStudioPath);
            this.Controls.Add(this.label1);
            this.Name = "MainForm";
            this.Text = "ARM Auto-patcher by Rene Lergner";
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
        private System.Windows.Forms.Button cmdInputFolder;
        private System.Windows.Forms.TextBox txtInputFolder;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button cmdOutputFolder;
        private System.Windows.Forms.TextBox txtOutputFolder;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button cmdPatchDefinitionsFile;
        private System.Windows.Forms.TextBox txtPatchDefinitionsFile;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtConsole;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button cmdCompile;
        private System.Windows.Forms.Button cmdPatch;
        private System.Windows.Forms.Button cmdScriptFile;
        private System.Windows.Forms.TextBox txtScriptFile;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button cmdBackupFolder;
        private System.Windows.Forms.TextBox txtBackupFolder;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.LinkLabel CapstoneLink;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.LinkLabel CapstoneNetLink;
        private System.Windows.Forms.Label label10;
    }
}

