/*---------------------------------------------------------------------------------------------------------

	Kombine MSYS2 package download and extraction

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("Testing MSYS2 package download and extraction");
	Msg.BeginIndent();

	Msg.Print("");
	Msg.Print("Downloading MSYS2 package");
	bool result = Http.DownloadFile("https://mirror.msys2.org/mingw/ucrt64/mingw-w64-ucrt-x86_64-openssl-3.6.0-1-any.pkg.tar.zst", "openssl.tar.zst");

	Msg.Print("Extracting MSYS2 package");
	Compress.Tar.Decompress("openssl.tar.zst", "openssl/",true);
	File.Delete("openssl.tar.zst");

	Msg.Print("Doing a testing build against the msys2 library");
	Kombine("msys2.build.csx","build",args);


	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return 0;
}
