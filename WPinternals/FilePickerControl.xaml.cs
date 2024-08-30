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
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace WPinternals
{
    public class PathChangedEventArgs : EventArgs
    {
        public PathChangedEventArgs(string NewPath)
            : base()
        {
            this.NewPath = NewPath;
        }

        public string NewPath;
    }

    public delegate void PathChangedEventHandler(
        Object sender,
        PathChangedEventArgs e
    );

    /// <summary>
    /// Interaction logic for FilePickerControl.xaml
    /// </summary>
    public partial class FilePickerBase : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private readonly SynchronizationContext UIContext;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public event EventHandler<PathChangedEventArgs> PathChanged = delegate { };

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                if (SynchronizationContext.Current == UIContext)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
                else
                {
                    UIContext.Post((s) => PropertyChanged(this, new PropertyChangedEventArgs(propertyName)), null);
                }
            }
        }

        public FilePickerBase()
        {
            UIContext = SynchronizationContext.Current;
            InitializeComponent();
        }

        public bool AllowNull
        {
            get
            {
                return (bool)GetValue(AllowNullProperty);
            }
            set
            {
                SetValue(AllowNullProperty, value);
                Resize();
            }
        }

        public static readonly DependencyProperty AllowNullProperty =
            DependencyProperty.Register("AllowNull", typeof(bool), typeof(FilePickerBase), new UIPropertyMetadata(false));

        public string Caption
        {
            get
            {
                return (string)GetValue(CaptionProperty);
            }
            set
            {
                SetValue(CaptionProperty, value);
                Resize();
            }
        }

        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register("Caption", typeof(string), typeof(FilePickerBase), new UIPropertyMetadata(null));

        public string Path
        {
            get
            {
                return (string)GetValue(PathProperty);
            }
            set
            {
                if ((string)GetValue(PathProperty) != value)
                {
                    SetValue(PathProperty, value);
                    Resize();
                    PathChanged(this, new PathChangedEventArgs(value));
                }
            }
        }

        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register("Path", typeof(string), typeof(FilePickerBase), new UIPropertyMetadata(null));

        public string SelectionText
        {
            get
            {
                return (string)GetValue(SelectionTextProperty);
            }
            set
            {
                SetValue(SelectionTextProperty, value);
                Resize();
            }
        }

        public static readonly DependencyProperty SelectionTextProperty =
            DependencyProperty.Register("SelectionText", typeof(string), typeof(FilePickerBase), new UIPropertyMetadata(""));

        private void SizeChangedHandler(object sender, SizeChangedEventArgs e)
        {
            Resize();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var resultSize = new Size(availableSize.Width, 0);

#if NETCORE
            FormattedText formatted = new FormattedText(
                "TEST",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(PathTextBlock.FontFamily, PathTextBlock.FontStyle, PathTextBlock.FontWeight, PathTextBlock.FontStretch),
                FontSize,
                Foreground,
                100 / 96
            );
#else
            FormattedText formatted = new(
                "TEST",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(PathTextBlock.FontFamily, PathTextBlock.FontStyle, PathTextBlock.FontWeight, PathTextBlock.FontStretch),
                FontSize,
                Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );
#endif

            resultSize.Height = formatted.Height;

            return resultSize;
        }

        private void Resize()
        {
            if (!IsLoaded)
            {
                return;
            }

            CaptionTextBlock.Text = Caption;
#if NETCORE
            FormattedText formatted = new FormattedText(
                CaptionTextBlock.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(CaptionTextBlock.FontFamily, CaptionTextBlock.FontStyle, CaptionTextBlock.FontWeight, CaptionTextBlock.FontStretch),
                FontSize,
                Foreground,
                100 / 96
                );
#else
            FormattedText formatted = new(
                CaptionTextBlock.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(CaptionTextBlock.FontFamily, CaptionTextBlock.FontStyle, CaptionTextBlock.FontWeight, CaptionTextBlock.FontStretch),
                FontSize,
                Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
#endif
            double CaptionWidth = formatted.Width;
            if (CaptionWidth > 0)
            {
                CaptionWidth += 10;
            }

            bool SelectVisible = Path == null;
            bool ChangeVisible = Path != null;
            bool ClearVisible = (Path != null) && AllowNull;

            double NewWidth = ActualWidth - CaptionWidth;
            if (SelectVisible)
            {
                NewWidth -= SelectLink.ActualWidth;
            }

            if (ChangeVisible)
            {
                NewWidth -= ChangeLink.ActualWidth + 10;
            }

            if (ClearVisible)
            {
                NewWidth -= ClearLink.ActualWidth + 10;
            }

            SetText(NewWidth);

            // Calculate the new ActualWidth
            // We can't use PathTextBlock.ActualWidth yet, because LayoutUpdated event has not yet been triggered
#if NETCORE
            formatted = new FormattedText(
                        PathTextBlock.Text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(PathTextBlock.FontFamily, PathTextBlock.FontStyle, PathTextBlock.FontWeight, PathTextBlock.FontStretch),
                        FontSize,
                        Foreground,
                        100 / 96
                        );
#else
            formatted = new FormattedText(
                        PathTextBlock.Text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(PathTextBlock.FontFamily, PathTextBlock.FontStyle, PathTextBlock.FontWeight, PathTextBlock.FontStretch),
                        FontSize,
                        Foreground,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip
                        );
#endif
            if (NewWidth < 0)
            {
                PathTextBlock.Width = 0;
            }
            else
            {
                PathTextBlock.Width = formatted.Width > NewWidth ? NewWidth : formatted.Width;
            }

            PathTextBlock.Margin = new Thickness(CaptionWidth, 0, 0, 0);
            double Pos = PathTextBlock.Width + CaptionWidth;

            if (SelectVisible)
            {
                SelectLink.Visibility = Visibility.Visible;
                SelectLink.Margin = new Thickness(Pos, 0, 0, 0);
                Pos += SelectLink.ActualWidth;
            }
            else
            {
                SelectLink.Visibility = Visibility.Collapsed;
            }

            if (ChangeVisible)
            {
                ChangeLink.Visibility = Visibility.Visible;
                ChangeLink.Margin = new Thickness(Pos + 10, 0, 0, 0);
                Pos += ChangeLink.ActualWidth + 10;
            }
            else
            {
                ChangeLink.Visibility = Visibility.Collapsed;
            }

            if (ClearVisible)
            {
                ClearLink.Visibility = Visibility.Visible;
                ClearLink.Margin = new Thickness(Pos + 10, 0, 0, 0);
                Pos += ClearLink.ActualWidth + 10;
            }
            else
            {
                ClearLink.Visibility = Visibility.Collapsed;
            }
        }

        private void SetText(double MaxWidth)
        {
            string Text = Path;

            if (System.IO.Path.IsPathRooted(Text))
            {
                // It is a valid path
                string filename = "";
                try
                {
                    filename = System.IO.Path.GetFileName(Text);
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
                string directory = "";
                try
                {
                    directory = System.IO.Path.GetDirectoryName(Text);
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
                FormattedText formatted;
                bool widthOK = false;
                bool changedWidth = false;

                do
                {
#if NETCORE
                    formatted = new FormattedText(
                        "{0}...\\{1}".FormatWith(directory, filename),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(PathTextBlock.FontFamily, PathTextBlock.FontStyle, PathTextBlock.FontWeight, PathTextBlock.FontStretch),
                        FontSize,
                        Foreground,
                        100 / 96
                        );
#else
                    formatted = new FormattedText(
                        "{0}...\\{1}".FormatWith(directory, filename),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(PathTextBlock.FontFamily, PathTextBlock.FontStyle, PathTextBlock.FontWeight, PathTextBlock.FontStretch),
                        FontSize,
                        Foreground,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip
                        );
#endif

                    widthOK = formatted.Width < MaxWidth;

                    if (!widthOK)
                    {
                        changedWidth = true;

                        if (directory.Length > 0)
                        {
                            directory = directory[0..^1];
                        }

                        if (directory.Length == 0)
                        {
                            Text = "...\\" + filename;
                            break;
                        }
                    }

                } while (!widthOK);

                if (changedWidth && (directory.Length > 0))
                {
                    Text = "{0}...\\{1}".FormatWith(directory, filename);
                }
            }

            PathTextBlock.Text = Text;
        }

        private void LoadedHandler(object sender, RoutedEventArgs e)
        {
            Resize();
        }

        private void SelectLink_Click(object sender, RoutedEventArgs e)
        {
            Select();
            Resize();
        }

        private void ChangeLink_Click(object sender, RoutedEventArgs e)
        {
            Select();
            Resize();
        }

        private void ClearLink_Click(object sender, RoutedEventArgs e)
        {
            Path = null;
            Resize();
        }

        protected virtual void Select() { }
    }

    public class FilePicker : FilePickerBase
    {
        public FilePicker()
            : base()
        {
            if (SelectionText?.Length == 0)
            {
                SelectionText = "Select file...";
            }
        }

        protected override void Select()
        {
            bool? result;

            if (SaveDialog)
            {
                Microsoft.Win32.SaveFileDialog savedlg = new();

                savedlg.FileName = Path ?? DefaultFileName;

                // Show open file dialog box
                result = savedlg.ShowDialog();

                // Process open file dialog box results 
                if (result == true)
                {
                    // Open document 
                    Path = savedlg.FileName;
                }
            }
            else
            {
                // Select file
                Microsoft.Win32.OpenFileDialog dlg = new();

                dlg.FileName = Path ?? DefaultFileName;

                // Show open file dialog box
                result = dlg.ShowDialog();

                // Process open file dialog box results 
                if (result == true)
                {
                    // Open document 
                    Path = dlg.FileName;
                }
            }
        }

        public bool SaveDialog
        {
            get
            {
                return (bool)GetValue(SaveDialogProperty);
            }
            set
            {
                SetValue(SaveDialogProperty, value);
            }
        }

        public static readonly DependencyProperty SaveDialogProperty =
            DependencyProperty.Register("SaveDialog", typeof(bool), typeof(FilePicker), new UIPropertyMetadata(false));

        public string DefaultFileName
        {
            get
            {
                return (string)GetValue(DefaultFileNameProperty);
            }
            set
            {
                SetValue(DefaultFileNameProperty, value);
            }
        }

        public static readonly DependencyProperty DefaultFileNameProperty =
            DependencyProperty.Register("DefaultFileName", typeof(string), typeof(FilePicker), new UIPropertyMetadata(""));
    }

    public class FolderPicker : FilePickerBase
    {
        public FolderPicker()
            : base()
        {
            if (SelectionText?.Length == 0)
            {
                SelectionText = "Select folder...";
            }
        }

        protected override void Select()
        {
            // Select folder

            FolderSelectDialog dlg = new();
            if (Path != null)
            {
                dlg.InitialDirectory = Path;
            }

            if (dlg.ShowDialog())
            {
                Path = dlg.FileName;
            }
        }
    }

    internal static class Extensions
    {
        public static string FormatWith(this string s, params object[] args)
        {
            return string.Format(s, args);
        }
    }
}
