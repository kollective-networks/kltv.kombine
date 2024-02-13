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


class TestObject{
	public string Name {get;set;} = string.Empty;
	public KValue Value {get;set;} = string.Empty;
	public KList List {get;set;} = new KList();
}


int test(string[] args){
	// ----------------------------------------------------------
	Msg.Print("Exporting a value");
	KValue myvar = "my value";
	myvar.Export("myvar");
	// ----------------------------------------------------------
	Msg.Print("Exporting an object");
	TestObject obj = new TestObject();
	obj.Name = "myname";
	obj.Value = "myvalue";
	obj.List.Add("item1");
	Share.Set("myobj",obj);
	// ----------------------------------------------------------
	Msg.Print("Launching child scripts");
	// Execute the first script
	Kombine("child01/child01.csx","test");
	// Script 1 defined one registry value, lets fetch it 
	// Registry is script execution wide, so matter if was defined in a child script
	KValue regvalue = Share.Registry("myreg","mykey");
	Msg.Print("Shared Registry value: "+regvalue);
	// ----------------------------------------------------------
	// Execute the second script
	Kombine("child02/child02.csx","test");
	// ----------------------------------------------------------
	// Execute a tool
	int code = Exec("git",new string[] {"--version"},true );
	Msg.Print("Tool returned: " + code);
	// ----------------------------------------------------------
	// Execute a tool through shell 
	code = Shell("git","--version");
	Msg.Print("Tool returned: " + code);
	return 0;
}