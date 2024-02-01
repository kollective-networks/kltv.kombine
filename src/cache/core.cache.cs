/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.Security.Cryptography;
using System.Text;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	internal static class Cache {

		/// <summary>
		/// Base cache folder
		/// </summary>
		internal static string CacheFolder { get; private set; } = string.Empty;

		/// <summary>
		/// Folder for cached states (precompiled scripts)
		/// </summary>
		internal static string CacheStates { get; private set; } = string.Empty;

		/// <summary>
		/// Folder for downloaded scripts
		/// </summary>
		internal static string CacheScripts { get; private set; } = string.Empty;


		/// <summary>
		/// Cache actions. Execute cache related actions.
		/// </summary>
		/// <param name="args">Arguments for the action.</param>
		internal static void Action(string[] args) {
			if (args.Length == 0) {
				Msg.PrintErrorMod("No arguments specified. Exiting.", ".cache");
			}
			if (args[0] == "clear") {
				Msg.Print("[*] Deleting all cache folders.");
				Folders.Delete(CacheFolder,true);
			}
			if (args[0] == "help") {
				Msg.Print("kcache clear: Deletes all cache folders.");
			}
		}


		/// <summary>
		/// Initializes the cache folders.
		/// </summary>
		internal static void Init() {
			// Fetch cache folder
			// In Windows we use the application data (following the standard, not like the microsoft itself with the .dotnet folders)
			// For linux and osx we use the user profile folder (trying to use the standard as well)
			try {
				string baseCacheFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
				CacheFolder = Path.GetFullPath(Path.Combine(baseCacheFolder, Constants.Cache_Folder));
				if (!Directory.Exists(CacheFolder))
					Directory.CreateDirectory(CacheFolder);
				CacheStates = Path.GetFullPath(Path.Combine(baseCacheFolder, Constants.Cache_States));
				if (!Directory.Exists(CacheStates))
					Directory.CreateDirectory(CacheStates);
				CacheScripts = Path.GetFullPath(Path.Combine(baseCacheFolder, Constants.Cache_Scripts));
				if (!Directory.Exists(CacheScripts))
					Directory.CreateDirectory(CacheScripts);
			} catch (Exception ex){
				// This is a fatal error, we can't continue without the cache folder
				Msg.PrintErrorMod("Failed to fetch/Create cache folder: "+ ex.Message,".cache");
				Msg.Deinitialize();
				Environment.Exit(-1);
			}
		}

		/// <summary>
		/// Returns if the script is cached and it could be used.
		/// </summary>
		/// <param name="scriptname">Scriptname to check on the cache.</param>
		/// <returns>True if we can use it. False otherwise.</returns>
		internal static bool IsScriptCached(string scriptname) {
			string filename = ConvertFilename(scriptname);
			Msg.PrintMod("Trying to fetch this: "+filename+" from cache.", ".cache", Msg.LogLevels.Debug);
			// Check if it exists
			if (!Files.Exists(filename)) {
				Msg.PrintMod(filename + " does not exists in cache.", ".cache", Msg.LogLevels.Debug);
				return false;
			}
			// Check the modified date.
			long compiledDate = Files.GetModifiedTime(filename);
			Msg.PrintMod("Compiled date in cache: " + compiledDate, ".cache",Msg.LogLevels.Debug);
			long scriptDate = Files.GetModifiedTime(scriptname);
			Msg.PrintMod("Script modified date: " + scriptDate, ".cache", Msg.LogLevels.Debug);
			if (scriptDate > compiledDate) { 
				Msg.PrintMod("Script is newer than the compiled version. Recompile.", ".cache", Msg.LogLevels.Debug);
				return false;
			}
			Msg.PrintMod("Script is older than the compiled version. Use the compiled version.", ".cache", Msg.LogLevels.Debug);
			return true;
		}


		/// <summary>
		/// Load a cached script.
		/// </summary>
		/// <param name="scriptname">Filename for the script to be loaded.</param>
		/// <returns>A byte array with the contents or null if failed.</returns>
		internal static byte[]? LoadScriptCached(string scriptname) {
			string filename = ConvertFilename(scriptname);
			try {
				byte[] result = File.ReadAllBytes(filename);
				return result;
			} catch (Exception ex) {
				Msg.PrintErrorMod("Failed to load cached script: "+ex.Message,".cache");
				return null;
			}
		}

		/// <summary>
		/// Saves a cached script.
		/// </summary>
		/// <param name="scriptname">Filename for the script to be saved</param>
		/// <param name="content">Byte array with the contents.</param>
		/// <returns>True if saving was okey. False otherwise.</returns>
		internal static bool SaveScriptCached(string scriptname, byte[] content) {
			string filename = ConvertFilename(scriptname);
			try {
				File.WriteAllBytes(filename, content);
				return true;
			} catch (Exception ex) {
				Msg.PrintErrorMod("Failed to save cached script: " + ex.Message, ".cache");
				return false;
			}
		}


		/// <summary>
		/// Converts the given scriptname into a filename for the cache.
		/// </summary>
		/// <param name="scriptname">Script name to be converted</param>
		/// <returns>The resulting name including cache path</returns>
		internal static string ConvertFilename(string scriptname) {
			// since we want a hash of the scriptname no matter if its invoked
			// as relative path, absolute or just the name, we will use the complete filename
			// as the key for the cache.
			scriptname = Path.GetFullPath(scriptname);
			StringBuilder sb = new StringBuilder();
			byte[] GetHash;
			using (HashAlgorithm algorithm = SHA256.Create()) {
				GetHash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(scriptname));
			}
			foreach (byte b in GetHash)
				sb.Append(b.ToString("X2").ToLower());
			string hashedName = sb + Constants.Ext_Compiled;
			string filename = Path.Combine(CacheStates, hashedName);
			return Path.GetFullPath(filename);
		}

	}
}


