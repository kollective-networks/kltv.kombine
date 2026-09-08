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
- [Engine settings (Engine)](#engine-settings-engine)
- [Logging (Msg)](#logging-msg)
- [Error reporting](#error-reporting)
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
- [Progress](#progress)

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
| `-ksrb` / `-ksrebuild` | Rebuild the script even if a compiled version is cached. A script can force it for its children with `Engine.RebuildScripts`. |
| `-ko:s` / `-ko:n` / `-ko:v` / `-ko:d` | Output level: silent, normal, verbose, debug. Normal shows the script output; verbose and debug add the messages of Kombine itself (see [Logging](#logging-msg)). |
| `-kfile:<name>` | Script file to execute (default `kombine.csx`). |
| `-kforward` | Allows the forward search of the subfolders when resolving `#load` and child script references. Disabled by default, since it can bind a foreign copy of a helper when repositories are nested. A script can toggle it for its children with `Engine.ForwardSearch`. |

`mkb -h` and `mkb --help` print the help, as `mkb khelp` does.

### Exit codes

| Exit code | When |
| --- | --- |
| `0` | The action completed and returned 0, or no action was given and the help was shown. |
| `n` | The action returned `n`: the return value of the action is the exit code of the process. |
| `1` | The tool detected a failure: the script does not compile, the action threw an exception or aborted (`Msg.PrintAndAbort`, or any `ExitIfError` abort), the action was not found or does not return an `int`, the script file is missing, or a reserved action failed. |
| `130` | The execution was cancelled with Ctrl+C. The running tools are killed first. |

`Kombine()` returns the exit code of the child script under the same contract and, by
default, aborts the calling script when it is not zero.

---

## Global Functions (Statics)

`Kltv.Kombine.Api.Statics` — static methods published directly into the script; call
them without any class prefix.

| Method | Description |
| --- | --- |
| `KList Glob(string pattern)` | Resolves a glob pattern to a list of files, using the current working directory as base path. |
| `KList Glob(string folder, string pattern)` | Resolves a glob pattern using the given folder as base path. |
| `KValue RealPath(KValue path)` | Returns the absolute path for the given path, converted to the underlying OS conventions. Empty on error. |
| `int Kombine(string script, string action, string[]? args = null, bool exitonerror = true, bool changedir = true, bool search = true)` | Executes a child Kombine script **in the same process** and returns its exit code. If `search` is true the script is looked up through the resolution paths; if `changedir` is true the working directory is switched to the script folder while it runs; if `exitonerror` is true a non-zero result aborts the calling script. When the child cannot run at all (not found, unresolved references, compile error, missing action) it returns 1 without printing anything and `Engine.LastError` holds the reason (see [Engine settings](#engine-settings-engine)). |
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
| `bool Export(string name)` | Exports the value to the script environment under the given name. The environment is inherited by executed tools and child scripts. Returns false, with `KValue.LastError`, when no script is running. |
| `static ApiError LastError` | Last failure of an `Export` call (see [Error reporting](#error-reporting)). |
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

## Engine settings (Engine)

`Kltv.Kombine.Api.Engine` — engine switches a script can read and change. They mirror the
tool parameters and apply to the scripts compiled from then on, that is, the child scripts
run with [`Kombine()`](#global-functions-statics); the running script is already compiled.

| Member | Description |
| --- | --- |
| `ApiError LastError` | Last failure of a child script run with `Kombine()`: script not found, unresolved references, compile error, action not found or not returning an int, exception or abort. Reset when a child runs. The engine prints nothing for a child failure: the calling script reads the return code and this reason (see [Error reporting](#error-reporting)). |
| `bool ForwardSearch` | Allows the forward search of the subfolders when resolving `#load` and child script references. Mirrors `-kforward`. Disabled by default, since it can bind a foreign copy of a helper when repositories are nested. |
| `bool RebuildScripts` | Forces the rebuild of the scripts compiled from now on, even if a compiled version is cached. Mirrors `-ksrb`. Needed to make a change of `ForwardSearch` effective on a cached child script, since the resolution happens at compile time. |

```csharp
bool previous = Engine.ForwardSearch;
Engine.RebuildScripts = true;            // the child must be compiled again for the resolution to run
Engine.ForwardSearch = true;             // forward search only for this child
Kombine("legacy/build.csx", "build", args);
Engine.ForwardSearch = previous;
```

A child that fails before its action runs (not found, unresolved references, compile error,
missing action) returns 1 without printing anything: the reason is in `Engine.LastError`, so
the calling script owns the message.

```csharp
if (Kombine("tools/pack.csx", "pack", args, false) != 0 && Engine.LastError.IsError)
    Msg.PrintError("pack.csx failed: " + Engine.LastError.Message);
```

The [`#load` resolution example](../examples/08.loadresolution/mkb.loadresolution.csx)
runs its checks in both modes this way.

---

## Logging (Msg)

`Kltv.Kombine.Api.Msg` — console output for scripts. All print methods accept an
optional `Msg.LogLevels` argument (default `Normal`); the message is only shown when the
current output level (see `-ko:` parameters) is equal or higher.

> [!IMPORTANT]
> The output level is for Kombine, not for the script. `Normal` shows what the script
> prints and nothing else; `Verbose` and `Debug` add the internal messages of the API and
> the engine, to diagnose Kombine itself. A failed API call reports the failure to the
> script through its return value and prints nothing at normal level: the script owns its
> output and prints its own messages with `Msg`.

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

## Error reporting

A failed API call returns `false` (or empty, `-1`, `0`, `null`) and prints nothing at
normal level. The reason is available in the `LastError` of the facility, so the script
can explain the failure with its own message:

```csharp
if (!Files.Copy(src, dst)) {
	Msg.PrintError("Cannot copy " + src + ": " + Files.LastError.Message);
	return 1;
}

if (!Compress.Tar.Decompress(archive, folder)) {
	if (Compress.Tar.LastError.Code == ErrorCode.NotFound)
		Msg.PrintError("Archive not found: " + archive);
	else
		Msg.PrintError("Extraction failed: " + Compress.Tar.LastError);   // "Failed: 2 entries refused (path traversal) (x.tar)"
	return 1;
}

if (!Files.Compare(a, b, Files.CompareOptions.CompareContents)) {
	if (Files.LastError.Code == ErrorCode.Different)
		Msg.Print("The files differ: " + Files.LastError.Message);
	else
		Msg.PrintError("Cannot compare: " + Files.LastError.Message);   // a file is missing
}
```

**`Kltv.Kombine.Api.ApiError`:**

| Member | Description |
| --- | --- |
| `ErrorCode Code` | Kind of failure (see below). `None` when the last call succeeded. |
| `string Message` | Reason of the failure, ready to be shown. |
| `string Source` | Item involved: a path, an url, a key or a name. Empty when it does not apply. |
| `bool IsError` | True when `Code` is not `None`. |
| `string ToString()` | `Code: Message (Source)`. |

**`ErrorCode`:**

| Code | Meaning |
| --- | --- |
| `None` | No error, the last call succeeded. |
| `NotFound` | File, folder, archive, key or object not found. |
| `AlreadyExists` | The target already exists, or the key or object is already registered. |
| `AccessDenied` | Permissions or a locked file. |
| `InvalidArgument` | Empty name, mismatched lists, unsupported combination, no script running. |
| `NotSupported` | The operation is not available, for example xz compression. |
| `IoError` | Any other file system or archive failure (a folder that is not empty, a corrupt archive). |
| `NetworkError` | Transport failure without an HTTP status. |
| `Different` | The compared files differ (`Files.Compare`). |
| `Failed` | The operation ran and reported a failure: a refused entry, an HTTP error status, a tool exit code. |

The facilities with a `LastError` are `Files`, `Folders`, `Compress.Zip`, `Compress.Tar`,
`Share`, `Http` (next to `LastReturnCode`) and `KValue` (for `Export`). It is reset at
the start of every call of that facility and set when the call fails, so read it right
after the failed call. Queries that answer a question never set it: `Files.Exists`,
`Folders.Exists` and the folder searches return their answer. The `ExitIfError`
parameters keep aborting the script, with the reason visible at any log level.

The engine logs the same reason at verbose level, for its own diagnosis.

---

## Files

`Kltv.Kombine.Api.Files` — file operations. Methods take `KValue` filenames, so plain
strings work as well. Failures are reported in the return value and in `LastError` (see
[Error reporting](#error-reporting)) instead of throwing, and logged in verbose mode.

| Method | Description |
| --- | --- |
| `ApiError LastError` | Last failure of a `Files` call. `Exists` never sets it. |
| `bool Exists(KValue Filename)` | Returns true if the file exists. |
| `KValue ReadTextFile(KValue Filename, bool ExitIfError = false)` | Reads a text file into a `KValue`. With `ExitIfError` the script aborts if the file is missing or unreadable. |
| `bool WriteTextFile(KValue Filename, KValue Contents, bool ExitIfError = false)` | Writes a text file with the given contents. |
| `long GetModifiedTime(KValue Filename)` | Modification time of the file (Unix timestamp, UTC, seconds), or `0` if the file does not exist. |
| `bool Rename(KValue oldFilename, KValue newFilename)` | Renames a file. |
| `bool Move(KValue oldFilename, KValue newFilename)` | Moves a file (same operation as `Rename`). |
| `bool Delete(KValue Filename)` | Deletes a file. Returns false if it does not exist. |
| `long GetFileSize(KValue Filename)` | Size of the file in bytes, or `-1` if invalid. |
| `bool Copy(KValue source, KValue destination, bool newerOnly = true)` | Copies a file, overwriting the destination if it exists. With `newerOnly` (default) the copy is skipped — and `true` returned — when the destination is newer than the source. |
| `bool Compare(KValue first, KValue second, CompareOptions Options = CompareSize)` | Compares two files. Size is always compared; add `CompareTime` and/or `CompareContents` for stricter checks. Returns true if the files are equal. On false, `LastError` is `Different` with what differed in the message, or `NotFound` naming the missing file. |

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
| `ITaskProgress? Progress` | Reporter used by `Copy` when the `ShowProgress` option is given (see [Progress](#progress)). Not set by default: `Progress.Default` is used. Assign a renderer to select it. |
| `ApiError LastError` | Last failure of a `Folders` call (see [Error reporting](#error-reporting)). `Exists` and the searches never set it. |

### Methods

| Method | Description |
| --- | --- |
| `bool Exists(KValue folder)` | Returns true if the folder exists. |
| `bool Create(KValue folder)` / `Create(string folder)` | Creates a folder (including intermediate folders). |
| `bool Create(KList folders)` / `Create(string[] folders)` | Creates a list of folders. Returns false if any of them failed. |
| `bool Delete(KValue folder, bool recurse = false)` / `Delete(string folder, bool recurse = false)` | Deletes a folder, optionally including its subfolders. |
| `bool Delete(KList folders, bool recurse = false)` | Deletes a list of folders. |
| `bool Move(KValue src, KValue dst)` | Moves a folder. |
| `bool Copy(string Source, string Target, CopyOptions Options = Default, string FileMask = "*.*")` | Copies a folder with options (see below), optionally filtering files with a mask. With `ShowProgress` it prints `Copying <folder>: <progress> n/total done` (or `failed`) through `Folders.Progress`: the items are counted first, and every processed file advances the progress, also the ones skipped as unchanged. |
| `bool SetCurrentFolder(string CWD, bool PushCurrent = true)` | Sets the working folder; by default the previous one is pushed onto the folder stack. Returns false, with `LastError`, when the folder is empty or does not exist. |
| `string GetCurrentFolder()` | Returns the current working folder. |
| `void CurrentFolderPush()` | Pushes the current working folder onto the stack. |
| `bool CurrentFolderPop()` | Pops a folder from the stack and makes it current. Returns false when the stack is empty or the folder is gone. |
| `KValue SearchBackPath(string filename)` | Searches for a filename walking **up** from the script folder (or working folder). Returns the full path or empty. |
| `string SearchForwardPath(string filename)` | Searches for a filename in the script folder (or working folder) and its subfolders. Returns the full path or empty. |

**`Folders.CopyOptions`** (combinable flags): `Default`, `IncludeSubFolders`,
`OnlyModifiedFiles`, `ShowProgress` (progress line through `Folders.Progress`), `OnlyFolders`,
`DeleteMissingFiles` (mirror copy).

```csharp
Folders.SetCurrentFolder(CurrentScriptFolder + "/src/", true);
Exec("dotnet", "build -c debug", true);
Folders.CurrentFolderPop();

Folders.Copy("assets", "out/assets",
	Folders.CopyOptions.IncludeSubFolders | Folders.CopyOptions.OnlyModifiedFiles | Folders.CopyOptions.ShowProgress);
// Copying assets: [####################] 100% 48/48 done
```

---

## Compress

`Kltv.Kombine.Api.Compress` — compression helpers, split into `Zip` and `Tar`. Every
method returns false on failure with the reason in `Compress.Zip.LastError` or
`Compress.Tar.LastError` (see [Error reporting](#error-reporting)): `NotFound` for a
missing source or archive, `AlreadyExists` for an existing output with overwrite
disabled, `NotSupported`, `IoError` or `Failed`. A failed compression leaves no partial
archive behind.

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
**extraction only**: requesting it for compression returns false with `NotSupported`, but
`Decompress` auto-detects the archive format and extracts `.tar.xz` files fine.

| Method | Description |
| --- | --- |
| `bool CompressFolder(string folderPath, string outputFile, bool overwrite = true, bool includeFolder = true, TarCompressionType compressionType = Gzip)` | Compresses a folder into a tar file. |
| `bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true, bool includeFolder = true, TarCompressionType compressionType = Gzip)` | Compresses a list of folders into a tar file. |
| `bool CompressFile(string filePath, string outputFile, bool overwrite = true, TarCompressionType compressionType = Gzip)` | Compresses a single file into a tar file. |
| `bool Decompress(string tarPath, string outputFolder, bool overwrite = true)` | Decompresses a tar file into a folder, created if needed. The format is auto-detected (`.tar`, `.tar.gz`, `.tar.bz2`, `.tar.lz`, `.tar.xz`); symbolic links are skipped. Entries that would escape the destination folder are refused: the rest are still extracted, but the call returns false with `Failed` saying how many entries were refused or not extracted. |

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
| `ApiError LastError` | Last failure (see [Error reporting](#error-reporting)): `Failed` with the status in the message for an HTTP error status, `NetworkError` for a transport failure. |
| `ITaskProgress? Progress` | Reporter used by the downloads to show their progress line (see [Progress](#progress)). Not set by default: `Progress.Default` is used. Assign a `ProgressBar`, `ProgressDots`, `ProgressPlain` or a custom reporter to select the renderer. |
| `bool ShowProgress` | If the downloads show their progress line, `true` by default. When `false` the downloads print nothing. Default of the `showprogress` parameter below. |
| `bool DownloadFile(string uri, string path, Dictionary<string,string>? headers = null, bool? showprogress = null)` | Downloads a file. The destination folder is created if needed; redirects are followed. The progress line reads `Downloading <file>: <progress> done`, or `failed`; `showprogress` overrides `ShowProgress` for this call. Returns `false` on any network or HTTP error (the status is stored in `LastReturnCode`) without aborting the script, and no partial file is left behind — so the script can retry another URL or report its own message. |
| `bool DownloadFiles(string[] uris, string[] paths, Dictionary<string,string>? headers = null, bool? showprogress = null)` | Downloads multiple files in parallel with one progress line for the batch (`Downloading N files: ...`). Both arrays must have the same length. Returns `false` if any download failed. |
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
Failures are reported in the return value and in `Share.LastError` (see
[Error reporting](#error-reporting)): `AlreadyExists` for a duplicate key or object,
`NotFound` for a missing one, `InvalidArgument` when no script is running.

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

## Progress

`Kltv.Kombine.Api.ITaskProgress` — console progress reporting for long operations. A
reporter owns one console line that reads, at the current indentation of the message
system:

```
Downloading kombine.win.zip: [##########----------]  50% /        while running
Downloading kombine.win.zip: [####################] 100% done     finished; the line stays on screen
```

The engine uses it for the Http downloads and the folder copies. Scripts and extensions
use it for their own operations in the same way.

### Using a reporter

1. Create the renderer you want: `new ProgressBar()`, `new ProgressDots()` or `new ProgressPlain()`.
2. `Start("message")` opens the line and prints `message: `.
3. `Report(value, status)` as the work advances: `value` goes from 0.0 to 1.0, and the
   optional `status` (for example `"12/48"`) is shown after the progress.
4. `Finish("message", outcome)` prints the end message in the colour of the outcome and
   closes the line.

```csharp
ITaskProgress progress = new ProgressBar();
progress.Start("Packing assets");
for (int i = 0; i < files.Count(); i++) {
	Pack(files[i]);
	progress.Report((i + 1) / (double)files.Count(), $"{i + 1}/{files.Count()}");
}
progress.Finish("done");          // Packing assets: [####################] 100% 48/48 done
```

| Member | Description |
| --- | --- |
| `void Start(string message)` | Opens the line printing `message: ` at the current indentation. |
| `void Report(double value, string? status = null)` | Reports the progress (0.0 to 1.0) with an optional status text shown after it. |
| `void Finish(string message = "", ProgressOutcome outcome = Success)` | Renders the final state, prints the end message coloured by the outcome and closes the line. An empty message closes the line without message. |
| `void Dispose()` | Closes the line if the operation was not finished, keeping the last rendering. The reporter stays usable. |

**Outcomes** (`ProgressOutcome`): `Success` prints the message in green and snaps the
progress to 100%; `Warning` (yellow) and `Error` (red) keep the last reported progress.

```csharp
progress.Finish("done with 2 warnings", ProgressOutcome.Warning);
progress.Finish("failed", ProgressOutcome.Error);
```

**Unknown size:** do not call `Report`. The bar shows a spinner alone while the operation
runs and the line ends with the message only.

```csharp
progress.Start("Waiting for the server");
WaitForServer();
progress.Finish("done");          // Waiting for the server: done
```

**Rules:**

- A reporter is reusable: `Finish` resets it and the next `Start` opens a new line, so one
  instance serves every operation of a facility, one after another.
- One operation at a time per instance. Do not print other messages while a line is open;
  finish it first.
- Nothing is printed at the `Silent` log level.
- When the output is redirected (a pipe, a log) the bar prints only the start and end
  messages, since nobody can see it. The dots print normally, they never move the cursor.

### Renderers

| Class | Looks like | Use it when |
| --- | --- | --- |
| `ProgressBar` | `[##########----------]  50% 12/48 /` | The output is a console. Spinner alone while nothing was reported. |
| `ProgressDots` | `.......... 48/48` (one dot per 10%, the status only at the end) | The output goes to a log and a trace of the progress is still wanted. |
| `ProgressPlain` | nothing between the messages: `Packing assets: done` | Only the messages are wanted. It is the default when the output is redirected. |

### Selecting the reporter of a facility

Every facility that reports progress exposes a `Progress` member of type `ITaskProgress`.
The facility uses that member and nothing else: assign it with the renderer you want.
Until it is assigned, the facility uses `Progress.Default`, which is a `ProgressBar`, or a
`ProgressPlain` when the output is redirected. Assign `Progress.Default` to change every
facility left unconfigured at once.

| Facility | Member | Silence switch |
| --- | --- | --- |
| Http downloads | `Http.Progress` | `Http.ShowProgress = false` (or the `showprogress` parameter of the call) prints nothing. |
| Folder copies | `Folders.Progress` | The `CopyOptions.ShowProgress` flag of `Folders.Copy` enables the line. |
| Extensions | their own `Progress` member | as documented by each extension |

```csharp
Http.Progress = new ProgressDots();        // downloads report with dots from now on
Folders.Progress = new ProgressBar();      // folder copies keep the bar
Progress.Default = new ProgressPlain();    // anything else prints only the messages
Http.ShowProgress = false;                 // downloads print nothing at all
```

### Styling

Every renderer has a `ConsoleColor? Color` property for the colour of the progress
rendering; null keeps the console colour, and the end message always takes the colour of
its outcome. `ProgressBar` adds `Width` (20 blocks by default), `FilledChar` (`#`) and
`EmptyChar` (`-`):

```csharp
Http.Progress = new ProgressBar { FilledChar = '█', EmptyChar = '·', Color = ConsoleColor.Green };
Folders.Progress = new ProgressBar { Width = 10, FilledChar = '=', EmptyChar = ' ' };
Progress.Default = new ProgressDots { Color = ConsoleColor.Green };
```

Block glyphs need a console font that has them. The bar is never drawn on redirected
output, so logs are not affected.

### Writing a renderer

Derive from `ProgressBase`, which implements the contract, the animation timer, the
visibility rules and the redraw. Provide the text drawn after the prefix and two
properties:

| Member | Description |
| --- | --- |
| `string Render(double value, string? status, bool reported, bool final, int frame)` | The text shown after `message: `. It replaces the previous text; only the difference is written to the console. `reported` is false while nothing was reported, `final` is true for the last frame before the end message, `frame` counts the redraws for animations. |
| `bool Animated` | True to be redrawn by a timer at 8 frames per second, false to be drawn on every report. |
| `bool RedirectSafe` | True when the rendering only appends text (no cursor movement) and can be shown on redirected output. |
| `void OnStart()` | Optional. Called when an operation starts, to reset the renderer state. |

```csharp
// Shows the percentage only: Packing assets: 50% ... Packing assets: 100% done
public sealed class ProgressPercent : ProgressBase {
	protected override bool Animated { get { return false; } }
	protected override bool RedirectSafe { get { return false; } }
	protected override string Render(double value, string? status, bool reported, bool final, int frame) {
		return reported ? ((int)(value * 100)).ToString() + "%" : string.Empty;
	}
}
```

The renderer is usable everywhere a reporter is expected: `Http.Progress = new ProgressPercent();`.
The example [examples/00.base/mkb.progress.csx](../examples/00.base/mkb.progress.csx)
shows every renderer, the outcomes, the styles and the facility configuration.
