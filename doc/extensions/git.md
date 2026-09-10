[Back to the extensions index](../extensions.md)

# git.csx

Verbs around the `git` command line tool for build scripts: acquiring sources, keeping them at
a wanted state, stamping builds, deciding what changed, patching third parties, packaging and
publishing. Requires Kombine 1.6 (the file declares it with `#pragma kombine requires 1.6`).
Used by the sdl2 extra example ([examples/07.extras/00.sdl2](../../examples/07.extras/00.sdl2/))
and tested by the git example ([examples/06.extensions/02.git](../../examples/06.extensions/02.git/)).

```csharp
#load "extensions/git.csx"
```

## How the verbs work

- **One method per verb**, named after the git command it runs. Every verb takes one options
  object; a property left alone gives what the previous extension did for the existing verbs
  (`Clone`, `Pull`, `Patch`, `Status`, `GetModifiedFiles`, `Add`, `InstallPreCommit`) and what
  git does by default for the new ones. `Clone` keeps its `branch` third parameter, so existing
  calls compile unchanged.
- **True or false.** A verb returns true when it did what was asked and false otherwise, with
  the reason in `Git.LastError` (a code and the first line of git's message) and git's full text
  in `Git.LastOutput`. What a verb found or produced is readable afterwards in a `Last<Verb>`
  property: `Git.LastInfo`, `Git.LastPull`, `Git.LastDiff`... The answers of a call that returned
  true live there too (`Git.LastDiff.Changed`, `Git.LastIgnored`).
- **Nothing printed at normal level** but the progress line: the script prints its own messages.
  `Git.AbortOnFailure` makes any failing verb print its reason and abort the script instead.
- **Git never prompts.** `GIT_TERMINAL_PROMPT=0` is exported when the extension loads: a missing
  credential fails at once with `AccessDenied` instead of waiting on the console.

```csharp
#load "extensions/git.csx"

int fetch(string[] args) {
	if (!Git.VersionCheck(2, 30, 0))
		Msg.PrintAndAbort("git 2.30 or newer is required: " + Git.LastError.Message);
	if (!Folders.Exists("ext/sdl"))
		return Git.Clone("https://github.com/libsdl-org/SDL.git", "ext/sdl", "release-2.30.x") ? 0 : 1;
	if (!Git.Pull("ext/sdl")) {
		Msg.PrintError("sdl not updated (" + Git.LastPull.Status + "): " + Git.LastError.Message);
		return 1;
	}
	Git.Info("ext/sdl");
	Msg.Print("sdl at " + Git.LastInfo.Describe);
	return 0;
}
```

## Settings

| Member | Default | Meaning |
| --- | --- | --- |
| `ITaskProgress? Progress` | null, the engine default | Reporter of the progress line of the transfer verbs; assign a `ProgressBar`, `ProgressDots` or `ProgressPlain` (see [Progress](../api.md#progress)). |
| `GitOutput Output` | `Progress` | What reaches the console while git runs: `Silent` (nothing but what the extension prints), `Progress` (one line per transfer through `Progress`), `Detailed` (git's own text). In `Silent` and `Progress` git's text still goes out at verbose level, without the progress updates it redraws, so a `Msg.OnMessage` handler receives it. The transfer verbs take an `Output` property to override it for one call. |
| `ApiError LastError` | none | The reason of the last failure: `NotFound`, `AccessDenied`, `NetworkError`, `AlreadyExists`, `Different`, `NotSupported`, `InvalidArgument` or `Failed`, and git's message. Reset by every verb. |
| `string LastOutput` | empty | The full text git printed in the last call. |
| `bool AbortOnFailure` | false | A failing verb prints its reason and aborts the script (exit code 1) instead of returning false. Only failures abort, never the answers of a call that returned true. |
| `int Timeout` | 0, no limit | Milliseconds a git command may run before it is killed; the transfer verbs take a `Timeout` option per call. |
| `InheritSettings Inherit` | disabled | The authentication inheritance, below. |

The runner behind every verb adds `-C <path>`, `--no-pager`, `-c color.ui=never`,
`-c core.quotepath=false`, `-c advice.detachedHead=false` (unless a verb option asks for the
notice), `-c core.longpaths=true` on Windows, and retries once with `-c safe.directory=<path>`
after git's "dubious ownership" message. The command line is logged at verbose level (`-ko:v`)
with the user info of the URLs masked.

## Verbs

The signature, then the options with their defaults. `Last<Verb>` names the property the verb
fills.

### Clone

`bool Clone(string uri, string path, string? branch = null, CloneOptions? options = null)` — `LastClone` (`Commit`, `Url`, `Inherited`)

Without options: the full history, the submodules recursively, the detached HEAD notice
silenced, the progress line shown, one attempt. `branch` is a branch or a tag (a tag gives a
detached checkout). A local source (a path or a `file://` URL) allows the file transport for its
submodules.

| Option | Default | Meaning |
| --- | --- | --- |
| `string? Commit` | null | Commit hash to pin: the hash is fetched with depth 1 (the whole history when the server refuses a hash), checked out detached and verified. A repository cloned this way has no branch until `Fetch`. |
| `bool DetachedHeadAdvice` | false | True shows git's detached HEAD notice. |
| `SubmoduleMode Submodules` | `Recursive` | `None`, `Init` (top level only), `Recursive`. |
| `int SubmoduleDepth` | 0 | Shallow submodules with that depth. |
| `int Jobs` | 0 | Submodules cloned in parallel. |
| `int Depth` | 0, full history | Shallow clone. `describe`, the commit counts and `MergeBase` are unreliable on it; `Info` reports `Shallow`. |
| `bool SingleBranch` | false | `--single-branch`. |
| `bool NoTags` | false | `--no-tags`. |
| `CloneFilter Filter` | `None` | `Blobless` (contents at checkout), `Treeless` (trees and contents on demand). The on demand downloads need network during the build. |
| `string[]? Sparse` | null | Folders of a sparse checkout, only these materialized. |
| `string? Reference` | null | Local mirror to take objects from (`--reference-if-able --dissociate`). |
| `bool SkipLfs` | false | The large files of Git LFS not downloaded. |
| `bool? Inherit` | null, `Git.Inherit.Enabled` | Reuse the authentication of the source repository for this clone. |
| `string[]? Config` | null | Extra `key=value` entries written into the new repository (`clone -c`). |
| `int Retries` | 0 | Attempts made again after a `NetworkError`, with a growing delay. |
| `GitOutput? Output` | null, `Git.Output` | Console output for this call. |
| `int Timeout` | 0, `Git.Timeout` | Milliseconds before the command is killed. |

### Pull

`bool Pull(string path, PullOptions? options = null)` — `LastPull` (`Status`, `From`, `To`)

Without options: fast forward only (the branch moves forward, or the call returns false with
the `Diverged` status, never a merge commit), submodules updated, one attempt. The one default
that is not the previous extension's: `Mode` is `FastForward`, since the verb serves
dependencies; a script updating living repositories sets `Merge`.

| Option | Default | Meaning |
| --- | --- | --- |
| `PullMode Mode` | `FastForward` | `Merge` (what git does by itself), `FastForward`, `Rebase`, `Reset` (fetch then `reset --hard`: local commits and changes discarded). |
| `bool Submodules` | true | Submodules updated after the pull. |
| `bool Prune` | false | Branches and tags deleted on the remote forgotten. |
| `bool Tags` | false | Every tag downloaded, moved tags followed. |
| `bool RequireClean` | false | A tree with uncommitted changes is refused: false with the `Dirty` status. |
| `string? ExpectedRemote` | null | The origin must name this repository (any form of the URL), else false with `Different`. |
| `bool? Inherit`, `string[]? Config`, `int Retries`, `GitOutput? Output`, `int Timeout` | | As in `Clone`. |

`PullStatus`: `UpToDate` and `Updated` return true; `Diverged`, `Dirty` and `Failed` return false.

### Fetch

`bool Fetch(string path, FetchOptions? options = null)`

Plain `git fetch` without options: the configured remote and branches, nothing touched in the
working tree.

| Option | Default | Meaning |
| --- | --- | --- |
| `string? Remote` | null | The remote of the current branch. |
| `string? Reference` | null | One branch, tag or hash. |
| `bool Prune`, `bool Tags` | false | As in `Pull`. |
| `int Depth`, `int Deepen`, `bool Unshallow` | 0, 0, false | Shallow history control. |
| `bool? Inherit`, `int Retries`, `GitOutput? Output`, `int Timeout` | | As in `Clone`. |

### Checkout

`bool Checkout(string path, string reference, CheckoutOptions? options = null)`

A branch stays attached and follows the remote on the next pull; a tag or a commit gives a
detached checkout, the pinned dependency case.

| Option | Default | Meaning |
| --- | --- | --- |
| `bool Detach` | false | A branch is checked out detached too. |
| `bool Fetch` | false | The reference is fetched from origin first. |
| `bool Submodules` | false | Submodules updated after the checkout. |
| `bool Force` | false | Local changes in the way discarded. |
| `bool DetachedHeadAdvice` | false | True shows git's notice. |

### Submodule

`bool Submodule(string path, SubmoduleOptions? options = null)`

Brings the submodules to the commits the repository records. Without options: `--init --recursive`.

| Option | Default | Meaning |
| --- | --- | --- |
| `bool Init`, `bool Recursive` | true, true | Initialize the new ones, follow the nested ones. |
| `bool Sync` | false | `submodule sync --recursive` first, after a URL change. |
| `int Depth`, `int Jobs` | 0, 0 | Shallow submodules, parallel updates. |
| `bool Remote` | false | The latest commit of the tracked branch instead of the recorded one. |
| `bool Force` | false | Local changes inside the submodules discarded. |
| `bool? Inherit`, `GitOutput? Output`, `int Timeout` | | As in `Clone`. |

### Push

`bool Push(string path, PushOptions? options = null)`

Plain `git push` without options. A rejected push is `Different`.

| Option | Default | Meaning |
| --- | --- | --- |
| `string? Remote`, `string? Refspec` | null | The remote, and what to push (a branch, a tag, `local:remote`). |
| `bool Tags`, `bool FollowTags` | false | Every tag, or the annotated tags reachable from the pushed commits. |
| `bool ForceWithLease` | false | Replace the remote branch unless someone else pushed meanwhile; never a plain force. |
| `bool SetUpstream`, `bool Delete`, `bool DryRun` | false | `-u`, `--delete`, `--dry-run`. |
| `bool? Inherit`, `int Retries`, `GitOutput? Output`, `int Timeout` | | As in `Clone`. |

### LsRemote

`bool LsRemote(string uri, LsRemoteOptions? options = null)` — `LastLsRemote` (reference and commit pairs)

Asks a server for its branches and tags without cloning: the cheap "did anything change" check,
and the way to find the newest version tag of a dependency (`Tags`, `Refs`, `Sort = "-v:refname"`).

| Option | Default | Meaning |
| --- | --- | --- |
| `bool Heads`, `bool Tags`, `bool Refs` | false | Branches only, tags only, no `^{}` entries. |
| `string? Pattern`, `string? Sort` | null | Reference pattern (`v*`), sort key. |
| `bool? Inherit`, `int Retries`, `int Timeout` | | As in `Clone`. |

### Status

`bool Status(string path, StatusOptions? options = null)` — `LastStatus` (`Clean`, `Modified`, `Added`, `Deleted`, `Renamed`, `Untracked`, `Conflicts`, `Branch`, `Upstream`, `Ahead`, `Behind`)

From the machine readable form of `git status` (`--porcelain=v2 --branch`). Options: `Untracked`
(true, list untracked files), `Submodules` (false, a submodule at another commit counts as modified).

### Info

`bool Info(string path, InfoOptions? options = null)` — `LastInfo`

The stamp of a repository: `Commit`, `ShortCommit`, `Branch` (empty when detached), `Tag`,
`Describe` (`v1.5.3-12-gabcdef12`, `-dirty` appended), `Dirty`, `Detached`, `Shallow`,
`CommitDate`, `CommitEpoch` (for `SOURCE_DATE_EPOCH`), `CommitCount`, `CommitsSinceTag`, `Ahead`,
`Behind`, `Author`, `Subject`, `RemoteUrl` (masked), `TopLevel`, `AuthKind`, `AuthBase`. Options:
`Match` (tags describe considers, `v*`), `FirstParent`, `Abbrev` (0, git decides), `Untracked`
(false, untracked files do not make the tree dirty), `Environment` (false; true fills the commit
and the branch from the CI variables when the folder is not a repository).

### Diff

`bool Diff(string path, DiffOptions? options = null)` — `LastDiff` (`Changed`, `Files`, `Patch`)

The files changed between two states, and the patch of those changes in the form `Patch`
applies. Without options: the index against the working tree, names only. Untracked files are
not part of a diff: `Add` them with `IntentToAdd` first.

| Option | Default | Meaning |
| --- | --- | --- |
| `string? From`, `string? To` | null | The two states (commits, tags, hashes). |
| `bool Cached` | false | The index against `From`. |
| `string[]? Paths`, `string? Filter` | null | Limit to paths, or to kinds of change (`ACMRD`). |
| `bool Patch` | false | Produce the unified diff. |
| `string? Output` | null | File the diff is written to (LF endings); otherwise `LastDiff.Patch`. |
| `bool Binary` | false | Binary changes included, so `Patch` can apply them. |
| `int Context`, `bool Renames`, `bool IgnoreWhitespace` | 3, true, false | `-U`, `--no-renames`, `-w`. |

`KList GetModifiedFiles(string[] paths, string[] extensions, bool staged = true)` keeps its
list result, being an existing verb: the modified files of the index (or of the working tree
with `staged` false) of the repository of the current folder, filtered by path prefixes and
extensions.

### LsFiles, CheckIgnore, RevParse, MergeBase

- `bool LsFiles(string path, LsFilesOptions? options = null)` — `LastLsFiles`. The tracked files,
  or the untracked ones (`Others`, `ExcludeStandard`), a `SubPath`, `RecurseSubmodules`.
- `bool CheckIgnore(string path, string file)` — `LastIgnored`. Whether a file is ignored.
- `bool RevParse(string path, string revision, RevParseOptions? options = null)` — `LastRevParse`.
  A revision to a hash: `HEAD`, `HEAD:src` (the hash of a folder's tree, a cache key for what is
  built from it), `v1.0^{commit}`. `Short`, `Abbrev`.
- `bool MergeBase(string path, string a, string b)` — `LastMergeBase`. The common ancestor; `a`
  is an ancestor of `b` when the base equals the hash of `a`.

### Patch

`bool Patch(string patchFile, string patchDir, PatchOptions? options = null)` — `LastPatch` (`Status`, `Rejected`, `Strip`)

Applies a patch with `git apply`: the reverse check first (an already applied patch returns
true with `AlreadyApplied`, so a build can run twice), then the check, then the apply. Works on
a plain folder too.

| Option | Default | Meaning |
| --- | --- | --- |
| `int Strip` | 1 | Leading path components removed (`-p`). |
| `bool AutoStrip` | false | Try `-p0` when the check fails. |
| `bool CheckReverse` | true | Detect an already applied patch. |
| `bool IgnoreWhitespace`, `bool ThreeWay`, `bool Reject` | false | `--ignore-whitespace`, `--3way`, `--reject` (rejected files in `LastPatch.Rejected`). |
| `bool Commit`, `string? Name`, `string? Email` | false, null, null | Commit the applied patch ("kombine: patch &lt;file&gt;", hooks skipped). |

### Add, Commit, Tag

- `bool Add(KList files, AddOptions? options = null)`: `Path` (the current folder), `All`,
  `Update`, `Force`, `IntentToAdd`.
- `bool Commit(string path, string message, CommitOptions? options = null)`: `Name` and `Email`
  for this commit only, `All`, `AllowEmpty`, `NoVerify` (hooks skipped), `Date` (author and
  committer date, for reproducible commits), `Amend`, `Sign`. Nothing to commit is a failure
  unless `AllowEmpty`.
- `bool Tag(string path, string name, TagOptions? options = null)`: `Message` (an annotated
  tag), `Commit`, `Force`, `Sign`, `Delete`. An existing tag without `Force` is `AlreadyExists`.

### Archive, Clean

- `bool Archive(string path, string output, ArchiveOptions? options = null)`: `Reference`
  (`HEAD`), `Format` (from the extension: `zip`, `tar`, `tar.gz`), `Prefix`, `Paths`,
  `Submodules` (the files of the submodules included through the `Compress` facility, which
  `git archive` cannot do).
- `bool Clean(string path, CleanOptions? options = null)` — `LastClean`. Without options nothing
  is removed: the files that would be are listed. `DryRun` (true), `Directories`, `Ignored`
  (build outputs too), `Exclude`, `Paths`.

### SparseCheckout, Worktree, Lfs, Bundle

- `bool SparseCheckout(string path, string[] folders, SparseCheckoutOptions? options = null)` —
  `LastSparseCheckout`. Restricts the tree to the folders: `Cone` (true), `Add`, `Disable`.
- `bool Worktree(string path, WorktreeOptions? options = null)` — `LastWorktrees` (`Path`,
  `Commit`, `Branch`). `Add` a folder at a `Reference` (`Detach`), `Remove` one (`Force`),
  `Prune`; with nothing set, the call lists.
- `bool Lfs(string path, LfsOptions? options = null)` — `LastLfs` (`Available`, `Version`,
  `Files`, `Missing`). Without options the availability of `git lfs`; `Install`, `Pull` with
  `Include` and `Exclude`, `List`.
- `bool Bundle(string path, BundleOptions? options = null)`: `Create` a bundle file of
  `References` (everything by default), `Verify` one. A bundle is cloned with `Clone`.

### InstallPreCommit, Version, VersionCheck

- `bool InstallPreCommit(string[] requiredLines, HookOptions? options = null)`: installs a
  pre-commit hook containing the lines (the shebang first), missing lines added to an existing
  hook (`Merge`, true), LF endings, executable outside Windows (`Executable`, true), in the
  repository of the current folder or of `Path`. Worktrees and a configured `core.hooksPath`
  are honored.
- `GitVersion Version()`: the installed git version, `-1` fields when not available.
- `bool VersionCheck(int Major, int Minor, int Revision)`: true when the installed git is at
  least that version, false with `NotSupported` otherwise.

## Enumerations

| Enumeration | Values |
| --- | --- |
| `GitOutput` | `Silent`, `Progress`, `Detailed` |
| `SubmoduleMode` | `None`, `Init`, `Recursive` |
| `CloneFilter` | `None`, `Blobless`, `Treeless` |
| `PullMode` | `Merge`, `FastForward`, `Rebase`, `Reset` |
| `PullStatus` | `UpToDate`, `Updated`, `Diverged`, `Dirty`, `Failed` |
| `PatchStatus` | `Applied`, `AlreadyApplied`, `Failed` |
| `InheritScope` | `Owner`, `Host` |
| `GitService` | `Generic`, `GitHub`, `GitLab`, `Bitbucket`, `AzureDevOps`, `Gitea` |
| `AuthKind` | `None`, `Ssh`, `Token`, `Header`, `Helper` |

## Authentication inheritance

A user without a credential manager clones the main repository with the authentication they
have, an ssh key for instance, and runs the script inside it. The script clones the other
repositories of the project by their https URLs, which need authentication and fail. With
`Git.Inherit.Enabled`, every transfer verb against a repository of the **same host and owner**
as the **source** (the repository holding the running script, or `Inherit.Source`) reuses the
authentication of the source, and a cloned repository is pinned to the same account so its
later operations keep it whatever the global git configuration becomes.

| The script says, inside `git@github.com:foo/repo1.git` | What happens |
| --- | --- |
| `Git.Clone("https://github.com/foo/repo2.git", ...)` | same host and owner: cloned as `git@github.com:foo/repo2.git`, the key applies, the origin stays ssh |
| `Git.Clone("https://github.com/bar/lib.git", ...)` | other owner: cloned as written (`Scope = Host` or an `Include` entry would add the authentication) |
| `Git.Clone("https://gitlab.com/x/y.git", ...)` | other host: cloned as written, always |

The profile of the source, from its remote URL and local configuration (`Git.LastInfo.AuthKind`):

| Kind | Detected when the source has | Applied to a call as | Written into a cloned target |
| --- | --- | --- | --- |
| `Ssh` | an ssh remote (`git@host:path`, `ssh://user@host:port/path`, an alias of `~/.ssh/config`) | the target URL rewritten to the same ssh base, plus an `insteadOf` rule for its submodules | the origin is the ssh URL; the rule with `Persist` |
| `Token` | a token in the user info of an https remote | a basic authorization header scoped to the host and owner, on the command line only | nothing, a token is never written |
| `Header` | `http.<url>.extraheader` entries in the local configuration (a CI checkout) | the entries whose URL covers the target | nothing |
| `Helper` | `credential.*` entries in the local configuration (a per repository helper, an account named for the host) | the entries | the entries: the account pin |
| `None` | nothing local | nothing | nothing |

`Git.Inherit` settings:

| Property | Default | Meaning |
| --- | --- | --- |
| `bool Enabled` | false | Turns the feature on for `Clone`, `Pull`, `Fetch`, `Push`, `LsRemote` and `Submodule`; every one of them takes an `Inherit` property to override it per call. |
| `string? Source` | null, the repository of the script | Another repository to take the profile from. |
| `InheritScope Scope` | `Owner` | `Owner`: same host and owner. `Host`: same host, any owner. Another host never. |
| `string[]? Include`, `string[]? Exclude` | null | URL prefixes always or never in scope, any form of the URL, same host only. |
| `bool Probe` | true | The target is tried anonymously first; a public repository is cloned plainly. One round trip per target. |
| `bool Persist` | true | The non secret part of the profile (the pin) and a marker (`kombine.inherited.*`) written into a cloned target. |
| `bool Identity` | true | The local `user.name` and `user.email` of the source are part of the profile and of the pin. |
| `Dictionary<string, HostRule> Hosts` | the built in table | The rule of a host name: `Hosts["git.example.com"] = GitService.Gitea` for a self hosted service, `HostRule.Of(GitService.GitLab, "/gitlab")` for an instance under a path. |

The hosting service of a URL decides how it is read (which path segment is the owner, which
host names are one service) and rewritten (the https and ssh forms differ on Azure DevOps).
The built in table knows `github.com`, `gitlab.com`, `bitbucket.org`, `dev.azure.com` with
`ssh.dev.azure.com` and the `visualstudio.com` names, and `codeberg.org`; any other host uses
the generic rule (owner first, ssh and https share the host name), which is right for GitLab,
Gitea and Forgejo instances at the root of their host. No network is used to detect a service.

```csharp
Git.Inherit.Enabled = true;                                   // reuse the authentication of the script's repository
Git.Inherit.Hosts["git.example.com"] = GitService.Gitea;      // a self hosted service
Git.Clone("https://git.example.com/foo/tools.git", "ext/tools");
if (Git.LastClone.Inherited)
	Msg.Print("cloned through " + Git.LastClone.Url);
```

A word on the ssh rewrite: the child is cloned with the converted URL and its origin is the
ssh URL from then on, so every later command, by the extension or typed by hand, uses the key;
a script comparing the origin with the https URL it gave must canonicalize first, which
`ExpectedRemote` does.

[Back to the extensions index](../extensions.md)
