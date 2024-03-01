/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int test(string[] args){
	Msg.Print("Folder testing from child. Looking for a file in parent folder");
	KValue filefound = Folders.SearchBackPath("folder1/parent.csx");
	Msg.Print("File found: "+filefound);
	// Execute the script using the back searching functionality
	Kombine("folder1/parent.csx","test");

	return -1;
}