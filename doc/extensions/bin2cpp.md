[Back to the extensions index](../extensions.md)

# bin2cpp.csx

Generates `.cpp` files embedding the content of binary files as byte arrays, so assets
can be compiled into a C++ project. Only regenerates outputs that are missing or older
than their input. Demonstrated in
[examples/06.extensions/04.bin2cpp](../../examples/06.extensions/04.bin2cpp/).

```csharp
#load "extensions/bin2cpp.csx"
```

| Member | Description |
| --- | --- |
| `bool Generate(KList bin, KList cpp)` | Generates one `.cpp` per input binary (both lists must match in size). |
| `bool Generate(KList bin, KValue cpp)` | Generates a single `.cpp` containing all the input binaries. |
| `Dictionary<string, string> Symbols` | Symbols generated in the last `Generate` call (symbol name → variable name), useful to emit a header declaring them. |

```csharp
#load "extensions/bin2cpp.csx"

int assets(string[] args) {
	Bin2Cpp generator = new Bin2Cpp();
	KList bin = Glob("assets/*.png");
	KList cpp = bin.WithExtension(".cpp").WithPrefix("out/gen/");
	return generator.Generate(bin, cpp) ? 0 : 1;
}
```

[Back to the extensions index](../extensions.md)
