
## [Unreleased]
- [Feature] `#load` and child script resolution is now deterministic: including file directory, script directory, current directory, backward trace and tool directory, in that order. The recursive forward search (walk of every subfolder, first match wins) no longer runs by default. With repos that embed other repos sharing the same relative layout it could silently bind a foreign copy of a helper, and the state cache then persisted the wrong bind
- [Feature] New `-kforward` switch re-enables the forward search as a deprecated bridge; every forward hit prints a warning naming the source and the resolved file. Without the switch, a reference that only the forward search could satisfy fails the compile naming the file it would have picked and how to fix it
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