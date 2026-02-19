/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api
{
	/// <summary>
	/// 
	/// </summary>
	public static class Msg {

		/// <summary>
		/// Initializes the Msg static class.
		/// Note that parameters can be changed at runtime during the script execution so, inside the script
		/// you can raise the log level or change the output flags
		/// </summary>
		/// <param name="Level">Specify the log level</param>
		internal static void Initialize(LogLevels Level) {
			// TODO: Add the rest of parameters to specify the output
			LogLevel = Level;
			// Disable cursor on the console
			try{
				Console.CursorVisible = false;
			}catch(Exception ex){
				Msg.PrintErrorMod("Failed to disable cursor on the console:"+ex.Message, "Msg", LogLevels.Debug);
			}
		}

		/// <summary>
		/// Deinitializes the Msg static class.
		/// </summary>
		internal static void Deinitialize() {
			try {
				Console.CursorVisible = true;
			} catch(Exception ex){
				Msg.PrintErrorMod("Failed to disable cursor on the console:"+ex.Message, "Msg", LogLevels.Debug);
			}
		}


		/// <summary>
		/// 
		/// </summary>
		internal enum OutputDevices {
			/// <summary>
			/// 
			/// </summary>
			Default = 0x00000001,
			/// <summary>
			/// 
			/// </summary>
			Console = 0x00000001,
			/// <summary>
			/// 
			/// </summary>
			File = 0x00000002,
			/// <summary>
			/// 
			/// </summary>
			Debugger = 0x00000004
		};

		/// <summary>
		/// 
		/// </summary>
		internal enum LogTypes {
			/// <summary>
			/// 
			/// </summary>
			IDE,
			/// <summary>
			/// 
			/// </summary>
			Console,
			/// <summary>
			/// 
			/// </summary>
			Silent
		};

		/// <summary>
		/// 
		/// </summary>
		public enum LogLevels {
			/// <summary>
			/// 
			/// </summary>
			Silent = 0,
			/// <summary>
			/// 
			/// </summary>
			Normal = 1,
			/// <summary>
			/// 
			/// </summary>
			Verbose = 2,
			/// <summary>
			/// 
			/// </summary>
			Debug = 3,
			/// <summary>
			/// 
			/// </summary>
			Undefined = 4,
		}

		/// <summary>
		/// 
		/// </summary>
		internal static OutputDevices OutputDevice { get; set; } = OutputDevices.Console;
		/// <summary>
		/// 
		/// </summary>
		internal static LogTypes LogType { get; set; } = LogTypes.Console;
		/// <summary>
		/// 
		/// </summary>
		internal static LogLevels LogLevel { get; set; } = LogLevels.Normal;


		#region Private Elements
		//private static System.IO.StreamWriter? LogFileStream = null;
		private static Mutex LogLocker = new Mutex();
//		private static string LogFilePath = "";
		#endregion

		/// <summary>
		/// 
		/// </summary>
		static public void Lock(){
			LogLocker.WaitOne();
		}

		/// <summary>
		/// 
		/// </summary>
		static public void UnLock() {
			LogLocker.ReleaseMutex();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void Print(string Message,LogLevels Level = LogLevels.Normal) {
			PrintMod(Message, "", Level);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Mod"></param>
		/// <param name="Level"></param>
		static internal void PrintMod(string Message, string Mod = "", LogLevels Level = LogLevels.Normal) {
			InternalPrint(Message+Environment.NewLine, Mod, Level);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintWarning(string Message,LogLevels Level = LogLevels.Normal) {
			PrintWarningMod(Message, "", Level);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Mod"></param>
		/// <param name="Level"></param>
		static internal void PrintWarningMod(string Message, string Mod = "", LogLevels Level = LogLevels.Normal) {
			InternalPrint(Message + Environment.NewLine, Mod, Level, ConsoleColor.Yellow);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintError(string Message, LogLevels Level = LogLevels.Normal) {
			PrintErrorMod(Message, "", Level);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Mod"></param>
		/// <param name="Level"></param>
		static internal void PrintErrorMod(string Message, string Mod = "", LogLevels Level = LogLevels.Normal) {
			InternalPrint(Message + Environment.NewLine, Mod, Level, ConsoleColor.Red);
		}

		/// <summary>
		/// Prints a message and aborts the script execution.
		/// </summary>
		/// <param name="Message">Message to print</param>
		/// <param name="Level">Log level, default normal</param>
		static public void PrintAndAbort(string Message = "", LogLevels Level = LogLevels.Normal) {
			PrintAndAbortMod(Message, "", Level);
		}

		/// <summary>
		/// Prints a message and aborts the script execution.
		/// </summary>
		/// <param name="Message">Message to print</param>
		/// <param name="Mod">Module source of the message</param>
		/// <param name="Level">Loglevel, by default, normal.</param>
		static internal void PrintAndAbortMod(string Message = "", string Mod = "", LogLevels Level = LogLevels.Normal) {
			InternalPrint(Message + Environment.NewLine, Mod, Level, ConsoleColor.Red);
			// TODO: Close log here for log outputed to file

			// We use an exception to abort the script execution
			// This will be catched up at the script execution.
			// This method should not be used outside the script execution.
			throw new ScriptAbortException();
		}

		/// <summary>
		/// Raw print message to the console
		/// </summary>
		/// <param name="Message">Message to be printed.</param>
		/// <param name="Level">Log level</param>
		/// <remarks>It skips colors and indentation</remarks>
		static public void RawPrint(string Message, LogLevels Level = LogLevels.Normal){
			InternalPrint(Message, "", Level, ConsoleColor.Gray, true);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintTask(string Message, LogLevels Level = LogLevels.Normal) {
			InternalPrint(Message, "", Level);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintTaskSuccess(string Message = "",LogLevels Level = LogLevels.Normal) {
			if (Message == "")
				Message = "Done";
			InternalPrint(Message+Environment.NewLine, "", Level,ConsoleColor.Green,true);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintTaskWarning(string Message = "", LogLevels Level = LogLevels.Normal) {
			if (Message == "")
				Message = "Done";
			InternalPrint(Message + Environment.NewLine, "", Level, ConsoleColor.Yellow,true);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintTaskError(string Message="", LogLevels Level = LogLevels.Normal) {
			if (Message == "")
				Message = "Failed";
			InternalPrint(Message + Environment.NewLine, "", Level, ConsoleColor.Red,true);
		}

		#region Log Indentation
		static private int cm_CurrentIndent = 0;
		static private bool cm_Used = false;

		/// <summary>
		/// Adds another level of indentation in the log output
		/// We use the flag "used".
		/// Indentation is only added if we already used the current level.
		/// </summary>
		/// <param name="bSkipNotUsed">Specify if the indentation should be skipped when not used.</param>
		static public void BeginIndent(bool bSkipNotUsed = false) {
			if (bSkipNotUsed == false) {
				++cm_CurrentIndent;
			} else {
				if (cm_Used == true) {
					++cm_CurrentIndent;
					cm_Used = false;
				}
			}
		}

		/// <summary>
		/// Removes one level of indentation in the log output
		/// </summary>
		static public void EndIndent() {
			if (cm_CurrentIndent > 0) 
				--cm_CurrentIndent;
		}

		/// <summary>
		/// Retrieve the current indentation.
		/// </summary>
		/// <returns>An empty string or an string with spaces representing the indentation level</returns>
		static private string GetIndent() {
			string indent = "";
			for (int Indent = 0; Indent < cm_CurrentIndent; Indent++) {
				indent += "    ";
			}
			return indent;
		}


		#endregion


		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Mod"></param>
		/// <param name="Level"></param>
		/// <param name="Color"></param>
		/// <param name="SkipIndent">Specify if the indentation should be skipped.</param>
		private static void InternalPrint(string Message, string Mod, LogLevels Level,ConsoleColor Color = ConsoleColor.Gray,bool SkipIndent = false) {

			if (Mod != "") {
				Message = string.Format(Constants.Log_Prefix,Mod)+ Message;
			}
			// Type silent disables all logging
			if (LogType == LogTypes.Silent)
				return;
			// Type IDE reformats some logs to be IDE compatible / disable colors
			if (LogType == LogTypes.IDE) {

			}
			if (LogType == LogTypes.Console) {
				// Skip due to log level
				if (Level > LogLevel)
					return;
				cm_Used = true;
				// Output to console with colors + indent
				if (OutputDevice.HasFlag(OutputDevices.Console)) {
					if (SkipIndent == false)
						Message = GetIndent() + Message;
					Console.ForegroundColor = Color;
					Console.Write(Message);
					Console.ResetColor();
				}
				// Output to file without colors + indent
				if (OutputDevice.HasFlag(OutputDevices.File)) {

				}
			}
		}
	}
}