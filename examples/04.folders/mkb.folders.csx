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
	TestTarLinks();

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
	Banner("[1/10] Well known folders");
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
	Banner("[2/10] Forward file search");
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
	Banner("[3/10] Child scripts and backward search");
	int code = Kombine("child/child.csx", "test", args, false);
	Check("child.csx returned", code.ToString(), "0");
	// The forward search on dispatch is disabled by default (-kforward): locate the script explicitly
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
	Banner("[4/10] File operations");
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
	Check("Compare error", Files.LastError.Code.ToString(), "Different");
	Check("Delete", Show(Files.Delete(moved)), "true");
	Check("Delete gone", Show(Files.Exists(moved)), "false");
	// The executable bits: nothing to do on Windows, set and cleared elsewhere
	Check("SetExecutable", Show(Files.SetExecutable(renamed)), "true");
	if (!Host.IsWindows()){
		Check("executable bit set", Show((File.GetUnixFileMode(renamed) & UnixFileMode.UserExecute) != 0), "true");
		Check("SetExecutable cleared", Show(Files.SetExecutable(renamed, false) && (File.GetUnixFileMode(renamed) & UnixFileMode.UserExecute) == 0), "true");
	}
	EndBanner();
}

/// <summary>
/// File operations on a missing file: the failure is reported in the return value.
/// </summary>
void TestMissingFiles(){
	Banner("[5/10] File operations on a missing file (expected to fail)");
	// Every failure is reported through the return value and Files.LastError, nothing is printed
	string missing = Sandbox + "/files/missing.txt";
	string target = Sandbox + "/files/target.txt";
	Check("Exists", Show(Files.Exists(missing)), "false");
	Check("ReadTextFile", Quote(Files.ReadTextFile(missing)), "\"\"");
	Check("ReadTextFile error", Files.LastError.Code.ToString(), "NotFound");
	Check("GetFileSize", Files.GetFileSize(missing).ToString(), "-1");
	Check("GetFileSize error", Files.LastError.Code.ToString(), "NotFound");
	Check("GetModifiedTime", Files.GetModifiedTime(missing).ToString(), "0");
	Check("GetModifiedTime error", Files.LastError.Code.ToString(), "NotFound");
	Check("Rename", Show(Files.Rename(missing, target)), "false");
	Check("Rename error", Files.LastError.Code.ToString(), "NotFound");
	Check("SetExecutable", Show(Files.SetExecutable(missing)), "false");
	Check("SetExecutable error", Files.LastError.Code.ToString(), "NotFound");
	Check("Copy", Show(Files.Copy(missing, target)), "false");
	Check("Copy error", Files.LastError.Code.ToString(), "NotFound");
	Check("Compare", Show(Files.Compare(missing, Sandbox + "/files/renamed.txt")), "false");
	Check("Compare error", Files.LastError.Code.ToString(), "NotFound");
	Check("Delete", Show(Files.Delete(missing)), "false");
	Check("Delete error", Files.LastError.Code.ToString(), "NotFound");
	Check("nothing created", Show(Files.Exists(target)), "false");
	// The message and the item are ready to be shown by the script
	Verify("LastError message", Files.LastError.Message, Files.LastError.Message.Length > 0, "a message explaining the failure");
	Verify("LastError source", Tail(Files.LastError.Source, 2), Unix(Files.LastError.Source).EndsWith("/files/missing.txt"), "the missing file");
	EndBanner();
}

