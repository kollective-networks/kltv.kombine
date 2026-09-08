[Back to the extensions index](../extensions.md)

# modder.csx

Applies a set of modified files over a downloaded third-party project, verifying first
that the project files still match the originals the mod was created against, so an
upstream update that invalidates the mod is detected instead of silently clobbered.

```csharp
#load "extensions/modder.csx"
```

It works with three folders, mirroring the same structure:

- `Prj`: the downloaded project to be modified.
- `Org`: copies of the original files, as they were before modification.
- `Mod`: the modified files to apply.

| Member | Description |
| --- | --- |
| `string Prj`, `string Org`, `string Mod` | The three folders described above. |
| `bool VerifyMod()` | Verifies the mod can be applied: every original file must still exist in the project with matching content. Call before `ApplyMod`. |
| `bool ApplyMod()` | Copies the modified files over the project. |
| `bool RemoveMod()` | Restores the original files in the project. |

```csharp
#load "extensions/modder.csx"

int patch(string[] args) {
	Modder modder = new Modder();
	modder.Prj = "ext/library";
	modder.Org = "mods/library/org";
	modder.Mod = "mods/library/mod";
	if (!modder.VerifyMod())
		Msg.PrintAndAbort("The library changed upstream, the mod must be reviewed");
	return modder.ApplyMod() ? 0 : 1;
}
```

[Back to the extensions index](../extensions.md)
