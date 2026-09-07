/*---------------------------------------------------------------------------------------------------------

	Kombine Child Scripts Example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the child scripts
#load "mkb.child.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Every script is compiled on its own, so this class is a different type in each one of them.
// That is why the children recover the shared object with Cast<TestObject>(), which copies
// the matching properties into an instance of their own type.
class TestObject{
	public string Name {get;set;} = string.Empty;
	public KValue Value {get;set;} = string.Empty;
	public KList List {get;set;} = new KList();
}

/// <summary>
/// Exercises the child script API: values exported to the environment, objects shared between
/// scripts, the run wide registry, nested child scripts and tool execution. Every child verifies
/// what it receives and returns its number of failed checks, which is verified here as well.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check passed, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing child scripts");
	Msg.BeginIndent();

	//
	// Values and objects to be shared with the children
	//
	Banner("[1/5] Export a value and share an object");
	// Exported values go to the script environment, inherited by the children (and by executed tools)
	KValue myvar = "my value";
	myvar.Export("myvar");
	Check("Export myvar", Quote(KValue.Import("myvar", "not exported")), "\"my value\"");
	// Shared objects are stored by name, only once
	TestObject obj = new TestObject();
	obj.Name = "myname";
	obj.Value = "myvalue";
	obj.List.Add("item1");
	Check("Share.Set myobj", Show(Share.Set("myobj", obj)), "true");
	Check("Share.Set again", Show(Share.Set("myobj", obj)), "false");
	Check("Share.Get myobj", Show(object.ReferenceEquals(Share.Get("myobj"), obj)), "true");
	EndBanner();

	//
	// First child: imports the value and the object, exports a value for its own child, runs it
	// and registers a run wide value. Its output is indented below.
	//
	Banner("[2/5] Child script child01 (runs its own child subchild01)");
	int code = Kombine("child01/child01.csx", "test", args, false);
	Check("child01 returned", code.ToString(), "0");
	EndBanner();

	//
	// What the parent sees once the child finished: registry values are run wide, exports are not
	//
	Banner("[3/5] Parent state after child01");
	KValue regvalue = Share.Registry("myreg", "mykey");
	Verify("Registry myreg/mykey", Tail(regvalue, 3), Unix(regvalue).TrimEnd('/').EndsWith("/child01/includes"), "a path ending in /child01/includes");
	Check("Import another", Quote(KValue.Import("another", "not visible")), "\"not visible\"");
	Check("shared object Name", obj.Name, "myname");
	EndBanner();

	//
	// Second child: a sibling of the first one. It receives the parent exports and the registry,
	// but not the values exported by its sibling.
	//
	Banner("[4/5] Child script child02 (sibling of child01)");
	code = Kombine("child02/child02.csx", "test", args, false);
	Check("child02 returned", code.ToString(), "0");
	EndBanner();

	//
	// Tools: direct execution and through the system shell. The tool itself is used in silent mode,
	// so the checks do not depend on any other program being installed.
	//
	Banner("[5/5] Tool execution");
	string mkb = CurrentToolFolder + (Host.IsWindows() ? "/mkb.exe" : "/mkb");
	Check("Exec kversion", Exec(mkb, new string[] { "-ko:silent", "kversion" }).ToString(), "0");
	Check("Exec missing script", Exec(mkb, new string[] { "-ko:silent", "-kfile:missing.csx", "any" }).ToString(), "1");
	Check("Shell kversion", Shell(mkb, "-ko:silent kversion").ToString(), "0");
	EndBanner();

	int failedChecks = Summary();
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failedChecks != 0){
		Msg.PrintError($"Child scripts tests failed: {failedChecks}");
		return 1;
	}
	return 0;
}