/// <summary>
/// Folder operations on the sandbox, including the failing ones.
/// </summary>
void TestFolders(){
	Banner("[6/10] Folder operations");
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
	// With ShowProgress the copy prints a progress line through Folders.Progress (the engine default here)
	string shown = root + "/shown";
	Check("Copy with progress", Show(Folders.Copy("folder1", shown, Folders.CopyOptions.IncludeSubFolders | Folders.CopyOptions.ShowProgress)), "true");
	Check("shown file exists", Show(Files.Exists(shown + "/src/file1.txt")), "true");
	// Unchanged files are skipped but still counted, so the progress reaches the end
	Check("Copy again, only modified", Show(Folders.Copy("folder1", shown, Folders.CopyOptions.IncludeSubFolders | Folders.CopyOptions.OnlyModifiedFiles | Folders.CopyOptions.ShowProgress)), "true");
	Check("Copy missing with progress", Show(Folders.Copy(root + "/absent", root + "/absent2", Folders.CopyOptions.ShowProgress)), "false");
	// Move
	string moved = root + "/moved";
	Check("Move", Show(Folders.Move(copied, moved)), "true");
	Check("Move source gone", Show(Folders.Exists(copied)), "false");
	Check("Move onto existing", Show(Folders.Move(moved, flat)), "false");
	Check("Move error", Folders.LastError.Code.ToString(), "AlreadyExists");
	// Delete: a folder with subfolders needs the recursive flag
	Check("Delete not recursive", Show(Folders.Delete(root + "/created", false)), "false");
	Check("Delete error", Folders.LastError.Code.ToString(), "IoError");
	Check("Delete recursive", Show(Folders.Delete(root + "/created", true)), "true");
	Check("Delete gone", Show(Folders.Exists(root + "/created")), "false");
	Check("Delete missing", Show(Folders.Delete(root + "/missing", true)), "true");
	Check("Copy missing source", Show(Folders.Copy(root + "/missing", root + "/missing2")), "false");
	Check("Copy error", Folders.LastError.Code.ToString(), "NotFound");
	Check("nothing created", Show(Folders.Exists(root + "/missing2")), "false");
	EndBanner();
}

/// <summary>
/// Zip compression: folders, several folders, single files, and the failing cases.
/// </summary>
void TestZip(){
	Banner("[7/10] Zip compression");
	string root = Sandbox + "/zip";
	Folders.Create(root);
	// The archive operations show a progress line (Compress.Progress, a bar by default): shown for the
	// first round trip and silenced for the rest to keep the report short
	// A folder, with the folder itself included or with its contents only
	Check("CompressFolder", Show(Compress.Zip.CompressFolder("folder1", root + "/folder.zip")), "true");
	Check("Decompress", Show(Compress.Zip.Decompress(root + "/folder.zip", root + "/out1/")), "true");
	Check("folder included", Show(Files.Exists(root + "/out1/folder1/src/file1.txt")), "true");
	Compress.ShowProgress = false;
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
	// Failing cases: false plus the reason in Compress.Zip.LastError, and no partial archive left behind
	Check("CompressFile no overwrite", Show(Compress.Zip.CompressFile(Fixture, root + "/file.zip", false)), "false");
	Check("no overwrite error", Compress.Zip.LastError.Code.ToString(), "AlreadyExists");
	Check("CompressFolder missing", Show(Compress.Zip.CompressFolder(root + "/missing", root + "/broken.zip")), "false");
	Check("missing error", Compress.Zip.LastError.Code.ToString(), "NotFound");
	Check("no archive left", Show(Files.Exists(root + "/broken.zip")), "false");
	Check("Decompress missing", Show(Compress.Zip.Decompress(root + "/nothing.zip", root + "/out5/")), "false");
	Check("Decompress error", Compress.Zip.LastError.Code.ToString(), "NotFound");
	// Existing files with overwrite disabled are skipped and reported
	Check("Decompress no overwrite", Show(Compress.Zip.Decompress(root + "/folder.zip", root + "/out1/", false)), "false");
	Check("skipped error", Compress.Zip.LastError.Code.ToString(), "AlreadyExists");
	Compress.ShowProgress = true;
	EndBanner();
}

