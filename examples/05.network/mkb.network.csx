/*---------------------------------------------------------------------------------------------------------

	Kombine Network Functions Example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

//
// Load a script include from http source
//
#load "https://raw.githubusercontent.com/kollective-networks/kltv.kombine/main/extensions/clang.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
// The results of the checks, for the examples runner
#load "mkb.results.csx"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.Text;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Text document to retrieve
string DocumentUrl = "https://raw.githubusercontent.com/kollective-networks/kltv.kombine/main/extensions/clang.csx";

// Download source that must succeed: release assets of this project (tag Release-1.4.24072788).
// They are served with a Content-Length header so the progress bar can be reported.
// Sizes come from the GitHub releases API and are used to verify the downloaded files.
string ReleaseUrl = "https://github.com/kollective-networks/kltv.kombine/releases/download/Release-1.4.24072788/";
string SingleFile = "kombine.win.zip";
long SingleSize = 35104624;
string[] MultiFiles = { "kombine.ref.zip", "kombine.lnx.tar.gz", "kombine.osx.tar.gz" };
long[] MultiSizes = { 12146, 34650125, 35160398 };

// Download source that must fail: files that do not exist in this repository (http status 404)
string MissingUrl = "https://raw.githubusercontent.com/kollective-networks/kltv.kombine/main/does-not-exist/";

int passed = 0;
int failed = 0;

/// <summary>
/// Exercises the network API: script include from http (#load), document retrieval and single /
/// multiple file downloads with progress. Every function is checked against a source that must
/// succeed and a source that must fail, so both outcomes are verified.
/// The engine only logs network errors at verbose level (they are meant to debug the engine),
/// so the script checks the results and Http.LastReturnCode and prints its own messages.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check passed, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing network functions");
	Msg.BeginIndent();

	string outFolder = CurrentWorkingFolder + "/.tmp.network";
	if (Folders.Exists(outFolder))
		Folders.Delete(outFolder, true);

	// The #load directive at the top of this script pulls the clang extension from GitHub.
	// Reaching this point means the include was downloaded and compiled fine.
	Msg.Print("Script include loaded from http (#load): clang.csx");
	Msg.RawPrint(Environment.NewLine);

	//
	// Document retrieval: verify status and content, do not dump the document
	//
	Msg.Print("[1/6] Get document");
	Msg.BeginIndent();
	Msg.Print("url      : " + DocumentUrl);
	string content = Http.GetDocument(DocumentUrl);
	int status = Http.LastReturnCode;
	int bytes = Encoding.UTF8.GetByteCount(content);
	int lines = content.Length == 0 ? 0 : content.Split('\n').Length;
	Report("result", "status " + status + ", " + FormatSize(bytes) + ", " + lines + " lines",
		status == 200 && bytes > 0,
		"could not retrieve the document (http status " + status + ")");
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);

	//
	// Document retrieval from a missing url: must return an empty document and the 404 status
	//
	Msg.Print("[2/6] Get document from a missing url (expected to fail)");
	Msg.BeginIndent();
	Msg.Print("url      : " + MissingUrl + "document.txt");
	content = Http.GetDocument(MissingUrl + "document.txt");
	status = Http.LastReturnCode;
	Report("result", "status " + status + ", " + content.Length + " chars returned",
		status == 404 && content.Length == 0,
		"expected an empty document with http status 404");
	// The reason is also available as Http.LastError, ready to be shown by the script
	Report("error", "code " + Http.LastError.Code + ": " + Http.LastError.Message,
		Http.LastError.Code == ErrorCode.Failed,
		"code Failed with the status in the message");
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);

	//
	// Single download: the API prints start / finish and draws the progress bar in between
	//
	Msg.Print("[3/6] Download file");
	Msg.BeginIndent();
	string file = outFolder + "/" + SingleFile;
	bool ok = Http.DownloadFile(ReleaseUrl + SingleFile, file);
	status = Http.LastReturnCode;
	long written = Files.Exists(file) ? Files.GetFileSize(file) : 0;
	string error = "";
	if (!ok)
		error = "download failed (http status " + status + ")";
	else if (written != SingleSize)
		error = "size mismatch on " + SingleFile;
	Report("result", FormatSize(written) + " written, expected " + FormatSize(SingleSize), error == "", error);
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);

	//
	// Single download from a missing url: must return false, the 404 status and leave no file behind
	//
	Msg.Print("[4/6] Download file from a missing url (expected to fail)");
	Msg.BeginIndent();
	file = outFolder + "/missing.bin";
	ok = Http.DownloadFile(MissingUrl + "missing.bin", file);
	status = Http.LastReturnCode;
	bool left = Files.Exists(file);
	Report("result", "status " + status + ", returned " + (ok ? "true" : "false") + (left ? ", file left behind" : ", no file left"),
		!ok && status == 404 && !left,
		"expected a failed download with http status 404 and no file left behind");
	Report("error", "code " + Http.LastError.Code + " (status " + status + ")",
		Http.LastError.Code == ErrorCode.Failed,
		"code Failed for an http error status");
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);

	//
	// Multiple downloads: run in parallel, the progress bar shows the average progress
	//
	Msg.Print("[5/6] Download multiple files");
	Msg.BeginIndent();
	string[] uris = new string[MultiFiles.Length];
	string[] paths = new string[MultiFiles.Length];
	long expectedTotal = 0;
	for (int i = 0; i < MultiFiles.Length; i++){
		uris[i] = ReleaseUrl + MultiFiles[i];
		paths[i] = outFolder + "/" + MultiFiles[i];
		expectedTotal += MultiSizes[i];
	}
	// The downloads report through Http.Progress: dots for this batch, the default bar afterwards
	Http.Progress = new ProgressDots();
	ok = Http.DownloadFiles(uris, paths);
	Http.Progress = null;
	status = Http.LastReturnCode;
	long writtenTotal = 0;
	string mismatched = "";
	for (int i = 0; i < MultiFiles.Length; i++){
		written = Files.Exists(paths[i]) ? Files.GetFileSize(paths[i]) : 0;
		writtenTotal += written;
		if (written != MultiSizes[i])
			mismatched += (mismatched == "" ? "" : ", ") + MultiFiles[i];
	}
	error = "";
	if (!ok)
		error = "one or more downloads failed (http status " + status + ")";
	else if (mismatched != "")
		error = "size mismatch on " + mismatched;
	Report("result", MultiFiles.Length + " files, " + FormatSize(writtenTotal) + " written, expected " + FormatSize(expectedTotal), error == "", error);
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);

	//
	// Multiple downloads with missing urls: a single failure must fail the whole batch
	//
	Msg.Print("[6/6] Download multiple files with missing urls (expected to fail)");
	Msg.BeginIndent();
	uris = new string[] { ReleaseUrl + "kombine.ref.zip", MissingUrl + "missing1.bin", MissingUrl + "missing2.bin" };
	paths = new string[] { outFolder + "/mixed1.zip", outFolder + "/mixed2.bin", outFolder + "/mixed3.bin" };
	ok = Http.DownloadFiles(uris, paths);
	status = Http.LastReturnCode;
	Report("result", "status " + status + ", returned " + (ok ? "true" : "false") + " (1 valid, 2 missing)",
		!ok && status == 404,
		"expected a failed batch with http status 404");
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);

	Folders.Delete(outFolder, true);

	int total = passed + failed;
	TestSummary(passed, failed);

	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"Network tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Prints an aligned result line with a colored OK / FAILED tag and accounts the outcome.
/// On failure, the given error is printed below as the script's own explanation.
/// </summary>
/// <param name="label">Label of the line.</param>
/// <param name="detail">Detail to be shown.</param>
/// <param name="ok">Outcome of the check.</param>
/// <param name="error">Reason to be shown when the check failed.</param>
void Report(string label, string detail, bool ok, string error = ""){
	Msg.PrintTask($"{label,-8} : {detail,-48}");
	if (ok){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		if (error != "")
			Msg.PrintError($"{"error",-8} : {error}");
	}
	TestResult(ok ? "OK" : "FAILED", label + " : " + detail);
}

/// <summary>
/// Human readable size.
/// </summary>
/// <param name="bytes">Size in bytes.</param>
/// <returns>Size with unit.</returns>
string FormatSize(long bytes){
	if (bytes >= 1024 * 1024)
		return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
	if (bytes >= 1024)
		return (bytes / 1024.0).ToString("0.0") + " KB";
	return bytes + " bytes";
}
