[Back to the extensions index](../extensions.md)

# clang.csx

Verbs around the clang toolchain for build scripts: compiling sources into objects, archiving
objects into static libraries, linking executables and shared libraries, formatting sources and
keeping the compile database editors read. Requires Kombine 1.6 (the file declares it with
`#pragma kombine requires 1.6`). Demonstrated by the sub projects of
[examples/06.extensions/00.clang](../../examples/06.extensions/00.clang/) (a static library, an
executable, a shared library, each in its own child script) and tested by the grouped example
of the same folder; used by the sdl2 extra example
([examples/07.extras/00.sdl2](../../examples/07.extras/00.sdl2/)).

```csharp
#load "extensions/clang.csx"
```

## How the verbs work

- **One instance per configuration.** A `Clang` instance holds its options (`clang.Options`, a
  `ClangOptions`) and its results. The verbs are `Compile`, `Librarian`, `Linker`, `Format`,
  `Clean` and `OpenCompileCommands`, plus `Version`, `VersionCheck` and `ToolVersion` for the
  tools. Every verb reads the options of its instance; no variants of a verb exist. A script may
  hold two instances with different options.
- **Defaults equal to the previous extension.** A call without changes to the options compiles,
  archives and links with the same switches and writes the same files as before, and existing
  calls compile unchanged. What is not as before: the listings of include paths, defines,
  switches and libraries print only with `Verbose`, the `-v` of the tools is `ClangVerbose`,
  `LD` names the linker the driver uses, `Format` is an instance method, and a rebuilt script no
  longer forces a rebuild of every unit (the recorded command lines decide).
- **The results.** `Compile`, `Librarian` and `Linker` return the `ToolResult` of the tools as
  they always did (`Status` `Success`, `Failed`, or `NoChanges` when nothing was to do;
  `ExitCode`, `Stdout`, `Stderr`); `Format`, `Clean` and `OpenCompileCommands` return true or
  false. Every failure leaves its reason in `clang.LastError` (a code and the first error line)
  and what a verb did in a `Last<Verb>` property: `LastCompile`, `LastLibrarian`, `LastLinker`,
  `LastFormat`. `Clang.Status` accumulates the counters of the whole run, child scripts included.
- **Nothing printed at normal level** but the progress line and, once a build ended, its
  diagnostics: the script prints its own messages, and the extension writes no report of its own.
- **The abort.** The `abortwhenfailed` parameter of a verb, null by default, takes
  `ClangOptions.AbortOnFailure` (true): a failure prints its reason and aborts the script,
  whatever the output mode. When the report already printed the diagnostics (`Progress` and
  `Detailed`) the abort line gives only the counts ("clang Compile failed: 1 error in 1 unit");
  in `Silent` it gives the full reason, since nothing else was printed. With false the verb returns its failed result and the script reads
  `LastError` and `Last<Verb>`; a failed unit does not stop the other units of the batch, so every
  error of a build is visible at once.

```csharp
#load "extensions/clang.csx"

int build(string[] args) {
	Clang clang = new Clang();
	clang.Options.IncludeDirs += "inc/";
	clang.Options.SwitchesCXX += "-O2";
	clang.Options.AbortOnFailure = false;
	clang.OpenCompileCommands("out/tmp/compile_commands.json");
	KList src = Glob("src/*.cpp");
	KList obj = src.WithExtension(clang.Options.ObjectExtension).WithPrefix("out/tmp/");
	if (clang.Compile(src, obj).Status == ToolStatus.Failed) {
		Msg.PrintError("compile failed: " + clang.LastError.Message);
		return 1;
	}
	if (clang.Linker(obj, "out/bin/myapp" + clang.Options.BinaryExtension).Status == ToolStatus.Failed)
		return 1;
	return 0;
}
```

## Output modes

`ClangOptions.Output` decides what reaches the console while the tools run:

