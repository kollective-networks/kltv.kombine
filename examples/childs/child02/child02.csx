/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

int test(string[] args){

	KValue myvar = KValue.Import("myvar","i didn't receive the value");
	Msg.Print("Hello from script 02 with value "+myvar);
	KValue another = KValue.Import("another","but i will not receive, is for childs only.");
	Msg.Print("But import/export is limited to childs only: "+another);

	// We want to get here as well the registry value set by a previous executed script
	KValue regvalue = Share.Registry("myreg","mykey");
	Msg.Print("Shared Registry value fetched: "+regvalue);
	return 0;
}