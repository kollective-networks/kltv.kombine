/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/
// Load the clang tool script
// The load directive has capabilities to resolve the path to the script
// It will look the path in the following order:
// 1. Current directory
// 2. Script directory
// 3. Backtrace directories 
// 4. Tool directory
// Also it is possible to specify a full path to the script 
// The full path could be on the filesystem or an URL
// So you can fetch your common scripts from your own repository.
// -----------------------------------------------------------------------
#load "scripts/clang.csx"


// In this example we just define some variables to be used in the script globally
//
KValue Name = "mylib";
// The folders we want to use for the output artifacts
// We import them but if not found we use our default values
KValue OutputBin = KValue.Import("OutputBin","out/bin/");
KValue OutputLib = KValue.Import("OutputLib","out/lib/");
KValue OutputTmp = KValue.Import("OutputTmp","out/tmp/");

// We will add to the output folders our current project name and the platform we're building on
// Binary is not used in this case being this example a static library, but just for the record.
OutputLib += Name + "/" + Host.GetOSKind() + "/";
OutputBin += Name + "/" + Host.GetOSKind() + "/";
OutputTmp += Name + "/" + Host.GetOSKind() + "/";

// The sources to be compiled.
KList	src = Glob("src/*.c");
		src += Glob("src/*.cpp");

// The flags to be used in the compilation
//
KList CFlags = new KList { "-std=c17", "-g", "-O0", "-gdwarf-4" };
KList CxxFlags = new KList { "-std=c++20", "-g", "-O0", "-gdwarf-4" };
KList LinkerFlags = new KList { "-g", "-gdwarf-4" };
// The list of defines to use
KList Defines = new KList { "DEBUG" };
// Include directories
KList Includes = new KList();
// Library directories
KList LibraryDirs = new KList();
// Libraries
KList Libraries = new KList();

/// <summary>
/// Build the project action.
/// </summary>
/// <returns>Exiting code</returns>
int build(string[] args){
	// Fancy printing if we want.
	Msg.Print("Building static library: "+Name);
	// Create an instance of the clang tool.
	Clang clang = new Clang();
	//
	// You can override here parameters for the tool
	// like the compiler executable, the extensions, etc
	// Check the Clang class for more information
	clang.SwitchesCC = CFlags;
	clang.SwitchesCXX = CxxFlags;
	clang.SwitchesLD = LinkerFlags;
	clang.Defines = Defines;
	clang.IncludeDirs = Includes;
	clang.LibraryDirs = LibraryDirs;
	clang.Libraries = Libraries;

	clang.OpenCompileCommands("out/tmp/compile_commands.json");	

	// Also we can use the given parameters into this function to define diferent configurations
	// For example, args[0] could be "debug" or "release" and just operate in consequence here
	// altering the output paths, the defines, switches,etc.
	// It may be achieved per platform as well. 
	if (Host.IsMacOS()) {
		// Do something
	}
	if (Host.IsLinux()) {
		// Do something
	}
	if (Host.IsWindows()) {
		// Do something
	}
	// You can download and extract files in this action or another one to prepare your build 
	// without rely on different commands by platform
	//
	// Http.DownloadFile("https://bit.ly/1GB-testfile", "out/test.bin");
	//
	// Generate the list of object files to be used as output
	KList objs = src.WithExtension(clang.ObjectExtension).WithPrefix(OutputTmp);
	// And compile the sources
	clang.Compile(src, objs);
	// Use the librarian to generate a static library
	clang.Librarian(objs, OutputLib + Name + clang.LibExtension);
	Msg.PrintTask("Building static library: " + Name +" ");
	Msg.PrintTaskSuccess(" done");
	return 0;
}

/// <summary>
/// Clan the project action
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int clean(string[] args){
	Msg.Print("Cleaning artifacts for static library: "+Name);
	Clang clang = new Clang();
	KList objs = src.WithExtension(clang.ObjectExtension).WithPrefix(OutputTmp);
	KValue output = OutputLib + Name + clang.LibExtension;
	clang.Clean(objs,output);
	return 0;
}

