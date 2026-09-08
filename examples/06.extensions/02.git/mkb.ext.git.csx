/*---------------------------------------------------------------------------------------------------------

	Kombine Git Extension Example

	(C)Kollective Networks 2026

	Test of the git extension (extensions/git.csx), by groups. Every group checks what it needs first
	and skips itself with a visible line when the environment cannot satisfy it, so the rest still runs:

		[1/9] Version and failures       git available
		[2/9] Local repositories         fixture repositories built in a sandbox; the output modes; one clone
		                                 from GitHub with the progress bar, skipped without network
		[3/9] Changes and patches        same fixtures
		[4/9] Commit, tag, push, archive, clean
		[5/9] Worktrees, sparse checkout, bundles
		[6/9] Hooks
		[7/9] Large files                the git lfs command
		[8/9] Authentication inheritance the decisions, offline: hosts under the reserved .invalid domain
		[9/9] Authentication inheritance a private repository, given through the environment:
		                                 kltv_token                a GitHub token for kollective-networks/kltv.kombine.test.repo;
		                                                           the source that authenticates is made here with it
		                                 or, for any other setup:
		                                 KOMBINE_GIT_TEST_SOURCE   folder of a clone that authenticates (the source)
		                                 KOMBINE_GIT_TEST_PRIVATE  URL of a private repository of the same owner
		                                 KOMBINE_GIT_TEST_PUBLIC   URL of a public repository of the same owner (optional)
		                                 KOMBINE_GIT_TEST_OTHER    URL of a repository of another owner (optional)

	Every check prints one aligned line with an OK or FAILED tag; nothing git prints is dumped on
	screen (the extension runs silent here, its progress line is shown once).

---------------------------------------------------------------------------------------------------------*/

#load "../../../extensions/git.csx"

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;

int passed = 0;
int failed = 0;
int skipped = 0;
string Sandbox = Path.GetFullPath(Path.Combine(CurrentScriptFolder, ".tmp.git"));
string Origin = string.Empty;      // bare repository the clones come from
string Work = string.Empty;        // working repository that feeds the bare one
string LibOrigin = string.Empty;   // bare repository used as a submodule
string FirstCommit = string.Empty; // the commit tagged v1.0
string SecondCommit = string.Empty;

/// <summary>
/// Runs every group.
/// </summary>
/// <param name="args"></param>
/// <returns>0 when every check passed, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing the git extension");
	Msg.BeginIndent();
	Git.Output = GitOutput.Silent;
	Git.Timeout = 120000;
	bool ready = TestVersion();
	if (ready && Fixtures()){
		TestRepositories();
		TestChanges();
		TestCommits();
		TestWorktrees();
		TestHooks();
		TestLfs();
	} else if (ready)
		Skip("[2/9] to [7/9]", "the fixture repositories could not be built");
	if (ready)
		TestDecisions();
	TestInheritance();
	int total = passed + failed;
	Msg.PrintTask($"Summary : {passed} of {total} checks passed" + (skipped > 0 ? $", {skipped} groups skipped " : " "));
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");
	Msg.EndIndent();
	Msg.EndIndent();
	// The sandbox is removed once the report is printed; run "clean" to remove one left by an interrupted run
	Nuke(Sandbox);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"git extension tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Removes the sandbox.
/// </summary>
int clean(string[] args){
	Nuke(Sandbox);
	return 0;
}

// ------------------------------------------------------------------------------------------------
// [1/9] Version and failures
// ------------------------------------------------------------------------------------------------

