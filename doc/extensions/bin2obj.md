[Back to the extensions index](../extensions.md)

# bin2obj.csx

Generates object files embedding binary files as data sections, one object per file or one
object for all of them, so assets can be linked without an intermediate C++ source. The format
follows the host, COFF on Windows, ELF on Linux, Mach-O 64 bit on macOS, and the machine its
architecture; `Format` and `Machine` set them to anything else. Requires Kombine 1.6 (the file
declares it with `#pragma kombine requires 1.6`). Demonstrated and tested by
[examples/06.extensions/05.bin2obj](../../examples/06.extensions/05.bin2obj/).

```csharp
#load "extensions/bin2obj.csx"
```

## How it works

- **Two forms of one verb.** `Generate(bin, obj)` with two lists writes one object per binary;
  `Generate(bin, obj)` with one output writes a single object holding every binary. Both return
  true when every output is in place and false otherwise, with the reason in `LastError`
  (`NotFound` for a missing binary, `InvalidArgument` for mismatched lists, duplicate names, an
  unknown or empty machine or a machine the format does not take, `Failed` for a file that could
  not be written) and what was done in `LastGenerate`.
- **Three formats, the same layout.** One data section holds every binary followed by its size,
  with a symbol for the block and one for its size. COFF objects (`.data` section, read only) for
  the x86, x64, arm and arm64 machines; ELF relocatable objects (`.rodata` section, 32 bit for x86
  and arm, 64 bit for x64 and arm64, no relocations) that GNU ld, gold and lld link; Mach-O 64 bit
  objects (`__DATA,__data`) for x64 and arm64, with the leading underscore the platform adds to
  its symbols. The defaults follow the host, so a script needs nothing for a native build and
  sets `Format` and `Machine` for a cross build.
- **Up to date by content.** An output is generated again when it or its record (`<output>.kdep`,
  next to it) is missing, when what generates it changed (the layout version of the extension,
  the symbol and friendly names, the list of inputs of a single output, `Format` and `Machine`) or
  when an input changed. An input whose date and size match the record counts as unchanged
  without being read; one whose date or size moved is read and its content hash compared. A file
  touched without an edit generates nothing; an edit dated older than the output still
  generates; the one edit that passes unseen keeps both the date and the size of the file.
- **Reproducible objects.** No header carries a timestamp, so an object generated again from the
  same data has the same bytes, and the archive or the link behind it is not made again.
- **A failure leaves the previous output intact.** An output is written to a temporary file and
  moved into place once complete; no partial file is ever left.
- **The symbol derives from the path as given**, so a script should pass its binaries through
  relative paths and keep them stable: a path given differently is a different symbol, and the
  output is generated again with it. `Symbols` holds the friendly name (the path with dots, lower
  case) and the symbol of every input of the last call, generated or not; `<symbol>_size` names
  the length of each block.
- **The size is a 32 bit value** in every format. Declare it as `unsigned int` (or `uint32_t`), not
  as `unsigned long`, which is 8 bytes on Linux and macOS and would read past it:

```c
extern "C" const unsigned char varlogo_png1234567890[];
extern "C" const unsigned int  varlogo_png1234567890_size;
```

```csharp
#load "extensions/bin2obj.csx"

int assets(string[] args) {
	Bin2obj generator = new Bin2obj();
	generator.AbortOnFailure = false;
	KList bin = Glob("assets/*.png");
	KList obj = bin.WithExtension(".o").WithPrefix("out/gen/");   // the extension is the script's choice
	if (!generator.Generate(bin, obj)) {
		Msg.PrintError("assets not generated: " + generator.LastError.Message);
		return 1;
	}
	return 0;
}
```

## Settings

| Member | Default | Meaning |
| --- | --- | --- |
| `ObjectFormat Format` | the host's | The format of the objects: `Coff`, `Elf` or `MachO`. The default is `HostFormat()`: `Coff` on Windows, `MachO` on macOS, `Elf` elsewhere. A change generates the outputs again. |
| `string Machine` | the host's | The machine of the objects: `x86`, `x64`, `arm` or `arm64` (arm64 pads every block to four bytes). The default is `HostMachine()`, the architecture of the operating system (not of the process, which may run emulated); empty on a host with another architecture, which fails the calls with `InvalidArgument` until it is set. Mach-O takes `x64` and `arm64` only. A change generates the outputs again. |
| `OutputMode Output` | `Progress` | What reaches the console: `Silent` (nothing), `Progress` (one progress line per call through `Progress`, ended with `ok`, `ok (up to date)`, `ok (n up to date)` or `failed (n)`), `Detailed` (one line per file, as the previous version printed). The per file lines always go out, at verbose level in the other modes, so a `Msg.OnMessage` handler receives them whatever the mode. |
| `ITaskProgress? Progress` | null, the engine default | The reporter of the progress line; assign a `ProgressBar`, `ProgressDots` or `ProgressPlain` (see [Progress](../api.md#progress)). |
| `bool AbortOnFailure` | true | A failing call prints its reason and aborts the script; false returns false with the reason in `LastError`. |
| `string TaskLabel` | empty | The start message of the progress line; empty uses "Generating N files" or "Generating <output>". |
| `ApiError LastError` | none | The reason of the last failure. Reset by every call. |
| `Result? LastGenerate` | null | What the last call did: `Entries` (each with `Source`, `Output`, `Symbol`, `FriendlyName`, `Status` and `Message`), `Generated`, `UpToDate`, `Failed`, `Single`. |
| `Dictionary<string, string> Symbols` | empty | Friendly name and symbol of every input of the last call. |

`EntryStatus`: `UpToDate`, `Generated`, `Failed`, `Skipped` (not attempted because an earlier
entry failed with the abort in effect).

`Bin2obj.HostFormat()` and `Bin2obj.HostMachine()` return the defaults, so a script can start
from them: `generator.Machine = Bin2obj.HostMachine() == "arm64" ? "arm64" : "x64"`.

## Verbs

| Method | Description |
| --- | --- |
| `bool Generate(KList bin, KList obj)` | One object per binary, in the same order and number (`InvalidArgument` otherwise). The outputs that are up to date are left alone. |
| `bool Generate(KList bin, KValue obj)` | One object holding every binary, in the order of the list, each block followed by its size; a file added, removed or moved in the list generates it again. |

[Back to the extensions index](../extensions.md)
