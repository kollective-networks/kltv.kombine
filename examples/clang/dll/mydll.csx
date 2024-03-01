/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

#load "extensions/clang.csx"

// Set this project name
//
KValue Name = "mydll";
// The folders we want to use for the output artifacts
// We import them but if not found we use our default values
KValue OutputBin = KValue.Import("OutputBin","out/bin/");
KValue OutputLib = KValue.Import("OutputLib","out/lib/");
KValue OutputTmp = KValue.Import("OutputTmp","out/tmp/");

// The sources to be compiled.
KList	src = Glob("src/**/*.c");
		src += Glob("src/**/*.cpp");
		
// Compile flags and includes / defines
KList CFlags = new KList { "-std=c17", "-g", "-O0", "-gdwarf-4" };
KList CxxFlags = new KList { "-std=c++20", "-g", "-O0", "-gdwarf-4" };
// Defines
KList Defines = new KList { "DEBUG" };
// Include directories
KList Includes = new KList();
// Linker flags and libraries
KList LinkerFlags = new KList { "-g", "-gdwarf-4" };
// Library directoriesld-
KList LibraryDirs = new KList();
// Libraries
KList Libraries = new KList();


// We will add to the output folders our current project name and the platform we're building on
// Library is not used in this case being this example a binary, but just for the record.
OutputLib += Name + "/" + Host.GetOSKind() + "/";
OutputBin += Name + "/" + Host.GetOSKind() + "/";
OutputTmp += Name + "/" + Host.GetOSKind() + "/";


/// <summary>
/// Build the project action.
/// </summary>
/// <returns>Exiting code</returns>
int build(string[] args){
	// Fancy printing if we want.
	Msg.Print("Building shared library: "+Name);
	// Create an instance of the clang tool.
	Clang clang = new Clang();
	clang.OpenCompileCommands("out/tmp/compile_commands.json");	
	// Compile
	// -------------------------------------------------------
	clang.Options.IncludeDirs = Includes;
	clang.Options.Defines = Defines;
	clang.Options.SwitchesCC = CFlags;
	clang.Options.SwitchesCXX = CxxFlags;
	// Generate the list of object files to be used as output
	KList objs = src.WithExtension(clang.Options.ObjectExtension).WithPrefix(OutputTmp);
	// And compile the sources
	clang.Compile(src, objs);
	// Linker (we pass to the linker we shall generate a shared library)
	// -----------------------------------------------------------------
	clang.Options.LibraryDirs = LibraryDirs;
	clang.Options.Libraries = Libraries;
	clang.Options.SwitchesLD = LinkerFlags;
	clang.Linker(objs, OutputBin + Name + clang.Options.SharedExtension,true);
	Msg.PrintTask("Building shared library: " + Name +" ");
	Msg.PrintTaskSuccess(" done");
	return 0;
}

/// <summary>
/// Clan the project action
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int clean(string[] args){
	Msg.Print("Cleaning artifacts for shared library: "+Name);
	Clang clang = new Clang();
	// This is just an example but you can just use Folders.Delete and destroy all the artifacts as well.
	KList objs = src.WithExtension(clang.Options.ObjectExtension).WithPrefix(OutputTmp);
	KValue output = OutputBin + Name + clang.Options.SharedExtension;
	clang.Clean(objs,output);
	return 0;
}

