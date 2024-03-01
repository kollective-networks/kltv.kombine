/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Kltv.Kombine {
	/// <summary>
	/// Kombine main class
	/// </summary>
	internal static partial class KombineMain {

		/// <summary>
		/// 
		/// Kombine Parameters:
		///
		/// mkb [parameters] [action] [action parameters]
		/// 
		/// [parameters] They are optional and can be any of the following:
		/// 
		/// -ksdbg: Script will include debug information so script debugging will be possible
		/// -ksrb or -ksrebuild: Script will be rebuilded even if it is cached
		/// -ko:silent or -ko:s  : Output will be silent
		/// -ko:normal or -ko:n	 : Output will be normal
		/// -ko:verbose or -ko:v : Output will be verbose
		/// -ko:debug or -ko:d   : Output will be debug
		/// -kfile: Indicates which script file we should execute (default kombine.csx)
		/// 
		/// [action] Action to be executed. If not specified the default action is "khelp"
		/// The action is used to specify which function in the script should be called after evaluation but
		/// there are some reserved actions for the tool itself which cannot be used for the scripts:
		/// 
		/// kversion: Shows tool version and exit
		/// khelp: Show this help and exit
		/// kconfig: Manages the tool configuration (not yet implemented)
		/// kcache: Manages the tool cache (not fully implemented)
		/// 
		/// [action parameters]
		/// They are optional and belongs to the specified action. In case of scripts,they are passed to the
		/// executed function as parameters.
		///
		/// </summary>
		///-------------------------------------------------------------------------------------------------------------
		static int Main() {
			//
			// Set a console cancel event. Just to kill child process if parent gots canceled by console
			// Remove the handler is bugged at least until .NET 4.5.1 s
			//
			Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelExecutionHandler);
			// Initialize systems
			//
			Config.Initialize();
			// Show the banner if required
			if (Config.LogLevel >= Msg.LogLevels.Verbose)
				Config.ShowBanner();
			Msg.PrintMod("Starting Kombine", ".main", Msg.LogLevels.Debug);
			// Execute builtin actions
			if (Config.Action == "khelp") {
				Config.ShowHelp();
				Msg.Deinitialize();
				return 0;
			}
			if (Config.Action == "kversion") {
				Config.ShowBanner();
				Msg.Deinitialize();
				return 0;
			}
			if (Config.Action == "kcache") {
				Cache.Action(Config.ActionParameters);
				Msg.Deinitialize();
				return 0;
			}
			if (Config.Action == "kconfig") {
				Msg.PrintErrorMod("Not yet implemented", ".main");
				Msg.Deinitialize();
				return 0;
			}
			// Execute the script
			//
			if (Config.Action == string.Empty) {
				Msg.PrintErrorMod("No action specified. Exiting.", ".main");
				Msg.Deinitialize();
				return -1;
			}
			if (string.IsNullOrEmpty(Config.ScriptFile) == false) {
				// First script always change the current folder if required.
				int result = RunScript(Config.ScriptFile, Config.Action, Config.ActionParameters, true);
				Msg.Deinitialize();
				return result;
			} else {
				Msg.PrintErrorMod("Script file not defined. Exiting.", ".main");
				Msg.Deinitialize();
				return -1;
			}
		}

		/// <summary>
		/// Executes one script
		/// </summary>
		/// <param name="script">Script to be executed</param>
		/// <param name="action">Action to be executed in the script</param>
		/// <param name="args">Arguments for the action.</param>
		/// <param name="changedir">If the current folder should be changed to enter where the script is.</param>
		/// <returns></returns>
		static internal int RunScript(string script, string action, string[]? args = null, bool changedir = true) {
			// Prepare name & path
			string? path = Path.GetDirectoryName(Path.GetFullPath(script));
			if (path == null) {
				path = Folders.GetCurrentFolder();
				if (path == null)
					path = string.Empty;
			}
			// There is a path with the name. Check if we need to use it.
			if (changedir) {
				// Set the current folder to the script folder and set the flag to pop it after execution
				Folders.SetCurrentFolder(path, true);
				// Also, script name should be fixed to contain just the script and not the relative path
				script = Path.GetFileName(script);
			}
			// Create a new script instance
			KombineScript main = new KombineScript(script,path, Config.BuildDebug);
			// Set the parent script (if any) on the new one
			main.ParentScript = CurrentRunningScript;
			// Set the current running script
			KombineScript? kombineScript = CurrentRunningScript;
			CurrentRunningScript = main;
			// Transport the environment variables & shared objects to the new script (if any)
			if (kombineScript != null) {
				main.State.Environment = kombineScript.State.Environment.Clone();
				main.State.SharedObjects = kombineScript.State.SharedObjects.Clone();
			}
			// If its not the first script (is a nested one) add indentation
			if (kombineScript != null)
				Msg.BeginIndent();
			// Execute the script. 
			//
			int result = 0;
			Msg.PrintMod("Script to execute: " + script + " with action: " + action, ".main", Msg.LogLevels.Debug);
			try {
				result = main.Execute(action, args);
			} catch (Exception e) {
				// We need to catch here to trap when the script itself wants to abort
				Msg.PrintErrorMod("Exception executing script: " + e.Message, ".main");
				if (kombineScript != null)
					Msg.EndIndent();
				result = -1;
			}
			if (Config.SaveAlwaysOnExit) {
				// If we want to enable state saving on all executions
				Msg.PrintMod("Saving state", ".main", Msg.LogLevels.Debug);
				// Serialize the script state 
				main.State.Serialize();
			}
			if (kombineScript != null)
				Msg.EndIndent();
			if (changedir)
				Folders.CurrentFolderPop();
			// Restore the current running script
			CurrentRunningScript = kombineScript;
			// And return the result
			return result;
		} 

		/// <summary>
		/// This handler is used to trap from the actual Kombine console an exit event 
		/// that means, ctrl+c ... the idea is to add a gracefully exit
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private static void CancelExecutionHandler(object? sender, ConsoleCancelEventArgs args) {
			// TODO: 
			// We must signal an event for the CurrentRunningScript thread asking to exit
			// We must control the signal on all the controlled functions to abort gracefully the script execution (return to invocation code)
			// After script is ended, then, save the state and exit.
			// Right now, we just exit. We will lose the state but is not the end of the world
			Msg.Print("");
			Msg.PrintErrorMod("User cancel execution.", ".main");
			ChildProcess.KillAllChilds();
			Msg.PrintErrorMod("All processes killed.", ".main");
			Environment.Exit(-1);
		}

		/// <summary>
		/// Holds the current running script
		/// </summary>
		internal static KombineScript? CurrentRunningScript { get; private set; } = null;



	}
}












