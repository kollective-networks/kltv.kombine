/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

#r "mkb.dll"
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
	Msg.Print("Testing: simple");
	Kombine("simple/simple.csx","build",args);
	Kombine("simple/simple.csx","clean",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: types");
	Kombine("types/types.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: childrens");	
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
	Kombine("clang/clang.csx","build",args);
	Kombine("clang/clang.csx","clean",args);
	Kombine("clang/clang.csx","help",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: sdl2");
	Kombine("sdl2/sdl2.csx","build",args);
	Kombine("sdl2/sdl2.csx","clean",args);
	return 0;
}
