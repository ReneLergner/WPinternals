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
using System.Threading.Tasks;

namespace WPinternals
{
    internal static class TestCode
    {
        internal static async Task Test(System.Threading.SynchronizationContext UIContext)
        {
            // To avoid warnings when there is no code here.
            await Task.Run(() => { });

            // PhoneNotifierViewModel Notifier = new PhoneNotifierViewModel();
            // Notifier.Start();
            // await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_MassStorage);
            // MassStorage MassStorage = (MassStorage)Notifier.CurrentModel;
        }

        internal static async Task TestProgrammer(System.Threading.SynchronizationContext UIContext, string ProgrammerPath)
        {
            LogFile.BeginAction("TestProgrammer");
            try
            {
                LogFile.Log("Starting Firehose Test", LogType.FileAndConsole);

                PhoneNotifierViewModel Notifier = new PhoneNotifierViewModel();
                UIContext.Send(s => Notifier.Start(), null);
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                    LogFile.Log("Phone found in emergency mode", LogType.FileAndConsole);
                else
                {
                    LogFile.Log("Phone needs to be switched to emergency mode.", LogType.FileAndConsole);
                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                    PhoneInfo Info = ((NokiaFlashModel)Notifier.CurrentModel).ReadPhoneInfo();
                    Info.Log(LogType.ConsoleOnly);
                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Qualcomm_Download);
                    if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Download)
                        throw new WPinternalsException("Switching mode failed.", "Could not switch the phone to Qualcomm Emergency 9008.");
                    LogFile.Log("Phone is in emergency mode.", LogType.FileAndConsole);
                }

                // Send and start programmer
                QualcommSerial Serial = (QualcommSerial)Notifier.CurrentModel;
                QualcommSahara Sahara = new QualcommSahara(Serial);

                if (await Sahara.Reset(ProgrammerPath))
                    LogFile.Log("Emergency programmer test succeeded", LogType.FileAndConsole);
                else
                    LogFile.Log("Emergency programmer test failed", LogType.FileAndConsole);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }
            finally
            {
                LogFile.EndAction("TestProgrammer");
            }
        }
    }
}
