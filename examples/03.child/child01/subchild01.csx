/*---------------------------------------------------------------------------------------------------------

	Kombine Child Scripts Example: child of the first child

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the parent scripts
#load "../mkb.child.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Same shape as the class of the other scripts, but a different type: recovered with Cast
class TestObject{
	public string Name {get;set;} = string.Empty;
	public KValue Value {get;set;} = string.Empty;
	public KList List {get;set;} = new KList();
}

/// <summary>
/// Child of the first child: verifies the values exported by its parent and its grandparent
/// and the shared object, untouched by the changes the parent made on its own copy.
/// </summary>
/// <param name="args"></param>
/// <returns>The number of failed checks.</returns>
int test(string[] args){
	Banner("subchild01: data received from parent and grandparent");
	// Exported by the grandparent (the main script)
	Check("Import myvar", Quote(KValue.Import("myvar", "not received")), "\"my value\"");
	// Exported by the parent (child01)
	Check("Import another", Quote(KValue.Import("another", "not received")), "\"another value\"");
	// The shared object is the one of the main script: the parent changed only its own copy
	TestObject? obj = Cast<TestObject>(Share.Get("myobj"));
	Check("Share.Get + Cast", Show(obj != null), "true");
	if (obj != null)
		Check("object Name", obj.Name, "myname");
	EndBanner();
	return Summary();
}
