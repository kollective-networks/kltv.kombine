/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Tlotb.Kombine {
	public static partial class NativeMethods {

		public static readonly IntPtr INVALID_HANDLE = new IntPtr(-1);
		public const FileAttributes FILE_FLAG_BACKUP_SEMANTICS = (FileAttributes)0x02000000;

		public enum NTSTATUS : uint {
			SUCCESS = 0x00000000,
			NOT_IMPLEMENTED = 0xC0000002,
			INVALID_INFO_CLASS = 0xC0000003,
			INVALID_PARAMETER = 0xC000000D,
			NOT_SUPPORTED = 0xC00000BB,
			DIRECTORY_NOT_EMPTY = 0xC0000101,
			ACCESS_DENIED = 0xC0000022,
		}

		public enum FILE_INFORMATION_CLASS {
			None = 0,
			// Note: If you use the actual enum in here, remember to
			// start the first field at 1. There is nothing at zero.
			FileCaseSensitiveInformation = 71,
		}

		// It's called Flags in FileCaseSensitiveInformation so treat it as flags
		[Flags]
		public enum CASE_SENSITIVITY_FLAGS : uint {
			CaseInsensitiveDirectory = 0x00000000,
			CaseSensitiveDirectory = 0x00000001,
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IO_STATUS_BLOCK {
			[MarshalAs(UnmanagedType.U4)]
			public NTSTATUS Status;
			public ulong Information;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct FILE_CASE_SENSITIVE_INFORMATION {
			[MarshalAs(UnmanagedType.U4)]
			public CASE_SENSITIVITY_FLAGS Flags;
		}

		// An override, specifically made for FileCaseSensitiveInformation, no IntPtr necessary.
		[DllImport("ntdll.dll")]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern NTSTATUS NtQueryInformationFile(
			IntPtr FileHandle,
			ref IO_STATUS_BLOCK IoStatusBlock,
			ref FILE_CASE_SENSITIVE_INFORMATION FileInformation,
			int Length,
			FILE_INFORMATION_CLASS FileInformationClass);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern IntPtr CreateFile(
				[MarshalAs(UnmanagedType.LPTStr)] string filename,
				[MarshalAs(UnmanagedType.U4)] FileAccess access,
				[MarshalAs(UnmanagedType.U4)] FileShare share,
				IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
				[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
				[MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
				IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CloseHandle(IntPtr hObject);
		public static bool IsDirectoryCaseSensitive(string directory, bool throwOnError = true) {
			// Read access is NOT required
			IntPtr hFile = CreateFile(directory, 0, FileShare.ReadWrite,
										IntPtr.Zero, FileMode.Open,
										FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
			if (hFile == INVALID_HANDLE)
				throw new Win32Exception();
			try {
				IO_STATUS_BLOCK iosb = new IO_STATUS_BLOCK();
				FILE_CASE_SENSITIVE_INFORMATION caseSensitive = new FILE_CASE_SENSITIVE_INFORMATION();
				NTSTATUS status = NtQueryInformationFile(hFile, ref iosb, ref caseSensitive,
															Marshal.SizeOf<FILE_CASE_SENSITIVE_INFORMATION>(),
															FILE_INFORMATION_CLASS.FileCaseSensitiveInformation);
				switch (status) {
					case NTSTATUS.SUCCESS:
						return caseSensitive.Flags.HasFlag(CASE_SENSITIVITY_FLAGS.CaseSensitiveDirectory);

					case NTSTATUS.NOT_IMPLEMENTED:
					case NTSTATUS.NOT_SUPPORTED:
					case NTSTATUS.INVALID_INFO_CLASS:
					case NTSTATUS.INVALID_PARAMETER:
						// Not supported, must be older version of windows.
						// Directory case sensitivity is impossible.
						return false;
					default:
						throw new Exception($"Unknown NTSTATUS: {(uint)status:X8}!");
				}
			} finally {
				CloseHandle(hFile);
			}
		}

		public const FileAccess FILE_WRITE_ATTRIBUTES = (FileAccess)0x00000100;

		// An override, specifically made for FileCaseSensitiveInformation, no IntPtr necessary.
		[DllImport("ntdll.dll")]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern NTSTATUS NtSetInformationFile(
			IntPtr FileHandle,
			ref IO_STATUS_BLOCK IoStatusBlock,
			ref FILE_CASE_SENSITIVE_INFORMATION FileInformation,
			int Length,
			FILE_INFORMATION_CLASS FileInformationClass);

		// Require's elevated priviledges
		public static void SetDirectoryCaseSensitive(string directory, bool enable) {
			// FILE_WRITE_ATTRIBUTES access is the only requirement
			IntPtr hFile = CreateFile(directory, FILE_WRITE_ATTRIBUTES, FileShare.ReadWrite,
										IntPtr.Zero, FileMode.Open,
										FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
			if (hFile == INVALID_HANDLE)
				throw new Win32Exception();
			try {
				IO_STATUS_BLOCK iosb = new IO_STATUS_BLOCK();
				FILE_CASE_SENSITIVE_INFORMATION caseSensitive = new FILE_CASE_SENSITIVE_INFORMATION();
				if (enable)
					caseSensitive.Flags |= CASE_SENSITIVITY_FLAGS.CaseSensitiveDirectory;
				NTSTATUS status = NtSetInformationFile(hFile, ref iosb, ref caseSensitive,
														Marshal.SizeOf<FILE_CASE_SENSITIVE_INFORMATION>(),
														FILE_INFORMATION_CLASS.FileCaseSensitiveInformation);
				switch (status) {
					case NTSTATUS.SUCCESS:
						return;
					case NTSTATUS.DIRECTORY_NOT_EMPTY:
						throw new IOException($"Directory \"{directory}\" contains matching " +
											  $"case-insensitive files!");

					case NTSTATUS.NOT_IMPLEMENTED:
					case NTSTATUS.NOT_SUPPORTED:
					case NTSTATUS.INVALID_INFO_CLASS:
					case NTSTATUS.INVALID_PARAMETER:
						// Not supported, must be older version of windows.
						// Directory case sensitivity is impossible.
						throw new NotSupportedException("This version of Windows does not support directory case sensitivity!");
					case NTSTATUS.ACCESS_DENIED:
						throw new NotSupportedException("You need admin rights to change folder case sensitive.!");
					default:
						throw new Exception($"Unknown NTSTATUS: {(uint)status:X8}!");
				}
			} finally {
				CloseHandle(hFile);
			}
		}

		private static readonly string TempDirectory =
		  Path.Combine(Path.GetTempPath(), "88DEB13C-E516-46C3-97CA-46A8D0DDD8B2");

		private static bool? isSupported;
		public static bool IsDirectoryCaseSensitivitySupported() {
			if (isSupported.HasValue)
				return isSupported.Value;

			// Make sure the directory exists
			if (!Directory.Exists(TempDirectory))
				Directory.CreateDirectory(TempDirectory);

			IntPtr hFile = CreateFile(TempDirectory, 0, FileShare.ReadWrite,
									IntPtr.Zero, FileMode.Open,
									FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
			if (hFile == INVALID_HANDLE)
				throw new Exception("Failed to open file while checking case sensitivity support!");
			try {
				IO_STATUS_BLOCK iosb = new IO_STATUS_BLOCK();
				FILE_CASE_SENSITIVE_INFORMATION caseSensitive = new FILE_CASE_SENSITIVE_INFORMATION();
				// Strangely enough, this doesn't fail on files
				NTSTATUS result = NtQueryInformationFile(hFile, ref iosb, ref caseSensitive,
															Marshal.SizeOf<FILE_CASE_SENSITIVE_INFORMATION>(),
															FILE_INFORMATION_CLASS.FileCaseSensitiveInformation);
				switch (result) {
					case NTSTATUS.SUCCESS:
						return (isSupported = true).Value;
					case NTSTATUS.NOT_IMPLEMENTED:
					case NTSTATUS.INVALID_INFO_CLASS:
					case NTSTATUS.INVALID_PARAMETER:
					case NTSTATUS.NOT_SUPPORTED:
						// Not supported, must be older version of windows.
						// Directory case sensitivity is impossible.
						return (isSupported = false).Value;
					default:
						throw new Exception($"Unknown NTSTATUS {(uint)result:X8} while checking case sensitivity support!");
				}
			} finally {
				CloseHandle(hFile);
				try {
					// CHOOSE: If you delete the folder, future calls to this will not be any faster
					// Directory.Delete(TempDirectory);
				} catch { }
			}
		}

	}
}