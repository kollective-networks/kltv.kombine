## [Unreleased]

### Breaking changes

What no longer compiles or no longer runs, why, and what to change.

- **Clang extension: `Format` is an instance method.** `Clang.Format(files, args)` no longer compiles: the verb runs through the tool runner with the options of an instance and leaves its result in `LastFormat`. Call it on the instance the script already has, or on `new Clang()`.
- **Clang extension: `ClangOptions.LD` names the linker, not the driver.** The link is always driven by the C++ compiler of the options (`CXX`), and `LD` is passed to it as `-fuse-ld=<LD>` (`lld` by default, what the previous version forced on every link). A script that set `LD` to a compiler driver ("clang++", "clang++-16", "g++") now fails the link with "invalid linker name". Put the driver in `CXX` and in `LD` the linker name (`lld`, `gold`, `bfd`, `mold`, or a path), or an empty string to let the driver choose.
- **Bin2obj and bin2cpp: no `WasProcessed`.** Copies of the extensions that carried a `WasProcessed` member find nothing under that name: `LastGenerate.Generated` says how many outputs the call wrote and `LastGenerate.Entries` what happened to each input.
- **Git extension: the fourth parameter of `Clone` is the options object.** `Clone(uri, path, branch, showdettach)` took a bool; it takes a `CloneOptions` now, so a call passing the bool positionally no longer compiles. Use `Git.Clone(uri, path, branch, new CloneOptions { DetachedHeadAdvice = true })`; calls with three arguments compile unchanged.

### Features

- Modules: a loaded file that declares `#pragma kombine module` is compiled once per run as its own assembly, cached by its content and shared by every script that loads it, one copy of its types and statics for the run; the shipped extensions are modules. Two different copies of one module in a run are warned about, `-kmodules:strict` makes it a failure. See doc/api.md, Loaded files and modules
- The cache goes by content: a script or a module is compiled again when its content or the content of a file it loads changed, a touched or moved file compiles nothing, an edited file compiles only what loads it. See doc/building.md, The cache
- `Msg.OnMessage`: a delegate that receives every message of the engine and of every script of the run, with its level, kind, module and indentation, before it is written to the console, so a script filters the log and forwards it to another facility or log system. See doc/api.md, Logging
- The clang, git, bin2cpp and bin2obj extensions emit their detailed lines whatever their output mode, at verbose level in `Progress` and `Silent`, so a `Msg.OnMessage` handler receives the whole detail while the console shows the progress line
- `Tool.OnStdout` and `Tool.OnStderr` deliver the output fragments while a command runs; `Tool.Timeout` kills a command that runs longer, synchronous or queued; `Tool.CancelCommands()` cancels a running batch. See doc/api.md, Tool
- Clang extension rewritten: output modes (`Silent`, `Progress`, `Detailed`) with the diagnostics printed after the build naming the offending files, results in `LastCompile`, `LastLibrarian`, `LastLinker`, `LastFormat` and `LastError`, run wide counters in `Clang.Status`, tool presence and version checks, timeout per invocation, up to date checks by content hash and recorded command line for the units, the archives and the links, the libraries of the link and system headers tracked, duplicate symbols of an archive reported per `ClangOptions.DuplicateSymbols`. See doc/extensions/clang.md
- Bin2cpp and bin2obj extensions: outputs generated again by the content hash of their inputs and what generates them (names, list, layout version, machine) instead of the dates, a failure leaves the previous output intact, `LastError`, `LastGenerate`, output modes and `AbortOnFailure`; bin2obj objects carry no timestamp. See doc/extensions/bin2cpp.md and bin2obj.md
- `#pragma kombine requires <major.minor>`: a script or extension declares the minimum Kombine version, reported instead of compiler errors by an older engine. See doc/api.md, Script Basics
- Git extension rewritten: one verb per git command (clone, pull, fetch, checkout, submodule, push, ls-remote, status, info, diff, ls-files, check-ignore, rev-parse, merge-base, patch, add, commit, tag, archive, clean, sparse-checkout, worktree, lfs, bundle, hooks), options per verb with the previous defaults, results in `Git.Last<Verb>`, `Git.LastError`, output modes, abort on failure, authentication inheritance. See doc/extensions/git.md
- Progress reporting: `ITaskProgress` contract with bar, dots and plain renderers, selectable per facility (`Http.Progress`, `Progress.Default`). See doc/api.md, Progress
- `Folders.Copy` honors `CopyOptions.ShowProgress` through `Folders.Progress`
- `Compress.Zip` and `Compress.Tar` show a progress line through `Compress.Progress`; `Compress.ShowProgress` or the `showprogress` argument silence it. See doc/api.md, Compress
- Error reporting: every facility exposes `LastError` (`ErrorCode` and message) so scripts explain failures themselves. See doc/api.md, Error reporting
- `Engine.ForwardSearch`, `Engine.RebuildScripts` and `Engine.LastError`: the script side of `-kforward` and `-ksrb`, and the reason of a failed child script. See doc/api.md, Engine settings

