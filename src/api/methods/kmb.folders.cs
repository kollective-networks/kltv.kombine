/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Dynamic;
using Kltv.Kombine.Types;

namespace Kltv.Kombine.Api {
	
	/// <summary>
	/// It wraps several folder methods to simplify use and check / add case sensitive file system support
	/// </summary>
	public static class Folders {

		/// <summary>
		/// Last failure of a Folders call. Reset at the start of every call, set when it fails.
		/// Exists and the searches never set it: their result is the answer.
		/// </summary>
		public static ApiError LastError { get; private set; } = ApiError.None;

		/// <summary>
		/// Records a failure and logs it at verbose level.
		/// </summary>
		private static bool Fail(ErrorCode code, string message, string source) {
			LastError = new ApiError(code, message, source);
			Msg.PrintWarningMod(LastError.ToString(), ".folders", Msg.LogLevels.Verbose);
			return false;
		}

		/// <summary>
		/// Records a failure from an exception and logs it at verbose level.
		/// </summary>
		private static bool Fail(Exception ex, string source) {
			LastError = ApiError.From(ex, source);
			Msg.PrintWarningMod(LastError.ToString(), ".folders", Msg.LogLevels.Verbose);
			return false;
		}

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
			LastError = ApiError.None;
			if (!Directory.Exists(src))
				return Fail(ErrorCode.NotFound, "The folder to move does not exist", src);
			if (Directory.Exists(dst))
				return Fail(ErrorCode.AlreadyExists, "The destination folder already exists", dst);
			try {
				Msg.PrintMod("Moving folder: " + src + " to: " + dst, ".folders", Msg.LogLevels.Verbose);
				Directory.Move(src, dst);
				return true;
			} catch(Exception ex) {
				return Fail(ex, src);
			}
		}

		/// <summary>
		/// Create a single folder
		/// </summary>
		/// <param name="folder">Folder to be created.</param>
		/// <returns>True if okey. False otherwise.</returns>
		public static bool Create(KValue folder) {
			return Create(folder.ToString());
		}

