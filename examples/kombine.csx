/*---------------------------------------------------------------------------------------------------------

	Kombine Test version functions

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


/// <summary>
/// Execute all the scripts as test.
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int test(string[] args){
	Msg.Print("");
	Msg.Print("Testing scripts: ");
	Msg.Print("");
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: Base functions");
	Kombine("00.base/mkb.version.csx","test",args);
	Kombine("00.base/mkb.admin.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: Simple script (two actions)");
	Kombine("01.simple/mkb.simple.csx","build",args);
	Kombine("01.simple/mkb.simple.csx","clean",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Built in types");
	Kombine("02.types/mkb.types.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Child scripts");
	Kombine("03.child/mkb.child.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Files & folders & compression");
	Kombine("04.folders/mkb.folders.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Network API");
	Kombine("05.network/mkb.network.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");


	Msg.Print("Testing: Extensions - Bin2cpp");
	Kombine("06.extensions/04.bin2cpp/mkb.ext.bin2cpp.csx", "build", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");




	Msg.Print("Testing: clang");
	Kombine("clang/clang.build.csx","build",args);
	Kombine("clang/clang.build.csx","clean",args);
	Kombine("clang/clang.build.csx","help",args);
	Kombine("clang/clang.gendoc.csx","doc",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: sdl2");
	Kombine("sdl2/sdl2.csx","build",args);
	Kombine("sdl2/sdl2.csx","clean",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: msys22");
	Kombine("msys2/msys2.packages.csx", "test", args);
	Kombine("msys2/msys2.build.csx", "build", args);
	return 0;
}