| Value | Console | Tool text |
| --- | --- | --- |
| `Silent` | nothing at all; the script reads `LastCompile` and `LastError` and prints what it wants | captured, in `LastOutput` and the results |
| `Progress` (default) | one progress line per verb through `ClangOptions.Progress`, started with `TaskLabel` and ended with the result: `ok`, `ok (up to date)`, `ok (n warnings)`, `failed (n errors)`, `failed (tool failure)`; the listings before the line with `Verbose` | captured and counted, never shown while running |
| `Detailed` | one task line per unit ("Compiling x: Ok"), the verb closed with its result line; with `Verbose` the listings and the response file messages, with `ClangVerbose` the extra lines the tools print | kept, the `-v` text shown after the batch |

The mode decides what the console shows, not what is emitted: the detailed lines (the task line
of every unit, the listings, the objects added, the response file messages, the result line, the
text of the tools, and in `Silent` the diagnostics too) always go out, at verbose level in the
modes that do not show them. They stay off the console unless `-ko:v`, and a `Msg.OnMessage`
handler receives them whatever the mode, so a script can show the progress line on screen and
forward the whole detail to a log.

Once a build ended, `Progress` and `Detailed` print nothing when it had no warning and no
error. Otherwise they print the diagnostics themselves, grouped per unit, every line naming the
offending file as the compiler wrote it:

```
Compiling 5 files: [██████████] 100% 2/2 compiled, 3 up to date failed (1 error)
src/warn.c:
    src/warn.c:1:19: warning: unused variable 'unused' [-Wunused-variable]
        1 | int w(void) { int unused = 1; return 0; }
          |                   ^~~~~~
src/bad.c:
    src/bad.c:1:24: error: call to undeclared function 'missing_function'
        1 | int bad(void) { return missing_function(); }
          |                        ^
```

The errors are always printed in full; `ShowWarnings` false reduces the warnings to their count
per unit; `DiagnosticFilter` hides the diagnostics a script does not want to see (the noise of a
third party unit) without changing the counts.

## ClangOptions

The configuration of an instance. `SetAsDefault()` stores it in the `Share` registry as the
defaults every new `Clang` instance copies, in this script and in its child scripts (every
property, the lists as new lists, so a change on an instance never touches the shared object;
the first call of the run sets the defaults). Every default corresponds to what the previous
extension did.

