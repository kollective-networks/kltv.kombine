/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

KValue mymessage = "hello world!";

int build(string[] args){
	Msg.Print("I'm building: "+mymessage);
	return 0;
}
int clean(string[] args){
	Msg.Print("I'm cleaning: "+mymessage);
	return 0;
}