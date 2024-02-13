/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
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
		/// The script path. Used to resolve #load directives in the script among others
		/// </summary>
		internal string ScriptPath { get; set; } = string.Empty;

		/// <summary>
		/// If we should execute a debug build
		/// </summary>
		internal bool DebugBuild { get; private set; } = false;

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
			"Kltv.Kombine.Api.Statics"
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
			// Forces the assemblies to be loaded into memory (all of references)
			if (ParentScript == null){
				Msg.PrintMod("Loading all referenced assemblies into memory.", ".exec.script", Msg.LogLevels.Debug);
				Assembly a = Assembly.GetExecutingAssembly();
				LoadReferencedAssembly(a);
			}
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
				Msg.PrintErrorMod("Invalid script filename. Aborting.", ".exec.script");
				return -1;
			}
			//
			// Check state / fetch from cache
			//
			if ( (State.FetchCache(Scriptfile) == false) || (Config.Rebuild == true) ) {
				//
				// There is no previous state saved. We will try to build the script
				// 
				if (Config.Rebuild){
					Msg.PrintMod("Rebuild forced. Triggering rebuild.", ".exec.script", Msg.LogLevels.Debug);
				} else {
					Msg.PrintMod("No previous state. Triggering rebuild.", ".exec.script", Msg.LogLevels.Debug);
				}
				if (Compile(Scriptfile,DebugBuild) == false) {
					Msg.PrintErrorMod("There was errors building the script. Aborting.", ".exec.script");
					return -1;
				}
				Msg.PrintMod("Script '"+Scriptfile+"' compiled successfully.", ".exec.script", Msg.LogLevels.Debug);
			}
			// Load the built assembly from the saved state
			//
			Msg.PrintMod("Loading the compiled script.", ".exec.script", Msg.LogLevels.Debug);
			Assembly assembly;
			if (State.Data.CompiledScript == null) {
				Msg.PrintErrorMod("Could not load script. Bytes are null. Aborting.", ".exec.script");
				return -1;
			}
			if (State.Data.CompiledScriptPDB != null) {
				assembly = Assembly.Load(State.Data.CompiledScript, State.Data.CompiledScriptPDB);
			} else {
				assembly = Assembly.Load(State.Data.CompiledScript);
			}
			if (assembly == null) {
				Msg.PrintErrorMod("Something wrong happened loading the compiled script into memory. Aborting.", ".exec.script");
				return -1;
			}
			// Fetch the entrypoint
			// We use the script class (defined in compilation step with the scriptname pluss _class sufix)
			// 
			Msg.PrintMod("Fetching the entrypoint.", ".exec.script", Msg.LogLevels.Debug);
			Type? ScriptClass = assembly.ExportedTypes.FirstOrDefault(x => x.Name == ClassName); 
			if (ScriptClass == null) {
				Msg.PrintErrorMod("Something wrong happened retrieving the script underlying class. Aborting.", ".exec.script");
				return -1;
			}
			MethodInfo? entrypoint;
			// First try to get entrypoint for top level statements.
			// Its the preferred way to code the script but we support a "main" function also
			entrypoint = ScriptClass.GetMethod("<Factory>", BindingFlags.Static | BindingFlags.Public);
			if (entrypoint == null) {
				Msg.PrintErrorMod("No top level statements as entrypoint found.", ".exec.script", Msg.LogLevels.Debug);
				Msg.PrintErrorMod("Something wrong happened retrieving the script entry point. Aborting.", ".exec.script");
				return -1;
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
					Msg.PrintErrorMod("Failed evaluating the global script execution. Aborting.", ".exec.script");
					return -1;
				}
				// After the execution of the top level statements, the runtime environment contains
				// the instance created to run the top level statements, so, we can use it as an instance to call our methods.
				entrypoint = ScriptClass.GetMethod(Action);
				if (entrypoint == null) {
					Msg.PrintErrorMod("Something wrong happened retrieving the script entry point. Aborting.", ".exec.script");
					return -1;
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
					return code;
				}
				Msg.PrintWarningMod("Script executed but the returned code was wrong. Review your action.", ".exec.script",Msg.LogLevels.Normal);
				return 0;
			} catch (Exception ex) {
				//
				// Here we catch if the script execution failed (could not be invoked) or the script itself raised an exception.
				// If the scripts wants to abort execution, it will raise an exception that we must catch here just to return -1
				// but do not stop the kombine process.
				//
				if (ex is ScriptAbortException){
					Msg.PrintErrorMod("Script aborted execution.", ".exec.script");
					return -1;
				}
				if (ex.InnerException != null) {
					Msg.PrintErrorMod("Script exception: " + ex.InnerException.Message, ".exec.script");
				}
				Msg.PrintErrorMod("Failed executing script: " + ex.Message, ".exec.script");
				return -1;
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
						Msg.PrintErrorMod("Script globals execution could not be evaluated. Invalid Object cast.", ".exec.script");
						return false;
					}
					if (!t.IsCompletedSuccessfully) {
						Msg.PrintWarningMod("Script globals failed execution", ".exec.script");
						Msg.PrintWarningMod("State: " + t.Status.ToString(), ".exec.script");
						if (t.Exception != null) {
							Msg.PrintWarningMod("Exception: " + t.Exception.Message, ".exec.script");
						}
						return false;
					} else {
						Msg.PrintMod("Script globals executed successfully.", ".exec.script", Msg.LogLevels.Debug);
						return true;
					}
				} else {
					Msg.PrintErrorMod("Script globals execution could not be evaluated. Invalid Object type.", ".exec.script");
					return false;
				}
			} else {
				Msg.PrintErrorMod("Script globals execution could not be evaluated. No returned information.", ".exec.script");
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
			// Load the script text
			string? scriptText = FetchScriptText(filename,Debug);
			if (scriptText == null){
				Msg.PrintErrorMod("Could not load script text. Aborting",".exec.script");
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
				// Allow concurrent build.
				options = options.WithConcurrentBuild(true);
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
				// Nullability: We will use the default one but this is prone to be tweaked by command line / config
				options = options.WithNullableContextOptions(NullableContextOptions.Enable);
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
				var BuildResults = compilation.GetDiagnostics();
				// We may want to check BuildResults even if building exceptions are trapped.
				//
				if (BuildResults.Length > 0) {
					Msg.PrintErrorMod("Errors found compiling the script: "+filename, ".exec.script");
					Msg.BeginIndent();
					foreach (Diagnostic res in BuildResults) {
						if (res.IsWarningAsError){
							Msg.PrintWarning(res.ToString());
						} else{
							Msg.PrintError(res.ToString()); 
						}
					}
					Msg.EndIndent();
					Msg.PrintErrorMod("Aborting.", ".exec.script");
					return false;
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
					Msg.PrintWarningMod("Failed when retrieving binary.", ".exec.script");
					foreach (Diagnostic res in CompiledResult.Diagnostics) {
						Msg.Print(res.ToString()); 
					}
					// TODO: If we hit this point we had errors so we can check also for CompiledResult here
					// since in the exception handler we will trap build errors but no things like unresolved references or other scripts.
					return false;
				}
			} catch (Exception ex) {
				if (ex is CompilationErrorException) {
					CompilationErrorException? e = ex as CompilationErrorException;
					if (e != null) {
						Msg.PrintErrorMod("Compilation errors: "+filename, ".exec.script");
						foreach(Diagnostic d in e.Diagnostics) {
							Msg.PrintErrorMod(d.ToString(), ".exec.script");
						}
					} else {
						Msg.PrintErrorMod("Compilation errors: ", ".exec.script");
						Msg.PrintErrorMod(ex.Message, ".exec.script");
					}
				}
				if (ex.InnerException != null) {
					Msg.PrintErrorMod("Inner exception: "+ex.InnerException.Message,".exec.script");
				}
				Msg.PrintWarningMod("Exception during compilation: " + ex.Message+" Type:"+ex.GetType().ToString(), ".exec.script");
				Msg.PrintErrorMod("With following imports and references:", ".exec.script");
				foreach(string i in options.Usings) {
					Msg.PrintErrorMod("Import: " + i, ".exec.script]");
				}
				foreach (MetadataReference mr in references) {
					Msg.PrintErrorMod("Reference: " + mr.Display, ".exec.script]");
				}
			}
			// We should not hit this point.
			Msg.PrintErrorMod("Compile script failed uncontrolled.", ".exec.script");
			return false;
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
					Msg.PrintErrorMod("No script file to execute specified.", ".exec.script");
					return null;
				}
				scriptText = File.ReadAllText(filename);
			} catch (Exception ex) {
				Msg.PrintErrorMod("File to execute '"+filename+"' is not found or innacessible. Exception:"+ex.Message, ".exec.script");
				return null;
			}
			if (scriptText == null || scriptText.Length == 0) {
				Msg.PrintErrorMod("File to execute '"+filename+"' is wrong or empty.", ".exec.script");
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