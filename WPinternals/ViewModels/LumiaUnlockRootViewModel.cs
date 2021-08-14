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
    internal class LumiaUnlockRootViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        private readonly Action SwitchToUnlockBoot;
        private readonly Action SwitchToDumpRom;
        private readonly Action SwitchToFlashRom;
        private readonly Action Callback;
        private readonly bool DoUnlock;

        internal LumiaUnlockRootViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action SwitchToDumpRom, Action SwitchToFlashRom, bool DoUnlock, Action Callback)
            : base()
        {
            IsSwitchingInterface = false;
            IsFlashModeOperation = true;

            this.PhoneNotifier = PhoneNotifier;
            this.SwitchToDumpRom = SwitchToDumpRom;
            this.SwitchToFlashRom = SwitchToFlashRom;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.DoUnlock = DoUnlock;
            this.Callback = Callback;
        }

        internal override void EvaluateViewState()
        {
            if (!IsActive)
            {
                return;
            }

            if (DoUnlock)
            {
                if ((SubContextViewModel == null) || (SubContextViewModel is LumiaUndoRootTargetSelectionViewModel))
                {
                    ActivateSubContext(new LumiaUnlockRootTargetSelectionViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToDumpRom, SwitchToFlashRom, DoUnlockPhone, DoUnlockImage));
                }
            }
            else
            {
                if ((SubContextViewModel == null) || (SubContextViewModel is LumiaUnlockRootTargetSelectionViewModel))
                {
                    ActivateSubContext(new LumiaUndoRootTargetSelectionViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToDumpRom, SwitchToFlashRom, DoUnlockPhone, DoUnlockImage));
                }
            }
        }

        internal async void DoUnlockPhone()
        {
            try
            {
                IsSwitchingInterface = true;
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_MassStorage,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                bool HasNewBootloader = HasNewBootloaderFromMassStorage();
                string EFIESPPath = HasNewBootloader ? null : ((MassStorage)PhoneNotifier.CurrentModel).Drive + @"\EFIESP\";
                string MainOSPath = ((MassStorage)PhoneNotifier.CurrentModel).Drive + @"\";

                bool HasV11Patches = HasV11PatchesFromMassStorage();

                StartPatch(EFIESPPath, MainOSPath, HasNewBootloader, HasV11Patches);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, () =>
                {
                    Callback();
                    ActivateSubContext(null);
                }));
            }
        }

        internal void DoUnlockImage(string EFIESPMountPoint, string MainOSMountPoint)
        {
            StartPatch(EFIESPMountPoint, MainOSMountPoint, false, false); // Unlock image is only supported for Lumia's with bootloader Spec A. Due to complexity of Spec B bootloader hack, it cannot be applied on a mounted image.
        }

        // Magic!
        // Apply patches for Root Access
        private void StartPatch(string EFIESP, string MainOS, bool HasNewBootloader, bool HasV11Patches)
        {
            IsSwitchingInterface = false;
            new Thread(() =>
                {
                    if (DoUnlock)
                    {
                        LogFile.BeginAction("EnableRootAccess");
                    }
                    else
                    {
                        LogFile.BeginAction("DisableRootAccess");
                    }

                    bool Result = false;

                    if (EFIESP != null && !HasV11Patches)
                    {
                        if (DoUnlock)
                        {
                            ActivateSubContext(new BusyViewModel("Enable Root Access on EFIESP..."));
                        }
                        else
                        {
                            ActivateSubContext(new BusyViewModel("Disable Root Access on EFIESP..."));
                        }

                        try
                        {
                            App.PatchEngine.TargetPath = EFIESP;
                            if (DoUnlock)
                            {
                                Result = App.PatchEngine.Patch("SecureBootHack-V1-EFIESP");

                                if (!Result)
                                {
                                    ActivateSubContext(new MessageViewModel("Failed to enable Root Access on EFIESP! Check the OS version on the phone and verify the compatibility-list in the \"Getting started\" section.", Exit));
                                    return;
                                }
                            }
                            else
                            {
                                App.PatchEngine.Restore("SecureBootHack-V1-EFIESP");
                                Result = true;
                            }
                        }
                        catch (UnauthorizedAccessException Ex)
                        {
                            LogFile.LogException(Ex);
                            ActivateSubContext(new MessageViewModel("Failed to enable Root Access on EFIESP! Not enough privileges to perform action. Try to logon to Windows with an administrator account.", Exit));
                            return;
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }

                        if (!Result)
                        {
                            ActivateSubContext(new MessageViewModel("Failed to enable Root Access on EFIESP!", Exit));
                            return;
                        }
                    }

                    if (MainOS != null)
                    {
                        if (DoUnlock)
                        {
                            ActivateSubContext(new BusyViewModel("Enable Root Access on MainOS..."));
                        }
                        else
                        {
                            ActivateSubContext(new BusyViewModel("Disable Root Access on MainOS..."));
                        }

                        try
                        {
                            App.PatchEngine.TargetPath = MainOS;
                            if (DoUnlock)
                            {
                                Result = App.PatchEngine.Patch("RootAccess-MainOS");

                                if (Result)
                                {
                                    Result = App.PatchEngine.Patch("SecureBootHack-MainOS");
                                }

                                if (!Result)
                                {
                                    ActivateSubContext(new MessageViewModel("Failed to enable Root Access on MainOS! Check the OS version on the phone and verify the compatibility-list in the \"Getting started\" section.", Exit));
                                    return;
                                }
                            }
                            else
                            {
                                App.PatchEngine.Restore("RootAccess-MainOS");

                                if (!HasNewBootloader)
                                {
                                    App.PatchEngine.Restore("SecureBootHack-MainOS");
                                }

                                Result = true;
                            }
                        }
                        catch (UnauthorizedAccessException Ex)
                        {
                            LogFile.LogException(Ex);
                            ActivateSubContext(new MessageViewModel("Failed to enable Root Access on MainOS! Not enough privileges to perform action. Try to logon to Windows with an administrator account.", Exit));
                            Result = false;
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }

                        if (!Result)
                        {
                            ActivateSubContext(new MessageViewModel("Failed to enable Root Access on MainOS!", Exit));
                        }
                        else
                        {
                            if (DoUnlock)
                            {
                                ActivateSubContext(new MessageViewModel("Root Access successfully enabled!", Exit));
                            }
                            else
                            {
                                ActivateSubContext(new MessageViewModel("Root Access successfully disabled!", Exit));
                            }
                        }
                    }

                    if (DoUnlock)
                    {
                        LogFile.EndAction("EnableRootAccess");
                    }
                    else
                    {
                        LogFile.EndAction("DisableRootAccess");
                    }
                }).Start();
        }

        private void Exit()
        {
            IsSwitchingInterface = false;
            Callback();
            ActivateSubContext(null);
        }

        private bool HasNewBootloaderFromMassStorage()
        {
            bool Result = false;
            MassStorage Phone = (MassStorage)PhoneNotifier.CurrentModel;
            Phone.OpenVolume(false);
            byte[] GPTBuffer = Phone.ReadSectors(1, 33);
            GPT GPT = new(GPTBuffer);
            Partition Partition = GPT.GetPartition("UEFI");
            byte[] UefiBuffer = Phone.ReadSectors(Partition.FirstSector, Partition.LastSector - Partition.FirstSector + 1);
            UEFI UEFI = new(UefiBuffer);
            string BootMgrName = UEFI.EFIs.First(efi => (efi.Name != null) && (efi.Name.Contains("BootMgrApp") || efi.Name.Contains("FlashApp"))).Name;
            byte[] BootMgr = UEFI.GetFile(BootMgrName);
            // "Header V2"
            Result = ByteOperations.FindAscii(BootMgr, "Header V2") != null;
            Phone.CloseVolume();
            return Result;
        }

        private bool HasV11PatchesFromMassStorage()
        {
            bool Result = false;
            MassStorage Phone = (MassStorage)PhoneNotifier.CurrentModel;
            Phone.OpenVolume(false);
            byte[] GPTBuffer = Phone.ReadSectors(1, 33);
            GPT GPT = new(GPTBuffer);
            Partition Partition = GPT.GetPartition("BACKUP_BS_NV");
            Result = Partition != null;
            Phone.CloseVolume();
            return Result;
        }
    }
}