/// <summary>
/// Tar compression with every supported type, xz extraction, and the failing cases.
/// </summary>
void TestTar(){
	Banner("[8/10] Tar compression");
	string root = Sandbox + "/tar";
	Folders.Create(root);
	// The progress line is silenced for the round trips and shown as dots for the several folders case below
	Compress.ShowProgress = false;
	// Round trip of a folder with every supported compression type (xz is extraction only)
	(string Ext, TarCompressionType Type)[] types = {
		(".tar",     TarCompressionType.None),
		(".tar.gz",  TarCompressionType.Gzip),
		(".tar.bz2", TarCompressionType.Bzip2),
		(".tar.lz",  TarCompressionType.Lzma),
	};
	// The destination folder is created by Decompress
	foreach (var t in types){
		string archive = root + "/folder" + t.Ext;
		string outdir = root + "/out" + t.Ext + "/";
		bool ok = Compress.Tar.CompressFolder("folder1", archive, true, true, t.Type)
			&& Compress.Tar.Decompress(archive, outdir)
			&& Files.Compare(Fixture, outdir + "folder1/src/file1.txt", Files.CompareOptions.CompareContents);
		Check("round trip " + t.Ext, Show(ok), "true");
	}
	// Contents only, several folders and a single file
	Check("CompressFolder contents", Show(Compress.Tar.CompressFolder("folder1", root + "/contents.tar.gz", true, false)), "true");
	Compress.Tar.Decompress(root + "/contents.tar.gz", root + "/outc/");
	Check("folder not included", Show(Files.Exists(root + "/outc/src/file1.txt")), "true");
	// Several folders, with the progress line rendered as dots: the renderer is selected by assigning Compress.Progress
	Compress.Progress = new ProgressDots();
	Check("CompressFolders", Show(Compress.Tar.CompressFolders(new string[] { "child", "folder1" }, root + "/folders.tar.gz", showprogress: true)), "true");
	Compress.Tar.Decompress(root + "/folders.tar.gz", root + "/outf/", showprogress: true);
	Compress.Progress = null;
	Check("both folders present", Show(Files.Exists(root + "/outf/child/child.csx") && Files.Exists(root + "/outf/folder1/parent.csx")), "true");
	Check("CompressFile", Show(Compress.Tar.CompressFile(Fixture, root + "/file.tar.bz2", true, TarCompressionType.Bzip2)), "true");
	Compress.Tar.Decompress(root + "/file.tar.bz2", root + "/outs/");
	Check("file round trip", Show(Files.Compare(Fixture, root + "/outs/file1.txt", Files.CompareOptions.CompareContents)), "true");
	// xz archives can be extracted, compressing to xz is not supported and says so
	Check("Decompress .tar.xz", Show(Compress.Tar.Decompress("test.files/test.tar.xz", root + "/outx/")), "true");
	Check("xz entries extracted", Show(Files.Exists(root + "/outx/exe/test.exe") && Files.Exists(root + "/outx/jpg/test.jpg") && Folders.Exists(root + "/outx/Empty")), "true");
	Check("CompressFile to xz", Show(Compress.Tar.CompressFile(Fixture, root + "/file.tar.xz", true, TarCompressionType.Lzma2)), "false");
	Check("xz error", Compress.Tar.LastError.Code.ToString(), "NotSupported");
	// Failing cases: false plus the reason in Compress.Tar.LastError, and no partial archive left behind
	Check("CompressFolder missing", Show(Compress.Tar.CompressFolder(root + "/missing", root + "/broken.tar.gz")), "false");
	Check("missing error", Compress.Tar.LastError.Code.ToString(), "NotFound");
	Check("no archive left", Show(Files.Exists(root + "/broken.tar.gz")), "false");
	Check("Decompress missing", Show(Compress.Tar.Decompress(root + "/nothing.tar.gz", root + "/outm/")), "false");
	Check("Decompress error", Compress.Tar.LastError.Code.ToString(), "NotFound");
	// Existing files with overwrite disabled are skipped and reported
	Check("Decompress no overwrite", Show(Compress.Tar.Decompress(root + "/folders.tar.gz", root + "/outf/", false)), "false");
	Check("skipped error", Compress.Tar.LastError.Code.ToString(), "AlreadyExists");
	Compress.ShowProgress = true;
	EndBanner();
}

/// <summary>
/// Safe extraction: entries trying to escape the destination folder (zip-slip) must be refused.
/// </summary>
void TestSafeExtract(){
	Banner("[9/10] Safe extraction (path traversal entries are refused)");
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
	// The refused entries make the call fail, the benign ones are still extracted
	Check("Decompress evil.tar", Show(Compress.Tar.Decompress(root + "/evil.tar", root + "/out/")), "false");
	Check("refusal reported", Compress.Tar.LastError.Code.ToString(), "Failed");
	Check("benign entry extracted", Show(Files.Exists(root + "/out/benign.txt")), "true");
	Check("file entry refused", Show(Files.Exists(root + "/escaped_file")), "false");
	Check("directory entry refused", Show(Folders.Exists(root + "/escaped_dir")), "false");
	EndBanner();
}

