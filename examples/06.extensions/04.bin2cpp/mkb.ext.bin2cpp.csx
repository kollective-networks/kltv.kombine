/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	This is an example of a custom extension that converts a binary file into a C++ header file with the content as a byte array. 
	This can be useful for embedding binary resources directly into C++ applications.

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/
#load "extensions/bin2cpp.csx"
#load "extensions/clang.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


// Define the output artifact folders
// We make use of the built in property "CurrentWorkingFolder" to define the output folders
// The current folder will be the folder where the script is executed
KValue OutputBin = CurrentWorkingFolder + "/out/bin/";
KValue OutputTmp = CurrentWorkingFolder + "/out/tmp/";
KValue OutputLib = CurrentWorkingFolder + "/out/lib/";


// Export them so they can be used by other scripts
OutputBin.Export("OutputBin");
OutputTmp.Export("OutputTmp");
OutputLib.Export("OutputLib");


//
// Test the Bin2Cpp extension
//
int build(string[] args) {
	Msg.Print("Starting Bin2cpp conversion...");
	//
	// Convert all .svg files in the res folder to .cpp files in the src folder, with the same name.
	// For example, res/image.svg will be converted to src/image.cpp
	//
	Bin2cpp converter = new Bin2cpp();
	KList binFiles = new KList();
	binFiles = Glob("res/**/*.svg");
	KList cppFiles = new KList();
	cppFiles = binFiles.WithExtension(".cpp");
	cppFiles = cppFiles.WithReplace("res/","gen/");
	// And do the conversion
	if (converter.Generate(binFiles, cppFiles) == false) {
		Msg.PrintError("Failed to generate cpp files.");
		return 1;
	}
	//
	// After the conversion on the converter instance
	// you can fetch the generated symbols for each file, for example:
	// Is a dictionary <string,string> with a friendly name and the generated symbol name.
	// 
	// Beware that the path is used to generate the symbol name uniqueness so if you work with
	// absolute paths and change the path the symbol name will change.
	//
	// Usage of relative paths is encouraged for this reason, and also to make the generated code more portable.
	// This example uses relative paths to feed the converter.
	//
	// Symbols are on the list even if the generation was unnecessary and skipped.
	//
	Msg.Print("Generated symbols:");
	Msg.BeginIndent();
	foreach (var symbol in converter.Symbols) {
		Msg.Print("Generated symbol: "+symbol);
	}
	Msg.EndIndent();

	//
	// Convert all the .svg files in the res
	// folder to a single cpp file in the src folder, 
	//
	KList allBinFiles = Glob("res/**/*.svg");
	KValue amalgamation = "gen/amalgamation.cpp";
	if (converter.Generate(allBinFiles, amalgamation) == false) {
		Msg.PrintError("Failed to generate amalgamation cpp file.");
		return 1;
	}
	Msg.Print("All cpp files generated successfully.");
	//
	// And to test the generated files we can just compile them with clang
	// Using the same example from the clang extension
	//
	// Open the compile commands file
	// and leave it prepared for the build process
	//
	Clang clang = new Clang();
	clang.OpenCompileCommands("out/tmp/compile_commands.json");
	// And do the build
	Kombine("mybin.csx", "build", args);
	return 0;
}

int clean(string[] args) {
	Msg.Print("Cleaning generated cpp files...");
	Kombine("mybin.csx", "clean", args);
	Folders.Delete("gen",true);
	return 0;
}