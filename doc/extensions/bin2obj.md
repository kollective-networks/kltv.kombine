[Back to the extensions index](../extensions.md)

# bin2obj.csx

Generates object files embedding binary files as data sections, one object per file or one
object for all of them, so assets can be linked without an intermediate C++ source: COFF
objects on Windows and Linux hosts, Mach-O 64 bit objects on macOS. Requires Kombine 1.6 (the
file declares it with `#pragma kombine requires 1.6`). Demonstrated and tested by
[examples/06.extensions/05.bin2obj](../../examples/06.extensions/05.bin2obj/).

```csharp
#load "extensions/bin2obj.csx"
```

## How it works

- **Two forms of one verb.** `Generate(bin, obj)` with two lists writes one object per binary;
  `Generate(bin, obj)` with one output writes a single object holding every binary. Both return
  true when every output is in place and false otherwise, with the reason in `LastError`
  (`NotFound` for a missing binary, `InvalidArgument` for mismatched lists, duplicate names or
  an unknown machine, `Failed` for a file that could not be written) and what was done in
  `LastGenerate`.
- **Up to date by content.** An output is generated again when it or its record (`<output>.kdep`,
  next to it) is missing, when what generates it changed (the layout version of the extension,
  the symbol and friendly names, the list of inputs of a single output, `Machine` and the format
  written) or when an input changed. An input whose date and size match the record counts as
  unchanged without being read; one whose date or size moved is read and its content hash
  compared. A file touched without an edit generates nothing; an edit dated older than the
  output still generates; the one edit that passes unseen keeps both the date and the size of
  the file.
- **Reproducible objects.** The COFF header carries no timestamp, so an object generated again
  from the same data has the same bytes, and the archive or the link behind it is not made again.
- **A failure leaves the previous output intact.** An output is written to a temporary file and
  moved into place once complete; no partial file is ever left.
- **The symbol derives from the path as given**, so a script should pass its binaries through
  relative paths and keep them stable: a path given differently is a different symbol, and the
  output is generated again with it. `Symbols` holds the friendly name (the path with dots, lower
  case) and the symbol of every input of the last call, generated or not; `<symbol>_size` names
  the length of each block.

```csharp
#load "extensions/bin2obj.csx"

int assets(string[] args) {
	Bin2obj generator = new Bin2obj();
	generator.AbortOnFailure = false;
	KList bin = Glob("assets/*.png");
	KList obj = bin.WithExtension(".obj").WithPrefix("out/gen/");
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
| `string Machine` | `x64` | The machine of the COFF objects: `x86`, `x64`, `arm` or `arm64` (arm64 pads every block to four bytes). The Mach-O objects take the architecture of the host. A change generates the outputs again. |
| `OutputMode Output` | `Progress` | What reaches the console: `Silent` (nothing), `Progress` (one progress line per call through `Progress`, ended with `ok`, `ok (up to date)`, `ok (n up to date)` or `failed (n)`), `Detailed` (one line per file, as the previous version printed). |
| `ITaskProgress? Progress` | null, the engine default | The reporter of the progress line; assign a `ProgressBar`, `ProgressDots` or `ProgressPlain` (see [Progress](../api.md#progress)). |
| `bool AbortOnFailure` | true | A failing call prints its reason and aborts the script; false returns false with the reason in `LastError`. |
| `string TaskLabel` | empty | The start message of the progress line; empty uses "Generating N files" or "Generating <output>". |
| `ApiError LastError` | none | The reason of the last failure. Reset by every call. |
| `Result? LastGenerate` | null | What the last call did: `Entries` (each with `Source`, `Output`, `Symbol`, `FriendlyName`, `Status` and `Message`), `Generated`, `UpToDate`, `Failed`, `Single`. |
| `Dictionary<string, string> Symbols` | empty | Friendly name and symbol of every input of the last call. |

`EntryStatus`: `UpToDate`, `Generated`, `Failed`, `Skipped` (not attempted because an earlier
entry failed with the abort in effect).

## Verbs

| Method | Description |
| --- | --- |
| `bool Generate(KList bin, KList obj)` | One object per binary, in the same order and number (`InvalidArgument` otherwise). The outputs that are up to date are left alone. |
| `bool Generate(KList bin, KValue obj)` | One object holding every binary, in the order of the list, each block followed by its size; a file added, removed or moved in the list generates it again. |

[Back to the extensions index](../extensions.md)
