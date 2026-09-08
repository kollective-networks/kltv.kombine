/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;

namespace Kltv.Kombine.Api {
	/// <summary>
	/// All the file related functionality.
	/// Failures are reported through the return value and LastError; nothing is printed at normal level.
	/// </summary>
	public static class Files {

		/// <summary>
		/// Last failure of a Files call. Reset at the start of every call, set when it fails.
		/// Exists never sets it: false is the answer.
		/// </summary>
		public static ApiError LastError { get; private set; } = ApiError.None;

		/// <summary>
		/// Records a failure and logs it at verbose level.
		/// </summary>
		private static bool Fail(ErrorCode code, string message, string source) {
			LastError = new ApiError(code, message, source);
			Msg.PrintWarningMod(LastError.ToString(), ".files", Msg.LogLevels.Verbose);
			return false;
		}

		/// <summary>
		/// Records a failure from an exception and logs it at verbose level.
		/// </summary>
		private static bool Fail(Exception ex, string source) {
			LastError = ApiError.From(ex, source);
			Msg.PrintWarningMod(LastError.ToString(), ".files", Msg.LogLevels.Verbose);
			return false;
		}

		/// <summary>
		/// Check if the file exists
		/// </summary>
		/// <param name="Filename">Filename to check</param>
		/// <returns>True if exists, false otherwise</returns>
		static public bool Exists(KValue Filename) {
			if (FSAPI.FileExists(Filename))
				return true;
			return false;
		}

		/// <summary>
		/// Read a text file into a single KValue
		/// </summary>
		/// <param name="Filename">Filename to be fetched</param>
		/// <param name="ExitIfError">If the script should exit if the file is missing / trigers error. Default false</param>
		/// <returns>A KValue with the file contents, or empty on failure (see LastError).</returns>
		static public KValue ReadTextFile(KValue Filename,bool ExitIfError = false) {
			LastError = ApiError.None;
			if (string.IsNullOrEmpty(Filename)) {
				if (ExitIfError) {
					Msg.PrintAndAbortMod("The requested filename to read is invalid: "+Filename, ".files");
				}
				Fail(ErrorCode.InvalidArgument, "The filename to read is empty", Filename);
				return string.Empty;
			}
			string content = FSAPI.ReadTextFile(Filename, ExitIfError);
			if (FSAPI.LastError.IsError)
				LastError = FSAPI.LastError;
			return content;
		}

		/// <summary>
		/// Writes a text file with the given content
		/// </summary>
		/// <param name="Filename">Filename to use</param>
		/// <param name="Contents">Contents to be written</param>
		/// <param name="ExitIfError">If it should automaticallty quit on error, default false.</param>
		/// <returns>Returns true if okey, false otherwise (see LastError).</returns>
		static public bool WriteTextFile(KValue Filename, KValue Contents,bool ExitIfError = false) {
			LastError = ApiError.None;
			if (string.IsNullOrEmpty(Filename)) {
				if (ExitIfError) {
					Msg.PrintAndAbortMod("The requested filename to write is invalid: "+Filename, ".files");
				}
				return Fail(ErrorCode.InvalidArgument, "The filename to write is empty", Filename);
			}
			if (FSAPI.WriteTextFile(Filename, Contents, ExitIfError))
				return true;
			LastError = FSAPI.LastError;
			return false;
		}


		/// <summary>
		/// Returns the modification time of the given file
		/// </summary>
		/// <param name="Filename">File to inspect</param>
		/// <returns>The modified time in UTC zone in seconds, or 0 for a missing file (see LastError).</returns>
		static public long GetModifiedTime(KValue Filename) {
			LastError = ApiError.None;
			long time = FSAPI.GetModifiedTimeUTC(Filename);
			if (FSAPI.LastError.IsError)
				LastError = FSAPI.LastError;
			return time;
		}

		/// <summary>
		/// Renames a file.
		/// </summary>
		/// <param name="oldFilename">The current name of the file.</param>
		/// <param name="newFilename">The new name for the file.</param>
		/// <returns>True if the file was successfully renamed, false otherwise (see LastError).</returns>
		static public bool Rename(KValue oldFilename, KValue newFilename) {
			LastError = ApiError.None;
			if (!FSAPI.FileExists(oldFilename))
				return Fail(ErrorCode.NotFound, "The file to rename does not exist", oldFilename);
			if (FSAPI.FileExists(newFilename))
				return Fail(ErrorCode.AlreadyExists, "The destination file already exists", newFilename);
			try {
				File.Move(oldFilename, newFilename);
				return true;
			} catch (Exception ex) {
				return Fail(ex, oldFilename);
			}
		}

		/// <summary>
		/// Move a file from one location to another.
		/// </summary>
		/// <param name="oldFilename">Current filename.</param>
		/// <param name="newFilename">New filename and location.</param>
		/// <returns>True if okey, false othewise (see LastError).</returns>
		static public bool Move(KValue oldFilename, KValue newFilename) {
			return Rename(oldFilename, newFilename);
		}

