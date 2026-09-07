[Back to the readme](../readme.md)

# Third-Party Licenses

Kombine is distributed as a single self-contained executable, so the third-party
libraries it depends on are bundled inside the binary. This document lists those
dependencies, what they are used for, and their licenses.

Kombine itself is released under the [MIT License](../license.md). All its
dependencies are MIT licensed as well, so the whole bundle is permissively licensed.

## Dependency summary

| Package | Version | License | Used for |
| --- | --- | --- | --- |
| [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/5.0.0) | 5.0.0 | MIT | Compiling the build scripts (Roslyn). |
| [Microsoft.CodeAnalysis.CSharp.Scripting](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Scripting/5.0.0) | 5.0.0 | MIT | Executing the build scripts as C# scripts. |
| [Microsoft.Extensions.FileSystemGlobbing](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/10.0.0) | 10.0.0 | MIT | File globbing (the `Glob` function). |
| [SharpCompress](https://www.nuget.org/packages/SharpCompress/0.42.0) | 0.42.0 | MIT | Tar compression and decompression. |
| [System.Text.Json](https://www.nuget.org/packages/System.Text.Json/10.0.0) | 10.0.0 | MIT | JSON reading and writing. |
| [BinaryPack](https://www.nuget.org/packages/BinaryPack/1.0.3) | 1.0.3 | MIT | Binary serialization of the script cache state. |

## Details

### Microsoft.CodeAnalysis.CSharp / Microsoft.CodeAnalysis.CSharp.Scripting

The [Roslyn compiler platform](https://github.com/dotnet/roslyn). This is the heart of
Kombine: the build scripts are C# scripts, and Roslyn is what compiles them and
executes them in-process, resolves the `#load` includes, and produces the compiled
assemblies that Kombine stores in its cache. Used by the script execution engine in
`src/exec/`.

### Microsoft.Extensions.FileSystemGlobbing

Microsoft's [file globbing library](https://github.com/dotnet/runtime). It implements
the pattern matching behind the `Glob()` static function, so scripts can gather source
files with patterns like `src/**/*.cpp` instead of listing every file. Used in
`src/api/methods/kmb.statics.cs`.

### SharpCompress

A pure C# [compression library](https://github.com/adamhathcock/sharpcompress). It
provides the tar family support of the Compression API: writing `.tar`, `.tar.gz`,
`.tar.bz2` and `.tar.lz` archives, and reading them (plus `.tar.xz`) with format
auto-detection. Used in `src/api/methods/kmb.compress.tar.cs`. Zip support does not
need it — it uses the `System.IO.Compression` classes built into the runtime.

### System.Text.Json

Microsoft's [JSON library](https://github.com/dotnet/runtime). It backs the `JsonFile`
encapsulation exposed to scripts (used, for example, by the clang extension to maintain
`compile_commands.json`) and is available to scripts directly since its namespaces are
imported by default. Used in `src/api/methods/kmb.json.cs`.

### BinaryPack

A fast [binary serialization library](https://github.com/Sergio0694/BinaryPack). It
serializes the script state files stored in the cache — the compiled script assembly,
its debug information and the file dependency list used to decide when a script must be
rebuilt. This is what makes executions after the first one fast. Used in
`src/core/core.state.cs`.

## Runtime

The self-contained package also bundles the [.NET runtime](https://github.com/dotnet/runtime)
(net8.0), which is licensed under the MIT license.

[Back to the readme](../readme.md)
