[Back to the extensions index](../extensions.md)

# bin2obj.csx

Generates object files embedding binary files as data sections, so assets can be
linked directly without an intermediate C++ file: COFF objects on Windows and Linux
hosts, Mach-O 64-bit objects on macOS. Only regenerates outputs that are missing or
older than their input. Demonstrated in
[examples/06.extensions/05.bin2obj](../../examples/06.extensions/05.bin2obj/).

```csharp
#load "extensions/bin2obj.csx"
```

| Member | Description |
| --- | --- |
| `string Machine` | Target machine for the COFF file (default `x64`). The Mach-O output detects the host architecture. |
| `bool Generate(KList bin, KList obj)` | Generates one object file per input binary (both lists must match in size). |
| `bool Generate(KList bin, KValue obj)` | Generates a single object file containing all the input binaries. |
| `Dictionary<string, string> Symbols` | Symbols generated in the last `Generate` call (friendly name → symbol name). |

```csharp
#load "extensions/bin2obj.csx"

int assets(string[] args) {
	Bin2Obj generator = new Bin2Obj();
	KList bin = Glob("assets/*.png");
	KList obj = bin.WithExtension(".obj").WithPrefix("out/gen/");
	return generator.Generate(bin, obj) ? 0 : 1;
}
```

[Back to the extensions index](../extensions.md)
