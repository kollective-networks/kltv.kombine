# Kombine Script API Reference

This document describes the public API available inside Kombine scripts (`kombine.csx`).
Only the classes and methods intended to be used from scripts are listed here; engine
internals are not part of the script API.

## Table of Contents

- [Script Basics](#script-basics)
- [Global Functions (Statics)](#global-functions-statics)
- [Types](#types)
  - [KValue](#kvalue)
  - [KList](#klist)
- [Action Arguments (Args / EntryArgs)](#action-arguments-args--entryargs)
- [Logging (Msg)](#logging-msg)
- [Files](#files)
- [Folders](#folders)
- [Compress](#compress)
  - [Compress.Zip](#compresszip)
  - [Compress.Tar](#compresstar)
- [Http](#http)
- [JSON (JsonFile / JsonExtensions)](#json-jsonfile--jsonextensions)
- [Yaml](#yaml)
- [Share](#share)
- [Tool](#tool)
- [Host](#host)
- [ProgressBar](#progressbar)

---

## Script Basics

A Kombine script is a C# script (`.csx`) executed by `mkb`. Each **action** is a plain
function that receives the action arguments and returns an exit code:

```csharp
int build(string[] args) {
	Msg.Print("Building...");
	return 0;
}
```

Invoke it with `mkb build [action parameters]`. The action names `khelp`, `kversion`,
`kconfig` and `kcache` are reserved by the tool.

Code at the top level of the script (variable definitions, prints, etc.) runs when the
script is evaluated, before the requested action is invoked.

Other scripts and extensions can be included with `#load`, either from a local path or
from an URL (remote scripts are cached):

```csharp
#load "clang.csx"
#load "https://raw.githubusercontent.com/kollective-networks/kltv.kombine/main/extensions/clang.csx"
```

### Default usings

The following namespaces are imported automatically, so everything in this document can
be used without `using` directives:

`System`, `System.IO`, `System.Text`, `System.Text.Json`, `System.Text.Json.Nodes`,
`Kltv.Kombine.Types`, `Kltv.Kombine.Api`, `Kltv.Kombine.Api.Tool`,
`Kltv.Kombine.Api.Statics`, `Kltv.Kombine.Api.Compress.Tar`.

### Injected properties

These read-only properties are injected into every script and can be used directly:

| Property | Description |
| --- | --- |
| `CurrentWorkingFolder` | Current working folder (same as `Folders.CurrentWorkingFolder`). |
| `CurrentScriptFolder` | Folder where the running script lives (same as `Folders.CurrentScriptFolder`). |
| `CurrentToolFolder` | Folder where the `mkb` binary lives (same as `Folders.CurrentToolFolder`). |
| `ParentScriptFolder` | Folder of the parent script, or empty if none (same as `Folders.ParentScriptFolder`). |

### Useful tool parameters

`mkb [parameters] [action] [action parameters]`

| Parameter | Description |
| --- | --- |
| `-ksdbg` | Build the script with debug information (script debugging). |
| `-ksdbgw` | As `-ksdbg`, but wait for a debugger to attach before executing the action. |
| `-ksrb` / `-ksrebuild` | Rebuild the script even if a compiled version is cached. |
| `-ko:s` / `-ko:n` / `-ko:v` / `-ko:d` | Output level: silent, normal, verbose, debug. |
| `-kfile:<name>` | Script file to execute (default `kombine.csx`). |

---

## Global Functions (Statics)

`Kltv.Kombine.Api.Statics` — static methods published directly into the script; call
them without any class prefix.

| Method | Description |
| --- | --- |
| `KList Glob(string pattern)` | Resolves a glob pattern to a list of files, using the current working directory as base path. |
| `KList Glob(string folder, string pattern)` | Resolves a glob pattern using the given folder as base path. |
| `KValue RealPath(KValue path)` | Returns the absolute path for the given path, converted to the underlying OS conventions. Empty on error. |
| `int Kombine(string script, string action, string[]? args = null, bool exitonerror = true, bool changedir = true, bool search = true)` | Executes a child Kombine script **in the same process** and returns its exit code. If `search` is true the script is looked up through the resolution paths; if `changedir` is true the working directory is switched to the script folder while it runs; if `exitonerror` is true a non-zero result aborts the calling script. |
| `int Exec(string command, string[]? args = null, bool showoutput = false)` | Executes a command line tool and returns its exit code. Arguments containing spaces are quoted automatically. `showoutput` echoes the tool output to the console. |
| `int Exec(string command, string? args = null, bool showoutput = false)` | Same as above taking the arguments as a single string. |
| `int Shell(string command, string[]? args = null)` | Executes a command through the system shell and returns its exit code. |
| `int Shell(string command, string? args = null)` | Same as above taking the arguments as a single string. |
| `string ArgEscape(string arg)` | Escapes an argument for command line processing (currently escapes double quotes). |
| `T? Cast<T>(object? myobj)` | Casts an object to another type copying as many matching properties as possible. Returns null if invalid. Useful with [`Share.Get`](#share). |
| `string MkbVersion()` | Kombine version as `Major.Minor.Build`. |
| `string MkbVersionShort()` | Kombine version as `Major.Minor`. |
| `int MkbMajorVersion()` | Major version number. |
| `int MkbMinorVersion()` | Minor version number. |
| `int MkbHexVersion()` | Major+minor version encoded as a hex value. |
| `string GenVersionBuildNumber()` | Generates a time-based build number: a two digit year prefix followed by the minutes elapsed since January 1st (UTC), zero padded to six digits (e.g. `24072788`). |

```csharp
// Compile every .cpp under src/ and stop on failure
KList sources = Glob("src/**/*.cpp");
foreach (KValue src in sources) {
	if (Exec("clang++", "-c " + src, true) != 0)
		Msg.PrintAndAbort("Compile failed: " + src);
}

// Run the test action of another script
Kombine("examples/kombine.csx", "test", args);
```

---

## Types

### KValue

`Kltv.Kombine.Types.KValue` — the Kombine string value. It converts implicitly to and
from `string`, so it can be used anywhere a string is expected. Most helpers treat the
value as a path or a fragment of a command line.

| Member | Description |
| --- | --- |
| `KValue()` | Creates an empty value. |
| `void Export(string name)` | Exports the value to the script environment under the given name. The environment is inherited by executed tools and child scripts. |
| `static KValue Import(string name, string? defvalue = null)` | Imports a value from the script environment (initialized from the system environment). If the variable is missing and no default value is given, **the script is aborted**. |
| `KList ToArray()` | Splits the value by spaces into a list. |
| `KList ToArray(KValue separator)` | Splits the value by the given separator. |
| `KList ToArray(string[] separators)` | Splits the value by any of the given separators. |
| `KList ToArgs()` | Splits the value into command line arguments: spaces separate, quoted fragments stay as one argument. |
| `KValue WithExtension(string n)` | Treats the value as a file and returns it with the extension changed. Empty if it is not a file. |
| `bool HasExtension(string ext)` | Returns true if the value ends with the given extension. |
| `bool IsEmpty()` | Returns true if the value is empty. |
| `KValue AsFolder()` | Returns the folder part of the value (removes the filename). |
| `KValue GetParent()` | Returns the parent folder if the value can be considered a path; empty otherwise. |
| `KValue WithNamePrefix(string prefix)` | Returns the value with the filename part prefixed (e.g. `lib` + `foo.a` → `libfoo.a`). |
| `KValue ReduceWhitespace()` | Collapses consecutive whitespace into a single space. |
| `string ToString()` | The represented string. |
| `int GetHashCode()` | Runtime hash (not stable between runs). |
| `UInt64 GetHashCode64()` | Stable content hash: the same string always produces the same value. |

**Operators:** `==` / `!=` compare contents (not references). `+` concatenates.
`-` removes every occurrence of the right value from the left one. Implicit conversion
to and from `string`.

```csharp
KValue token = KValue.Import("api_token", "");   // empty default: never aborts
KValue objs  = "out/obj/main.cpp";
KValue obj   = objs.WithExtension(".o");         // out/obj/main.o
KValue dir   = objs.AsFolder();                  // out/obj
```

### KList

`Kltv.Kombine.Types.KList` — a list of `KValue` with helpers for build scripts. It is
enumerable (`foreach`), indexable and converts implicitly from `string`, `KValue` and
`string[]`, and to `KValue`, `string` and `string[]` (flattening when needed). It also
supports collection initializers: `KList src = new() { "item1", "item2" };`.

| Member | Description |
| --- | --- |
| `KList()` / `KList(KList other)` | Default and copy constructors. |
| `KValue Flatten()` | Flattens the list into a single space-separated string. |
| `KValue Flatten(string separator)` | Flattens the list using the given separator. |
| `int Count()` | Number of elements. |
| `bool HasDuplicates()` | Returns true if the list contains duplicated values. |
| `KList RemoveDuplicates()` | Returns a new list without duplicates. |
| `KList WithExtension(string n)` | Returns a new list with every value treated as a file and its extension changed. Values that become empty are discarded. |
| `KList WithReplace(string ov, string nv)` | Returns a new list replacing `ov` with `nv` in every element. Elements that become empty are discarded. |
| `KList WithPrefix(KValue n)` | Returns a new list with every value prefixed with `n`. |
| `KList AsFolders()` | Returns a new list with only the folder part of every value, without duplicates. |
| `bool Compare(KList other)` | Returns true if both lists have the same elements in the same order. |
| `bool Contains(KValue v)` | Returns true if the value is in the list. |
| `void Add(KValue v)` / `Add(string v)` / `Add(KList v)` | Appends a value or a whole list. |
| `void Remove(KValue v)` / `Remove(string v)` / `Remove(KList v)` | Removes a value or every value of another list. |
| `this[int index]` | Read-only indexer. |

**Operators:** `+` appends a `KList`, `KValue` or `string`; `-` removes one; `==` / `!=`
compare contents. `+` and `-` return a **new** list — the operands are never modified,
so `KList c = a + b;` leaves `a` untouched (and `a += b;` rebinds `a` to the new list).

```csharp
KList sources = Glob("src/*.cpp");
KList objects = sources.WithExtension(".o").WithPrefix("out/obj/");
KValue cmd    = "clang++ -o out/bin/app " + objects.Flatten();
```

---

## Action Arguments (Args / EntryArgs)

`Kltv.Kombine.Api.Args` gives access to the action arguments of the **current** script;
`Kltv.Kombine.Api.EntryArgs` gives access to the arguments of the **starting** script
(useful inside child scripts). Both are static classes.

| Member | Description |
| --- | --- |
| `bool Args.WasRebuilded` | True if the script (or its parent) was rebuilt in this run. Useful to invalidate caches derived from the script. |
| `bool Contains(string arg)` | Returns true if the given value is present in the action arguments. |
| `string Get(int index)` | Returns the argument at the given index, or empty if out of bounds. |

```csharp
int build(string[] args) {
	bool verbose = Args.Contains("verbose");
	string config = Args.Get(0);   // first action parameter or ""
	return 0;
}
```

---

## Logging (Msg)

`Kltv.Kombine.Api.Msg` — console output for scripts. All print methods accept an
optional `Msg.LogLevels` argument (default `Normal`); the message is only shown when the
current output level (see `-ko:` parameters) is equal or higher.

**`Msg.LogLevels`:** `Silent`, `Normal`, `Verbose`, `Debug`, `Undefined`.

| Method | Description |
| --- | --- |
| `void Print(string Message, LogLevels Level = Normal)` | Prints a message. |
| `void PrintWarning(string Message, LogLevels Level = Normal)` | Prints a warning (yellow). |
| `void PrintError(string Message, LogLevels Level = Normal)` | Prints an error (red). |
| `void PrintAndAbort(string Message = "", LogLevels Level = Normal)` | Prints an error and **aborts the script execution**. |
| `void RawPrint(string Message, LogLevels Level = Normal)` | Prints without colors, indentation or trailing newline. |
| `void PrintTask(string Message, LogLevels Level = Normal)` | Prints a task message **without a newline**, so it can be completed with one of the methods below. |
| `void PrintTaskSuccess(string Message = "", LogLevels Level = Normal)` | Completes a task line in green. Default message: `Done`. |
| `void PrintTaskWarning(string Message = "", LogLevels Level = Normal)` | Completes a task line in yellow. Default message: `Done`. |
| `void PrintTaskError(string Message = "", LogLevels Level = Normal)` | Completes a task line in red. Default message: `Failed`. |
| `void BeginIndent(bool bSkipNotUsed = false)` | Adds one level of indentation to the log output. With `bSkipNotUsed` the level is only added if the current one was used. |
| `void EndIndent()` | Removes one level of indentation. |
| `void Lock()` / `void UnLock()` | Locks/unlocks the log output. Use to keep output together when printing from concurrent callbacks. |

```csharp
Msg.PrintTask("Compiling module... ");
if (Exec("clang++", flags) == 0)
	Msg.PrintTaskSuccess();
else
	Msg.PrintTaskError();
```

---

## Files

`Kltv.Kombine.Api.Files` — file operations. Methods take `KValue` filenames, so plain
strings work as well. Failures are reported in the return value (and logged in verbose
mode) instead of throwing.

| Method | Description |
| --- | --- |
| `bool Exists(KValue Filename)` | Returns true if the file exists. |
| `KValue ReadTextFile(KValue Filename, bool ExitIfError = false)` | Reads a text file into a `KValue`. With `ExitIfError` the script aborts if the file is missing or unreadable. |
| `bool WriteTextFile(KValue Filename, KValue Contents, bool ExitIfError = false)` | Writes a text file with the given contents. |
| `long GetModifiedTime(KValue Filename)` | Modification time of the file (Unix timestamp, UTC, seconds), or `0` if the file does not exist. |
| `bool Rename(KValue oldFilename, KValue newFilename)` | Renames a file. |
| `bool Move(KValue oldFilename, KValue newFilename)` | Moves a file (same operation as `Rename`). |
| `bool Delete(KValue Filename)` | Deletes a file. Returns false if it does not exist. |
| `long GetFileSize(KValue Filename)` | Size of the file in bytes, or `-1` if invalid. |
| `bool Copy(KValue source, KValue destination, bool newerOnly = true)` | Copies a file, overwriting the destination if it exists. With `newerOnly` (default) the copy is skipped — and `true` returned — when the destination is newer than the source. |
| `bool Compare(KValue first, KValue second, CompareOptions Options = CompareSize)` | Compares two files. Size is always compared; add `CompareTime` and/or `CompareContents` for stricter checks. Returns true if the files are equal. |

**`Files.CompareOptions`:** `CompareSize` (default), `CompareTime`, `CompareContents`.

---

## Folders

`Kltv.Kombine.Api.Folders` — folder operations, the working-folder stack and the
well-known folders of a script run.

### Properties

| Property | Description |
| --- | --- |
| `string CurrentWorkingFolder` | The current working folder. |
| `string CurrentScriptFolder` | Folder of the currently running script. |
| `string ParentScriptFolder` | Folder of the parent script, or empty if none. |
| `string CurrentToolFolder` | Folder where the `mkb` binary lives. |

### Methods

| Method | Description |
| --- | --- |
| `bool Exists(KValue folder)` | Returns true if the folder exists. |
| `bool Create(KValue folder)` / `Create(string folder)` | Creates a folder (including intermediate folders). |
| `bool Create(KList folders)` / `Create(string[] folders)` | Creates a list of folders. Returns false if any of them failed. |
| `bool Delete(KValue folder, bool recurse = false)` / `Delete(string folder, bool recurse = false)` | Deletes a folder, optionally including its subfolders. |
| `bool Delete(KList folders, bool recurse = false)` | Deletes a list of folders. |
| `bool Move(KValue src, KValue dst)` | Moves a folder. |
| `bool Copy(string Source, string Target, CopyOptions Options = Default, string FileMask = "*.*")` | Copies a folder with options (see below), optionally filtering files with a mask. |
| `void SetCurrentFolder(string CWD, bool PushCurrent = true)` | Sets the working folder; by default the previous one is pushed onto the folder stack. |
| `string GetCurrentFolder()` | Returns the current working folder. |
| `void CurrentFolderPush()` | Pushes the current working folder onto the stack. |
| `void CurrentFolderPop()` | Pops a folder from the stack and makes it current. |
| `KValue SearchBackPath(string filename)` | Searches for a filename walking **up** from the script folder (or working folder). Returns the full path or empty. |
| `string SearchForwardPath(string filename)` | Searches for a filename in the script folder (or working folder) and its subfolders. Returns the full path or empty. |

**`Folders.CopyOptions`** (combinable flags): `Default`, `IncludeSubFolders`,
`OnlyModifiedFiles`, `ShowProgress`, `OnlyFolders`, `DeleteMissingFiles` (mirror copy).

```csharp
Folders.SetCurrentFolder(CurrentScriptFolder + "/src/", true);
Exec("dotnet", "build -c debug", true);
Folders.CurrentFolderPop();

Folders.Copy("assets", "out/assets",
	Folders.CopyOptions.IncludeSubFolders | Folders.CopyOptions.OnlyModifiedFiles);
```

---

## Compress

`Kltv.Kombine.Api.Compress` — compression helpers, split into `Zip` and `Tar`.

### Compress.Zip

| Method | Description |
| --- | --- |
| `bool CompressFolder(string folderPath, string outputFile, bool overwrite = true, bool includeFolder = true)` | Compresses a folder into a zip file. With `includeFolder` the folder itself is included; otherwise only its contents. |
| `bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true, bool includeFolder = true)` | Compresses a list of folders into a zip file. |
| `bool CompressFile(string filePath, string outputFile, bool overwrite = true)` | Compresses a single file into a zip file. |
| `bool Decompress(string zipPath, string outputFolder, bool overwrite = true)` | Decompresses a zip file into a folder. |

### Compress.Tar

**`Tar.TarCompressionType`** (for compression): `None` (plain `.tar`), `Gzip`
(`.tar.gz`, default), `Bzip2` (`.tar.bz2`), `Lzma` (`.tar.lz`). `Lzma2` (`.tar.xz`) is
**extraction only**: requesting it for compression aborts the script, but `Decompress`
auto-detects the archive format and extracts `.tar.xz` files fine.

| Method | Description |
| --- | --- |
| `bool CompressFolder(string folderPath, string outputFile, bool overwrite = true, bool includeFolder = true, TarCompressionType compressionType = Gzip)` | Compresses a folder into a tar file. |
| `bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true, bool includeFolder = true, TarCompressionType compressionType = Gzip)` | Compresses a list of folders into a tar file. |
| `bool CompressFile(string filePath, string outputFile, bool overwrite = true, TarCompressionType compressionType = Gzip)` | Compresses a single file into a tar file. |
| `bool Decompress(string tarPath, string outputFolder, bool overwrite = true)` | Decompresses a tar file into a folder. The format is auto-detected (`.tar`, `.tar.gz`, `.tar.bz2`, `.tar.lz`, `.tar.xz`); symbolic links are skipped. |

```csharp
Compress.Zip.CompressFolder("out/bin/win-x64/release/", "out/pkg/app.win.zip", true, false);
Compress.Tar.CompressFile("out/pub/linux-x64/release/app", "out/pkg/app.lnx.tar.gz");
Compress.Tar.CompressFolder("child", "test.tar.bz2", true, true, TarCompressionType.Bzip2);
Compress.Tar.Decompress("sdk.tar.xz", "out/sdk/");   // xz: extraction only
```

---

## Http

`Kltv.Kombine.Api.Http` — HTTP helpers. All methods accept an optional dictionary of
headers to inject in the request.

| Member | Description |
| --- | --- |
| `int LastReturnCode` | HTTP status code of the last transaction, including failed downloads (`-1` on transport error). |
| `string LastResponse` | Response body of the last `GetDocument` / `PostDocument` transaction. |
| `bool DownloadFile(string uri, string path, Dictionary<string,string>? headers = null, bool showprogress = true)` | Downloads a file. The destination folder is created if needed; redirects are followed. `showprogress` displays a progress bar. Returns `false` on any network or HTTP error (the status is stored in `LastReturnCode`) without aborting the script, and no partial file is left behind — so the script can retry another URL or report its own message. |
| `bool DownloadFiles(string[] uris, string[] paths, Dictionary<string,string>? headers = null, bool showprogress = true)` | Downloads multiple files in parallel. Both arrays must have the same length. Returns `false` if any download failed. |
| `string GetDocument(string uri, Dictionary<string,string>? headers = null)` | GETs a document and returns its body, or empty on error. |
| `bool PostDocument(string uri, string content, Dictionary<string,string>? headers = null, bool usePatch = false)` | POSTs (or PATCHes with `usePatch`) a document. Content type defaults to `application/json`; override it with a `Content-Type` header. The response body is available in `LastResponse`. |
| `bool PostFile(string uri, string filePath, Dictionary<string,string>? headers = null, bool usePatch = false)` | POSTs (or PATCHes) a file as `application/octet-stream`. |
| `bool DeleteDocument(string uri, Dictionary<string,string>? headers = null)` | Sends a DELETE request. |

```csharp
var headers = new Dictionary<string, string> {
	{ "Authorization", "Bearer " + token },
	{ "User-Agent", "kombine" }
};
string json = Http.GetDocument("https://api.github.com/repos/owner/repo/releases", headers);
if (Http.LastReturnCode != 200)
	Msg.PrintAndAbort("Request failed: " + Http.LastReturnCode);
```

---

## JSON (JsonFile / JsonExtensions)

`Kltv.Kombine.Api.JsonFile` wraps a JSON document on disk. The document is exposed as a
standard `System.Text.Json.Nodes.JsonNode` through the `Doc` property (`System.Text.Json`
and `System.Text.Json.Nodes` are imported by default).

| Member | Description |
| --- | --- |
| `JsonFile(KValue file)` | Opens and parses the given file. If it does not exist or cannot be parsed, `Doc` starts as null and a new document can be created. |
| `JsonNode? Doc` | The root node of the document. Read and modify it with the standard `JsonNode` API. |
| `bool Save()` | Saves the document back to the file (indented). Creates the folder if needed. |

`Kltv.Kombine.Api.JsonExtensions` adds:

| Method | Description |
| --- | --- |
| `JsonNode? Find(this JsonArray array, string name, object value)` | Returns the first object in the array that has a property `name` with the given value, or null. |

```csharp
JsonFile pkg = new JsonFile("package.json");
if (pkg.Doc != null) {
	pkg.Doc["version"] = "1.4.0";
	pkg.Save();
}
```

---

## Yaml

`Kltv.Kombine.Api.Yaml` — a minimal YAML reader. Documents are parsed into a tree of
`Yaml.YamlObject` nodes.

### Yaml.YamlObject

| Member | Description |
| --- | --- |
| `string Key` | The node key. |
| `object? Value` | The node value: a `string`, a `List<YamlObject>`, or null. |
| `bool IsString()` | True if the value is a plain string. |
| `bool IsList()` | True if the value is a list of nodes. |

### Methods

| Method | Description |
| --- | --- |
| `YamlObject? LoadFile(string filename)` | Loads and parses a YAML file. Returns the document root node, or null if the file does not exist. |
| `string GetPropertyValue(YamlObject obj, string name)` | Returns the string value of the property `name` inside the given node, or empty. |
| `YamlObject? FindObject(YamlObject obj, string name, bool recurse, string? content = null)` | Returns the node with the given key (optionally requiring a matching string value), searching recursively when `recurse` is true. |
| `YamlObject? FindObjectInList(YamlObject obj, bool recurse, string key, string? value = null)` | Returns the **containing** node whose list holds an element with the given key (and optional value). |

```csharp
Yaml.YamlObject? doc = Yaml.LoadFile("config.yaml");
if (doc != null) {
	Yaml.YamlObject? server = Yaml.FindObject(doc, "server", true);
	if (server != null)
		Msg.Print("Port: " + Yaml.GetPropertyValue(server, "port"));
}
```

---

## Share

`Kltv.Kombine.Api.Share` — exchanges data between scripts (for example between a parent
script and the child scripts executed with [`Kombine()`](#global-functions-statics)).

The **registry** stores string values grouped by name and key, and is shared across all
scripts in the run regardless of their relationship. The **shared object pool** stores
object instances in the current script state.

| Method | Description |
| --- | --- |
| `bool Register(KValue name, KValue key, KValue value, bool ExitIfError = false)` | Adds a value to the shared registry. Returns false if the key already exists. |
| `KValue Registry(KValue name, KValue key, bool ExitIfError = false)` | Fetches a value from the registry, or empty if not found. |
| `void DumpRegistry()` | Dumps the registry contents (verbose log) for debugging. |
| `bool Set(string name, object obj, bool ExitIfError = false)` | Shares an object under the given name. Returns false if it was already shared. |
| `object? Get(string name, bool ExitIfError = false)` | Fetches a shared object, or null if it does not exist. Combine with `Cast<T>()` to recover the type. |
| `void DumpObjects(bool ExitIfError = false)` | Dumps the shared object pool (verbose log) for debugging. |

With any `ExitIfError` set to true, a failure aborts the script instead of returning,
printing the reason at any log level.

```csharp
// Parent script
Share.Register("build", "config", "release");

// Child script
KValue config = Share.Registry("build", "config");
```

---

## Tool

`Kltv.Kombine.Api.Tool` — wraps the execution of external tools, synchronously or
asynchronously with a concurrency limit. For simple cases prefer the global
[`Exec` / `Shell`](#global-functions-statics) functions, which use `Tool` internally.

### Construction and properties

| Member | Description |
| --- | --- |
| `Tool(string ToolTag)` / `Tool()` | Creates a tool wrapper. The tag is used in logs and as an indexer for state data. |
| `int ExpectedExitCode` | Exit code considered a success. Zero by default. |
| `ToolStatus Status` | Current tool status. |
| `string ToolTag` | The tool tag. |
| `bool UseShell` | If true, commands are launched through the system shell (sync commands only). Default false. |
| `uint ConcurrentCommands` | Number of queued commands executed concurrently by `ExecuteCommands`. Default 1. Combine with `Host.ProcessorCount()`. |
| `bool CaptureOutput` | If true, the tool output (stdout/stderr) is echoed to the console while it runs. Default false. |

**`Tool.ToolStatus`:** `Undefined`, `Success`, `Failed`, `Warnings`, `NoChanges`, `Pending`.

### Synchronous execution

| Method | Description |
| --- | --- |
| `ToolResult CommandSync(string cmd, string[]? args, object? id = null)` | Launches the command and waits. Returns a `ToolResult` with the full execution information. Arguments containing spaces are quoted automatically. |
| `ToolResult CommandSync(string cmd, string? args, object? id = null)` | Same, taking the arguments as a single string. |

### Asynchronous execution

| Method | Description |
| --- | --- |
| `delegate void CommandAsyncResults(ref ToolResult results)` | Callback invoked when an async command completes. It may modify the result. |
| `ToolResult CommandAsync(string cmd, string args, CommandAsyncResults? callback, object? id = null)` | Launches the command without waiting. Returns a partial result with status `Pending`. |
| `void CommandAsyncWaitAll(uint Limit = 0)` | Blocks until the number of pending commands is at or below `Limit` (0 = wait for all). |
| `void QueueCommand(string cmd, string args, object? id, CommandAsyncResults? callback)` | Queues a command for `ExecuteCommands`. The `id` is handed back in the callback result to identify the command (e.g. the source filename). |
| `ToolResult ExecuteCommands(bool AggregateResults = false)` | Executes all the queued commands, respecting `ConcurrentCommands`, and blocks until they finish. Returns a global result (`NoChanges` if nothing was queued; `Failed` if any command failed or could not be launched); with `AggregateResults` the stdout/stderr of every command is aggregated into it. |

### Tool.ToolResult

| Member | Description |
| --- | --- |
| `string[] Stdout` | Captured standard output, line by line. |
| `string[] Stderr` | Captured standard error, line by line. |
| `ToolStatus Status` | Result status. |
| `int ExitCode` | Tool exit code. |
| `object? Id` | The user-given id for the command. |

```csharp
Tool compiler = new Tool("cc");
compiler.ConcurrentCommands = (uint)Host.ProcessorCount();
foreach (KValue src in Glob("src/*.cpp")) {
	compiler.QueueCommand("clang++", "-c " + src, src, (ref Tool.ToolResult r) => {
		if (r.Status == Tool.ToolStatus.Failed)
			Msg.PrintError("Failed: " + r.Id);
	});
}
Tool.ToolResult result = compiler.ExecuteCommands();
if (result.Status == Tool.ToolStatus.Failed)
	Msg.PrintAndAbort("Build failed.");
```

---

## Host

`Kltv.Kombine.Api.Host` — information about the machine running the script.

| Method | Description |
| --- | --- |
| `bool IsRoot()` | True if running with administrator/root privileges. |
| `bool IsInteractive()` | True if running in an interactive shell. |
| `string GetOSKind()` | Short OS name: `"win"`, `"lnx"` or `"osx"` (empty if unknown). Handy to compose platform-specific paths or package names. |
| `bool IsWindows()` | True on Windows. |
| `bool IsLinux()` | True on Linux. |
| `bool IsMacOS()` | True on macOS. |
| `int ProcessorCount()` | Number of CPU cores available. |

---

## ProgressBar

`Kltv.Kombine.Api.ProgressBar` — an ASCII console progress bar (the one used by the
Http downloads). Dispose it to restore the console.

| Member | Description |
| --- | --- |
| `ProgressBar()` | Creates and shows the progress bar. |
| `void Report(double value)` | Updates the progress. `value` goes from 0.0 to 1.0. |
| `void Dispose()` | Removes the progress bar from the console. |

```csharp
using (var bar = new ProgressBar()) {
	for (int i = 0; i <= 100; i++) {
		bar.Report(i / 100.0);
		// ... work ...
	}
}
```
