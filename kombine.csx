/*---------------------------------------------------------------------------------------------------------

	Kombine Build & Publishing Script

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

//
#load "extensions/github.csx"


// Remember, this is just used for intellisense, nothing else
#r "out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


/// <summary>
/// Default action. Prints the usage help with the available actions.
/// </summary>
/// <param name="args">Action arguments. Not used.</param>
/// <returns>Always zero.</returns>
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

/// <summary>
/// Runs the unit tests by executing the example scripts in child Kombine instances:
/// the "test", "extensions" and "extras" actions from examples/kombine.csx.
/// </summary>
/// <param name="args">Action arguments. Forwarded to the child scripts.</param>
/// <returns>Always zero.</returns>
int test(string[] args){
	Msg.Print("Running tests");
	Kombine("examples/kombine.csx", "test",args);
	Kombine("examples/kombine.csx", "extensions", args);
	Kombine("examples/kombine.csx", "extras", args);
	return 0;
}

/// <summary>
/// Builds the tool for the current system in debug mode.
/// Output is left in out/bin/[rid]/debug/ as configured in the project file.
/// </summary>
/// <param name="args">Action arguments. Not used.</param>
/// <returns>The dotnet build exit code (zero on success).</returns>
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
/// Builds all the different packages for the tool.
/// Generates the version number, builds release binaries for Windows, Linux and MacOS
/// (both unpacked and single file, self-contained) and compresses them into out/pkg/.
/// Run this before the "release" action.
/// </summary>
/// <param name="args">Action arguments. Not used.</param>
/// <returns>Zero on success, otherwise the exit code of the failed build step.</returns>
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
	Msg.Print("Done!");
	return 0;
}

/// <summary>
/// Pushes the release to GitHub: creates (or reuses) the release, uploads the packages
/// from out/pkg/ as assets and publishes it.
/// Requires:
/// - A GitHub token in the environment variable "kltv_token".
/// - The packages and out/pkg/version.txt generated by a previous "publish" action.
/// - The release notes for the version present in changelog.md (see GetReleaseNotes).
/// Aborts the script if any step fails.
/// </summary>
/// <param name="args">Action arguments. Not used.</param>
/// <returns>Zero on success (failures abort the script).</returns>
int release(string[] args) {

	//
	// Get the token from environment variable
	//
	KValue token = KValue.Import("kltv_token","");
	if (token == "") {
		Msg.PrintAndAbort("GitHub token not found in environment variable kltv_token. Please set the token and try again.");
	}
	//
	// Get the version from the version.txt file
	//
	string version = Files.ReadTextFile("out/pkg/version.txt");
	string versionNumber = version.Trim();
	version = "Release-"+version.Trim();
	if (version == "") {
		Msg.PrintAndAbort("Version number not found in version.txt. Did you create the packages?");
	}
	//
	// Get the release notes from the changelog.md file
	//
	string notes = GetReleaseNotes(versionNumber);
	//
	// Create the github instance and configure it
	//
	Github github = new Github();
	github.Repository = "kltv.kombine";
	github.Owner = "kollective-networks";
	github.Token = token.ToString();
	//
	// Create or get the release on GitHub.
	// If the release already exists, it will return the existing release ID, otherwise it will create a new release and return the new release ID.
	//
	string releaseId = github.CreateRelease(version,"Release " + versionNumber,notes,true);
	if (releaseId == "") {
		Msg.PrintAndAbort("Failed to create or get the release on GitHub.");
	}
	// If the release was created successfully, upload the assets
	//
	string[] assets = new string[] {
		"out/pkg/kombine.ref.zip",
		"out/pkg/kombine.debug.win.zip",
		"out/pkg/kombine.win.zip",
		"out/pkg/kombine.debug.lnx.tar.gz",
		"out/pkg/kombine.lnx.tar.gz",
		"out/pkg/kombine.debug.osx.tar.gz",
		"out/pkg/kombine.osx.tar.gz",
	};
	if (github.UploadAssets(releaseId,assets) == false) {
		Msg.PrintAndAbort("Failed to upload assets to GitHub release.");
	}
	if (github.PublishRelease(releaseId) == false) {
		Msg.PrintAndAbort("Failed to publish the GitHub release.");
	}
	return 0;
}

/// <summary>
/// Stamps the version number into the sources before building.
/// Backs up src/version.cs, replaces the [BUILD] placeholder with a generated build number
/// and writes the resulting full version (major.minor.build) to out/pkg/version.txt so the
/// "release" action can pick it up later. Call RestoreVersionNumber() after building to
/// put the placeholder back.
/// </summary>
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

/// <summary>
/// Restores src/version.cs from the backup created by ApplyVersionNumber(),
/// leaving the [BUILD] placeholder in place for the next publish.
/// </summary>
private void RestoreVersionNumber(){
	Files.Delete("src/version.cs");
	Files.Move("src/version.cs.bak","src/version.cs");
}

/// <summary>
/// Generates a time-based build number: the year prefix followed by the number of
/// minutes elapsed since the start of the year, zero-padded to six digits
/// (e.g. "24072788"). Monotonically increasing within the year.
/// </summary>
/// <returns>The generated build number.</returns>
private string GetVersionBuildNumber() {
	DateTime currentTime = DateTime.UtcNow;
	long now = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
	DateTime currentYear = new DateTime(DateTime.Now.Year, 1, 1);
	long year = ((DateTimeOffset)currentYear).ToUnixTimeSeconds();
	// Minutes since the start of the year, prefixed with the year identifier
	long bn = now - year;
	string buildNumber = "24" + (bn / 60).ToString("D6");
	return buildNumber;
}

/// <summary>
/// Returns the text found between the first occurrence of the given start and end markers.
/// </summary>
/// <param name="text">Text to search in.</param>
/// <param name="start">Marker that precedes the wanted fragment.</param>
/// <param name="end">Marker that follows the wanted fragment.</param>
/// <returns>The fragment between both markers, or an empty string if either is not found.</returns>
private string ExtractBetween(string text, string start, string end) {
	int startIndex = text.IndexOf(start);
	if (startIndex == -1) return "";
	startIndex += start.Length;
	int endIndex = text.IndexOf(end, startIndex);
	if (endIndex == -1) return "";
	return text.Substring(startIndex, endIndex - startIndex);
}

/// <summary>
/// Extracts the release notes for the given version from changelog.md.
///
/// How to prepare changelog.md so the notes are published by the release action:
/// 1. Run "mkb publish" first; it generates the version number and stores it in out/pkg/version.txt.
/// 2. Add a new section at the top of changelog.md with a header matching that exact version:
///        ## [major.minor.build]        (e.g. ## [1.4.24072788])
/// 3. Below the header, list the notes as bullet lines, one per change:
///        - [Feature] Added something new
///        - [Bugfix] Fixed something broken
/// 4. Run "mkb release"; everything between the version header and the next "## [" header
///    (or the end of the file) is used as the body of the GitHub release.
///
/// If the file is missing or no header matches the version, an empty string is returned
/// and the release is created without notes.
/// </summary>
/// <param name="version">Version number to look for (as stored in out/pkg/version.txt).</param>
/// <returns>The release notes for that version, or an empty string if not found.</returns>
private string GetReleaseNotes(string version) {
	if (!Files.Exists("changelog.md")) return "";
	string content = Files.ReadTextFile("changelog.md");
	string start = "## [" + version + "]";
	int startIndex = content.IndexOf(start);
	if (startIndex == -1) return "";
	// Skip the header line
	int headerEnd = content.IndexOf('\n', startIndex);
	if (headerEnd == -1) return "";
	startIndex = headerEnd + 1;
	int endIndex = content.IndexOf("## [", startIndex);
	if (endIndex == -1) endIndex = content.Length;
	return content.Substring(startIndex, endIndex - startIndex).Trim();
}