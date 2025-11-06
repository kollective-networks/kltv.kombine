/*---------------------------------------------------------------------------------------------------------

	Kombine Clang Extension Example

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

/// <summary>
/// It provides functionality to deal with the clang compiler in top of the Kombine Tool
/// </summary>
public class Clang {

	/// <summary>
	/// It packs all the clang options to be used in the tool and shared across instances
	/// </summary>
	public class ClangOptions {

		/// <summary>
		/// Clang Options constructor
		/// </summary>
		public ClangOptions(){
			if (Host.IsWindows()) {
				LibExtension = ".lib";
				SharedExtension = ".dll";
				BinaryExtension = ".exe";
				ObjectExtension = ".obj";
			} else if (Host.IsLinux()) {
				LibExtension = ".a";
				SharedExtension = ".so";
				BinaryExtension = ".out";
				ObjectExtension = ".o";
			} else if (Host.IsMacOS()) {
				LibExtension = ".a";
				SharedExtension = ".dylib";
				BinaryExtension = ".out";
				ObjectExtension = ".o";
			} else {
				Msg.PrintAndAbort("Error: Unknown OS");
			}
			/// Set the number of concurrent commands to be executed
			/// By default, the number of cores.
			ConcurrentBuild = Host.ProcessorCount();
		}

		/// <summary>
		/// Set the current options as default for the tool using the shared objects
		/// </summary>
		public void SetAsDefault(){
			Share.Set("ClangOptions",this);
		}

		/// <summary>
		/// C Compiler tool executable name
		/// </summary>
		public string CC { get; set; } = "clang";

		/// <summary>
		/// C++ Compiler tool executable name
		/// </summary>
		public string CXX { get; set; } = "clang++";

		/// <summary>
		/// Linker tool name
		/// </summary>
		public string LD { get; set; } = "clang++";

		/// <summary>
		/// Librarian tool name
		/// </summary>
		public string AR { get; set; } = "llvm-ar";

		/// <summary>
		/// Resource tool name
		/// </summary>
		public string RC { get; set; } = "llvm-rc";

		/// <summary>
		/// C file extension to use
		/// </summary>
		public string CExtension { get; set; } = ".c";

		/// <summary>
		/// C++ file extension to use
		/// </summary>
		public string CppExtension { get; set; } = ".cpp";

		/// <summary>
		/// Resource file extension to use
		/// </summary>
		public string ResExtension { get; set; } = ".rc";

		/// <summary>
		/// List of include directories to use
		/// </summary>
		public KList IncludeDirs { get; set; } = new KList();

		/// <summary>
		/// List of defines to use
		/// </summary>
		public KList Defines { get; set; } = new KList();

		/// <summary>
		/// List of C compiler switches to use
		/// </summary>
		public KList SwitchesCC { get; set; } = new KList();

		/// <summary>
		/// List of C++ compiler switches to use
		/// </summary>
		public KList SwitchesCXX { get; set; } = new KList();

		/// <summary>
		/// List of library directories to use
		/// </summary>
		public KList LibraryDirs { get; set; } = new KList();

		/// <summary>
		/// List of libraries to include when linking
		/// </summary>
		public KList Libraries { get; set; } = new KList();

		/// <summary>
		/// List of linker switches to use
		/// </summary>
		public KList SwitchesLD { get; set; } = new KList();

		/// <summary>
		/// Defines the number of concurrent commands to be executed
		/// </summary>
		public int ConcurrentBuild { get; set; } = 0;

		/// <summary>
		/// If we should print out verbose from the tool execution
		/// </summary>
		public bool Verbose { get; set; } = false;

		/// <summary>
		/// Contains the recomended static library extension for the current platform
		/// </summary>
		public string LibExtension { get; private set; } = ".a";

		/// <summary>
		/// Contains the recomended shared library extension for the current platform
		/// </summary>
		public string SharedExtension { get; private set; } = ".so";

		/// <summary>
		/// Contains the recomended binary extension for the current platform
		/// </summary>
		public string BinaryExtension { get; private set; } = ".out";

		/// <summary>
		/// Contains the recomended object extension for the current platform
		/// </summary>
		public string ObjectExtension { get; private set; } = ".o";

	}

	/// <summary>
	/// Clang tool instance constructor
	/// </summary>
	public Clang() {
		OpenSharedCompileOptions();
		OpenSharedCompileCommands();
		ProcessFile = null;
	}

	/// <summary>
	/// Clang Compiler Options Container
	/// </summary>
	public ClangOptions Options { get; private set; } = new ClangOptions();


	/// <summary>
	/// Delegate used to be called when a file is about to be processed.
	/// Useful to add parameters per file basis
	/// </summary>
	/// <param name="file">File about to be processed</param>
	/// <returns>Argument to be added if any</returns>
	public delegate string ProcessFileDelegate(string file);

	/// <summary>
	/// Event to be called when a file is about to be processed.
	/// Useful to add parameters per file basis
	/// </summary>
	/// <param name="file">File about to be processed</param>
	/// <returns>Argument to be added if any</returns>
	public ProcessFileDelegate? ProcessFile;


	/// <summary>
	/// Clean the provided object list and output folder
	/// </summary>
	/// <param name="obj">List of objects to fetch the files and folders to clean.</param>
	/// <param name="output">Value of the output artifact to clean</param>
	public void Clean(KList obj, KValue output) {
		// Delete the output folders for the objects
		KList folders = obj.AsFolders();
		Folders.Delete(folders);
		// Delete the output folder for the library / binary
		KValue folder = output.AsFolder();
		Folders.Delete(folder);
	}

	/// <summary>
	/// Compiles a list of sources into a list of objects.
	/// The parameters defined in the properties will be used
	/// </summary>
	/// <param name="src">List of sources</param>
	/// <param name="obj">Corresponding list of objects</param>
	/// <param name="rebuild">If we should rebuild everything.</param>
	public ToolResult Compile(KList src, KList obj, bool abortwhenfailed = true, bool rebuild = false) {
		// Sanity checks over the parameters passed.
		if (src.Count() != obj.Count()) {
			Msg.PrintAndAbort("Error: src and obj list must have the same number of elements");
			return ToolResult.DefaultFailed();
		}
		if (src.HasDuplicates() || obj.HasDuplicates()) {
			Msg.PrintAndAbort("Error: source or object list has duplicates.");
			return ToolResult.DefaultFailed();
		}
		if (Args.WasRebuilded) {
			Msg.Print("Script was rebuilt we will rebuild as well. Forcing rebuild.",Msg.LogLevels.Verbose);
			rebuild = true;
		}
		// Prepare includes & defines & switches
		KValue includes = string.Empty;
		KValue defines = string.Empty;
		KValue switchesCC = string.Empty;
		KValue switchesCXX = string.Empty;

		// We add the clang switches to the arguments automatically
		Msg.Print("Include paths:");
		Msg.BeginIndent();
		foreach (KValue v in Options.IncludeDirs){
			if (v.IsEmpty()){
				Msg.PrintAndAbort("Error: empty include directory found.");
			}
			includes += "-I" + v + " ";
			Msg.Print(v);
		}
		Msg.EndIndent();
		Msg.Print("Defines:");
		Msg.BeginIndent();
		foreach (KValue v in Options.Defines){
			if (v.IsEmpty()){
				Msg.PrintAndAbort("Error: empty define found.");
			}
			defines += "-D" + v + " ";
			Msg.Print(v);
		}
		Msg.EndIndent();
		// Sanitize
		includes = includes.ReduceWhitespace();
		defines = defines.ReduceWhitespace();
		if (Options.Verbose) {
			Options.SwitchesCC.Add("-v");
			Options.SwitchesCXX.Add("-v");
		}
		switchesCC = Options.SwitchesCC.Flatten().ReduceWhitespace();
		switchesCXX = Options.SwitchesCXX.Flatten().ReduceWhitespace();
		Msg.Print("Switches for C compiler: ");
		Msg.BeginIndent();
		foreach (KValue v in Options.SwitchesCC){
			Msg.Print(v);
		}
		Msg.EndIndent();
		Msg.Print("Switches for C++ compiler: ");
		Msg.BeginIndent();
		foreach (KValue v in Options.SwitchesCXX){
			Msg.Print(v);
		}
		Msg.EndIndent();
		// Create and configure the tool
		Tool tool = new Tool("clang");
		// Set the number of concurrent commands to be executed
		tool.ConcurrentCommands = (uint)Options.ConcurrentBuild;
		// Create the required folders required for the output files
		KList folders = obj.AsFolders();
		Folders.Create(folders);
		// Go through the list of sources to add the ones that need to be compiled with the appropiate command
		//
		for (int a = 0; a != src.Count(); a++) {
			KValue cmd = string.Empty;
			KValue args = string.Empty;
			// Fetch the absolute paths
			KValue srcf = RealPath(src[a]);
			KValue objf = RealPath(obj[a]);
			// Note: We can check here for argument lenght just in case we need to use response files due to
			// command line lenght limitations.
			// Create the arguments
			if (src[a].HasExtension(Options.CExtension)){
				cmd = Options.CC;
				KValue additionalArg = string.Empty;
				if (ProcessFile != null)
					additionalArg = ProcessFile.Invoke(src[a]);
				args = "-c -MMD "+includes+" "+defines+" "+switchesCC+" "+ srcf + " -o " + objf + " " + additionalArg;
			}else if (src[a].HasExtension(Options.CppExtension)){
				cmd = Options.CXX;
				KValue additionalArg =string.Empty;
				if (ProcessFile != null)
					additionalArg = ProcessFile.Invoke(src[a]);
				args = "-c -MMD "+includes+" "+defines+" "+switchesCXX+" " + srcf + " -o " + objf + " " + additionalArg;
			} else if (src[a].HasExtension(Options.ResExtension)) {
				// Note: We may use the same output extension for compiled resource files
				// since is provided by the user and a ".res" extension is not required.
				// All intermediates can be outputed as .obj / .o whatever
				cmd = Options.RC;
				args = srcf + " /FO " + objf;
			} else {
				Msg.PrintWarning("Error: file " + src[a] + " has an unknown extension. Skipped.");
				continue;
			}
			// Commpile commands are added / switched in any case if they're active.
			AddCompileCommands(cmd+" "+args,srcf,objf);
			if (ShouldProcess(srcf, objf) || rebuild) {
				tool.QueueCommand(cmd, args, src[a], CompileTaskDone);
			}
		}
		// Execute the commands and return the results
		ToolResult res = tool.ExecuteCommands();
		// Check if we need to abort when failed
		if (abortwhenfailed) {
			if (res.Status == ToolStatus.Failed) {
				// Save the compilation database if any prior to exit
				compdb?.Save();
				Msg.PrintAndAbort("Error: compilation operation failed");
			}
		}
		if (res.Status == ToolStatus.NoChanges) {
			Msg.Print("Compile: Nothing has done. Everything up to date.");
		}
		// Update compdb
		compdb?.Save();
		return res;
	}

	/// <summary>
	/// Invokes the librarian to include the given objects into a static library.
	/// </summary>
	/// <param name="src">List of objects to be included</param>
	/// <param name="output">Static library output</param>
	/// <returns>Tool result with the execution.</returns>
	public ToolResult Librarian(KList objs,KValue output, bool abortwhenfailed = true) {
		// Create and configure the tool
		Tool tool = new Tool("clang");
		// We allow only one instance of the librarian running
		tool.ConcurrentCommands = 0;
		// Create the required output folder for the library
		Folders.Create(output.AsFolder());
		if ((Host.IsLinux() || Host.IsMacOS()) ) {
			// On linux/macOS we need to prefix the library with "lib"
			output = output.WithNamePrefix("lib");
		}
		// Apply verbose for llvm-ar
		KValue args = " rcs ";
		if (Options.Verbose)
			args = " rcsv ";
		// Go through the list of objects to add the ones that need to be added
		string lobjs = string.Empty;
		for (int a = 0; a != objs.Count(); a++) {
			if (ShouldProcess(objs[a],output)){
				Msg.Print("Adding object: "+objs[a]);
				 lobjs += " "+objs[a];
			}
			 // Linux & OSX have a higher limit.
			 // On Windows the theorical limit per process is 32767 but looks like hard limit to 8191. We use just 4K
			 if (lobjs.Length > 4096) {
				// We need to split the command so if the command line for the librarian is too long
				// we queue the command and reset the string to create another command
				tool.QueueCommand(Options.AR, args + output+" "+lobjs, objs[a], LibrarianTaskDone);
				 lobjs = string.Empty;
			 }
		}
		if (string.IsNullOrEmpty(lobjs) == false) {
			tool.QueueCommand(Options.AR, args + output+" "+lobjs, objs[objs.Count()-1], LibrarianTaskDone);
		}
		// Execute the commands and return the results
		ToolResult res = tool.ExecuteCommands();
		// Check if we need to abort when failed
		if (abortwhenfailed) {
			if (res.Status == ToolStatus.Failed) {
				Msg.PrintAndAbort("Error: Librarian operation failed");
			}
		}
		if (res.Status == ToolStatus.NoChanges) {
			Msg.Print("Librarian: Nothing has done. Everything up to date.");
			return res;
		}
		Msg.PrintTask("Librarian: "+output+" created:");
		Msg.PrintTaskSuccess(" Ok");
		return res;
	}

	/// <summary>
	/// Links a list of objects into a binary.
	/// </summary>
	/// <param name="objs">Objects to be linked</param>
	/// <param name="output">Output file</param>
	/// <param name="abortwhenfailed">If we should abort when failed. Default true.</param>
	/// <returns></returns>
	public ToolResult Linker(KList objs,KValue output,bool SharedLibrary = false, bool abortwhenfailed = true) {
		// Create the tool to be executed
		Tool tool = new Tool("clang");
		// Prepare includes & libraries & switches
		KValue libdirs = string.Empty;
		KValue libs = string.Empty;
		KValue switchesLD = string.Empty;
		Msg.Print("Library paths:");
		Msg.BeginIndent();
		foreach (KValue v in Options.LibraryDirs){
			if (v.IsEmpty()){
				Msg.PrintAndAbort("Error: empty library directory found.");
			}
			Msg.Print(v);
			libdirs += "-L" + v + " ";
		}
		Msg.EndIndent();
		Msg.Print("Libraries:");
		Msg.BeginIndent();
		foreach (KValue v in Options.Libraries){
			if (v.IsEmpty()){
				Msg.PrintAndAbort("Error: empty library found.");
			}
			Msg.Print(v);
			libs += "-l" + v + " ";
		}
		Msg.EndIndent();
		libdirs = libdirs.ReduceWhitespace();
		libs = libs.ReduceWhitespace();
		if (Options.Verbose) {
			Options.SwitchesLD.Add("-v");
		}
		if (SharedLibrary) {
			Options.SwitchesLD.Add("-shared");
		}
		if ( (Host.IsLinux() || Host.IsMacOS()) && SharedLibrary) {
			// On linux/macOS we need to prefix the library with "lib"
			output = output.WithNamePrefix("lib");
		}
		switchesLD = Options.SwitchesLD.Flatten().ReduceWhitespace();
		Msg.Print("Switches for Linker: "+switchesLD);
		// Create the required output folder for the binary
		Folders.Create(output.AsFolder());
		bool ShouldLink = false;
		// Check for libraries changed
		foreach(KValue lib in Options.Libraries) {
			foreach(KValue libdir in Options.LibraryDirs) {
				if (ShouldProcessLibrary(libdir+lib,output)) {
					ShouldLink = true;
					break;
				}
			}
			if (ShouldLink)
				break;
		}
		// No libraries changed, look into the objects as well.
		if (!ShouldLink){
			ShouldLink = ShouldProcess(objs, output);
		}
		// Compose the command line
		if (ShouldLink) {

			KValue args = "-fuse-ld=lld " + switchesLD + " " + objs.Flatten() + " " + libdirs + " " + libs + " " + " -o " + output;
			ToolResult res;
			// Check if we need to use a response file
			// If argument's size is bigger than the Window's limit (32767), we
			// hardcode it, its lower than in Linux/OSX but we'll use the same there
			if (args.ToString().Length > 32766) {
				string fileargs = args.ToString();
				fileargs = fileargs.Replace("\\","/"); // Clean arguments, replace \ with /
				// We need to use a response file
				KValue responsefile = output.WithExtension(".rsp");
				Files.WriteTextFile(responsefile,fileargs);
				fileargs = "@" + responsefile;
				// Execute the command
				res = tool.CommandSync(Options.LD, fileargs, output);
				// Delete the response file
				if (File.Exists(responsefile)) {
					File.Delete(responsefile);
					Msg.Print($"Response file deleted: {responsefile}");
				}
			} else {
				res = tool.CommandSync(Options.LD, args, output);
			}
			// Continue with the task done
			TaskDone("Linking",output,ref res);
			if (abortwhenfailed) {
				if (res.Status == ToolStatus.Failed) {
					Msg.PrintAndAbort("Error: linking operation failed");
				}
			}
			return res;
		}
		Msg.Print("Linker: Nothing has done. Everything up to date.");
		return ToolResult.DefaultNoChanges();
	}

	/// <summary>
	/// Formats a list of files
	/// </summary>
	/// <param name="src">List of files to format</param>
	/// <param name="abortwhenfailed">Abort operation if any file failed</param>
	/// <returns></returns>
	public static bool Format(KList src, string extraArgs, bool abortwhenfailed = true) {
		bool returnCode = true;
		Msg.BeginIndent();
		if (src.Count() == 0) {
			Msg.Print("No files to format.");
			Msg.EndIndent();
			return true;
		}
		Msg.Print("Formatting files:");
		Msg.BeginIndent();
		foreach (KValue file in src) {
			Msg.PrintTask("Formatting file: " + file);
			/// Note: To avoid problems with different shells this may be an exec and not shell call
			///
			int code = Shell("clang-format", $" -i \"{file}\"");
			if (code != 0) {
				Msg.PrintTaskError(" Failed");
				if (abortwhenfailed) {
					Msg.PrintAndAbort("Error: formatting operation failed");
				}
				returnCode = false;
			}
			Msg.PrintTaskSuccess(" Ok");
		}
		Msg.EndIndent();
		Msg.EndIndent();
		return returnCode;
	}

	/// <summary>
	///  Called when a compilation task is done
	///  We have a chance here to print the results of the compilation and to modify the results
	///  For example, in this case, we detect the warnings and set the status accordingly
	/// </summary>
	/// <param name="res">The results for this execution</param>
	private void LibrarianTaskDone(ref ToolResult res) {
		TaskDone("Adding objects to library","",ref res);
	}

	/// <summary>
	///  Called when a compilation task is done
	///  We have a chance here to print the results of the compilation and to modify the results
	///  For example, in this case, we detect the warnings and set the status accordingly
	/// </summary>
	/// <param name="res">The results for this execution</param>
	private void CompileTaskDone(ref ToolResult res) {
		string? name = res.Id?.ToString();
		if (name == null)
			name = string.Empty;
		TaskDone("Compiling",name,ref res);
	}

	/// <summary>
	/// Generic output for a task
	/// This can be improved watching on the output to apply colors depending on warning or errors
	/// </summary>
	/// <param name="task">Task name to be printed.</param>
	/// <param name="element">Element processed if any.</param>
	/// <param name="res">Results.</param>
	private void TaskDone(string task, string element, ref ToolResult res) {
		if (res.Status == ToolStatus.Failed) {
			Msg.PrintTask(task + " " + element + ":");
			Msg.PrintTaskError(" Failed");
			Msg.BeginIndent();
			foreach (string s in res.Stderr) {
				string s2 = s.Replace("\n", "").Replace("\r", "");
				if (s2 != string.Empty)
					Msg.PrintError(s2);
			}
			Msg.PrintAndAbort("Error: " + task + " " + element + " failed.");
			Msg.EndIndent();
			return;
		}
		if (res.Stderr.Length != 0) {
			Msg.PrintTask(task + " " + element + ":");
			Msg.PrintTaskWarning(" Warnings");
			Msg.BeginIndent();
			foreach (string s in res.Stderr) {
				string s2 = s.Replace("\n", "").Replace("\r", "");
				if (s2 != string.Empty)
					Msg.PrintWarning(s2);
			}
			Msg.EndIndent();
			res.Status = ToolStatus.Warnings;
			return;
		}
		Msg.PrintTask(task + " " + element + ":");
		Msg.PrintTaskSuccess(" Ok");
	}

	/// <summary>
	/// Checks if any of the given object list has been modified after the output file.
	/// </summary>
	/// <param name="objs">List of objects to be checked</param>
	/// <param name="output">Output file to be compared against</param>
	/// <returns>True if any file has been modified. False otherwise.</returns>
	private bool ShouldProcess(KList objs,KValue output) {
		foreach(KValue obj in objs) {
			if (ShouldProcess(obj,output)) {
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Checks if the given object has been modified after the output file.
	/// </summary>
	/// <param name="src"></param>
	/// <param name="obj"></param>
	/// <returns></returns>
	private bool ShouldProcess(KValue src, KValue obj) {
		// Check if the source file exists
		if (Files.Exists(src) == false) {
			// This will exit this build process with error
			Msg.PrintAndAbort("Error: source file " + src + " sent to be built is not found.");
			return false;
		}
		// Check if the object file exists. If not exists, it should be built
		if (Files.Exists(obj) == false) {
			Msg.Print("File " + obj + " does not exists. It will be built.",Msg.LogLevels.Verbose);
			return true;
		}
		// Fetch dependency file.
		KValue depfile = obj.WithExtension(".d");
		if (Files.Exists(depfile) == false) {
			// Does not exists, so, check for the source file only
			if (Files.GetModifiedTime(src) > Files.GetModifiedTime(obj)) {
				// source file is newer than the object file. If its newer, we need to rebuild
				Msg.Print("No dependency file and source newer than object. It will be built.",Msg.LogLevels.Verbose);
				return true;
			}
			// No dependencie file, source file is older, do not process
			Msg.Print("No dependency file and source older than object. No build.",Msg.LogLevels.Verbose);
			return false;
		}
		// Read the dependency file
		// TODO: Beware of paths with spaces
		KValue content = Files.ReadTextFile(depfile);
		string a = content;
		string[] delimiters = { " \\ ", " \\\r", " \\\n"," "};
		string[] b = a.Split(delimiters,StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		// Looks like the dependency file is empty, so, check for the source file only
		if (b.Length == 0) {
			// No dependencies, check for the source file only
			if (Files.GetModifiedTime(src) > Files.GetModifiedTime(obj)) {
				// source file is newer than the object file. If its newer, we need to rebuild
				Msg.Print("Invalid dependency file and source newer than object. It will be built.",Msg.LogLevels.Verbose);
				return true;
			}
			// Invalid dependency file, source file is older, do not process
			Msg.Print("Invalid dependency file and source older than object. No build.",Msg.LogLevels.Verbose);
			return false;
		}
		// Check if any of the dependencies is newer than the object file
		Msg.Print("Building: "+src,Msg.LogLevels.Verbose);
		foreach(string c in b){
			if (Files.Exists(c) == false)
				continue;
			Msg.Print("Checking dependency: "+c,Msg.LogLevels.Verbose);
			if (Files.GetModifiedTime(c) > Files.GetModifiedTime(obj)) {
				// source file is newer than the object file. If its newer, we need to rebuild
				Msg.Print("Dependency newer than object. It will be built.",Msg.LogLevels.Verbose);
				return true;
			}
		}
		Msg.Print("Dependencies older than object. No build.",Msg.LogLevels.Verbose);
		return false;
	}

	/// <summary>
	/// Check if a library is newer than the output file
	/// </summary>
	/// <param name="src">Library candidate.</param>
	/// <param name="obj">Executable output.</param>
	/// <returns>True if build should be triggered. False otherwise</returns>
	private bool ShouldProcessLibrary(KValue src, KValue obj) {
		// Check if the library source file exists
		if (Files.Exists(src) == false) {
			// Library does not exist in this path, no triggering the build
			return false;
		}
		// Check if the object file exists. If not exists, it should be built
		if (Files.Exists(obj) == false) {
			Msg.Print("File " + obj + " does not exists. It will be built.",Msg.LogLevels.Verbose);
			return true;
		}
		if (Files.GetModifiedTime(src) > Files.GetModifiedTime(obj)) {
			Msg.Print("Input library newer than output binary. It will be built.",Msg.LogLevels.Verbose);
			return true;
		}
		// No dependencie file, source file is older, do not process
		Msg.Print("Input library older than output binary. No build.",Msg.LogLevels.Verbose);
		return false;
	}

	/// <summary>
	/// Opens the compile commands file to be used in this tool.
	/// If the file does not exists, it will be created.
	/// File can contain a relative path.
	/// The object will be shared with the rest of clang instances which may run in this script
	/// or child scripts. Being that way you can open in one location and use it in other.
	/// </summary>
	/// <param name="file">The filename to be produced as output.</param>
	/// <returns>True if everything is okey, false otherwise.</returns>
	public bool OpenCompileCommands(KValue file) {
		// Check if we've object in the shared data
		if (Share.Get("compile_commands") != null) {
			compdb = Share.Get("compile_commands") as JsonFile;
			return true;
		}else{
			// Create it if not exists
			compdb = new JsonFile(file);
			if (compdb.Doc == null) {
				// Is a new one, just create the array.
				compdb.Doc = new JsonArray();
				if (compdb.Save() == true){
					Share.Set("compile_commands",compdb);
					return true;
				}
				Msg.PrintWarning("Failed to create a new compile commands file: "+file,Msg.LogLevels.Verbose);
				return false;
			}
			Share.Set("compile_commands",compdb);
			return true;
		}
	}

	/// <summary>
	/// Open a previously shared compile commands.
	/// This is triggered by the constructor so if we've some previous shared compile commands we will use it.
	/// </summary>
	private void OpenSharedCompileCommands(){
		if (Share.Get("compile_commands") != null) {
			compdb = Share.Get("compile_commands") as JsonFile;
		}
	}

	/// <summary>
	/// Add a register into the compile commands file.
	/// </summary>
	/// <param name="cmd">command to be added.</param>
	/// <param name="file">file which this command belongs.</param>
	/// <param name="outputfile">outputfile for this command.</param>
	private void AddCompileCommands(string cmd,string file, string outputfile){
		if (compdb == null)
			return;
		if (compdb.Doc == null)
			compdb.Doc = new JsonArray();
		foreach(JsonNode? node in compdb.Doc.AsArray()){
			if (node == null)
				continue;
			if (node["file"]?.AsValue().ToString() == file){
				if (node["directory"]?.AsValue().ToString() != Folders.GetCurrentFolder())
					continue;
				// Update the arguments
				node["command"] = JsonValue.Create(cmd);
				return;
			}
		}
		// It doesn't exists, so, add it
		JsonObject entry = new JsonObject();
		entry.Add("directory",JsonValue.Create(Folders.GetCurrentFolder()));
		entry.Add("command",JsonValue.Create(cmd));
		entry.Add("file",JsonValue.Create(file));
		entry.Add("output",JsonValue.Create(outputfile));
		compdb.Doc.AsArray().Add(entry);
	}

	/// <summary>
	/// Holds the compile commands file.
	/// </summary>
	private JsonFile? compdb = null;

	/// <summary>
	/// Retrieve from shared objects the compile options to be used in this tool if any.
	/// </summary>
	private void OpenSharedCompileOptions(){
		object? obj = Share.Get("ClangOptions");
		if (obj != null) {
			ClangOptions? opt = Cast<ClangOptions>(obj);
			if (opt != null) {
				Options.CC = opt.CC;
				Options.CXX = opt.CXX;
				Options.LD = opt.LD;
				Options.AR = opt.AR;
				Options.RC = opt.RC;
				Options.ResExtension = opt.ResExtension;
				Options.CExtension = opt.CExtension;
				Options.CppExtension = opt.CppExtension;
				Options.IncludeDirs = new KList(opt.IncludeDirs);
				Options.Defines = new KList(opt.Defines);
				Options.SwitchesCC = new KList(opt.SwitchesCC);
				Options.SwitchesCXX = new KList(opt.SwitchesCXX);
				Options.LibraryDirs = new KList(opt.LibraryDirs);
				Options.Libraries = new KList(opt.Libraries);
				Options.SwitchesLD = new KList(opt.SwitchesLD);
				Options.ConcurrentBuild = opt.ConcurrentBuild;
				Options.Verbose = opt.Verbose;
				return;
			}
		}
	}
}
