## [Unreleased]

- [Feature] Progress reporting: `ITaskProgress` contract with bar, dots and plain renderers, selectable per facility (`Http.Progress`, `Progress.Default`). See doc/api.md, Progress
- [Feature] `Folders.Copy` honors `CopyOptions.ShowProgress` through `Folders.Progress`
- [Feature] `Compress.Zip` and `Compress.Tar` show a progress line through `Compress.Progress`; `Compress.ShowProgress` or the `showprogress` argument silence it. See doc/api.md, Compress
- [Feature] Error reporting: every facility exposes `LastError` (`ErrorCode` and message) so scripts explain failures themselves. See doc/api.md, Error reporting
- [Feature] `Engine.ForwardSearch`, `Engine.RebuildScripts` and `Engine.LastError`: the script side of `-kforward` and `-ksrb`, and the reason of a failed child script. See doc/api.md, Engine settings
- [Bugfix] `Compress.Tar.Decompress` creates the destination folder and returns false when entries are refused or not extracted
- [Bugfix] Failed compressions leave no partial archive; xz compression returns false instead of aborting
- [Misc] Zip extraction goes entry by entry like tar: refused (path traversal) and failed entries are reported, existing files with overwrite disabled are skipped and reported as `AlreadyExists` (tar too); archive entries always use `/`
- [Misc] `Folders.SetCurrentFolder`, `Folders.CurrentFolderPop` and `KValue.Export` return bool
- [Misc] Engine messages a script can handle through its return value or `LastError` are logged at verbose level only
- [Misc] Child script failures (not found, unresolved references, compile errors, missing action) print nothing: `Kombine()` returns 1 and `Engine.LastError` carries the reason
- [Misc] Forward search hits (`-kforward`) are logged at verbose level instead of printing a warning
- [Misc] Documentation: extensions split into one page each (doc/extensions/), building guide with the root script actions, progress and error reporting guides, readme index with direct links, usage output and exit code contract
- [Misc] Examples: progress example, `LastError` checks in the folders, types and network examples, `#load` resolution example running both forward search modes

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
