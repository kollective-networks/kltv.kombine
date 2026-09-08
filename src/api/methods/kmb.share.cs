/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2025

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Shared object API
	/// It is intended to share instances of object across scripts.
	/// Failures are reported through the return value and LastError; nothing is printed at normal
	/// level unless ExitIfError aborts the script.
	/// </summary>
	public static class Share {

		/// <summary>
		/// Registry dictionary shared across all scripts no mather the relationship
		/// </summary>
		private static Dictionary<string,Dictionary<string,string>> registry = new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// Last failure of a Share call. Reset at the start of every call, set when it fails.
		/// </summary>
		public static ApiError LastError { get; private set; } = ApiError.None;

		/// <summary>
		/// Records a failure, logs it at verbose level and aborts the script when requested.
		/// </summary>
		private static bool Fail(ErrorCode code, string message, string source, string mod, bool ExitIfError) {
			LastError = new ApiError(code, message, source);
			Msg.PrintWarningMod(LastError.ToString(), mod, Msg.LogLevels.Verbose);
			if (ExitIfError) {
				// The abort reason must be visible at any log level
				Msg.PrintAndAbortMod(message + ": " + source, mod);
			}
			return false;
		}

		/// <summary>
		/// Adds a new registry to the shared registry pool
		/// It is shared across all the scripts. No matter the relationship
		/// </summary>
		/// <param name="name">Name for the registry</param>
		/// <param name="key">Key to store</param>
		/// <param name="value">Value to store</param>
		/// <param name="ExitIfError">If the script should exit if the value is missing / trigers error. Default false</param>
		/// <returns>True if okey, false otherwise (see LastError).</returns>
		public static bool Register(KValue name,KValue key, KValue value,bool ExitIfError = false){
			LastError = ApiError.None;
			if (!registry.ContainsKey(name)) {
				registry.Add(name,new Dictionary<string, string>());
				registry[name].Add(key,value);
				return true;
			}
			if (registry[name].ContainsKey(key))
				return Fail(ErrorCode.AlreadyExists, "Registry key already exists", name + "/" + key, ".reg", ExitIfError);
			registry[name].Add(key,value);
			return true;
		}

		/// <summary>
		/// Fetch a value from the registry under the given name and key
		/// </summary>
		/// <param name="name">Name to resolve.</param>
		/// <param name="key">Key to be queried.</param>
		/// <param name="ExitIfError">If the script should exit if the value is missing / trigers error. Default false</param>
		/// <returns>Value for that entry or empty if not found (see LastError).</returns>
		public static KValue Registry(KValue name,KValue key,bool ExitIfError = false) {
			LastError = ApiError.None;
			if (registry.ContainsKey(name)) {
				if (registry[name].ContainsKey(key)) {
					return registry[name][key];
				}
			}
			Fail(ErrorCode.NotFound, "Registry key not found", name + "/" + key, ".reg", ExitIfError);
			return new KValue();
		}

		/// <summary>
		/// Dumps the content of the registry for script debugging purposes
		/// </summary>
		public static void DumpRegistry(){
			Msg.PrintMod("Dumping registry",".reg",Msg.LogLevels.Verbose);
			foreach (KeyValuePair<string, Dictionary<string, string>> entry in registry) {
				Msg.PrintMod("Registry: "+entry.Key,".reg",Msg.LogLevels.Verbose);
				foreach (KeyValuePair<string, string> subentry in entry.Value) {
					Msg.PrintMod("  "+subentry.Key+" = "+subentry.Value,".reg",Msg.LogLevels.Verbose);
				}
			}
		}

		/// <summary>
		/// Set an object to be shared
		/// </summary>
		/// <param name="name">name for the object to be shared</param>
		/// <param name="obj">object to be shared</param>
		/// <param name="ExitIfError">If the script should exit if the value is missing / trigers error. Default false</param>
		/// <returns>True if the object is added. False if it was already shared or no script is running (see LastError).</returns>
		public static bool Set(string name, object obj,bool ExitIfError = false) {
			LastError = ApiError.None;
			if (KombineMain.CurrentRunningScript == null)
				return Fail(ErrorCode.InvalidArgument, "No script running, the object cannot be shared", name, ".share", ExitIfError);
			if (KombineMain.CurrentRunningScript.State.SharedObjects.ContainsKey(name))
				return Fail(ErrorCode.AlreadyExists, "Shared object already exists", name, ".share", ExitIfError);
			KombineMain.CurrentRunningScript.State.SharedObjects.Add(name,obj);
			return true;
		}

		/// <summary>
		/// Get an object from the shared pool
		/// </summary>
		/// <param name="name">Name of the object to be fetched.</param>
		/// <param name="ExitIfError">If the script should exit if the value is missing / trigers error. Default false</param>
		/// <returns>The object to use or null if doesn't exists (see LastError).</returns>
		public static object? Get(string name,bool ExitIfError = false) {
			LastError = ApiError.None;
			if (KombineMain.CurrentRunningScript == null) {
				Fail(ErrorCode.InvalidArgument, "No script running, the object cannot be fetched", name, ".share", ExitIfError);
				return null;
			}
			if (KombineMain.CurrentRunningScript.State.SharedObjects.ContainsKey(name)) {
				return KombineMain.CurrentRunningScript.State.SharedObjects[name];
			}
			Fail(ErrorCode.NotFound, "Shared object not found", name, ".share", ExitIfError);
			return null;
		}

		/// <summary>
		/// Dump the content of the shared object pool for debugging purposes
		/// </summary>
		public static void DumpObjects(bool ExitIfError = false){
			if (KombineMain.CurrentRunningScript == null) {
				Msg.PrintWarningMod("No script running. Cannot fetch objects",".share",Msg.LogLevels.Verbose);
				if (ExitIfError) {
					// The abort reason must be visible at any log level
					Msg.PrintAndAbortMod("No script running. Cannot fetch objects",".share");
				}
				return;
			}
			Msg.PrintMod("Dumping shared objects",".share",Msg.LogLevels.Verbose);
			foreach (KeyValuePair<string, object> entry in KombineMain.CurrentRunningScript.State.SharedObjects) {
				Msg.PrintMod("Object: "+entry.Key+" = "+entry.Value.GetType().ToString(),".share",Msg.LogLevels.Verbose);
			}
		}

	}
}
