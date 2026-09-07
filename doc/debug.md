[Back to the readme](../readme.md)

# Debugging Kombine Scripts

Kombine scripts are compiled to real .NET assemblies and executed in-process by the
`mkb` tool, so debugging a script means debugging the `mkb` process with a managed .NET
debugger: breakpoints in your `.csx` files, stepping, watches, everything works —
including files included with `#load`.

## Requirements

- **Debug information for the script.** Pass `-ksdbg` (or `-ksdbgw`, see below) so the
  script is compiled with debug information. Without it, breakpoints will not bind.
  Remember the compiled script is cached: the flag triggers a rebuild with debug info
  when needed.
- **A managed .NET debugger:**
  - **Visual Studio 2022** (any edition) with the .NET desktop development workload.
  - **Visual Studio Code** with the official [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)
    (it provides the `coreclr` debugger).
- **The right tool build for your method.** The single-file `mkb` executable can be
  debugged by **attaching** to it, but debuggers fail to **launch** it (see
  [Known issues](#known-issues-single-file-executables) below). For the launch (F5)
  workflow, use the self-contained but **not** single-file build:
  [Windows](https://github.com/kollective-networks/kltv.kombine/releases/latest/download/kombine.debug.win.zip),
  [Linux](https://github.com/kollective-networks/kltv.kombine/releases/latest/download/kombine.debug.lnx.tar.gz),
  [Mac OSX](https://github.com/kollective-networks/kltv.kombine/releases/latest/download/kombine.debug.osx.tar.gz).

## The -ksdbgw flag: wait for the debugger

Build scripts are short-lived, so attaching by hand is normally a race you lose. The
`-ksdbgw` flag solves it: it implies `-ksdbg` and, before executing the action, the
tool prints its process id and waits until a debugger is attached:

```text
$ mkb -ksdbgw build
Waiting for a debugger to attach. PID: 38760 (Ctrl+C to abort)
```

Attach from your IDE (instructions below), and the execution resumes automatically with
your breakpoints armed. Ctrl+C aborts the wait like any other execution. This works
with **any** build of the tool, including the single-file executable.

Note that attaching is only *required* for the single-file executable. With the
unpacked (non-single-file) debug build you can simply **launch** the tool under the
debugger with `-ksdbg` — no attach step, no `-ksdbgw` — as described in the launch
sections below.

## Debugging with Visual Studio

### Attach (recommended, works with the single-file executable)

1. Open your `.csx` script in Visual Studio and set your breakpoints.
2. Recommended: disable *Tools → Options → Debugging → Enable Just My Code*, so symbol
   resolution does not skip the script assembly.
3. From a terminal, in your script folder, run: `mkb -ksdbgw youraction`.
   The tool prints its PID and waits.
4. In Visual Studio: *Debug → Attach to Process...* (Ctrl+Alt+P), find `mkb` (use the
   printed PID).
5. **Important:** do not rely on the automatic engine detection — click *Select...* and
   explicitly choose **Managed (.NET Core, .NET 5+)**. The automatic detection is
   exactly what fails with single-file executables.
6. Click *Attach*. The tool resumes and your breakpoints hit when the script executes.

### Launch (requires the unpacked debug build)

If you prefer F5-style debugging, use the self-contained non-single-file build:

- **From a makefile project:** the `examples.debug` project in this repository shows
  the setup — a Visual Studio makefile project that launches `mkb` with the wanted
  action and `-ksdbg`, with the managed debugger attached.
- **From the command line:** `devenv /debugexe c:\path\to\mkb.exe -ksdbg youraction`
  opens Visual Studio debugging the tool with the managed engine selected up front.

### Just-in-time (Windows only)

Because scripts are plain C#, you can also trigger the debugger from the script itself:

```csharp
System.Diagnostics.Debugger.Launch();
```

placed in the global code (or inside an action) pops the Windows JIT-debugger dialog,
letting you pick a running Visual Studio instance. Handy for one-off checks; prefer
`-ksdbgw` for a repeatable workflow.

## Debugging with Visual Studio Code

### Attach (recommended, works with the single-file executable)

Add an attach configuration to your `.vscode/launch.json`:

```json
{
	"version": "0.2.0",
	"configurations": [
		{
			"name": "Attach to Kombine",
			"type": "coreclr",
			"request": "attach",
			"processId": "${command:pickProcess}",
			"justMyCode": false
		}
	]
}
```

Then:

1. Set your breakpoints in the `.csx` file.
2. Run `mkb -ksdbgw youraction` in the terminal; it prints the PID and waits.
3. Start the *Attach to Kombine* configuration and pick the `mkb` process (search by
   the printed PID).
4. The tool resumes and your breakpoints hit.

### Launch (requires the unpacked debug build)

Point a launch configuration at the non-single-file `mkb` and pass the parameters:

```json
{
	"version": "0.2.0",
	"configurations": [
		{
			"name": "C#: Debug script",
			"type": "coreclr",
			"request": "launch",
			"windows": {
				"program": "mkb.exe"
			},
			"linux":{
				"program": "mkb.out"
			},
			"osx": {
				"program": "mkb.out"
			},
			"args": [ "-ksdbg","-ko:d", "youractionhere","yourparameters" ],
			"cwd": "folder for your script",
			"console": "integratedTerminal",
		}
	]
}
```

## Known issues: single-file executables

The .NET debuggers have a long-standing problem with self-contained single-file
executables: the launch path fails to auto-detect the bundle as a managed process
(typically surfacing as `Failed to attach to process: Unknown Error: 0x80131c3c`),
while attaching with the managed engine explicitly selected works.

- [dotnet/runtime#42927](https://github.com/dotnet/runtime/issues/42927) — the original
  report about debugging self-contained single-file binaries.
- [dotnet/runtime#84428](https://github.com/dotnet/runtime/issues/84428) — closed in
  July 2024, not by a code fix but with the guidance that attaching with the
  *Managed (.NET Core, .NET 5+)* engine works; launch auto-detection remains
  unreliable.
- [dotnet/runtime#84965](https://github.com/dotnet/runtime/pull/84965) — fixed setting
  breakpoints in single-file apps with direct-mapped ReadyToRun sections (shipped in
  .NET 8).
- [Microsoft Q&A: 0x80131c3c](https://learn.microsoft.com/en-gb/answers/questions/1867578/how-to-fix-failed-to-attach-to-process-unknown-err)
  — the error still being reported on recent .NET versions when the engine is
  auto-detected.

This is why Kombine offers both routes: `-ksdbgw` + attach for the single-file
executable, and the unpacked debug build for the launch workflow.

## Tips

- Combine with the output flags: `mkb -ksdbgw -ko:d youraction` shows the tool debug
  log while you step through the script.
- Breakpoints inside scripts included with `#load` (extensions, shared scripts) work
  exactly like breakpoints in the main script.
- Child scripts executed with `Kombine()` run in the same process, so their breakpoints
  hit in the same debugging session — no extra attach needed.
- If breakpoints show as hollow, check you passed `-ksdbg`/`-ksdbgw` (the cached script
  may have been built without debug information — the flag rebuilds it) and that
  *Just My Code* is disabled.

[Back to the readme](../readme.md)
