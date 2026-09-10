/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Collections;
using BinaryPack.Attributes;
using BinaryPack.Enums;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	/// <summary>
	/// Holds the functions to serialize / deserialize a kombine script state
	/// </summary>
	internal class KombineState {

		/// <summary>
		/// The script filename which belongs to this instance
		/// </summary>
		private string? scriptfilename = null;

		/// <summary>
		/// The signature of the state files this engine writes and accepts
		/// </summary>
		private const long StateSignature = 0x000020002;

		/// <summary>
		///  Initializes one Kombine script instance.
		///  It initializes the environment variables with the current system environment
		/// </summary>
		public KombineState() {
			foreach (DictionaryEntry de in System.Environment.GetEnvironmentVariables()) {
				string? k = de.Key as string;
				string? v = de.Value as string;
				if (k != null && v != null)
					Environment[k] = v;
			}
		}

		/// <summary>
		/// Fetch and deserialize the cache.
		/// Returns true if the script is available to be executed
		/// false otherwise (non existent, outdated)
		/// </summary>
		/// <param name="filename">Script name to try recover state</param>
		/// <param name="scriptHash">The content hash of the script text.</param>
		/// <param name="debug">If the script must carry debug information.</param>
		/// <returns>True if ready. False it should be rebuilt.</returns>
		public bool FetchCache(string filename, string scriptHash, bool debug) {
			//
			// Check if was initialized.
			// We will try to find the precompiled file alongside the script
			//
			if (scriptfilename == null) {
				scriptfilename = filename;
				ScriptHash = scriptHash;
				Msg.PrintMod("Trying to locate the state", ".exec.state", Msg.LogLevels.Debug);
				if (!Cache.IsScriptCached(filename)) {
					// Compile the script
					Msg.PrintMod("Script is not cached. Triggering build.", ".exec.state", Msg.LogLevels.Debug);
					return false;
				} else {
					// Load the script
					return Deserialize(debug);
				}
			} else {
				Msg.PrintMod("State is already in memory.", ".exec.state", Msg.LogLevels.Debug);
				return true;
			}
		}

		/// <summary>
		/// Saves the memory stream for a newly built script
		/// </summary>
		/// <param name="ms">Object with the assembly</param>
		/// <param name="ds">Object with the debug information</param>
		public bool SetCompiledScript(MemoryStream ms,MemoryStream? ds = null) {
			// We reset to null just to disable old PDB information which maybe not replaced due to non debug compilation
			Data.CompiledScript = null;
			Data.CompiledScriptPDB = null;
			Data.CompiledScript = ms.ToArray();
			Data.BuildWithDebug = false;
			if (ds != null) {
				Data.CompiledScriptPDB = ds.ToArray();
				Data.BuildWithDebug = true;
			}
			return Serialize();
		}

		/// <summary>
		/// Saves the state file
		/// </summary>
		public bool Serialize() {
			// Sanity check
			if (scriptfilename == null) {
				Msg.PrintErrorMod("Script filename as null. Aborting", ".exec.state");
				return false;
			}
			// Set Signature and version
			//
			stateFile.Signature = StateSignature;
			stateFile.Version = KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build;
			// The content the script was compiled from: a touch without an edit keeps the state valid
			//
			stateFile.ScriptHash = ScriptHash;
			//
			// Set the file dependencies with their content hashes to trigger rebuilds if something changed:
			// the helpers merged into the script and the modules referenced, transitively
			//
			stateFile.SourceDependencies = FileDependencies.Keys.ToArray();
			stateFile.SourceDependenciesHash = FileDependencies.Values.ToArray();
			// Save the compiled script bytes into the struct
			//
			byte[] result = BinaryPack.BinaryConverter.Serialize(stateFile);
			return Cache.SaveScriptCached(scriptfilename, result);
		}

		/// <summary>
		/// Loads the state file and checks it is valid for this run: written by this engine version with
		/// the same debug setting, from the same script content and the same content of every file the
		/// script loads.
		/// </summary>
		/// <param name="debug">If the script must carry debug information.</param>
		public bool Deserialize(bool debug) {
			if (scriptfilename == null) {
				Msg.PrintErrorMod("Script filename is null. Aborting", ".exec.state");
				return false;
			}
			byte[]? result = Cache.LoadScriptCached(scriptfilename);
			if (result == null) {
				Msg.PrintWarningMod("State file could not be loaded. Deleting state.",".exec.state",Msg.LogLevels.Verbose);
				return false;
			}
			try {
				stateFile = BinaryPack.BinaryConverter.Deserialize<StateFile>(result);
			} catch (Exception ex) {
				Msg.PrintWarningMod("State file is corrupted or outdated. Deleting state.", ".exec.state", Msg.LogLevels.Verbose);
				Msg.PrintWarningMod("Exception: " + ex.Message, ".exec.state", Msg.LogLevels.Debug);
				return false;
			}
			Msg.PrintMod("Loaded cached state file.", ".exec.state", Msg.LogLevels.Debug);
			// Check the version because maybe the script was cached but for a previous Kombine version
			// and that could trigger errors.
			if (stateFile.Signature != StateSignature) {
				Msg.PrintWarningMod("State file signature is not valid. Deleting state.", ".exec.state", Msg.LogLevels.Verbose);
				return false;
			}
			if (stateFile.Version != KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build) {
				Msg.PrintWarningMod("State file version is not valid. Deleting state.", ".exec.state", Msg.LogLevels.Verbose);
				return false;
			}
			if (stateFile.BuildWithDebug != debug) {
				Msg.PrintMod("State file was built " + (stateFile.BuildWithDebug ? "with" : "without") + " debug information and this run needs the opposite. Rebuilding.", ".exec.state", Msg.LogLevels.Verbose);
				return false;
			}
			if (stateFile.ScriptHash != ScriptHash) {
				Msg.PrintMod("The script content changed. Rebuilding.", ".exec.state", Msg.LogLevels.Verbose);
				return false;
			}
			//
			// Check if the file dependencies are still valid. If something changed we need to rebuild.
			// The list of this run comes from the pre pass over the loaded files; it must be the same list,
			// file by file, with the same content.
			//
			if (stateFile.SourceDependencies.Length != stateFile.SourceDependenciesHash.Length || stateFile.SourceDependencies.Length != FileDependencies.Count) {
				Msg.PrintMod("The files loaded by the script changed. Rebuilding.", ".exec.state", Msg.LogLevels.Verbose);
				return false;
			}
			for (int i = 0; i < stateFile.SourceDependencies.Length; i++) {
				string f = stateFile.SourceDependencies[i];
				if (!FileDependencies.TryGetValue(f, out string? hash)) {
					Msg.PrintMod("State file dependency " + f + " is not loaded anymore. Rebuilding.", ".exec.state", Msg.LogLevels.Verbose);
					return false;
				}
				if (hash != stateFile.SourceDependenciesHash[i]) {
					Msg.PrintMod("State file dependency " + f + " has changed. Rebuilding.", ".exec.state", Msg.LogLevels.Verbose);
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Environment variables for the script
		/// </summary>
		public Dictionary<string,string> Environment { get; set; } = new Dictionary<string,string>();

		/// <summary>
		/// Shared objects for the script
		/// </summary>
		public Dictionary<string,object> SharedObjects { get; set; } = new Dictionary<string,object>();

		/// <summary>
		/// The content hash of the script text this state belongs to
		/// </summary>
		public string ScriptHash { get; set; } = string.Empty;

		/// <summary>
		/// File dependencies for the script with filename and content hash: the helpers merged into the
		/// script and the modules it references, transitively. A change of any of them triggers a rebuild.
		/// </summary>
		public Dictionary<string,string> FileDependencies { get; set; } = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Return the data object which belongs to this state.
		/// </summary>
		public StateFile Data {
			get { return stateFile; }
		}

		private StateFile stateFile = new();

		/// <summary>
		/// State file holds the full structure state for building serialized to disk
		/// It includes among signatures and co, the content hash of the script (just to rebuild if something changed)
		/// and the loaded files with their content hashes
		/// </summary>
		[BinarySerialization(SerializationMode.Fields)]
		public class StateFile {

			/// <summary>
			/// Signature to recognize a state file (by default 0x000020002)
			/// </summary>
			public long				Signature;
			/// <summary>
			/// Version of the state file (just in case we need to discard older ones due to update)
			/// </summary>
			public string			Version = "invalid";
			/// <summary>
			/// Content hash of the script text
			/// </summary>
			public string			ScriptHash = string.Empty;

			/// <summary>
			/// Array of file dependencies with filename and content hash
			/// </summary>
			public string[]         SourceDependencies = new string[0];
			public string[]         SourceDependenciesHash = new string[0];

			/// <summary>
			/// If the script was built with debug information
			/// </summary>
			public bool				BuildWithDebug = false;
			/// <summary>
			/// Binary blob holding the compiled script assembly
			/// </summary>
			public byte[]?			CompiledScript = null;
			/// <summary>
			/// Binary blob holding the compiled script debug information (if any)
			/// </summary>
			public byte[]?			CompiledScriptPDB = null;
		}
	}
}
