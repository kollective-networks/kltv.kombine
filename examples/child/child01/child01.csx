/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

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
		obj.Name = "myname changed";
		obj.Value = "myvalue changed";
		obj.List.Add("item2");
	} else {
		Msg.PrintError("Failed to import object");
	}
	// ----------------------------------------------------------
	// Test importing a value
	KValue myvar = KValue.Import("myvar","i didn't receive the value");
	Msg.Print("Hello from script 01 with value "+myvar);
	// ----------------------------------------------------------
	// Set another value for the childs that belong to this one
	// That value will be available for the child scripts along with myvar
	KValue anothervar = "another value";
	anothervar.Export("another");
	// ----------------------------------------------------------
	Msg.Print("Executing another script");
	Kombine("subchild01.csx","test");
	// ----------------------------------------------------------
	// We will set a registry value to be used for all the scripts executed next to this one
	// no matter the relationship (parent,child,brother...)
	Share.Register("myreg","mykey",RealPath("includes/"));
	// ----------------------------------------------------------
	// Get the parent folder using a KValue method
	KValue parent = CurrentWorkingFolder;
	parent = parent.GetParent();

	return 0;
}