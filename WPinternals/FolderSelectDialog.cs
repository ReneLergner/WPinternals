// This class was found online.
// Original author is probably: Swizzy
// https://github.com/ttgxdinger/Random/blob/master/CPUKey%20Checker/CPUKey%20Checker/FolderSelectDialog.cs

using System;
using System.Windows.Forms;

namespace WPinternals
{
    /// <summary>
    /// Wraps System.Windows.Forms.OpenFileDialog to make it present
    /// a vista-style dialog.
    /// </summary>
    public class FolderSelectDialog
    {
        // Wrapped dialog
        private readonly OpenFileDialog ofd = null;

        /// <summary>
        /// Default constructor
        /// </summary>
        public FolderSelectDialog()
        {
            ofd = new OpenFileDialog
            {
                Filter = "Folders|\n",
                AddExtension = false,
                CheckFileExists = false,
                DereferenceLinks = true,
                Multiselect = false
            };
        }

        #region Properties

        /// <summary>
        /// Gets/Sets the initial folder to be selected. A null value selects the current directory.
        /// </summary>
        public string InitialDirectory
        {
            get { return ofd.InitialDirectory; }
            set { ofd.InitialDirectory = string.IsNullOrEmpty(value) ? Environment.CurrentDirectory : value; }
        }

        /// <summary>
        /// Gets/Sets the title to show in the dialog
        /// </summary>
        public string Title
        {
            get { return ofd.Title; }
            set { ofd.Title = value ?? "Select a folder"; }
        }

        /// <summary>
        /// Gets the selected folder
        /// </summary>
        public string FileName
        {
            get { return ofd.FileName; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Shows the dialog
        /// </summary>
        /// <returns>True if the user presses OK else false</returns>
        public bool ShowDialog()
        {
            return ShowDialog(IntPtr.Zero);
        }

        /// <summary>
        /// Shows the dialog
        /// </summary>
        /// <param name="hWndOwner">Handle of the control to be parent</param>
        /// <returns>True if the user presses OK else false</returns>
        public bool ShowDialog(IntPtr hWndOwner)
        {
            var fbd = new FolderBrowserDialog
            {
                Description = this.Title,
                SelectedPath = this.InitialDirectory,
                ShowNewFolderButton = false
            };
            if (fbd.ShowDialog(new WindowWrapper(hWndOwner)) != DialogResult.OK)
            {
                return false;
            }

            ofd.FileName = fbd.SelectedPath;

            return true;
        }

        #endregion
    }

    /// <summary>
    /// Creates IWin32Window around an IntPtr
    /// </summary>
    public class WindowWrapper : IWin32Window
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="handle">Handle to wrap</param>
        public WindowWrapper(IntPtr handle)
        {
            Handle = handle;
        }

        /// <summary>
        /// Original ptr
        /// </summary>
        public IntPtr Handle { get; }
    }
}
