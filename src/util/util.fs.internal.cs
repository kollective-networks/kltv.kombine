/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	/// <summary>
	/// 
	/// This class implements the calls to FileSystem API wrapping exception handling but also
	/// Native PInvoke calls that may be required to achieve operations in some OS.
	/// 
	/// </summary>
	internal static class FSAPI {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="folder"></param>
		/// <returns></returns>
		internal static bool SetCurrentFolder(string folder) {
			try {
				Directory.SetCurrentDirectory(folder);
			} catch (Exception ex) {
				InterpretException(ex);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Returns the current working folder
		/// </summary>
		/// <returns>The working folder as string</returns>
		internal static string GetCurrentFolder() {
			try {
				return Directory.GetCurrentDirectory();
			} catch (Exception ex) {
				InterpretException(ex);
			}
			return string.Empty;
		}

		/// <summary>
		/// Returns the folder where the process is running
		/// </summary>
		/// <returns>Path to the folder or empty string if error.</returns>
		internal static string GetProcessFolder() {
			try {
				string? path = Path.GetDirectoryName(Environment.ProcessPath);
				if (path == null) {
					return string.Empty;
				}
				return path;
			} catch(Exception ex) {
				InterpretException(ex);
				return string.Empty;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		internal static bool FileExists(string filename) {
			try {
				// For this one maybe we need to change the method to be case sensitive
				// Since looks like the .NET exists may cause trouble in some situations in Linux platform
				// we made this wrapper to take countermeasures just in case. We will leave it "as is" right now
				return File.Exists(filename);
			}
			catch (Exception ex) {
				InterpretException(ex);
				return false;
			}
		}
		
		/// <summary>
		/// Returns if a folder exists
		/// </summary>
		/// <param name="folder">Folder to be checked</param>
		/// <returns>True if it exists. False otherwise</returns>
		internal static bool FolderExist(string folder) {
			try {
				// For this one maybe we need to change the method to be case sensitive
				// Since looks like the .NET exists may cause trouble in some situations in Linux platform
				// we made this wrapper to take countermeasures just in case. We will leave it "as is" right now
				return Directory.Exists(folder);
			}catch(Exception ex) {
				InterpretException(ex);
				return false;
			}
		}

		/// <summary>
		/// Returns the unix timestamp of the last modified time of a given file in UTC
		/// </summary>
		/// <param name="filename">Filename to retrieve the modified time.</param>
		/// <returns>The file modification time.</returns>
		internal static long GetModifiedTimeUTC(string filename) {
			try {
				DateTime mTime = File.GetLastWriteTimeUtc(filename);
				long modTime = (long)mTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
				Msg.PrintMod("Checking modified access for: " + filename + " Time: " + modTime, ".fsapi", Msg.LogLevels.Debug);
				return modTime;
			}catch(Exception ex) {
				InterpretException(ex);
				return 0;
			}
		}

		/// <summary>
		/// Returns the size of a given file or -1 if invalid
		/// </summary>
		/// <param name="filename">filename to fetch the size</param>
		/// <returns>The size of the file.</returns>
		internal static long GetFileSize(string filename) {
			try {
				FileInfo fi = new FileInfo(filename);
				return fi.Length;
			}catch(Exception ex) {
				InterpretException(ex);
				return -1;
			}
		}


		/// <summary>
		/// Read a text file and returns the contents as string
		/// </summary>
		/// <param name="filename">The filename to use</param>
		/// <param name="ExitIfError">If the function should automatically quit on error. Default false.</param>
		/// <returns>A string with the file contents or empty.</returns>
		internal static string ReadTextFile(string filename,bool ExitIfError = false) {
			string content = string.Empty;
			try {
				StreamReader ProjectFile = new StreamReader(filename);
				content = ProjectFile.ReadToEnd();
				ProjectFile.Close();
				ProjectFile.Dispose();
			} catch (Exception ex) {
				if (ExitIfError) {
					Msg.PrintAndAbortMod("Error. Failed reading file:" + filename + " Error:" + ex.Message, ".fsapi");
				}
				Msg.PrintWarningMod("Error. Failed reading file:" + filename + " Error:" + ex.Message, ".fsapi");
			}
			return content;
		}

		/// <summary>
		/// Writes a text file with the given content
		/// </summary>
		/// <param name="filename">The filename to use</param>
		/// <param name="content">The content to be written.</param>
		/// <param name="ExitIfError">If the function should automatically quit on error. Default false</param>
		/// <returns>True if operation was okey, false otherwise.	</returns>
		internal static bool WriteTextFile(string filename,string content,bool ExitIfError = false){
			try {
				StreamWriter ProjectFile = new StreamWriter(filename);
				ProjectFile.Write(content);
				ProjectFile.Close();
				ProjectFile.Dispose();
				return true;
			} catch (Exception ex) {
				if (ExitIfError) {
					Msg.PrintAndAbortMod("Error. Failed writing file:" + filename + " Error:" + ex.Message, ".fsapi");
				}
				Msg.PrintWarningMod("Error. Failed writing file:" + filename + " Error:" + ex.Message, ".fsapi");
			}
			return false;
		}



		/// <summary>
		/// Prints a warning message with the exception
		/// </summary>
		/// <param name="ex">Exception to be show.</param>
		internal static void InterpretException(Exception ex) {
			Msg.PrintWarningMod("Exception in FSAPI Error:" + ex.Message, ".fsapi");
		}
	}
}