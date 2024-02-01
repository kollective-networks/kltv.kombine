/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;

namespace Kltv.Kombine.Api {
	/// <summary>
	/// All the file related functionality
	/// </summary>
	public static class Files {
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
		/// <returns>A KValue with the file contents</returns>
		static public KValue ReadTextFile(KValue Filename,bool ExitIfError = false) {
			if (string.IsNullOrEmpty(Filename)) {
				if (ExitIfError) {
					Msg.PrintAndAbortMod("The requested filename to read is invalid: "+Filename, ".files");
				}
				Msg.PrintWarningMod("The requested filename to read is invalid", ".files",Msg.LogLevels.Verbose);
				return string.Empty;
			}
			return FSAPI.ReadTextFile(Filename, ExitIfError);
		}

		/// <summary>
		/// Writes a text file with the given content
		/// </summary>
		/// <param name="Filename">Filename to use</param>
		/// <param name="Contents">Contents to be written</param>
		/// <param name="ExitIfError">If it should automaticallty quit on error, default false.</param>
		/// <returns>Returns true if okey, false otherwise.</returns>
		static public bool WriteTextFile(KValue Filename, KValue Contents,bool ExitIfError = false) {
			if (string.IsNullOrEmpty(Filename)) {
				Msg.PrintWarningMod("The requested filename to write is invalid:"+Filename, ".files",Msg.LogLevels.Verbose);
				return false;
			}
			return FSAPI.WriteTextFile(Filename, Contents,ExitIfError);
		}


		/// <summary>
		/// Returns the modification time of the given file
		/// </summary>
		/// <param name="Filename">File to inspect</param>
		/// <returns>The modified time in UTC zone in seconds.</returns>
		static public long GetModifiedTime(KValue Filename) {
			return FSAPI.GetModifiedTimeUTC(Filename);
		}

		/// <summary>
		/// Renames a file.
		/// </summary>
		/// <param name="oldFilename">The current name of the file.</param>
		/// <param name="newFilename">The new name for the file.</param>
		/// <returns>True if the file was successfully renamed, false otherwise.</returns>
		static public bool Rename(KValue oldFilename, KValue newFilename) {
			try {
				if (FSAPI.FileExists(oldFilename)) {
					File.Move(oldFilename, newFilename);
					return true;
				} else {
					Msg.PrintMod("The file to rename doesn't exist.", ".files", Msg.LogLevels.Verbose);
					Msg.PrintMod("File: " + oldFilename, ".files", Msg.LogLevels.Verbose);
					return false;
				}
			} catch (Exception ex) {
				Msg.PrintMod("An error occurred while renaming the file.", ".files", Msg.LogLevels.Verbose);
				Msg.PrintMod("Error: " + ex.Message, ".files", Msg.LogLevels.Verbose);
				return false;
			}
		}

		/// <summary>
		/// Move a file from one location to another.
		/// </summary>
		/// <param name="oldFilename">Current filename.</param>
		/// <param name="newFilename">New filename and location.</param>
		/// <returns>True if okey, false othewise</returns>
		static public bool Move(KValue oldFilename, KValue newFilename) {
			return Rename(oldFilename, newFilename);
		}

		/// <summary>
		/// Deletes a file.
		/// </summary>
		/// <param name="Filename">File to be deleted.</param>
		/// <returns>True if okey, false otherwise</returns>
		static public bool Delete(KValue Filename) {
			try {
				if (FSAPI.FileExists(Filename)) {
					File.Delete(Filename);
					return true;
				} else {
					Msg.PrintMod("The file to delete doesn't exist.", ".files", Msg.LogLevels.Verbose);
					Msg.PrintMod("File: " + Filename, ".files", Msg.LogLevels.Verbose);
					return false;
				}
			} catch (Exception ex) {
				Msg.PrintMod("An error occurred while deleting the file.", ".files", Msg.LogLevels.Verbose);
				Msg.PrintMod("Error: " + ex.Message, ".files", Msg.LogLevels.Verbose);
				return false;
			}
		}

		/// <summary>
		/// Returns the size of a given file.
		/// </summary>
		/// <param name="Filename">Filename.</param>
		/// <returns>The filesize or -1 if invalid.</returns>
		static public long GetFileSize(KValue Filename) {
			return FSAPI.GetFileSize(Filename);
		}

