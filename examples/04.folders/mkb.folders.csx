/*---------------------------------------------------------------------------------------------------------

	Kombine Files, Folders and Compression Example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the child scripts
#load "mkb.folders.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Text;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Fixture file committed with the example, used as source for copies and archives
string Fixture = "folder1/src/file1.txt";
// Sandbox where every artifact of the test is created, removed at the end
string Sandbox = "";

/// <summary>
/// Exercises the well known folders, the file and folder searches, the file and folder operations
/// and the zip / tar compression, including the error cases: every operation is verified through its
/// return value and its effect on disk, and the failures are expected to be reported, not thrown.
/// The child scripts verify what they see from their own folders and return their failed checks.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check passed, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing files, folders and compression");
	Msg.BeginIndent();

	Sandbox = CurrentWorkingFolder + "/.tmp.folders";
	if (Folders.Exists(Sandbox))
		Folders.Delete(Sandbox, true);
	Msg.Print("Sandbox : " + Tail(Sandbox, 3) + " (created and removed by the test)");
	Msg.RawPrint(Environment.NewLine);

	TestWellKnownFolders();
	TestSearch();
	TestChildScripts(args);
	TestFiles();
	TestMissingFiles();
	TestFolders();
	TestZip();
	TestTar();
	TestSafeExtract();

	Folders.Delete(Sandbox, true);

	int failedChecks = Summary();
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failedChecks != 0){
		Msg.PrintError($"Files, folders and compression tests failed: {failedChecks}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Built in properties with the well known folders of a script run.
/// </summary>
void TestWellKnownFolders(){
	Banner("[1/9] Well known folders");
	Verify("CurrentWorkingFolder", Tail(CurrentWorkingFolder, 2), Unix(CurrentWorkingFolder).EndsWith("/examples/04.folders"), "a path ending in /examples/04.folders");
	Verify("CurrentScriptFolder", Tail(CurrentScriptFolder, 2), Unix(CurrentScriptFolder) == Unix(CurrentWorkingFolder), "the working folder (scripts run in their own folder)");
	string tool = CurrentToolFolder + (Host.IsWindows() ? "/mkb.exe" : "/mkb");
	Verify("CurrentToolFolder", Tail(CurrentToolFolder, 2), Files.Exists(tool), "the folder holding the mkb binary");
	// ParentScriptFolder is verified from the child scripts, this script may have no parent
	EndBanner();
}

/// <summary>
/// Forward search: the script folder and its subfolders, with or without a relative path.
/// </summary>
void TestSearch(){
	Banner("[2/9] Forward file search");
	string found = Folders.SearchForwardPath("src/file1.txt");
	Verify("SearchForwardPath", Tail(found, 3), Unix(found).EndsWith("/folder1/src/file1.txt"), "a path ending in /folder1/src/file1.txt");
	found = Folders.SearchForwardPath("file1.txt");
	Verify("SearchForwardPath name", Tail(found, 3), Unix(found).EndsWith("/folder1/src/file1.txt"), "a path ending in /folder1/src/file1.txt");
	Check("SearchForwardPath missing", Quote(Folders.SearchForwardPath("does.not.exist.txt")), "\"\"");
	EndBanner();
}

/// <summary>
/// Child scripts: the backward search and the well known folders are verified from a child folder.
/// </summary>
/// <param name="args">Action arguments, forwarded to the children.</param>
void TestChildScripts(string[] args){
	Banner("[3/9] Child scripts and backward search");
	int code = Kombine("child/child.csx", "test", args, false);
	Check("child.csx returned", code.ToString(), "0");
	// The automatic forward search on dispatch is deprecated (-kforward): locate the script explicitly
	string childscript = Folders.SearchForwardPath("child.csx");
	Verify("SearchForwardPath child", Tail(childscript, 2), Unix(childscript).EndsWith("/child/child.csx"), "a path ending in /child/child.csx");
	code = Kombine(childscript, "test", args, false);
	Check("child.csx returned (2)", code.ToString(), "0");
	EndBanner();
}

/// <summary>
/// File operations on the sandbox.
/// </summary>
void TestFiles(){
	Banner("[4/9] File operations");
	string folder = Sandbox + "/files";
	Folders.Create(folder);
	string file = folder + "/file.txt";
	KValue content = "This is my rifle, is my life";
	Check("WriteTextFile", Show(Files.WriteTextFile(file, content)), "true");
	Check("Exists", Show(Files.Exists(file)), "true");
	Check("ReadTextFile", Quote(Files.ReadTextFile(file)), Quote(content));
	Check("GetFileSize", Files.GetFileSize(file).ToString(), content.ToString().Length.ToString());
	long modtime = Files.GetModifiedTime(file);
	Verify("GetModifiedTime", modtime.ToString(), modtime > 0, "a unix timestamp greater than zero");
	// Rename, copy and move report the outcome and leave no source behind
	string renamed = folder + "/renamed.txt";
	Check("Rename", Show(Files.Rename(file, renamed)), "true");
	Check("Rename source gone", Show(Files.Exists(file)), "false");
	string copy = folder + "/copy.txt";
	Check("Copy", Show(Files.Copy(renamed, copy)), "true");
	Check("Compare contents", Show(Files.Compare(renamed, copy, Files.CompareOptions.CompareContents)), "true");
	string moved = folder + "/moved.txt";
	Check("Move", Show(Files.Move(copy, moved)), "true");
	Check("Move source gone", Show(Files.Exists(copy)), "false");
	// Compare checks the size by default, the contents only when requested
	string other = folder + "/other.txt";
	Files.WriteTextFile(other, "This is my rifle, is my wife");
	Check("Compare same size", Show(Files.Compare(renamed, other)), "true");
	Check("Compare different", Show(Files.Compare(renamed, other, Files.CompareOptions.CompareContents)), "false");
	Check("Delete", Show(Files.Delete(moved)), "true");
	Check("Delete gone", Show(Files.Exists(moved)), "false");
	EndBanner();
}

/// <summary>
/// File operations on a missing file: the failure is reported in the return value.
/// </summary>
void TestMissingFiles(){
	Banner("[5/9] File operations on a missing file (expected to fail)");
	Msg.Print("The engine reports the missing file for the read and size operations, those lines are expected:");
	string missing = Sandbox + "/files/missing.txt";
	string target = Sandbox + "/files/target.txt";
	Check("Exists", Show(Files.Exists(missing)), "false");
	Check("ReadTextFile", Quote(Files.ReadTextFile(missing)), "\"\"");
	Check("GetFileSize", Files.GetFileSize(missing).ToString(), "-1");
	Check("GetModifiedTime", Files.GetModifiedTime(missing).ToString(), "0");
	Check("Rename", Show(Files.Rename(missing, target)), "false");
	Check("Copy", Show(Files.Copy(missing, target)), "false");
	Check("Compare", Show(Files.Compare(missing, Sandbox + "/files/renamed.txt")), "false");
	Check("Delete", Show(Files.Delete(missing)), "false");
	Check("nothing created", Show(Files.Exists(target)), "false");
	EndBanner();
}

/// <summary>
/// Folder operations on the sandbox, including the failing ones.
/// </summary>
void TestFolders(){
	Banner("[6/9] Folder operations");
	string root = Sandbox + "/folders";
	string created = root + "/created/nested";
	Check("Create nested", Show(Folders.Create(created)), "true");
	Check("Exists", Show(Folders.Exists(created)), "true");
	Check("Create existing", Show(Folders.Create(created)), "true");
	// Copy a tree, with and without its subfolders
	string copied = root + "/copied";
	Check("Copy with subfolders", Show(Folders.Copy("folder1", copied, Folders.CopyOptions.IncludeSubFolders)), "true");
	Check("copied file exists", Show(Files.Exists(copied + "/src/file1.txt")), "true");
	string flat = root + "/flat";
	Check("Copy top level only", Show(Folders.Copy("folder1", flat)), "true");
	Check("subfolder not copied", Show(Folders.Exists(flat + "/src")), "false");
	// Move
	string moved = root + "/moved";
	Check("Move", Show(Folders.Move(copied, moved)), "true");
	Check("Move source gone", Show(Folders.Exists(copied)), "false");
	Check("Move onto existing", Show(Folders.Move(moved, flat)), "false");
	// Delete: a folder with subfolders needs the recursive flag
	Check("Delete not recursive", Show(Folders.Delete(root + "/created", false)), "false");
	Check("Delete recursive", Show(Folders.Delete(root + "/created", true)), "true");
	Check("Delete gone", Show(Folders.Exists(root + "/created")), "false");
	Check("Delete missing", Show(Folders.Delete(root + "/missing", true)), "true");
	Check("Copy missing source", Show(Folders.Copy(root + "/missing", root + "/missing2")), "false");
	EndBanner();
}

/// <summary>
/// Zip compression: folders, several folders, single files, and the failing cases.
/// </summary>
void TestZip(){
	Banner("[7/9] Zip compression");
	string root = Sandbox + "/zip";
	Folders.Create(root);
	// A folder, with the folder itself included or with its contents only
	Check("CompressFolder", Show(Compress.Zip.CompressFolder("folder1", root + "/folder.zip")), "true");
	Check("Decompress", Show(Compress.Zip.Decompress(root + "/folder.zip", root + "/out1/")), "true");
	Check("folder included", Show(Files.Exists(root + "/out1/folder1/src/file1.txt")), "true");
	Check("CompressFolder contents", Show(Compress.Zip.CompressFolder("folder1", root + "/contents.zip", true, false)), "true");
	Compress.Zip.Decompress(root + "/contents.zip", root + "/out2/");
	Check("folder not included", Show(Files.Exists(root + "/out2/src/file1.txt")), "true");
	// Several folders in one archive
	Check("CompressFolders", Show(Compress.Zip.CompressFolders(new string[] { "child", "folder1" }, root + "/folders.zip")), "true");
	Compress.Zip.Decompress(root + "/folders.zip", root + "/out3/");
	Check("both folders present", Show(Files.Exists(root + "/out3/child/child.csx") && Files.Exists(root + "/out3/folder1/parent.csx")), "true");
	// A single file, verified byte by byte after the round trip
	Check("CompressFile", Show(Compress.Zip.CompressFile(Fixture, root + "/file.zip")), "true");
	Compress.Zip.Decompress(root + "/file.zip", root + "/out4/");
	Check("file round trip", Show(Files.Compare(Fixture, root + "/out4/file1.txt", Files.CompareOptions.CompareContents)), "true");
	// Failing cases
	Check("CompressFile no overwrite", Show(Compress.Zip.CompressFile(Fixture, root + "/file.zip", false)), "false");
	Check("CompressFolder missing", Show(Compress.Zip.CompressFolder(root + "/missing", root + "/broken.zip")), "false");
	Check("Decompress missing", Show(Compress.Zip.Decompress(root + "/nothing.zip", root + "/out5/")), "false");
	EndBanner();
}

/// <summary>
/// Tar compression with every supported type, xz extraction, and the failing cases.
/// </summary>
void TestTar(){
	Banner("[8/9] Tar compression");
	string root = Sandbox + "/tar";
	Folders.Create(root);
	// Round trip of a folder with every supported compression type (xz is extraction only)
	(string Ext, TarCompressionType Type)[] types = {
		(".tar",     TarCompressionType.None),
		(".tar.gz",  TarCompressionType.Gzip),
		(".tar.bz2", TarCompressionType.Bzip2),
		(".tar.lz",  TarCompressionType.Lzma),
	};
	// Note: unlike zip, tar extraction needs the destination folder to exist
	foreach (var t in types){
		string archive = root + "/folder" + t.Ext;
		string outdir = root + "/out" + t.Ext + "/";
		Folders.Create(outdir);
		bool ok = Compress.Tar.CompressFolder("folder1", archive, true, true, t.Type)
			&& Compress.Tar.Decompress(archive, outdir)
			&& Files.Compare(Fixture, outdir + "folder1/src/file1.txt", Files.CompareOptions.CompareContents);
		Check("round trip " + t.Ext, Show(ok), "true");
	}
	// Contents only, several folders and a single file
	Check("CompressFolder contents", Show(Compress.Tar.CompressFolder("folder1", root + "/contents.tar.gz", true, false)), "true");
	Folders.Create(root + "/outc");
	Compress.Tar.Decompress(root + "/contents.tar.gz", root + "/outc/");
	Check("folder not included", Show(Files.Exists(root + "/outc/src/file1.txt")), "true");
	Check("CompressFolders", Show(Compress.Tar.CompressFolders(new string[] { "child", "folder1" }, root + "/folders.tar.gz")), "true");
	Folders.Create(root + "/outf");
	Compress.Tar.Decompress(root + "/folders.tar.gz", root + "/outf/");
	Check("both folders present", Show(Files.Exists(root + "/outf/child/child.csx") && Files.Exists(root + "/outf/folder1/parent.csx")), "true");
	Check("CompressFile", Show(Compress.Tar.CompressFile(Fixture, root + "/file.tar.bz2", true, TarCompressionType.Bzip2)), "true");
	Folders.Create(root + "/outs");
	Compress.Tar.Decompress(root + "/file.tar.bz2", root + "/outs/");
	Check("file round trip", Show(Files.Compare(Fixture, root + "/outs/file1.txt", Files.CompareOptions.CompareContents)), "true");
	// xz archives can be extracted (compressing to xz is not supported)
	Folders.Create(root + "/outx");
	Check("Decompress .tar.xz", Show(Compress.Tar.Decompress("test.files/test.tar.xz", root + "/outx/")), "true");
	Check("xz entries extracted", Show(Files.Exists(root + "/outx/exe/test.exe") && Files.Exists(root + "/outx/jpg/test.jpg") && Folders.Exists(root + "/outx/Empty")), "true");
	// Failing cases
	Check("CompressFolder missing", Show(Compress.Tar.CompressFolder(root + "/missing", root + "/broken.tar.gz")), "false");
	Check("Decompress missing", Show(Compress.Tar.Decompress(root + "/nothing.tar.gz", root + "/outm/")), "false");
	EndBanner();
}

/// <summary>
/// Safe extraction: entries trying to escape the destination folder (zip-slip) must be refused.
/// </summary>
void TestSafeExtract(){
	Banner("[9/9] Safe extraction (path traversal entries are refused)");
	string root = Sandbox + "/safe";
	Folders.Create(root + "/out");
	// A hand made tar with a benign entry and two entries trying to escape the destination folder
	byte[] payload = Encoding.ASCII.GetBytes("owned");
	byte[] evil = MakeTar(new (string name, byte[] data, bool isDir)[] {
		("benign.txt", payload, false),
		("../escaped_file", payload, false),
		("../escaped_dir/", Array.Empty<byte>(), true)
	});
	File.WriteAllBytes(root + "/evil.tar", evil);
	Msg.Print("The engine reports every refused entry, the line below is expected:");
	Check("Decompress evil.tar", Show(Compress.Tar.Decompress(root + "/evil.tar", root + "/out/")), "true");
	Check("benign entry extracted", Show(Files.Exists(root + "/out/benign.txt")), "true");
	Check("file entry refused", Show(Files.Exists(root + "/escaped_file")), "false");
	Check("directory entry refused", Show(Folders.Exists(root + "/escaped_dir")), "false");
	EndBanner();
}

/// <summary>
/// Minimal ustar tar builder, so the traversal test needs no committed binary fixture.
/// </summary>
/// <param name="entries">Entries to write: name, data and whether it is a directory.</param>
/// <returns>The tar archive bytes.</returns>
byte[] MakeTar((string name, byte[] data, bool isDir)[] entries){
	using var ms = new MemoryStream();
	foreach (var e in entries){
		byte[] h = new byte[512];
		long size = e.isDir ? 0 : e.data.Length;
		TarPutString(h, 0, 100, e.name);
		TarPutOctal(h, 100, 8, 0x1ED);
		TarPutOctal(h, 124, 12, size);
		TarPutOctal(h, 136, 12, 0);
		h[156] = (byte)(e.isDir ? '5' : '0');
		TarPutString(h, 257, 6, "ustar");
		h[263] = (byte)'0';
		h[264] = (byte)'0';
		for (int i = 148; i < 156; i++)
			h[i] = (byte)' ';
		int sum = 0;
		foreach (byte b in h)
			sum += b;
		TarPutString(h, 148, 6, Convert.ToString(sum, 8).PadLeft(6, '0'));
		h[154] = 0;
		h[155] = (byte)' ';
		ms.Write(h, 0, 512);
		if (!e.isDir && size > 0){
			ms.Write(e.data, 0, e.data.Length);
			int pad = (int)((512 - (size % 512)) % 512);
			if (pad > 0)
				ms.Write(new byte[pad], 0, pad);
		}
	}
	ms.Write(new byte[1024], 0, 1024);
	return ms.ToArray();
}

/// <summary>
/// Writes an ASCII string into a tar header field.
/// </summary>
void TarPutString(byte[] header, int offset, int length, string value){
	byte[] bytes = Encoding.ASCII.GetBytes(value);
	for (int i = 0; i < bytes.Length && i < length; i++)
		header[offset + i] = bytes[i];
}

/// <summary>
/// Writes an octal number into a tar header field.
/// </summary>
void TarPutOctal(byte[] header, int offset, int length, long value){
	TarPutString(header, offset, length - 1, Convert.ToString(value, 8).PadLeft(length - 1, '0'));
	header[offset + length - 1] = 0;
}
