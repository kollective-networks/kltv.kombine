/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/
#load "scripts/build/helper.csx"

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;


int check(string[] args){
	if (args.Length < 1) {
		Msg.PrintError("Expected the owner to assert as the action parameter.");
		return -1;
	}
	if (HelperOwner != args[0]) {
		Msg.PrintError("Wrong helper bound: expected '" + args[0] + "' got '" + HelperOwner + "'");
		return -1;
	}
	Msg.Print("OK: " + args[0] + " root script bound " + HelperOwner);
	return 0;
}
