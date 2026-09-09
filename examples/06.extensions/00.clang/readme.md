# clang extension example

`mkb.ext.clang.csx` tests the clang extension (`extensions/clang.csx`) by groups, so a group whose
environment cannot be satisfied is skipped with a visible line and the rest still runs:

| Group | What it checks | Needs |
| --- | --- | --- |
| 1 Tools | presence and versions, a wrong tool name | clang, clang++, llvm-ar |
| 2 Output modes | the same build from clean in `Silent`, `Progress` and `Detailed`, shown on screen, with a warning unit and an error unit; a label, a dots reporter, the warnings as a count, a filter, `ClangVerbose`; what each mode renders through a recording reporter | |
| 3 Every verb once | compile (a resource unit on Windows), the dependency files and the records, `ProcessFile`, the compile database without the resource unit, librarian, linker (executable and shared library), format, clean, the database reused, `Clang.Status`, two instances with their own options | clang-format for the format checks |
| 4 Failures | an error unit with the others still compiled, the abort through a child script, a missing source, an unknown extension, mismatched lists, a missing object, a tool that runs but is not a compiler, a timeout, a linker error | git as the "wrong" tool |
| 5 Nothing left behind | every change that must build something builds exactly the units concerned and the archive and executables behind them: an object deleted, a source or a header edited (shared, through another header, `-isystem`, in a folder with a space), a source dated older than its object, an edit keeping date and size, a dependency file deleted, a define, an include path, a switch, the compiler name or a linker switch changed, the archive or an executable deleted, `rebuild`, a header deleted, the archive list changed, a library by path made again, a resource and the files it names on Windows | |
| 6 Nothing built without need | every change that must build nothing builds nothing: no change, a header no unit includes, a source or a header touched with its content unchanged, one unit edited (its executable made again, not the other), the unit order, options that do not reach the command, the database opened again, an empty list, an unrelated library made again | |
| 7 Child script | the status counters and the options set as default by the parent, output mode and reporter included, reach a child script that appends to the same compile database | |

Groups 5 and 6 verify each claim two ways: the counts of `LastCompile` and the dates of the
objects, the archive and the executables read before and after the call. The sources live in a
sandbox (`.tmp.clang`) written by the example and removed at the end. On Windows the example
passes `-mno-incremental-linker-compatible` so the objects of the MSVC target do not carry the
time of their compile: without it, a unit compiled again from the same code gives different bytes
and the archive and the link behind it are made again.

```
mkb test      runs every group
mkb build     builds the three sub projects (lib, exe, dll), each in its own child script
mkb clean     removes the sandbox
mkb cleanall  removes the sandbox and the output of the sub projects
```

The example is run by the `extensions` action of `examples/kombine.csx`.
