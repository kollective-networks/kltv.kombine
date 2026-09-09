[Back to the extensions index](../extensions.md)

# clang.doc.csx

Generates Markdown documentation from the YAML files produced by `clang-doc`.
Requires Kombine 1.5 (the file declares it with `#pragma kombine requires 1.5`).
Demonstrated in [examples/06.extensions/01.clang.docs](../../examples/06.extensions/01.clang.docs/).

```csharp
#load "extensions/clang.doc.csx"
```

Configuration is done through static fields of the `ClangDocs` class before calling
`Generate()`:

| Member | Description |
| --- | --- |
| `SourceFiles` | Sources to document. |
| `IncludePaths`, `ExtraArguments` | Include paths and extra arguments for clang-doc. |
| `OutputFolder` | Destination folder for the generated Markdown files. |
| `TemporalFolder` | Working folder for the intermediate YAML files. |
| `Paths`, `Namespaces` | Filters: only symbols from these paths / namespaces are documented. |
| `OnlyPublic` | Document only public symbols (default true). |
| `ProjectName`, `HeaderText`, `FooterText`, `ImagePath` | Output customization: project name, header/footer text and logo image. |
| `static bool Generate()` | Runs clang-doc and produces the Markdown documentation tree. |

```csharp
#load "extensions/clang.doc.csx"

int doc(string[] args) {
	ClangDocs.SourceFiles = Glob("src/*.cpp");
	ClangDocs.IncludePaths += "inc/";
	ClangDocs.OutputFolder = "out/doc/";
	ClangDocs.TemporalFolder = "out/tmp/doc/";
	ClangDocs.ProjectName = "My library";
	return ClangDocs.Generate() ? 0 : 1;
}
```

[Back to the extensions index](../extensions.md)
