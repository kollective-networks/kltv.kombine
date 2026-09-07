# Kombine Extensions

Extensions are regular Kombine scripts located in the [extensions/](../extensions/) folder.
They encapsulate reusable functionality (a tool wrapper, a code generator, an API client)
so it can be shared between projects.

## Loading an extension

Include an extension in your script with the `#load` directive, before any regular
statement:

```csharp
#load "extensions/clang.csx"
```

The path is resolved by searching, in order: the current working directory, the script
directory, the subfolders of the script directory, the parent directories up to the
root, and the tool directory. This means `extensions/clang.csx` works from anywhere in
the repository. An URL can be used as well (the file is fetched and cached):

```csharp
#load "https://raw.githubusercontent.com/kollective-networks/kltv.kombine/main/extensions/clang.csx"
```

## Provided extensions

| Extension | Description |
| --- | --- |
| [clang.csx](#clangcsx) | Clang compiler wrapper: compile, link, static libraries, clang-format, compile_commands.json. |
| [clang.doc.csx](#clangdoccsx) | Generates Markdown documentation from clang-doc YAML output. |
| [git.csx](#gitcsx) | Git operations: clone, pull, patch, status, hooks. |
| [github.csx](#githubcsx) | GitHub releases: create, upload assets, publish. |
| [bin2cpp.csx](#bin2cppcsx) | Embeds binary files into C++ sources as byte arrays. |
| [bin2obj.csx](#bin2objcsx) | Embeds binary files into COFF object files as sections. |
| [modder.csx](#moddercsx) | Applies (and verifies) modifications over downloaded third-party sources. |
| [dotnet.doc.csx](#dotnetdoccsx) | Converts a C# XML documentation file into Markdown. |

---

## clang.csx

Wraps the clang toolchain (`clang`, `clang++`, `llvm-ar`, `llvm-rc`, `clang-format`).
Demonstrated in [examples/06.extensions/00.clang](../examples/06.extensions/00.clang/)
(static library, shared library and executable).

### Clang.ClangOptions

Holds the toolchain configuration. The constructor selects the platform artifact
extensions automatically (`.lib/.dll/.exe/.obj` on Windows, `.a/.so/.out/.o` on Linux,
`.a/.dylib/.out/.o` on macOS) and sets `ConcurrentBuild` to the number of CPU cores.

| Member | Description |
| --- | --- |
| `CC`, `CXX`, `LD`, `AR`, `RC` | Tool executable names (`clang`, `clang++`, `clang++`, `llvm-ar`, `llvm-rc`). |
| `CExtension`, `CppExtension`, `ResExtension` | Source extensions (`.c`, `.cpp`, `.rc`). |
| `IncludeDirs`, `Defines` | Include directories and preprocessor defines. |
| `SwitchesCC`, `SwitchesCXX`, `SwitchesLD` | Extra switches for the C compiler, C++ compiler and linker. |
| `LibraryDirs`, `Libraries` | Library search paths and libraries to link. |
| `ConcurrentBuild` | Number of concurrent compile commands (default: CPU cores). |
| `Verbose` | Verbose tool output. |
| `LibExtension`, `SharedExtension`, `BinaryExtension`, `ObjectExtension` | Platform artifact extensions (read only). |
| `void SetAsDefault()` | Publishes these options as the shared defaults (`Share.Set("ClangOptions", ...)`), so child scripts creating a `Clang` instance inherit them. |

### Clang

The constructor picks up the shared `ClangOptions` and the shared compile commands
database if a parent script published them.

| Member | Description |
| --- | --- |
| `ClangOptions Options` | The options used by this instance. |
| `ProcessFileDelegate? ProcessFile` | Optional callback invoked per file about to be compiled; returns extra arguments for that file. |
| `ToolResult Compile(KList src, KList obj, bool abortwhenfailed = true, bool rebuild = false)` | Compiles the sources into the corresponding objects (both lists must match). Only modified files are compiled unless `rebuild` is true. Compilation runs concurrently up to `ConcurrentBuild`. With `abortwhenfailed` false, a failed file does not stop the remaining ones, so all the errors are reported at once. |
| `ToolResult Librarian(KList objs, KValue output, bool abortwhenfailed = true)` | Creates a static library from the objects. |
| `ToolResult Linker(KList objs, KValue output, bool SharedLibrary = false, bool abortwhenfailed = true)` | Links the objects into an executable, or a shared library with `SharedLibrary`. |
| `void Clean(KList obj, KValue output)` | Deletes the object folders and the output artifact folder. |
| `static bool Format(KList src, string extraArgs, bool abortwhenfailed = true)` | Formats the given files in place with `clang-format`. |
| `bool OpenCompileCommands(KValue file)` | Opens (or creates) a `compile_commands.json` database and shares it, so every `Clang` instance in this script and its children appends to the same file. |

```csharp
#load "extensions/clang.csx"

int build(string[] args) {
	Clang clang = new Clang();
	clang.Options.IncludeDirs += "inc/";
	clang.Options.SwitchesCXX += "-O2";
	KList src = Glob("src/*.cpp");
	KList obj = src.WithExtension(clang.Options.ObjectExtension).WithPrefix("out/tmp/");
	clang.Compile(src, obj);
	clang.Linker(obj, "out/bin/myapp" + clang.Options.BinaryExtension);
	return 0;
}
```

---

## clang.doc.csx

Generates Markdown documentation from the YAML files produced by `clang-doc`.
Demonstrated in [examples/06.extensions/01.clang.docs](../examples/06.extensions/01.clang.docs/).

Configuration is done through static fields of the `ClangDocs` class before calling
`Generate()`:

| Member | Description |
| --- | --- |
| `SourceFiles` | Sources to document. |
| `IncludePaths`, `ExtraArguments` | Include paths and extra arguments for clang-doc. |
| `OutputFolder` | Destination folder for the generated Markdown files. |
| `TemporalFolder` | Working folder for the intermediate YAML files. |
| `Paths`, `Namespaces` | Filters: only symbols from these paths / namespaces are documented. |
| `OnlyPublic` | Document only public symbols (default true). |
| `ProjectName`, `HeaderText`, `FooterText`, `ImagePath` | Output customization: project name, header/footer text and logo image. |
| `static bool Generate()` | Runs clang-doc and produces the Markdown documentation tree. |

---

## git.csx

Static helpers around the `git` command line tool. Used by the sdl2 extra example to
clone the repository ([examples/07.extras/00.sdl2](../examples/07.extras/00.sdl2/)).

| Member | Description |
| --- | --- |
| `static GitVersion Version()` | Returns the installed git version (major/minor/revision, `-1` if git is not available). |
| `static bool VersionCheck(int Major, int Minor, int Revision)` | Returns true if the installed git is at least the given version. |
| `static bool Clone(string uri, string path, string? branch = null, bool showdettach = false)` | Clones a repository, optionally a specific branch/tag. Returns false if git reports an error. |
| `static bool Pull(string path)` | Pulls changes in the given repository path. Returns false if git reports an error. |
| `static bool Patch(string patchFile, string patchDir)` | **Not implemented yet**: prints an error and returns false. |
| `static void Status(string path)` | Prints the repository status. |
| `static void Add(KList files)` | Adds files to the staging area. |
| `static void InstallPreCommit(string[] requiredLines)` | Installs a pre-commit hook containing the given lines. |

---

## github.csx

Client for the GitHub releases API. This repository's own [kombine.csx](../kombine.csx)
uses it in the `release` action to publish the tool packages.

Set `Owner`, `Repository` and `Token` once on the instance; every call uses them.

| Member | Description |
| --- | --- |
| `string Owner`, `string Repository` | Repository coordinates (e.g. `kollective-networks` / `kltv.kombine`). |
| `string Token` | GitHub API token with permissions to manage releases. |
| `string GetReleaseID(string tag)` | Returns the release id for a tag, or empty if it does not exist. |
| `string CreateRelease(string tag, string name, string notes, bool draft = true)` | Creates a release (draft by default) and returns its id. If the release already exists, the existing id is returned. |
| `bool UploadAssets(string releaseId, string[] filePaths)` | Uploads the given files as release assets. |
| `bool PublishRelease(string releaseId)` | Publishes the (draft) release. |

```csharp
#load "extensions/github.csx"

Github github = new Github();
github.Owner = "myorg";
github.Repository = "myrepo";
github.Token = KValue.Import("github_token").ToString();
string id = github.CreateRelease("v1.0", "Release 1.0", "Notes here", true);
github.UploadAssets(id, new string[] { "out/pkg/app.zip" });
github.PublishRelease(id);
```

---

## bin2cpp.csx

Generates `.cpp` files embedding the content of binary files as byte arrays, so assets
can be compiled into a C++ project. Only regenerates outputs that are missing or older
than their input. Demonstrated in
[examples/06.extensions/04.bin2cpp](../examples/06.extensions/04.bin2cpp/).

| Member | Description |
| --- | --- |
| `bool Generate(KList bin, KList cpp)` | Generates one `.cpp` per input binary (both lists must match in size). |
| `bool Generate(KList bin, KValue cpp)` | Generates a single `.cpp` containing all the input binaries. |
| `Dictionary<string, string> Symbols` | Symbols generated in the last `Generate` call (symbol name → variable name), useful to emit a header declaring them. |

---

## bin2obj.csx

Generates COFF object files embedding binary files as data sections, so assets can be
linked directly without an intermediate C++ file. Only regenerates outputs that are
missing or older than their input. Demonstrated in
[examples/06.extensions/05.bin2obj](../examples/06.extensions/05.bin2obj/).

| Member | Description |
| --- | --- |
| `string Machine` | Target machine for the COFF file (default `x64`; reserved for future use). |
| `bool Generate(KList bin, KList obj)` | Generates one object file per input binary (both lists must match in size). |
| `bool Generate(KList bin, KValue obj)` | Generates a single object file containing all the input binaries. |
| `Dictionary<string, string> Symbols` | Symbols generated in the last `Generate` call (friendly name → symbol name). |

---

## modder.csx

Applies a set of modified files over a downloaded third-party project, verifying first
that the project files still match the originals the mod was created against (so an
upstream update that invalidates the mod is detected instead of silently clobbered).

It works with three folders, mirroring the same structure:

- `Prj` — the downloaded project to be modified.
- `Org` — copies of the original files, as they were before modification.
- `Mod` — the modified files to apply.

| Member | Description |
| --- | --- |
| `string Prj`, `string Org`, `string Mod` | The three folders described above. |
| `bool VerifyMod()` | Verifies the mod can be applied: every original file must still exist in the project with matching content. Call before `ApplyMod`. |
| `bool ApplyMod()` | Copies the modified files over the project. |
| `bool RemoveMod()` | Restores the original files in the project. |

---

## dotnet.doc.csx

Converts a C# XML documentation file (the one produced by the compiler with
`GenerateDocumentationFile`) into a Markdown file. A temporary utility until a proper
documentation generator is in place.

| Member | Description |
| --- | --- |
| `static void Convert(string InputFile, string OutputFile)` | Reads the XML documentation file and writes the Markdown conversion. |

```csharp
#load "extensions/dotnet.doc.csx"

XmlToMarkdown.Convert("out/bin/win-x64/release/mytool.xml", "doc/api.generated.md");
```

---

## Contributing

These extensions are not exhaustive — they cover what the examples and this repository
need. If you create an extension that could be useful for others, please share it;
everything is welcome.
