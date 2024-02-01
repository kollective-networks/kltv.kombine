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

	Msg.Print("Exporting a value");
	KValue myvar = "my value";
	myvar.Export("myvar");
	Msg.Print("Launching child scripts");
	// Execute the first script
	Kombine("child01/child01.csx","test");

	// Script 1 defined one registry value, lets fetch it 
	KValue regvalue = Share.Registry("myreg","mykey");
	Msg.Print("Shared Registry value: "+regvalue);


	// Execute the second script
	Kombine("child02/child02.csx","test");
	// Execute a tool
	int code = Exec("git",new string[] {"--version"},true );
	Msg.Print("Tool returned: " + code);
	// Execute a tool through shell 
	code = Shell("git","--version");
	Msg.Print("Tool returned: " + code);

	return 0;
}