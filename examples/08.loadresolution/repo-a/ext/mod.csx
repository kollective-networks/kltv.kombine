/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

	Subfolder script using a root-relative #load, with the embedded repo-b sitting in this
	same folder. It must bind repo-a's helper through the backward step; the retired forward
	search used to bind repo-b's copy from here.

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
	Msg.Print("OK: " + args[0] + " subfolder script bound " + HelperOwner);
	return 0;
}
