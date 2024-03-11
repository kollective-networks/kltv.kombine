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
	Kombine("simple/kombine.csx","build",args);
	Kombine("simple/kombine.csx","clean",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: types");
	Kombine("types/kombine.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: childrens");	
	Kombine("child/kombine.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: files & folders & compression");
	Kombine("folders/folders.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: network");
	Kombine("network/kombine.csx","test",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: clang");
	Kombine("clang/kombine.csx","build",args);
	Kombine("clang/kombine.csx","clean",args);
	Kombine("clang/kombine.csx","help",args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: sdl2");
	Kombine("sdl2/kombine.csx","build",args);
	Kombine("sdl2/kombine.csx","clean",args);
	return 0;
}
