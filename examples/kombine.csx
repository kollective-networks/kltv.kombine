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
	Msg.Print("Testing scripts: ");
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: simple");
	if (Kombine("simple/kombine.csx","build",args) != 0) 
		return -1;
	if (Kombine("simple/kombine.csx","clean",args) != 0) 
		return -1;
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: types");
	if (Kombine("types/kombine.csx","test",args) != 0) 
		return -1;
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: childs");	
	if (Kombine("childs/kombine.csx","test",args) != 0) 
		return -1;
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: files & folders & compression");
	if (Kombine("folders/kombine.csx","test",args) != 0) 
		return -1;
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: network");
	if (Kombine("network/kombine.csx","test",args) != 0) 
		return -1;
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: clang");
	if (Kombine("clang/kombine.csx","build",args) != 0) 
		return -1;
	if (Kombine("clang/kombine.csx","clean",args) != 0) 
		return -1;
	if (Kombine("clang/kombine.csx","help",args) != 0) 
		return -1;
	Msg.Print("----------------------------------------------------------");
	Msg.Print("Testing: sdl2");
	if (Kombine("sdl2/kombine.csx","build",args) != 0) 
		return -1;
	if (Kombine("sdl2/kombine.csx","clean",args) != 0) 
		return -1;
	return 0;
}
