[Back to the readme](../readme.md)

# How Kombine runs a script

This document explains what happens between `mkb build` and the exit code: how the script
is found, how its references are resolved, when it is compiled and when a cached build is
used, and how it runs. It is about the tool at work, not about writing scripts (see the
[readme](../readme.md)) nor about building the tool itself (see [Building Kombine](building.md)).

- [The script and the action](#the-script-and-the-action)
- [Resolving references](#resolving-references)
- [Compiling](#compiling)
- [The cache](#the-cache)
- [Running](#running)

## The script and the action

```text
mkb [parameters] [action] [action parameters]
```

The script is `kombine.csx` in the working folder unless `-kfile:` names another one. The
working folder is switched to the folder of the script before anything else, so a script
always runs from its own folder whatever folder `mkb` was invoked from; the original folder
is restored when the script ends.

A script has two parts: the code at the top level, which runs every time the script is
evaluated, and the actions, the functions that receive the action parameters and whose
return value is the exit code. Evaluation comes first, then the action named on the command
line (see [Script structure](../readme.md#script-structure-and-execution) and the
[exit codes](../readme.md#exit-codes)). The actions `khelp`, `kversion`, `kconfig` and
`kcache` belong to the tool and never reach a script.

## Resolving references

A reference is a `#load` line, a `#r` line or the script name given to `Kombine()` when its
`search` parameter is true. It is resolved before compiling, in this order, the first match
winning:

1. An URL (`http://`, `https://`): fetched the first time it is loaded in the run and again
   when a rebuild is asked (`-ksrb`, `Engine.RebuildScripts`); the cached copy is used
   otherwise, so a normal run makes no request. When the fetch fails the cached copy is
   used when there is one.
2. An absolute path: taken as it is.
3. The folder of the file that contains the reference, when it is a loaded file itself (a
   helper loading another helper next to it).
4. The folder of the script being compiled.
5. The working folder.
6. The parent folders of the script folder, up to the root.
7. The folder of the `mkb` executable, where the shipped extensions live.
8. The subfolders of the script folder, recursively, only when the forward search is enabled
   with `-kforward` or `Engine.ForwardSearch`.

The forward search is disabled by default on purpose: with repositories that embed other
repositories sharing the same layout it can bind a foreign copy of a helper, and the cache
would then keep that binding until the script is compiled again. When a reference is found
only through the forward search while it is disabled, the failure reason names the file
and how to enable it (`Engine.LastError` for a child script, verbose output otherwise).

`#r "mkb.dll"` lines, the ones that give the editor its intellisense, resolve to the
running engine assembly by name, on every platform.

## Compiling

The script is compiled in memory with the C# compiler (Roslyn) into an assembly that is
executed in the `mkb` process. Before compiling, the `#pragma kombine requires` lines of the
script, of its loaded files and of its child scripts are checked, so an older engine reports
the version it lacks instead of compiler errors.

A file brought in with `#load` is merged into the script that loads it and compiled with it,
so every script gets its own copy of its types, variables and functions. A file that declares
`#pragma kombine module` is compiled once per run as its own assembly, cached by content and
shared by every script of the run that loads it; the shipped extensions are modules. What
that changes for the module file is described in
[Loaded files and modules](api.md#loaded-files-and-modules).

`-ksdbg` (and `-ksdbgw`) compiles with debug information, which is what a debugger needs to
bind breakpoints (see the [debugging guide](debug.md)). A build with debug information is
not used by a run without it, nor the other way around: the script is compiled again with
what the run needs.

A script that does not compile exits with 1 and the compiler errors are printed whatever
the output level. A module that does not compile is reported once, with its own file name,
and the scripts loading it do not run.

## The cache

The result of a compilation, the assembly with what it was built from, is the state of the
script. States are kept in the cache folder of the user, `kombine/cache` under the
application data folder of the platform (`%APPDATA%\kombine\cache` on Windows,
`~/.config/kombine/cache` on Linux and macOS):

```
cache/states/<hash of the script path>.dat     the state of a script
cache/states/<hash of the module path>.mod     the state of a module
cache/scripts/<hash of the URL>.cache          a remote file loaded with #load
```

A remote file with the module pragma is a module like a local one, its state keyed by the
cached copy.

A state records the engine version, whether it was built with debug information, the content
hash of the file and the content hash of every file it depends on: for a script the files it
merges with `#load` and the modules it references; for a module the files it loads,
transitively. The state is used again when every one of those matches, so:

- Running a script again compiles nothing.
- Moving, copying or touching a script or a module compiles nothing.
- Editing a script compiles that script, and nothing else: the child scripts it runs keep their
  states.
- Editing a module compiles the module once and the scripts that load it, and no other.
- Editing a file merged with `#load` compiles the scripts that merge it.
- A new engine version, or a run with `-ksdbg` after one without it, compiles again.

`-ksrb` (or `Engine.RebuildScripts` from a script) compiles every script and module of the run
whatever their states say; `mkb kcache clear` deletes the whole cache folder. An edited module
is compiled once per run, before the first script that loads it. `Args.WasRebuilded` tells a
script it was compiled in this run, which is useful to invalidate anything the script derives
from itself.

The cache has no garbage collection yet: states of scripts that no longer exist stay until
`kcache clear` removes everything.

## Running

The compiled script is loaded in the `mkb` process: its top level code runs, then the action.
The script environment starts as a copy of the system environment; the values a script
exports with `KValue.Export` are added to it and passed to every tool the script launches.

A child script run with `Kombine()` runs in the same process, in the same way: its
references are resolved, its own state is used or compiled, its working folder is switched to
its folder while it runs and restored after. It starts with a copy of the exported values and
of the shared objects of its parent, so what the child exports does not reach the parent
(see [Executing child scripts](../readme.md#executing-child-scripts-and-sharing-values)).
Its messages are indented one level. Its exit code is returned to the parent; when the child
could not run at all, the parent gets 1 and the reason in `Engine.LastError`.

Ctrl+C kills the tools the script launched, with their children, and exits with 130.
