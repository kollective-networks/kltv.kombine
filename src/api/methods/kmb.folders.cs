/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.Dynamic;
using Kltv.Kombine.Types;

namespace Kltv.Kombine.Api {
	
	/// <summary>
	/// It wraps several folder methods to simplify use and check / add case sensitive file system support
	/// </summary>
	public static class Folders {

		#region Folder Creation Operations

		/// <summary>
		/// Check if a folder exists
		/// </summary>
		/// <param name="folder">Folder to be checked.</param>
		/// <returns>True if exists, false otherwise.</returns>
		public static bool Exists(KValue folder) {
			return FSAPI.FolderExist(folder);
		}

		/// <summary>
		/// Move a folder from source to destination
		/// </summary>
		/// <param name="src">source path for the folder</param>
		/// <param name="dst">destination path for the folder</param>
		/// <returns></returns>
		public static bool Move(KValue src,KValue dst) {
			try {
				Msg.PrintMod("Moving folder: " + src + " to: " + dst, ".folders", Msg.LogLevels.Verbose);
				Directory.Move(src, dst);
				return true;
			} catch(Exception ex) {
				Msg.PrintWarningMod("Failed moving folder: " + src + " to: " + dst + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
				return false;
			}
		}

		/// <summary>
		/// Create a single folder
		/// </summary>
		/// <param name="folder">Folder to be created.</param>
		/// <returns>True if okey. False otherwise.</returns>
		public static bool Create(KValue folder) {
			try {
				Msg.PrintMod("Creating folder: " + folder, ".folders", Msg.LogLevels.Verbose);
				Directory.CreateDirectory(folder);
				return true;
			} catch (Exception ex) {
				Msg.PrintWarningMod("Failed creating folder: " + folder + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
				return false;
			}
		}

		/// <summary>
		/// Create a single folder
		/// </summary>
		/// <param name="folder">Folder to be created.</param>
		/// <returns>True if okey. False otherwise.</returns>
		public static bool Create(string folder) {
			try {
				Msg.PrintMod("Creating folder: " + folder, ".folders", Msg.LogLevels.Verbose);
				Directory.CreateDirectory(folder);
				return true;
			} catch (Exception ex) {
				Msg.PrintWarningMod("Failed creating folder: " + folder + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
				return false;
			}
		}

		/// <summary>
		/// Create a list of folders.
		/// </summary>
		/// <param name="folders">List of folders to be created.</param>
		/// <returns>If any folder failed to be created it returns false. True pennywise. </returns>
		public static bool Create(KList folders) {
			bool result = true;
			foreach (KValue v in folders) {
				if (Create(v) == false)
					result = false;
			}
			return result;
		}

		/// <summary>
		/// Create a list of folders.
		/// </summary>
		/// <param name="folders">List of folders to be created.</param>
		/// <returns>If any folder failed to be created it returns false. True otherwise.</returns>
		public static bool Create(string[] folders) {
			bool result = true;
			foreach (string v in folders) {
				if (Create(v) == false)
					result = false;
			}
			return result;
		}

		#endregion

		#region Folder Deletion Operations

		/// <summary>
		/// Delete a list of folders.
		/// </summary>
		/// <param name="folders">Folder list to be deleted.</param>
		/// <param name="recurse">If the subfolders should be deleted as well. Default false</param>
		/// <returns></returns>
		public static bool Delete(KList folders,bool recurse = false) {
			bool result = true;
			foreach (KValue v in folders) {
				Msg.PrintMod("Deleting folder: " + v, ".folders", Msg.LogLevels.Verbose);
				if (Delete(v, recurse) == false)
					result = false;
			}
			return result;
		}

		/// <summary>
		/// Delete a folder and optionally its subfolders
		/// </summary>
		/// <param name="folder">Folder to delete</param>
		/// <param name="recurse">If subfolders should be deleted as well. Default false.</param>
		/// <returns></returns>
		public static bool Delete(KValue folder, bool recurse = false) {
			return Delete(folder.ToString(), recurse);
		}

		/// <summary>
		/// Delete a folder and optionally its subfolders
		/// </summary>
		/// <param name="folder">Folder to delete.</param>
		/// <param name="DeleteSubFolders">If subfolders should be deleted as well. Default false.</param>
		/// <returns></returns>
		public static bool Delete(string folder, bool DeleteSubFolders = false) {
			if (Directory.Exists(folder)) {
				Msg.PrintMod("Deleting folder:"+folder, ".folders", Msg.LogLevels.Verbose);
				DirectoryInfo directory = new DirectoryInfo(folder);
				// Delete files inside the folder (just in case we need to set attributes in order to delete the folder)
				foreach (FileInfo file in directory.GetFiles()) {
					try {
						File.SetAttributes(file.FullName, FileAttributes.Normal);
						file.Delete();
					} catch (Exception ex) {
						Msg.PrintWarningMod("Failed deleting / set attributes: " + file.Name + " error: " + ex.Message,".folders",Msg.LogLevels.Verbose);
						return false;
					}
				}
				// Remove subfolders if requested
				if (DeleteSubFolders) {
					foreach (DirectoryInfo subDirectory in directory.GetDirectories()) {
						try {
							File.SetAttributes(subDirectory.FullName, FileAttributes.Normal);
							if (Delete(subDirectory.FullName, true) == false)
								return false;
							subDirectory.Delete();
						} catch (Exception ex) {
							if (ex is not DirectoryNotFoundException) {
								Msg.PrintWarningMod("Failed deleting / set attributes: " + subDirectory.Name + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
								return false;
							}
						}
					}
				}
				// Finally remove the folder itself
				try {
					Directory.Delete(folder);
				} catch (Exception ex) {
					Msg.PrintWarningMod("Failed deleting / set attributes: " + folder + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
				}
			} else {
				Msg.PrintMod("Folder to delete does not exists: "+folder, ".folders", Msg.LogLevels.Verbose);
			}
			return true;
		}



		#endregion

		#region Current Folder Operations

		/// <summary>
		/// Fileitem with the current working folder
		/// </summary>
		public static string CurrentWorkingFolder { get { return FSAPI.GetCurrentFolder(); } } 

		/// <summary>
		/// Folder stack to push/pop current folder for cwd jumping operations
		/// </summary>
		private static readonly Stack<string> folderStack = new();

		/// <summary>
		/// Push the current working folder into the stack
		/// </summary>
		public static void CurrentFolderPush() {
			folderStack.Push(CurrentWorkingFolder);
			Msg.PrintMod("Current folder push: " + CurrentWorkingFolder, ".folders", Msg.LogLevels.Debug);
			return;
		}

		/// <summary>
		/// Pop a folder from the stack
		/// </summary>
		public static void CurrentFolderPop() {
			if (folderStack.Count > 0) {
				string p = folderStack.Pop();
				SetCurrentFolder(p, false);
				Msg.PrintMod("Current folder pop to: " + p, ".folders", Msg.LogLevels.Debug);
				return;
			}
			Msg.PrintWarningMod("Current folder pop but stack is empty.", ".folders",Msg.LogLevels.Verbose);
		}

		/// <summary>
		/// Sets a new working folder specified by CWD and optionally pushes the current one into the stack
		/// </summary>
		/// <param name="CWD">New working folder</param>
		/// <param name="PushCurrent">If the current one should be saved</param>
		public static void SetCurrentFolder(string CWD,bool PushCurrent = true) {
			if (!string.IsNullOrEmpty(CWD)) {
				if (PushCurrent == true)
					CurrentFolderPush();
				FSAPI.SetCurrentFolder(CWD);
				Msg.PrintMod("Current folder set to: " + CWD, ".folders", Msg.LogLevels.Verbose);
				return;
			}
			Msg.PrintWarningMod("Folder to set for current working directory is empty.", ".folders",Msg.LogLevels.Verbose);
		}

		/// <summary>
		/// Returns the current working folder
		/// </summary>
		/// <returns>An string with the current working folder.</returns>
		public static string GetCurrentFolder() {
			return CurrentWorkingFolder;
		}

		#endregion

		#region Kombine Tool Folder

		/// <summary>
		/// Kombine Binary Folder
		/// </summary>
		public static string CurrentToolFolder { get { return FSAPI.GetProcessFolder(); } }

		#endregion

		#region Script Folder

		/// <summary>
		/// Returns the current script folder
		/// </summary>
		public static string CurrentScriptFolder { get { 
				if (KombineMain.CurrentRunningScript != null)
					return KombineMain.CurrentRunningScript.ScriptPath;
				Msg.PrintWarningMod("Script folder requested but no script folder can be fetched like no script is running.", ".folders", Msg.LogLevels.Normal);
				return string.Empty;
			}
		}

		/// <summary>
		/// Returns the parent script folder if any or empty if none.
		/// </summary>
		public static string ParentScriptFolder { get{
				if (KombineMain.CurrentRunningScript != null){
					if (KombineMain.CurrentRunningScript.ParentScript != null){
						return KombineMain.CurrentRunningScript.ParentScript.ScriptPath;
					}
				}
				Msg.PrintWarningMod("Script folder requested but no script folder can be fetched like no script is running.", ".folders", Msg.LogLevels.Normal);
				return string.Empty;
			}
		}

		#endregion

		#region Folder Copy Operations
		

		/// <summary>
		/// 
		/// </summary>
		private class FolderPair {
			/// <summary>
			/// 
			/// </summary>
			public string Source { get; private set; }
			/// <summary>
			/// 
			/// </summary>
			public string Target { get; private set; }
			/// <summary>
			/// 
			/// </summary>
			/// <param name="source"></param>
			/// <param name="target"></param>
			public FolderPair(string source, string target) {
				Source = source;
				Target = target;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public enum CopyOptions {
			/// <summary>
			/// 
			/// </summary>
			Default = 0x00000000,
			/// <summary>
			/// 
			/// </summary>
			IncludeSubFolders = 0x00000001,
			/// <summary>
			/// 
			/// </summary>
			OnlyModifiedFiles = 0x00000002,
			/// <summary>
			/// 
			/// </summary>
			ShowProgress = 0x00000004,
			/// <summary>
			/// 
			/// </summary>
			OnlyFolders = 0x00000008,
			/// <summary>
			/// 
			/// </summary>
			DeleteMissingFiles = 0x00000010,
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Source"></param>
		/// <param name="Target"></param>
		/// <param name="Options"></param>
		/// <returns></returns>
		public static bool Copy(string Source, string Target,CopyOptions Options = CopyOptions.Default) {

			try {
				// We use a stack to push directories we may found
				// At beggining just the initial one is added.
				var stack = new Stack<FolderPair>();
				stack.Push(new FolderPair(Source, Target));

				while (stack.Count > 0) {

					var folders = stack.Pop();
					Directory.CreateDirectory(folders.Target);
					// Copy Files if OnlyFolders is absent
					//
					if (Options.HasFlag(CopyOptions.OnlyFolders) == false) {

						// If we need to check destination files to delete absent files from source (mirror copy)
						// we extract first the files in the target folder and compare them with source
						// excluding absolute path part (since we want to preserve structure)
						string[] target_files;
						string[] source_files = Directory.GetFiles(folders.Source, "*.*");
						if (Options.HasFlag(CopyOptions.DeleteMissingFiles)) {
							target_files = Directory.GetFiles(folders.Target, "*.*");
							foreach (var file1 in target_files) {
								bool bFound = false;
								foreach (var file2 in source_files) {
									// TODO: Review. Case sensitive tips ahead ( Linux vs Windows )
									if (Path.GetFileName(file1) == Path.GetFileName(file2))
										bFound = true;
								}
								// If wasn't found, we can remove this file at target
								if (!bFound) {
									// TODO: Attributes?
									File.Delete(file1);
								}
							}

						}
						// And process every file.
						foreach (var file in source_files) {
							// Obtain the target filename
							string target_file = Path.Combine(folders.Target, Path.GetFileName(file));
							// Show copy info
							if (Options.HasFlag(CopyOptions.ShowProgress)) {
								// Show task info
							}
							if (Options.HasFlag(CopyOptions.OnlyModifiedFiles)) {
								// Compare files if destination exists
								if (File.Exists(target_file)) {
									// Check if file is different source vs target
									if (Files.Compare(file, target_file, Files.CompareOptions.CompareTime)) {
										// Skip, files are equal
										continue;
									}
								}
							}
							// File was different or absent
							File.Copy(file, target_file,true);
							// Updated or added
						}
					}
					// Copy SubFolders if IncludeSubFolders is pressent
					//
					if (Options.HasFlag(CopyOptions.IncludeSubFolders)) {
						foreach (var folder in Directory.GetDirectories(folders.Source)) {
							stack.Push(new FolderPair(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
						}
					}
				}
				return true;
			}
			catch (Exception ex) {
				Msg.PrintWarningMod("Failed copying folder: " + Source + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
				return false;
			}
		}

		#endregion

		#region Search Files in folders Operations

		/// <summary>
		/// Search for a filename (which may have relative paht) in the script folder and backwards
		/// If the script folder cannot be resolve, it will take the current working folder
		/// </summary>
		/// <param name="filename">The filename to search for (may include relative path)</param>
		/// <returns>The result or empty if nothing found.</returns>
		public static KValue SearchBackPath(string filename){
			Msg.PrintMod("BackPath search for: " + filename, ".folders", Msg.LogLevels.Verbose);
			string? cpath = CurrentScriptFolder;
			if (string.IsNullOrEmpty(cpath))
				cpath = CurrentWorkingFolder;
			while(cpath != null) {
				DirectoryInfo? di = Directory.GetParent(cpath);
				if (di == null)
					break;
				string? look = Path.Combine(di.FullName, filename);
				if ((look != null) && Files.Exists(look)) {
					Msg.PrintMod("BackPath search found this: " + look, ".folders", Msg.LogLevels.Verbose);
					return Path.GetFullPath(look);
				}
				Msg.PrintMod("BackPath search not found in: " + look, ".folders", Msg.LogLevels.Debug);
				cpath = di.FullName;
			}
			Msg.PrintMod("BackPath search failed for: " + filename, ".folders", Msg.LogLevels.Verbose);
			return string.Empty;
		}

		/// <summary>
		/// Search for a filename (which may have relative paht) in the script folder and forwards
		/// If the script folder cannot be resolve, it will take the current working folder
		/// </summary>
		/// <param name="filename">The filename to search for (may include relative paths)</param>
		/// <returns>The result or empty if nothing found.</returns>
		public static string SearchForwardPath(string filename) {
			string? cpath = CurrentScriptFolder;
			if (string.IsNullOrEmpty(cpath))
				cpath = CurrentWorkingFolder;
			// Look in the current folder
			string? look = Path.Combine(cpath, filename);
			if ((look != null) && Files.Exists(look)) {
				Msg.PrintMod("ForwardPath search found this: " + look, ".folders", Msg.LogLevels.Verbose);
				return Path.GetFullPath(look);
			}
			try{
				// Not found in the current folder, search subfolders.
				string[] folders = Directory.GetDirectories(cpath, "*", SearchOption.AllDirectories);
				foreach (string folder in folders) {
					look = Path.Combine(folder, filename);
					if ((look != null) && Files.Exists(look)) {
						Msg.PrintMod("ForwardPath search found this: " + look, ".folders", Msg.LogLevels.Verbose);
						return Path.GetFullPath(look);
					}
					Msg.PrintMod("ForwardPath search not found in: " + look, ".folders", Msg.LogLevels.Debug);
				} 
			}catch(Exception ex){
				Msg.PrintWarningMod("ForwardPath search failed for: " + filename + " error: " + ex.Message, ".folders",Msg.LogLevels.Verbose);
			}
			return string.Empty;
		}


		#endregion

		#region Resolve Operation 

		/// <summary>
		/// Resolve a filename by the given order.
		/// Absolute path
		/// Relative path from current working directory
		/// Relative path from script directory
		/// Relative path from forward trace
		/// Relative path from backward trace
		/// </summary>
		/// <param name="path">Path+file to look for.</param>
		/// <returns>Place where is found or null if any.</returns>
		internal static string? ResolveFilename(string path){
			string? look = null;

			// Check if its an URL
			if (path.StartsWith("http://") || path.StartsWith("https://")){
				KValue content = Http.GetDocument(path);
				if (content.IsEmpty()){
					Msg.PrintWarningMod("Failed to fetch: " + path+" trying to use cache.", ".folders", Msg.LogLevels.Verbose);
					if (Cache.IsIncludeCached(path)) {
						return Cache.GetIncludeCached(path);
					}
					Msg.PrintWarningMod("Failed to fetch: " + path + " and no cache found.", ".folders", Msg.LogLevels.Verbose);
					return null;
				}
				return Cache.SetIncludeCached(path, content);
			}
			// Check if its an absolute path
			if (Path.IsPathRooted(path) == true) {
				Msg.PrintMod("ResolveReference for absolute path:"+path,".folders", Msg.LogLevels.Debug);
				if (Files.Exists(path))
					return Path.GetFullPath(path);
				Msg.PrintMod("ResolveReference for absolute path:" + path + " does not exists.", ".folders", Msg.LogLevels.Debug);
				return null;
			}
			// Is relative path
			if (Files.Exists(path)) {
				Msg.PrintMod("ResolveReference (CurrentDirectory):" + path, ".folders", Msg.LogLevels.Debug);
				return Path.GetFullPath(path);
			}
			// Script directory
			//
			look = Path.Combine(Folders.CurrentScriptFolder, path);
			if ( (look != null) && (Files.Exists(look)) ) {
				Msg.PrintMod("ResolveReference (ScriptDirectory):" + look, ".folders", Msg.LogLevels.Debug);
				return Path.GetFullPath(look);
			}
			// Forward trace directories
			look = Folders.SearchForwardPath(path);
			if (string.IsNullOrEmpty(look)== false){
				Msg.PrintMod("ResolveReference (BacktraceDirectory):" + look, ".folders", Msg.LogLevels.Debug);
				return Path.GetFullPath(look);
			}
			// Backtrace directories
			look = Folders.SearchBackPath(path);
			if (string.IsNullOrEmpty(look)== false){
				Msg.PrintMod("ResolveReference (BacktraceDirectory):" + look, ".folders", Msg.LogLevels.Debug);
				return Path.GetFullPath(look);
			}
			// Tool directory
			look = Path.Combine(Folders.CurrentToolFolder, path);
			if ((look != null) && (Files.Exists(look))) {
				Msg.PrintMod("ResolveReference (ToolDirectory):" + look, ".exec.folders", Msg.LogLevels.Debug);
				return Path.GetFullPath(look);
			}
			return null;
		}
		#endregion

	}
}