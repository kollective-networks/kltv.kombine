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

//
// KList examples
// 
// --------------------------------------------------------------------------------------------------------------------

// Define a list "src"
KList	src = "my item1";
		src += "my item2";
// Print the content.
// Note that KList has the "Flatten" method which automatically converts the list on a single value space separated.
Msg.Print("The list content: "+src.Flatten());

// Another way to initialize a list is this one
KList src2 = new() { "item1","item2"};
Msg.Print("This new list content: "+src2.Flatten());
// We can remove an item as well
src2 -= "item2";
Msg.Print("This new list content after removing item2: "+src2.Flatten());
// You can quickly check for some value
if (src2.Contains("item1")){
	Msg.Print("Src2 stills contain item1");
}
// It is posible to assing one list to another
KList src3 = src;
// Also to append both
src3 += src2;
Msg.Print("Now src3 has the content of src and src2 "+src.Flatten());
// You can use the methods also
src3.Add(src2);
// And search for duplicates
if (src3.HasDuplicates()){
	Msg.Print("The list has duplicates");
}
// You can access one single item using indexer
Msg.Print("The first item is: "+src3[0]);
// You can pass through all the items with a foreach
foreach(KValue v in src3){
	Msg.Print("Item in list is: "+v);
}
// Fetch the count of items
Msg.Print("src3 has total items: "+src3.Count());
// Some special functions.
// AsFolders
// Returns the unique folders in the list if can be interpreted
KList src4 = new() { "/out/file1.obj","/out/folder2/file1.obj","/out/folder1/file1.obj","/out/folder1/file1.obj"};
KList folders = src4.AsFolders();
Msg.Print("Unique folders in the list: "+folders.Flatten());
// WithPrefix
// Adds the prefix to all the items
KList src5 = src4.WithPrefix("/mybuild");
Msg.Print("The new list is: "+src5.Flatten());
// WithExtension
// Changes the extension for the items in the list if can be interpreted as files
KList src6 = src5.WithExtension(".o");
Msg.Print("The new list is: "+src6.Flatten());



//
// KValue examples
// 
// --------------------------------------------------------------------------------------------------------------------
KValue a = "my value";
KValue b = "        my second value";
a += b;
Msg.Print("Result is: "+a);
// We can eliminate redundant whitespaces
KValue c = a.ReduceWhitespace();
Msg.Print("Now is: "+c);
// Comparison operators
if ( (c != "My var") || (c == a) ) {
	Msg.Print("Never should be equal");
}
// Extract a folder name if can be interpreted as a path
KValue d = "/opt/out/mybin/example.out";
KValue e = d.AsFolder();
Msg.Print("Returned as folder: "+e);
if (d.HasExtension("out")){
	Msg.Print("It has .out extension");
}
// With prefix on the name adds a prefix into the filename if the value can be consideer that way
KValue f = d.WithNamePrefix("lib");
Msg.Print("Now the filename is: "+f);

KValue paramstest = " param1 param2 param3 \"param4 with spaces\" param5";
KList parlist = paramstest.ToArgs();



KValue g = "my \"value\"";
g = ArgEscape(g);
Msg.Print("Escaped value: "+g);


// We just define one function to be used as an action.
int test(string[] args) {

	return 0;
}