		/// <summary>
		/// Deletes a file.
		/// </summary>
		/// <param name="Filename">File to be deleted.</param>
		/// <returns>True if okey, false otherwise (see LastError).</returns>
		static public bool Delete(KValue Filename) {
			LastError = ApiError.None;
			if (!FSAPI.FileExists(Filename))
				return Fail(ErrorCode.NotFound, "The file to delete does not exist", Filename);
			try {
				File.Delete(Filename);
				return true;
			} catch (Exception ex) {
				return Fail(ex, Filename);
			}
		}

		/// <summary>
		/// Returns the size of a given file.
		/// </summary>
		/// <param name="Filename">Filename.</param>
		/// <returns>The filesize or -1 if invalid (see LastError).</returns>
		static public long GetFileSize(KValue Filename) {
			LastError = ApiError.None;
			long size = FSAPI.GetFileSize(Filename);
			if (FSAPI.LastError.IsError)
				LastError = FSAPI.LastError;
			return size;
		}

		/// <summary>
		/// Copy a file.
		/// </summary>
		/// <param name="source">Source file.</param>
		/// <param name="destination">Destination file.</param>
		/// <param name="newerOnly">Copy only if source is newer, default true.</param>
		/// <returns>true if the copy was okey or the file on destination is newer, false otherwise (see LastError).</returns>
		static public bool Copy(KValue source, KValue destination,bool newerOnly = true) {
			LastError = ApiError.None;
			if (!FSAPI.FileExists(source))
				return Fail(ErrorCode.NotFound, "The file to copy does not exist", source);
			try {
				if (newerOnly) {
					if (FSAPI.FileExists(destination)) {
						if (FSAPI.GetModifiedTimeUTC(source) <= FSAPI.GetModifiedTimeUTC(destination)) {
							Msg.PrintMod("The file to copy is older than the destination: " + source, ".files", Msg.LogLevels.Verbose);
							return true;
						}
					}
				}
				// Overwrite the destination: incremental copies must be able to update older files
				File.Copy(source, destination, true);
				return true;
			} catch (Exception ex) {
				return Fail(ex, source);
			}
		}


		/// <summary>
		/// File Comparing Options
		/// </summary>
		public enum CompareOptions  {
			CompareSize = 0x00000000,
			CompareTime = 0x00000001,
			CompareContents = 0x00000002
		}

		/// <summary>
		/// Compare two files using the comparisson specified in Options.
		/// When the files differ, LastError says what differed (Different); when one of them is
		/// missing, LastError says which one (NotFound).
		/// </summary>
		/// <param name="first">First file to compare</param>
		/// <param name="second">Second file to compare</param>
		/// <param name="Options">Comparing options. By default is compared by size.</param>
		/// <returns>True if both files are equal. False otherwise (see LastError)</returns>
		static public bool Compare(KValue first,KValue second,CompareOptions Options = CompareOptions.CompareSize) {
			LastError = ApiError.None;
			if (!FSAPI.FileExists(first))
				return Fail(ErrorCode.NotFound, "The file to compare does not exist", first);
			if (!FSAPI.FileExists(second))
				return Fail(ErrorCode.NotFound, "The file to compare does not exist", second);
			try {
				//
				// Size comparisson (always executed)
				FileInfo fsource = new FileInfo(first);
				FileInfo fdest = new FileInfo(second);
				if (fsource.Length != fdest.Length)
					return Fail(ErrorCode.Different, "The sizes differ (" + fsource.Length + " and " + fdest.Length + " bytes)", first + " | " + second);
				//
				// Modified time comparisson.
				if (Options.HasFlag(CompareOptions.CompareTime)) {
					if (fsource.LastWriteTime.Equals(fdest.LastWriteTime) == false)
						return Fail(ErrorCode.Different, "The modified times differ", first + " | " + second);
				}
				//
				// Content comparisson.
				if (Options.HasFlag(CompareOptions.CompareContents)) {
					const int BYTES_TO_READ = sizeof(Int64);
					int iterations = (int)Math.Ceiling((double)fsource.Length / BYTES_TO_READ);
					using (FileStream fs1 = fsource.OpenRead())
					using (FileStream fs2 = fdest.OpenRead()) {
						byte[] one = new byte[BYTES_TO_READ];
						byte[] two = new byte[BYTES_TO_READ];

						for (int i = 0; i < iterations; i++) {
							// ReadAtLeast returns the real byte count (a stream read may return fewer bytes
							// than requested); compare only the bytes actually read so a final short block
							// cannot match on stale buffer contents.
							int read1 = fs1.ReadAtLeast(one, BYTES_TO_READ, throwOnEndOfStream: false);
							int read2 = fs2.ReadAtLeast(two, BYTES_TO_READ, throwOnEndOfStream: false);
							if (read1 != read2 || !one.AsSpan(0, read1).SequenceEqual(two.AsSpan(0, read2)))
								return Fail(ErrorCode.Different, "The contents differ", first + " | " + second);
						}
					}
				}
				return true;
			} catch (Exception ex) {
				return Fail(ex, first + " | " + second);
			}
		}

	}
}
