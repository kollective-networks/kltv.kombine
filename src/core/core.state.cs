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
		/// <returns>True if ready. False it should be rebuilt.</returns>
		public bool FetchCache(string filename) {
			//
			// Check if was initialized. 
			// We will try to find the precompiled file alongside the script
			//
			if (scriptfilename == null) {
				scriptfilename = filename;
				Msg.PrintMod("Trying to locate the state", ".exec.state", Msg.LogLevels.Debug);
				if (!Cache.IsScriptCached(filename)) {
					// Compile the script
					Msg.PrintMod("Script is not cached. Triggering build.", ".exec.state", Msg.LogLevels.Debug);
					return false;
				} else {
					// Load the script
					return Deserialize();
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
			stateFile.Signature = 0x000020001;
			stateFile.Version = KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build;
			// Fetch file time from the script and save it to serialize
			//
			stateFile.ScriptModifiedTime = File.GetLastWriteTimeUtc(scriptfilename).ToBinary();
			// Save the compiled script bytes into the struct
			//
			byte[] result = BinaryPack.BinaryConverter.Serialize(stateFile);
			return Cache.SaveScriptCached(scriptfilename, result);
		}

		/// <summary>
		/// Loads the state file 
		/// </summary>
		public bool Deserialize() {
			if (scriptfilename == null) {
				Msg.PrintErrorMod("Script filename is null. Aborting", ".exec.state");
				return false;
			}
			byte[]? result = Cache.LoadScriptCached(scriptfilename);
			if (result == null) {
				Msg.PrintWarningMod("State file could not be loaded. Deleting state.",".exec.state",Msg.LogLevels.Normal);
				return false;
			}
			stateFile = BinaryPack.BinaryConverter.Deserialize<StateFile>(result);
			Msg.PrintMod("Loaded cached state file.", ".exec.state", Msg.LogLevels.Debug);
			// Check the version because maybe the script was cached but for a previous Kombine version
			// and that could trigger errors.
			if (stateFile.Signature != 0x000020001) {
				Msg.PrintWarningMod("State file signature is not valid. Deleting state.", ".exec.state", Msg.LogLevels.Normal);
				return false;
			}
			if (stateFile.Version != KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build) {
				Msg.PrintWarningMod("State file version is not valid. Deleting state.", ".exec.state", Msg.LogLevels.Normal);
				return false;
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
		/// Return the data object which belongs to this state.
		/// </summary>
		public StateFile Data {
			get { return stateFile; }
		}

		private StateFile stateFile = new();

		/// <summary>
		/// State file holds the full structure state for building serialized to disk
		/// It includes among signatures and co, the file time for the script (just to rebuild if something changed)
		/// and the project list with units, sources,etc, everything with modification dates to build the DAG
		/// </summary>
		[BinarySerialization(SerializationMode.Fields)]
		public class StateFile {

			/// <summary>
			/// Signature to recognize a state file (by default 0x000020001)
			/// </summary>
			public long				Signature;
			/// <summary>
			/// Version of the state file (just in case we need to discard older ones due to update) 
			/// Current: 0x00010000;
			/// </summary>
			public string			Version = "invalid";
			/// <summary>
			/// Script modification time in EPOCH
			/// </summary>
			public long				ScriptModifiedTime = 0;
			/// <summary>
			/// If the script was built with debug information
			/// </summary>
			public bool				BuildWithDebug = false;
			/// <summary>
			/// Bynary blob holding the compiled script assembly
			/// </summary>
			public byte[]?			CompiledScript = null;
			/// <summary>
			/// Binary blob holding the compiled script debug information (if any)
			/// </summary>
			public byte[]?			CompiledScriptPDB = null;
		}
	}
}