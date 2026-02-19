/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Reflection;

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
		/// Resolves a Glob pattern to a list of files.
		/// It uses the current working directory as the base path to resolve the pattern.
		/// </summary>
		/// <param name="folder">Folder to be used as the base path.</param>
		/// <param name="pattern">Pattern to be resolved</param>
		/// <returns>A KList with the resolved files.</returns>
		public static KList Glob(string folder, string pattern) {
			Matcher matcher = new Matcher();
			matcher.AddInclude(pattern);
			DirectoryInfo d = new DirectoryInfo(folder);
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
			try {
				KValue ret = Path.GetFullPath(path);
				return ret;
			} catch (Exception ex) {
				Msg.PrintErrorMod("Error resolving path: " + path, ".statics.realpath", Msg.LogLevels.Verbose);
				Msg.PrintErrorMod(ex.Message, ".statics.realpath", Msg.LogLevels.Verbose);
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
		/// <param name="exitonerror">if true, exits the script on error.</param>
		/// <param name="changedir">if true, changes the current working directory to the script folder.</param>
		/// <param name="search">if true, search for the script in different routes.</param>
		/// <returns>The return code from the script execution.</returns>
		public static int Kombine(string script, string action, string[]? args = null, bool exitonerror = true, bool changedir = true, bool search = true) {
			if (search == true) {
				string? found = Folders.ResolveFilename(script);
				if (found != null)
					script = found;
			}
			int retCode = KombineMain.RunScript(script, action, args, changedir);
			if (exitonerror) {
				if (retCode != 0) {
					Msg.PrintAndAbortMod("Script execution returned error: " + script + " exitcode:" + retCode, ".statics.kombine", Msg.LogLevels.Verbose);
					return retCode;
				}
			}
			return retCode;
		}

		/// <summary>
		/// Executes a command line tool.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <param name="args">argument array for the command</param>
		/// <param name="showoutput">if true, shows the output of the command.</param>
		/// <returns>Exitcode from the command.</returns>
		public static int Exec(string command, string[]? args = null, bool showoutput = false) {
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
		public static int Exec(string command, string? args = null, bool showoutput = false) {
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

		/// <summary>
		/// Function to escape parameters for command line processing
		/// </summary>
		/// <param name="arg">argument to be escaped</param>
		/// <returns>the escaped argument</returns>
		public static string ArgEscape(string arg) {
			// Right now just escapes the double quote
			string result = arg.Replace("\"", "\\\"");
			return result;
		}


		/// <summary>
		/// Cast an object to another type trying to copy as much as possible.
		/// </summary>
		/// <typeparam name="T">Returned type</typeparam>
		/// <param name="myobj">Object to be casted</param>
		/// <returns>A new created object of the new type or null if invalid.</returns>
		public static T? Cast<T>(object? myobj) {
			if (myobj == null)
				return default(T);
			Type objectType = myobj.GetType();
			Type target = typeof(T);
			var x = Activator.CreateInstance(target, false);
			var z = from source in objectType.GetMembers().ToList()
					where source.MemberType == MemberTypes.Property
					select source;
			var d = from source in target.GetMembers().ToList()
					where source.MemberType == MemberTypes.Property
					select source;
			List<MemberInfo> members = d.Where(memberInfo => d.Select(c => c.Name)
			.ToList().Contains(memberInfo.Name)).ToList();
			PropertyInfo? propertyInfo;
			object? value;
			foreach (var memberInfo in members) {
				propertyInfo = typeof(T).GetProperty(memberInfo.Name);
				value = myobj.GetType().GetProperty(memberInfo.Name)?.GetValue(myobj, null);
				propertyInfo?.SetValue(x, value, null);
			}
			return (T?)x;
		}


		/// <summary>
		/// Retuns the Kombine version string.
		/// </summary>
		/// <returns>The string in dot formated</returns>
		public static string MkbVersion() {
			return KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build;
		}

		/// <summary>
		/// Returns the major and minor version numbers of the application as a string in the format "Major.Minor".
		/// </summary>
		/// <returns>A string representing the application's major and minor version numbers, separated by a period. For example,
		/// "2.5".</returns>
		public static string MkbVersionShort() {
			return KombineMain.Version.Major + "." + KombineMain.Version.Minor;
		}


		/// <summary>
		/// Returns the major version number.
		/// </summary>
		/// <returns>The major version</returns>
		public static int MkbMajorVersion() {
			return int.Parse(KombineMain.Version.Major);
		}

		/// <summary>
		/// Returns the minor version number.
		/// </summary>
		/// <returns>The minor version</returns>
		public static int MkbMinorVersion() {
			return int.Parse(KombineMain.Version.Minor);

		}

		/// <summary>
		/// Returns the najor+minor version as a hex value.
		/// </summary>
		/// <returns>The version</returns>
		public static int MkbHexVersion() {
			return KombineMain.Version.HexVersion;
		}

	}
}
