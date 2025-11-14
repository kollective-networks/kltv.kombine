/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

//
// Use the clang and git extensions
//
#load "extensions/clang.csx"
#load "extensions/git.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

KValue Name = "sdl2";
KValue OutputBin = KValue.Import("OutputBin","out/bin/");
KValue OutputLib = KValue.Import("OutputLib","out/lib/");
KValue OutputTmp = KValue.Import("OutputTmp","out/tmp/");

// We will add to the output folders our current project name and the platform we're building on
// Binary is not used in this case being this example a static library, but just for the record.
OutputLib += Name + "/" + Host.GetOSKind() + "/";
OutputBin += Name + "/" + Host.GetOSKind() + "/";
OutputTmp += Name + "/" + Host.GetOSKind() + "/";

// The SDL sources to be compiled.
// They're fetched in a function
KList src = new KList();

// The flags to be used in the compilation
//
KList CFlags = new KList { "-std=c17", "-g", "-O0","-msse3", "-Wno-empty-body","-gdwarf-4" };
KList CxxFlags = new KList { "-std=c++20", "-g", "-O0","-msse3","-Wno-empty-body", "-gdwarf-4" };
KList LinkerFlags = new KList { "-g", "-gdwarf-4" };
// The list of defines to use
KList Defines = new KList { "DEBUG" };
// Include directories
KList Includes = new KList();
Includes += "sdl.github/include";
Includes += "sdl.github/src";
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
	Msg.Print("Building sdl2 library: "+Name);
	// Download SDL2 library from github
	if (Folders.Exists("sdl.github/")) {
		Msg.Print("Updating SDL2 sources");
		Git.Pull(CurrentScriptFolder+"/sdl.github/");
	} else {
		Msg.Print("Cloning SDL2 sources");
		Git.Clone("https://github.com/libsdl-org/SDL.git","sdl.github/","release-2.30.x");
	}
	// Create an instance of the clang tool.
	Clang clang = new Clang();
	// Create the list of sources to be compiled
	CreateSourceList();
	//
	// You can override here parameters for the tool
	// like the compiler executable, the extensions, etc
	// Check the Clang class for more information
	clang.Options.SwitchesCC = CFlags;
	clang.Options.SwitchesCXX = CxxFlags;
	clang.Options.SwitchesLD = LinkerFlags;
	clang.Options.Defines = Defines;
	clang.Options.IncludeDirs = Includes;
	clang.Options.LibraryDirs = LibraryDirs;
	clang.Options.Libraries = Libraries;
	clang.OpenCompileCommands("out/tmp/compile_commands.json");	
	// Generate the list of object files to be used as output
	KList objs = src.WithExtension(clang.Options.ObjectExtension).WithPrefix(OutputTmp);
	// And compile the sources
	clang.Compile(src, objs);
	// Use the librarian to generate a static library
	clang.Librarian(objs, OutputLib + Name + clang.Options.LibExtension);
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
	Folders.Delete(OutputTmp,true);
	Folders.Delete(OutputLib,true);
	return 0;
}

/// <summary>
/// 
/// </summary>
void CreateSourceList(){
	src += Glob("sdl.github/src/atomic/*.c");
	src += Glob("sdl.github/src/audio/directsound/*.c");
	src += Glob("sdl.github/src/audio/disk/*.c");
	src += Glob("sdl.github/src/audio/winmm/*.c");
	src += Glob("sdl.github/src/audio/wasapi/*.c");
	src += Glob("sdl.github/src/audio/*.c");
	src += Glob("sdl.github/src/core/windows/*.c");
	src += Glob("sdl.github/src/cpuinfo/*.c");
	src += Glob("sdl.github/src/dynapi/*.c");
	src += Glob("sdl.github/src/events/*.c");
	src += Glob("sdl.github/src/file/*.c");
	src += Glob("sdl.github/src/filesystem/windows/*.c");
	src += Glob("sdl.github/src/haptic/*.c");
	src += Glob("sdl.github/src/haptic/windows/*.c");
	src += Glob("sdl.github/src/hidapi/*.c");
	src += Glob("sdl.github/src/joystick/*.c");
	src += Glob("sdl.github/src/joystick/dummy/*.c");
	src += Glob("sdl.github/src/joystick/hidapi/*.c");
	src += Glob("sdl.github/src/joystick/windows/*.c");
	src += Glob("sdl.github/src/joystick/virtual/*.c");
	src += Glob("sdl.github/src/libm/*.c");
	src += Glob("sdl.github/src/loadso/windows/*.c");
	src += Glob("sdl.github/src/locale/windows/*.c");
	src += Glob("sdl.github/src/locale/*.c");
	src += Glob("sdl.github/src/misc/*.c");
	src += Glob("sdl.github/src/misc/windows/*.c");
	src += Glob("sdl.github/src/power/*.c");
	src += Glob("sdl.github/src/power/windows/*.c");
	src += Glob("sdl.github/src/render/direct3d/*.c");
	src += Glob("sdl.github/src/render/direct3d11/*.c");
	src += Glob("sdl.github/src/render/direct3d12/*.c");
	src += Glob("sdl.github/src/render/opengl/*.c");
	src += Glob("sdl.github/src/render/opengles2/*.c");
	src += Glob("sdl.github/src/render/software/*.c");
	src += Glob("sdl.github/src/render/*.c");
	src += Glob("sdl.github/src/sensor/*.c");
	src += Glob("sdl.github/src/render/dummy/*.c");
	src += Glob("sdl.github/src/render/windows/*.c");
	src += Glob("sdl.github/src/stdlib/*.c");
	src += Glob("sdl.github/src/thread/*.c");
	src += Glob("sdl.github/src/thread/generic/*.c");
	src += Glob("sdl.github/src/thread/windows/*.c");
	src += Glob("sdl.github/src/timer/*.c");
	src += Glob("sdl.github/src/timer/windows/*.c");
	src += Glob("sdl.github/src/video/dummy/*.c");
	src += Glob("sdl.github/src/video/windows/*.c");
	src += Glob("sdl.github/src/video/yuv2rgb/*.c");
	src += Glob("sdl.github/src/video/*.c");
	src += Glob("sdl.github/src/*.c");
}
