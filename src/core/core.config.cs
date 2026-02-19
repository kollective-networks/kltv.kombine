/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Collections;
using Microsoft.CodeAnalysis.Scripting;
using Kltv.Kombine.Api;

namespace Kltv.Kombine
{
	/// <summary>
	///
	/// </summary>
	internal static class Config {

		/// <summary>
		/// Script file to be executed. By default "kombine.csx" in the current working directory
		/// </summary>
		public static string ScriptFile { get; private set; } = Constants.Mak_File;

		/// <summary>
		/// Script path. Taken from script file command line if specified or current working directory
		/// </summary>
		public static string ScriptPath { get; private set; } = string.Empty;

		/// <summary>
		/// Debug script build
		/// </summary>
		public static bool BuildDebug { get; private set; } = false;

		/// <summary>
		/// If the script should be rebuilded
		/// </summary>
		public static bool Rebuild { get; private set; } = false;

		/// <summary>
		/// 
		/// </summary>
		public static bool BuildOnly { get; private set; } = false;

		/// <summary>
		/// 
		/// </summary>
		public static bool AllowAssemblyLoad { get; private set; } = false;

		/// <summary>
		/// Always save the state when the script terminates
		/// </summary>
		public static bool SaveAlwaysOnExit {get; private set; } = false;

		/// <summary>
		/// Action to be executed
		/// </summary>
		public static string Action { get; private set; } = string.Empty;

		/// <summary>
		/// Action parameters to be attached in action execution
		/// </summary>
		public static string[] ActionParameters { get; private set; } = new string[0];

		/// <summary>
		/// Log level for the output
		/// </summary>
		public static Msg.LogLevels LogLevel = Msg.LogLevels.Undefined;


		/// <summary>
		/// Initializes this module, executes command line and initializes rest of modules
		/// </summary>
		public static void Initialize() {

			// Initialize command line
			//
			ParseCommandLine();
			// Initialize Logging
			//
			Msg.Initialize(LogLevel);
			// Initialize cache
			//
			Cache.Init();
		}


		/// <summary>
		///
		/// Kombine Parameters:
		///
		/// mkb [parameters] [action] [action parameters]
		///
		/// [parameters] They are optional and can be any of the following:
		///
		/// -ksdbg: Script will include debug information so script debugging will be possible
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
		/// kconfig: Manages the tool configuration
		/// kcache: Manages the tool cache
		///
		/// [action parameters]
		/// They are optional and belongs to the specified action. In case of scripts,they are passed to the
		/// executed function as parameters.
		///
		/// </summary>
		private static void ParseCommandLine() {
			string[] commands = Environment.GetCommandLineArgs();
			List<string> parameters = new List<string>();
			// Parse tool parameters
			for (int a = 0; a != commands.Length; a++) {
				string? cmd = commands[a];
				// Skip if its null
				if (cmd == null)
					continue;
				// Skip the first one (the tool name)
				if (a == 0)
					continue;
				// If we have an action, then the rest of parameters are action parameters
				if (Action != string.Empty) {
					parameters.Add(cmd);
					continue;
				}
				// Check if it is a kombine parameter
				if (ParseKombineParameters(cmd))
					continue;
				// Next should be the action.
				// Rest of the iterations will push the rest of parameters to the parameters list
				Action = cmd;
			}
			// Save the action parameters
			ActionParameters = parameters.ToArray();
			// Check the log level (set default is was not set)
			if (LogLevel == Msg.LogLevels.Undefined)
				LogLevel = Msg.LogLevels.Normal;
			// Set the script file path
			string? spath = Path.GetDirectoryName(ScriptFile);
			if (spath != null)
				ScriptPath = spath;
			// Set default action.
			if (Action == string.Empty)
				Action = "khelp";
		}


