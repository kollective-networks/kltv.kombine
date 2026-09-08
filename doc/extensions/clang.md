[Back to the extensions index](../extensions.md)

# clang.csx

Wraps the clang toolchain (`clang`, `clang++`, `llvm-ar`, `llvm-rc`, `clang-format`):
compile, link, static libraries, formatting and the `compile_commands.json` database.
Demonstrated in [examples/06.extensions/00.clang](../../examples/06.extensions/00.clang/)
(static library, shared library and executable).

```csharp
#load "extensions/clang.csx"
```

## Clang.ClangOptions

Holds the toolchain configuration. The constructor selects the platform artifact
extensions automatically (`.lib/.dll/.exe/.obj` on Windows, `.a/.so/.out/.o` on Linux,
`.a/.dylib/.out/.o` on macOS) and sets `ConcurrentBuild` to the number of CPU cores.

| Member | Description |
| --- | --- |
| `CC`, `CXX`, `LD`, `AR`, `RC` | Tool executable names (`clang`, `clang++`, `clang++`, `llvm-ar`, `llvm-rc`). |
| `CExtension`, `CppExtension`, `ResExtension` | Source extensions (`.c`, `.cpp`, `.rc`). |
| `IncludeDirs`, `Defines` | Include directories and preprocessor defines. |
| `SwitchesCC`, `SwitchesCXX`, `SwitchesLD` | Extra switches for the C compiler, C++ compiler and linker. |
| `LibraryDirs`, `Libraries` | Library search paths and libraries to link. A library given as a full path is passed as is; a bare name is passed as `-l<name>`. |
| `ConcurrentBuild` | Number of concurrent compile commands (default: CPU cores). |
| `Verbose` | Passes `-v` to the tools. |
| `LibExtension`, `SharedExtension`, `BinaryExtension`, `ObjectExtension` | Platform artifact extensions (read only). |
| `void SetAsDefault()` | Publishes these options as the shared defaults (`Share.Set("ClangOptions", ...)`), so child scripts creating a `Clang` instance inherit them. |

## Clang

The constructor picks up the shared `ClangOptions` and the shared compile commands
database if a parent script published them.

| Member | Description |
| --- | --- |
| `ClangOptions Options` | The options used by this instance. |
| `ProcessFileDelegate? ProcessFile` | Optional callback invoked per file about to be compiled; returns extra arguments for that file. |
| `ToolResult Compile(KList src, KList obj, bool abortwhenfailed = true, bool rebuild = false)` | Compiles the sources into the corresponding objects (both lists must match). Only modified files are compiled, using the dependency files written by the compiler, unless `rebuild` is true. Compilation runs concurrently up to `ConcurrentBuild`. With `abortwhenfailed` false, a failed file does not stop the remaining ones, so all the errors are reported at once. |
| `ToolResult Librarian(KList objs, KValue output, bool abortwhenfailed = true)` | Creates a static library from the objects. On Linux and macOS the output name gets the `lib` prefix. |
| `ToolResult Linker(KList objs, KValue output, bool SharedLibrary = false, bool abortwhenfailed = true)` | Links the objects into an executable, or a shared library with `SharedLibrary` (with the `lib` prefix on Linux and macOS). A response file is used when the command line exceeds the platform limit. |
| `void Clean(KList obj, KValue output)` | Deletes the object folders and the output artifact folder. |
| `static bool Format(KList src, string extraArgs, bool abortwhenfailed = true)` | Formats the given files in place with `clang-format`. |
| `bool OpenCompileCommands(KValue file)` | Opens (or creates) a `compile_commands.json` database and shares it, so every `Clang` instance in this script and its children appends to the same file. |

Every operation returns the `ToolResult` of the tool execution: check `Status` and
`ExitCode`, or let `abortwhenfailed` stop the script.

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

[Back to the extensions index](../extensions.md)
