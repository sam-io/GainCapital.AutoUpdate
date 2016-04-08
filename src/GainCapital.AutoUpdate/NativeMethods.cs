using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate
{
	public static class NativeMethods
	{
		public static string ResolvePath(string path)
		{
			var hFile = CreateFileW(path, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open,
				FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
			if (hFile == INVALID_HANDLE_VALUE)
				throw new Win32Exception();

			try
			{
				var buf = new StringBuilder(8 * 1024);
				var res = GetFinalPathNameByHandle(hFile, buf, 1024, 0);
				if (res == 0)
					throw new Win32Exception();

				return buf.ToString();
			}
			finally
			{
				CloseHandle(hFile);
			}
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		static extern IntPtr CreateFileW(
			[MarshalAs(UnmanagedType.LPWStr)] string filename,
			[MarshalAs(UnmanagedType.U4)] FileAccess access,
			[MarshalAs(UnmanagedType.U4)] FileShare share,
			IntPtr securityAttributes,
			[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
			[MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
			IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath,
			uint cchFilePath, uint dwFlags);

		private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
		private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;
	}
}
