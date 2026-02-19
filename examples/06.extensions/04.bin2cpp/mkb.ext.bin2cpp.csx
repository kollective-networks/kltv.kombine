/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	This is an example of a custom extension that converts a binary file into a C++ header file with the content as a byte array. 
	This can be useful for embedding binary resources directly into C++ applications.

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/
#load "extensions/bin2cpp.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

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
	cppFiles = cppFiles.WithReplace("res/","src/");

	if (converter.Generate(binFiles, cppFiles) == false) {
		Msg.PrintError("Failed to generate cpp files.");
		return 1;
	}

	//
	// Convert all the .svg files in the res
	// folder to a single cpp file in the src folder, 
	//
	KList allBinFiles = Glob("res/**/*.svg");
	KValue amalgamation = "src/amalgamation.cpp";
	if (converter.Generate(allBinFiles, amalgamation) == false) {
		Msg.PrintError("Failed to generate amalgamation cpp file.");
		return 1;
	}
	Msg.Print("All cpp files generated successfully. Clearing");
	return 0;
}

int clean(string[] args) {
	Msg.Print("Cleaning generated cpp files...");
	Folders.Delete("src",true);
	return 0;
}