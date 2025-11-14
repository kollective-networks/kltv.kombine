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
	Msg.Print("Testing: base");
	Kombine("base/mkb.version.csx","test",args);
	Kombine("base/mkb.admin.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: simple");
	Kombine("simple/simple.csx","build",args);
	Kombine("simple/simple.csx","clean",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: types");
	Kombine("types/types.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: children");
	Kombine("child/child.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: files & folders & compression");
	Kombine("folders/folders.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: network");
	Kombine("network/network.csx","test",args);
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

