[Back to the readme](../readme.md)

# Kombine Extensions

Extensions are regular Kombine scripts located in the [extensions/](../extensions/) folder.
They encapsulate reusable functionality (a tool wrapper, a code generator, an API client)
so it can be shared between projects. Every extension has its own page, listed below.

## Loading an extension

Include an extension in your script with the `#load` directive, before any regular
statement:

```csharp
#load "extensions/clang.csx"
```

A relative path is resolved like any `#load`, in this order: the folder of the script
that contains the directive, the script folder, the current working directory, the parent
folders up to the root, and the tool folder. So `extensions/clang.csx` works from anywhere
below the repository root. The forward search of the subfolders is disabled by default,
since it could bind a foreign copy of a helper when repositories are nested; the
`-kforward` switch (or `Engine.ForwardSearch` from a script) enables it.

An URL can be used as well; the file is fetched and cached:

```csharp
#load "https://raw.githubusercontent.com/kollective-networks/kltv.kombine/main/extensions/clang.csx"
```

## Provided extensions

| Extension | Documentation | Description |
| --- | --- | --- |
| [clang.csx](../extensions/clang.csx) | [clang.md](extensions/clang.md) | Clang toolchain: compile, static libraries, executables and shared libraries, clang-format, compile_commands.json; output modes, results per verb, up to date checks by content. Requires Kombine 1.6. |
| [clang.doc.csx](../extensions/clang.doc.csx) | [clang.doc.md](extensions/clang.doc.md) | Generates Markdown documentation from clang-doc YAML output. |
| [git.csx](../extensions/git.csx) | [git.md](extensions/git.md) | Git for build scripts: one verb per git command (clone, pull, fetch, checkout, submodules, push, status, info, diff, patch, tags, archives, worktrees, bundles, hooks...), results in `Git.Last<Verb>`, output modes, authentication inheritance. Requires Kombine 1.6. |
| [github.csx](../extensions/github.csx) | [github.md](extensions/github.md) | GitHub releases: create, upload assets, publish. |
| [bin2cpp.csx](../extensions/bin2cpp.csx) | [bin2cpp.md](extensions/bin2cpp.md) | Embeds binary files into C++ sources as byte arrays; up to date by content, results and output modes. Requires Kombine 1.6. |
| [bin2obj.csx](../extensions/bin2obj.csx) | [bin2obj.md](extensions/bin2obj.md) | Embeds binary files into object files as data sections (COFF, Mach-O on macOS); up to date by content, reproducible objects, results and output modes. Requires Kombine 1.6. |
| [modder.csx](../extensions/modder.csx) | [modder.md](extensions/modder.md) | Applies (and verifies) modifications over downloaded third-party sources. |
| [dotnet.doc.csx](../extensions/dotnet.doc.csx) | [dotnet.doc.md](extensions/dotnet.doc.md) | Converts a C# XML documentation file into Markdown. |

Each page lists the members of the extension, shows a usage example and links to the
example that demonstrates it.

## Writing an extension

An extension is a plain `.csx` file with a class, or static helpers, that scripts load
with `#load`. Some conventions of the provided ones, worth keeping:

- Return values the script can act on (`bool`, `ToolResult`, a result class) instead of
  aborting; abort only when asked through an `abortwhenfailed` style parameter.
- Print with `Msg`, so the output honors the indentation and the log level, and report
  long operations through the [progress reporters](api.md#progress) with a `Progress`
  member the script can assign.
- Guard OS specific parts with `Host.IsWindows()`, `Host.IsLinux()` and `Host.IsMacOS()`,
  and run tools with `Exec` or `Tool` rather than through a shell.
- Share defaults with child scripts through `Share.Set` / `Share.Get`, the way
  `ClangOptions.SetAsDefault()` does.

## Contributing

These extensions are not exhaustive: they cover what the examples and this repository
need. If you create an extension that could be useful for others, please share it;
everything is welcome.

[Back to the readme](../readme.md)