### Bugfixes

- Tool: `ConcurrentCommands` let one command more than asked run at the same time (a limit of 1 ran two); the limit is exact now
- Tool: the captured output of a very short command could miss its last fragments; the exit is signaled once the output readers end
- Clang extension: a long archive is written in one `ar` command through a response file instead of several incremental ones, which could overlap and lose objects
- Clang extension: the library change detection never fired, a header whose path contains a space was skipped, a deleted header was ignored, system headers were not tracked, a missing source aborted whatever `abortwhenfailed` said, and an unknown extension left a hole the link tripped on later; every case is handled by the up to date checks and the failures of the verbs
- sdl2 extra: on Windows the generic thread folder contributes only the sources the Windows folder does not provide, as upstream does; the rest duplicated the symbols of the Windows folder inside the archive
- Git example: a step run against a failed clone could add and commit into the Kombine repository itself; the sandbox is now walled off with `GIT_CEILING_DIRECTORIES`, the group stops when the clone failed, and the test sandboxes are ignored by git
- `Compress.Tar.Decompress` creates the destination folder and returns false when entries are refused or not extracted
- Failed compressions leave no partial archive; xz compression returns false instead of aborting

### Misc

- A rebuilt script no longer rebuilds the child scripts it runs: a child is compiled again only when its own content or a file it loads changed, or with `-ksrb` / `Engine.RebuildScripts`; `Args.WasRebuilded` is true only for a script compiled in this run
- A state built with `-ksdbg` is not used by a run without it and the other way around: the script is compiled again with the information the run needs
- A remote file (`#load` of an URL) is fetched the first time it is loaded and again with `-ksrb`, once per run; the cached copy is used otherwise, so a normal run makes no request and an edit of the loading script no longer refetches it. A remote file with the module pragma is a module like a local one
- Clang extension: `Clang.Status` is one static set of counters for the run and the options set as default are copied as the same type, since the extension is a module; the registry container and the copy by property name go
- Clang extension: the default output mode is `Progress`, one line per verb; the per unit lines of the previous version are the `Detailed` mode and the listings print only with `Verbose`; `Verbose` no longer passes `-v` to the tools, `ClangVerbose` does
- Clang extension: `Librarian` deletes the archive and writes it again with every object of the call, so an object removed from the list leaves it; a script that built one archive from several calls passes the whole list in one call
- Bin2cpp and bin2obj: a failure aborts the script by default (`AbortOnFailure`); false is returned instead with `AbortOnFailure = false`; the per file lines are the `Detailed` output mode, `Progress` is the default
- Git extension: `Pull` only fast forwards by default, a diverged branch returns false with `Different` (`PullMode.Merge`, `Rebase` or `Reset` ask for the rest); `GIT_TERMINAL_PROMPT=0` is exported when the extension loads, so a missing credential fails at once with `AccessDenied` instead of prompting
- Tool: `ConcurrentCommands` is exact: 1 runs the queued commands one at a time, N runs N, 0 runs every one at once (0 used to mean one at a time)
- Every extension declares its minimum Kombine version with `#pragma kombine requires`: 1.6 for clang, git, bin2cpp and bin2obj, 1.5 for clang.doc, dotnet.doc, github and modder
- Clang extension: the up to date checks look at every file once per call and read only the files whose date or size moved; the records are compact text files and the compile database is indexed, so a check of hundreds of units with the system headers tracked takes a fraction of a second; the record of an archive or an executable lives in the folder of its first object, not next to the output. The records of the earlier 1.6 development builds are made again once
- Clang extension: a rebuilt script no longer forces a rebuild of every unit, the recorded command lines decide; the dependency files are generated with `-MD`
- Tool: a batch continues at once when a command completes instead of at the next 10 ms poll
- Bin2cpp and bin2obj: an input whose date and size match the record is not read again; bin2cpp writes large assets many times faster
- Git extension: `Patch` is implemented, the version is read once per run, the git example tests every verb by groups
- `Engine.LastError` of a child script aborted with `Msg.PrintAndAbort` carries the abort message; the line `Kombine()` prints for such a child says "(aborted)" instead of repeating the reason the child printed
- Zip extraction goes entry by entry like tar: refused (path traversal) and failed entries are reported, existing files with overwrite disabled are skipped and reported as `AlreadyExists` (tar too); archive entries always use `/`
- `Folders.SetCurrentFolder`, `Folders.CurrentFolderPop` and `KValue.Export` return bool
- Engine messages a script can handle through its return value or `LastError` are logged at verbose level only
- Child script failures (not found, unresolved references, compile errors, missing action) print nothing: `Kombine()` returns 1 and `Engine.LastError` carries the reason
- Forward search hits (`-kforward`) are logged at verbose level instead of printing a warning
- Documentation: extensions split into one page each (doc/extensions/), building guide with the root script actions, progress and error reporting guides, readme index with direct links, usage output and exit code contract
- Examples: progress example, `LastError` checks in the folders, types and network examples, `#load` resolution example running both forward search modes

