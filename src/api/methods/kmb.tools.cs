/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Wraps a tool interaction (status / result / launch / version)
	/// </summary>
	public partial class Tool {

		/// <summary>
		/// Tool Constructor
		/// </summary>
		/// <param name="ToolTag">Tag name for the tool</param>
		public Tool(string ToolTag) {
			Status = ToolStatus.Undefined;
			this.ToolTag = ToolTag;
			PendingAsyncTasks = 0;
			ExpectedExitCode = 0;
			ExpectedExitCode = 0;
		}

		/// <summary>
		/// Tool Constructor
		/// </summary>
		public Tool() {
			Status = ToolStatus.Undefined;
			ToolTag = string.Empty;
			PendingAsyncTasks = 0;
		}

		/// <summary>
		/// Valid states for tools
		/// </summary>
		public enum ToolStatus {
			/// <summary>
			/// Tool execution was fine
			/// </summary>
			Success = 1,
			/// <summary>
			/// Tool execution returned errors
			/// </summary>
			Failed = 2,
			/// <summary>
			/// Tool execution returned warnings
			/// </summary>
			Warnings = 3,
			/// <summary>
			/// Tool execution was fine but nothing has been done
			/// </summary>
			NoChanges = 4,
			/// <summary>
			/// Tool is executing so status is pending
			/// </summary>
			Pending = 5,
			/// <summary>
			/// Undefined state before execution
			/// </summary>
			Undefined = 0
		}

		/// <summary>
		/// Expected exit code for the tool. Zero by default
		/// </summary>
		public int ExpectedExitCode { get; set; }

		/// <summary>
		/// Holds the tool status
		/// </summary>
		public ToolStatus Status { get; protected set; }

		/// <summary>
		/// Holds the tool tag. Its used on logs but also as indexer for data stored in the state
		/// </summary>
		public string ToolTag { get; protected set; }

		/// <summary>
		/// If true, the tool will be launched using the system shell
		/// It is only available for sync commands
		/// </summary>
		public bool UseShell { get; set; } = false;

		/// <summary>
		/// Number of concurrent process launched by the tool
		/// </summary>
		public uint ConcurrentCommands { get; set; } = 1;

		/// <summary>
		/// If true, the tool will show the output in the console
		/// </summary>
		public bool CaptureOutput {get;set;} = false;

		/// <summary>
		/// Internal object to lock the tool for output order.
		/// </summary>
		private object Lock = new object();


		/// <summary>
		/// Launch a tool in sync way. 
		/// </summary>
		/// <param name="cmd">Command to launch</param>
		/// <param name="args">Arguments for the tool</param>
		/// <param name="id">Optional, id to be attached in the child process control code.</param>
		/// <returns>ToolResult type with all the executing information.</returns>
		public ToolResult CommandSync(string cmd,string[]? args,object? id = null) {
			string argsstr = string.Empty;
			if (args != null) {
				foreach (string s in args) {
					argsstr += " " + s;
				}
			}
			return CommandSync(cmd, argsstr, id);
		}

		/// <summary>
		/// Launch a tool in sync way. 
		/// </summary>
		/// <param name="cmd">Command to launch</param>
		/// <param name="args">Arguments for the tool</param>
		/// <param name="id">Optional, id to be attached in the child process control code.</param>
		/// <returns>ToolResult type with all the executing information.</returns>
		public ToolResult CommandSync(string cmd, string? args,object? id = null) {

			ToolResult res = new(Array.Empty<string>(), Array.Empty<string>(), ToolStatus.Failed, -1);
			ChildProcess p = new ChildProcess(cmd, args);
			// Set the environment for the tool
			if (KombineMain.CurrentRunningScript != null)
				p.Environment = KombineMain.CurrentRunningScript.State.Environment;
			if (UseShell == true)
				p.UseShell = true;
			if (CaptureOutput){
				p.OnStdoutLine += (string s) => { 
					Msg.RawPrint(s); 
				};
				p.OnStderrLine += (string s) => { 
					Msg.RawPrint(s); 
				};
			}
			if (id != null)
				p.Id = id;
			if (p.Launch() == false) {
				Msg.PrintWarningMod("[err] Error launching sync. Could not launch.", "."+ToolTag,Msg.LogLevels.Verbose);
				return res;
			}
			if (p.WaitExit(out int ExitCode) == false) {
				Msg.PrintWarningMod("[err] Error launching sync. Cannot wait for the process to finish.", "."+ToolTag,Msg.LogLevels.Verbose);
				return res;
			}
			if (ExitCode != 0) {
				Msg.PrintWarningMod("[err] Error launching sync. Executed with errors.", "."+ToolTag, Msg.LogLevels.Verbose);
				res = new(p.GetOutput(),p.GetErrors(), ToolStatus.Failed, ExitCode, p.Id);
				return res;
			}
			res = new(p.GetOutput(), p.GetErrors(), ToolStatus.Success, ExitCode, p.Id);
			return res;
		}


		/// <summary>
		/// Holds an async command task to be executed
		/// </summary>
		private class AsyncCommand {
			public string cmd;
			public string args;
			public object? id;
			public CommandAsyncResults? callback;
			public ToolResult? res;
			public AsyncCommand(string cmd,string args,object? id,CommandAsyncResults? callback) {
				this.cmd = cmd;
				this.args = args;
				this.id = id;
				this.callback = callback;
				this.res = null;
			}
		}

		/// <summary>
		/// List of async commands to be executed
		/// </summary>
		private List<AsyncCommand>? asyncCommands = null;

		/// <summary>
		/// Queue a command to be executed. It will be executed in the order they are queued
		/// The number of concurrent commands could be changed using the ConcurrentCommands property
		/// </summary>
		/// <param name="cmd">Command to execute. It may be on path or specify the exact location</param>
		/// <param name="args">Arguments to pass</param>
		/// <param name="id">Id for this command. Useful to do the relationship when the callback is called when command is complete.
		/// Can be anything. For example the source file name passed to the tool.
		/// </param>
		/// <param name="callback">Callback to be called when the command is completed. See CommandAsyncResults delegate.</param>
		public void QueueCommand(string cmd,string args,object? id,CommandAsyncResults? callback) { 
			if (asyncCommands == null)
				asyncCommands = new List<AsyncCommand>();
			AsyncCommand c = new AsyncCommand(cmd, args, id, callback);
			asyncCommands.Add(c);
			Msg.PrintMod("Added an async command to the queue. Total: " + asyncCommands.Count.ToString(), ".tool", Msg.LogLevels.Verbose);
		}

		/// <summary>
		/// Execute all the queued commands. It will respect the concurrentcommands property
		/// It will block until all the commands are finished
		/// </summary>
		/// <param name="AggregateResults">If true, it will return a global result with the status of all the commands. </param>
		/// <returns>ToolResult object is returned. If there are no commands, result is set to nochanges.</returns>
		public ToolResult ExecuteCommands(bool AggregateResults = false) {
			if (asyncCommands == null) {
				return ToolResult.DefaultNoChanges();
			}
			Msg.PrintMod("Starting async commands.", ".tool", Msg.LogLevels.Debug);
			// Execute all the commands but wait for the concurrent build limit
			foreach (AsyncCommand c in asyncCommands) {
				CommandAsync(c.cmd, c.args, c.callback, c.id);
				CommandAsyncWaitAll(ConcurrentCommands);
			}
			// Wait for all the commands to finish
			CommandAsyncWaitAll(0);
			Msg.PrintMod("Finished all the async commands.", ".tool", Msg.LogLevels.Debug);
			// Fetch the results / elaborate global result
			ToolStatus status = ToolStatus.Success;
			int offendingExitCode = 0;
			List<string> astdout = new List<string>();
			List<string> astderr = new List<string>();
			foreach (AsyncCommand c in asyncCommands) {
				if (c.res == null) {
					Msg.PrintWarningMod("Error fetching results for async command. Looks like was not executed.", ".tool." + ToolTag, Msg.LogLevels.Verbose);
					continue;
				}
				// If some result was failed, switch global to failed and copy the exit code
				if (c.res.Status == ToolStatus.Failed) {
					status = ToolStatus.Failed;
					offendingExitCode = c.res.ExitCode;
				}
				// If some result was warnings, switch global to warnings if it was success not error
				// Level always raise, never lower
				if (c.res.Status == ToolStatus.Warnings) {
					if (status == ToolStatus.Success)
						status = ToolStatus.Warnings;
				}
				if (AggregateResults) {
					astdout.AddRange(c.res.Stdout);
					astderr.AddRange(c.res.Stderr);
				}
			}
			// Generate the global result and return
			ToolResult res = new ToolResult(astdout.ToArray(), astderr.ToArray(), status, offendingExitCode);
			// Remove our async commands list
			asyncCommands.Clear();
			asyncCommands = null;
			// And return the result
			return res;
		}


		/// <summary>
		/// Internal counter for async pending tasks
		/// </summary>
		private UInt64 PendingAsyncTasks;

		/// <summary>
		/// Delegate to deliver tool execution results
		/// </summary>
		/// <param name="results"></param>
		public delegate void CommandAsyncResults(ref ToolResult results);

		/// <summary>
		/// Launch a tool in async way. 
		/// </summary>
		/// <param name="cmd">Command to launc</param>
		/// <param name="args">Arguments for the tool</param>
		/// <param name="CommandAsyncResult">Delegate to be called once the process is done</param>
		/// <param name="id">Optional, id to be attached in the child process control code. </param>
		/// <returns>ToolResult type with partial execution information. </returns>
		public ToolResult CommandAsync(string cmd,string args,CommandAsyncResults? CommandAsyncResult,object? id = null) {
			// Increase the number of pending tasks
			Interlocked.Increment(ref PendingAsyncTasks);
			// Prepare the result and the process to be launched
			ToolResult res = new(Array.Empty<string>(), Array.Empty<string>(), ToolStatus.Failed, -1);
			ChildProcess p = new ChildProcess(cmd, args);
			// Set the environment for the tool
			if (KombineMain.CurrentRunningScript != null)
				p.Environment = KombineMain.CurrentRunningScript.State.Environment;
			p.OnProcessExit += CommandAsyncFinished;
			if (CaptureOutput){
				p.OnStdoutLine += (string s) => { 
					Msg.RawPrint(s); 
				};
				p.OnStderrLine += (string s) => { 
					Msg.RawPrint(s); 
				};
			}
			if (id != null)
				p.Id = id;
			if (CommandAsyncResult != null)
				p.UserData = CommandAsyncResult;
			if (p.Launch() == false) {
				Msg.PrintWarningMod("Error launching async. Could not launch.", ".tool." + ToolTag,Msg.LogLevels.Verbose);
				Interlocked.Decrement(ref PendingAsyncTasks);
				return res;
			}
			res = new(Array.Empty<string>(), Array.Empty<string>(), ToolStatus.Pending, 0);
			return res;
		}

		/// <summary>
		/// Its being called after the command execution in async way. It collects the data and calls the result delegate
		/// </summary>
		/// <param name="proc"></param>
		private void CommandAsyncFinished(ChildProcess proc) {
			// Set the state was success
			ToolStatus status = ToolStatus.Success;
			// Check against the desired exit code
			if (proc.ExitCode != ExpectedExitCode) {
				status = ToolStatus.Failed;
			}
			// Prepare the tool result information
			ToolResult res = new(proc.GetOutput(), proc.GetErrors(), status, proc.ExitCode, proc.Id);
			// Lock the output
			lock (Lock) {
				// Call the delegate if any and let it modify the result if required
				(proc.UserData as CommandAsyncResults)?.Invoke(ref res);
				// And finally append the result to our tasks just if its required to be evaluated later
				if (asyncCommands != null) {
					foreach (AsyncCommand cmd in asyncCommands) {
						if (cmd.id == proc.Id) {
							cmd.res = res;
						}
					}
				}
			}
			// And finally this task has been finished
			Interlocked.Decrement(ref PendingAsyncTasks);
			return;
		}

		/// <summary>
		/// Waits for all the pending tasks to complete (or by given threshold) 
		/// </summary>
		public void CommandAsyncWaitAll(uint Limit = 0) {
			UInt64 Pending = Interlocked.Read(ref PendingAsyncTasks);
			// TODO: Add some mechanism so this can be interrupted by timeout
			// TODO: Commented code is present only for concurrency debug.
			while (Pending > Limit) {
				//Msg.PrintMod("Waiting for pending tasks to finish. Pending: " + PendingAsyncTasks.ToString(),".tool",Msg.LogLevels.Debug);
				Thread.Sleep(10);
				Pending = Interlocked.Read(ref PendingAsyncTasks);
			}
			//Msg.PrintMod("Nothing to wait. Pending: " + Pending +" Limit:"+Limit, ".tool", Msg.LogLevels.Debug);
		}
	}
}