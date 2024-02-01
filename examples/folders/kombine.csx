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

// Builtin properties with useful folders
//-------------------------------------------------
Msg.Print("Current Working Folder: "+CurrentWorkingFolder);
Msg.Print("Current Script Folder: "+CurrentScriptFolder);
Msg.Print("Current Tool Folder: "+CurrentToolFolder);


int test(string[] args){
	Msg.Print("Folder testing");

	// Folder & file search
	//-------------------------------------------------

	// search forward including a relative path
	KValue filefound = Folders.SearchForwardPath("src/file1.txt");
	Msg.Print("File found: "+filefound);
	// search forward without including a relative path
	filefound = Folders.SearchForwardPath("file1.txt");
	Msg.Print("File found: "+filefound);
	Msg.Print("Execute child to test folders from child");
	Kombine("child/child.csx","test");
	Msg.Print("Execute it again but letting the system to search for the script");
	Kombine("child.csx","test");

	// File Operations
	// -------------------------------------------------
	KValue content = "This is my rifle, is my life";
	Msg.Print("Writing file");
	Files.WriteTextFile("file2.txt",content);
	Files.Rename("file2.txt","filenew.txt");
	if (Files.Exists("filenew.txt")) {
		Msg.Print("File exists");
	}
	Files.Copy("filenew.txt","file2.txt");
	Files.Move("file2.txt","file3.txt");
	Files.Delete("filenew.txt");
	long modtime = Files.GetModifiedTime("file3.txt");
	long size = Files.GetFileSize("file3.txt");
	Msg.Print("File size: "+size);
	Msg.Print("File mod time: "+modtime);
	Files.Delete("file3.txt");

	// Zip Operations
	//-------------------------------------------------
	Compress.Zip.CompressFolder("child", "test.zip");
	Folders.Create("testfolder");
	Compress.Zip.Decompress("test.zip", "testfolder/");
	Files.Delete("test.zip");
	Folders.Delete("testfolder",true);

	Compress.Zip.CompressFolders(new string[]{"child","folder1"}, "test2.zip");
	Folders.Create("testfolder");
	Compress.Zip.Decompress("test2.zip", "testfolder/");
	Files.Delete("test2.zip");
	Folders.Delete("testfolder",true);

	Files.Copy("folder1/src/file1.txt","test.txt");
	Compress.Zip.CompressFile("folder1/src/file1.txt","test3.zip");
	Compress.Zip.CompressFile("test.txt","test4.zip");
	Files.Delete("test.txt");
	Files.Delete("test3.zip");
	Files.Delete("test4.zip");

	Compress.Tar.CompressFolder("child", "test.tar.gz");
	Compress.Tar.CompressFolders(new string[]{"child","folder1"}, "test2.tar.gz");
	Folders.Create("testfolder");
	Compress.Tar.Decompress("test.tar.gz", "testfolder/");
	Compress.Tar.Decompress("test2.tar.gz", "testfolder/");
	Files.Delete("test.tar.gz");
	Files.Delete("test2.tar.gz");	
	Folders.Delete("testfolder",true);

	Files.Copy("folder1/src/file1.txt","test.txt");
	Compress.Tar.CompressFile("folder1/src/file1.txt","test3.tar.gz");
	Compress.Tar.CompressFile("test.txt","test4.tar.gz");
	Files.Delete("test.txt");
	Files.Delete("test3.tar.gz");
	Files.Delete("test4.tar.gz");
	


	// Folder operations
	//-------------------------------------------------

	return 0;
}