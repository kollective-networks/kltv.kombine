/*---------------------------------------------------------------------------------------------------------

	Kombine Built-in Types Example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int passed = 0;
int failed = 0;

/// <summary>
/// Exercises the built in types (KValue and KList): every helper is executed with a small sample,
/// its result is verified against the expected value and reported on an aligned line.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check passed, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing built in types");
	Msg.BeginIndent();

	TestKValueBasics();
	TestKValuePaths();
	TestKValueSplitting();
	TestKValueEnvironment();
	TestKListBasics();
	TestKListTransforms();

	int total = passed + failed;
	Msg.PrintTask($"Summary : {passed} of {total} checks passed ");
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");

	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"Built in types tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// KValue: creation, concatenation, whitespace, comparison and hashing.
/// </summary>
void TestKValueBasics(){
	Banner("[1/6] KValue basics");
	// Implicit conversion from string and concatenation with +
	KValue a = "my value";
	KValue b = "        my second value";
	KValue c = a + b;
	Check("from string", Quote(a), "\"my value\"");
	Check("concatenation +", Quote(c), "\"my value        my second value\"");
	// Redundant whitespace can be collapsed
	KValue d = c.ReduceWhitespace();
	Check("ReduceWhitespace", Quote(d), "\"my value my second value\"");
	// The - operator removes every occurrence of the right value
	KValue e = d - "my ";
	Check("subtraction -", Quote(e), "\"value second value\"");
	// Comparison is by content, not by reference
	Check("== compares content", Show(d == "my value my second value"), "true");
	Check("!= compares content", Show(d != a), "true");
	Check("Equals(string)", Show(d.Equals("my value my second value")), "true");
	// Empty check
	KValue empty = "";
	Check("IsEmpty on empty", Show(empty.IsEmpty()), "true");
	Check("IsEmpty on value", Show(a.IsEmpty()), "false");
	// The 64 bit hash only depends on the content: same content, same hash, in every run
	KValue a2 = "my value";
	Verify("GetHashCode64 stable", a.GetHashCode64().ToString(), a.GetHashCode64() == a2.GetHashCode64(), "same hash for the same content");
	Verify("GetHashCode64 differs", b.GetHashCode64().ToString(), a.GetHashCode64() != b.GetHashCode64(), "different hash for different content");
	EndBanner();
}

/// <summary>
/// KValue interpreted as a path: folder, extension, name prefix and parent.
/// </summary>
void TestKValuePaths(){
	Banner("[2/6] KValue as path");
	KValue file = "/opt/out/mybin/example.out";
	Check("value", file, "/opt/out/mybin/example.out");
	Check("AsFolder", file.AsFolder(), "/opt/out/mybin");
	Check("HasExtension out", Show(file.HasExtension("out")), "true");
	Check("HasExtension bin", Show(file.HasExtension("bin")), "false");
	Check("WithExtension .o", file.WithExtension(".o"), "/opt/out/mybin/example.o");
	// The prefix goes into the filename part. The result keeps the separators of the host,
	// so the comparison is made with the separators normalized.
	KValue prefixed = file.WithNamePrefix("lib");
	Verify("WithNamePrefix lib", prefixed, Unix(prefixed) == "/opt/out/mybin/libexample.out", "/opt/out/mybin/libexample.out (any separator)");
	// The parent is resolved as an absolute path (on Windows it gets the current drive)
	KValue parent = file.AsFolder().GetParent();
	Verify("GetParent", parent, Unix(parent).EndsWith("/opt/out"), "a path ending in /opt/out");
	EndBanner();
}

/// <summary>
/// KValue split into lists: by spaces, by separators and as command line arguments.
/// </summary>
void TestKValueSplitting(){
	Banner("[3/6] KValue splitting");
	// Whitespace separated, empty entries are dropped
	KValue spaced = " one  two   three ";
	Check("ToArray by spaces", Show(spaced.ToArray()), "{ one, two, three }");
	// Custom separators
	KValue csv = "a, b;c";
	Check("ToArray separator ,", Show(csv.ToArray(",")), "{ a, b;c }");
	Check("ToArray separators", Show(csv.ToArray(new[] { ",", ";" })), "{ a, b, c }");
	// Command line arguments: quoted fragments stay as a single argument (quotes are kept)
	KValue cmdline = " p1 p2 \"p3 with spaces\" p4";
	KList arguments = cmdline.ToArgs();
	Check("ToArgs", Show(arguments), "{ p1, p2, \"p3 with spaces\", p4 }");
	Check("ToArgs count", arguments.Count().ToString(), "4");
	// Escaping a value to be embedded inside a quoted argument
	Check("ArgEscape", ArgEscape("my \"value\""), "my \\\"value\\\"");
	EndBanner();
}

/// <summary>
/// KValue exported to and imported from the script environment.
/// </summary>
void TestKValueEnvironment(){
	Banner("[4/6] KValue environment");
	// Exported values are visible to executed tools and child scripts, and can be imported back
	KValue exported = "exported value";
	exported.Export("KOMBINE_TYPES_TEST");
	Check("Export / Import", KValue.Import("KOMBINE_TYPES_TEST"), "exported value");
	// A missing variable falls back to the default value (without a default, the script aborts)
	Check("Import default", KValue.Import("KOMBINE_TYPES_MISSING", "fallback"), "fallback");
	EndBanner();
}

/// <summary>
/// KList: creation, flatten, add / remove, duplicates, iteration and comparison.
/// </summary>
void TestKListBasics(){
	Banner("[5/6] KList basics");
	// A list can be created from a single value and grown with +=
	KList src = "my item1";
	src += "my item2";
	Check("from string and +=", Show(src), "{ my item1, my item2 }");
	Check("Count", src.Count().ToString(), "2");
	Check("indexer [0]", src[0], "my item1");
	// Flatten joins the items with a space (or with the given separator), including a trailing one
	Check("Flatten", Quote(src.Flatten()), "\"my item1 my item2 \"");
	Check("Flatten separator", Quote(src.Flatten(", ")), "\"my item1, my item2, \"");
	// Collection initializer, removal and search
	KList src2 = new() { "item1", "item2" };
	src2 -= "item2";
	Check("initializer and -=", Show(src2), "{ item1 }");
	Check("Contains item1", Show(src2.Contains("item1")), "true");
	Check("Contains item2", Show(src2.Contains("item2")), "false");
	// Operators return a new list, the operands are never modified
	KList src3 = src + src2;
	Check("list + list", Show(src3), "{ my item1, my item2, item1 }");
	Check("operand untouched", Show(src), "{ my item1, my item2 }");
	// Add appends in place, duplicates can be detected and removed
	src3.Add(src2);
	Check("Add(list)", Show(src3), "{ my item1, my item2, item1, item1 }");
	Check("HasDuplicates", Show(src3.HasDuplicates()), "true");
	Check("RemoveDuplicates", Show(src3.RemoveDuplicates()), "{ my item1, my item2, item1 }");
	// Iteration with foreach
	int count = 0;
	foreach (KValue v in src3)
		count++;
	Check("foreach items", count.ToString(), "4");
	// Removal of a whole list (one occurrence per item) and content comparison
	KList src4 = src3 - src2;
	Check("list - list", Show(src4), "{ my item1, my item2, item1 }");
	Check("== compares content", Show(src4 == src3.RemoveDuplicates()), "true");
	Check("!= compares content", Show(src4 != src), "true");
	Check("Compare", Show(src.Compare(new KList() { "my item1", "my item2" })), "true");
	EndBanner();
}

/// <summary>
/// KList transforms for file lists and implicit conversions.
/// </summary>
void TestKListTransforms(){
	Banner("[6/6] KList transforms");
	// Values are treated as files: unique folders, prefixes, extensions and replacements
	KList files = new() { "out/a.obj", "out/x/b.obj", "out/y/c.obj", "out/y/c.obj" };
	Check("AsFolders (unique)", Show(files.AsFolders()), "{ out, out/x, out/y }");
	KList objs = new() { "out/a.obj", "out/x/b.obj" };
	Check("WithPrefix bld/", Show(objs.WithPrefix("bld/")), "{ bld/out/a.obj, bld/out/x/b.obj }");
	Check("WithExtension .o", Show(objs.WithExtension(".o")), "{ out/a.o, out/x/b.o }");
	Check("WithReplace out/ obj/", Show(objs.WithReplace("out/", "obj/")), "{ obj/a.obj, obj/x/b.obj }");
	// Transforms return new lists, so they can be chained
	Check("chained transforms", Show(objs.WithExtension(".o").WithPrefix("bld/")), "{ bld/out/a.o, bld/out/x/b.o }");
	// Implicit conversions from and to string arrays and strings
	KList fromArray = new string[] { "one", "two" };
	Check("from string[]", Show(fromArray), "{ one, two }");
	string[] toArray = fromArray;
	Check("to string[]", toArray.Length.ToString(), "2");
	string flat = fromArray;
	Check("to string (flatten)", Quote(flat), "\"one two \"");
	EndBanner();
}

/// <summary>
/// Prints the banner of a group of checks and indents its lines.
/// </summary>
/// <param name="title">Group title.</param>
void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
}

/// <summary>
/// Closes a group of checks.
/// </summary>
void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

/// <summary>
/// Compares the actual value against the expected one and reports the outcome.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value.</param>
/// <param name="expected">Expected value.</param>
void Check(string what, string actual, string expected){
	Verify(what, actual, actual == expected, expected);
}

/// <summary>
/// Prints an aligned line with the actual value and a colored OK / FAILED tag.
/// On failure the expected value is printed below in red.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value to be shown.</param>
/// <param name="ok">Outcome of the check.</param>
/// <param name="expected">Expected value, shown when the check failed.</param>
void Verify(string what, string actual, bool ok, string expected){
	Msg.PrintTask($"{what,-22} : {actual,-42} ");
	if (ok){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-22} : {expected}");
	}
}

/// <summary>
/// Renders a list as { item1, item2, ... }
/// </summary>
/// <param name="list">List to render.</param>
/// <returns>The rendered list.</returns>
string Show(KList list){
	string items = "";
	foreach (KValue v in list)
		items += (items == "" ? "" : ", ") + v;
	return "{ " + items + " }";
}

/// <summary>
/// Renders a boolean as true / false
/// </summary>
/// <param name="value">Value to render.</param>
/// <returns>The rendered value.</returns>
string Show(bool value){
	return value ? "true" : "false";
}

/// <summary>
/// Renders a value between quotes, so leading, trailing and repeated spaces are visible.
/// </summary>
/// <param name="value">Value to render.</param>
/// <returns>The quoted value.</returns>
string Quote(KValue value){
	return "\"" + value + "\"";
}

/// <summary>
/// Normalizes the separators of a path to unix style.
/// </summary>
/// <param name="value">Path to normalize.</param>
/// <returns>The normalized path.</returns>
string Unix(KValue value){
	return value.ToString().Replace('\\', '/');
}
