/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


int test(string[] args){
	// Test a regular download
	bool result;

	Msg.Print("Testing GetDocument");
	KValue message = Http.GetDocument("https://raw.githubusercontent.com/neuecc/MarkdownGenerator/master/VSDocParser.cs");
	Msg.Print("Message is: "+message.ToString());

	Msg.Print("Testing download");
	result = Http.DownloadFile("https://bit.ly/1GB-testfile", "out/test.bin");

	Msg.Print("Testing multiple downloads");
	KList uris = new KList();
	uris.Add("https://bit.ly/1GB-testfile");
	uris.Add("https://bit.ly/1GB-testfile");
	uris.Add("https://bit.ly/1GB-testfile");
	KList paths = new KList();
	paths.Add("out/test1.bin");
	paths.Add("out/test2.bin");
	paths.Add("out/test3.bin");
	result = Http.DownloadFiles(uris, paths);


	Folders.Delete("out/");
	return 0;
}
