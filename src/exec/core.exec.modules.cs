/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using BinaryPack.Attributes;
using BinaryPack.Enums;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	/// <summary>
	/// The modules of a run: the loaded files that carry "#pragma kombine module" and are compiled once,
	/// as their own assembly, cached by content and shared by every script that loads them. A loaded file
	/// without the pragma keeps being merged into the script that loads it.
	/// </summary>
	internal static class Modules {

		/// <summary>
		/// One module: its file, the names it is known by, what it was built from and its assembly once loaded.
		/// </summary>
		internal class Module {
			/// <summary>The full path of the file: for a remote module, its cached copy.</summary>
			public string Path = string.Empty;
			/// <summary>The URL of a remote module, empty for a local one.</summary>
			public string Origin = string.Empty;
			/// <summary>The file name (the last segment of the URL for a remote module), the name the duplicates are detected by.</summary>
			public string Name = string.Empty;
			/// <summary>The module as the messages name it: the URL of a remote one with its cached copy, the path otherwise.</summary>
			public string Display { get { return Origin.Length > 0 ? Origin + " (cached at " + Path + ")" : Path; } }
			/// <summary>The generated static class that holds the top level members and the initializer; the scripts import it.</summary>
			public string ClassName = string.Empty;
			/// <summary>The assembly name, unique per file and per content.</summary>
			public string AssemblyName = string.Empty;
			/// <summary>The content hash of the file.</summary>
			public string Hash = string.Empty;
			/// <summary>The modules this module loads, in dependency order, transitively.</summary>
			public List<Module> ModulesLoaded = new List<Module>();
			/// <summary>Every file the module depends on with its content hash: itself, its helpers and its modules, transitively.</summary>
			public Dictionary<string, string> Dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			/// <summary>The helpers merged into the module: their path and text.</summary>
			public List<(string path, string text)> Helpers = new List<(string, string)>();
			/// <summary>The compiled bytes and debug symbols.</summary>
			public byte[]? Bytes = null;
			public byte[]? Pdb = null;
			/// <summary>The assembly, once loaded into the process.</summary>
			public Assembly? Assembly = null;
			/// <summary>True once the initializer (the top level statements) ran.</summary>
			public bool Initialized = false;
			/// <summary>True when the module was compiled in this run, false when it came from its state.</summary>
			public bool Compiled = false;
		}

		/// <summary>The state of a module in the cache.</summary>
		[BinarySerialization(SerializationMode.Fields)]
		public class ModuleFile {
			public long Signature;
			public string Version = "invalid";
			public bool BuildWithDebug = false;
			public string AssemblyName = string.Empty;
			public string ClassName = string.Empty;
			public string SourceHash = string.Empty;
			public string[] DependencyPaths = new string[0];
			public string[] DependencyHashes = new string[0];
			public byte[]? CompiledModule = null;
			public byte[]? CompiledModulePDB = null;
		}

		private const long ModuleSignature = 0x000030001;

		/// <summary>The modules of the run, by full path.</summary>
		private static readonly Dictionary<string, Module> byPath = new Dictionary<string, Module>(StringComparer.OrdinalIgnoreCase);

		/// <summary>The loaded modules, by assembly name, for the resolve handler.</summary>
		private static readonly Dictionary<string, Assembly> byAssemblyName = new Dictionary<string, Assembly>(StringComparer.Ordinal);

		/// <summary>The modules seen, by file name, for the duplicate detection.</summary>
		private static readonly Dictionary<string, Module> byName = new Dictionary<string, Module>(StringComparer.OrdinalIgnoreCase);

		private static bool resolveHooked = false;

		/// <summary>The kind of the last failure reported by Get: NotSupported for a version the engine does not have, Failed otherwise.</summary>
		internal static ErrorCode FailureCode { get; private set; } = ErrorCode.Failed;

		private static readonly Regex LoadDirective = new Regex(@"^\s*#load\s+""([^""]+)""", RegexOptions.Compiled);
		private static readonly Regex ModulePragma = new Regex(@"^\s*#pragma\s+kombine\s+module\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>The prefix of every module assembly name, so the consumers reference their own modules only.</summary>
		internal const string AssemblyPrefix = "kmodule.";

		// ------------------------------------------------------------------------------------------------
		// Hashing
		// ------------------------------------------------------------------------------------------------

		/// <summary>The content hash of a text.</summary>
		internal static string Hash(string text) {
			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
		}

		/// <summary>The content hash of a file, empty when it cannot be read.</summary>
		internal static string HashFile(string path) {
			try {
				return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
			} catch {
				return string.Empty;
			}
		}

		// ------------------------------------------------------------------------------------------------
		// The pre pass over the #load lines
		// ------------------------------------------------------------------------------------------------

		/// <summary>
		/// True when the file declares itself a module ("#pragma kombine module" in its first lines).
		/// </summary>
		internal static bool IsModuleFile(string path) {
			try {
				int count = 0;
				foreach (string line in File.ReadLines(path)) {
					if (count++ >= 40)
						break;
					if (ModulePragma.IsMatch(line))
						return true;
				}
			} catch {
			}
			return false;
		}

		/// <summary>
		/// True when the path is a module of this run: the source resolver then hands empty text to the
		/// compiler, since the module is referenced as an assembly, not merged.
		/// </summary>
		internal static bool IsModule(string path) {
			return byPath.ContainsKey(System.IO.Path.GetFullPath(path));
		}

		/// <summary>
		/// Resolves the #load lines of a file (a script, a helper or a module), the way the source resolver
		/// does, and prepares what the loading compilation needs: the modules to reference, in dependency
		/// order and transitively, and every file the compilation depends on with its content hash (the
		/// helpers merged and the modules referenced, transitively). The modules found are compiled or
		/// taken from their state, and loaded. Returns false with the reasons when something failed.
		/// </summary>
		/// <param name="file">The file whose #load lines are read.</param>
		/// <param name="text">Its text.</param>
		/// <param name="baseDir">The folder of the file, where a relative #load is looked for first (null for the root script).</param>
		/// <param name="scriptDir">The folder of the script, the second place a relative #load is looked for.</param>
		/// <param name="modules">Receives the modules to reference, in dependency order, each once.</param>
		/// <param name="dependencies">Receives every file the compilation depends on with its content hash.</param>
		/// <param name="helpers">Receives the helpers merged into a module (null for a script, whose helpers the compiler merges itself).</param>
		/// <param name="errors">Receives the reasons of a failure.</param>
		/// <param name="visiting">The files on the current path, to stop a cycle.</param>
		internal static bool Resolve(string file, string text, string? baseDir, string scriptDir, List<Module> modules, Dictionary<string, string> dependencies, List<(string path, string text)>? helpers, List<string> errors, HashSet<string>? visiting = null) {
			visiting ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string full = System.IO.Path.GetFullPath(file);
			if (!visiting.Add(full))
				return true;
			bool ok = true;
			foreach (string raw in text.Split('\n')) {
				Match m = LoadDirective.Match(raw);
				if (!m.Success)
					continue;
				string reference = m.Groups[1].Value;
				// A remote file resolves to its cached copy, fetched by the rules of the resolver
				bool remote = reference.StartsWith("http://") || reference.StartsWith("https://");
				string? resolved = Folders.ResolveFilename(reference, baseDir, scriptDir);
				if (resolved == null) {
					// The source resolver will report it with the reason when the compilation runs
					continue;
				}
				if (IsModuleFile(resolved)) {
					Module? module = Get(resolved, errors, remote ? reference : null);
					if (module == null) {
						ok = false;
						continue;
					}
					AddModule(modules, module);
					foreach (KeyValuePair<string, string> d in module.Dependencies)
						dependencies[d.Key] = d.Value;
				} else {
					// A helper: merged by the compiler; its own loads may name modules
					string helperText;
					try {
						helperText = File.ReadAllText(resolved);
					} catch (Exception ex) {
						errors.Add("#load \"" + reference + "\": " + ex.Message);
						ok = false;
						continue;
					}
					dependencies[resolved] = Hash(helperText);
					helpers?.Add((resolved, helperText));
					if (!Resolve(resolved, helperText, System.IO.Path.GetDirectoryName(resolved), scriptDir, modules, dependencies, helpers, errors, visiting))
						ok = false;
				}
			}
			visiting.Remove(full);
			return ok;
		}

		/// <summary>
		/// Adds a module and the modules it loads to a reference list, in dependency order, each once.
		/// </summary>
		private static void AddModule(List<Module> list, Module module) {
			foreach (Module inner in module.ModulesLoaded)
				AddModule(list, inner);
			if (!list.Contains(module))
				list.Add(module);
		}

		// ------------------------------------------------------------------------------------------------
		// Compile or load
		// ------------------------------------------------------------------------------------------------

		/// <summary>
		/// The module of a file: compiled in this run or taken from its state, and loaded into the process
		/// with its initializer run once. Null with the reasons in errors when it could not be made.
		/// </summary>
		/// <param name="path">The file: for a remote module, its cached copy.</param>
		/// <param name="errors">Receives the reasons of a failure.</param>
		/// <param name="origin">The URL of a remote module, null for a local one.</param>
		internal static Module? Get(string path, List<string> errors, string? origin = null) {
			string full = System.IO.Path.GetFullPath(path);
			if (byPath.TryGetValue(full, out Module? known))
				return known;
			string text;
			try {
				text = File.ReadAllText(full);
			} catch (Exception ex) {
				errors.Add("module " + (origin ?? path) + ": " + ex.Message);
				return null;
			}
			// The version the module needs, checked before anything is compiled
			string? versionError = KombineScript.CheckRequiredVersion(origin ?? full, text.Split('\n'));
			if (versionError != null) {
				FailureCode = ErrorCode.NotSupported;
				errors.Add(versionError);
				return null;
			}
			FailureCode = ErrorCode.Failed;
			Module module = new Module();
			module.Path = full;
			module.Origin = origin ?? string.Empty;
			module.Name = origin != null ? NameOfUrl(origin, full) : System.IO.Path.GetFileName(full);
			module.Hash = Hash(text);
			string baseName = Sanitize(System.IO.Path.GetFileNameWithoutExtension(module.Name));
			string pathHash = Hash(full.ToLowerInvariant()).Substring(0, 8).ToLowerInvariant();
			module.ClassName = baseName + "_module";
			module.AssemblyName = AssemblyPrefix + baseName + "." + pathHash + "." + module.Hash.Substring(0, 8).ToLowerInvariant();
			module.Dependencies[full] = module.Hash;
			// Two copies of one module in a run: the second file with the same name and another content
			if (byName.TryGetValue(module.Name, out Module? seen) && !string.Equals(seen.Path, full, StringComparison.OrdinalIgnoreCase)) {
				string message = "The module " + module.Name + " is loaded from two different files in this run: " + seen.Display + " and " + module.Display + (seen.Hash == module.Hash ? " (same content)" : " (different content)");
				if (Config.ModulesStrict) {
					errors.Add(message);
					return null;
				}
				Msg.PrintWarningMod(message, ".exec.modules");
			} else {
				byName[module.Name] = module;
			}
			// Registered before its own loads are resolved, so a cycle stops here
			byPath[full] = module;
			// Its own loads: the modules it references and the helpers merged into it
			string folder = System.IO.Path.GetDirectoryName(full) ?? string.Empty;
			if (!Resolve(full, text, folder, folder, module.ModulesLoaded, module.Dependencies, module.Helpers, errors)) {
				byPath.Remove(full);
				return null;
			}
			// From the state when nothing it was built from changed, compiled otherwise
			if (Config.Rebuild || !LoadState(module)) {
				if (!Compile(module, text, errors)) {
					byPath.Remove(full);
					return null;
				}
				SaveState(module);
				module.Compiled = true;
			}
			if (!Load(module, errors)) {
				byPath.Remove(full);
				return null;
			}
			return module;
		}

		/// <summary>
		/// The file name of a remote module: the last segment of the URL without its query, or the name
		/// of the cached copy when the URL ends with a slash.
		/// </summary>
		private static string NameOfUrl(string url, string cached) {
			string name = url;
			int query = name.IndexOfAny(new[] { '?', '#' });
			if (query >= 0)
				name = name.Substring(0, query);
			name = name.TrimEnd('/');
			int slash = name.LastIndexOf('/');
			if (slash >= 0)
				name = name.Substring(slash + 1);
			return name.Length > 0 ? name : System.IO.Path.GetFileName(cached);
		}

		/// <summary>
		/// A name usable in an identifier: letters, digits and underscores.
		/// </summary>
		private static string Sanitize(string name) {
			StringBuilder sb = new StringBuilder();
			foreach (char c in name)
				sb.Append(char.IsLetterOrDigit(c) ? c : '_');
			if (sb.Length == 0 || char.IsDigit(sb[0]))
				sb.Insert(0, '_');
			return sb.ToString();
		}

		// ------------------------------------------------------------------------------------------------
		// The state of a module
		// ------------------------------------------------------------------------------------------------

		/// <summary>
		/// Loads the compiled bytes of a module from its state when the state exists, was written by this
		/// engine version with the same debug setting, and every file the module was built from has the
		/// same content hash.
		/// </summary>
		private static bool LoadState(Module module) {
			string file = Cache.ConvertModuleFilename(module.Path);
			if (!File.Exists(file))
				return false;
			ModuleFile state;
			try {
				state = BinaryPack.BinaryConverter.Deserialize<ModuleFile>(File.ReadAllBytes(file));
			} catch (Exception ex) {
				Msg.PrintMod("The state of the module " + module.Name + " cannot be read (" + ex.Message + "), it will be compiled", ".exec.modules", Msg.LogLevels.Verbose);
				return false;
			}
			if (state.Signature != ModuleSignature || state.Version != EngineVersion() || state.BuildWithDebug != Config.BuildDebug || state.SourceHash != module.Hash || state.CompiledModule == null) {
				Msg.PrintMod("The state of the module " + module.Name + " is of another build, it will be compiled", ".exec.modules", Msg.LogLevels.Verbose);
				return false;
			}
			for (int i = 0; i < state.DependencyPaths.Length && i < state.DependencyHashes.Length; i++) {
				if (!module.Dependencies.TryGetValue(state.DependencyPaths[i], out string? hash) || hash != state.DependencyHashes[i]) {
					Msg.PrintMod("The module " + module.Name + " depends on " + state.DependencyPaths[i] + ", which changed: it will be compiled", ".exec.modules", Msg.LogLevels.Verbose);
					return false;
				}
			}
			if (state.DependencyPaths.Length != module.Dependencies.Count)
				return false;
			module.AssemblyName = state.AssemblyName;
			module.ClassName = state.ClassName;
			module.Bytes = state.CompiledModule;
			module.Pdb = state.CompiledModulePDB;
			Msg.PrintMod("The module " + module.Name + " comes from its state", ".exec.modules", Msg.LogLevels.Debug);
			return true;
		}

		/// <summary>
		/// Writes the state of a module just compiled.
		/// </summary>
		private static void SaveState(Module module) {
			ModuleFile state = new ModuleFile();
			state.Signature = ModuleSignature;
			state.Version = EngineVersion();
			state.BuildWithDebug = Config.BuildDebug;
			state.AssemblyName = module.AssemblyName;
			state.ClassName = module.ClassName;
			state.SourceHash = module.Hash;
			state.DependencyPaths = module.Dependencies.Keys.ToArray();
			state.DependencyHashes = module.Dependencies.Values.ToArray();
			state.CompiledModule = module.Bytes;
			state.CompiledModulePDB = module.Pdb;
			try {
				File.WriteAllBytes(Cache.ConvertModuleFilename(module.Path), BinaryPack.BinaryConverter.Serialize(state));
			} catch (Exception ex) {
				Msg.PrintWarningMod("The state of the module " + module.Name + " could not be saved: " + ex.Message, ".exec.modules", Msg.LogLevels.Verbose);
			}
		}

		private static string EngineVersion() {
			return KombineMain.Version.Major + "." + KombineMain.Version.Minor + "." + KombineMain.Version.Build;
		}

		// ------------------------------------------------------------------------------------------------
		// The transformation and the compilation
		// ------------------------------------------------------------------------------------------------

		/// <summary>
		/// Compiles a module as a regular library. The file is written in script syntax: its top level
		/// types stay top level types (public unless they say otherwise), its top level statements become
		/// the initializer of a generated static class named after the module, and its top level
		/// functions and variables become static members of that class; the helpers it loads are merged
		/// the same way. The #load and #r directives are handled here, not by the compiler.
		/// </summary>
		private static bool Compile(Module module, string text, List<string> errors) {
			Msg.PrintMod("Compiling the module " + module.Name, ".exec.modules", Msg.LogLevels.Verbose);
			// The references: the engine and the framework as the scripts see them, plus the modules it
			// loads; the transformation adds the #r references of the files
			List<MetadataReference> references = new List<MetadataReference>();
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
				string? name = asm.GetName().Name;
				if (name != null && name.StartsWith(AssemblyPrefix))
					continue;
				unsafe {
					if (asm.TryGetRawMetadata(out byte* blob, out int length))
						references.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
				}
			}
			foreach (Module inner in module.ModulesLoaded) {
				MetadataReference? r = ReferenceOf(inner);
				if (r != null)
					references.Add(r);
			}
			List<string> resolveErrors = new List<string>();
			string generated = Transform(module, text, references, resolveErrors);
			if (resolveErrors.Count > 0) {
				errors.Add("The references of the module " + module.Name + " could not be resolved:\n" + string.Join("\n", resolveErrors));
				return false;
			}
			CSharpParseOptions parse = new CSharpParseOptions(languageVersion: LanguageVersion.Latest, kind: SourceCodeKind.Regular);
			// The generated parts (the usings, the module class) are named after the module; the parts
			// taken from the files carry line directives to them
			SyntaxTree tree = CSharpSyntaxTree.ParseText(generated, parse, module.Path + ".generated.cs");
			CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
				.WithAllowUnsafe(false)
				.WithConcurrentBuild(false)
				.WithDeterministic(true)
				.WithGeneralDiagnosticOption(ReportDiagnostic.Default)
				.WithMetadataImportOptions(MetadataImportOptions.Public)
				.WithModuleName(module.AssemblyName + ".dll")
				.WithNullableContextOptions(NullableContextOptions.Disable)
				.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> {
					{ "CS8632", ReportDiagnostic.Suppress },
					{ "CS1633", ReportDiagnostic.Suppress },
					// The default usings of a script are added to the module text: the module's own repeat some
					{ "CS0105", ReportDiagnostic.Suppress }
				})
				.WithPlatform(Platform.AnyCpu);
			if (Config.BuildDebug)
				options = options.WithOverflowChecks(true).WithOptimizationLevel(OptimizationLevel.Debug);
			else
				options = options.WithOverflowChecks(false).WithOptimizationLevel(OptimizationLevel.Release);
			CSharpCompilation compilation = CSharpCompilation.Create(module.AssemblyName, new[] { tree }, references, options);
			try {
				var diagnostics = System.Collections.Immutable.ImmutableArray<Diagnostic>.Empty;
				try {
					diagnostics = compilation.GetDiagnostics();
				} catch (Exception ex) {
					Msg.PrintWarningMod("GetDiagnostics() threw (Roslyn bug): " + ex.GetType().Name + ". Proceeding without diagnostics.", ".exec.modules", Msg.LogLevels.Verbose);
				}
				if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)) {
					string report = "Errors found compiling the module: " + module.Display;
					foreach (Diagnostic d in diagnostics)
						if (d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
							report += "\n" + d.ToString();
					errors.Add(report);
					return false;
				}
				foreach (Diagnostic d in diagnostics)
					if (d.Severity == DiagnosticSeverity.Warning)
						Msg.PrintWarningMod(d.ToString(), ".exec.modules", Msg.LogLevels.Verbose);
				using (MemoryStream ms = new MemoryStream()) {
					EmitResult result;
					if (Config.BuildDebug) {
						using (MemoryStream ds = new MemoryStream()) {
							result = compilation.Emit(ms, ds, options: new EmitOptions(false, DebugInformationFormat.PortablePdb));
							if (result.Success) {
								module.Bytes = ms.ToArray();
								module.Pdb = ds.ToArray();
								return true;
							}
						}
					} else {
						result = compilation.Emit(ms);
						if (result.Success) {
							module.Bytes = ms.ToArray();
							module.Pdb = null;
							return true;
						}
					}
					string emitErrors = "The module binary could not be emitted: " + module.Display;
					foreach (Diagnostic d in result.Diagnostics)
						emitErrors += "\n" + d.ToString();
					errors.Add(emitErrors);
					return false;
				}
			} catch (Exception ex) {
				errors.Add("Exception compiling the module " + module.Display + ": " + ex.Message);
				return false;
			}
		}

		/// <summary>
		/// The text of the regular library of a module: the usings, the static imports of the modules it
		/// loads, its top level types, and the generated class with the members and the initializer.
		/// </summary>
		private static string Transform(Module module, string text, List<MetadataReference> references, List<string> resolveErrors) {
			StringBuilder usings = new StringBuilder();
			StringBuilder types = new StringBuilder();
			StringBuilder members = new StringBuilder();
			StringBuilder statements = new StringBuilder();
			HashSet<string> seenUsings = new HashSet<string>();
			// The default usings of a script name namespaces and static classes alike; a regular library
			// needs "using static" for the classes
			foreach (string u in KombineScript.DefaultUsings) {
				string line = (IsEngineType(u) ? "using static " : "using ") + u + ";";
				if (seenUsings.Add(line))
					usings.AppendLine(line);
			}
			// The module imports its own class, so its code names the injected properties and its helpers
			usings.AppendLine("using static global::" + module.ClassName + ";");
			foreach (Module inner in module.ModulesLoaded)
				usings.AppendLine("using static global::" + inner.ClassName + ";");
			List<(string path, string text)> units = new List<(string, string)> { (module.Path, text) };
			units.AddRange(module.Helpers);
			// The units parsed in script form, once
			CSharpParseOptions parse = new CSharpParseOptions(languageVersion: LanguageVersion.Latest, kind: SourceCodeKind.Script);
			List<(string path, SyntaxTree tree)> parsed = new List<(string, SyntaxTree)>();
			foreach ((string path, string unitText) in units)
				parsed.Add((path, CSharpSyntaxTree.ParseText(unitText, parse, path)));
			// The usings of every unit and the #r references, resolved as the scripts resolve them
			bool hasVar = false;
			foreach ((string path, SyntaxTree tree) in parsed) {
				CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
				foreach (UsingDirectiveSyntax u in root.Usings) {
					string line = u.WithoutTrivia().ToFullString().Trim();
					if (seenUsings.Add(line)) {
						usings.AppendLine("#line " + (tree.GetLineSpan(u.Span).StartLinePosition.Line + 1) + " \"" + path + "\"");
						usings.AppendLine(line);
					}
				}
				foreach (ReferenceDirectiveTriviaSyntax r in root.GetReferenceDirectives()) {
					string file = r.File.ValueText;
					var resolved = new KombineScript.AssemblyResolver().ResolveReference(file, path, default);
					if (resolved.IsDefaultOrEmpty)
						resolveErrors.Add("#r \"" + file + "\" could not be resolved");
					else
						references.AddRange(resolved);
				}
				foreach (MemberDeclarationSyntax member in root.Members)
					if (member is FieldDeclarationSyntax field && field.Declaration.Type.IsVar)
						hasVar = true;
			}
			// The types of the top level "var" variables: a script infers them, a class cannot
			Dictionary<string, string> varTypes = hasVar ? InferVarTypes(module, units, references) : new Dictionary<string, string>();
			foreach ((string path, SyntaxTree tree) in parsed)
				TransformUnit(path, tree, types, members, statements, varTypes);
			StringBuilder sb = new StringBuilder();
			sb.Append(usings);
			sb.AppendLine();
			sb.Append(types);
			sb.AppendLine();
			sb.AppendLine("/// <summary>The top level members and the initializer of the module " + module.Name + ", imported by every script that loads it.</summary>");
			sb.AppendLine("public static class " + module.ClassName + " {");
			sb.AppendLine("\tpublic static string CurrentWorkingFolder { get { return Kltv.Kombine.Api.Folders.CurrentWorkingFolder; } }");
			sb.AppendLine("\tpublic static string CurrentScriptFolder { get { return Kltv.Kombine.Api.Folders.CurrentScriptFolder; } }");
			sb.AppendLine("\tpublic static string CurrentToolFolder { get { return Kltv.Kombine.Api.Folders.CurrentToolFolder; } }");
			sb.AppendLine("\tpublic static string ParentScriptFolder { get { return Kltv.Kombine.Api.Folders.ParentScriptFolder; } }");
			sb.Append(members);
			sb.AppendLine("\t/// <summary>The top level statements of the module, run once when it is first loaded.</summary>");
			sb.AppendLine("\tpublic static void __KombineInitialize() {");
			sb.Append(statements);
			sb.AppendLine("\t}");
			sb.AppendLine("}");
			return sb.ToString();
		}

		/// <summary>
		/// True when a dotted name of the default usings is a type of the engine, nested or not, and
		/// not a namespace.
		/// </summary>
		private static bool IsEngineType(string name) {
			Assembly engine = typeof(Modules).Assembly;
			string[] parts = name.Split('.');
			// The namespace part shrinks from the whole name down to the first segment; the rest is a nested type path
			for (int i = parts.Length; i >= 1; i--) {
				string candidate = string.Join(".", parts, 0, i);
				if (i < parts.Length)
					candidate += "+" + string.Join("+", parts, i, parts.Length - i);
				if (engine.GetType(candidate) != null)
					return true;
			}
			return false;
		}

		/// <summary>
		/// The types of the top level variables declared with "var", by name, inferred by a script
		/// compilation of the units merged the way the scripts merge them. A variable whose type cannot
		/// be named (an anonymous type, an initializer that does not bind) is left out and keeps its
		/// "var", which the compile of the module then reports.
		/// </summary>
		private static Dictionary<string, string> InferVarTypes(Module module, List<(string path, string text)> units, List<MetadataReference> references) {
			Dictionary<string, string> found = new Dictionary<string, string>();
			// The merged text: the usings first, the directives the compiler does not know left out
			StringBuilder usings = new StringBuilder();
			StringBuilder body = new StringBuilder();
			foreach ((string path, string text) in units) {
				foreach (string raw in text.Split('\n')) {
					string line = raw.TrimEnd('\r');
					string trimmed = line.TrimStart();
					if (trimmed.StartsWith("#load") || trimmed.StartsWith("#r ") || trimmed.StartsWith("#r\"") || (trimmed.StartsWith("#pragma") && trimmed.Contains("kombine")))
						continue;
					if (trimmed.StartsWith("using ") && trimmed.TrimEnd().EndsWith(";") && !trimmed.Contains("("))
						usings.AppendLine(line);
					else
						body.AppendLine(line);
				}
			}
			try {
				List<string> imports = new List<string>(KombineScript.DefaultUsings);
				foreach (Module inner in module.ModulesLoaded)
					imports.Add(inner.ClassName);
				CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
					.WithNullableContextOptions(NullableContextOptions.Disable)
					.WithUsings(imports);
				SyntaxTree tree = CSharpSyntaxTree.ParseText(usings.ToString() + body.ToString(), new CSharpParseOptions(languageVersion: LanguageVersion.Latest, kind: SourceCodeKind.Script));
				CSharpCompilation compilation = CSharpCompilation.CreateScriptCompilation(module.AssemblyName + ".infer", tree, references, options, null, typeof(object), null);
				INamedTypeSymbol? script = compilation.ScriptClass;
				if (script == null)
					return found;
				foreach ((string path, string text) in units) {
					CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(languageVersion: LanguageVersion.Latest, kind: SourceCodeKind.Script), path).GetCompilationUnitRoot();
					foreach (MemberDeclarationSyntax member in root.Members) {
						if (member is not FieldDeclarationSyntax field || !field.Declaration.Type.IsVar)
							continue;
						foreach (VariableDeclaratorSyntax v in field.Declaration.Variables) {
							IFieldSymbol? symbol = script.GetMembers(v.Identifier.ValueText).OfType<IFieldSymbol>().FirstOrDefault();
							if (symbol == null || symbol.Type.TypeKind == TypeKind.Error || symbol.Type.IsAnonymousType)
								continue;
							found[v.Identifier.ValueText] = symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
						}
					}
				}
			} catch (Exception ex) {
				Msg.PrintMod("The types of the var variables of the module " + module.Name + " could not be inferred: " + ex.Message, ".exec.modules", Msg.LogLevels.Verbose);
			}
			return found;
		}

		/// <summary>
		/// Splits one file parsed in script form into the parts of the library: types, members and
		/// statements, each piece preceded by a #line directive to its origin.
		/// </summary>
		/// <param name="path">The file, named by the line directives.</param>
		/// <param name="tree">Its syntax tree.</param>
		/// <param name="types">Receives the top level types.</param>
		/// <param name="members">Receives the members of the module class.</param>
		/// <param name="statements">Receives the top level statements, the body of the initializer.</param>
		/// <param name="varTypes">The types of the top level "var" variables, by name.</param>
		private static void TransformUnit(string path, SyntaxTree tree, StringBuilder types, StringBuilder members, StringBuilder statements, Dictionary<string, string> varTypes) {
			CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
			foreach (MemberDeclarationSyntax member in root.Members) {
				string comments = TriviaText(member.GetLeadingTrivia());
				int line = tree.GetLineSpan(member.Span).StartLinePosition.Line + 1;
				if (member is GlobalStatementSyntax global) {
					statements.Append(comments);
					statements.AppendLine("#line " + line + " \"" + path + "\"");
					statements.AppendLine(global.Statement.WithLeadingTrivia(SyntaxTriviaList.Empty).ToFullString());
					continue;
				}
				if (member is BaseTypeDeclarationSyntax || member is DelegateDeclarationSyntax || member is BaseNamespaceDeclarationSyntax) {
					MemberDeclarationSyntax typed = member is BaseNamespaceDeclarationSyntax ? member : WithAccessibility(member, false);
					types.Append(comments);
					types.AppendLine("#line " + line + " \"" + path + "\"");
					types.AppendLine(typed.WithLeadingTrivia(SyntaxTriviaList.Empty).ToFullString());
					continue;
				}
				// A top level function, variable, property or event: a static member of the module class
				MemberDeclarationSyntax made = member;
				if (made is FieldDeclarationSyntax field && field.Declaration.Type.IsVar && field.Declaration.Variables.Count == 1 && varTypes.TryGetValue(field.Declaration.Variables[0].Identifier.ValueText, out string? inferred)) {
					TypeSyntax typeName = SyntaxFactory.ParseTypeName(inferred).WithTriviaFrom(field.Declaration.Type);
					made = field.WithDeclaration(field.Declaration.WithType(typeName));
				}
				made = WithAccessibility(made, true);
				members.Append(comments);
				members.AppendLine("#line " + line + " \"" + path + "\"");
				members.AppendLine(made.WithLeadingTrivia(SyntaxTriviaList.Empty).ToFullString());
			}
		}

		/// <summary>
		/// The comments of a leading trivia, without the directives the compiler must not see again
		/// (#load, #r, #pragma kombine) and without the structured ones that belong to the script form.
		/// </summary>
		private static string TriviaText(SyntaxTriviaList trivia) {
			StringBuilder sb = new StringBuilder();
			foreach (SyntaxTrivia t in trivia) {
				if (t.IsDirective) {
					SyntaxKind kind = t.Kind();
					if (kind == SyntaxKind.LoadDirectiveTrivia || kind == SyntaxKind.ReferenceDirectiveTrivia || kind == SyntaxKind.BadDirectiveTrivia)
						continue;
					// The pragmas of the engine: read before the compile, unknown to the compiler
					string text = t.ToString().TrimStart();
					if (text.StartsWith("#pragma") && text.Contains("kombine"))
						continue;
				}
				sb.Append(t.ToFullString());
			}
			string s = sb.ToString();
			if (s.Length > 0 && !s.EndsWith("\n"))
				s += Environment.NewLine;
			return s;
		}

		/// <summary>
		/// A member made public (the loading script sees every top level member of a loaded file today,
		/// whatever accessibility it declares) and, for the members of the module class, static.
		/// </summary>
		private static MemberDeclarationSyntax WithAccessibility(MemberDeclarationSyntax member, bool makeStatic) {
			SyntaxTriviaList leading = member.GetLeadingTrivia();
			MemberDeclarationSyntax bare = member.WithLeadingTrivia(SyntaxTriviaList.Empty);
			List<SyntaxToken> kept = new List<SyntaxToken>();
			foreach (SyntaxToken t in bare.Modifiers) {
				SyntaxKind k = t.Kind();
				if (k == SyntaxKind.PublicKeyword || k == SyntaxKind.PrivateKeyword || k == SyntaxKind.InternalKeyword || k == SyntaxKind.ProtectedKeyword)
					continue;
				kept.Add(t);
			}
			bool isStatic = kept.Any(t => t.IsKind(SyntaxKind.StaticKeyword) || t.IsKind(SyntaxKind.ConstKeyword));
			List<SyntaxToken> list = new List<SyntaxToken>();
			list.Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space));
			if (makeStatic && !isStatic)
				list.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space));
			foreach (SyntaxToken t in kept)
				list.Add(t.WithTrailingTrivia(SyntaxFactory.Space));
			return bare.WithModifiers(SyntaxFactory.TokenList(list)).WithLeadingTrivia(leading);
		}

		// ------------------------------------------------------------------------------------------------
		// Loading
		// ------------------------------------------------------------------------------------------------

		/// <summary>
		/// The metadata reference of a module, for the compilations that load it.
		/// </summary>
		internal static MetadataReference? ReferenceOf(Module module) {
			if (module.Bytes == null)
				return null;
			return MetadataReference.CreateFromImage(module.Bytes, default, null, module.AssemblyName + ".dll");
		}

		/// <summary>
		/// Loads the assembly of a module into the process, once, makes it findable by the assemblies that
		/// reference it, and runs its initializer once. An abort or an exception of the initializer is a
		/// failure of the loading script.
		/// </summary>
		private static bool Load(Module module, List<string> errors) {
			if (module.Assembly == null) {
				if (module.Bytes == null) {
					errors.Add("The module " + module.Name + " has no binary");
					return false;
				}
				try {
					module.Assembly = module.Pdb != null ? Assembly.Load(module.Bytes, module.Pdb) : Assembly.Load(module.Bytes);
				} catch (Exception ex) {
					errors.Add("The module " + module.Name + " could not be loaded: " + ex.Message);
					return false;
				}
				lock (byAssemblyName) {
					byAssemblyName[module.AssemblyName] = module.Assembly;
					if (!resolveHooked) {
						resolveHooked = true;
						AssemblyLoadContext.Default.Resolving += (AssemblyLoadContext context, AssemblyName name) => {
							lock (byAssemblyName) {
								return name.Name != null && byAssemblyName.TryGetValue(name.Name, out Assembly? found) ? found : null;
							}
						};
					}
				}
			}
			if (!module.Initialized) {
				module.Initialized = true;
				try {
					Type? type = module.Assembly.GetType(module.ClassName);
					MethodInfo? init = type?.GetMethod("__KombineInitialize", BindingFlags.Static | BindingFlags.Public);
					init?.Invoke(null, null);
				} catch (Exception ex) {
					Exception inner = ex is TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
					if (inner is ScriptAbortException)
						errors.Add("The module " + module.Name + " aborted while initializing" + (inner.Message.Length > 0 && !inner.Message.StartsWith("Exception of type") ? ": " + inner.Message : ""));
					else
						errors.Add("The module " + module.Name + " failed while initializing: " + inner.Message);
					return false;
				}
			}
			return true;
		}
	}
}
