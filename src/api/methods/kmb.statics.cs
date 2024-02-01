/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Static methods published into the script.
	/// They can be used directly without any class decoration.
	/// </summary>
	public static class Statics {

		/// <summary>
		/// Resolves a Glob pattern to a list of files.
		/// It uses the current working directory as the base path to resolve the pattern.
		/// </summary>
		/// <param name="pattern">Pattern to be resolved</param>
		/// <returns>A KList with the resolved files.</returns>
		public static KList Glob(string pattern) {
			Matcher matcher = new Matcher();
			matcher.AddInclude(pattern);
			DirectoryInfo d = new DirectoryInfo(Folders.CurrentWorkingFolder);
			DirectoryInfoWrapper dir = new DirectoryInfoWrapper(d);
			PatternMatchingResult res = matcher.Execute(dir);
			KList list = new KList();
			if (res.HasMatches) {
				foreach (FilePatternMatch p in res.Files) {
					Msg.PrintMod("Detected file to include in glob: " + p.Path, "statics.glob", Msg.LogLevels.Verbose);
					list.Add(p.Path);
				}
			}
			return list;
		}

		/// <summary>
		/// Returns the real path of the given path.
		/// It is converted to the underlying OS if required.
		/// If its a relative path, it is converted to an absolute path.
		/// </summary>
		/// <param name="path">Path to be evaluated.</param>
		/// <returns>Realpath returned</returns>
		public static KValue RealPath(KValue path) {
			try{
				KValue ret = Path.GetFullPath(path);
				return ret;
			} catch (Exception ex){
				Msg.PrintErrorMod("Error resolving path: "+path,".statics.realpath",Msg.LogLevels.Verbose);
				Msg.PrintErrorMod(ex.Message,".statics.realpath",Msg.LogLevels.Verbose);
				return string.Empty;
			}
		}


		/// <summary>
		/// Executes a child Kombine script.
		/// This method do not spawn a new process, it just executes the script in the current process.
		/// To exchange information check for import/export methods on kvalues and the shared object api.
		/// </summary>
		/// <param name="script">script to be executed.</param>
		/// <param name="action">action to be executed.</param>
		/// <param name="args">parameters to the action.</param>
		/// <param name="changedir">if true, changes the current working directory to the script folder.</param>
		/// <param name="search">if true, search for the script in different routes.</param>
		/// <returns>The return code from the script execution.</returns>
		public static int Kombine(string script,string action, string[]? args = null,bool changedir = true,bool search=true) {
			if (search == true) {
				string? found = Folders.ResolveFilename(script);
				if (found != null)
					script = found;
			}
			return KombineMain.RunScript(script, action, args, changedir);
		}

		/// <summary>
		/// Executes a command line tool.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <param name="args">argument array for the command</param>
		/// <param name="showoutput">if true, shows the output of the command.</param>
		/// <returns>Exitcode from the command.</returns>
		public static int Exec(string command, string[]? args = null,bool showoutput=false) {
			Tool tool = new Tool(command);
			tool.CaptureOutput = showoutput;
			Tool.ToolResult result = tool.CommandSync(command, args, null);
			return result.ExitCode;
		}

		/// <summary>
		/// Executes a command line tool.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <param name="args">argument array for the command</param>
		/// <param name="showoutput">if true, shows the output of the command.</param>
		/// <returns>Exitcode from the command.</returns>
		public static int Exec(string command,string? args = null,bool showoutput=false){
			Tool tool = new Tool(command);
			tool.CaptureOutput = showoutput;
			Tool.ToolResult result = tool.CommandSync(command, args, null);
			return result.ExitCode;
		}

		/// <summary>
		/// Executes a command line tool using the system shell.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <param name="args">Arguments for the command.</param>
		/// <returns>Exitcode from the command.</returns>
		public static int Shell(string command, string[]? args = null) {
			Tool tool = new Tool(command);
			tool.UseShell = true;
			Tool.ToolResult result = tool.CommandSync(command, args, null);
			return result.ExitCode;
		}
		/// <summary>
		/// Executes a command line tool using the system shell.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <param name="args">Arguments for the command.</param>
		/// <returns>Exitcode from the command.</returns>
		public static int Shell(string command, string? args = null) {
			Tool tool = new Tool(command);
			tool.UseShell = true;
			Tool.ToolResult result = tool.CommandSync(command, args, null);
			return result.ExitCode;
		}
	}
}