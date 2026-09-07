/*---------------------------------------------------------------------------------------------------------

	Kombine Child Scripts Example: first child

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the parent script
#load "../mkb.child.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Same shape as the parent's class, but a different type: the shared object is recovered with Cast
class TestObject{
	public string Name {get;set;} = string.Empty;
	public KValue Value {get;set;} = string.Empty;
	public KList List {get;set;} = new KList();
}

/// <summary>
/// First child: verifies the value and the object received from the parent, exports a value for
/// its own child and runs it, registers a run wide value and checks the working folder.
/// </summary>
/// <param name="args"></param>
/// <returns>The number of failed checks.</returns>
int test(string[] args){
	Banner("child01: data received from the parent");
	// The value exported by the parent is imported from the environment
	Check("Import myvar", Quote(KValue.Import("myvar", "not received")), "\"my value\"");
	// The shared object is recovered as a copy made of this script's own type
	TestObject? obj = Cast<TestObject>(Share.Get("myobj"));
	Check("Share.Get + Cast", Show(obj != null), "true");
	if (obj != null){
		Check("object Name", obj.Name, "myname");
		Check("object Value", obj.Value, "myvalue");
		Check("object List item1", Show(obj.List.Contains("item1")), "true");
		// Changing the copy does not touch the object of the parent
		obj.Name = "myname changed";
	}
	EndBanner();

	Banner("child01: data for its own child");
	// Exported values are inherited by the children of this script, not by its parent or siblings
	KValue another = "another value";
	another.Export("another");
	int code = Kombine("subchild01.csx", "test", args, false);
	Check("subchild01 returned", code.ToString(), "0");
	EndBanner();

	Banner("child01: run wide registry and folders");
	// Registry values are visible to every script of the run, whatever the relationship
	Check("Register myreg/mykey", Show(Share.Register("myreg", "mykey", RealPath("includes/"))), "true");
	Check("Register again", Show(Share.Register("myreg", "mykey", "other value")), "false");
	// The working folder is switched to the folder of the script while it runs
	KValue cwd = CurrentWorkingFolder;
	Verify("CurrentWorkingFolder", Tail(cwd, 3), Unix(cwd).EndsWith("/03.child/child01"), "a path ending in /03.child/child01");
	Verify("GetParent", Tail(cwd.GetParent(), 3), Unix(cwd.GetParent()).EndsWith("/examples/03.child"), "a path ending in /examples/03.child");
	EndBanner();
	return Summary();
}
