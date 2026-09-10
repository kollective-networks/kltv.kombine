/*---------------------------------------------------------------------------------------------------------

	Kombine Modules Example

	(C)Kollective Networks 2026

	A loaded file that carries "#pragma kombine module" is compiled once, as its own assembly, cached
	by its content and shared by every script that loads it: one copy of its types and of its top
	level variables per run, its top level statements run once when it is first loaded. A loaded
	file without the pragma is merged into the loading script, as always.

	The example writes a sandbox of small modules and scripts and checks, in groups:

	1. Compile once: two child scripts and their parent load the same module; the module is compiled
	   once, its initializer runs once, a static set by one child is read by the other.
	2. Merge kept: a helper without the pragma keeps one variable per script.
	3. Failures: a module with a compile error, a module needing a newer engine, a missing module and
	   a module whose initializer aborts are reported to the loading script.
	4. Cache by content: a second run compiles nothing, a touch compiles nothing, an edit of the module
	   compiles the module and the scripts that load it and no other, an edit of a child compiles that
	   child only, an edit of the root compiles the root only, -ksrb compiles everything.
	5. Modules loading modules: the order of the initializers, an edit of the inner module compiles
	   the outer one and the script.
	6. Duplicates: two copies of one module with the same name and another content are warned about,
	   and fail with -kmodules:strict.
	7. Remote module: a module loaded from an URL (served by a listener of this example on
	   localhost) is fetched once, used from its cached copy by the following runs, and fetched
	   again with -ksrb.

	The extensions themselves are modules since 1.6: the extension examples (mkb extensions) are
	their test. The groups that observe what is compiled run the tool as a child process at verbose
	level, where the engine prints one line per script and per module it compiles.

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int passed = 0;
int failed = 0;
string sandbox = string.Empty;
string mkb = string.Empty;

/// <summary>
/// Writes the sandbox and runs the seven groups.
/// </summary>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing modules");
	Msg.BeginIndent();
	sandbox = Path.GetFullPath(CurrentWorkingFolder + "/.tmp.modules");
	mkb = CurrentToolFolder + (Host.IsWindows() ? "/mkb.exe" : "/mkb");
	WriteSandbox();
	Msg.Print("Sandbox : " + sandbox);
	Msg.RawPrint(Environment.NewLine);

	//
	// 1. Compile once, in this process: the runner and its two children load counter.csx. The
	// engine line "Compiling the module" is counted through the message handler.
	//
	Banner("[1/7] Compile once, one copy for every script");
	int moduleCompiles = 0;
	Msg.OnMessage = (Msg.LogLevels level, Msg.MessageKind kind, string module, string message, int indent) => {
		if (message.StartsWith("Compiling the module counter.csx"))
			moduleCompiles++;
	};
	int code = Kombine(sandbox + "/runner.csx", "once", args, false);
	Msg.OnMessage = null;
	Check("runner and children", code.ToString(), "0");
	Check("module compiled", moduleCompiles.ToString(), "1");
	EndBanner();

	//
	// 2. Merge kept: helper.csx has no pragma, every script that loads it keeps its own counter
	//
	Banner("[2/7] Helper without the pragma: merged, one variable per script");
	Check("counters per script", Kombine(sandbox + "/runner.csx", "merge", args, false).ToString(), "0");
	EndBanner();

	//
	// 3. Failures, reported to the loading script through Engine.LastError
	//
	Banner("[3/7] Failures");
	code = Kombine(sandbox + "/usebroken.csx", "run", args, false);
	Check("compile error: code", code + " " + Engine.LastError.Code, "1 Failed");
	Check("compile error: names the module", Show(Engine.LastError.Message.Contains("broken.csx") && Engine.LastError.Message.Contains("compiling the module")), "true");
	Check("compile error: script did not run", Show(!File.Exists(sandbox + "/usebroken.ran")), "true");
	code = Kombine(sandbox + "/usefuture.csx", "run", args, false);
	Check("newer engine needed: code", code + " " + Engine.LastError.Code, "1 NotSupported");
	Check("newer engine needed: message", Show(Engine.LastError.Message.Contains("Kombine version 99.0")), "true");
	code = Kombine(sandbox + "/usemissing.csx", "run", args, false);
	Check("missing module: code", code + " " + Engine.LastError.Code, "1 NotFound");
	code = Kombine(sandbox + "/useabort.csx", "run", args, false);
	Check("initializer aborts: code", code + " " + Engine.LastError.Code, "1 Failed");
	Check("initializer aborts: message", Show(Engine.LastError.Message.Contains("aborted while initializing")), "true");
	EndBanner();

	//
	// 4. Cache by content, one tool process per run so the states on disk decide
	//
	Banner("[4/7] Cache by content");
	string[] runner = new[] { "-ko:v", "-kfile:" + sandbox + "/runner.csx", "once" };
	Compiles r = Run(runner);
	Check("second run", r.ToString(), "exit 0, modules 0, scripts 0");
	File.SetLastWriteTimeUtc(sandbox + "/counter.csx", DateTime.UtcNow);
	r = Run(runner);
	Check("touched module", r.ToString(), "exit 0, modules 0, scripts 0");
	File.AppendAllText(sandbox + "/counter.csx", Environment.NewLine + "// edit 1" + Environment.NewLine);
	r = Run(runner);
	Check("edited module", r.ToString(), "exit 0, modules 1, scripts 3");
	Check("edited module: its loaders", r.Scripts, "runner.csx,child1.csx,child2.csx");
	File.AppendAllText(sandbox + "/child1.csx", Environment.NewLine + "// edit 1" + Environment.NewLine);
	r = Run(runner);
	Check("edited child", r.ToString() + " " + r.Scripts, "exit 0, modules 0, scripts 1 child1.csx");
	File.AppendAllText(sandbox + "/runner.csx", Environment.NewLine + "// edit 1" + Environment.NewLine);
	r = Run(runner);
	Check("edited root", r.ToString() + " " + r.Scripts, "exit 0, modules 0, scripts 1 runner.csx");
	r = Run(new[] { "-ksrb" }.Concat(runner).ToArray());
	Check("-ksrb", r.ToString(), "exit 0, modules 1, scripts 4");
	r = Run(runner);
	Check("after -ksrb", r.ToString(), "exit 0, modules 0, scripts 0");
	EndBanner();

	//
	// 5. Modules loading modules: nested.csx loads outer.csx, which loads counter.csx
	//
	Banner("[5/7] Modules loading modules");
	string[] nested = new[] { "-ko:v", "-kfile:" + sandbox + "/nested.csx", "order" };
	r = Run(nested);
	Check("first run", r.ToString() + " " + r.Modules, "exit 0, modules 1, scripts 1 outer.csx");
	Check("initializers in order", r.Line("order="), "order=counter-init,outer-init name=outer:42");
	File.AppendAllText(sandbox + "/counter.csx", Environment.NewLine + "// edit 2" + Environment.NewLine);
	r = Run(nested);
	Check("edited inner module", r.ToString() + " " + r.Modules, "exit 0, modules 2, scripts 1 counter.csx,outer.csx");
	r = Run(nested);
	Check("third run", r.ToString(), "exit 0, modules 0, scripts 0");
	EndBanner();

	//
	// 6. Duplicates: dup.csx loads a/shared.csx and runs b/usedup.csx, which loads b/shared.csx
	//
	Banner("[6/7] Two copies of one module");
	string[] dup = new[] { "-kfile:" + sandbox + "/dup.csx", "run" };
	r = Run(dup);
	Check("warning by default", r.Exit + " " + Show(r.Contains("is loaded from two different files")), "0 true");
	Check("each script uses its own", r.Line("dup=") + " " + r.Line("usedup="), "dup=a usedup=b");
	r = Run(new[] { "-kmodules:strict" }.Concat(dup).ToArray());
	Check("failure with -kmodules:strict", r.Exit.ToString(), "1");
	EndBanner();

	//
	// 7. A remote module, served by a listener of this process. The URL carries a stamp so the cache
	// of a previous run of the example does not answer for it.
	//
	Banner("[7/7] Remote module");
	int served = 0;
	System.Net.HttpListener? server = StartServer(sandbox + "/remote", () => served++);
	if (server == null) {
		Check("listener on localhost", "not available, group skipped", "not available, group skipped");
	} else {
		string url = server.Prefixes.First() + "remote.csx?run=" + DateTime.UtcNow.Ticks;
		Files.WriteTextFile(sandbox + "/useremote.csx", "#load \"" + url + "\"" + Environment.NewLine + @"int run(string[] args) {
	Msg.Print(""remote="" + Remote());
	return Remote() == ""remote"" ? 0 : 1;
}
");
		code = Kombine(sandbox + "/useremote.csx", "run", args, false);
		Check("first load fetches", code + " served " + served, "0 served 1");
		code = Kombine(sandbox + "/useremote.csx", "run", args, false);
		Check("second load in the run", code + " served " + served, "0 served 1");
		string[] remote = new[] { "-ko:v", "-kfile:" + sandbox + "/useremote.csx", "run" };
		r = Run(remote);
		Check("next run uses the cached copy", r.ToString() + " served " + served, "exit 0, modules 0, scripts 0 served 1");
		File.AppendAllText(sandbox + "/remote/remote.csx", Environment.NewLine + "// edit 1" + Environment.NewLine);
		r = Run(remote);
		Check("edited remote file, no -ksrb", r.ToString() + " served " + served, "exit 0, modules 0, scripts 0 served 1");
		r = Run(new[] { "-ksrb" }.Concat(remote).ToArray());
		Check("-ksrb fetches again", r.ToString() + " " + r.Modules + " served " + served, "exit 0, modules 1, scripts 1 remote.csx served 2");
		server.Stop();
	}
	EndBanner();

	int total = passed + failed;
	Msg.PrintTask($"Summary : {passed} of {total} checks passed ");
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return failed == 0 ? 0 : 1;
}

/// <summary>
/// Writes the modules and the scripts of the sandbox. A stamp in every file makes the content of
/// this run different from the previous one, so the first group compiles everything.
/// </summary>
void WriteSandbox(){
	if (Folders.Exists(sandbox))
		Folders.Delete(sandbox, true);
	Folders.Create(sandbox);
	Folders.Create(sandbox + "/a");
	Folders.Create(sandbox + "/b");
	string stamp = "// run " + DateTime.UtcNow.Ticks + Environment.NewLine;
	// The module: a counter of its initializer runs, a list every script adds to, a type and a function
	Files.WriteTextFile(sandbox + "/counter.csx", stamp + @"#pragma kombine requires 1.6
#pragma kombine module
using System.Collections.Generic;

/// <summary>How many times the initializer ran: once per process.</summary>
int Inits = 0;

/// <summary>What the scripts recorded, in order.</summary>
List<string> Seen = new List<string>();

/// <summary>A type of the module: the same type in every script.</summary>
class Widget {
	public string Name;
	public Widget(string name) { Name = name; Seen.Add(name); }
}

/// <summary>A function of the module.</summary>
int Helper(int x) { return x * 2; }

Inits++;
Seen.Add(""counter-init"");
");
	// A module loading the module
	Files.WriteTextFile(sandbox + "/outer.csx", stamp + @"#pragma kombine requires 1.6
#pragma kombine module
#load ""counter.csx""

string OuterName() { return ""outer:"" + Helper(21); }
Seen.Add(""outer-init"");
");
	// A helper without the pragma: merged into every script that loads it
	Files.WriteTextFile(sandbox + "/helper.csx", stamp + @"int calls = 0;
int Bump() { return ++calls; }
");
	// The runner and its children
	Files.WriteTextFile(sandbox + "/runner.csx", stamp + @"#load ""counter.csx""
#load ""helper.csx""
using System.Collections.Generic;

int once(string[] args) {
	int failed = 0;
	new Widget(""runner"");
	if (Kombine(""child1.csx"", ""once"", args) != 0) failed++;
	if (Kombine(""child2.csx"", ""once"", args) != 0) failed++;
	if (Kombine(""child3.csx"", ""once"", args) != 0) failed++;
	if (Inits != 1) failed++;
	if (string.Join("","", Seen) != ""counter-init,runner,child1,child2"") failed++;
	Msg.Print(""seen="" + string.Join("","", Seen) + "" inits="" + Inits, Msg.LogLevels.Verbose);
	return failed;
}

int merge(string[] args) {
	Bump(); Bump(); Bump();
	int a = Kombine(""child1.csx"", ""merge"", args, false);
	int b = Kombine(""child2.csx"", ""merge"", args, false);
	int mine = Bump();
	Msg.Print(""merge="" + mine + "","" + a + "","" + b, Msg.LogLevels.Verbose);
	return (mine == 4 && a == 2 && b == 1) ? 0 : 1;
}
");
	Files.WriteTextFile(sandbox + "/child1.csx", stamp + @"#load ""counter.csx""
#load ""helper.csx""

int once(string[] args) {
	new Widget(""child1"");
	return 0;
}

int merge(string[] args) {
	Bump();
	return Bump();
}
");
	Files.WriteTextFile(sandbox + "/child2.csx", stamp + @"#load ""counter.csx""
#load ""helper.csx""

int once(string[] args) {
	new Widget(""child2"");
	// The static set by child1 is visible here: the same type, the same assembly
	return Seen.Contains(""child1"") ? 0 : 1;
}

int merge(string[] args) {
	return Bump();
}
");
	Files.WriteTextFile(sandbox + "/child3.csx", stamp + @"#load ""helper.csx""

int once(string[] args) {
	return Bump() == 1 ? 0 : 1;
}
");
	Files.WriteTextFile(sandbox + "/nested.csx", stamp + @"#load ""outer.csx""

int order(string[] args) {
	Msg.Print(""order="" + string.Join("","", Seen) + "" name="" + OuterName());
	return 0;
}
");
	// The failures
	Files.WriteTextFile(sandbox + "/broken.csx", stamp + @"#pragma kombine module
int Broken() { return ""not an int""; }
");
	Files.WriteTextFile(sandbox + "/usebroken.csx", stamp + @"#load ""broken.csx""
int run(string[] args) {
	Files.WriteTextFile(CurrentScriptFolder + ""/usebroken.ran"", ""ran"");
	return 0;
}
");
	Files.WriteTextFile(sandbox + "/future.csx", stamp + @"#pragma kombine requires 99.0
#pragma kombine module
int Future = 1;
");
	Files.WriteTextFile(sandbox + "/usefuture.csx", stamp + @"#load ""future.csx""
int run(string[] args) { return 0; }
");
	Files.WriteTextFile(sandbox + "/usemissing.csx", stamp + @"#load ""missing.csx""
int run(string[] args) { return 0; }
");
	Files.WriteTextFile(sandbox + "/abortmod.csx", stamp + @"#pragma kombine module
Msg.PrintAndAbort(""expected abort of the module initializer"");
");
	Files.WriteTextFile(sandbox + "/useabort.csx", stamp + @"#load ""abortmod.csx""
int run(string[] args) { return 0; }
");
	// The remote module, served from its folder by the listener of the example
	Folders.Create(sandbox + "/remote");
	Files.WriteTextFile(sandbox + "/remote/remote.csx", stamp + @"#pragma kombine requires 1.6
#pragma kombine module
string Remote() { return ""remote""; }
");
	// The duplicates: the same file name, another content, in two folders
	Files.WriteTextFile(sandbox + "/a/shared.csx", stamp + @"#pragma kombine module
string Which() { return ""a""; }
");
	Files.WriteTextFile(sandbox + "/b/shared.csx", stamp + @"#pragma kombine module
string Which() { return ""b""; }
");
	Files.WriteTextFile(sandbox + "/dup.csx", stamp + @"#load ""a/shared.csx""
int run(string[] args) {
	Msg.Print(""dup="" + Which());
	return Kombine(""b/usedup.csx"", ""run"", args);
}
");
	Files.WriteTextFile(sandbox + "/b/usedup.csx", stamp + @"#load ""shared.csx""
int run(string[] args) {
	Msg.Print(""usedup="" + Which());
	return Which() == ""b"" ? 0 : 1;
}
");
}

/// <summary>
/// What one tool run compiled, read from its verbose output: the engine prints one line per script
/// and per module it compiles.
/// </summary>
class Compiles {
	public int Exit;
	public List<string> Lines = new List<string>();
	public string Modules = string.Empty;
	public string Scripts = string.Empty;
	public int ModuleCount;
	public int ScriptCount;

	public Compiles(ToolResult result) {
		Exit = result.ExitCode;
		Lines.AddRange(result.Stdout);
		Lines.AddRange(result.Stderr);
		List<string> modules = new List<string>();
		List<string> scripts = new List<string>();
		foreach (string line in Lines) {
			int at = line.IndexOf("Compiling the module ");
			if (at >= 0)
				modules.Add(line.Substring(at + "Compiling the module ".Length).Trim());
			at = line.IndexOf("Compiling the script ");
			if (at >= 0)
				scripts.Add(line.Substring(at + "Compiling the script ".Length).Trim());
		}
		ModuleCount = modules.Count;
		ScriptCount = scripts.Count;
		Modules = string.Join(",", modules);
		Scripts = string.Join(",", scripts);
	}

	/// <summary>The exit code and the counts, the way the checks compare them.</summary>
	public override string ToString() {
		return "exit " + Exit + ", modules " + ModuleCount + ", scripts " + ScriptCount;
	}

	/// <summary>True when a line of the output contains the text.</summary>
	public bool Contains(string text) {
		return Lines.Any(l => l.Contains(text));
	}

	/// <summary>The first line containing the text, trimmed, or empty.</summary>
	public string Line(string text) {
		string? found = Lines.FirstOrDefault(l => l.Contains(text));
		return found == null ? string.Empty : found.Trim();
	}
}

/// <summary>
/// Starts a listener on localhost that serves the files of a folder, on the first free port of a
/// small range, and counts the requests it answers. Null when no port could be opened.
/// </summary>
/// <param name="folder">The folder served: a request for /name.csx answers with folder/name.csx.</param>
/// <param name="onServed">Called after every answered request.</param>
System.Net.HttpListener? StartServer(string folder, Action onServed) {
	for (int port = 18475; port < 18495; port++) {
		System.Net.HttpListener listener = new System.Net.HttpListener();
		listener.Prefixes.Add("http://localhost:" + port + "/");
		try {
			listener.Start();
		} catch {
			continue;
		}
		System.Threading.Thread worker = new System.Threading.Thread(() => {
			while (listener.IsListening) {
				System.Net.HttpListenerContext context;
				try {
					context = listener.GetContext();
				} catch {
					break;
				}
				string file = folder + "/" + Path.GetFileName(context.Request.Url?.AbsolutePath ?? string.Empty);
				try {
					if (File.Exists(file)) {
						byte[] bytes = File.ReadAllBytes(file);
						context.Response.StatusCode = 200;
						context.Response.ContentLength64 = bytes.Length;
						context.Response.OutputStream.Write(bytes, 0, bytes.Length);
					} else {
						context.Response.StatusCode = 404;
					}
					context.Response.Close();
				} catch {
				}
				onServed();
			}
		});
		worker.IsBackground = true;
		worker.Start();
		return listener;
	}
	return null;
}

/// <summary>
/// Runs the tool as a child process with its output captured and returns what it compiled.
/// </summary>
Compiles Run(string[] arguments) {
	Tool tool = new Tool("modules");
	return new Compiles(tool.CommandSync(mkb, arguments, null));
}

void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
}

void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

string Show(bool value){
	return value ? "true" : "false";
}

void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-36} : {actual,-52} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-36} : {expected}");
	}
}
