/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Built in properties with useful folders
//-------------------------------------------------
Msg.Print("");
Msg.Print("Built in properties with useful folders");
Msg.Print("-------------------------------------------------------------------");
Msg.Print("Current Working Folder: "+CurrentWorkingFolder);
Msg.Print("Current Script Folder: "+CurrentScriptFolder);
Msg.Print("Current Tool Folder: "+CurrentToolFolder);
Msg.Print("Parent Script Folder: "+ParentScriptFolder);


int test(string[] args){
	// Folder & file search
	//-------------------------------------------------
	Msg.Print("");
	Msg.Print("Folder & file search");
	Msg.Print("-------------------------------------------------------------------");
	// search forward including a relative path
	KValue filefound = Folders.SearchForwardPath("src/file1.txt");
	Msg.Print("Search forward. File found: "+filefound);
	// search forward without including a relative path
	filefound = Folders.SearchForwardPath("file1.txt");
	Msg.Print("Search forward. File found: "+filefound);
	Msg.Print("Execute child to test folders from child");
	Kombine("child/child.csx","test");
	Msg.Print("Execute it again locating the script first with the forward search");
	// Automatic forward search on dispatch is deprecated (-kforward); search explicitly instead.
	KValue childscript = Folders.SearchForwardPath("child.csx");
	Kombine(childscript,"test");

	// File Operations
	// -------------------------------------------------
	Msg.Print("");
	Msg.Print("File Operations");
	Msg.Print("-------------------------------------------------------------------");	
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
	Msg.Print("");
	Msg.Print("File Zip Operations");
	Msg.Print("-------------------------------------------------------------------");	
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

	// Tar.gz Operations
	//-------------------------------------------------
	Msg.Print("");
	Msg.Print("File Tar.gz Operations");
	Msg.Print("-------------------------------------------------------------------");	

	Compress.Tar.CompressFolder("child", "test.tar.gz");
	Compress.Tar.CompressFolders(new string[]{"child","folder1"}, "test2.tar.gz");
	Compress.Tar.CompressFolder("child","test3.tar.gz",true,false);
	Compress.Tar.CompressFolders(new string[]{"child","folder1"}, "test4.tar.gz",true,false);
	Folders.Create("testfolder");
	Compress.Tar.Decompress("test.tar.gz", "testfolder/");
	Compress.Tar.Decompress("test2.tar.gz", "testfolder/");
	Compress.Tar.Decompress("test3.tar.gz", "testfolder/");
	Compress.Tar.Decompress("test4.tar.gz", "testfolder/");
	Files.Delete("test.tar.gz");
	Files.Delete("test2.tar.gz");	
	Files.Delete("test3.tar.gz");
	Files.Delete("test4.tar.gz");
	Folders.Delete("testfolder",true);

	Files.Copy("folder1/src/file1.txt","test.txt");
	Compress.Tar.CompressFile("folder1/src/file1.txt","test3.tar.gz");
	Compress.Tar.CompressFile("test.txt","test4.tar.gz");
	Files.Delete("test.txt");
	Files.Delete("test3.tar.gz");
	Files.Delete("test4.tar.gz");

	// Tar.bz2 Operations
	//-------------------------------------------------
	Msg.Print("");
	Msg.Print("File Tar.bz2 Operations");
	Msg.Print("-------------------------------------------------------------------");	

	Compress.Tar.CompressFolder("child", "test.tar.bz2",true,true,TarCompressionType.Bzip2);
	Compress.Tar.CompressFolders(new string[]{"child","folder1"}, "test2.tar.bz2",true,true,TarCompressionType.Bzip2);
	Compress.Tar.CompressFolder("child","test3.tar.bz2",true,false,TarCompressionType.Bzip2);
	Compress.Tar.CompressFolders(new string[]{"child","folder1"}, "test4.tar.bz2",true,false,TarCompressionType.Bzip2);
	Folders.Create("testfolder");
	Compress.Tar.Decompress("test.tar.bz2", "testfolder/",true);
	Compress.Tar.Decompress("test2.tar.bz2", "testfolder/",true);
	Compress.Tar.Decompress("test3.tar.bz2", "testfolder/",true);
	Compress.Tar.Decompress("test4.tar.bz2", "testfolder/",true);
	Files.Delete("test.tar.bz2");
	Files.Delete("test2.tar.bz2");	
	Files.Delete("test3.tar.bz2");
	Files.Delete("test4.tar.bz2");
	Folders.Delete("testfolder",true);

	Files.Copy("folder1/src/file1.txt","test.txt");
	Compress.Tar.CompressFile("folder1/src/file1.txt","test3.tar.bz2",true,TarCompressionType.Bzip2);
	Compress.Tar.CompressFile("test.txt","test4.tar.bz2",true,TarCompressionType.Bzip2);
	Files.Delete("test.txt");
	Files.Delete("test3.tar.bz2");
	Files.Delete("test4.tar.bz2");

	// Tar.xz Operations
	//-------------------------------------------------
	Msg.Print("");
	Msg.Print("File Tar.xz Operations (Decompression only)");
	Msg.Print("-------------------------------------------------------------------");
	Folders.Create("testfolder");
	Compress.Tar.Decompress("test.files/test.tar.xz", "testfolder/",true);
	Folders.Delete("testfolder",true);	



	// Tar round-trip + safe-extract (zip-slip) rejection
	//-------------------------------------------------
	Msg.Print("");
	Msg.Print("Tar round-trip + safe-extract");
	Msg.Print("-------------------------------------------------------------------");

	// Minimal ustar tar builder, so the traversal test needs no committed binary fixture.
	static byte[] MakeTar((string name, byte[] data, bool isDir)[] entries){
		static void PutStr(byte[] h, int off, int len, string s){
			byte[] b = System.Text.Encoding.ASCII.GetBytes(s);
			for (int i = 0; i < b.Length && i < len; i++) h[off + i] = b[i];
		}
		static void PutOctal(byte[] h, int off, int len, long val){
			PutStr(h, off, len - 1, System.Convert.ToString(val, 8).PadLeft(len - 1, '0'));
			h[off + len - 1] = 0;
		}
		using var ms = new System.IO.MemoryStream();
		foreach (var e in entries){
			byte[] h = new byte[512];
			long size = e.isDir ? 0 : e.data.Length;
			PutStr(h, 0, 100, e.name);
			PutOctal(h, 100, 8, 0x1ED);
			PutOctal(h, 124, 12, size);
			PutOctal(h, 136, 12, 0);
			h[156] = (byte)(e.isDir ? '5' : '0');
			PutStr(h, 257, 6, "ustar");
			h[263] = (byte)'0'; h[264] = (byte)'0';
			for (int i = 148; i < 156; i++) h[i] = (byte)' ';
			int sum = 0; foreach (byte b in h) sum += b;
			PutStr(h, 148, 6, System.Convert.ToString(sum, 8).PadLeft(6, '0'));
			h[154] = 0; h[155] = (byte)' ';
			ms.Write(h, 0, 512);
			if (!e.isDir && size > 0){
				ms.Write(e.data, 0, e.data.Length);
				int pad = (int)((512 - (size % 512)) % 512);
				if (pad > 0) ms.Write(new byte[pad], 0, pad);
			}
		}
		ms.Write(new byte[1024], 0, 1024);
		return ms.ToArray();
	}

	// (1) Real round-trip: compress a folder, extract it, verify content byte-for-byte.
	Compress.Tar.CompressFolder("folder1", "rt.tar.gz", true, false);
	Folders.Create("rt_out");
	Compress.Tar.Decompress("rt.tar.gz", "rt_out/");
	if (!Files.Compare("folder1/src/file1.txt", "rt_out/src/file1.txt", Files.CompareOptions.CompareContents))
		Msg.PrintAndAbort("Safe-extract test FAILED: round-trip content mismatch");
	Msg.Print("Round-trip content verified OK");
	Files.Delete("rt.tar.gz");
	Folders.Delete("rt_out", true);

	// (2) Safe-extract: traversal entries must never write outside the destination folder.
	byte[] payload = System.Text.Encoding.ASCII.GetBytes("owned");
	byte[] evil = MakeTar(new (string name, byte[] data, bool isDir)[]{
		("benign.txt", payload, false),                          // must extract inside the sandbox
		("../zipslip_file", payload, false),                     // file traversal, must be refused
		("../zipslip_dir/", System.Array.Empty<byte>(), true)    // directory traversal, must be refused
	});
	System.IO.File.WriteAllBytes(CurrentWorkingFolder + "/evil.tar", evil);
	Folders.Create("sandbox");
	Compress.Tar.Decompress("evil.tar", "sandbox/");
	bool benignOk = Files.Exists("sandbox/benign.txt");
	bool escapedFile = Files.Exists("zipslip_file");
	bool escapedDir = Folders.Exists("zipslip_dir");
	// Clean up (including any escaped artifacts, should the guard ever regress) before asserting.
	Files.Delete("evil.tar");
	Folders.Delete("sandbox", true);
	if (escapedFile) Files.Delete("zipslip_file");
	if (escapedDir) Folders.Delete("zipslip_dir", true);
	if (!benignOk)
		Msg.PrintAndAbort("Safe-extract test FAILED: benign entry did not extract (malformed archive)");
	if (escapedFile || escapedDir)
		Msg.PrintAndAbort("Safe-extract test FAILED: an entry escaped the destination folder");
	Msg.Print("Safe-extract rejected path-traversal entries OK");

	// Folder operations
	//-------------------------------------------------
	Folders.Create("testfolder");
	Files.Copy("folder1/src/file1.txt", "testfolder/test.txt");
	Folders.Move("testfolder","testfolder2");
	Folders.Delete("testfolder2",true);


	return 0;
}