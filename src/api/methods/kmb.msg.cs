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
		public static LogLevels LogLevel { get; internal set; } = LogLevels.Normal;

		/// <summary>
		/// What kind of message a handler receives: the plain, warning and error lines, the task lines (a
		/// task line has no newline, the task result that follows it closes the line) and the raw text.
		/// </summary>
		public enum MessageKind {
			/// <summary>A plain line (Print).</summary>
			Normal,
			/// <summary>A warning line (PrintWarning).</summary>
			Warning,
			/// <summary>An error line (PrintError, PrintAndAbort).</summary>
			Error,
			/// <summary>The start of a task line (PrintTask): no newline, a task result follows.</summary>
			Task,
			/// <summary>The result that closes a task line (PrintTaskSuccess).</summary>
			TaskSuccess,
			/// <summary>The result that closes a task line (PrintTaskWarning).</summary>
			TaskWarning,
			/// <summary>The result that closes a task line (PrintTaskError).</summary>
			TaskError,
			/// <summary>Raw text (RawPrint), printed as it is.</summary>
			Raw
		}

		/// <summary>
		/// Receives one message of the engine or of a script.
		/// </summary>
		/// <param name="level">The level the message was printed at (Normal, Verbose, Debug).</param>
		/// <param name="kind">What the message is.</param>
		/// <param name="module">The engine module that printed it (".exec.script", ".tool"), empty for a script message.</param>
		/// <param name="message">The text without the indentation, the module prefix and the trailing newline.</param>
		/// <param name="indent">The indentation depth of the message (BeginIndent), for a log that keeps the structure.</param>
		public delegate void MessageHandler(LogLevels level, MessageKind kind, string module, string message, int indent);

		/// <summary>
		/// Called with every message before it is written to the console, whatever the log level and the
		/// output: a script installs it to filter the log and forward it to another facility or log system.
		/// It receives the messages of every script of the run, since the facility is one for the process,
		/// and of every thread (the callbacks of the tools), one message at a time. A message printed by the
		/// handler itself reaches the console but not the handler again, and an exception thrown by it is
		/// printed once at verbose level and does not stop the script. Null by default.
		/// </summary>
		public static MessageHandler? OnMessage { get; set; } = null;

		/// <summary>
		/// The current indentation depth, as BeginIndent and EndIndent left it.
		/// </summary>
		public static int Indent { get { return cm_CurrentIndent; } }

		/// <summary>True while the handler runs on this thread, so its own messages do not come back to it.</summary>
		[ThreadStatic]
		private static bool inHandler;

		/// <summary>The lock that hands the handler one message at a time.</summary>
		private static readonly object handlerLock = new object();

		/// <summary>
		/// Hands a message to the handler, if any: under a lock, never re-entered, never allowed to throw.
		/// </summary>
		private static void Forward(LogLevels level, MessageKind kind, string module, string message) {
			MessageHandler? handler = OnMessage;
			if (handler == null || inHandler)
				return;
			lock (handlerLock) {
				inHandler = true;
				try {
					handler(level, kind, module, message.TrimEnd('\r', '\n'), cm_CurrentIndent);
				} catch (Exception ex) {
					PrintWarningMod("The message handler threw: " + ex.Message, ".msg", LogLevels.Verbose);
				} finally {
					inHandler = false;
				}
			}
		}

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
			Forward(Level, MessageKind.Normal, Mod, Message);
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
			Forward(Level, MessageKind.Warning, Mod, Message);
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
			Forward(Level, MessageKind.Error, Mod, Message);
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
			Forward(Level, MessageKind.Error, Mod, Message);
			InternalPrint(Message + Environment.NewLine, Mod, Level, ConsoleColor.Red);
			// TODO: Close log here for log outputed to file

			// We use an exception to abort the script execution
			// This will be catched up at the script execution.
			// This method should not be used outside the script execution.
			// The message travels with it so a parent script reads the reason in Engine.LastError
			throw new ScriptAbortException(Message);
		}

		/// <summary>
		/// Raw print message to the console
		/// </summary>
		/// <param name="Message">Message to be printed.</param>
		/// <param name="Level">Log level</param>
		/// <remarks>It skips colors and indentation</remarks>
		static public void RawPrint(string Message, LogLevels Level = LogLevels.Normal){
			Forward(Level, MessageKind.Raw, "", Message);
			InternalPrint(Message, "", Level, ConsoleColor.Gray, true);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="Level"></param>
		static public void PrintTask(string Message, LogLevels Level = LogLevels.Normal) {
			Forward(Level, MessageKind.Task, "", Message);
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
			Forward(Level, MessageKind.TaskSuccess, "", Message);
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
			Forward(Level, MessageKind.TaskWarning, "", Message);
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
			Forward(Level, MessageKind.TaskError, "", Message);
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