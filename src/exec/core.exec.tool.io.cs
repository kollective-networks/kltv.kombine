/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	public delegate void StringReadEventHandler(string text);

	public class ChildProcessOutput {
		// Command line process that is executing and being
		// monitored by this class for stdout/stderr output.
		private Process runningProcess;

		// Buffers to hold characters read from either stdout, or stderr streams
		private StringBuilder stderrbuffer = new StringBuilder(4096);
		private StringBuilder stdoutbuffer = new StringBuilder(4096);
		

		public delegate void DataReceivedEventHandlerCustom(object sender, string data);
		
		public DataReceivedEventHandlerCustom? OutputDataReceived = null;

		public DataReceivedEventHandlerCustom? ErrorDataReceived = null;

		/// <summary>
		/// Initializes the IO manager with the supplied process.
		/// </summary>
		/// <param name="process">Process to be captured.</param>
		public ChildProcessOutput(Process process) {            
			this.runningProcess = process;
		}
	  
		/// <summary>
		/// Starts the background threads reading any output produced (standard output, standard error)
		/// that is produces by the running process.
		/// </summary>
		public void StartProcessOutputRead() {
			// Just to make sure there aren't previous threads running.
			StopMonitoringProcessOutput();
			if (runningProcess.StartInfo.RedirectStandardOutput == true) {
				stdoutThread = new Thread(new ThreadStart(ReadStandardOutputThreadMethod));
				stdoutThread.IsBackground = true;
				stdoutThread.Start();
			}
			if (runningProcess.StartInfo.RedirectStandardError == true) {
				stderrThread = new Thread(new ThreadStart(ReadStandardErrorThreadMethod));
				stderrThread.IsBackground = true;
				stderrThread.Start();
			}
		}

		public void WriteStdin(string text) {
			// Make sure we have a valid, running process
			if (CheckForValidProcess() == false)
				return;
			if (runningProcess.StartInfo.RedirectStandardInput == true)
				runningProcess.StandardInput.WriteLine(text);
		}
		

		
		/// <summary>
		/// Checks for valid (non-null Process), and optionally check to see if the process has exited.
		/// </summary>
		/// <returns>True, valid process was checked, false otherwise.</returns>
		private bool CheckForValidProcess() {
			if(runningProcess==null){
				Msg.PrintErrorMod("Childprocess IO running process is not defined. " ,".exec.io",Msg.LogLevels.Debug);
				return false;
			}
			if(runningProcess.HasExited){
				Msg.PrintWarningMod("Childprocess IO running process is already exited. " ,".exec.io",Msg.LogLevels.Debug);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Read characters from the supplied stream, and accumulate them in the
		/// 'streambuffer' variable until there are no more characters to read.
		/// </summary>
		/// <param name="firstCharRead">The first character that has already been read.</param>
		/// <param name="streamReader">The stream reader to read text from.</param>
		/// <param name="isstdout">if set to <c>true</c> the stream is assumed to be standard output, otherwise assumed to be standard error.</param>
		private void ReadStream(int firstCharRead, StreamReader streamReader, bool isstdout) {
			// Single character read from either stdout or stderr
			int ch;
			StringBuilder? sout = null;
			if (isstdout)
				sout = stdoutbuffer;
			else
				sout = stderrbuffer;

			sout.Length = 0;
			sout.Append((char)firstCharRead);
			while (streamReader.Peek() > -1) {
				// Read the character in the queue
				ch = streamReader.Read();
				// Accumulate the characters read in the stream buffer
				sout.Append((char)ch);
				// Send text one line at a time - much more efficient than  one character at a time
				if ( (ch == '\n') || (ch == '\r') )
					NotifyAndFlushBufferText(sout, isstdout);
			}
			// Flush any remaining text in the buffer
			NotifyAndFlushBufferText(sout, isstdout);
		}

		/// <summary>
		/// Invokes the OnStdoutTextRead (if isstdout==true)/ OnStderrTextRead events
		/// with the supplied streambuilder 'textbuffer', then clears
		/// textbuffer after event is invoked.
		/// </summary>
		/// <param name="textbuffer">The textbuffer containing the text string to pass to events.</param>
		/// <param name="isstdout">if set to true, the stdout event is invoked, otherwise stedrr event is invoked.</param>
		private void NotifyAndFlushBufferText(StringBuilder textbuffer, bool isstdout) {
			if (textbuffer.Length > 0) {
				if (isstdout){
					OutputDataReceived?.Invoke(this, textbuffer.ToString());
				}else{
					ErrorDataReceived?.Invoke(this, textbuffer.ToString());	
				}
				// 'Clear' the text buffer
				textbuffer.Length = 0;
			}
		}

		/// <summary>
		/// Method started in a background thread (stdoutThread) to manage the reading and reporting of
		/// standard output text produced by the running process.
		/// </summary>
		private void ReadStandardOutputThreadMethod() {
			try {
				// character read from stdout
				int ch;
				// The Read() method will block until something is available
				while (runningProcess != null && (ch = runningProcess.StandardOutput.Read()) > -1)
					ReadStream(ch, runningProcess.StandardOutput, true);
			} catch (Exception ex) {
				Msg.PrintMod("Childprocess IO error: " + ex.Message,".exec.io",Msg.LogLevels.Debug);
			}
		}

		/// <summary>
		/// Method started in a background thread (stderrThread) to manage the reading and reporting of
		/// standard error text produced by the running process.
		/// </summary>
		private void ReadStandardErrorThreadMethod() {
			try {
				// Character read from stderr
				int ch;
				// The Read() method will block until something is available
				while (runningProcess != null && (ch = runningProcess.StandardError.Read()) > -1)
					ReadStream(ch, runningProcess.StandardError, false);
			} catch (Exception ex) {
				Msg.PrintMod("Childprocess IO error: " + ex.Message,".exec.io",Msg.LogLevels.Debug);
			}
		}

		/// <summary>
		/// Stops both the standard input and stardard error background reader threads (via the Abort() method)        
		/// </summary>
		public void StopMonitoringProcessOutput() {
			try {
				if (stdoutThread != null)
					stdoutThread.Interrupt();
			} catch (Exception ex) {
				Msg.PrintMod("Childprocess IO stop stdout thread: " + ex.Message,".exec.io",Msg.LogLevels.Debug);
			}
			try {
				if (stderrThread != null)
					stderrThread.Interrupt();
			} catch (Exception ex) {
				Msg.PrintMod("Childprocess IO stop stderr thread: " + ex.Message,".exec.io",Msg.LogLevels.Debug);
			}
			stdoutThread = null;
			stderrThread = null;
		}
 

		private Thread? stdoutThread = null;

		private Thread? stderrThread = null;

	}
}