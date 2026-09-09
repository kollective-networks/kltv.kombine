/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {
	/// <summary>
	/// 
	/// Compiles and executes a Kombine script
	/// 
	/// </summary>
	internal partial class KombineScript {

		/// <summary>
		/// Holds the parent script if any
		/// </summary>
		public KombineScript? ParentScript { get; internal set; } = null;

		/// <summary>
		/// Holds the script state
		/// That includes:
		///		-Precompiled binary
		///		-Custom script data
		/// </summary>
		internal readonly KombineState State = new();

		/// <summary>
		/// Class name for the script. It will be the script name plus _class sufix
		/// </summary>
		private string ClassName { get; set; } = "default";

		/// <summary>
		/// Assembly name for the script. It will be the script name (metadata reference)
		/// </summary>
		private string AssemblyName { get; set; } = "default";

		/// <summary>
		/// Module name for the script. It will be the script name plus .kscript.dll (binary name)
		/// </summary>
		private string ModuleName { get; set; } = "default";

		/// <summary>
		/// The script filename to be executed
		/// </summary>
		internal string Scriptfile { get; private set; } = string.Empty;

		/// <summary>
		/// Action parameters to be attached in action execution
		/// </summary>
		internal string[] ActionParameters { get; private set; } = new string[0];

		/// <summary>
		/// The script path. Used to resolve #load directives in the script among others
		/// </summary>
		internal string ScriptPath { get; set; } = string.Empty;

		/// <summary>
		/// If we should execute a debug build
		/// </summary>
		internal bool DebugBuild { get; private set; } = false;

		/// <summary>
		/// Signals if the script was rebuilt (due to script changed or by version, not forced)
		/// </summary>
		internal bool WasRebuilt { get; private set; } = false;

		/// <summary>
		/// Signals if the parent script was rebuilt (due to script changed or by version, not forced)
		/// </summary>
		internal bool ParentWasRebuilt { get; set; } = false;

		/// <summary>
		///	 Automatic usings for the script to be included
		/// </summary>
		private string[] Usings { get; set; } = new string[] {
			"System",
			"System.IO",
			"System.Text",
			"System.Text.Json",
			"System.Text.Json.Nodes",
			"Kltv.Kombine.Types",
			"Kltv.Kombine.Api",
			"Kltv.Kombine.Api.Tool",
			"Kltv.Kombine.Api.Statics",
			"Kltv.Kombine.Api.Compress.Tar",
		};

		/// <summary>
		/// Allowed assemblies to be referenced from the script
		/// </summary>
		private string[] Assemblies { get; set; } = new string[] {


		};

		/// <summary>
		/// Properties to be added in all the scripts
		/// </summary>
		private string InjectedCode { get; } = @"
// ------------------------------------------------------------------------------------------------
// 
// Kombine Script Injected Code
// 
// ------------------------------------------------------------------------------------------------
string CurrentWorkingFolder { get { return Folders.CurrentWorkingFolder; } }
string CurrentScriptFolder { get { return Folders.CurrentScriptFolder; } }
string CurrentToolFolder { get { return Folders.CurrentToolFolder; } }
string ParentScriptFolder { get { return Folders.ParentScriptFolder; } }

";
		/// <summary>
		///  Creates an instance for one Kombine script
		/// </summary>
		/// <param name="Script">Filename for the script</param>
		/// <param name="Scriptpath">Path where the script is.</param>
		/// <param name="Debug">If we should activate debug options for the script.</param>
		internal KombineScript(string Script,string Scriptpath,bool Debug = false) {
			// Set the script file
			Scriptfile = Script;
			// Set the script path
			// It is used to resolve #load directives in the script since the script itself (the name)
			// could have some relative path.
			ScriptPath = Scriptpath;
			// Debug options
			DebugBuild = Debug;
			// Set the script internal name(s)
			// Just we ensure the only the filename is used to generate the class, assembly and module names to avoid wrong chars.
			// Note: We must add some form of hashing to avoid collisions between scripts with same name but in different paths.
			//
			ClassName =Path.GetFileNameWithoutExtension(Script) + "_class";
			AssemblyName = Path.GetFileNameWithoutExtension(Script) + "_assembly";
			// Sanitize Class & AssemblyName since some characters are not allowed
			ClassName = ClassName.Replace(".","_");
			AssemblyName = AssemblyName.Replace(".","_");
			ModuleName = Path.GetFileNameWithoutExtension(Script) + ".kscript.dll";
		}

		/// <summary>
		/// Load all the references for the script
		/// </summary>
		internal void LoadReferences(){
			// Forces the assemblies to be loaded into memory (all of references)
			if (ParentScript == null){
				Msg.PrintMod("Loading all referenced assemblies into memory.", ".exec.script", Msg.LogLevels.Debug);
				Assembly a = Assembly.GetExecutingAssembly();
				LoadReferencedAssembly(a);
			}
		}

		/// <summary>
		/// Reasons of the references that could not be resolved while compiling, collected by the source resolver.
		/// </summary>
		internal List<string> ResolveErrors { get; } = new List<string>();

		/// <summary>
		/// Minimum version failures of the loaded files, collected by the source resolver before the compile.
		/// </summary>
		internal List<string> VersionErrors { get; } = new List<string>();

		/// <summary>
		/// Checks the minimum Kombine version a script declares in its first lines with
		/// "#pragma kombine requires major.minor". Several declarations: the highest wins.
		/// </summary>
		/// <param name="file">The script file, named in the message.</param>
		/// <param name="lines">Its lines; only the first forty are looked at.</param>
		/// <returns>Null when satisfied or not declared, the failure message otherwise.</returns>
		internal static string? CheckRequiredVersion(string file, IEnumerable<string> lines) {
			Regex declaration = new Regex(@"^\s*#pragma\s+kombine\s+requires\s+(\d+)\.(\d+)\b", RegexOptions.IgnoreCase);
			int required = 0;
			string requiredText = string.Empty;
			int count = 0;
			foreach (string line in lines) {
				if (count++ >= 40)
					break;
				Match m = declaration.Match(line);
				if (!m.Success)
					continue;
				int version = (int.Parse(m.Groups[1].Value) << 8) | int.Parse(m.Groups[2].Value);
				if (version > required) {
					required = version;
					requiredText = m.Groups[1].Value + "." + m.Groups[2].Value;
				}
			}
			if (required == 0 || required <= KombineMain.Version.HexVersion)
				return null;
			return "Kombine version " + requiredText + " at minimum is required to use " + file + " (running " + KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build + ")";
		}

		/// <summary>
		/// Reports a failure of this script execution. The entry script prints it, since nobody else can;
		/// a child script hands it to the calling script through Engine.LastError and its return code,
		/// logging it at verbose level only. A multi line message is printed one line per row.
		/// </summary>
		/// <summary>
		/// The reason of an aborted script for Engine.LastError: the message given to Msg.PrintAndAbort,
		/// when there was one, after the generic text.
		/// </summary>
		private static string AbortReason(Exception ex) {
			string message = ex.Message.Trim();
			// An exception without message reports the type name: nothing worth adding
			if (message.Length == 0 || message == typeof(ScriptAbortException).FullName || message.StartsWith("Exception of type"))
				return "Script aborted execution";
			return "Script aborted execution: " + message.Split('\n')[0].TrimEnd('\r');
		}

		/// <param name="code">Kind of failure.</param>
		/// <param name="message">Reason, possibly several lines.</param>
		private void ReportFailure(ErrorCode code, string message) {
			Engine.LastError = new ApiError(code, message, Scriptfile ?? string.Empty);
			Msg.LogLevels level = ParentScript == null ? Msg.LogLevels.Normal : Msg.LogLevels.Verbose;
			string[] lines = message.Split('\n');
			Msg.PrintErrorMod(lines[0], ".exec.script", level);
			if (lines.Length > 1) {
				Msg.BeginIndent();
				for (int i = 1; i < lines.Length; i++)
					Msg.PrintError(lines[i], level);
				Msg.EndIndent();
			}
		}

		/// <summary>
		/// Reports a warning of this script execution with the same visibility rule as the failures.
		/// </summary>
		/// <param name="message">Warning text.</param>
		private void ReportWarning(string message) {
			Msg.PrintWarningMod(message, ".exec.script", ParentScript == null ? Msg.LogLevels.Normal : Msg.LogLevels.Verbose);
		}

		/// <summary>
		///  Executes the script given by filename. It will try first to load it from the saved state (precompiled one)
		///  If not, it will be compiled.
		/// </summary>
		/// <param name="Action">Action to be executed in the script.</param>
		/// <param name="ActionParameters">The parameters for the given action.</param>
		/// <returns>Returns the exitcode from script execution</returns>
		internal int Execute(string Action, string[]? ActionParameters) {
			// Its not supposed to happen but just in case.
			if (string.IsNullOrEmpty(Scriptfile)) {
				ReportFailure(ErrorCode.InvalidArgument, "Invalid script filename");
				return Constants.ExitCodeFailure;
			}
			// Save the action parameters
			this.ActionParameters = ActionParameters ?? Array.Empty<string>();
			//
			// Check state / fetch from cache
			//
			if ( (State.FetchCache(Scriptfile) == false) || (Config.Rebuild == true) || (ParentWasRebuilt) ) {
				//
				// There is no previous state saved. We will try to build the script
				// 
				if (Config.Rebuild){
					Msg.PrintMod("Rebuild forced. Triggering rebuild.", ".exec.script", Msg.LogLevels.Debug);
				} else {
					Msg.PrintMod("No previous state or old. Triggering rebuild.", ".exec.script", Msg.LogLevels.Debug);
					if (ParentWasRebuilt) {
						Msg.PrintMod("Parent script was rebuilt. Triggering rebuild.", ".exec.script", Msg.LogLevels.Debug);
					}
					// Mark ourselves as rebuilt because we will do it due to cache miss or by parent changed
					WasRebuilt = true;
				}
				if (Compile(Scriptfile,DebugBuild) == false) {
					// The failure was reported by the compilation with its reason
					return Constants.ExitCodeFailure;
				}
				Msg.PrintMod("Script '"+Scriptfile+"' compiled successfully.", ".exec.script", Msg.LogLevels.Debug);
			}
			// Load the built assembly from the saved state
			//
			Msg.PrintMod("Loading the compiled script.", ".exec.script", Msg.LogLevels.Debug);
			Assembly assembly;
			if (State.Data.CompiledScript == null) {
				ReportFailure(ErrorCode.Failed, "The compiled script could not be loaded: the state holds no binary");
				return Constants.ExitCodeFailure;
			}
			if (State.Data.CompiledScriptPDB != null) {
				assembly = Assembly.Load(State.Data.CompiledScript, State.Data.CompiledScriptPDB);
			} else {
				assembly = Assembly.Load(State.Data.CompiledScript);
			}
			if (assembly == null) {
				ReportFailure(ErrorCode.Failed, "The compiled script could not be loaded into memory");
				return Constants.ExitCodeFailure;
			}
			// Fetch the entry point
			// We use the script class (defined in compilation step with the script name plus _class suffix)
			// 
			Msg.PrintMod("Fetching the entrypoint.", ".exec.script", Msg.LogLevels.Debug);
			Type? ScriptClass = assembly.ExportedTypes.FirstOrDefault(x => x.Name == ClassName); 
			if (ScriptClass == null) {
				ReportFailure(ErrorCode.Failed, "The script class was not found in the compiled script");
				return Constants.ExitCodeFailure;
			}
			MethodInfo? entrypoint;
			// First try to get entrypoint for top level statements.
			// Its the preferred way to code the script but we support a "main" function also
			entrypoint = ScriptClass.GetMethod("<Factory>", BindingFlags.Static | BindingFlags.Public);
			if (entrypoint == null) {
				ReportFailure(ErrorCode.Failed, "The script entry point (the top level statements) was not found");
				return Constants.ExitCodeFailure;
			}
			// And just execute the script
			object? ReturnCode = null;
			try {
				Msg.PrintMod("Executing script.", ".exec.script", Msg.LogLevels.Debug);

				// Parameters for a top level statement are not well documented.
				// Taken from: https://www.strathweb.com/2019/06/building-a-c-interactive-shell-in-a-browser-with-blazor-webassembly-and-roslyn/
				// Since our approach is a bit like a REPL but just executing two steps. The top level statements and after that
				// the target function we want to execute.
				//
				// For the invoke method, first parameter is the instance, second parameter is an array of objects
				// that will be used as arguments for the method. In this case, for the <Factory> method, parameters are:
				//
				// 
				// [0] the globals object
				// [1] the runtime environent
				//
				// The return value is a task
				object?[] submissiongArray = new object?[] { null, null };
				object[] parameters = new object[] { submissiongArray };
				// We pass the instance object as null, and execution will create a new instance for us (holding the top level statements instance).
				ReturnCode = entrypoint.Invoke(null, parameters);
				// Check the return code. If its a task, we will wait for it to finish.
				//
				if (!EvaluateResult(ReturnCode)){
					ReportFailure(ErrorCode.Failed, "The global code of the script failed");
					return Constants.ExitCodeFailure;
				}
				// After the execution of the top level statements, the runtime environment contains
				// the instance created to run the top level statements, so, we can use it as an instance to call our methods.
				entrypoint = ScriptClass.GetMethod(Action);
				if (entrypoint == null) {
					ReportFailure(ErrorCode.NotFound, "The action '" + Action + "' was not found in the script");
					return Constants.ExitCodeFailure;
				}
				// Fetch the instance from the runtime environment (is left on the submission array after call the top level statements)
				object? Instance = submissiongArray[1];
				// Invoke the action passing the action parameters we fetched from the command line
				//
				if (ActionParameters == null)
					ActionParameters = Array.Empty<string>();
				parameters = new object[] { ActionParameters };
				ReturnCode = entrypoint.Invoke(Instance, parameters);
				// Check if the return code is an int.
				if ( ( ReturnCode != null) && (ReturnCode.GetType() == typeof(int))) {
					int code = (int) ReturnCode;
					Msg.PrintMod("Script executed successfully. Return code: "+code, ".exec.script", Msg.LogLevels.Debug);
					// The action ran: whatever it returns is its own result, not an engine failure
					Engine.LastError = ApiError.None;
					return code;
				}
				ReportFailure(ErrorCode.InvalidArgument, "The action '" + Action + "' must return an int");
				return Constants.ExitCodeFailure;
			} catch (Exception ex) {
				//
				// Here we catch if the script execution failed (could not be invoked) or the script itself raised an exception.
				// If the scripts wants to abort execution, it will raise an exception that we must catch here just to return
				// a failure exit code
				// but do not stop the kombine process.
				//
				if (ex is ScriptAbortException){
					ReportFailure(ErrorCode.Failed, AbortReason(ex));
					return Constants.ExitCodeFailure;
				}
				if (ex.InnerException != null) {
					if (ex.InnerException is ScriptAbortException){
						// The abort message was already printed by the script; it is kept as the reason
						Engine.LastError = new ApiError(ErrorCode.Failed, AbortReason(ex.InnerException), Scriptfile ?? string.Empty);
						return Constants.ExitCodeFailure;
					}
					ReportFailure(ErrorCode.Failed, "Script exception: " + ex.InnerException.Message);
					return Constants.ExitCodeFailure;
				}
				ReportFailure(ErrorCode.Failed, "Failed executing script: " + ex.Message);
				return Constants.ExitCodeFailure;
			}
		}

		/// <summary>
		/// Evaluates the result from a top level statement task execution.
		/// Since is a bit more tedious in code, is here, in this separate function
		/// </summary>
		/// <param name="result">the resulting object to be evaluated</param>
		/// <returns>True if the execution was fine, false otherwise.</returns>
		private bool EvaluateResult(object? result) {
			// Check execution result
			if (result != null) {
				if (result is Task) {
					Task? t = result as Task;
					if (t == null) {
						Msg.PrintErrorMod("Script globals execution could not be evaluated. Invalid Object cast.", ".exec.script", Msg.LogLevels.Verbose);
						return false;
					}
					if (!t.IsCompletedSuccessfully) {
						Msg.PrintWarningMod("Script globals failed execution", ".exec.script", Msg.LogLevels.Verbose);
						Msg.PrintWarningMod("State: " + t.Status.ToString(), ".exec.script", Msg.LogLevels.Verbose);
						if (t.Exception != null) {
							Msg.PrintWarningMod("Exception: " + t.Exception.Message, ".exec.script", Msg.LogLevels.Verbose);
						}
						return false;
					} else {
						Msg.PrintMod("Script globals executed successfully.", ".exec.script", Msg.LogLevels.Debug);
						return true;
					}
				} else {
					Msg.PrintErrorMod("Script globals execution could not be evaluated. Invalid Object type.", ".exec.script", Msg.LogLevels.Verbose);
					return false;
				}
			} else {
				Msg.PrintErrorMod("Script globals execution could not be evaluated. No returned information.", ".exec.script", Msg.LogLevels.Verbose);
				return false;
			}
		}

		/// <summary>
		/// Compiles a script given by filename.
		/// Assembly Name, Class Name and Module Name should be defined before calling this function.
		/// State should be initialized before calling this function.
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="Debug"></param>
		/// <returns>True if compilation was okey. False otherwise</returns>
		private bool Compile(string filename, bool Debug = false) {
			ResolveErrors.Clear();
			VersionErrors.Clear();
			// Load the script text
			string? scriptText = FetchScriptText(filename,Debug);
			if (scriptText == null){
				Msg.PrintErrorMod("Could not load script text. Aborting",".exec.script", Msg.LogLevels.Verbose);
				return false;
			}
			//
			// Prepare the compilation environment
			//
			Msg.PrintMod("Configuring compilation environment.", ".exec.script", Msg.LogLevels.Debug);
			// Start with the compilation options. Initial parameter, we will build a library to be executed
			CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
			// Initialize the compilation opptions.
			{
				// Do not allow unsafe code.
				options = options.WithAllowUnsafe(false);
				// Identity comparer maybe is needed if we allow #r directives
				// -> options.WithAssemblyIdentityComparer( AssemblyIdentityComparer )
				// Allow concurrent build - disabled due to Roslyn 5.0.0 crashes on .NET 10.0
				options = options.WithConcurrentBuild(false);
				// Crypto options
				// CryptoKeyContainer: The name of the key container that contains the key pair used to generate a strong name for the compilation's output assembly.
				// CryptoKeyFile: The path to the file that contains the key pair used to generate a strong name for the compilation's output assembly.
				// CryptoPublicKey: The public key used to generate a strong name for the compilation's output assembly.
				// DelaySign: true if the compilation's output assembly should include only the public key of the key pair used to generate a strong name for the assembly; otherwise, false.
				// PublicSign: true if the compilation's output assembly should be marked as fully signed; otherwise, false.
				// Compilation should be deterministic. Same input, same output.
				// StrongNameProvider: The provider of strong name and signature information for the compilation's output assembly.
				options = options.WithDeterministic(true);
				// Error level: We will use the default one but this is prone to be tweaked by command line.
				options = options.WithGeneralDiagnosticOption(ReportDiagnostic.Default);
				// We allow any entry point / main function
				options = options.WithMainTypeName(null);
				// Inheritance: Default. Only public members are visible to the executed script
				options = options.WithMetadataImportOptions(MetadataImportOptions.Public);
				//
				if (true) {
					// This should be tweaked by configuration. Activated to test. Allow to use #r directives in the script source code
					Msg.PrintMod("Adding assembly resolver.", ".exec.script", Msg.LogLevels.Debug);
					options = options.WithMetadataReferenceResolver(new KombineScript.AssemblyResolver());
				}
				if (true) {
					// This should be tweaked by configuration. Activated to test. Allow to use #load directives in the script source code
					Msg.PrintMod("Adding source resolver.", ".exec.script", Msg.LogLevels.Debug);
					options = options.WithSourceReferenceResolver(new KombineScript.SourceResolver(ScriptPath));
				}
				// Module name. We will use the script name plus .kscript.dll
				options = options.WithModuleName(ModuleName);
				// Script class name.
				options = options.WithScriptClassName(ClassName);
				// Nullability: Disabled due to Roslyn 5.0.0 crashes on .NET 10.0
				// TODO: Re-enable when Roslyn is updated to a version without this bug
				// Tracking issue: https://github.com/dotnet/roslyn/issues/XXXXX
				options = options.WithNullableContextOptions(NullableContextOptions.Disable);
				// With the nullable context disabled, scripts using nullable annotations would emit
				// CS8632 on every compile. Suppress it until the nullable context can be re-enabled.
				options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> {
					{ "CS8632", ReportDiagnostic.Suppress },
					// "#pragma kombine requires" is read by the engine before compiling; the compiler does not know it
					{ "CS1633", ReportDiagnostic.Suppress }
				});
				// Optimization level & debug
				if (Debug) {
					Msg.PrintMod("Setting options to build on debug mode (invoked with -ksdbg)", ".exec.script", Msg.LogLevels.Debug);
					options = options.WithOverflowChecks(true);
					options = options.WithOptimizationLevel(OptimizationLevel.Debug);
					options = options.WithReportSuppressedDiagnostics(true);
					//options = options.WithWarningLevel();
				} else {
					Msg.PrintMod("Setting options to build on release mode.", ".exec.script", Msg.LogLevels.Debug);
					options = options.WithOverflowChecks(false);
					options = options.WithOptimizationLevel(OptimizationLevel.Release);
					options = options.WithReportSuppressedDiagnostics(false);
					//options = options.WithWarningLevel();
				}
				// Platform: We will use AnyCPU so cached binaries can be used in any platform
				options = options.WithPlatform(Platform.AnyCpu);
				// Diagnostic options.
				// Used to lower / raise the error level for a given diagnostic
				// options = options.WithSpecificDiagnosticOptions(ImmutableDictionary.Create<string, ReportDiagnostic>());
				// Syntax tree
				// options = options.WithSyntaxTreeOptions(SyntaxTreeOptions.);
				// Add the usings to the script.
				Msg.PrintMod("Adding Imports.", ".exec.script", Msg.LogLevels.Debug);
				foreach(string s in Usings) {
					Msg.PrintMod("Import: " + s, ".exec.script", Msg.LogLevels.Debug);
				}
				options = options.WithUsings(Usings);
				// XmlReferenceResolver. 
				// TODO: Check if we need to use it.
			}
			// Initialize the syntax tree to use
			// TODO: Review if we need to add additional parse options.
			// For example preprocessor names, etc.
			SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(
										scriptText+InjectedCode,
										new CSharpParseOptions(	kind: SourceCodeKind.Script,
																languageVersion: LanguageVersion.Latest));
			// Initialize metadata references
			// We include in the metadata references all the current loaded assemblies.
			// This is nice to debug but we should control what we allow and what we don't from script perspective.
			// TODO: Review.
			// This approach is a workaround for an ongoing bug in .net core which prevents the usage of the Roslyn Scripting API when
			// you publish the application in single file mode.
			// PR stills open:
			// https://github.com/dotnet/roslyn/pull/57910
			// Issue stills open:
			// https://github.com/dotnet/roslyn/issues/50719
			// Offending code: https://github.com/dotnet/roslyn/blob/main/src/Scripting/Core/Script.cs#L256
			// https://github.com/dotnet/runtime/issues/36590#issuecomment-689883856
			// We use the workaround from the last link. So we will create a reference list using the raw metadata to avoid the bug.
			Msg.PrintMod("Adding references.", ".exec.script", Msg.LogLevels.Debug);
			List<MetadataReference> references = new List<MetadataReference>();
			Assembly[] refs = AppDomain.CurrentDomain.GetAssemblies(); 
			foreach (Assembly asm in refs) {
				unsafe {
					Msg.PrintMod("Adding reference for: " + asm.GetName(), ".exec.script", Msg.LogLevels.Debug);
					if (asm.TryGetRawMetadata(out var blob, out var length))
						references.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
				}
			}
			// Create the compilation object
			CSharpCompilation compilation = CSharpCompilation.CreateScriptCompilation(AssemblyName,
				syntaxTree,
				references,
				options,
				null,
				returnType: typeof(object),
				null);
			Msg.PrintMod("Done preparing compilation environment.", ".exec.script", Msg.LogLevels.Debug);
			// Try to compile the script
			//
			try {
				//
				// Building block
				//
				Msg.PrintMod("Evaluate and build the code.", ".exec.script", Msg.LogLevels.Debug);

				// NOTE: GetDiagnostics() can crash due to a Roslyn 5.0.0 bug on .NET 10.0
				// We wrap it in try-catch to handle the crash gracefully
				var BuildResults = System.Collections.Immutable.ImmutableArray<Diagnostic>.Empty;
				try {
					BuildResults = compilation.GetDiagnostics();
				} catch (Exception diagEx) {
					Msg.PrintWarningMod($"GetDiagnostics() threw exception (Roslyn bug): {diagEx.GetType().Name}. Proceeding without diagnostics.", ".exec.script", Msg.LogLevels.Verbose);
				}

				// We may want to check BuildResults even if building exceptions are trapped.
				// Warnings are reported but only errors (or warnings promoted to errors) abort the build.
				//
				if (VersionErrors.Count > 0) {
					// A loaded file needs a newer Kombine: that is the reason, whatever the compiler said
					ReportFailure(ErrorCode.NotSupported, string.Join("\n", VersionErrors));
					return false;
				}
				bool HasErrors = BuildResults.Any(res => res.Severity == DiagnosticSeverity.Error || res.IsWarningAsError);
				if (HasErrors) {
					if (ResolveErrors.Count > 0) {
						// The references could not be resolved: that is the reason, the compile errors are its consequence
						ReportFailure(ErrorCode.NotFound, "The references of the script could not be resolved: " + filename + "\n" + string.Join("\n", ResolveErrors));
						return false;
					}
					string errors = "Errors found compiling the script: " + filename;
					foreach (Diagnostic res in BuildResults) {
						if (res.Severity == DiagnosticSeverity.Error || res.IsWarningAsError)
							errors += "\n" + res.ToString();
					}
					ReportFailure(ErrorCode.Failed, errors);
					return false;
				}
				foreach (Diagnostic res in BuildResults) {
					if (res.Severity == DiagnosticSeverity.Warning) {
						ReportWarning(res.ToString());
					}
				}
				Msg.PrintMod("Compilation done.", ".exec.script", Msg.LogLevels.Debug);
				// Get the compilation
				// Fetch the ILASM inside a memory stream and optinally debug symbols
				EmitResult CompiledResult;
				using (MemoryStream ms = new MemoryStream()) {
					if (Debug) {
						using (MemoryStream ds = new MemoryStream()) {
							Msg.PrintMod("Fetching compilation with debug.", ".exec.script", Msg.LogLevels.Debug);
							var emitOptions = new EmitOptions(false, DebugInformationFormat.PortablePdb);
							CompiledResult = compilation.Emit(ms, ds, options: emitOptions);
							if (CompiledResult.Success == true) {
								// Save state and serialize
								Msg.PrintMod("Saving binary with debug info. (invoked with -ksdbg)", ".exec.script", Msg.LogLevels.Debug);
								return State.SetCompiledScript(ms, ds);
							}
						}
					} else {
						Msg.PrintMod("Fetching compilation.", ".exec.script", Msg.LogLevels.Debug);
						CompiledResult = compilation.Emit(ms);
						if (CompiledResult.Success == true) {
							// Save state and serialize
							Msg.PrintMod("Saving binary.", ".exec.script", Msg.LogLevels.Debug);
							return State.SetCompiledScript(ms);
						}
					}
					string emitErrors = "The script binary could not be emitted: " + filename;
					foreach (Diagnostic res in CompiledResult.Diagnostics)
						emitErrors += "\n" + res.ToString();
					ReportFailure(ErrorCode.Failed, emitErrors);
					return false;
				}
			} catch (Exception ex) {
				string reason = "Exception during compilation: " + filename + "\n" + ex.Message;
				if (ex is CompilationErrorException compileError) {
					foreach (Diagnostic d in compileError.Diagnostics)
						reason += "\n" + d.ToString();
				}
				if (ex.InnerException != null)
					reason += "\nInner exception: " + ex.InnerException.Message;
				ReportFailure(ErrorCode.Failed, reason);
				// The imports and references only matter to debug the engine itself
				Msg.PrintMod("Imports and references of the failed compilation:", ".exec.script", Msg.LogLevels.Debug);
				foreach (string i in options.Usings)
					Msg.PrintMod("Import: " + i, ".exec.script", Msg.LogLevels.Debug);
				foreach (MetadataReference mr in references)
					Msg.PrintMod("Reference: " + mr.Display, ".exec.script", Msg.LogLevels.Debug);
				return false;
			}
		}

		/// <summary>
		/// Fetches the script text from the given filename.
		/// </summary>
		/// <param name="Debug"></param>
		/// <param name="filename"></param>
		/// <returns></returns>
		private string? FetchScriptText(string filename,bool Debug) {
			string? scriptText = null;
			try {
				if (string.IsNullOrEmpty(filename)) {
					ReportFailure(ErrorCode.InvalidArgument, "No script file to execute specified");
					return null;
				}
				scriptText = File.ReadAllText(filename);
			} catch (Exception ex) {
				ReportFailure(ApiError.CodeOf(ex), "The script file '" + filename + "' was not found or is not accessible: " + ex.Message);
				return null;
			}
			if (scriptText == null || scriptText.Length == 0) {
				ReportFailure(ErrorCode.InvalidArgument, "The script file '" + filename + "' is empty");
				return null;
			}
			// The minimum version the script declares, checked before anything is compiled
			string? versionError = CheckRequiredVersion(filename, scriptText.Split('\n'));
			if (versionError != null) {
				ReportFailure(ErrorCode.NotSupported, versionError);
				return null;
			}
			Msg.PrintMod("Compiling script.", ".exec.script", Msg.LogLevels.Debug);
			// Preprocesor to indicate the source file to use. Its needed to track source file from debugger.
			Msg.PrintMod("Adding source file debug reference (invoked with -ksdbg)", ".exec.script", Msg.LogLevels.Debug);
			string realfile = Path.GetFullPath(filename);
			scriptText = scriptText.Insert(0, "#line 1 \"" + realfile + "\"\r\n");
			return scriptText;
		}

		/// <summary>
		/// Forces the loading of all the referenced assemblies from kombine to be available for the scripts
		/// This could be tweaked to force only the loading of the desired assemblies to be available for the scripts
		/// TODO: To be optimized
		/// </summary>
		/// <param name="assembly">Assembly to get the references</param>
		private static void LoadReferencedAssembly(Assembly assembly) {
			
			foreach (AssemblyName name in assembly.GetReferencedAssemblies()) {
				if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName == name.FullName)){
					LoadReferencedAssembly(Assembly.Load(name));
				}
			}
		}

	}
}