## [1.5.24359387]

- [Feature] Added -ksdbgw flag: waits for a debugger to attach before executing the action, enabling script debugging with the single-file executable
- [Feature] Added a debugging guide (doc/debug.md) covering Visual Studio and VSCode
- [Bugfix] Http downloads now report failures: DownloadFile/DownloadFiles return false on HTTP errors, store the status code in LastReturnCode and leave no partial files behind
- [Bugfix] Files.Copy now overwrites existing destination files so incremental copies work
- [Bugfix] Tar compression with includeFolder no longer stores absolute paths inside the archive
- [Bugfix] Tool: fixed a race with fast-exiting processes being reported as failed
- [Bugfix] Tool: CommandSync now honors the ExpectedExitCode property
- [Bugfix] Tool: queued commands that fail to launch now downgrade the global result to failed
- [Bugfix] KValue: path slash conversion to unix style was not being applied
- [Bugfix] Clang extension: verbose and shared switches are applied per invocation and no longer pollute the shared options (linking a binary after a shared library produced a shared binary)
- [Bugfix] Git extension: Clone and Pull now return false when git reports an error; Patch is marked as not implemented yet
- [Bugfix] Script aborts now always print the reason regardless of the log level (Share/Registry with ExitIfError, child script errors)
- [Bugfix] Folders.Delete now returns false when the folder itself could not be deleted
- [Bugfix] KList + and - operators return a new list and no longer modify the left operand
- [Bugfix] KValue.Equals no longer throws when compared against non KValue types; ToArgs now handles single token quoted arguments
- [Bugfix] Cast now copies the properties present in both source and target (it compared the target against itself) and skips incompatible properties instead of crashing
- [Bugfix] Remote scripts (#load by http) are cached by origin URL; the cache key no longer depends on the working directory
- [Bugfix] Tool output is captured as UTF8 so non ascii output is not mangled
- [Bugfix] Tool: queued command results are bound to the exact command, no longer matched by id (null or duplicated ids cross assigned results)
- [Bugfix] Http.GetDocument now stores the real HTTP status code in LastReturnCode on failures
- [Bugfix] Clang extension: with abortwhenfailed false a failed compile no longer cancels the remaining files, so all errors are reported at once
- [Bugfix] Git extension: Add now launches git directly instead of through cmd, so it works on any OS, captures the git output and reports errors (shell launches cannot redirect output and the exit code was being discarded)
- [Bugfix] Exec/CommandSync with argument arrays quote arguments containing spaces automatically
- [Bugfix] Files.GetModifiedTime returns 0 (with a diagnostic warning) for missing files instead of a bogus timestamp
- [Misc] Documentation overhaul: rewritten API reference (doc/api.md), new extensions documentation (doc/extensions.md), reformatted readme, feature list and third-party licenses
- [Misc] Publish no longer regenerates doc/api.md from the XML documentation (it is hand-curated now)
- [Misc] Removed outdated example leftovers (clang, msys2, sdl2) and fixed intellisense references in the relocated examples
- [Feature] `#load` and child script resolution is now deterministic: including file directory, script directory, current directory, backward trace and tool directory, in that order. The recursive forward search (walk of every subfolder, first match wins) no longer runs by default. With repos that embed other repos sharing the same relative layout it could silently bind a foreign copy of a helper, and the state cache then persisted the wrong bind
- [Feature] New `-kforward` switch enables the forward search of the subfolders when resolving `#load` and child scripts, disabled by default since it can bind a foreign copy of a helper when repositories are nested. Without it, a reference that only the forward search could satisfy fails naming the file it would have picked and how to fix it
- [Feature] Every `#load` reports its resolved absolute path at verbose level, so a wrong binding is visible instead of silent
- [Feature] `mkb -h` and `mkb --help` now act as aliases for the `khelp` action
- [Feature] Help output shows the engine banner and version; local and debug builds report the version as "development"
- [Feature] Process exit codes follow a documented contract: 0 on success, 1 for any failure (script errors, unhandled script exceptions, unknown actions, missing scripts, unimplemented `kconfig`), 130 when canceled with Ctrl+C
- [Bugfix] Build scripts that only produce warnings (for example nullable annotations) no longer abort compilation; only real errors stop the build
- [Bugfix] Async command queue: an immediate process-spawn failure is now retried up to three times with a short backoff before giving up, and the final failed result is recorded so it counts against the batch instead of vanishing silently
- [Bugfix] A queued command that produced no result now fails the whole batch (error + exit code -1) instead of being logged only at verbose level and treated as success
- [Bugfix] Fixed Roslyn 5.0.0 crashes on .NET 10.0 (concurrent build, nullable options)
- [Bugfix] Fixed `Directory.Build.props` file-name casing so the build works on case-sensitive file systems (Linux)
- [Security] Updated SharpCompress 0.42.0 → 0.49.1, clearing a medium-severity zip-slip / directory-traversal advisory (GHSA-6c8g-7p36-r338) that affected archive extraction in versions up to 0.47.4
- [Security] Tar extraction now refuses any entry whose path escapes the destination folder, closing the manual long-name and directory branches that bypassed the library guard; covered by a new round-trip + zip-slip rejection example test
- [Updated] .NET target framework from 8.0 to 10.0
- [Misc] Launch-failure diagnostics now name the command, and the "could not launch" message prints at normal level
- [Misc] Warning-clean build: dropped the framework-provided System.Text.Json package reference and fixed an inexact stream read (CA2022) in the file-content comparison
- [Misc] Examples: added `00.base/mkb.exitcodes.csx` (exit-code contract) and `08.loadresolution` (two repos sharing the same helper layout, one embedded inside the other's dependency folder, asserting each script binds its own repo's helpers); the SDL2 example disables `-msse3` on ARM64 and filters sources per platform; the MSYS2 extras test is skipped on non-Windows hosts
- [Feature] bin2obj extension: added Mach-O 64-bit object output (x86_64 and arm64) so binary embedding works on macOS; COFF output on other hosts is unchanged
- [Misc] Examples: exit code, built in types, child scripts, #load resolution, files / folders / compression and network tests rewritten with per check reports and colored OK / FAILED tags, child tool output is no longer dumped on screen; the network test downloads the project release assets (verified by size) and checks the expected failures against missing urls, the previous 1GB test file sits behind a bot check and returns 403

## [1.4.24072788]
- [Feature] Added methods in Http API to support uploads and credentials
- [Feature] Improved build system to automatically publish a release
- [Feature] Added a github.csx extension to manage github interaction
- [Bugfix] Kombine state now also keeps track of loaded dependencies to trigger rebuild if required
- [Feature] Added a bin2cpp extension to convert binary files to C++ source code
- [Feature] Added a bin2obj extension to convert binary files to object files
- [Feature] Added a modder extension to apply mods to other projects
- [Feature] Improved examples
- [Feature] Added static function to generate build numbers
- [Bugfix] Fixed HTTP file upload was multipart and was causing issues.

## [1.3.24494684]
- [Fixed] Documentation
- [Fixed] Upgraded dependencies. Now uncompressing operations do not fail.
- [Feature] File copy now accepts a file mask

## [1.2.24435852]

- [Feature] Added version function
- [Feature] Added internal Yaml parser
- [Feature] Improved and clang extensions
- [Security] Updated dependencies
- [Feature] Added a clang.doc extension
- [Misc] Improved examples (like msys2 packages)

## [1.1.24259864]

- [Bugfix] Fixed a potential deadlock when a set of parallel async tasks wants to be cancelled (for example on clang build failed)
