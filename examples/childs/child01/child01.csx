/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

int test(string[] args){

	KValue myvar = KValue.Import("myvar","i didn't receive the value");
	Msg.Print("Hello from script 01 with value "+myvar);

	// Set another value for the childs that belong to this one
	KValue anothervar = "another value";
	anothervar.Export("another");
	Msg.Print("Executing another script");
	Kombine("subchild01.csx","test");

	// We will set a registry value to be used for all the scripts executed next to this one
	// no matter the relationship (parent,child,brother...)
	Share.Register("myreg","mykey",RealPath("includes/"));


	return 0;
}