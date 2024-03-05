/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	/// <summary>
	/// Helper class to execute and obtain results from third party child processes
	/// </summary>
	internal class ChildProcess {

		/// <summary>
		/// This will be triggered if the user pressed ctrl+z so all executions will be skipped
		/// </summary>
		static public bool AbortExecution { get; private set; } = false;

		/// <summary>
		/// Static lock object to protect the list of running processes
		/// </summary>
		static object CurrentRunningProcessesLock = new object();
		
		/// <summary>
		/// Static list of running processes
		/// </summary>
		static private List<ChildProcess> CurrentRunningProcesses = new List<ChildProcess>();

		/// <summary>
		/// Destroys a childprocess instance. Is just intended to kill the child process if we exit
		/// </summary>
		static public void KillAllChilds(){
			// Signal to not accept more executions
			AbortExecution = true;
			// Lock the current running processes list and start killing
			lock(CurrentRunningProcessesLock) {
				Msg.PrintMod("Destroying all ChildProcesses", ".exec", Msg.LogLevels.Debug);
				foreach(ChildProcess proc in CurrentRunningProcesses) {
					try{
						Msg.PrintMod("Destroying ChildProcess: " + proc.Name, ".exec", Msg.LogLevels.Debug);
						proc.Kill();
					} catch(Exception ex){
						Msg.PrintMod("Error destroying ChildProcess: " + proc.Name + " Message: " + ex.Message, ".exec", Msg.LogLevels.Debug);
					}
				}
				CurrentRunningProcesses.Clear();
			}
			Msg.PrintMod("Destroying all ChildProcesses completed", ".exec", Msg.LogLevels.Debug);
		}

		/// <summary>
		/// Arguments that will be passed to the new process
		/// Arguments should be space separated in the same way you specify in a command line
		/// </summary>
		public string? Arguments { get; private set; } = null;

		/// <summary>
		/// Commandline to execute
		/// </summary>
		public string? CmdLine { get; private set; } = null;

		/// <summary>
		/// Name to identify this process (its extracted from the commandline and represent the filename without extension)
		/// </summary>
		public string? Name { get; private set; } = null;

		/// <summary>
		/// Indicates if the process should be launched through a shell. By default is false and use it on true
		/// is not recomended unless the process you want to run is a shell itself with an script or a link with schema for another application
		/// Anyway, always is better to use as command the self itself and pass the parameters as arguments
		/// This property should be avoided unless you know what you're doing and you're sure that you need it
		/// </summary>
		public bool UseShell { get; set; } = false;

		/// <summary>
		/// Exitcode value returned by the execution if its completed
		/// </summary>
		public int ExitCode { get; private set; } = 0;

		/// <summary>
		/// Time elapsed in milliseconds comprising the execution time from start to end.
		/// </summary>
		public long ProcessTime { get; private set; } = 0;

		/// <summary>
		/// If set to false we will let the application output without any control
		/// </summary>
		public bool OutputCapture { get; set; } = true;

		/// <summary>
		/// If set, we will capture using the streams at char level without any modification (no encoding) 
		/// </summary>
		public bool OutputCharCapture {get; set;} = true;

		/// <summary>
		/// A Public object Id which may be used to identify concurrent process operations (user property)
		/// </summary>
		public object Id { get; set;}

		/// <summary>
		/// A Public object for user data which may be used to hold any class / callback reference
		/// </summary>
		public object? UserData { get; set; }

		/// <summary>
		/// Delegate type to be called when the process is exited
		/// </summary>
		/// <param name="proc"></param>
		public delegate void pOnProcessExit(ChildProcess proc);

		/// <summary>
		/// Delegate property to be called once the process is finished
		/// </summary>
		public pOnProcessExit? OnProcessExit { get; set; }

		/// <summary>
		/// 
		/// </summary>
		private ProcessStartInfo? ProcessInfo;

		/// <summary>
		/// 
		/// </summary>
		private Process? ProcessHandle;

		/// <summary>
		/// 
		/// </summary>
		private List<string> OutputStd = new();

		/// <summary>
		/// 
		/// </summary>
		private List<string> OutputErr = new();

		/// <summary>
		/// 
		/// </summary>
		private bool ProcessExited = false;

		/// <summary>
		/// Class to intercept the output streams in raw format without altering the contents.
		/// </summary>
		ChildProcessOutput? Outstreams = null;

		/// <summary>
		///  Constructs a new Child process object
		/// </summary>
		/// <param name="CmdLine">Command line to execute. May include absolute/relative path and/or process name.</param>
		/// <param name="Arguments">Arguments to be passed into the new process</param>
		public ChildProcess(string CmdLine, string? Arguments) {
			this.Name = Path.GetFileNameWithoutExtension(CmdLine);
			this.CmdLine = CmdLine;
			if (Arguments != null)
				this.Arguments = Arguments;
			else
				this.Arguments = string.Empty;
			ProcessInfo = null;
			ProcessHandle = null;
			OnProcessExit = null;
			Id = string.Empty;
			UserData = null;
		}

		/// <summary>
		/// Fetch the contents of the stdout of the child process
		/// </summary>
		/// <returns>A string array with stdout content line by line</returns>
		public string[] GetOutput() {
			return OutputStd.ToArray();
		}

		/// <summary>
		/// Fetch the contents of the stderr of the child process
		/// </summary>
		/// <returns>A string array with stderr content line by line</returns>
		public string[] GetErrors() {
			return OutputErr.ToArray();
		}

		/// <summary>
		/// Wait for the process to exit without fetching the exit code
		/// </summary>
		/// <returns></returns>
		public bool WaitExit() {
			return WaitExit(out _);
		}
		/// <summary>
		/// Wait for the process to exit fetching the exit code
		/// </summary>
		/// <param name="pExitCode">Variable to receive the exit code</param>
		/// <returns>true if the process was launched, false otherwise</returns>
		public bool WaitExit(out int pExitCode) {
			if (ProcessHandle != null) {
				ProcessHandle.WaitForExit();
				pExitCode = ExitCode;
				return true; 
			}
			pExitCode = 0;
			return false;
		}

		/// <summary>
		/// Environment variables to be passed to the child process
		/// </summary>
		public Dictionary<string,string> Environment { get; set; } = new Dictionary<string, string>();


		/// <summary>
		/// Returns if the process is running or not
		/// </summary>
		/// <returns>True if stills running. False otherwise</returns>
		public bool IsRunning() {
			if (ProcessHandle != null) {
				if (ProcessHandle.HasExited == true)
					return false;
				return true;
			}
			return ProcessExited;
		}

		/// <summary>
		/// Kills the running instance and cancel the reading. 
		/// </summary>
		/// <returns></returns>
		public bool Kill() {
			if (IsRunning()) {
				try {
					if ( (!UseShell) && (ProcessHandle != null) ){
						if (OutputCapture) {
							if (OutputCharCapture){
								Outstreams?.StopMonitoringProcessOutput();
							} else {
								ProcessHandle.CancelOutputRead();
								ProcessHandle.CancelErrorRead();
							}
						} 
					}
					if (ProcessHandle != null) { 
						// We kill descendants as well.
						ProcessHandle.Kill(true);
						ProcessHandle.Dispose();
					}
				} catch (Exception ex) {
					Msg.PrintWarningMod("Error when killing a requested tool: " + this.Name + " Message:"+ex.Message, ".exec");
				}
			}
			return true;
		}

		/// <summary>
		/// Returns the exit code of the process
		/// If process has not been launched or not exited it returns false and -1
		/// Valid exit code and true otherwise
		/// </summary>
		/// <param name="pExitCode">Exit code or -1</param>
		/// <returns>False if process has not been launched nor finished. True otherwise</returns>
		public bool GetExitCode(out int pExitCode ) {
			if (ProcessExited == true) {
				pExitCode = ExitCode;
				return true;
			}
			pExitCode = -1;
			return false;
		}


		/// <summary>
		/// Launch a configured process
		/// </summary>
		/// <param name="KillPrevious">If previous process instances should be killed</param>
		/// <returns>True if the process was launched, false otherwise.</returns>
		public bool Launch(bool KillPrevious = false) {
			if (AbortExecution){
				Msg.PrintMod("Abort execution signal received. Skipping process: " + this.Name, ".exec", Msg.LogLevels.Debug);
				return false;
			}
			//
			// Remove already launched processes with the same name if required
			//
			if (KillPrevious)
				this.KillPrevious();
			//
			ProcessHandle = new Process();
			if (ProcessHandle != null) {
				//
				// create process info and process instances:
				//
				ProcessInfo = new ProcessStartInfo();
				if (ProcessInfo != null) {

					// Set the kind of launch, process and arguments
					//
					ProcessInfo.UseShellExecute = UseShell;
					ProcessInfo.Arguments = Arguments;
					ProcessInfo.FileName = CmdLine;
					Msg.PrintMod("Launching: " + CmdLine + " with arguments: " + Arguments, ".exec", Msg.LogLevels.Debug);
					// Disable error dialogs / window creation
					ProcessInfo.ErrorDialog = false;
					// Configure if we should capture the output or not					
					ProcessInfo.CreateNoWindow = false;
					if (OutputCapture)
						ProcessInfo.CreateNoWindow = true;
					// If a new window is created it should be hidden.
					ProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
					// Supplied credentials
					// -> ProcessInfo.Domain 
					// -> ProcessInfo.UserName
					// -> ProcessInfo.Password / PasswordInClearText
					// Set Environment if we've. If Environment is empty, we don't set it and we leave the current environment to be used
					if (!UseShell){
						// Under shell we cannot set the environment
						// This is just weird.
						if (Environment.Count > 0) {
							foreach(KeyValuePair<string,string> entry in Environment) {
								ProcessInfo.EnvironmentVariables[entry.Key] =  entry.Value;
							}
						}
					}
					// Set redirections
					// Redirections are not possible if we're launching the tool through the predefined OS Shell
					// so, in case of use shell, output trap will be disabled 
					if (!UseShell) {
						if (OutputCapture){
							ProcessInfo.RedirectStandardInput = true;
							ProcessInfo.RedirectStandardOutput = true;
							ProcessInfo.RedirectStandardError = true;
							ProcessInfo.StandardOutputEncoding = Encoding.ASCII;
							ProcessInfo.StandardErrorEncoding = Encoding.ASCII;
						}
					}
					// Exit handler
					//
					ProcessHandle.EnableRaisingEvents = true;
					ProcessHandle.Exited += new EventHandler(ExitedExecutionHandler);
					// 
					// Stderr / stdout handlers
					//
					
					if (!UseShell) {
						// OutputCharCapture
						if (OutputCapture){
							if (OutputCharCapture){
								Outstreams = new ChildProcessOutput(ProcessHandle);
								Outstreams.OutputDataReceived += StdoutOutputHandlerCustom;
								Outstreams.ErrorDataReceived += StderrOutputHandlerCustom;
							} else	{
								ProcessHandle.OutputDataReceived += new DataReceivedEventHandler(StdoutOutputHandler);
								ProcessHandle.ErrorDataReceived += new DataReceivedEventHandler(StderrOutputHandler);
							}
						}
					}
					//
					ProcessHandle.StartInfo = ProcessInfo;
					try {
						// Lock the current running processes list and start the process
						lock(CurrentRunningProcessesLock) {
							// Try to start the process
							if (ProcessHandle.Start() == false) {
								ProcessHandle.Dispose();
								return false;
							}
							// Add the process to the list of running processes
							Msg.PrintMod("Adding process to the list of running processes: " + this.Name, ".exec", Msg.LogLevels.Debug);
							CurrentRunningProcesses.Add(this);
							// If we're using shell, we cannot capture.
							if (!UseShell) {
								// If we're not using the shell, start reading the stderr / stdout of the child process
								if (OutputCapture){
									if (OutputCharCapture){
										Outstreams?.StartProcessOutputRead();
									} else {
										ProcessHandle.BeginErrorReadLine();
										ProcessHandle.BeginOutputReadLine();
									}
								}
							}
							ProcessStartTime = ProcessHandle.StartTime;
						}
					} catch (Exception ex) {
						Msg.PrintWarningMod("Error when executing a requested tool: " + this.Name,".exec");
						Msg.PrintWarningMod("Error Message: " + ex.Message,".exec");
						if (ProcessHandle != null)
							ProcessHandle.Dispose();
						ProcessHandle = null;
						return false;
					}
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Holds the start time since cannot be fetched on exiting delegate
		/// It raises an exception at least on macos
		/// </summary>
		private DateTime ProcessStartTime = DateTime.Now;


		/// <summary>
		/// Handle Exited event 
		/// Obtain exit code, signal process termination and dispose the process handle
		/// </summary>
		/// <param name="sender">Sender object</param>
		/// <param name="e">Event arguments</param>
		private void ExitedExecutionHandler(object? sender, System.EventArgs e) {
			Msg.PrintMod("Process exited: " + this.Name, ".exec", Msg.LogLevels.Debug);
			ProcessExited = true;
			if (ProcessHandle != null) {
				// Even if we're on the exit callback we need to wait because stderr / stdout maybe are not flushed
				// so, if we don't wait we will miss output lines for processes which runs quickly
				Msg.PrintMod("Waiting for process to exit (flushing): " + this.Name, ".exec", Msg.LogLevels.Debug);
				ProcessHandle.WaitForExit();
				Msg.PrintMod("Fetching exitcode: " + this.Name, ".exec", Msg.LogLevels.Debug);
				ExitCode = ProcessHandle.ExitCode;
				Msg.PrintMod("Fetching process time: " + this.Name, ".exec", Msg.LogLevels.Debug);
				ProcessTime = (long)Math.Round((ProcessHandle.ExitTime - ProcessStartTime).TotalMilliseconds);
				Msg.PrintMod("Process running time: "+ ProcessTime+"ms", ".exec", Msg.LogLevels.Debug);
			} else {
				// If the process was null, we signalize a normal error return code.
				Msg.PrintWarningMod("Process was null when exiting: " + this.Name, ".exec");
				ExitCode = -1;
				ProcessTime = -1;
			}
			Msg.PrintMod("Process exited with code: " + ExitCode + " and time: " + ProcessTime, ".exec", Msg.LogLevels.Debug);
			Msg.PrintMod("Calling delegate: " + this.Name, ".exec", Msg.LogLevels.Debug);
			// Call delegate if defined
			OnProcessExit?.Invoke(this);
			// Remove the process from the process list
			// It will be held from start adding to the list and setting initial parameters
			lock (CurrentRunningProcessesLock) {			
				// Dispose the process handle if required
				if (ProcessHandle != null) {
					ProcessHandle.Dispose();
					ProcessHandle = null;
				}
				CurrentRunningProcesses.Remove(this);
			}
		}

		/// <summary>
		/// Delegate type to be called when a new line is received from the child process
		/// </summary>
		/// <param name="line"></param>
		public delegate void OnOutContent(string line);
		
		/// <summary>
		/// Set a delegate to be called when a new line is received from the child process
		/// </summary>
		public OnOutContent? OnStderrLine {get; set;} = null;

		/// <summary>
		/// Set a delegate to be called when a new line is received from the child process
		/// </summary>
		public OnOutContent? OnStdoutLine {get; set;} = null;

		/// <summary>
		/// Std Output Handler
		/// </summary>
		/// <param name="sendingProcess">object representing the process</param>
		/// <param name="outLine">data</param>
		private void StdoutOutputHandler(object sendingProcess, DataReceivedEventArgs outLine) {
			StdoutOutputHandlerCustom(sendingProcess,outLine.Data);
		}
		private void StdoutOutputHandlerCustom(object sendingProcess, string? outLine) {
			if (!String.IsNullOrEmpty(outLine)) {
				OutputStd.Add(outLine);
				OnStdoutLine?.Invoke(outLine);
			}
		}

		/// <summary>
		/// Std Error Handler
		/// </summary>
		/// <param name="sendingProcess">object representing the process</param>
		/// <param name="outLine">data</param>
		private void StderrOutputHandler(object sendingProcess, DataReceivedEventArgs outLine) {
			StderrOutputHandlerCustom(sendingProcess,outLine.Data);
		}
		private void StderrOutputHandlerCustom(object sendingProcess, string? outLine) {
			if (!String.IsNullOrEmpty(outLine)) {
				OutputErr.Add(outLine);
				OnStderrLine?.Invoke(outLine);
			}
		}

		/// <summary>
		/// Kill all the previous instances which process name equals to this one
		/// </summary>
		private void KillPrevious() {
			Process[] processes = Process.GetProcesses();
			foreach (Process ProcessInstance in processes) {
				if (ProcessInstance.ProcessName == Name) {
					Msg.PrintMod("Kill process "+ProcessInstance.Id+":"+ProcessInstance.ProcessName,".exec",Msg.LogLevels.Debug);
					ProcessInstance.Kill();
				}
			}
		}
	}
}
