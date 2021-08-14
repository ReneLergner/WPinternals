using System;
using System.Runtime.InteropServices;

namespace RootAccessTestUWP
{
	public static class Core
	{
		[DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern int RegQueryValueEx(UIntPtr hKey, string lpValueName, int lpReserved, out Core.RegistryValueKind lpType, IntPtr lpData, ref int lpcbData);

		[DllImport("KERNELBASE.DLL", CharSet = CharSet.Unicode)]
		private static extern int RegOpenKeyEx(UIntPtr hKey, string subKey, int ulOptions, int samDesired, out UIntPtr hkResult);

		[DllImport("KERNELBASE.DLL", SetLastError = true)]
		private static extern int RegCloseKey(UIntPtr hKey);

		internal static string RegReadStringValue(UIntPtr rootKey, string keyPath, string valueName)
		{
			UIntPtr hKey;
			int num = Core.RegOpenKeyEx(rootKey, keyPath, 0, Core.KEY_READ, out hKey);
			if (num != 0)
			{
				throw new COMException("Failed to open registry key", num);
			}
			int cb = 1024;
			IntPtr intPtr = Marshal.AllocHGlobal(cb);
			Core.RegistryValueKind registryValueKind;
			num = Core.RegQueryValueEx(hKey, valueName, 0, out registryValueKind, intPtr, ref cb);
			if (num != 0)
			{
				throw new COMException("Failed to read registry value", num);
			}
			if (registryValueKind != Core.RegistryValueKind.String)
			{
				throw new Exception("Wrong registry value type");
			}
			string result = Marshal.PtrToStringUni(intPtr);
			Core.RegCloseKey(hKey);
			return result;
		}

		internal static uint RegReadDwordValue(UIntPtr rootKey, string keyPath, string valueName)
		{
			UIntPtr hKey;
			int num = Core.RegOpenKeyEx(rootKey, keyPath, 0, Core.KEY_READ, out hKey);
			if (num != 0)
			{
				throw new COMException("Failed to open registry key", num);
			}
			int cb = 1024;
			IntPtr intPtr = Marshal.AllocHGlobal(cb);
			Core.RegistryValueKind registryValueKind;
			num = Core.RegQueryValueEx(hKey, valueName, 0, out registryValueKind, intPtr, ref cb);
			if (num != 0)
			{
				throw new COMException("Failed to read registry value", num);
			}
			if (registryValueKind != Core.RegistryValueKind.DWord)
			{
				throw new Exception("Wrong registry value type");
			}
			uint result = (uint)Marshal.ReadInt32(intPtr);
			Core.RegCloseKey(hKey);
			return result;
		}

		public static UIntPtr HKEY_CLASSES_ROOT = (UIntPtr)2147483648U;

		public static UIntPtr HKEY_CURRENT_USER = (UIntPtr)2147483649U;

		public static UIntPtr HKEY_LOCAL_MACHINE = (UIntPtr)2147483650U;

		public static UIntPtr HKEY_USERS = (UIntPtr)2147483651U;

		private static int KEY_READ = 131097;

		public enum RegistryValueKind
		{
			Unknown,
			String,
			ExpandString,
			Binary,
			DWord,
			MultiString = 7,
			QWord = 11,
			None = -1
		}
	}
}