		/// <summary>
		/// Create a single folder
		/// </summary>
		/// <param name="folder">Folder to be created.</param>
		/// <returns>True if okey. False otherwise.</returns>
		public static bool Create(string folder) {
			LastError = ApiError.None;
			if (string.IsNullOrEmpty(folder))
				return Fail(ErrorCode.InvalidArgument, "The folder to create is empty", folder);
			try {
				Msg.PrintMod("Creating folder: " + folder, ".folders", Msg.LogLevels.Verbose);
				Directory.CreateDirectory(folder);
				return true;
			} catch (Exception ex) {
				return Fail(ex, folder);
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
			LastError = ApiError.None;
			if (Directory.Exists(folder)) {
				Msg.PrintMod("Deleting folder:"+folder, ".folders", Msg.LogLevels.Verbose);
				DirectoryInfo directory = new DirectoryInfo(folder);
				// Delete files inside the folder (just in case we need to set attributes in order to delete the folder)
				foreach (FileInfo file in directory.GetFiles()) {
					try {
						File.SetAttributes(file.FullName, FileAttributes.Normal);
						file.Delete();
					} catch (Exception ex) {
						return Fail(ex, file.FullName);
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
							if (ex is not DirectoryNotFoundException)
								return Fail(ex, subDirectory.FullName);
						}
					}
				}
				// Finally remove the folder itself. Without recursion a folder with subfolders is not empty.
				try {
					Directory.Delete(folder);
				} catch (Exception ex) {
					return Fail(ex, folder);
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
		/// Pop a folder from the stack and make it the working folder
		/// </summary>
		/// <returns>True if a folder was popped and set, false if the stack is empty or the folder is gone (see LastError).</returns>
		public static bool CurrentFolderPop() {
			LastError = ApiError.None;
			if (folderStack.Count > 0) {
				string p = folderStack.Pop();
				Msg.PrintMod("Current folder pop to: " + p, ".folders", Msg.LogLevels.Debug);
				return SetCurrentFolder(p, false);
			}
			return Fail(ErrorCode.InvalidArgument, "The folder stack is empty, nothing to pop", string.Empty);
		}

		/// <summary>
		/// Sets a new working folder specified by CWD and optionally pushes the current one into the stack
		/// </summary>
		/// <param name="CWD">New working folder</param>
		/// <param name="PushCurrent">If the current one should be saved</param>
		/// <returns>True if the working folder was changed, false otherwise (see LastError).</returns>
		public static bool SetCurrentFolder(string CWD,bool PushCurrent = true) {
			LastError = ApiError.None;
			if (string.IsNullOrEmpty(CWD))
				return Fail(ErrorCode.InvalidArgument, "The folder to set as working folder is empty", CWD);
			if (!Directory.Exists(CWD))
				return Fail(ErrorCode.NotFound, "The folder to set as working folder does not exist", CWD);
			if (PushCurrent == true)
				CurrentFolderPush();
			if (!FSAPI.SetCurrentFolder(CWD)) {
				LastError = FSAPI.LastError;
				return false;
			}
			Msg.PrintMod("Current folder set to: " + CWD, ".folders", Msg.LogLevels.Verbose);
			return true;
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
				Msg.PrintWarningMod("Script folder requested but no script is running.", ".folders", Msg.LogLevels.Verbose);
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
				Msg.PrintWarningMod("Script folder requested but no script is running.", ".folders", Msg.LogLevels.Verbose);
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
		/// Copy options for folder copy operation
		/// </summary>
		public enum CopyOptions {
			/// <summary>
			/// 
			/// </summary>
			Default = 0x00000000,
			/// <summary>
			/// Include subfolders when copying
			/// </summary>
			IncludeSubFolders = 0x00000001,
			/// <summary>
			/// Only copy modified files
			/// </summary>
			OnlyModifiedFiles = 0x00000002,
			/// <summary>
			/// Show progress during copy operation
			/// </summary>
			ShowProgress = 0x00000004,
			/// <summary>
			/// Copy only folders, not files
			/// </summary>
			OnlyFolders = 0x00000008,
			/// <summary>
			/// Delete missing files in target that are not present in source (mirror copy)
			/// </summary>
			DeleteMissingFiles = 0x00000010,
		}

		/// <summary>
		/// Reporter used by Copy to show its progress line when the ShowProgress option is given.
		/// When not set, Progress.Default is used. Assign an instance to select the renderer.
		/// </summary>
		public static ITaskProgress? Progress { get; set; } = null;

		/// <summary>
		/// Copies a folder from source to target with several options.
		/// With the ShowProgress option a progress line "Copying folder: progress n/total done" is
		/// shown through the Progress reporter: the items (files, or folders when only folders are
		/// copied) are counted first, and every processed item advances the progress, also the ones
		/// skipped as unchanged.
		/// </summary>
		/// <param name="Source">Source folder (required)</param>
		/// <param name="Target">Destination folder (required)</param>
		/// <param name="Options">Options to copy (optional)</param>
		/// <param name="FileMask">File mask to be used in the copy operation (optional)</param>
		/// <returns>True if the copy completed, false otherwise.</returns>
		public static bool Copy(string Source, string Target,CopyOptions Options = CopyOptions.Default,string FileMask = "*.*") {
			LastError = ApiError.None;
			if (!Directory.Exists(Source))
				return Fail(ErrorCode.NotFound, "The folder to copy does not exist", Source);
			// Progress line only when requested, through the configured reporter or the engine default
			ITaskProgress? reporter = Options.HasFlag(CopyOptions.ShowProgress) ? (Progress ?? Api.Progress.Default) : null;
			bool onlyFolders = Options.HasFlag(CopyOptions.OnlyFolders);
			int total = 0;
			int done = 0;
			try {
				// The line is opened before counting, so a big tree shows activity while it is scanned
				if (reporter != null) {
					reporter.Start("Copying " + CopyDisplayName(Source));
					total = CountCopyItems(Source, Options, FileMask);
					reporter.Report(0, "0/" + total);
				}
				// We use a stack to push directories we may found
				// At beggining just the initial one is added.
				var stack = new Stack<FolderPair>();
				stack.Push(new FolderPair(Source, Target));

				while (stack.Count > 0) {

					var folders = stack.Pop();
					Directory.CreateDirectory(folders.Target);
					// Copy Files if OnlyFolders is absent
					//
					if (onlyFolders == false) {

						// If we need to check destination files to delete absent files from source (mirror copy)
						// we extract first the files in the target folder and compare them with source
						// excluding absolute path part (since we want to preserve structure)
						string[] target_files;
						string[] source_files = Directory.GetFiles(folders.Source, FileMask);
						if (Options.HasFlag(CopyOptions.DeleteMissingFiles)) {
							target_files = Directory.GetFiles(folders.Target, FileMask);
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
							bool copy = true;
							if (Options.HasFlag(CopyOptions.OnlyModifiedFiles)) {
								// Compare files if destination exists: equal files are skipped
								if (File.Exists(target_file) && Files.Compare(file, target_file, Files.CompareOptions.CompareTime))
									copy = false;
							}
							// File was different or absent
							if (copy)
								File.Copy(file, target_file,true);
							// Processed, copied or skipped
							done++;
							reporter?.Report(total > 0 ? done / (double)total : 1, done + "/" + total);
						}
					} else {
						// Only folders are copied: the folders are the items
						done++;
						reporter?.Report(total > 0 ? done / (double)total : 1, done + "/" + total);
					}
					// Copy SubFolders if IncludeSubFolders is pressent
					//
					if (Options.HasFlag(CopyOptions.IncludeSubFolders)) {
						foreach (var folder in Directory.GetDirectories(folders.Source)) {
							stack.Push(new FolderPair(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
						}
					}
				}
				reporter?.Finish("done");
				return true;
			}
			catch (Exception ex) {
				reporter?.Finish("failed", ProgressOutcome.Error);
				return Fail(ex, Source);
			}
		}

		/// <summary>
		/// Counts the items Copy will process with the given options: the files matching the mask,
		/// or the folders when only folders are copied, walking the subfolders when requested.
		/// </summary>
		/// <param name="source">Source folder.</param>
		/// <param name="options">Copy options.</param>
		/// <param name="fileMask">File mask.</param>
		/// <returns>The number of items.</returns>
		private static int CountCopyItems(string source, CopyOptions options, string fileMask) {
			int count = 0;
			var pending = new Stack<string>();
			pending.Push(source);
			while (pending.Count > 0) {
				string folder = pending.Pop();
				if (options.HasFlag(CopyOptions.OnlyFolders))
					count++;
				else
					count += Directory.GetFiles(folder, fileMask).Length;
				if (options.HasFlag(CopyOptions.IncludeSubFolders)) {
					foreach (string sub in Directory.GetDirectories(folder))
						pending.Push(sub);
				}
			}
			return count;
		}

		/// <summary>
		/// Name shown in the progress line of a copy: the last segment of the source folder.
		/// </summary>
		/// <param name="source">Source folder.</param>
		/// <returns>The folder name, or the whole path when it has no name.</returns>
		private static string CopyDisplayName(string source) {
			string name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			return string.IsNullOrEmpty(name) ? source : name;
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
			return SearchBackPath(filename, null);
		}

		/// <summary>
		/// Search for a filename (which may have relative paht) from the given start folder and backwards
		/// If no start folder is given, it will take the script folder and, failing that, the current working folder
		/// </summary>
		/// <param name="filename">The filename to search for (may include relative path)</param>
		/// <param name="startFolder">Folder to start the search from or null for the ambient script folder.</param>
		/// <returns>The result or empty if nothing found.</returns>
		internal static string SearchBackPath(string filename, string? startFolder){
			Msg.PrintMod("BackPath search for: " + filename, ".folders", Msg.LogLevels.Verbose);
			string? cpath = startFolder;
			if (string.IsNullOrEmpty(cpath))
				cpath = CurrentScriptFolder;
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
			return SearchForwardPath(filename, null);
		}

		/// <summary>
		/// Search for a filename (which may have relative paht) from the given start folder and forwards
		/// If no start folder is given, it will take the script folder and, failing that, the current working folder
		/// </summary>
		/// <param name="filename">The filename to search for (may include relative paths)</param>
		/// <param name="startFolder">Folder to start the search from or null for the ambient script folder.</param>
		/// <returns>The result or empty if nothing found.</returns>
		internal static string SearchForwardPath(string filename, string? startFolder) {
			string? cpath = startFolder;
			if (string.IsNullOrEmpty(cpath))
				cpath = CurrentScriptFolder;
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
		/// Relative path from the including file directory (when provided)
		/// Relative path from script directory
		/// Relative path from current working directory
		/// Relative path from backward trace
		/// Relative path from the tool directory
		/// Relative path from forward trace (disabled by default, -kforward)
		/// The forward trace (recursive walk of every subfolder, first match wins) is out of the
		/// default chain: with repos that embed other repos sharing the same relative layout it can
		/// silently bind a foreign copy of the file, and the state cache then persists the wrong bind.
		/// </summary>
		/// <param name="path">Path+file to look for.</param>
		/// <param name="baseDir">Directory of the file containing the reference, when it comes from an already loaded include. Null otherwise.</param>
		/// <param name="scriptDir">Directory of the script being compiled/executed. Ambient current script folder when null.</param>
		/// <returns>Place where is found or null if any.</returns>
		internal static string? ResolveFilename(string path, string? baseDir = null, string? scriptDir = null){
			LastResolveReason = string.Empty;
			string? look = null;

			// Check if its an URL: the file is fetched the first time it is loaded and again when a rebuild
			// is asked (-ksrb, Engine.RebuildScripts), once per run; the cached copy is used otherwise,
			// whether the loading script is compiled or not, so a normal run makes no request
			if (path.StartsWith("http://") || path.StartsWith("https://")){
				string cached = Cache.GetIncludeCached(path);
				lock (fetchedUrls) {
					if (fetchedUrls.Contains(path))
						return Files.Exists(cached) ? cached : null;
				}
				if (Cache.IsIncludeCached(path) && !Config.Rebuild) {
					Msg.PrintMod("ResolveReference (cached copy): " + path + " -> " + cached, ".folders", Msg.LogLevels.Debug);
					return cached;
				}
				KValue content = Http.GetDocument(path);
				lock (fetchedUrls) {
					fetchedUrls.Add(path);
				}
				if (content.IsEmpty()){
					Msg.PrintWarningMod("Failed to fetch: " + path+" trying to use cache.", ".folders", Msg.LogLevels.Verbose);
					if (Cache.IsIncludeCached(path)) {
						return cached;
					}
					Msg.PrintWarningMod("Failed to fetch: " + path + " and no cache found.", ".folders", Msg.LogLevels.Verbose);
					return null;
				}
				Msg.PrintMod("ResolveReference (fetched): " + path + " -> " + cached, ".folders", Msg.LogLevels.Verbose);
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
			// Fetch the ambient script folder when no explicit one was given
			if (string.IsNullOrEmpty(scriptDir))
				scriptDir = Folders.CurrentScriptFolder;
			// Including file directory
			//
			if (string.IsNullOrEmpty(baseDir) == false) {
				look = Path.Combine(baseDir, path);
				if (Files.Exists(look)) {
					Msg.PrintMod("ResolveReference (IncludingFileDirectory):" + look, ".folders", Msg.LogLevels.Debug);
					return Path.GetFullPath(look);
				}
			}
			// Script directory
			//
			if (string.IsNullOrEmpty(scriptDir) == false) {
				look = Path.Combine(scriptDir, path);
				if (Files.Exists(look)) {
					Msg.PrintMod("ResolveReference (ScriptDirectory):" + look, ".folders", Msg.LogLevels.Debug);
					return Path.GetFullPath(look);
				}
			}
			// Current working directory
			if (Files.Exists(path)) {
				Msg.PrintMod("ResolveReference (CurrentDirectory):" + path, ".folders", Msg.LogLevels.Debug);
				return Path.GetFullPath(path);
			}
			// Backtrace directories
			look = Folders.SearchBackPath(path, scriptDir);
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
			// Forward search of the subfolders: a feature disabled by default (-kforward / Engine.ForwardSearch).
			// When disabled it is still probed on this failure path, so the reason can name the file it would find.
			look = Folders.SearchForwardPath(path, scriptDir);
			if (string.IsNullOrEmpty(look) == false){
				if (Config.ResolveForward) {
					Msg.PrintMod("ResolveReference (ForwardDirectory): " + path + " -> " + look, ".folders", Msg.LogLevels.Verbose);
					return Path.GetFullPath(look);
				}
				LastResolveReason = "'" + path + "' could not be resolved: it exists at " + look + ", reachable only through the forward search of the subfolders, which is disabled by default (enable it with -kforward or Engine.ForwardSearch, or make the reference relative to the including script)";
				Msg.PrintMod(LastResolveReason, ".folders", Msg.LogLevels.Verbose);
				return null;
			}
			LastResolveReason = "'" + path + "' could not be resolved in the including file folder, the script folder, the working folder, the parent folders or the tool folder";
			Msg.PrintMod(LastResolveReason, ".folders", Msg.LogLevels.Verbose);
			return null;
		}

		/// <summary>
		/// Reason of the last failed reference resolution, for the script executor to report.
		/// </summary>
		internal static string LastResolveReason { get; private set; } = string.Empty;

		/// <summary>
		/// The URLs fetched (or tried) in this run: a remote file is fetched once per run at most.
		/// </summary>
		private static readonly HashSet<string> fetchedUrls = new HashSet<string>(StringComparer.Ordinal);
		#endregion

	}
}