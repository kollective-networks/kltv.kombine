/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Collections;
using System.Text.RegularExpressions;
using Kltv.Kombine.Types;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// The environment of the script: the variables every tool and child script receives. It starts
	/// as a copy of the environment of the process and every child script gets a copy of its parent's.
	/// The facility is generic and knows no toolchain: what to clear or to set is the knowledge of the
	/// caller, an environment extension carries its own list. On Windows the names ignore the case, as
	/// the system does; elsewhere they are exact.
	/// Failures are reported through the return value and LastError; nothing is printed at normal level
	/// except the aborts asked with ExitIfError.
	/// </summary>
	public static class Env {

		/// <summary>
		/// Last failure of an Env call. Reset at the start of every call, set when it fails.
		/// </summary>
		public static ApiError LastError { get; private set; } = ApiError.None;

		/// <summary>
		/// The names the last Load set, changed or removed.
		/// </summary>
		public static KList LastLoaded { get; private set; } = new KList();

		/// <summary>
		/// The environment of the process, used when no script runs (the reserved actions).
		/// </summary>
		private static Dictionary<string, string>? processEnvironment = null;

		/// <summary>
		/// The environment in use: the one of the running script, or a copy of the process one.
		/// </summary>
		private static Dictionary<string, string> Variables {
			get {
				if (KombineMain.CurrentRunningScript != null)
					return KombineMain.CurrentRunningScript.State.Environment;
				if (processEnvironment == null) {
					processEnvironment = KombineState.NewEnvironment();
					foreach (DictionaryEntry de in System.Environment.GetEnvironmentVariables()) {
						if (de.Key is string k && de.Value is string v)
							processEnvironment[k] = v;
					}
				}
				return processEnvironment;
			}
		}

		/// <summary>
		/// The comparison of the names: the case is ignored on Windows, exact elsewhere.
		/// </summary>
		private static StringComparison NameComparison {
			get { return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; }
		}

		/// <summary>
		/// Records a failure and logs it at verbose level.
		/// </summary>
		private static bool Fail(ErrorCode code, string message, string source, bool ExitIfError = false) {
			LastError = new ApiError(code, message, source);
			if (ExitIfError)
				Msg.PrintAndAbortMod(LastError.ToString(), ".env");
			Msg.PrintWarningMod(LastError.ToString(), ".env", Msg.LogLevels.Verbose);
			return false;
		}

		#region Variables

		/// <summary>
		/// True when the variable exists.
		/// </summary>
		/// <param name="name">Name of the variable.</param>
		public static bool Has(KValue name) {
			return Variables.ContainsKey(name);
		}

		/// <summary>
		/// The value of a variable, or the default when it does not exist. Nothing aborts here, unlike
		/// KValue.Import: a missing variable is a normal answer.
		/// </summary>
		/// <param name="name">Name of the variable.</param>
		/// <param name="defvalue">Value returned when the variable does not exist, empty by default.</param>
		public static KValue Get(KValue name, KValue? defvalue = null) {
			if (Variables.TryGetValue(name, out string? value))
				return value;
			return defvalue ?? new KValue();
		}

		/// <summary>
		/// Sets a variable, created or replaced.
		/// </summary>
		/// <param name="name">Name of the variable.</param>
		/// <param name="value">Its value.</param>
		/// <returns>True if set, false for an empty name (see LastError).</returns>
		public static bool Set(KValue name, KValue value) {
			LastError = ApiError.None;
			if (name.IsEmpty())
				return Fail(ErrorCode.InvalidArgument, "The name of the variable is empty", string.Empty);
			Variables[name] = value;
			return true;
		}

		/// <summary>
		/// Removes a variable.
		/// </summary>
		/// <param name="name">Name of the variable.</param>
		/// <returns>True if it was there, false otherwise. Never an error.</returns>
		public static bool Remove(KValue name) {
			return Variables.Remove(name);
		}

		/// <summary>
		/// Removes the variables of the list, by name or by wildcard ("VSCMD_*", "?_PATH"). The list is
		/// the caller's: the engine knows no toolchain.
		/// </summary>
		/// <param name="names">Names and wildcards to remove.</param>
		/// <returns>The names removed, in the order they were found.</returns>
		public static KList Clean(KList names) {
			KList removed = new KList();
			foreach (KValue pattern in names) {
				if (pattern.IsEmpty())
					continue;
				Regex regex = Wildcard(pattern);
				List<string> matches = Variables.Keys.Where(k => regex.IsMatch(k)).ToList();
				foreach (string name in matches) {
					Variables.Remove(name);
					removed.Add(name);
				}
			}
			return removed;
		}

		/// <summary>
		/// Removes the variables given, by name or by wildcard. See Clean(KList).
		/// </summary>
		/// <param name="names">Names and wildcards to remove.</param>
		/// <returns>The names removed, in the order they were found.</returns>
		public static KList Clean(params string[] names) {
			KList list = new KList();
			foreach (string n in names)
				list.Add(n);
			return Clean(list);
		}

		/// <summary>
		/// A copy of the environment, to be given back to Restore.
		/// </summary>
		public static Dictionary<string, string> Snapshot() {
			return Variables.Clone();
		}

		/// <summary>
		/// Replaces the environment with a snapshot: every variable of the snapshot with its value, and
		/// nothing else.
		/// </summary>
		/// <param name="snapshot">The copy taken with Snapshot.</param>
		/// <returns>True if restored, false for a null snapshot (see LastError).</returns>
		public static bool Restore(Dictionary<string, string>? snapshot) {
			LastError = ApiError.None;
			if (snapshot == null)
				return Fail(ErrorCode.InvalidArgument, "The snapshot to restore is null", string.Empty);
			Dictionary<string, string> variables = Variables;
			variables.Clear();
			foreach (KeyValuePair<string, string> kv in snapshot)
				variables[kv.Key] = kv.Value;
			return true;
		}

		/// <summary>
		/// Aborts, or returns false, when a variable is missing: a script states what it needs.
		/// </summary>
		/// <param name="name">Name of the variable.</param>
		/// <param name="ExitIfError">True, the default, aborts the script with the reason; false returns false (see LastError).</param>
		public static bool Require(KValue name, bool ExitIfError = true) {
			LastError = ApiError.None;
			if (Variables.ContainsKey(name))
				return true;
			return Fail(ErrorCode.NotFound, "The environment variable is required and does not exist", name, ExitIfError);
		}

		/// <summary>
		/// Aborts, or returns false, when a variable is present: a script states what must not leak into
		/// its tools (an INCLUDE from another toolchain).
		/// </summary>
		/// <param name="name">Name of the variable.</param>
		/// <param name="ExitIfError">True, the default, aborts the script with the reason; false returns false (see LastError).</param>
		public static bool Forbid(KValue name, bool ExitIfError = true) {
			LastError = ApiError.None;
			if (!Variables.ContainsKey(name))
				return true;
			return Fail(ErrorCode.AlreadyExists, "The environment variable must not be present and it is", name, ExitIfError);
		}

		#endregion

		#region Path

		/// <summary>
		/// The name of the path variable in the environment, "PATH" when it does not exist yet.
		/// </summary>
		private static string PathName {
			get {
				foreach (string k in Variables.Keys)
					if (string.Equals(k, "PATH", StringComparison.OrdinalIgnoreCase))
						return k;
				return "PATH";
			}
		}

		/// <summary>
		/// The entries of the path variable, in order, empty entries dropped.
		/// </summary>
		public static KList Paths() {
			KList list = new KList();
			foreach (string entry in Get(PathName).ToString().Split(Path.PathSeparator)) {
				if (entry.Trim().Length > 0)
					list.Add(entry.Trim());
			}
			return list;
		}

		/// <summary>
		/// Replaces the path variable with the entries given.
		/// </summary>
		/// <param name="entries">The entries, in order.</param>
		public static bool SetPaths(KList entries) {
			return Set(PathName, string.Join(Path.PathSeparator, entries.Select(e => e.ToString())));
		}

		/// <summary>
		/// Puts a folder first in the path variable. A folder already in it is moved first.
		/// </summary>
		/// <param name="folder">The folder.</param>
		public static bool PrependPath(KValue folder) {
			LastError = ApiError.None;
			if (folder.IsEmpty())
				return Fail(ErrorCode.InvalidArgument, "The folder to add to the path is empty", string.Empty);
			KList entries = new KList();
			entries.Add(folder);
			foreach (KValue e in Paths()) {
				if (!SamePath(e, folder))
					entries.Add(e);
			}
			return SetPaths(entries);
		}

		/// <summary>
		/// Puts a folder last in the path variable, unless it is already in it.
		/// </summary>
		/// <param name="folder">The folder.</param>
		public static bool AppendPath(KValue folder) {
			LastError = ApiError.None;
			if (folder.IsEmpty())
				return Fail(ErrorCode.InvalidArgument, "The folder to add to the path is empty", string.Empty);
			KList entries = Paths();
			if (entries.Any(e => SamePath(e, folder)))
				return true;
			entries.Add(folder);
			return SetPaths(entries);
		}

		/// <summary>
		/// Removes the entries of the path variable that match a name or a wildcard
		/// ("*\Microsoft Visual Studio\*").
		/// </summary>
		/// <param name="pattern">The name or wildcard.</param>
		/// <returns>The entries removed, in order.</returns>
		public static KList RemovePath(KValue pattern) {
			KList removed = new KList();
			if (pattern.IsEmpty())
				return removed;
			Regex regex = Wildcard(pattern);
			KList kept = new KList();
			foreach (KValue entry in Paths()) {
				if (regex.IsMatch(entry) || SamePath(entry, pattern))
					removed.Add(entry);
				else
					kept.Add(entry);
			}
			if (removed.Count() > 0)
				SetPaths(kept);
			return removed;
		}

		/// <summary>
		/// Every file the path variable would run for a tool, in the order of the path: the name as
		/// given, and on Windows with every extension of PATHEXT as well. A name with a folder is
		/// checked as it is. Empty when nothing is found.
		/// </summary>
		/// <param name="tool">The tool, a name or a path.</param>
		public static KList Which(KValue tool) {
			KList found = new KList();
			string name = tool;
			if (name.Trim().Length == 0)
				return found;
			List<string> extensions = new List<string> { string.Empty };
			if (OperatingSystem.IsWindows()) {
				string pathext = Get("PATHEXT", ".COM;.EXE;.BAT;.CMD");
				foreach (string e in pathext.Split(';'))
					if (e.Trim().Length > 0)
						extensions.Add(e.Trim());
			}
			if (name.Contains('/') || name.Contains('\\')) {
				foreach (string e in extensions) {
					string candidate = Path.GetFullPath(name + e);
					if (File.Exists(candidate) && !found.Contains(candidate))
						found.Add(candidate);
				}
				return found;
			}
			foreach (KValue folder in Paths()) {
				foreach (string e in extensions) {
					string candidate;
					try {
						candidate = Path.GetFullPath(Path.Combine(folder.ToString().Trim('"'), name + e));
					} catch {
						continue;
					}
					if (File.Exists(candidate) && !found.Contains(candidate))
						found.Add(candidate);
				}
			}
			return found;
		}

		#endregion

		#region Load

		/// <summary>
		/// Runs a batch file (Windows) or a shell script (elsewhere) in a child process and brings into
		/// the environment the variables it defined, changed or removed: on Windows through
		/// cmd.exe /d /s /c "call file args &amp;&amp; set", elsewhere through /bin/sh -c "set -- args; . file &amp;&amp; env".
		/// The child starts with the environment of the script, so the difference is exactly what the
		/// file did. The names touched are in LastLoaded. A file that returns an error changes nothing.
		/// </summary>
		/// <param name="file">The batch file or shell script.</param>
		/// <param name="args">Its arguments, as one string.</param>
		/// <returns>True if loaded, false otherwise (see LastError).</returns>
		public static bool Load(KValue file, KValue? args = null) {
			LastError = ApiError.None;
			LastLoaded = new KList();
			string path;
			try {
				path = Path.GetFullPath(file);
			} catch (Exception ex) {
				return Fail(ErrorCode.InvalidArgument, "The environment file is not a valid path: " + ex.Message, file);
			}
			if (!File.Exists(path))
				return Fail(ErrorCode.NotFound, "The environment file does not exist", path);
			string arguments = args is null ? string.Empty : " " + args.ToString();
			Tool tool = new Tool("env");
			tool.CaptureOutput = false;
			Tool.ToolResult result;
			if (OperatingSystem.IsWindows())
				result = tool.CommandSync("cmd.exe", "/d /s /c \"call \"" + path + "\"" + arguments + " && set\"", null);
			else
				result = tool.CommandSync("/bin/sh", "-c \"set --" + arguments + "; . '" + path.Replace("'", "'\\''") + "' && env\"", null);
			if (result.Status != Tool.ToolStatus.Success) {
				string reason = result.Stderr.Length > 0 ? " " + string.Join(" ", result.Stderr).Trim() : string.Empty;
				return Fail(ErrorCode.Failed, "The environment file returned " + result.ExitCode + reason, path);
			}
			// The environment the file left, one NAME=value per line
			Dictionary<string, string> after = KombineState.NewEnvironment();
			foreach (string raw in result.Stdout) {
				string line = raw.TrimEnd('\r', '\n');
				int eq = line.IndexOf('=');
				// cmd prints its own "=C:" drive entries: not variables
				if (eq <= 0)
					continue;
				after[line.Substring(0, eq)] = line.Substring(eq + 1);
			}
			if (after.Count == 0)
				return Fail(ErrorCode.Failed, "The environment file printed no variables", path);
			Dictionary<string, string> variables = Variables;
			// Removed by the file
			foreach (string name in variables.Keys.ToList()) {
				if (!after.ContainsKey(name) && !IsShellOwn(name)) {
					variables.Remove(name);
					LastLoaded.Add(name);
				}
			}
			// Set or changed by the file
			foreach (KeyValuePair<string, string> kv in after) {
				if (IsShellOwn(kv.Key))
					continue;
				if (variables.TryGetValue(kv.Key, out string? previous) && previous == kv.Value)
					continue;
				variables[kv.Key] = kv.Value;
				LastLoaded.Add(kv.Key);
			}
			Msg.PrintMod("Environment loaded from " + path + ": " + LastLoaded.Count() + " variables", ".env", Msg.LogLevels.Verbose);
			return true;
		}

		/// <summary>
		/// The variables the shell itself defines or drops in the child, not the file.
		/// </summary>
		private static bool IsShellOwn(string name) {
			return name.Equals("PROMPT", StringComparison.OrdinalIgnoreCase) || name.Equals("SHLVL", StringComparison.OrdinalIgnoreCase)
				|| name.Equals("_", StringComparison.Ordinal) || name.Equals("OLDPWD", StringComparison.OrdinalIgnoreCase) || name.Equals("PWD", StringComparison.OrdinalIgnoreCase);
		}

		#endregion

		#region Dump

		/// <summary>
		/// Prints the environment the tools receive: every variable, sorted, and the path entries in
		/// order.
		/// </summary>
		/// <param name="Level">Log level, normal by default.</param>
		public static void Dump(Msg.LogLevels Level = Msg.LogLevels.Normal) {
			Dictionary<string, string> variables = Variables;
			Msg.Print("Environment: " + variables.Count + " variables", Level);
			Msg.BeginIndent();
			foreach (string name in variables.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)) {
				if (string.Equals(name, PathName, StringComparison.OrdinalIgnoreCase))
					continue;
				Msg.Print(name + "=" + variables[name], Level);
			}
			Msg.EndIndent();
			KList paths = Paths();
			Msg.Print(PathName + ": " + paths.Count() + " entries", Level);
			Msg.BeginIndent();
			foreach (KValue entry in paths)
				Msg.Print(entry, Level);
			Msg.EndIndent();
		}

		/// <summary>
		/// The reserved action kenv: the environment of the process, or where the tools given resolve to.
		/// </summary>
		/// <param name="tools">The tools to look for; none prints the environment.</param>
		internal static void DumpAction(string[] tools) {
			if (tools.Length == 0) {
				Dump();
				return;
			}
			foreach (string tool in tools) {
				KList found = Which(tool);
				if (found.Count() == 0) {
					Msg.Print(tool + ": not found on the path");
					continue;
				}
				Msg.Print(tool + ":");
				Msg.BeginIndent();
				foreach (KValue f in found)
					Msg.Print(f);
				Msg.EndIndent();
			}
		}

		#endregion

		/// <summary>
		/// The regular expression of a wildcard: * any run, ? one character, the case as the platform.
		/// </summary>
		private static Regex Wildcard(string pattern) {
			string expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
			return new Regex(expression, NameComparison == StringComparison.OrdinalIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
		}

		/// <summary>
		/// True when two path entries name the same folder: separators and a trailing one aside, and
		/// the case on Windows.
		/// </summary>
		private static bool SamePath(string a, string b) {
			string x = a.Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
			string y = b.Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
			return string.Equals(x, y, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		}
	}
}