/// <summary>
/// Links and permissions of a tar archive: a symbolic link is created as a link (on Windows as a
/// copy of its target when the process may not create links), a hard link as a copy, a link that
/// leaves the destination folder is refused, and outside Windows the executable bit is restored.
/// </summary>
void TestTarLinks(){
	Banner("[10/10] Tar links and permissions");
	string root = Sandbox + "/links";
	Folders.Create(root);
	// A hand made tar: a tool with the executable bit, a symbolic link to it, a hard link to it, a link
	// escaping the destination folder and a plain file
	byte[] tool = Encoding.ASCII.GetBytes("#!/bin/sh\necho tool\n");
	byte[] tar = MakeTar(new (string name, byte[] data, char type, string link, int mode)[] {
		("bin/", Array.Empty<byte>(), '5', "", 0x1ED),
		("bin/tool", tool, '0', "", 0x1ED),
		("bin/link", Array.Empty<byte>(), '2', "tool", 0x1FF),
		("bin/hard", Array.Empty<byte>(), '1', "bin/tool", 0x1ED),
		("bin/evil", Array.Empty<byte>(), '2', "../../outside", 0x1FF),
		("readme.txt", tool, '0', "", 0x1A4)
	});
	File.WriteAllBytes(root + "/links.tar", tar);
	// The escaping link is refused and makes the call fail; everything else is extracted
	Check("Decompress links.tar", Show(Compress.Tar.Decompress(root + "/links.tar", root + "/out/")), "false");
	Check("refusal reported", Compress.Tar.LastError.Code + ", " + (Compress.Tar.LastError.Message.Contains("refused") ? "refused" : Compress.Tar.LastError.Message), "Failed, refused");
	Check("file extracted", Show(Files.Exists(root + "/out/bin/tool")), "true");
	Check("symbolic link usable", Show(Files.Exists(root + "/out/bin/link") && Files.Compare(root + "/out/bin/tool", root + "/out/bin/link", Files.CompareOptions.CompareContents)), "true");
	Check("hard link copied", Show(Files.Compare(root + "/out/bin/tool", root + "/out/bin/hard", Files.CompareOptions.CompareContents)), "true");
	Check("escaping link refused", Show(!Files.Exists(root + "/out/bin/evil") && !Files.Exists(root + "/outside")), "true");
	if (!Host.IsWindows()){
		Check("executable bit restored", Show((File.GetUnixFileMode(root + "/out/bin/tool") & UnixFileMode.UserExecute) != 0), "true");
		Check("plain file not executable", Show((File.GetUnixFileMode(root + "/out/readme.txt") & UnixFileMode.UserExecute) == 0), "true");
		Check("link is a link", Show(new FileInfo(root + "/out/bin/link").LinkTarget != null), "true");
	}
	// Extracting again replaces the links and the files; the escaping link is refused again
	Check("Decompress again", Show(Compress.Tar.Decompress(root + "/links.tar", root + "/out/")), "false");
	Check("symbolic link still usable", Show(Files.Compare(root + "/out/bin/tool", root + "/out/bin/link", Files.CompareOptions.CompareContents)), "true");
	EndBanner();
}

/// <summary>
/// Minimal ustar tar builder, so the traversal test needs no committed binary fixture.
/// </summary>
/// <param name="entries">Entries to write: name, data and whether it is a directory.</param>
/// <returns>The tar archive bytes.</returns>
byte[] MakeTar((string name, byte[] data, bool isDir)[] entries){
	var full = new (string name, byte[] data, char type, string link, int mode)[entries.Length];
	for (int i = 0; i < entries.Length; i++)
		full[i] = (entries[i].name, entries[i].data, entries[i].isDir ? '5' : '0', "", 0x1ED);
	return MakeTar(full);
}

/// <summary>
/// The full form of the tar builder: the type of every entry ('0' file, '5' directory, '2' symbolic
/// link, '1' hard link), its link target and its mode.
/// </summary>
/// <param name="entries">Entries to write: name, data, type, link target and mode.</param>
/// <returns>The tar archive bytes.</returns>
byte[] MakeTar((string name, byte[] data, char type, string link, int mode)[] entries){
	using var ms = new MemoryStream();
	foreach (var e in entries){
		byte[] h = new byte[512];
		long size = e.type == '0' ? e.data.Length : 0;
		TarPutString(h, 0, 100, e.name);
		TarPutOctal(h, 100, 8, e.mode);
		TarPutOctal(h, 124, 12, size);
		TarPutOctal(h, 136, 12, 0);
		h[156] = (byte)e.type;
		TarPutString(h, 157, 100, e.link);
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
		if (size > 0){
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
