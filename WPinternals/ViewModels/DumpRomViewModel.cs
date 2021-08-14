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
using System.Linq;
using System.Threading;

namespace WPinternals
{
    internal class DumpRomViewModel : ContextViewModel
    {
        private readonly Action SwitchToUnlockBoot;
        private readonly Action SwitchToUnlockRoot;
        private readonly Action SwitchToFlashRom;

        internal DumpRomViewModel(Action SwitchToUnlockBoot, Action SwitchToUnlockRoot, Action SwitchToFlashRom)
            : base()
        {
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.SwitchToUnlockRoot = SwitchToUnlockRoot;
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
                ActivateSubContext(new DumpRomTargetSelectionViewModel(SwitchToUnlockBoot, SwitchToUnlockRoot, SwitchToFlashRom, DoDumpRom));
            }
        }

        internal void DoDumpRom(string FFUPath, string EFIESPPath, bool CompressEFIESP, string MainOSPath, bool CompressMainOS, string DataPath, bool CompressData)
        {
            new Thread(() =>
                {
                    bool Result = true;

                    ActivateSubContext(new BusyViewModel("Initializing ROM dump..."));

                    ulong TotalSizeSectors = 0;
                    int PartitionCount = 0;
                    Partition Partition;
                    FFU FFU = null;
                    try
                    {
                        FFU = new FFU(FFUPath);

                        if (EFIESPPath != null)
                        {
                            Partition = FFU.GPT.Partitions.First(p => p.Name == "EFIESP");
                            TotalSizeSectors += Partition.SizeInSectors;
                            PartitionCount++;
                        }

                        if (MainOSPath != null)
                        {
                            Partition = FFU.GPT.Partitions.First(p => p.Name == "MainOS");
                            TotalSizeSectors += Partition.SizeInSectors;
                            PartitionCount++;
                        }

                        if (DataPath != null)
                        {
                            Partition = FFU.GPT.Partitions.First(p => p.Name == "Data");
                            TotalSizeSectors += Partition.SizeInSectors;
                            PartitionCount++;
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        Result = false;
                    }

                    // We are on a worker thread!
                    // So we must pass the SynchronizationContext of the UI thread
                    BusyViewModel Busy = new("Dumping ROM...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
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
                                Busy.Message = "Dumping partition EFIESP (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                FFU.WritePartition("EFIESP", EFIESPPath, Updater, CompressEFIESP);
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
                                Busy.Message = "Dumping partition MainOS (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                FFU.WritePartition("MainOS", MainOSPath, Updater, CompressMainOS);
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
                                Busy.Message = "Dumping partition Data (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                FFU.WritePartition("Data", DataPath, Updater, CompressData);
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
                        ActivateSubContext(new MessageViewModel("Failed to dump ROM partitions!", Restart));
                        return;
                    }

                    ActivateSubContext(new MessageViewModel("Successfully dumped ROM partitions!", Restart));
                }).Start();
        }

        internal void Restart()
        {
            ActivateSubContext(new DumpRomTargetSelectionViewModel(SwitchToUnlockBoot, SwitchToUnlockRoot, SwitchToFlashRom, DoDumpRom));
        }
    }
}