bool TestVersion(){
	Banner("[1/9] Version and failures");
	GitVersion v = Git.Version();
	bool available = v.Major >= 0;
	Check("Version", available ? v.Major + "." + v.Minor + "." + v.Revision : "not available", available ? v.Major + "." + v.Minor + "." + v.Revision : "available");
	if (!available){
		EndBanner();
		Skip("[2/9] to [9/9]", "git is not available");
		return false;
	}
	Check("VersionCheck 2.20", Show(Git.VersionCheck(2, 20, 0)), "true");
	Check("VersionCheck 99.0", Show(Git.VersionCheck(99, 0, 0)), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotSupported");
	// A failure: a folder that is not a repository, reason in LastError, nothing printed
	Check("Info of no repository", Show(Git.Info(Path.GetTempPath())), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotFound");
	Check("error message", Git.LastError.Message.StartsWith("not a git repository") ? "not a git repository..." : Git.LastError.Message, "not a git repository...");
	Check("LastError reset", Git.Version().Major >= 0 && !Git.LastError.IsError ? "none after a success" : "still set", "none after a success");
	EndBanner();
	return true;
}

// ------------------------------------------------------------------------------------------------
// The fixtures: a working repository with two commits, a tag and a branch, pushed to a bare
// repository the tests clone from; a second bare repository used as a submodule.
// ------------------------------------------------------------------------------------------------

bool Fixtures(){
	Nuke(Sandbox);
	Folders.Create(Sandbox);
	Origin = Path.Combine(Sandbox, "origin.git");
	Work = Path.Combine(Sandbox, "work");
	LibOrigin = Path.Combine(Sandbox, "lib.git");
	if (!RawGit(Sandbox, "init", "--bare", "-b", "main", Origin)) return false;
	if (!RawGit(Sandbox, "init", "--bare", "-b", "main", LibOrigin)) return false;
	if (!RawGit(Sandbox, "init", "-b", "main", Work)) return false;
	// The library: one commit, pushed to its bare repository
	string lib = Path.Combine(Sandbox, "lib");
	if (!RawGit(Sandbox, "init", "-b", "main", lib)) return false;
	File.WriteAllText(Path.Combine(lib, "lib.h"), "#define LIB 1\n");
	if (!Git.Add("lib.h", new AddOptions { Path = lib })) return false;
	if (!Git.Commit(lib, "library", Identity())) return false;
	if (!RawGit(lib, "remote", "add", "origin", LibOrigin)) return false;
	if (!Git.Push(lib, new PushOptions { Remote = "origin", Refspec = "main", SetUpstream = true })) return false;
	// The working repository: src/file1.txt tagged v1.0, then a second file, a feature branch, the submodule
	Folders.Create(Path.Combine(Work, "src"));
	File.WriteAllText(Path.Combine(Work, "src", "file1.txt"), "one\n");
	File.WriteAllText(Path.Combine(Work, ".gitignore"), "*.log\nout/\n");
	if (!Git.Add(new string[] { "src/file1.txt", ".gitignore" }, new AddOptions { Path = Work })) return false;
	if (!Git.Commit(Work, "first", Identity())) return false;
	if (!Git.Tag(Work, "v1.0", new TagOptions { Message = "release 1.0" })) return false;
	if (!Git.RevParse(Work, "HEAD")) return false;
	FirstCommit = Git.LastRevParse;
	File.WriteAllText(Path.Combine(Work, "file2.txt"), "two\n");
	if (!Git.Add("file2.txt", new AddOptions { Path = Work })) return false;
	if (!Git.Commit(Work, "second", Identity())) return false;
	if (!Git.RevParse(Work, "HEAD")) return false;
	SecondCommit = Git.LastRevParse;
	if (!RawGit(Work, "branch", "feature")) return false;
	if (!RawGit(Work, "-c", "protocol.file.allow=always", "submodule", "add", LibOrigin, "lib")) return false;
	if (!Git.Commit(Work, "submodule", Identity())) return false;
	if (!RawGit(Work, "remote", "add", "origin", Origin)) return false;
	if (!Git.Push(Work, new PushOptions { Remote = "origin", Refspec = "main", SetUpstream = true })) return false;
	if (!Git.Push(Work, new PushOptions { Remote = "origin", Refspec = "feature" })) return false;
	if (!Git.Push(Work, new PushOptions { Remote = "origin", Tags = true })) return false;
	return true;
}

// ------------------------------------------------------------------------------------------------
// [2/9] Local repositories: clone, info, checkout, fetch, pull in every mode, submodules, status, ls-remote
// ------------------------------------------------------------------------------------------------

void TestRepositories(){
	Banner("[2/9] Local repositories (clone, output modes, info, checkout, fetch, pull, submodules, status, ls-remote)");
	string c1 = Path.Combine(Sandbox, "c1");
	// The progress line is shown once, for the first clone, then the group runs silent
	Git.Output = GitOutput.Progress;
	Check("Clone", Show(Git.Clone(Origin, c1)), "true");
	Git.Output = GitOutput.Silent;
	Check("LastClone.Commit", Short(Git.LastClone.Commit), Short(HeadOf(Work)));
	Check("submodule cloned", Show(File.Exists(Path.Combine(c1, "lib", "lib.h"))), "true");
	// A clone from GitHub with the progress bar of the engine: the local clones above finish at once,
	// this one takes a few seconds and shows the bar moving. Skipped, not failed, without network.
	Msg.Print("A clone from GitHub, the progress bar of the engine shown:");
	Git.Output = GitOutput.Progress;
	Git.Progress = null;
	bool github = Git.Clone("https://github.com/kollective-networks/kltv.kombine.git", Path.Combine(Sandbox, "github"), null, new CloneOptions { Depth = 1, Submodules = SubmoduleMode.None, Timeout = 180000 });
	Git.Output = GitOutput.Silent;
	if (!github && Git.LastError.Code == ErrorCode.NetworkError)
		Skip("clone from GitHub", "no network: " + Git.LastError.Message);
	else {
		Check("Clone from GitHub", Show(github), "true");
		Check("LastClone.Commit", Git.LastClone.Commit.Length == 40 ? "a full hash" : Git.LastClone.Commit, "a full hash");
		Check("repository cloned", Show(File.Exists(Path.Combine(Sandbox, "github", "readme.md"))), "true");
	}
	// The output modes, verified with a reporter that records what the Progress mode renders. The
	// clone goes over a file:// URL so the transfer protocol runs and git reports its percentages.
	RecordingProgress recorder = new RecordingProgress();
	Git.Progress = recorder;
	string fileOrigin = "file:///" + Origin.Replace('\\', '/');
	CloneOptions progressMode = new CloneOptions { Submodules = SubmoduleMode.None, Output = GitOutput.Progress };
	Check("Clone in Progress mode", Show(Git.Clone(fileOrigin, Path.Combine(Sandbox, "m1"), null, progressMode)), "true");
	Check("progress line rendered", recorder.Started + " start, " + recorder.Finished + " finish", "1 start, done finish");
	Check("percentages reported", Show(recorder.Reports > 0), "true");
	Check("LastOutput captured", Show(Git.LastOutput.Contains("Cloning into")), "true");
	recorder.Reset();
	CloneOptions silentMode = new CloneOptions { Submodules = SubmoduleMode.None, Output = GitOutput.Silent };
	Check("Clone in Silent mode", Show(Git.Clone(fileOrigin, Path.Combine(Sandbox, "m2"), null, silentMode)), "true");
	Check("nothing rendered", recorder.Started + " start, " + recorder.Reports + " reports", "0 start, 0 reports");
	Check("LastOutput captured", Show(Git.LastOutput.Contains("Cloning into")), "true");
	recorder.Reset();
	Msg.Print("Detailed mode: the text of git follows, as git prints it");
	Msg.BeginIndent();
	CloneOptions detailedMode = new CloneOptions { Submodules = SubmoduleMode.None, Output = GitOutput.Detailed };
	bool detailed = Git.Clone(fileOrigin, Path.Combine(Sandbox, "m3"), null, detailedMode);
	Msg.EndIndent();
	Check("Clone in Detailed mode", Show(detailed), "true");
	Check("nothing rendered by the reporter", recorder.Started + " start, " + recorder.Reports + " reports", "0 start, 0 reports");
	Check("LastOutput captured", Show(Git.LastOutput.Contains("Cloning into")), "true");
	recorder.Reset();
	// The facility value applies when the call gives none
	Git.Output = GitOutput.Progress;
	Check("Clone with the facility mode", Show(Git.Clone(fileOrigin, Path.Combine(Sandbox, "m4"), null, new CloneOptions { Submodules = SubmoduleMode.None })), "true");
	Check("facility mode applied", recorder.Started + " start, " + recorder.Finished + " finish", "1 start, done finish");
	recorder.Reset();
	Git.Output = GitOutput.Silent;
	Check("a failure in Progress mode", Show(Git.Clone(Path.Combine(Sandbox, "nothing.git"), Path.Combine(Sandbox, "m5"), null, progressMode)), "false");
	Check("line finished as failed", recorder.Finished, "failed");
	Git.Progress = null;
	Check("Clone into non empty", Show(Git.Clone(Origin, c1)), "false");
	Check("error code", Git.LastError.Code.ToString(), "AlreadyExists");
	Check("Clone missing", Show(Git.Clone(Path.Combine(Sandbox, "nothing.git"), Path.Combine(Sandbox, "c0"))), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotFound");
	// Info
	Check("Info", Show(Git.Info(c1)), "true");
	GitInfo i = Git.LastInfo;
	Check("Info.Branch", i.Branch, "main");
	Check("Info.Detached", Show(i.Detached), "false");
	Check("Info.Shallow", Show(i.Shallow), "false");
	Check("Info.Dirty", Show(i.Dirty), "false");
	Check("Info.CommitCount", i.CommitCount.ToString(), "3");
	Check("Info.CommitsSinceTag", i.CommitsSinceTag.ToString(), "2");
	Check("Info.Describe", i.Describe.StartsWith("v1.0-2-g") ? "v1.0-2-g..." : i.Describe, "v1.0-2-g...");
	Check("Info.Subject", i.Subject, "submodule");
	Check("Info.CommitEpoch", i.CommitEpoch > 0 ? "set" : "0", "set");
	Check("Info.TopLevel", Same(i.TopLevel, c1) ? "the clone" : i.TopLevel, "the clone");
	Check("Info.AuthKind", i.AuthKind.ToString(), "None");
	// A tag gives a detached checkout
	string c2 = Path.Combine(Sandbox, "c2");
	Check("Clone of a tag", Show(Git.Clone(Origin, c2, "v1.0", new CloneOptions { Submodules = SubmoduleMode.None })), "true");
	Git.Info(c2);
	Check("Info.Tag", Git.LastInfo.Tag, "v1.0");
	Check("Info.Detached", Show(Git.LastInfo.Detached), "true");
	Check("Info.Describe", Git.LastInfo.Describe.StartsWith("v1.0-0-g") ? "v1.0-0-g..." : Git.LastInfo.Describe, "v1.0-0-g...");
	// A pinned commit: the server refuses the hash (local transport), the fallback fetches everything
	string c3 = Path.Combine(Sandbox, "c3");
	Check("Clone of a commit", Show(Git.Clone(Origin, c3, null, new CloneOptions { Commit = FirstCommit, Submodules = SubmoduleMode.None })), "true");
	Check("LastClone.Commit", Short(Git.LastClone.Commit), Short(FirstCommit));
	Check("file2 absent at v1.0", Show(File.Exists(Path.Combine(c3, "file2.txt"))), "false");
	// Shallow clone
	string c4 = Path.Combine(Sandbox, "c4");
	Check("Shallow clone", Show(Git.Clone("file:///" + Origin.Replace('\\', '/'), c4, null, new CloneOptions { Depth = 1, Submodules = SubmoduleMode.None })), "true");
	Git.Info(c4);
	Check("Info.Shallow", Show(Git.LastInfo.Shallow), "true");
	Check("Fetch unshallow", Show(Git.Fetch(c4, new FetchOptions { Unshallow = true })), "true");
	Git.Info(c4);
	Check("Info.Shallow after", Show(Git.LastInfo.Shallow), "false");
	// Checkout
	Check("Checkout feature", Show(Git.Checkout(c1, "feature")), "true");
	Git.Info(c1);
	Check("Info.Branch", Git.LastInfo.Branch, "feature");
	Check("Checkout tag", Show(Git.Checkout(c1, "v1.0")), "true");
	Git.Info(c1);
	Check("Info.Detached", Show(Git.LastInfo.Detached), "true");
	Check("Checkout main detached", Show(Git.Checkout(c1, "main", new CheckoutOptions { Detach = true })), "true");
	Git.Info(c1);
	Check("Info.Detached", Show(Git.LastInfo.Detached), "true");
	Check("Checkout main", Show(Git.Checkout(c1, "main", new CheckoutOptions { Submodules = true })), "true");
	Check("Checkout missing", Show(Git.Checkout(c1, "nowhere")), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotFound");
	// Pull: nothing new, then a new commit on the remote
	Check("Pull up to date", Show(Git.Pull(c1)), "true");
	Check("LastPull.Status", Git.LastPull.Status.ToString(), "UpToDate");
	File.WriteAllText(Path.Combine(Work, "file3.txt"), "three\n");
	Git.Add("file3.txt", new AddOptions { Path = Work });
	Git.Commit(Work, "third", Identity());
	Git.Push(Work);
	Check("Pull updated", Show(Git.Pull(c1)), "true");
	Check("LastPull.Status", Git.LastPull.Status.ToString(), "Updated");
	Check("LastPull.To", Short(Git.LastPull.To), Short(HeadOf(Work)));
	Check("file3 present", Show(File.Exists(Path.Combine(c1, "file3.txt"))), "true");
	// Diverged: a local commit and a remote one
	File.WriteAllText(Path.Combine(c1, "local.txt"), "local\n");
	Git.Add("local.txt", new AddOptions { Path = c1 });
	Git.Commit(c1, "local", Identity());
	File.WriteAllText(Path.Combine(Work, "file4.txt"), "four\n");
	Git.Add("file4.txt", new AddOptions { Path = Work });
	Git.Commit(Work, "fourth", Identity());
	Git.Push(Work);
	Check("Pull diverged", Show(Git.Pull(c1)), "false");
	Check("LastPull.Status", Git.LastPull.Status.ToString(), "Diverged");
	Check("error code", Git.LastError.Code.ToString(), "Different");
	Check("Pull reset", Show(Git.Pull(c1, new PullOptions { Mode = PullMode.Reset })), "true");
	Check("LastPull.Status", Git.LastPull.Status.ToString(), "Updated");
	Check("local commit gone", Show(File.Exists(Path.Combine(c1, "local.txt"))), "false");
	Check("LastPull.To", Short(Git.LastPull.To), Short(HeadOf(Work)));
	// Dirty tree refused, wrong remote refused
	File.WriteAllText(Path.Combine(c1, "file3.txt"), "changed\n");
	Check("Pull dirty", Show(Git.Pull(c1, new PullOptions { RequireClean = true })), "false");
	Check("LastPull.Status", Git.LastPull.Status.ToString(), "Dirty");
	Git.Checkout(c1, "main", new CheckoutOptions { Force = true });
	Check("Pull wrong remote", Show(Git.Pull(c1, new PullOptions { ExpectedRemote = LibOrigin })), "false");
	Check("error code", Git.LastError.Code.ToString(), "Different");
	Check("Pull right remote", Show(Git.Pull(c1, new PullOptions { ExpectedRemote = Origin })), "true");
	// Submodules
	Check("Submodule", Show(Git.Submodule(c1, new SubmoduleOptions { Sync = true })), "true");
	Check("Status", Show(Git.Status(c1, new StatusOptions { Submodules = true })), "true");
	Check("Status.Clean", Show(Git.LastStatus.Clean), "true");
	Check("Status.Branch", Git.LastStatus.Branch, "main");
	Check("Status.Upstream", Git.LastStatus.Upstream, "origin/main");
	File.WriteAllText(Path.Combine(c1, "file2.txt"), "modified\n");
	File.WriteAllText(Path.Combine(c1, "new.txt"), "new\n");
	Git.Status(c1);
	Check("Status.Modified", string.Join(",", (string[])Git.LastStatus.Modified), "file2.txt");
	Check("Status.Untracked", string.Join(",", (string[])Git.LastStatus.Untracked), "new.txt");
	Check("Status.Clean", Show(Git.LastStatus.Clean), "false");
	Git.Checkout(c1, "main", new CheckoutOptions { Force = true });
	File.Delete(Path.Combine(c1, "new.txt"));
	// LsRemote against the bare repository
	Check("LsRemote", Show(Git.LsRemote(Origin)), "true");
	Check("branches listed", Show(Git.LastLsRemote.Any(e => e.Reference == "refs/heads/main") && Git.LastLsRemote.Any(e => e.Reference == "refs/heads/feature")), "true");
	Check("LsRemote tags", Show(Git.LsRemote(Origin, new LsRemoteOptions { Tags = true, Refs = true })), "true");
	Check("tag listed", string.Join(",", Git.LastLsRemote.Select(e => e.Reference)), "refs/tags/v1.0");
	Check("LsRemote missing", Show(Git.LsRemote(Path.Combine(Sandbox, "nothing.git"))), "false");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [3/9] Changes and patches: diff, patch round trip, ls-files, check-ignore, rev-parse, merge-base
// ------------------------------------------------------------------------------------------------

void TestChanges(){
	Banner("[3/9] Changes and patches (diff, patch, ls-files, check-ignore, rev-parse, merge-base)");
	string c1 = Path.Combine(Sandbox, "c1");
	Check("Diff clean tree", Show(Git.Diff(c1)), "true");
	Check("LastDiff.Changed", Show(Git.LastDiff.Changed), "false");
	File.WriteAllText(Path.Combine(c1, "src", "file1.txt"), "one\npatched\n");
	Check("Diff modified tree", Show(Git.Diff(c1)), "true");
	Check("LastDiff.Files", string.Join(",", (string[])Git.LastDiff.Files), "src/file1.txt");
	Check("LastDiff.Changed", Show(Git.LastDiff.Changed), "true");
	// The patch, to a file and as text
	string patch = Path.Combine(Sandbox, "change.patch");
	Check("Diff to a patch file", Show(Git.Diff(c1, new DiffOptions { Patch = true, Output = patch })), "true");
	Check("patch written", Show(File.Exists(patch) && File.ReadAllText(patch).Contains("+patched")), "true");
	Git.Diff(c1, new DiffOptions { Patch = true });
	Check("patch as text", Show(Git.LastDiff.Patch.Contains("--- a/src/file1.txt")), "true");
	Check("Diff between commits", Show(Git.Diff(c1, new DiffOptions { From = FirstCommit, To = SecondCommit })), "true");
	Check("LastDiff.Files", string.Join(",", (string[])Git.LastDiff.Files), "file2.txt");
	// Round trip: the patch applied on another checkout of the same base
	Git.Checkout(c1, "main", new CheckoutOptions { Force = true });
	string c3 = Path.Combine(Sandbox, "c3");
	Git.Checkout(c3, "main", new CheckoutOptions { Force = true });
	Check("Patch", Show(Git.Patch(patch, c3)), "true");
	Check("LastPatch.Status", Git.LastPatch.Status.ToString(), "Applied");
	Check("patched content", Show(File.ReadAllText(Path.Combine(c3, "src", "file1.txt")).Contains("patched")), "true");
	Check("Patch again", Show(Git.Patch(patch, c3)), "true");
	Check("LastPatch.Status", Git.LastPatch.Status.ToString(), "AlreadyApplied");
	Check("Patch with commit", Show(Git.Patch(patch, c3, new PatchOptions { CheckReverse = false })), "false");
	Check("LastPatch.Status", Git.LastPatch.Status.ToString(), "Failed");
	Check("Patch missing file", Show(Git.Patch(Path.Combine(Sandbox, "none.patch"), c3)), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotFound");
	// A patch applied and committed, outside any repository too
	// Outside any repository: the sandbox itself sits inside the Kombine repository
	string plain = Path.Combine(Path.GetTempPath(), "kombine.plain." + Guid.NewGuid().ToString("N"));
	Folders.Create(Path.Combine(plain, "src"));
	File.WriteAllText(Path.Combine(plain, "src", "file1.txt"), "one\n");
	Check("Patch on a plain tree", Show(Git.Patch(patch, plain)), "true");
	Check("plain patched", Show(File.ReadAllText(Path.Combine(plain, "src", "file1.txt")).Contains("patched")), "true");
	Nuke(plain);
	Git.Checkout(c1, "main", new CheckoutOptions { Force = true });
	Check("Patch and commit", Show(Git.Patch(patch, c1, new PatchOptions { Commit = true, Name = "Kombine", Email = "kombine@example.com" })), "true");
	Git.Info(c1);
	Check("commit made", Git.LastInfo.Subject, "kombine: patch change.patch");
	// Files and ignores
	Check("LsFiles", Show(Git.LsFiles(c1)), "true");
	Check("tracked files", string.Join(",", (string[])LsFilesSorted()), ".gitignore,.gitmodules,file2.txt,file3.txt,file4.txt,lib,src/file1.txt");
	File.WriteAllText(Path.Combine(c1, "build.log"), "log\n");
	File.WriteAllText(Path.Combine(c1, "notes.txt"), "notes\n");
	Check("LsFiles others", Show(Git.LsFiles(c1, new LsFilesOptions { Others = true, ExcludeStandard = true })), "true");
	Check("untracked not ignored", string.Join(",", (string[])Git.LastLsFiles), "notes.txt");
	Check("CheckIgnore build.log", Show(Git.CheckIgnore(c1, "build.log") && Git.LastIgnored), "true");
	Check("CheckIgnore notes.txt", Show(Git.CheckIgnore(c1, "notes.txt") && !Git.LastIgnored), "true");
	Check("LsFiles submodules", Show(Git.LsFiles(c1, new LsFilesOptions { RecurseSubmodules = true })), "true");
	Check("submodule file listed", Show(((string[])Git.LastLsFiles).Contains("lib/lib.h")), "true");
	File.Delete(Path.Combine(c1, "build.log"));
	File.Delete(Path.Combine(c1, "notes.txt"));
	// Intent to add: a new file shows in the diff without being staged
	File.WriteAllText(Path.Combine(c1, "intent.txt"), "intent\n");
	Check("Add intent", Show(Git.Add("intent.txt", new AddOptions { Path = c1, IntentToAdd = true })), "true");
	Git.Diff(c1);
	Check("intent in diff", string.Join(",", (string[])Git.LastDiff.Files), "intent.txt");
	Git.Checkout(c1, "main", new CheckoutOptions { Force = true });
	RawGit(c1, "reset", "--hard");
	File.Delete(Path.Combine(c1, "intent.txt"));
	// Revisions
	Check("RevParse HEAD~1", Show(Git.RevParse(c1, "HEAD~1") && Git.LastRevParse == HeadOf(Work)), "true");
	Check("RevParse short", Show(Git.RevParse(c1, "HEAD", new RevParseOptions { Short = true, Abbrev = 8 }) && Git.LastRevParse.Length == 8), "true");
	Check("RevParse tag", Show(Git.RevParse(c1, "v1.0^{commit}") && Git.LastRevParse == FirstCommit), "true");
	Check("RevParse tree", Show(Git.RevParse(c1, "HEAD~1:src")), "true");
	string tree = Git.LastRevParse;
	Check("tree hash stable", Show(Git.RevParse(c1, FirstCommit + ":src") && Git.LastRevParse == tree), "true");
	Check("RevParse missing", Show(Git.RevParse(c1, "nowhere")), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotFound");
	Check("MergeBase", Show(Git.MergeBase(c1, "main", "feature") && Git.LastMergeBase == SecondCommit), "true");
	Check("ancestor", Show(Git.MergeBase(c1, FirstCommit, "main") && Git.LastMergeBase == FirstCommit), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [4/9] Commit, tag, push, archive, clean
// ------------------------------------------------------------------------------------------------

void TestCommits(){
	Banner("[4/9] Commit, tag, push, archive, clean");
	string c1 = Path.Combine(Sandbox, "c1");
	File.WriteAllText(Path.Combine(c1, "release.txt"), "release\n");
	Check("Add", Show(Git.Add("release.txt", new AddOptions { Path = c1 })), "true");
	Check("Add nothing", Show(Git.Add(new KList(), new AddOptions { Path = c1 })), "false");
	DateTime when = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
	Check("Commit", Show(Git.Commit(c1, "release", new CommitOptions { Name = "Kombine", Email = "kombine@example.com", Date = when, NoVerify = true })), "true");
	Git.Info(c1);
	Check("Info.Author", Git.LastInfo.Author, "Kombine");
	Check("Info.CommitEpoch", Git.LastInfo.CommitEpoch.ToString(), ((DateTimeOffset)when).ToUnixTimeSeconds().ToString());
	Check("Commit nothing", Show(Git.Commit(c1, "nothing", Identity())), "false");
	Check("Commit empty allowed", Show(Git.Commit(c1, "stamp", new CommitOptions { Name = "Kombine", Email = "kombine@example.com", AllowEmpty = true })), "true");
	Check("Tag annotated", Show(Git.Tag(c1, "v1.1", new TagOptions { Message = "release 1.1" })), "true");
	Check("Tag exists", Show(Git.Tag(c1, "v1.1", new TagOptions { Message = "again" })), "false");
	Check("error code", Git.LastError.Code.ToString(), "AlreadyExists");
	Check("Tag moved", Show(Git.Tag(c1, "v1.1", new TagOptions { Message = "again", Force = true })), "true");
	Check("Tag lightweight", Show(Git.Tag(c1, "build-1")), "true");
	Check("Tag deleted", Show(Git.Tag(c1, "build-1", new TagOptions { Delete = true })), "true");
	// Push: the clone is ahead of the bare repository
	Check("Push", Show(Git.Push(c1, new PushOptions { FollowTags = true })), "true");
	Git.LsRemote(Origin, new LsRemoteOptions { Tags = true, Refs = true });
	Check("tag pushed", Show(Git.LastLsRemote.Any(e => e.Reference == "refs/tags/v1.1")), "true");
	Check("Push dry run", Show(Git.Push(c1, new PushOptions { DryRun = true })), "true");
	// A rejected push: the work repository is behind now
	File.WriteAllText(Path.Combine(Work, "file5.txt"), "five\n");
	Git.Add("file5.txt", new AddOptions { Path = Work });
	Git.Commit(Work, "fifth", Identity());
	Check("Push rejected", Show(Git.Push(Work)), "false");
	Check("error code", Git.LastError.Code.ToString(), "Different");
	Check("Pull then push", Show(Git.Pull(Work, new PullOptions { Mode = PullMode.Rebase, Submodules = false }) && Git.Push(Work)), "true");
	// Archives
	string zip = Path.Combine(Sandbox, "out", "c1.zip");
	string tgz = Path.Combine(Sandbox, "out", "c1.tar.gz");
	Check("Archive zip", Show(Git.Archive(c1, zip, new ArchiveOptions { Prefix = "c1-1.1" })), "true");
	Check("zip written", Show(File.Exists(zip) && new FileInfo(zip).Length > 0), "true");
	Check("Archive tar.gz of a tag", Show(Git.Archive(c1, tgz, new ArchiveOptions { Reference = "v1.0" })), "true");
	Check("tar.gz written", Show(File.Exists(tgz) && new FileInfo(tgz).Length > 0), "true");
	Check("Archive bad format", Show(Git.Archive(c1, Path.Combine(Sandbox, "out", "c1.rar"), new ArchiveOptions { Format = "rar" })), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotSupported");
	string sub = Path.Combine(Sandbox, "out", "c1.sub.zip");
	Check("Archive with submodules", Show(Git.Archive(c1, sub, new ArchiveOptions { Submodules = true })), "true");
	Compress.ShowProgress = false;
	Compress.Zip.Decompress(sub, Path.Combine(Sandbox, "out", "c1.sub"));
	Compress.ShowProgress = true;
	Check("submodule file archived", Show(File.Exists(Path.Combine(Sandbox, "out", "c1.sub", "lib", "lib.h"))), "true");
	// Clean: listed first, removed only when asked
	File.WriteAllText(Path.Combine(c1, "scratch.txt"), "scratch\n");
	File.WriteAllText(Path.Combine(c1, "scratch.log"), "log\n");
	Check("Clean dry run", Show(Git.Clean(c1)), "true");
	Check("would remove", string.Join(",", (string[])Git.LastClean), "scratch.txt");
	Check("still there", Show(File.Exists(Path.Combine(c1, "scratch.txt"))), "true");
	Check("Clean ignored too", Show(Git.Clean(c1, new CleanOptions { DryRun = false, Ignored = true })), "true");
	Check("removed", string.Join(",", ((string[])Git.LastClean).OrderBy(f => f)), "scratch.log,scratch.txt");
	Check("gone", Show(File.Exists(Path.Combine(c1, "scratch.txt")) || File.Exists(Path.Combine(c1, "scratch.log"))), "false");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [5/9] Worktrees, sparse checkout, bundles
// ------------------------------------------------------------------------------------------------

void TestWorktrees(){
	Banner("[5/9] Worktrees, sparse checkout, bundles");
	string c1 = Path.Combine(Sandbox, "c1");
	string wt = Path.Combine(Sandbox, "c1.v1.0");
	Check("Worktree list", Show(Git.Worktree(c1)), "true");
	Check("one working folder", Git.LastWorktrees.Count.ToString(), "1");
	Check("Worktree add", Show(Git.Worktree(c1, new WorktreeOptions { Add = wt, Reference = "v1.0" })), "true");
	Check("two working folders", Git.LastWorktrees.Count.ToString(), "2");
	Check("added folder detached", Show(Git.LastWorktrees.Any(w => Same(w.Path, wt) && w.Branch.Length == 0 && w.Commit == FirstCommit)), "true");
	Check("file2 absent there", Show(File.Exists(Path.Combine(wt, "file2.txt"))), "false");
	Check("Worktree remove", Show(Git.Worktree(c1, new WorktreeOptions { Remove = wt, Force = true, Prune = true })), "true");
	Check("one working folder", Git.LastWorktrees.Count.ToString(), "1");
	// Sparse checkout on a fresh clone
	string sp = Path.Combine(Sandbox, "sparse");
	Check("Clone sparse", Show(Git.Clone(Origin, sp, null, new CloneOptions { Sparse = new string[] { "src" }, Submodules = SubmoduleMode.None })), "true");
	Check("src present", Show(File.Exists(Path.Combine(sp, "src", "file1.txt"))), "true");
	Check("root files present", Show(File.Exists(Path.Combine(sp, "file2.txt"))), "true");
	Check("LastSparseCheckout", string.Join(",", (string[])Git.LastSparseCheckout), "src");
	Check("SparseCheckout disable", Show(Git.SparseCheckout(sp, new string[0], new SparseCheckoutOptions { Disable = true })), "true");
	Check("SparseCheckout no folders", Show(Git.SparseCheckout(sp, new string[0])), "false");
	Check("error code", Git.LastError.Code.ToString(), "InvalidArgument");
	// Bundles
	string bundle = Path.Combine(Sandbox, "out", "c1.bundle");
	Check("Bundle create", Show(Git.Bundle(c1, new BundleOptions { Create = bundle })), "true");
	Check("Bundle verify", Show(Git.Bundle(c1, new BundleOptions { Verify = bundle })), "true");
	Check("Bundle nothing", Show(Git.Bundle(c1)), "false");
	string fromBundle = Path.Combine(Sandbox, "frombundle");
	Check("Clone from bundle", Show(Git.Clone(bundle, fromBundle, null, new CloneOptions { Submodules = SubmoduleMode.None })), "true");
	Check("same commit", Short(Git.LastClone.Commit), Short(HeadOf(c1)));
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [6/9] Hooks
// ------------------------------------------------------------------------------------------------

void TestHooks(){
	Banner("[6/9] Hooks");
	string c1 = Path.Combine(Sandbox, "c1");
	string hook = Path.Combine(c1, ".git", "hooks", "pre-commit");
	Check("InstallPreCommit", Show(Git.InstallPreCommit(new string[] { "#!/bin/sh", "mkb format check" }, new HookOptions { Path = c1 })), "true");
	Check("hook written", Show(File.Exists(hook)), "true");
	string text = File.Exists(hook) ? File.ReadAllText(hook) : string.Empty;
	Check("shebang first", Show(text.StartsWith("#!/bin/sh\n")), "true");
	Check("LF endings", Show(!text.Contains("\r")), "true");
	Check("InstallPreCommit again", Show(Git.InstallPreCommit(new string[] { "#!/bin/sh", "mkb format check", "mkb lint" }, new HookOptions { Path = c1 })), "true");
	text = File.Exists(hook) ? File.ReadAllText(hook) : string.Empty;
	Check("no duplicates", text.Split('\n').Count(l => l == "mkb format check").ToString(), "1");
	Check("new line added", Show(text.Contains("mkb lint")), "true");
	Check("InstallPreCommit no lines", Show(Git.InstallPreCommit(new string[0], new HookOptions { Path = c1 })), "false");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [7/9] Large files
// ------------------------------------------------------------------------------------------------

void TestLfs(){
	Banner("[7/9] Large files (git lfs)");
	string c1 = Path.Combine(Sandbox, "c1");
	if (!Git.Lfs(c1)){
		EndBanner();
		Skip("[7/9]", "the git lfs command is not available");
		return;
	}
	Check("Lfs available", Show(Git.LastLfs.Available), "true");
	Check("Lfs version", Git.LastLfs.Version.Length > 0 ? "set" : "empty", "set");
	Check("Lfs install", Show(Git.Lfs(c1, new LfsOptions { Install = true, List = true })), "true");
	Check("no large files", Git.LastLfs.Files.Count().ToString(), "0");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [9/9] Authentication inheritance against a real private repository. Two ways to provide it:
// kltv_token, a GitHub token for kollective-networks/kltv.kombine.test.repo (the source that
// authenticates is then made here, cloning it with the token in the URL), or
// KOMBINE_GIT_TEST_SOURCE and KOMBINE_GIT_TEST_PRIVATE for any other setup. On a machine whose
// global git setup (a credential manager, stored credentials) already opens the private
// repository, the plain clone succeeds and the probe leaves the target plain: the checks derive
// their expectations from that first plain clone.
// ------------------------------------------------------------------------------------------------

void TestInheritance(){
	Banner("[9/9] Authentication inheritance (private repository)");
	string? token = Environment.GetEnvironmentVariable("kltv_token");
	string? source = Environment.GetEnvironmentVariable("KOMBINE_GIT_TEST_SOURCE");
	string? priv = Environment.GetEnvironmentVariable("KOMBINE_GIT_TEST_PRIVATE");
	string? other = Environment.GetEnvironmentVariable("KOMBINE_GIT_TEST_OTHER");
	string? publicSameOwner = Environment.GetEnvironmentVariable("KOMBINE_GIT_TEST_PUBLIC");
	string root = Path.Combine(Sandbox, "inherit");
	Nuke(root);
	Folders.Create(root);
	Git.Inherit = new InheritSettings();
	CloneOptions quick = new CloneOptions { Depth = 1, Submodules = SubmoduleMode.None, Timeout = 180000 };
	if (!string.IsNullOrEmpty(token)){
		// The source that authenticates: the private repository cloned with the token in its URL
		priv = "https://github.com/kollective-networks/kltv.kombine.test.repo.git";
		if (string.IsNullOrEmpty(publicSameOwner))
			publicSameOwner = "https://github.com/kollective-networks/kltv.kombine.git";
		if (string.IsNullOrEmpty(other))
			other = "https://github.com/octocat/Hello-World.git";
		source = Path.Combine(root, "source");
		bool made = Git.Clone("https://x-access-token:" + token + "@github.com/kollective-networks/kltv.kombine.test.repo.git", source, null, quick);
		if (!made && Git.LastError.Code == ErrorCode.NetworkError){
			EndBanner();
			Skip("[9/9]", "no network: " + Git.LastError.Message);
			return;
		}
		Check("source cloned with the token", Show(made), "true");
		if (!made){
			EndBanner();
			return;
		}
		Check("token absent from LastClone.Url", Show(Git.LastClone.Url.Contains(token)), "false");
	} else if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(priv)){
		EndBanner();
		Skip("[9/9]", "not configured: set kltv_token (a GitHub token for kollective-networks/kltv.kombine.test.repo), or KOMBINE_GIT_TEST_SOURCE (folder of a clone that authenticates) and KOMBINE_GIT_TEST_PRIVATE (URL of a private repository of the same owner)");
		return;
	}
	// The source and how it authenticates; an empty repository (no commit yet) still tells
	bool infoOk = Git.Info(source);
	bool empty = !infoOk && Git.LastError.Message.StartsWith("the repository has no commit");
	if (empty)
		Msg.Print($"{"source",-28} : the private repository has no commit yet, push one to it (Pull and the branch listing are skipped)");
	else
		Check("Info of the source", Show(infoOk), "true");
	AuthKind kind = Git.LastInfo.AuthKind;
	Check("Info.AuthKind", kind != AuthKind.None ? "authenticated (" + kind + ")" : "None", "authenticated (" + kind + ")");
	Msg.Print($"{"source",-28} : {kind} as {Git.LastInfo.AuthBase}");
	Check("token absent from Info.RemoteUrl", Show(token != null && Git.LastInfo.RemoteUrl.Contains(token)), "false");
	// Without inheritance: refused where nothing else authenticates, served by the global git setup otherwise
	string plain = Path.Combine(root, "plain");
	bool plainOk = Git.Clone(priv, plain, null, new CloneOptions { Depth = 1, Submodules = SubmoduleMode.None, Timeout = 60000 });
	if (plainOk)
		Msg.Print($"{"plain clone",-28} : succeeded, the global git setup of this machine authenticates by itself; the probe will leave the target plain");
	else {
		Check("Clone private, plain", Show(plainOk), "false");
		Check("error code", Git.LastError.Code == ErrorCode.AccessDenied || Git.LastError.Code == ErrorCode.NotFound ? "AccessDenied or NotFound" : Git.LastError.Code.ToString(), "AccessDenied or NotFound");
	}
	// With inheritance: cloned in any case; the profile applied when the probe finds the target closed
	Git.Inherit.Enabled = true;
	Git.Inherit.Source = source;
	string inherited = Path.Combine(root, "inherited");
	Check("Clone private, inherited", Show(Git.Clone(priv, inherited, null, quick)), "true");
	Check("LastClone.Inherited", Show(Git.LastClone.Inherited), Show(!plainOk));
	Msg.Print($"{"cloned as",-28} : {Git.LastClone.Url}");
	Check("token absent from the URL used", Show(token != null && Git.LastClone.Url.Contains(token)), "false");
	// With the probe off the profile is applied whatever the machine can do by itself
	Git.Inherit.Probe = false;
	string forced = Path.Combine(root, "forced");
	Check("Clone private, no probe", Show(Git.Clone(priv, forced, null, quick)), "true");
	Check("LastClone.Inherited", Show(Git.LastClone.Inherited), "true");
	string config = Path.Combine(forced, ".git", "config");
	string configText = File.Exists(config) ? File.ReadAllText(config) : string.Empty;
	Check("pin written", Show(configText.Contains("[kombine")), kind == AuthKind.Ssh || kind == AuthKind.Helper ? "true" : "false");
	Check("token absent from the child config", Show(token != null && configText.Contains(token)), "false");
	if (!empty){
		Check("Pull inherited", Show(Git.Pull(forced, new PullOptions { Submodules = false, Timeout = 120000 })), "true");
		Check("LsRemote inherited", Show(Git.LsRemote(priv, new LsRemoteOptions { Heads = true, Timeout = 60000 })), "true");
		Check("branches listed", Show(Git.LastLsRemote.Count > 0), "true");
	}
	Git.Inherit.Probe = true;
	// A public repository of the same owner: the probe finds it open and clones it plainly
	if (!string.IsNullOrEmpty(publicSameOwner)){
		Check("Clone public, probed", Show(Git.Clone(publicSameOwner, Path.Combine(root, "public"), null, quick)), "true");
		Check("not inherited, public", Show(Git.LastClone.Inherited), "false");
		Git.Inherit.Probe = false;
		Check("Clone public, no probe", Show(Git.Clone(publicSameOwner, Path.Combine(root, "public2"), null, quick)), "true");
		Check("inherited without the probe", Show(Git.LastClone.Inherited), "true");
		Git.Inherit.Probe = true;
	}
	// A repository of another owner: out of scope, cloned as written
	if (!string.IsNullOrEmpty(other)){
		bool ok = Git.Clone(other, Path.Combine(root, "other"), null, quick);
		Check("Clone other owner", Show(ok || Git.LastError.Code == ErrorCode.AccessDenied || Git.LastError.Code == ErrorCode.NotFound), "true");
		Check("not inherited, other owner", Show(Git.LastClone.Inherited), "false");
	}
	Git.Inherit = new InheritSettings();
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [8/9] Authentication inheritance, the decisions: no network needed. The hosts live under the
// reserved .invalid domain, so every clone attempt fails at once on the name resolution; what is
// checked is the decision taken before git runs: the URL used and whether the profile applied.
// ------------------------------------------------------------------------------------------------

void TestDecisions(){
	Banner("[8/9] Authentication inheritance (decisions, offline)");
	string root = Path.Combine(Sandbox, "decisions");
	Folders.Create(root);
	CloneOptions quick = new CloneOptions { Submodules = SubmoduleMode.None, Timeout = 30000 };
	int n = 0;
	string Dest(){ n++; return Path.Combine(root, "target" + n); }
	// A source that authenticates over ssh
	string ssh = SourceRepo(root, "ssh", "git@git.example.invalid:foo/repo1.git");
	Check("Info of the ssh source", Show(Git.Info(ssh)), "true");
	Check("Info.AuthKind", Git.LastInfo.AuthKind.ToString(), "Ssh");
	Check("Info.AuthBase", Git.LastInfo.AuthBase, "git@git.example.invalid:");
	Git.Inherit.Enabled = true;
	Git.Inherit.Source = ssh;
	Git.Inherit.Probe = false;
	Check("same owner cloned", Show(Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, quick)), "false");
	Check("through the ssh base", Git.LastClone.Url, "git@git.example.invalid:foo/repo2.git");
	Check("LastClone.Inherited", Show(Git.LastClone.Inherited), "true");
	Check("error code", Git.LastError.Code == ErrorCode.NetworkError || Git.LastError.Code == ErrorCode.AccessDenied ? "NetworkError or AccessDenied" : Git.LastError.Code.ToString(), "NetworkError or AccessDenied");
	Check("no .git suffix kept", Show(Git.Clone("https://git.example.invalid/foo/repo2", Dest(), null, quick) || Git.LastClone.Url == "git@git.example.invalid:foo/repo2"), "true");
	Git.Clone("https://git.example.invalid/bar/lib.git", Dest(), null, quick);
	Check("other owner untouched", Git.LastClone.Url, "https://git.example.invalid/bar/lib.git");
	Check("LastClone.Inherited", Show(Git.LastClone.Inherited), "false");
	Git.Clone("https://other.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("other host untouched", Git.LastClone.Url, "https://other.example.invalid/foo/repo2.git");
	Git.Clone("git@git.example.invalid:foo/repo2.git", Dest(), null, quick);
	Check("ssh target as given", Git.LastClone.Url, "git@git.example.invalid:foo/repo2.git");
	Check("not inherited", Show(Git.LastClone.Inherited), "false");
	// The scope
	Git.Inherit.Scope = InheritScope.Host;
	Git.Clone("https://git.example.invalid/bar/lib.git", Dest(), null, quick);
	Check("Scope Host, other owner", Git.LastClone.Url, "git@git.example.invalid:bar/lib.git");
	Git.Clone("https://other.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("Scope Host, other host", Show(Git.LastClone.Inherited), "false");
	Git.Inherit.Scope = InheritScope.Owner;
	Git.Inherit.Include = new string[] { "https://git.example.invalid/bar/lib" };
	Git.Clone("https://git.example.invalid/bar/lib.git", Dest(), null, quick);
	Check("Include, the repository", Show(Git.LastClone.Inherited), "true");
	Git.Clone("https://git.example.invalid/bar/other.git", Dest(), null, quick);
	Check("Include, another one", Show(Git.LastClone.Inherited), "false");
	Git.Inherit.Include = null;
	Git.Inherit.Exclude = new string[] { "git@git.example.invalid:foo/repo3.git" };
	Git.Clone("https://git.example.invalid/foo/repo3.git", Dest(), null, quick);
	Check("Exclude, any form", Show(Git.LastClone.Inherited), "false");
	Git.Inherit.Exclude = null;
	// A call that asks for it while disabled on the facility
	Git.Inherit.Enabled = false;
	Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, new CloneOptions { Submodules = SubmoduleMode.None, Timeout = 30000, Inherit = true });
	Check("Inherit on the call", Show(Git.LastClone.Inherited), "true");
	Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("disabled otherwise", Show(Git.LastClone.Inherited), "false");
	Git.Inherit.Enabled = true;
	// A source that authenticates with a token: the URL stays https, the header travels with the command
	string token = SourceRepo(root, "token", "https://user:secret@git.example.invalid/foo/repo1.git");
	Git.Inherit.Source = token;
	Git.Info(token);
	Check("Info.AuthKind", Git.LastInfo.AuthKind.ToString(), "Token");
	Check("Info.AuthBase masked", Git.LastInfo.AuthBase, "https://***@git.example.invalid/");
	Check("Info.RemoteUrl masked", Git.LastInfo.RemoteUrl, "https://***@git.example.invalid/foo/repo1.git");
	Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("token target as given", Git.LastClone.Url, "https://git.example.invalid/foo/repo2.git");
	Check("LastClone.Inherited", Show(Git.LastClone.Inherited), "true");
	Check("secret not in the error", Show(Git.LastError.Message.Contains("secret")), "false");
	// A GitLab instance: the owner is the top group, subgroups stay in the path
	Git.Inherit.Hosts["lab.example.invalid"] = GitService.GitLab;
	string lab = SourceRepo(root, "lab", "git@lab.example.invalid:grp/repo1.git");
	Git.Inherit.Source = lab;
	Git.Clone("https://lab.example.invalid/grp/sub/repo2.git", Dest(), null, quick);
	Check("GitLab subgroup rewritten", Git.LastClone.Url, "git@lab.example.invalid:grp/sub/repo2.git");
	Git.Clone("https://lab.example.invalid/grp2/repo2.git", Dest(), null, quick);
	Check("GitLab other group", Show(Git.LastClone.Inherited), "false");
	// An Azure DevOps shaped service: two host names, different paths over https and ssh
	HostRule azure = HostRule.Of(GitService.AzureDevOps);
	azure.Hosts = new string[] { "azure.example.invalid", "ssh.azure.example.invalid" };
	Git.Inherit.Hosts["azure.example.invalid"] = azure;
	Git.Inherit.Hosts["ssh.azure.example.invalid"] = azure;
	string az = SourceRepo(root, "azure", "git@ssh.azure.example.invalid:v3/org/proj/repo1");
	Git.Inherit.Source = az;
	Git.Clone("https://azure.example.invalid/org/proj/_git/repo2", Dest(), null, quick);
	Check("Azure shape rewritten", Git.LastClone.Url, "git@ssh.azure.example.invalid:v3/org/proj/repo2");
	Git.Clone("https://azure.example.invalid/other/proj/_git/repo2", Dest(), null, quick);
	Check("Azure other organization", Show(Git.LastClone.Inherited), "false");
	// An ssh source with a port
	string port = SourceRepo(root, "port", "ssh://git@gitea.example.invalid:2222/foo/repo1.git");
	Git.Inherit.Source = port;
	Git.Clone("https://gitea.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("port kept in the base", Git.LastClone.Url, "ssh://git@gitea.example.invalid:2222/foo/repo2.git");
	// A source without anything local: nothing to inherit
	string none = SourceRepo(root, "none", "https://git.example.invalid/foo/repo1.git");
	Git.Inherit.Source = none;
	Git.Info(none);
	Check("Info.AuthKind", Git.LastInfo.AuthKind.ToString(), "None");
	Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("nothing inherited", Show(Git.LastClone.Inherited), "false");
	// No repository around the source: explicit request fails, the facility setting stays quiet
	Git.Inherit.Source = Path.GetTempPath();
	Check("no source, explicit", Show(Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, new CloneOptions { Submodules = SubmoduleMode.None, Timeout = 30000, Inherit = true })), "false");
	Check("error code", Git.LastError.Code.ToString(), "NotFound");
	Git.Clone("https://git.example.invalid/foo/repo2.git", Dest(), null, quick);
	Check("no source, quiet", Show(Git.LastClone.Inherited), "false");
	Git.Inherit = new InheritSettings();
	EndBanner();
}

/// <summary>
/// A repository with one commit and the given origin URL, never fetched: the source of a decision check.
/// </summary>
string SourceRepo(string root, string name, string originUrl){
	string path = Path.Combine(root, "source." + name);
	RawGit(root, "init", "-b", "main", path);
	File.WriteAllText(Path.Combine(path, "readme.txt"), name + "\n");
	Git.Add("readme.txt", new AddOptions { Path = path });
	Git.Commit(path, "source " + name, Identity());
	RawGit(path, "remote", "add", "origin", originUrl);
	return path;
}

// ------------------------------------------------------------------------------------------------
// Helpers
// ------------------------------------------------------------------------------------------------

/// <summary>
/// A progress reporter that records the calls it receives, to verify what the Progress output
/// mode renders and that the other modes render nothing.
/// </summary>
class RecordingProgress : ITaskProgress {
	public int Started = 0;
	public int Reports = 0;
	public string Finished = string.Empty;
	public void Start(string message){ Started++; }
	public void Report(double value, string? status = null){ Reports++; }
	public void Finish(string message = "", ProgressOutcome outcome = ProgressOutcome.Success){ Finished = message; }
	public void Dispose(){ }
	public void Reset(){ Started = 0; Reports = 0; Finished = string.Empty; }
}

/// <summary>
/// The identity the fixture commits use.
/// </summary>
CommitOptions Identity(){
	return new CommitOptions { Name = "Kombine Test", Email = "test@kombine.local" };
}

/// <summary>
/// Runs a raw git command for the fixtures (init, remote add, submodule add): the extension has no
/// verb for them. Nothing printed; false when git fails.
/// </summary>
bool RawGit(string cwd, params string[] args){
	Tool tool = new Tool("git");
	List<string> full = new List<string> { "-C", cwd, "-c", "user.name=Kombine Test", "-c", "user.email=test@kombine.local" };
	full.AddRange(args);
	ToolResult r = tool.CommandSync("git", full.ToArray());
	if (r.ExitCode != 0)
		Msg.PrintError("fixture command failed: git " + string.Join(" ", args) + ": " + string.Join(" ", r.Stderr).Trim());
	return r.ExitCode == 0;
}

/// <summary>
/// The HEAD commit of a repository, through the extension.
/// </summary>
string HeadOf(string path){
	return Git.RevParse(path, "HEAD") ? Git.LastRevParse : string.Empty;
}

/// <summary>
/// The first eight characters of a hash.
/// </summary>
string Short(string hash){
	return hash.Length > 8 ? hash.Substring(0, 8) : hash;
}

/// <summary>
/// True when two paths name the same folder.
/// </summary>
bool Same(string a, string b){
	return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The tracked files of the last LsFiles, sorted.
/// </summary>
KList LsFilesSorted(){
	KList sorted = new KList();
	foreach (string f in ((string[])Git.LastLsFiles).OrderBy(f => f, StringComparer.Ordinal))
		sorted.Add(f);
	return sorted;
}

/// <summary>
/// Removes a folder with its read only files (git objects are read only).
/// </summary>
void Nuke(string path){
	if (!Directory.Exists(path))
		return;
	foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
		File.SetAttributes(file, FileAttributes.Normal);
	Directory.Delete(path, true);
}

string Show(bool value){
	return value ? "true" : "false";
}

void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
}

void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

void Skip(string group, string reason){
	skipped++;
	Msg.PrintTask($"{group,-28} : skipped ");
	Msg.PrintTaskWarning(reason);
	Msg.RawPrint(Environment.NewLine);
}

/// <summary>
/// Compares the actual value against the expected one and prints an aligned line with a colored
/// OK / FAILED tag. On failure the expected value and the last error are printed below.
/// </summary>
void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-28} : {actual,-40} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-28} : {expected}");
		if (Git.LastError.IsError)
			Msg.PrintError($"{"last error",-28} : {Git.LastError}");
	}
}
