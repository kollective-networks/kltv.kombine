/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	This is an example of a custom extension that converts a binary file into a C++ header file with the content as a byte array. 
	This can be useful for embedding binary resources directly into C++ applications.

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/
#load "extensions/bin2obj.csx"

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
	Msg.Print("Starting Bin2obj conversion...");


	//
	// Convert all .svg files in the res folder to .cpp files in the src folder, with the same name.
	// For example, res/image.svg will be converted to src/image.cpp
	//
	Bin2obj converter = new Bin2obj();
	KList binFiles = new KList();
	binFiles = Glob("res/**/*.svg");
	KList objFiles = new KList();
	objFiles = binFiles.WithExtension(".obj");
	objFiles = objFiles.WithReplace("res/","obj/");

	if (converter.Generate(binFiles, objFiles) == false) {
		Msg.PrintError("Failed to generate obj files.");
		return 1;
	}

	//
	// Convert all the .svg files in the res
	// folder to a single cpp file in the src folder, 
	//
	KList allBinFiles = Glob("res/**/*.svg");
	KValue amalgamation = "obj/amalgamation.obj";
	if (converter.Generate(allBinFiles, amalgamation) == false) {
		Msg.PrintError("Failed to generate amalgamation obj file.");
		return 1;
	}

	Msg.Print("All obj files generated successfully. Clearing");
	// Clean the generated files after the test
	//Folders.Delete("obj",true);
	return 0;
}
