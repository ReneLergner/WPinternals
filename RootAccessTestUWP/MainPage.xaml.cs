using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RootAccessTestUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            TextBlock.Text = "Reading: HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\PhoneProvisioner\\ColdBootDone" + Environment.NewLine + Environment.NewLine;

            bool flag = false;
            try
            {
                Core.RegReadDwordValue(Core.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\PhoneProvisioner", "ColdBootDone");
                flag = true;
            }
            catch
            {
            }

            if (flag)
            {
                TextBlock.Text = string.Concat(new string[]
                {
                    TextBlock.Text,
                    "Success!",
                    Environment.NewLine,
                    Environment.NewLine,
                    "Root Access: Enabled"
                });
            }
            else
            {
                TextBlock.Text = string.Concat(new string[]
                {
                    TextBlock.Text,
                    "Failed!",
                    Environment.NewLine,
                    Environment.NewLine,
                    "Root Access: Disabled"
                });
            }
        }
    }
}
