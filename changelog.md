
## [1.4.24068768]
- Added methods in Http API to support uploads and credentials
- Improved build system to automatically publish a release
- Added a github.csx extension to manage github interaction
- Kombine state now also keeps track of loaded dependencies to trigger rebuild if required
- Added a bin2cpp extension to convert binary files to C++ source code
- Added a bin2obj extension to convert binary files to object files
- Improved examples

## [1.3.24494684]
[Fixed] Documentation
[Fixed] Upgraded dependencies. Now uncompressing operations do not fail.
[Feature] File copy now accepts a file mask

## [1.2.24435852]

[Feature] Added version function
[Feature] Added internal Yaml parser
[Feature] Improved and clang extensions
[Security] Updated dependencies
[Feature] Added a clang.doc extension
[Misc] Improved examples (like msys2 packages)

## [1.1.24259864]

[Bugfix] Fixed a potential deadlock when a set of parallel async tasks wants to be cancelled (for example on clang build failed)