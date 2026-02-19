/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Runtime.InteropServices;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Offers access to the host information
	/// </summary>
	public static class Host {

		/// <summary>
		/// Returns if the script is running with administrator privileges
		/// </summary>
		/// <returns>True if we're running under administrator privileges. False otherwise.</returns>
		public static bool IsRoot() {
			return Environment.IsPrivilegedProcess;
		}

		/// <summary>
		/// Returns if the script is being executed in an interactive shell
		/// </summary>
		/// <returns>True if we're on an interactive shell. False otherwise</returns>
		public static bool IsInteractive() {
			return Environment.UserInteractive;
		}

		/// <summary>
		/// Returns a string with the kindly name of the os
		/// </summary>
		/// <returns>The os string</returns>
		public static string GetOSKind() {
			if (IsWindows())
				return "win";
			if (IsLinux())
				return "lnx";
			if (IsMacOS())
				return "osx";
			return string.Empty;
		}

		/// <summary>
		/// Returns if the script is being executed in a Windows environment
		/// </summary>
		/// <returns>True if we're on windows, false otherwise</returns>
		public static bool IsWindows() {
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
		}

		/// <summary>
		/// Returns if the script is being executed in a Linux environment
		/// </summary>
		/// <returns>True if we're on linux, false otherwise.</returns>
		public static bool IsLinux() {
			return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
		}

		/// <summary>
		/// Returns if the script is being executed in a macos environement
		/// </summary>
		/// <returns>True if we're on osx, false otherwise.</returns>
		public static bool IsMacOS() {
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
		}

		/// <summary>
		/// Returns the number of CPU cores available
		/// </summary>
		/// <returns>The number of cores.</returns>
		public static int ProcessorCount() {
			return Environment.ProcessorCount;
		}
	}
}
