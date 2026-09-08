[Back to the extensions index](../extensions.md)

# github.csx

Client for the GitHub releases API: create a release, upload its assets and publish it.
This repository's own [kombine.csx](../../kombine.csx) uses it in the `release` action to
publish the tool packages.

```csharp
#load "extensions/github.csx"
```

Set `Owner`, `Repository` and `Token` once on the instance; every call uses them.

| Member | Description |
| --- | --- |
| `string Owner`, `string Repository` | Repository coordinates (e.g. `kollective-networks` / `kltv.kombine`). |
| `string Token` | GitHub API token with permissions to manage releases. |
| `string GetReleaseID(string tag)` | Returns the release id for a tag, or empty if it does not exist. |
| `string CreateRelease(string tag, string name, string notes, bool draft = true)` | Creates a release (draft by default) and returns its id. If the release already exists, the existing id is returned. |
| `bool UploadAssets(string releaseId, string[] filePaths)` | Uploads the given files as release assets. |
| `bool PublishRelease(string releaseId)` | Publishes the (draft) release. |

```csharp
#load "extensions/github.csx"

Github github = new Github();
github.Owner = "myorg";
github.Repository = "myrepo";
github.Token = KValue.Import("github_token").ToString();
string id = github.CreateRelease("v1.0", "Release 1.0", "Notes here", true);
github.UploadAssets(id, new string[] { "out/pkg/app.zip" });
github.PublishRelease(id);
```

[Back to the extensions index](../extensions.md)