		/// <summary>
		/// Parses and sets the kombine parameters
		/// </summary>
		/// <param name="cmd">parameter to be evaluated</param>
		/// <returns>true if was a kombine parameter, false otherwise</returns>
		private static bool ParseKombineParameters(string cmd) {
			if (cmd == "-ksdbg") {
				BuildDebug = true;
				return true;
			}
			if ( (cmd == "-ksrb") || cmd == "-ksrebuild") {
				Rebuild = true;
				return true;
			}
			if ((cmd == "-ko:silent") || (cmd == "-ko:s")) {
				if (LogLevel == Msg.LogLevels.Undefined) {
					LogLevel = Msg.LogLevels.Silent;
				} else {
					Msg.PrintWarning("Log level already specified. Ignoring the second one.");
				}
				return true;
			}
			if ((cmd == "-ko:normal") || (cmd == "-ko:n")) {
				if (LogLevel == Msg.LogLevels.Undefined) {
					LogLevel = Msg.LogLevels.Normal;
				} else {
					Msg.PrintWarning("Log level already specified. Ignoring the second one.");
				}
				return true;
			}
			if ((cmd == "-ko:verbose") || (cmd == "-ko:v")){
				if (LogLevel == Msg.LogLevels.Undefined) {
					LogLevel = Msg.LogLevels.Verbose;
				} else {
					Msg.PrintWarning("Log level already specified. Ignoring the second one.");
				}
				return true;
			}
			if ((cmd == "-ko:debug") || (cmd == "-ko:d")) {
				if (LogLevel == Msg.LogLevels.Undefined) {
					LogLevel = Msg.LogLevels.Debug;
				} else {
					Msg.PrintWarning("Log level already specified. Ignoring the second one.");
				}
				return true;
			}
			if (cmd.StartsWith("-kfile:")) {
				ScriptFile = cmd.Substring(7);
				return true;
			}
			return false;
		}


		/// <summary>
		/// 
		/// </summary>
		public static void ShowBanner() {
			Msg.Print("");
			Msg.Print("Kombine Build Engine "+KombineMain.Version.Major+"."+KombineMain.Version.Minor+"."+KombineMain.Version.Build);
			Msg.Print("Copyrigth(C) Kollective Networks 2026. All rights reserved.");
			Msg.Print("");
		}

		/// <summary>
		/// 
		/// </summary>
		public static void ShowHelp() {
			Msg.BeginIndent();
			Msg.Print("mkb [parameters] [action] [action parameters]");
			Msg.Print("");
			Msg.Print("[parameters] They are optional and can be any of the following:");
			Msg.Print("");
			Msg.Print("-ksdbg");
			Msg.Print("   Script will include debug information so script debugging will be possible.");
			Msg.Print("-ksrb or -ksrebuild");
			Msg.Print("   Script will be rebuilded even if it is cached.");
			Msg.Print("-ko:silent or -ko:s");
			Msg.Print("   Script output will be silent.");
			Msg.Print("-ko:normal or -ko:n");
			Msg.Print("   Script output will be normal.");
			Msg.Print("-ko:verbose or -ko:v");
			Msg.Print("   Script output will be verbose.");
			Msg.Print("-ko:debug or -ko:d");
			Msg.Print("   Script output will be debug.");
			Msg.Print("-kfile:filename");
			Msg.Print("   Indicates which script file we should execute (default kombine.csx)");
			Msg.Print("");
			Msg.Print("[action] Action to be executed. If not specified the default action is \"khelp\"");
			Msg.Print("         The action is used to specify which function in the script should be called after evaluation but");
			Msg.Print("         there are some reserved actions for the tool itself which cannot be used for the scripts:");
			Msg.Print("");
			Msg.Print(" kversion: Shows tool version and exit.");
			Msg.Print(" khelp: Show this help and exit.");
			Msg.Print(" kconfig: Manages the tool configuration.");
			Msg.Print(" kcache: Manages the tool cache.");
			Msg.Print("");
			Msg.Print("[action parameters]");
			Msg.Print("         They are optional and belongs to the specified action. In case of scripts,they are passed to the");
			Msg.Print("         executed function as parameters. For example: mkb kcache help");
			Msg.Print("");
		}
	}
}