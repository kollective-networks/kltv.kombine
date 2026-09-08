[Back to the extensions index](../extensions.md)

# git.csx

Static helpers around the `git` command line tool: version checks, clone, pull,
staging and pre-commit hooks. Used by the sdl2 extra example to clone the repository
([examples/07.extras/00.sdl2](../../examples/07.extras/00.sdl2/)).

```csharp
#load "extensions/git.csx"
```

| Member | Description |
| --- | --- |
| `static GitVersion Version()` | Returns the installed git version (major/minor/revision, `-1` if git is not available). |
| `static bool VersionCheck(int Major, int Minor, int Revision)` | Returns true if the installed git is at least the given version. |
| `static bool Clone(string uri, string path, string? branch = null, bool showdettach = false)` | Clones a repository with its submodules, optionally a specific branch or tag. Credentials come from the git configuration. Returns false if git reports an error. |
| `static bool Pull(string path)` | Pulls changes in the given repository path, including submodules. Returns false if git reports an error. |
| `static bool Patch(string patchFile, string patchDir)` | **Not implemented yet**: prints an error and returns false. |
| `static void Status(string path)` | Runs `git status` in the given path. Placeholder, it does not report anything yet. |
| `static KList GetModifiedFiles(string[] paths, string[] extensions, bool staged = true)` | Lists the modified files of the staging area (or of the working tree with `staged` false), filtered by path prefixes and extensions; empty arrays mean no filter. |
| `static void Add(KList files)` | Adds files to the staging area. |
| `static void InstallPreCommit(string[] requiredLines)` | Installs a pre-commit hook in the repository of the current folder containing the given lines (the first one being the shebang), adding the missing lines to an existing hook. |

```csharp
#load "extensions/git.csx"

int fetch(string[] args) {
	if (!Git.VersionCheck(2, 30, 0))
		Msg.PrintAndAbort("git 2.30 or newer is required");
	if (!Folders.Exists("ext/sdl"))
		return Git.Clone("https://github.com/libsdl-org/SDL.git", "ext/sdl", "release-2.30.x") ? 0 : 1;
	return Git.Pull("ext/sdl") ? 0 : 1;
}
```

[Back to the extensions index](../extensions.md)