| Property | Default | Meaning |
| --- | --- | --- |
| `string CC` | `clang` | The C compiler; `gcc` takes the same command lines. |
| `string CXX` | `clang++` | The C++ compiler, and the driver of the link. |
| `string LD` | `lld` | The linker the driver must use: a name (`lld`, `gold`, `bfd`, `mold`) passed as `-fuse-ld=name`, a path passed as `--ld-path=path` (clang only), empty adds nothing and the driver picks the linker of its target (how a macOS build or a Linux without lld links). The previous extension forced `lld` on every link. |
| `string AR` | `llvm-ar` | The archiver; `ar` takes the same command line. |
| `string RC` | `llvm-rc` | The resource compiler of Windows resources. |
| `string ReadObj` | `llvm-readobj` | The object reader `Librarian` uses to find the symbols every object defines. |
| `DuplicateSymbolCheck DuplicateSymbols` | `Ignore` | What `Librarian` does about a symbol defined by more than one object: `Ignore` skips the check, `Warn` writes the archive and reports each duplicate as a warning naming the symbol and both objects, `Fail` refuses the archive with `InvalidArgument`. The check reads the symbols of every object once per archive written, a moment on a large archive. |
| `string CExtension`, `CppExtension`, `ResExtension` | `.c`, `.cpp`, `.rc` | How a source is classified; several extensions separated by `;` (`.cpp;.cc;.cxx`), the comparison ignoring the case on Windows and macOS. A source with an extension outside them fails the compile with `NotSupported`. |
| `KList IncludeDirs`, `Defines` | empty | `-I` and `-D` of every unit. |
| `KList SwitchesCC`, `SwitchesCXX` | empty | Switches of the C and C++ units. |
| `KList LibraryDirs`, `Libraries`, `SwitchesLD` | empty | `-L`, `-l` (a full path is passed as it is) and switches of the link. |
| `int ConcurrentBuild` | the number of processors | Units compiled, and files formatted, in parallel. |
| `bool Verbose` | false | The extension prints its own details: the listings and the response file messages, in the `Progress` and `Detailed` modes. |
| `bool ClangVerbose` | false | `-v` to the compilers and to the linker, `rcsv` to the archiver; their extra lines show in the `Detailed` mode. |
| `string TaskLabel` | empty | The start message of the progress line; empty uses the verb and the output ("Compiling 48 files", "Linking app.exe", "Library libapp.a"). |
| `bool ShowWarnings` | true | The warnings printed in full in the report; false reduces them to their count per unit. |
| `Func<string, string, bool>? DiagnosticFilter` | null | Given the source of a unit and one diagnostic line, returns whether the diagnostic is printed. |
| `ClangOutput Output` | `Progress` | The output mode, above. |
| `ITaskProgress? Progress` | null, the engine default | The reporter of the progress line; assign a `ProgressBar`, `ProgressDots` or `ProgressPlain` (see [Progress](../api.md#progress)). |
| `bool AbortOnFailure` | true | The default of the `abortwhenfailed` parameter of every verb. |
| `int Timeout` | 0, no limit | Milliseconds one tool invocation (one unit, one link, one archive, one format) may run before it is killed and counted as failed. |
| `string LibExtension`, `SharedExtension`, `BinaryExtension`, `ObjectExtension` | per platform, read only | `.lib .dll .exe .obj` on Windows, `.a .so .out .o` on Linux, `.a .dylib .out .o` on macOS. |

## Verbs

The signature, then what the verb does. `Last<Verb>` names the property the verb fills.

### Compile

`ToolResult Compile(KList src, KList obj, bool? abortwhenfailed = null, bool rebuild = false)` — `LastCompile`

Compiles the sources that are out of date into their objects, one to one (the lists must match
in length and hold no duplicates: `InvalidArgument` otherwise). A unit runs when its object is
missing, when its command line changed, when any input of its previous compile (the source and
every header of its dependency file, system headers included) is missing or changed in content,
or with `rebuild`. The units run in parallel up to `ConcurrentBuild`; the dependency file of
each unit is written next to its object (`-MD -MF`), and the compile database, if one is open,
is updated after the checks, without the resource units. A missing source is `NotFound` before
anything is queued; a missing tool is `NotFound` naming the option that configures it.
`ProcessFile`, a delegate of the instance, is called with every C and C++ unit about to be
compiled and returns extra arguments for it.

`LastCompile`, a `CompileResult`: `Units` (each with its `Source`, `Object`, `Status`
(`UpToDate`, `Compiled`, `Warnings`, `Failed`, `Skipped`), its `Diagnostics` as the compiler
wrote them and its `Warnings` and `Errors` counts), `Queued`, `Compiled`, `UpToDate`,
`Warnings`, `Errors`, `Failed` (the units that failed), `CompileDatabase`, `Cancelled`. The
`ToolResult`: `Success` when every queued unit compiled, `NoChanges` when nothing was to do,
`Failed` otherwise; `Stdout` and `Stderr` hold the text of every unit.

### Librarian

`ToolResult Librarian(KList objs, KValue output, bool? abortwhenfailed = null)` — `LastLibrarian`

Builds a static library from the objects (the `lib` prefix on Linux and macOS) when the archive
is missing, when an object or the command line changed, or when an object was removed from the
list: the archive is deleted and created again with every object in one command, so a removed
object leaves it. When the command line exceeds what the platform allows, the objects go through
a response file in the temp folder, removed afterwards whatever happens. Every object must exist
(`NotFound` otherwise). A symbol defined by two of the objects, the mark of a source list that
takes a generic and a platform folder of the same sources, is reported here instead of at the
link when `DuplicateSymbols` asks for it: a warning naming the symbol and both objects with
`Warn`, a refusal with `InvalidArgument` with `Fail`; off by default. The check reads the
symbols of the objects through `ReadObj` once per archive written; weak, common and COMDAT
definitions (the inline functions and templates every C++ unit emits) are not duplicates. `LastLibrarian`, a `LinkResult`: `Output`,
`UpToDate`, `Objects`, `Warnings`, `Errors`, `Diagnostics`.

### Linker

`ToolResult Linker(KList objs, KValue output, bool SharedLibrary = false, bool? abortwhenfailed = null)` — `LastLinker`

Links the objects into an executable, or into a shared library with `SharedLibrary` (the `lib`
prefix on Linux and macOS), when the output is missing, when the command line changed, or when an
object or a library of the options is missing or changed: a library named in `Libraries` is
searched in `LibraryDirs` as `libname.a`, `name.lib`, `libname.so`, `libname.dylib`, `name.dll`
and counts as an input when found, so a rebuilt library links the executable again. The command
is `CXX` as driver, the linker of `LD`, `SwitchesLD`, `-shared` for a shared library and `-v`
with `ClangVerbose`. When the command line exceeds what the platform allows the arguments go
through a response file in the temp folder, removed afterwards whatever happens. `LastLinker`, a
`LinkResult`: `Output`, `UpToDate`, `Objects`, `Shared`, `Warnings`, `Errors`, `Diagnostics`.

### Format

`bool Format(KList src, string extraArgs, bool? abortwhenfailed = null)` — `LastFormat`

Formats the files in place with `clang-format -i`, one command per file run in parallel up to
`ConcurrentBuild`, through the same runner as the other verbs: the output mode, the timeout, the
diagnostics printed after the batch, the abort rule. `extraArgs` is appended to every command.
False with `NotFound` for a missing tool or file, `Failed` for a rejected file. `LastFormat`, a
`FormatResult`: `Files` formatted, `Failed`, `Diagnostics`.

### Clean

`bool Clean(KList obj, KValue output)`

Deletes the folders of the objects and the folder of the output with everything in them (the
dependency files and the records included). True when the folders are gone, already absent
included; false with `IoError`.

### OpenCompileCommands

`bool OpenCompileCommands(KValue file)`

Opens or creates the compile database (`compile_commands.json`) and shares it through the
`Share` registry, so every `Clang` instance of the run, in this script and in its child scripts,
appends to the same file; it is saved by every `Compile`. False with `IoError` when the file
cannot be read or created.

### Version, VersionCheck, ToolVersion

- `ToolVersionInfo Version()`: the version of the `CC` of the instance (`CC --version`, the first
  `major.minor[.patch]` of its first lines, which reads clang, gcc, lld, ar and clang-format
  alike); the fields are -1 when the tool is missing or prints no version.
- `bool VersionCheck(int major, int minor, int patch)`: true when the C compiler is at least that
  version; false with `NotSupported` (or `NotFound` when the tool is missing).
- `static ToolVersionInfo ToolVersion(string tool)`: the same for any tool name or path.

```csharp
if (!clang.VersionCheck(16, 0, 0))
	Msg.PrintAndAbort("clang 16 or newer is required for C++23: " + clang.LastError.Message);
```

## Results and the summary of a build

| Verb | Property set by the call | Content |
| --- | --- | --- |
| `Compile` | `CompileResult LastCompile` | `Units`, `Queued`, `Compiled`, `UpToDate`, `Warnings`, `Errors`, `Failed`, `CompileDatabase`, `Cancelled` |
| `Librarian` | `LinkResult LastLibrarian` | `Output`, `UpToDate`, `Objects`, `Warnings`, `Errors`, `Diagnostics` |
| `Linker` | `LinkResult LastLinker` | `Output`, `UpToDate`, `Objects`, `Shared`, `Warnings`, `Errors`, `Diagnostics` |
| `Format` | `FormatResult LastFormat` | `Files`, `Failed`, `Diagnostics` |
| every verb | `ApiError LastError`, `string LastOutput` | the failure (`NotFound`, `Failed`, `InvalidArgument`, `NotSupported`, `IoError`) and the text of the tools |
| the run | `ClangStatus Clang.Status` | `Queued`, `Completed`, `Warnings`, `Errors` accumulated over every compile, link, archive and format of the script and its child scripts; `Reset()` starts over, `Print()` prints them in one line |

The results are the material of the summary a build script prints or writes at the end: the
extension writes no report of its own. `Clang.Status` keeps the numbers only; the diagnostics
stay in the result of the verb that produced them. The extension is a module, loaded once per
run, so the status is one set of numbers every script of the run adds to, child scripts
included; `Reset()` in the main script starts the count of a build.

```csharp
int build(string[] args) {
	Clang.Status.Reset();
	Clang clang = new Clang();
	clang.Options.AbortOnFailure = false;
	clang.Options.Output = ClangOutput.Progress;
	KList src = Glob("src/*.cpp");
	KList obj = src.WithExtension(clang.Options.ObjectExtension).WithPrefix("out/tmp/");
	ToolResult r = clang.Compile(src, obj);
	if (r.Status != ToolStatus.Failed)
		r = clang.Linker(obj, "out/bin/myapp" + clang.Options.BinaryExtension);
	// A summary of this build, from the results
	CompileResult c = clang.LastCompile;
	Msg.Print("units: " + c.Units.Count + ", compiled " + c.Compiled + ", up to date " + c.UpToDate + ", failed " + c.Failed.Count);
	foreach (CompileUnit u in c.Failed)
		Msg.PrintError("  " + u.Source + ": " + u.Errors + " errors");
	if (clang.LastLinker != null)
		Msg.Print("link: " + (clang.LastLinker.UpToDate ? "up to date" : clang.LastLinker.Output));
	// Or the counters of the whole run, child scripts included, in one line
	Clang.Status.Print();
	return r.Status == ToolStatus.Failed ? 1 : 0;
}
```

## Up to date checks

Every output (an object, an archive, an executable) gets a record, a small text file with the
hash of its command line and, for every input, its content hash, its date, its size and its
path. The record of an object lives next to it (`<object>.kdep`); the record of an archive or an
executable lives in the folder of its first object, named after the output, so the output folder
holds nothing but what is shipped. The output is made again when the record is missing, when the
command line differs, when an input is missing or when an input changed. An input whose date and
size are those of the record counts as unchanged without being read; one whose date or size
moved is read and its content hash compared, and only a different hash builds. Every file is
looked at once per verb call, whatever the number of units that include it, so a check of
hundreds of units with the system headers tracked takes a fraction of a second. The inputs of a
unit are its source and every header of the dependency file the compiler wrote with `-MD`
(system headers included, so a changed SDK recompiles what uses it); of a resource unit, the
files its script names; of an archive, its objects; of a link, its objects and the libraries of
the options found in the library paths. Two guarantees follow: a file touched without an edit (a
branch switched and switched back, a checkout, a copy) builds nothing, neither the unit nor the
archive and the link behind it, and an edit dated older than the output still builds. The one
edit that passes unseen is one that keeps both the date and the size of the file. An output
without record but present is checked by dates the way the previous extension did and gets its
record when it passes, so an upgrade does not rebuild everything once.

On Windows, the objects of the MSVC target carry the time of their compile unless the units are
compiled with `-mno-incremental-linker-compatible`: without that switch, a unit compiled again
from the same code gives different bytes, and the archive and the link behind it are made again.

## Enumerations

| Enumeration | Values |
| --- | --- |
| `ClangOutput` | `Silent`, `Progress`, `Detailed` |
| `DuplicateSymbolCheck` | `Ignore`, `Warn`, `Fail` |
| `UnitStatus` | `UpToDate`, `Compiled`, `Warnings`, `Failed`, `Skipped` (an unknown extension, or not run because the batch was cancelled) |

[Back to the extensions index](../extensions.md)
