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
using System.IO;
using System.Threading;

namespace WPinternals
{
    internal class RestoreViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        private readonly Action Callback;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;
        private readonly Action SwitchToUnlockBoot;
        private readonly Action SwitchToFlashRom;

        internal RestoreViewModel(PhoneNotifierViewModel PhoneNotifier, Action<PhoneInterfaces> RequestModeSwitch, Action SwitchToUnlockBoot, Action SwitchToFlashRom, Action Callback)
            : base()
        {
            IsSwitchingInterface = true;

            this.PhoneNotifier = PhoneNotifier;
            this.Callback = Callback;
            this.RequestModeSwitch = RequestModeSwitch;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.SwitchToFlashRom = SwitchToFlashRom;
        }

        internal override void EvaluateViewState()
        {
            if (!IsActive)
            {
                return;
            }

            if (SubContextViewModel == null)
            {
                ActivateSubContext(new RestoreSourceSelectionViewModel(PhoneNotifier, RequestModeSwitch, SwitchToUnlockBoot, SwitchToFlashRom, DoRestore));
            }

            if (SubContextViewModel is RestoreSourceSelectionViewModel)
            {
                ((RestoreSourceSelectionViewModel)SubContextViewModel).EvaluateViewState();
            }
        }

        internal async void DoRestore(string EFIESPPath, string MainOSPath, string DataPath)
        {
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Flash,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                RestoreTask(EFIESPPath, MainOSPath, DataPath);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal void RestoreTask(string EFIESPPath, string MainOSPath, string DataPath)
        {
            new Thread(() =>
                {
                    bool Result = true;

                    ActivateSubContext(new BusyViewModel("Initializing restore..."));

                    ulong TotalSizeSectors = 0;
                    int PartitionCount = 0;
                    try
                    {
                        if (EFIESPPath != null)
                        {
                            TotalSizeSectors += (ulong)new FileInfo(EFIESPPath).Length / 0x200;
                            PartitionCount++;
                        }

                        if (MainOSPath != null)
                        {
                            TotalSizeSectors += (ulong)new FileInfo(MainOSPath).Length / 0x200;
                            PartitionCount++;
                        }

                        if (DataPath != null)
                        {
                            TotalSizeSectors += (ulong)new FileInfo(DataPath).Length / 0x200;
                            PartitionCount++;
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        Result = false;
                    }

                    LumiaFlashAppModel Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

                    BusyViewModel Busy = new("Restoring...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
                    ProgressUpdater Updater = Busy.ProgressUpdater;
                    ActivateSubContext(Busy);

                    int i = 0;
                    if (Result)
                    {
                        try
                        {
                            if (EFIESPPath != null)
                            {
                                i++;
                                Busy.Message = "Restoring partition EFIESP (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.FlashRawPartition(EFIESPPath, "EFIESP", Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    if (Result)
                    {
                        try
                        {
                            if (MainOSPath != null)
                            {
                                i++;
                                Busy.Message = "Restoring partition MainOS (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.FlashRawPartition(EFIESPPath, "MainOS", Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    if (Result)
                    {
                        try
                        {
                            if (DataPath != null)
                            {
                                i++;
                                Busy.Message = "Restoring partition Data (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.FlashRawPartition(EFIESPPath, "Data", Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    if (!Result)
                    {
                        ActivateSubContext(new MessageViewModel("Failed to restore!", Exit));
                        return;
                    }

                    ActivateSubContext(new MessageViewModel("Successfully restored!", Exit));
                }).Start();
        }

        internal async void Exit()
        {
            IsSwitchingInterface = false;
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Normal,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                Callback();
                ActivateSubContext(null);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }
    }
}
