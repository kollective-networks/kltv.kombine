/*---------------------------------------------------------------------------------------------------------

	Kombine Build & Publishing Script

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

//
// Use dotnet doc extension
// 
#load "dotnet.doc.csx"
#load "github.csx"


// Remember, this is just used for intellisense, nothing else
#r "out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


int help(string[] args){
	Msg.Print("");
	Msg.Print("Kombine Build & Publishing Script");
	Msg.Print("");
	Msg.Print("Usage: mkb publish (builds and create packages)");
	Msg.Print("       mkb release (push the release to github)");
	Msg.Print("       mkb build (just build for the current system in debug mode)");
	Msg.Print("       mkb test (runs the unit tests)");
	Msg.Print("");
	return 0;
}

int test(string[] args){
	Msg.Print("Running tests");
	int ExitCode = Kombine("examples/kombine.csx","test",args);
	return ExitCode;
}

int build(string[] args){
	Msg.Print("Building for current system");
	Folders.SetCurrentFolder(CurrentScriptFolder+"/src/",true);
	int ExitCode = Exec("dotnet","build -c debug",true);
	Folders.CurrentFolderPop();
	return ExitCode;
}

// Other options to take into consideration
// -p:PublishReadyToRun=true
// -p:PublishSingleFile=true -p:PublishReadyToRun=true
// -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true

/// <summary>
/// Builds all the different packages for the tool
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int publish(string[] args){
	int ExitCode;

	// Apply the version number
	// ------------------------------------------------------------------------------------------
	ApplyVersionNumber();
	Folders.SetCurrentFolder(CurrentScriptFolder+"/src/",true);
	// Windows
	// ------------------------------------------------------------------------------------------
	Msg.Print("Building for Windows");
	ExitCode = Exec("dotnet","build -c Release -r win-x64",true);
	if (ExitCode != 0)
		return ExitCode;
	Msg.Print("Creating output folder.");
	Folders.Create("../out/pkg");
	Msg.Print("Compress the reference assembly");
	Compress.Zip.CompressFile("../out/bin/win-x64/release/ref/mkb.dll","../out/pkg/kombine.ref.zip");
	Msg.Print("[Windows] Compress the unpacked tool");
	Compress.Zip.CompressFolder("../out/bin/win-x64/release/","../out/pkg/kombine.debug.win.zip",true,false);
	// Generate the single file package
	Msg.Print("[Windows] Generate the single file tool");
	ExitCode = Exec("dotnet","publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true",true);
	if (ExitCode != 0)
		return ExitCode;
	Msg.Print("[Windows] Compress the single file tool");
	Compress.Zip.CompressFile("../out/pub/win-x64/release/mkb.exe","../out/pkg/kombine.win.zip");
	// Linux
	// ------------------------------------------------------------------------------------------
	Msg.Print("Building for Linux");
	ExitCode = Exec("dotnet","build -c Release -r linux-x64",true);
	if (ExitCode != 0)
		return ExitCode;
	Msg.Print("[Linux] Compress the unpacked tool");
	Compress.Tar.CompressFolder("../out/bin/linux-x64/release/","../out/pkg/kombine.debug.lnx.tar.gz",true,true);
	// Generate the single file package
	Msg.Print("[Linux] Generate the single file tool");
	ExitCode = Exec("dotnet","publish -c Release -r linux-x64 -p:PublishSingleFile=true --self-contained true",true);
	if (ExitCode != 0)
		return ExitCode;
	Msg.Print("[Linux] Compress the single file tool");
	Compress.Tar.CompressFile("../out/pub/linux-x64/release/mkb","../out/pkg/kombine.lnx.tar.gz");
	// OSX
	// ------------------------------------------------------------------------------------------
	Msg.Print("Building for Mac OSX");
	ExitCode = Exec("dotnet","build -c Release -r osx-x64",true);
	if (ExitCode != 0)
		return ExitCode;
	Msg.Print("[MacOS] Compress the unpacked tool");
	Compress.Tar.CompressFolder("../out/bin/osx-x64/release/","../out/pkg/kombine.debug.osx.tar.gz",true,true);
	// Generate the single file package
	Msg.Print("[MacOS] Generate the single file tool");
	ExitCode = Exec("dotnet","publish -c Release -r osx-x64 -p:PublishSingleFile=true --self-contained true",true);
	if (ExitCode != 0)
		return ExitCode;
	Msg.Print("[MacOS] Compress the single file tool");
	Compress.Tar.CompressFile("../out/pub/osx-x64/release/mkb","../out/pkg/kombine.osx.tar.gz");
	// ------------------------------------------------------------------------------------------
	Folders.CurrentFolderPop();
	RestoreVersionNumber();
	Msg.Print("Generate documentation from the XML generated into the doc folder");
	XmlToMarkdown.Convert("out/bin/win-x64/release/mkb.xml","doc/api.md");
	Msg.Print("Done!");
	return 0;
}

//
// Push the release to github, create the release and upload the packages
//
int release(string[] args) {

	//
	// Get the token from environment variable
	// 
	KValue token = KValue.Import("kltv_token");
	//
	// Get the version from the version.txt file
	string version = Files.ReadTextFile("out/pkg/version.txt");
	version = version.Trim();
	if (version == "") {
		Msg.PrintAndAbort("Version number not found in version.txt. Did you create the packages?");
	}
	string notes = GetReleaseNotes(version);

	//
	// Create the github instance and configure it
	//
	Github github = new Github();
	github.Repository = "kltv.kombine";
	github.Owner = "kollective-networks";
	github.Token = token.ToString();
	string releaseId = github.GetReleaseID(version);
	if (releaseId == "") {
		releaseId = github.CreateRelease(version,"Release " + version,GetReleaseNotes(version),true);
	}
	//
	// TODO: Upload the packages as assets to the release
	// This would require getting the release ID from the response, then POST to upload_url for each file
	return 0;
}

private void ApplyVersionNumber(){
	// Backup the version.cs file before modifying it
	Files.Copy("src/version.cs","src/version.cs.bak");
	// Read the version.cs file and extract the major and minor version numbers, then generate the build number and replace it in the file
	string file = "src/version.cs";
	string content = Files.ReadTextFile(file);
	string major = ExtractBetween(content, "public static string Major = \"", "\";");
	string minor = ExtractBetween(content, "public static string Minor = \"", "\";");
	string build = GetVersionBuildNumber();
	string version = major + "." + minor + "." + build;
	content = content.Replace("[BUILD]",build);
	Files.WriteTextFile(file,content);
	// Create the output folder and save the version number in a text file for later use
	Folders.Create("out/pkg/");
	Files.WriteTextFile("out/pkg/version.txt",version);
}

private void RestoreVersionNumber(){
	Files.Delete("src/version.cs");
	Files.Move("src/version.cs.bak","src/version.cs");
}

private string GetVersionBuildNumber() {
	DateTime currentTime = DateTime.UtcNow;
	long now = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
	DateTime currentYear = new DateTime(DateTime.Now.Year, 1, 1);
	long year = ((DateTimeOffset)currentYear).ToUnixTimeSeconds();
	long bn = now - year;
	string buildNumber = "24" + (bn / 60).ToString("D6");
	return buildNumber;
}

private string ExtractBetween(string text, string start, string end) {
	int startIndex = text.IndexOf(start);
	if (startIndex == -1) return "";
	startIndex += start.Length;
	int endIndex = text.IndexOf(end, startIndex);
	if (endIndex == -1) return "";
	return text.Substring(startIndex, endIndex - startIndex);
}

private string GetReleaseNotes(string version) {
	if (!Files.Exists("changelog.md")) return "";
	string content = Files.ReadTextFile("changelog.md");
	string start = "## [" + version + "]";
	int startIndex = content.IndexOf(start);
	if (startIndex == -1) return "";
	int endIndex = content.IndexOf("## [", startIndex + start.Length);
	if (endIndex == -1) endIndex = content.Length;
	return content.Substring(startIndex, endIndex - startIndex).Trim();
}