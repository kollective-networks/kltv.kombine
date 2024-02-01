/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

int test(string[] args){

	KValue myvar = KValue.Import("myvar","i didn't receive the value");
	Msg.Print("Hello from sub script 01 with value "+myvar);
	KValue another = KValue.Import("another","the another is not present");
	Msg.Print("We have another value as well: "+another);
	return 0;
}