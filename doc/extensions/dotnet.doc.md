[Back to the extensions index](../extensions.md)

# dotnet.doc.csx

Converts a C# XML documentation file (the one produced by the compiler with
`GenerateDocumentationFile`) into a Markdown file. A temporary utility until a proper
documentation generator is in place. Requires Kombine 1.5 (the file declares it
with `#pragma kombine requires 1.5`).

```csharp
#load "extensions/dotnet.doc.csx"
```

| Member | Description |
| --- | --- |
| `static void Convert(string InputFile, string OutputFile)` | Reads the XML documentation file and writes the Markdown conversion. |

```csharp
#load "extensions/dotnet.doc.csx"

XmlToMarkdown.Convert("out/bin/win-x64/release/mytool.xml", "doc/api.generated.md");
```

[Back to the extensions index](../extensions.md)
