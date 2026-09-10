[Back to the extensions index](../extensions.md)

# bin2cpp.csx

Generates C++ sources embedding binary files as byte arrays, one source per file or one source
for all of them, so assets can be compiled into a C++ project. Requires Kombine 1.6 (the file
declares it with `#pragma kombine requires 1.6`). Demonstrated and tested by
[examples/06.extensions/04.bin2cpp](../../examples/06.extensions/04.bin2cpp/).

```csharp
#load "extensions/bin2cpp.csx"
```

## How it works

- **Two forms of one verb.** `Generate(bin, cpp)` with two lists writes one source per binary;
  `Generate(bin, cpp)` with one output writes a single source holding every binary. Both return
  true when every output is in place and false otherwise, with the reason in `LastError`
  (`NotFound` for a missing binary, `InvalidArgument` for mismatched lists or duplicate names,
  `Failed` for a file that could not be written) and what was done in `LastGenerate`.
- **Up to date by content.** An output is generated again when it or its record (`<output>.kdep`,
  next to it) is missing, when what generates it changed (the layout version of the extension,
  the symbol and friendly names, the list of inputs of a single output) or when an input changed.
  An input whose date and size match the record counts as unchanged without being read; one
  whose date or size moved is read and its content hash compared. A file touched without an edit
  generates nothing; an edit dated older than the output still generates; the one edit that
  passes unseen keeps both the date and the size of the file.
- **A failure leaves the previous output intact.** An output is written to a temporary file and
  moved into place once complete; no partial file is ever left.
- **The symbol derives from the path as given**, so a script should pass its binaries through
  relative paths and keep them stable: a path given differently is a different symbol, and the
  output is generated again with it. `Symbols` holds the friendly name (the path with dots, lower
  case) and the symbol of every input of the last call, generated or not, for a script that
  writes a header declaring them; `<symbol>_size` names the length of each array.

```csharp
#load "extensions/bin2cpp.csx"

int assets(string[] args) {
	Bin2cpp generator = new Bin2cpp();
	generator.AbortOnFailure = false;
	KList bin = Glob("assets/*.png");
	KList cpp = bin.WithExtension(".cpp").WithPrefix("out/gen/");
	if (!generator.Generate(bin, cpp)) {
		Msg.PrintError("assets not generated: " + generator.LastError.Message);
		return 1;
	}
	Msg.Print(generator.LastGenerate.Generated + " generated, " + generator.LastGenerate.UpToDate + " up to date");
	return 0;
}
```

## Settings

| Member | Default | Meaning |
| --- | --- | --- |
| `OutputMode Output` | `Progress` | What reaches the console: `Silent` (nothing), `Progress` (one progress line per call through `Progress`, ended with `ok`, `ok (up to date)`, `ok (n up to date)` or `failed (n)`), `Detailed` (one line per file, as the previous version printed). The per file lines always go out, at verbose level in the other modes, so a `Msg.OnMessage` handler receives them whatever the mode. |
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
| `bool Generate(KList bin, KList cpp)` | One source per binary, in the same order and number (`InvalidArgument` otherwise). The outputs that are up to date are left alone. |
| `bool Generate(KList bin, KValue cpp)` | One source holding every binary, in the order of the list; a file added, removed or moved in the list generates it again. |

[Back to the extensions index](../extensions.md)
