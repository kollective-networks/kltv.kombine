/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int test(string[] args){

	KValue myvar = KValue.Import("myvar","i didn't receive the value");
	Msg.Print("Hello from script 02 with value "+myvar);
	KValue another = KValue.Import("another","but i will not receive, is for childs only.");
	Msg.Print("But import/export is limited to childs only: "+another);

	// We want to get here as well the registry value set by a previous executed script
	KValue regvalue = Share.Registry("myreg","mykey");
	Msg.Print("Shared Registry value fetched: "+regvalue);
	return 0;
}