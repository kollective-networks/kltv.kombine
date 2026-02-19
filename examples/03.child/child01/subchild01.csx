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

class TestObject{
	public string Name {get;set;} = string.Empty;
	public KValue Value {get;set;} = string.Empty;
	public KList List {get;set;} = new KList();
}


int test(string[] args){
	// ----------------------------------------------------------
	// Test import an object
	TestObject? obj = Cast<TestObject>(Share.Get("myobj"));
	if (obj != null) {
		Msg.Print("Object name: "+obj.Name);
		Msg.Print("Object value: "+obj.Value);
		obj.Name = "myname changed2";
	} else {
		Msg.PrintError("Failed to import object");
	}
	// ----------------------------------------------------------
	// Test importing a value
	KValue myvar = KValue.Import("myvar","i didn't receive the value");
	Msg.Print("Hello from sub script 01 with value "+myvar);
	// ----------------------------------------------------------
	// Test importing another value
	KValue another = KValue.Import("another","the another is not present");
	Msg.Print("We have another value as well: "+another);
	return 0;
}