		/// <summary>
		/// Copy a file.
		/// </summary>
		/// <param name="source">Source file.</param>
		/// <param name="destination">Destination file.</param>
		/// <param name="newerOnly">Copy only if source is newer, default true.</param>
		/// <returns>true if the copy was okey or the file on destination is newer, false otherwise.</returns>
		static public bool Copy(KValue source, KValue destination,bool newerOnly = true) {
			try {
				if (FSAPI.FileExists(source)) {
					if (newerOnly) {
						if (FSAPI.FileExists(destination)) {
							if (FSAPI.GetModifiedTimeUTC(source) <= FSAPI.GetModifiedTimeUTC(destination)) {
								Msg.PrintMod("The file to copy is older than the destination.", ".files", Msg.LogLevels.Verbose);
								Msg.PrintMod("File: " + source, ".files", Msg.LogLevels.Verbose);
								return true;
							}
						}
					}
					File.Copy(source, destination);
					return true;
				} else {
					Msg.PrintMod("The file to copy doesn't exist.", ".files", Msg.LogLevels.Verbose);
					Msg.PrintMod("File: " + source, ".files", Msg.LogLevels.Verbose);
					return false;
				}
			} catch (Exception ex) {
				Msg.PrintMod("An error occurred while copying the file.", ".files", Msg.LogLevels.Verbose);
				Msg.PrintMod("Error: " + ex.Message, ".files", Msg.LogLevels.Verbose);
				return false;
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
		/// Compare two files using the comparisson specified in Options
		/// </summary>
		/// <param name="first">First file to compare</param>
		/// <param name="second">Second file to compare</param>
		/// <param name="Options">Comparing options. By default is compared by size.</param>
		/// <returns>True if both files are equal. False otherwise</returns>
		static public bool Compare(KValue first,KValue second,CompareOptions Options = CompareOptions.CompareSize) {
			if (!FSAPI.FileExists(first) || !FSAPI.FileExists(second)) {
				Msg.PrintMod("[-] One or both files doesn't exists.", ".files",Msg.LogLevels.Verbose);
				Msg.BeginIndent();
				Msg.PrintMod("File:"+first, ".files", Msg.LogLevels.Verbose);
				Msg.PrintMod("File:"+second, ".files", Msg.LogLevels.Verbose);
				Msg.EndIndent();
				return false;
			}
			try {
				//
				// Size comparisson (always executed)
				FileInfo fsource = new FileInfo(first);
				FileInfo fdest = new FileInfo(second);
				if (fsource.Length != fdest.Length) {
					Msg.PrintMod("[-] Files mismatched by size.", ".files", Msg.LogLevels.Debug);
					Msg.BeginIndent();
					Msg.PrintMod("File:" + first, ".files", Msg.LogLevels.Verbose);
					Msg.PrintMod("File:" + second, ".files", Msg.LogLevels.Verbose);
					Msg.EndIndent();
					return false;
				}
				//
				// Modified time comparisson.
				if (Options.HasFlag(CompareOptions.CompareTime)) {
					if (fsource.LastWriteTime.Equals(fdest.LastWriteTime) == false) {
						Msg.PrintMod("[-] Files mismatched by modified time.", ".files", Msg.LogLevels.Verbose);
						Msg.BeginIndent();
						Msg.PrintMod("File:" + first, ".files", Msg.LogLevels.Verbose);
						Msg.PrintMod("File:" + second, ".files", Msg.LogLevels.Verbose);
						Msg.EndIndent();
						return false;
					}
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
							fs1.Read(one, 0, BYTES_TO_READ);
							fs2.Read(two, 0, BYTES_TO_READ);
							if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0)) {
								Msg.PrintMod("[-] Files mismatched by content.", ".files", Msg.LogLevels.Verbose);
								Msg.BeginIndent();
								Msg.PrintMod("File:" + first, ".files", Msg.LogLevels.Verbose);
								Msg.PrintMod("File:" + second, ".files", Msg.LogLevels.Verbose);
								Msg.EndIndent();
								return false;
							}
						}
					}
				}
				return true;
			} catch (Exception ex) {
				Msg.PrintWarningMod("[-] Exception comparing files: Error: "+ex.Message, ".files",Msg.LogLevels.Verbose);
				Msg.BeginIndent();
				Msg.PrintWarningMod("File:" + first, ".files",Msg.LogLevels.Verbose);
				Msg.PrintWarningMod("File:" + second, ".files",Msg.LogLevels.Verbose);
				Msg.EndIndent();
				return false;
			}
		}

	}
}