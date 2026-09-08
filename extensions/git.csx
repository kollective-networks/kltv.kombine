#pragma kombine requires 1.6
/*---------------------------------------------------------------------------------------------------------

	Kombine Git Extension

	(C) Kollective Networks 2026

	Verbs around the git command line tool for build scripts: acquiring sources, keeping them at a
	wanted state, stamping builds, deciding what changed, patching third parties, packaging and
	publishing. Every public member is documented in place; this header is the overview.

	One method per verb, named after the git command it runs (Clone, Pull, Fetch, Checkout, Status,
	Info, LsRemote, Submodule, Diff, GetModifiedFiles, LsFiles, CheckIgnore, RevParse, MergeBase,
	Patch, Add, Commit, Tag, Push, Archive, Clean, SparseCheckout, Worktree, Lfs, Bundle,
	InstallPreCommit, Version, VersionCheck). Every verb takes one options object; a property left
	alone gives what the previous extension did (existing verbs) or what git does by default (new
	verbs). No variants of a verb exist.

	Results. Every verb returns true when it did what was asked and false otherwise, with the reason
	in Git.LastError (code and first line of git's message) and git's full text in Git.LastOutput.
	What a verb found or produced is readable afterwards in a Last<Verb> property (Git.LastPull,
	Git.LastInfo, Git.LastDiff...). The answers of a call that returned true live there too:
	Git.LastDiff.Changed, Git.LastIgnored, Git.LastPatch.Status == AlreadyApplied.

	Output. Git.Output decides what reaches the console while git runs: Silent (nothing but what the
	extension prints itself), Progress (one progress line per transfer through Git.Progress, the
	default), Detailed (git's own text as git prints it). The verbs that run a transfer take an
	Output property to override it for one call. Nothing is printed at normal level but the progress
	line: the script prints its own messages. Git.AbortOnFailure, false by default, makes any failing
	verb print its reason and abort the script instead of returning false.

	The runner. Every verb runs "git -C <path>" with color and the pager off, core.quotepath=false,
	advice.detachedHead=false unless asked otherwise, core.longpaths=true on Windows and
	safe.directory=<path> after the "dubious ownership" message. GIT_TERMINAL_PROMPT=0 is exported
	when the extension loads so git fails at once instead of prompting on the console. Failures are
	classified from the exit code and git's message into NotFound, AccessDenied, NetworkError,
	AlreadyExists, Different or Failed. Retries, only when the options give a number, only on
	NetworkError. The command line is logged at verbose level with the user info of the URLs masked.

	Authentication inheritance (Git.Inherit). With Enabled, every transfer verb against a repository
	of the same host and owner as the source (the repository holding the running script, or
	Inherit.Source) reuses the authentication of the source: an ssh remote (the target URL is
	rewritten to the ssh form, its origin is ssh from then on), a token in the source URL (sent as a
	basic authorization header scoped to the host and owner, never written), the headers a CI
	checkout leaves, or the local credential helper settings. Scope: Owner (same host and owner,
	the default) or Host (same host, any owner), refined with Include and Exclude prefixes; another
	host never. Probe, on by default, tries the target anonymously first so a public repository is
	cloned plainly. Persist, on by default, writes the non secret part of the profile (the pin)
	and a marker into a cloned target so later operations keep the account whatever the global git
	configuration becomes. The URL of a target is read with the rule of its hosting service
	(Inherit.Hosts: an explicit entry, the built in table for github.com, gitlab.com, bitbucket.org,
	dev.azure.com and codeberg.org, the generic rule otherwise).

---------------------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;

// Git must never wait for a user name or a password on the console: with this variable it fails at
// once with "terminal prompts disabled" and the reason reaches Git.LastError. Exported into the script
// environment, which every tool the script runs inherits.
((KValue)"0").Export("GIT_TERMINAL_PROMPT");

// ------------------------------------------------------------------------------------------------
// Enumerations
// ------------------------------------------------------------------------------------------------

/// <summary>
/// What reaches the console while git runs. The facility value is Git.Output; every transfer verb
/// takes an Output property to override it for one call.
/// </summary>
public enum GitOutput {
	/// <summary>Nothing on the console apart from what the extension prints itself; git's text is captured in Git.LastOutput.</summary>
	Silent,
	/// <summary>One progress line per operation through Git.Progress; git's text is captured and parsed for the percentage, never shown. The default.</summary>
	Progress,
	/// <summary>Git's own text as git prints it, standard output and standard error, its progress indicator included.</summary>
	Detailed
}

/// <summary>
/// How the submodules of a clone are treated.
/// </summary>
public enum SubmoduleMode {
	/// <summary>Submodules not touched.</summary>
	None,
	/// <summary>The top level submodules are initialized and checked out, not their own submodules.</summary>
	Init,
	/// <summary>Submodules and their submodules (--recurse-submodules). The default, what the previous extension did.</summary>
	Recursive
}

/// <summary>
/// Partial clone filters: what a clone downloads up front.
/// </summary>
public enum CloneFilter {
	/// <summary>A full clone. The default.</summary>
	None,
	/// <summary>--filter=blob:none: history and trees now, file contents downloaded at checkout. On demand downloads need network during the build.</summary>
	Blobless,
	/// <summary>--filter=tree:0: commits only, trees and contents on demand; for builds that never walk the history.</summary>
	Treeless
}

/// <summary>
/// What Pull does when the remote moved.
/// </summary>
public enum PullMode {
	/// <summary>git pull as configured: the branch moves forward when possible, a merge commit is created otherwise (what git does by itself).</summary>
	Merge,
	/// <summary>--ff-only: the branch only moves forward; Pull returns false with the Diverged status instead of merging. The default: the verb serves dependencies, which never want a merge commit.</summary>
	FastForward,
	/// <summary>--rebase: local commits replayed on top of the remote.</summary>
	Rebase,
	/// <summary>fetch, then reset --hard to the remote reference: local commits and changes discarded, deterministic builds.</summary>
	Reset
}

/// <summary>
/// The outcome of a Pull, in Git.LastPull.
/// </summary>
public enum PullStatus {
	/// <summary>Nothing to fetch, tree unchanged. Pull returned true.</summary>
	UpToDate,
	/// <summary>The tree moved from LastPull.From to LastPull.To. Pull returned true.</summary>
	Updated,
	/// <summary>Local commits not on the remote and FastForward mode: nothing changed. Pull returned false with Different in LastError.</summary>
	Diverged,
	/// <summary>Uncommitted changes and RequireClean: nothing changed. Pull returned false.</summary>
	Dirty,
	/// <summary>Git failed, reason in LastError. Pull returned false.</summary>
	Failed
}

/// <summary>
/// The outcome of a Patch, in Git.LastPatch.
/// </summary>
public enum PatchStatus {
	/// <summary>The patch was applied. Patch returned true.</summary>
	Applied,
	/// <summary>The reverse check succeeded: the tree already contains the patch, nothing changed. Patch returned true.</summary>
	AlreadyApplied,
	/// <summary>The check or the apply failed: rejected files in LastPatch.Rejected, reason in LastError. Patch returned false.</summary>
	Failed
}

/// <summary>
/// The targets that receive the authentication of the source (Git.Inherit.Scope). A credential of
/// one host is never sent to another host, whatever the value.
/// </summary>
public enum InheritScope {
	/// <summary>Targets on the same host and the same owner (organization, group, team or user) as the source: with the source git@github.com:foo/repo1.git, github.com/foo/*. The default.</summary>
	Owner,
	/// <summary>Targets on the same host as the source, any owner: github.com/*/*.</summary>
	Host
}

/// <summary>
/// The hosting services with a built in URL rule (how a URL is read: which path segment is the
/// owner, which host names are the same service; and how it is rewritten between https and ssh).
/// Unknown hosts use Generic; Git.Inherit.Hosts assigns a service or a rule to a host name.
/// </summary>
public enum GitService {
	/// <summary>Owner is the first segment, https and ssh share the host name, no path prefix. Right for GitLab, Gitea, Forgejo and Gogs instances installed at the root of their host.</summary>
	Generic,
	/// <summary>github.com: https github.com/{owner}/{repo}, ssh git@github.com:{owner}/{repo}.</summary>
	GitHub,
	/// <summary>gitlab.com or self hosted: {owner}/{subgroups...}/{repo}; the owner is the top group.</summary>
	GitLab,
	/// <summary>bitbucket.org: {owner}/{repo}.</summary>
	Bitbucket,
	/// <summary>dev.azure.com: https dev.azure.com/{owner}/{project}/_git/{repo}, ssh git@ssh.dev.azure.com:v3/{owner}/{project}/{repo}; the https and ssh host names differ and are one service.</summary>
	AzureDevOps,
	/// <summary>Gitea, Forgejo, Gogs (codeberg.org built in): {owner}/{repo}, ssh with or without a port.</summary>
	Gitea
}

/// <summary>
/// How the source repository authenticates, detected from its remote URL and its local configuration
/// (Git.LastInfo.AuthKind, and what the inheritance reuses).
/// </summary>
public enum AuthKind {
	/// <summary>Nothing local: the global git setup serves every target, or nothing does. Nothing to inherit.</summary>
	None,
	/// <summary>An ssh remote (git@host:path, ssh://user@host:port/path or an alias of ~/.ssh/config). Reused by rewriting the target URL to the same ssh base. Not a secret.</summary>
	Ssh,
	/// <summary>A token in the user info of an https remote (https://user:token@host/...). Reused as a basic authorization header scoped to the host and owner, never written to disk.</summary>
	Token,
	/// <summary>http.&lt;url&gt;.extraheader entries in the local configuration, what a CI checkout leaves. Reused per command, never written.</summary>
	Header,
	/// <summary>credential.helper, credential.credentialStore and credential.&lt;url&gt;.* entries in the local configuration: a per repository helper or an account named for the host. Reused per command and written into a cloned target as its account pin.</summary>
	Helper
}

// ------------------------------------------------------------------------------------------------
// Options: one class per verb. A property left alone gives the behaviour of the previous extension
// for the existing verbs and the behaviour of plain git for the new ones.
// ------------------------------------------------------------------------------------------------

/// <summary>
/// Options of Git.Clone. Without options: full history, submodules recursively, the detached HEAD
/// notice silenced, the progress line shown, one attempt; what the previous extension did.
/// </summary>
public class CloneOptions {
	/// <summary>Commit hash to end on. Null (default) clones the branch or tag given as the branch argument, or the default branch. With a hash the repository is initialized, the hash fetched with depth 1 (the server must allow fetching by hash: GitHub, GitLab, Azure DevOps and Gitea do; otherwise the whole history is fetched), checked out detached and verified: a dependency build becomes deterministic.</summary>
	public string? Commit { get; set; } = null;
	/// <summary>True shows git's notice about the detached HEAD state (a checkout of a tag or a commit). False (default, the showdettach argument of the previous extension) silences it and writes advice.detachedHead=false into the new repository so later checkouts there stay quiet too.</summary>
	public bool DetachedHeadAdvice { get; set; } = false;
	/// <summary>How the submodules are cloned. Recursive (default): submodules and their submodules; Init: the top level ones; None: not touched.</summary>
	public SubmoduleMode Submodules { get; set; } = SubmoduleMode.Recursive;
	/// <summary>Depth of the submodule clones (--shallow-submodules). Zero (default): their full history.</summary>
	public int SubmoduleDepth { get; set; } = 0;
	/// <summary>Number of submodules cloned in parallel (--jobs). Zero (default): git decides.</summary>
	public int Jobs { get; set; } = 0;
	/// <summary>Number of commits of history to download (--depth), a shallow clone. Zero (default): the full history. On a shallow clone describe, rev-list --count and merge-base are wrong or fail; Git.Info reports Shallow, and Fetch with Unshallow or Deepen brings history later.</summary>
	public int Depth { get; set; } = 0;
	/// <summary>--single-branch: only the cloned branch, not every branch. False by default; a Depth implies it unless git is told otherwise.</summary>
	public bool SingleBranch { get; set; } = false;
	/// <summary>--no-tags: tags not downloaded. False by default. Without tags, describe cannot name commits from them.</summary>
	public bool NoTags { get; set; } = false;
	/// <summary>Partial clone filter: Blobless downloads file contents at checkout, Treeless also the trees. None by default. The on demand downloads need network during the build, so pair a filter with a complete checkout of the folders the build reads.</summary>
	public CloneFilter Filter { get; set; } = CloneFilter.None;
	/// <summary>Folders of a sparse checkout: only these are materialized (--sparse, then sparse-checkout set). Null (default): the whole tree.</summary>
	public string[]? Sparse { get; set; } = null;
	/// <summary>Local mirror to take objects from (--reference ... --dissociate), for build farms with a mirror cache. Null by default.</summary>
	public string? Reference { get; set; } = null;
	/// <summary>True does not download the large files of Git LFS (the filters are disabled for the clone); Git.Lfs with Pull fetches the needed ones later. False by default.</summary>
	public bool SkipLfs { get; set; } = false;
	/// <summary>Reuse the authentication of the source repository for this clone (see Git.Inherit). Null (default) takes Git.Inherit.Enabled.</summary>
	public bool? Inherit { get; set; } = null;
	/// <summary>Extra configuration entries for the clone, as "key=value" (each becomes a -c key=value, written into the new repository by git clone). Null by default.</summary>
	public string[]? Config { get; set; } = null;
	/// <summary>Attempts made again after a NetworkError (a dropped connection, a name that does not resolve, a stalled transfer), with a growing delay, never after any other failure. Zero (default): one attempt.</summary>
	public int Retries { get; set; } = 0;
	/// <summary>Console output for this call. Null (default) takes Git.Output.</summary>
	public GitOutput? Output { get; set; } = null;
	/// <summary>Milliseconds the git command may run before it is killed (see Git.Timeout). Zero (default) takes Git.Timeout.</summary>
	public int Timeout { get; set; } = 0;
}

/// <summary>
/// Options of Git.Pull. Without options: fast forward only, submodules updated, one attempt. The one
/// default that is not the previous extension's: Mode is FastForward (the previous one let git merge).
/// </summary>
public class PullOptions {
	/// <summary>What happens when the remote moved. FastForward (default): the branch moves forward or Pull returns false with Diverged, never a merge commit; Merge: what git does by itself, for a script that updates living repositories; Rebase; Reset: fetch then reset --hard, local commits and changes discarded.</summary>
	public PullMode Mode { get; set; } = PullMode.FastForward;
	/// <summary>Submodules updated to the recorded commits after the pull (--recurse-submodules and submodule update --init --recursive). True by default, what the previous extension did.</summary>
	public bool Submodules { get; set; } = true;
	/// <summary>--prune --prune-tags: branches and tags deleted on the remote are forgotten. False by default.</summary>
	public bool Prune { get; set; } = false;
	/// <summary>--tags --force: every tag downloaded, moved tags followed (release tags do get moved). False by default: only the tags of the fetched commits.</summary>
	public bool Tags { get; set; } = false;
	/// <summary>True refuses to update a tree with uncommitted changes: Pull returns false with the Dirty status. False by default.</summary>
	public bool RequireClean { get; set; } = false;
	/// <summary>URL the origin must have; Pull returns false with Different when it does not match (compared as canonical URLs: .git suffix and scheme do not count). Null (default): no check.</summary>
	public string? ExpectedRemote { get; set; } = null;
	/// <summary>Reuse the authentication of the source repository (see Git.Inherit). Null (default) takes Git.Inherit.Enabled.</summary>
	public bool? Inherit { get; set; } = null;
	/// <summary>Extra configuration entries for the command, as "key=value". Null by default.</summary>
	public string[]? Config { get; set; } = null;
	/// <summary>Attempts made again after a NetworkError, with a growing delay. Zero (default): one attempt.</summary>
	public int Retries { get; set; } = 0;
	/// <summary>Console output for this call. Null (default) takes Git.Output.</summary>
	public GitOutput? Output { get; set; } = null;
	/// <summary>Milliseconds the git command may run before it is killed. Zero (default) takes Git.Timeout.</summary>
	public int Timeout { get; set; } = 0;
}

/// <summary>
/// Options of Git.Fetch. Without options, plain "git fetch": the configured remote and branches.
/// </summary>
public class FetchOptions {
	/// <summary>Remote to fetch from. Null (default): the remote of the current branch.</summary>
	public string? Remote { get; set; } = null;
	/// <summary>One branch, tag or commit hash to fetch. Null (default): the configured branches.</summary>
	public string? Reference { get; set; } = null;
	/// <summary>--prune --prune-tags: branches and tags deleted on the remote are forgotten. False by default.</summary>
	public bool Prune { get; set; } = false;
	/// <summary>--tags --force: every tag downloaded, moved tags followed. False by default.</summary>
	public bool Tags { get; set; } = false;
	/// <summary>--depth: only the last n commits. Zero by default.</summary>
	public int Depth { get; set; } = 0;
	/// <summary>--deepen: n more commits of history for a shallow clone. Zero by default.</summary>
	public int Deepen { get; set; } = 0;
	/// <summary>--unshallow: the whole history for a shallow clone. False by default.</summary>
	public bool Unshallow { get; set; } = false;
	/// <summary>Reuse the authentication of the source repository (see Git.Inherit). Null (default) takes Git.Inherit.Enabled.</summary>
	public bool? Inherit { get; set; } = null;
	/// <summary>Attempts made again after a NetworkError, with a growing delay. Zero (default): one attempt.</summary>
	public int Retries { get; set; } = 0;
	/// <summary>Console output for this call. Null (default) takes Git.Output.</summary>
	public GitOutput? Output { get; set; } = null;
	/// <summary>Milliseconds the git command may run before it is killed. Zero (default) takes Git.Timeout.</summary>
	public int Timeout { get; set; } = 0;
}

/// <summary>
/// Options of Git.Checkout. Without options, plain "git checkout reference": a branch stays attached
/// and follows the remote on the next pull; a tag or a commit gives a detached checkout, the pinned
/// dependency case.
/// </summary>
public class CheckoutOptions {
	/// <summary>--detach: a branch is checked out detached too, the tree ends on the commit the branch points to. False by default.</summary>
	public bool Detach { get; set; } = false;
	/// <summary>Fetch the reference from origin first (branch, tag or hash), so a pin can be moved to something not yet local. False by default.</summary>
	public bool Fetch { get; set; } = false;
	/// <summary>Submodules updated to the recorded commits after the checkout (submodule update --init --recursive). False by default.</summary>
	public bool Submodules { get; set; } = false;
	/// <summary>--force: local changes in the way are discarded. False by default.</summary>
	public bool Force { get; set; } = false;
	/// <summary>True shows git's notice about the detached HEAD state. False by default, as Clone.</summary>
	public bool DetachedHeadAdvice { get; set; } = false;
}

/// <summary>
/// Options of Git.Status.
/// </summary>
public class StatusOptions {
	/// <summary>Untracked files listed (git's default). False: --untracked-files=no.</summary>
	public bool Untracked { get; set; } = true;
	/// <summary>The state of every submodule merged in (submodule status --recursive): a submodule at another commit than the recorded one counts as Modified. False by default.</summary>
	public bool Submodules { get; set; } = false;
}

/// <summary>
/// Options of Git.Info.
/// </summary>
public class InfoOptions {
	/// <summary>Pattern of the tags describe considers (--match), "v*" for version tags. Null (default): every tag.</summary>
	public string? Match { get; set; } = null;
	/// <summary>--first-parent: describe follows the first parent of merges only. False by default.</summary>
	public bool FirstParent { get; set; } = false;
	/// <summary>Length of the short commit hash. Zero (default): git decides (7 or more, as many as needed to be unique).</summary>
	public int Abbrev { get; set; } = 0;
	/// <summary>True: untracked files make the tree dirty. False by default: only changes to tracked files do.</summary>
	public bool Untracked { get; set; } = false;
	/// <summary>True: when the folder is not a repository, the commit and the branch come from the CI variables (GITHUB_SHA, GITHUB_REF_NAME, GITHUB_HEAD_REF, CI_COMMIT_SHA, CI_COMMIT_REF_NAME, BUILD_SOURCEVERSION, BUILD_SOURCEBRANCHNAME) instead of failing, for builds from an exported archive on a CI agent. False by default.</summary>
	public bool Environment { get; set; } = false;
}

/// <summary>
/// Options of Git.LsRemote. Without options every reference of the server is listed.
/// </summary>
public class LsRemoteOptions {
	/// <summary>--heads: branches only. False by default.</summary>
	public bool Heads { get; set; } = false;
	/// <summary>--tags: tags only. False by default.</summary>
	public bool Tags { get; set; } = false;
	/// <summary>--refs: no ^{} entries (the commit an annotated tag points to). False by default.</summary>
	public bool Refs { get; set; } = false;
	/// <summary>Reference pattern, "v*" for version tags, or one reference. Null by default.</summary>
	public string? Pattern { get; set; } = null;
	/// <summary>Sort key (--sort), "-v:refname" gives version numbers newest first (git 2.18). Null by default: the server order.</summary>
	public string? Sort { get; set; } = null;
	/// <summary>Reuse the authentication of the source repository (see Git.Inherit). Null (default) takes Git.Inherit.Enabled.</summary>
	public bool? Inherit { get; set; } = null;
	/// <summary>Attempts made again after a NetworkError, with a growing delay. Zero (default): one attempt.</summary>
	public int Retries { get; set; } = 0;
	/// <summary>Milliseconds the command may run before it is killed. Zero (default) takes Git.Timeout.</summary>
	public int Timeout { get; set; } = 0;
}

/// <summary>
/// Options of Git.Submodule, which brings the submodules to the commits the repository records
/// (submodule update). Without options: --init --recursive.
/// </summary>
public class SubmoduleOptions {
	/// <summary>--init: submodules not yet initialized are initialized. True by default; false touches only the initialized ones.</summary>
	public bool Init { get; set; } = true;
	/// <summary>--recursive: the submodules of the submodules too. True by default.</summary>
	public bool Recursive { get; set; } = true;
	/// <summary>submodule sync --recursive first: the URLs recorded in .gitmodules are copied into the configuration, needed after a URL change. False by default.</summary>
	public bool Sync { get; set; } = false;
	/// <summary>--depth: shallow submodule clones. Zero by default.</summary>
	public int Depth { get; set; } = 0;
	/// <summary>--jobs: submodules updated in parallel. Zero (default): git decides.</summary>
	public int Jobs { get; set; } = 0;
	/// <summary>--remote: the latest commit of the tracked branch instead of the recorded one. False by default; a build normally pins.</summary>
	public bool Remote { get; set; } = false;
	/// <summary>--force: local changes inside the submodules discarded. False by default.</summary>
	public bool Force { get; set; } = false;
	/// <summary>Reuse the authentication of the source repository (see Git.Inherit). Null (default) takes Git.Inherit.Enabled.</summary>
	public bool? Inherit { get; set; } = null;
	/// <summary>Console output for this call. Null (default) takes Git.Output.</summary>
	public GitOutput? Output { get; set; } = null;
	/// <summary>Milliseconds the git command may run before it is killed. Zero (default) takes Git.Timeout.</summary>
	public int Timeout { get; set; } = 0;
}

/// <summary>
/// Options of Git.Diff. Without options: the names of the files changed between the index and the
/// working tree.
/// </summary>
public class DiffOptions {
	/// <summary>First state: commit, tag or hash. Null (default): the index against the working tree.</summary>
	public string? From { get; set; } = null;
	/// <summary>Second state: commit, tag or hash. Null (default): the working tree (or the index with Cached).</summary>
	public string? To { get; set; } = null;
	/// <summary>--cached: the index against From (or HEAD). False by default.</summary>
	public bool Cached { get; set; } = false;
	/// <summary>Limit the diff to these paths. Null by default.</summary>
	public string[]? Paths { get; set; } = null;
	/// <summary>--diff-filter: the kinds of change to list, "ACMRD" selects added, copied, modified, renamed and deleted. Null (default): every kind.</summary>
	public string? Filter { get; set; } = null;
	/// <summary>True produces the unified diff (the text git diff prints) instead of the names only, in the form Git.Patch applies (a/ and b/ prefixes, one leading path component to strip). False by default.</summary>
	public bool Patch { get; set; } = false;
	/// <summary>File the diff is written to, as git emits it (LF endings, UTF-8). Null (default): the diff comes back in Git.LastDiff.Patch.</summary>
	public string? Output { get; set; } = null;
	/// <summary>--binary: binary changes included, so Git.Patch can apply them. False by default (git's default): a binary change only says "Binary files differ".</summary>
	public bool Binary { get; set; } = false;
	/// <summary>-U: lines of context around each change. 3 by default.</summary>
	public int Context { get; set; } = 3;
	/// <summary>Rename detection (git's default). False: --no-renames.</summary>
	public bool Renames { get; set; } = true;
	/// <summary>-w: whitespace changes ignored. False by default.</summary>
	public bool IgnoreWhitespace { get; set; } = false;
}

/// <summary>
/// Options of Git.LsFiles. Without options: the tracked files.
/// </summary>
public class LsFilesOptions {
	/// <summary>Folder to list, relative to the repository. Null (default): everything.</summary>
	public string? SubPath { get; set; } = null;
	/// <summary>--others: the untracked files instead of the tracked ones. False by default.</summary>
	public bool Others { get; set; } = false;
	/// <summary>--exclude-standard with Others: the ignored files left out, so the list is the untracked files git would show. False by default.</summary>
	public bool ExcludeStandard { get; set; } = false;
	/// <summary>--recurse-submodules: the files of the submodules too. False by default.</summary>
	public bool RecurseSubmodules { get; set; } = false;
}

/// <summary>
/// Options of Git.RevParse.
/// </summary>
public class RevParseOptions {
	/// <summary>--short: the abbreviated hash. False by default.</summary>
	public bool Short { get; set; } = false;
	/// <summary>Length of the abbreviated hash with Short. Zero (default): git decides.</summary>
	public int Abbrev { get; set; } = 0;
}

/// <summary>
/// Options of Git.Patch. Without options, the defaults of git apply plus the reverse check.
/// </summary>
public class PatchOptions {
	/// <summary>Leading path components removed from the paths of the patch (-p). 1 by default, the form git diff produces (a/ and b/ prefixes).</summary>
	public int Strip { get; set; } = 1;
	/// <summary>True tries -p0 when the check with Strip fails, for patches made without prefixes. False by default.</summary>
	public bool AutoStrip { get; set; } = false;
	/// <summary>True (default) runs apply --reverse --check first: success means the tree already contains the patch and Patch returns true with AlreadyApplied, so a build can run twice. False skips it and a patch applied twice fails.</summary>
	public bool CheckReverse { get; set; } = true;
	/// <summary>--ignore-whitespace --whitespace=nowarn: whitespace differences in the context tolerated, for sources with CRLF endings. False by default.</summary>
	public bool IgnoreWhitespace { get; set; } = false;
	/// <summary>--3way: inside a repository, a hunk that does not apply is resolved with a three way merge when the original blobs are known. False by default.</summary>
	public bool ThreeWay { get; set; } = false;
	/// <summary>--reject: the hunks that apply are applied and the others left in .rej files, listed in Git.LastPatch.Rejected. False by default: nothing is applied unless everything applies.</summary>
	public bool Reject { get; set; } = false;
	/// <summary>True commits the applied patch ("kombine: patch &lt;name&gt;", hooks skipped) so the tree stays clean and a later Reset pull re-patches from a clean state. False by default.</summary>
	public bool Commit { get; set; } = false;
	/// <summary>Identity of the commit made with Commit. Null (default): the configured identity.</summary>
	public string? Name { get; set; } = null;
	/// <summary>Identity of the commit made with Commit. Null (default): the configured identity.</summary>
	public string? Email { get; set; } = null;
}

/// <summary>
/// Options of Git.Add. Without options: the given files added in the current folder, what the
/// previous extension did.
/// </summary>
public class AddOptions {
	/// <summary>Repository folder. Null (default): the current folder.</summary>
	public string? Path { get; set; } = null;
	/// <summary>-A: everything, the files argument ignored. False by default.</summary>
	public bool All { get; set; } = false;
	/// <summary>-u: tracked files only (modified and deleted), never new ones. False by default.</summary>
	public bool Update { get; set; } = false;
	/// <summary>-f: ignored files added too. False by default.</summary>
	public bool Force { get; set; } = false;
	/// <summary>-N: the files are recorded as new without their content (intent to add), so Git.Diff shows them without staging them. False by default.</summary>
	public bool IntentToAdd { get; set; } = false;
}

/// <summary>
/// Options of Git.Commit.
/// </summary>
public class CommitOptions {
	/// <summary>Author and committer name for this commit only (-c user.name). Null (default): the configured identity.</summary>
	public string? Name { get; set; } = null;
	/// <summary>Author and committer email for this commit only (-c user.email). Null (default): the configured identity.</summary>
	public string? Email { get; set; } = null;
	/// <summary>-a: every change to a tracked file committed, without Add. False by default.</summary>
	public bool All { get; set; } = false;
	/// <summary>--allow-empty: a commit with no change, for stamp commits. False by default.</summary>
	public bool AllowEmpty { get; set; } = false;
	/// <summary>--no-verify: the pre-commit and commit-msg hooks are skipped, so a developer's hook does not run inside a build. False by default.</summary>
	public bool NoVerify { get; set; } = false;
	/// <summary>Author and committer date, for reproducible commits. Null (default): now.</summary>
	public DateTime? Date { get; set; } = null;
	/// <summary>--amend: the last commit replaced. False by default.</summary>
	public bool Amend { get; set; } = false;
	/// <summary>-S: the commit signed with the configured key. False by default.</summary>
	public bool Sign { get; set; } = false;
}

/// <summary>
/// Options of Git.Tag. Without options a lightweight tag on HEAD.
/// </summary>
public class TagOptions {
	/// <summary>Message of an annotated tag (-a -m). Null (default): a lightweight tag.</summary>
	public string? Message { get; set; } = null;
	/// <summary>The tagged commit. Null (default): HEAD.</summary>
	public string? Commit { get; set; } = null;
	/// <summary>-f: an existing tag is moved. False by default.</summary>
	public bool Force { get; set; } = false;
	/// <summary>-s: the tag signed with the configured key. False by default.</summary>
	public bool Sign { get; set; } = false;
	/// <summary>-d: the tag deleted instead of created. False by default.</summary>
	public bool Delete { get; set; } = false;
}

/// <summary>
/// Options of Git.Push. Without options, plain "git push": the current branch to its remote.
/// </summary>
public class PushOptions {
	/// <summary>Remote to push to. Null (default): the remote of the branch.</summary>
	public string? Remote { get; set; } = null;
	/// <summary>What to push: a branch, a tag, or "local:remote". Null (default): the current branch.</summary>
	public string? Refspec { get; set; } = null;
	/// <summary>--tags: every tag pushed. False by default.</summary>
	public bool Tags { get; set; } = false;
	/// <summary>--follow-tags: the annotated tags reachable from the pushed commits pushed too. False by default.</summary>
	public bool FollowTags { get; set; } = false;
	/// <summary>--force-with-lease: the remote branch replaced unless someone else pushed meanwhile. Never a plain --force. False by default.</summary>
	public bool ForceWithLease { get; set; } = false;
	/// <summary>-u: the remote branch remembered as upstream. False by default.</summary>
	public bool SetUpstream { get; set; } = false;
	/// <summary>--delete: the refspec deleted on the remote. False by default.</summary>
	public bool Delete { get; set; } = false;
	/// <summary>--dry-run: nothing sent, the outcome reported. False by default.</summary>
	public bool DryRun { get; set; } = false;
	/// <summary>Reuse the authentication of the source repository (see Git.Inherit). Null (default) takes Git.Inherit.Enabled.</summary>
	public bool? Inherit { get; set; } = null;
	/// <summary>Attempts made again after a NetworkError, with a growing delay. Zero (default): one attempt.</summary>
	public int Retries { get; set; } = 0;
	/// <summary>Console output for this call. Null (default) takes Git.Output.</summary>
	public GitOutput? Output { get; set; } = null;
	/// <summary>Milliseconds the git command may run before it is killed. Zero (default) takes Git.Timeout.</summary>
	public int Timeout { get; set; } = 0;
}

/// <summary>
/// Options of Git.Archive.
/// </summary>
public class ArchiveOptions {
	/// <summary>Commit, tag or hash to archive. HEAD by default.</summary>
	public string Reference { get; set; } = "HEAD";
	/// <summary>Archive format: "zip", "tar", "tar.gz" (also "tgz"). Null (default): from the extension of the output file.</summary>
	public string? Format { get; set; } = null;
	/// <summary>Folder every entry is placed under (--prefix), "name-1.0/". Null by default.</summary>
	public string? Prefix { get; set; } = null;
	/// <summary>Subset of the tree to archive. Null (default): everything.</summary>
	public string[]? Paths { get; set; } = null;
	/// <summary>True includes the files of the submodules, which git archive cannot: the tracked files are listed with the submodules, copied to a temporary folder and packed with the Compress facility. False by default.</summary>
	public bool Submodules { get; set; } = false;
}

/// <summary>
/// Options of Git.Clean. Without options nothing is removed: the files that would be are listed in
/// Git.LastClean, as git itself refuses to act without -f.
/// </summary>
public class CleanOptions {
	/// <summary>True (default) only lists (-n). False removes (-f).</summary>
	public bool DryRun { get; set; } = true;
	/// <summary>-d: untracked folders too. False by default.</summary>
	public bool Directories { get; set; } = false;
	/// <summary>-x: ignored files too, build outputs included. False by default.</summary>
	public bool Ignored { get; set; } = false;
	/// <summary>-e patterns kept whatever the rest says. Null by default.</summary>
	public string[]? Exclude { get; set; } = null;
	/// <summary>Limit the clean to these paths. Null (default): the whole tree.</summary>
	public string[]? Paths { get; set; } = null;
}

/// <summary>
/// Options of Git.SparseCheckout.
/// </summary>
public class SparseCheckoutOptions {
	/// <summary>True (default): the folders are directories, their contents included (cone mode). False: --no-cone, the folders are patterns.</summary>
	public bool Cone { get; set; } = true;
	/// <summary>True: the folders join the current set (sparse-checkout add). False (default): they replace it (set).</summary>
	public bool Add { get; set; } = false;
	/// <summary>True: sparse-checkout disable, the whole tree again; the folders are ignored. False by default.</summary>
	public bool Disable { get; set; } = false;
}

/// <summary>
/// Options of Git.Worktree. With none of Add, Remove and Prune set, the call only lists the working
/// folders into Git.LastWorktrees.
/// </summary>
public class WorktreeOptions {
	/// <summary>Folder to create as a new working tree (worktree add). Null by default.</summary>
	public string? Add { get; set; } = null;
	/// <summary>Commit, tag or branch the new folder is checked out at. Null (default): HEAD.</summary>
	public string? Reference { get; set; } = null;
	/// <summary>--detach: a branch is checked out detached; a commit or a tag is detached anyway. False by default.</summary>
	public bool Detach { get; set; } = false;
	/// <summary>Folder to remove (worktree remove). Null by default.</summary>
	public string? Remove { get; set; } = null;
	/// <summary>--force: remove a folder with local changes, or add over an existing folder. False by default.</summary>
	public bool Force { get; set; } = false;
	/// <summary>worktree prune: the folders deleted from disk are forgotten. False by default.</summary>
	public bool Prune { get; set; } = false;
}

/// <summary>
/// Options of Git.Lfs. With none of Install, Pull and List set, the call only checks that the git
/// lfs command is available.
/// </summary>
public class LfsOptions {
	/// <summary>lfs install --local: the large file filters enabled in this repository. False by default.</summary>
	public bool Install { get; set; } = false;
	/// <summary>lfs pull: the large files of the current checkout downloaded. False by default.</summary>
	public bool Pull { get; set; } = false;
	/// <summary>--include patterns for Pull: only these files. Null by default.</summary>
	public string[]? Include { get; set; } = null;
	/// <summary>--exclude patterns for Pull. Null by default.</summary>
	public string[]? Exclude { get; set; } = null;
	/// <summary>True fills Git.LastLfs.Files and Missing from lfs ls-files (implied by Pull). False by default.</summary>
	public bool List { get; set; } = false;
}

/// <summary>
/// Options of Git.Bundle. Create writes a bundle file, Verify checks one; both may be given.
/// </summary>
public class BundleOptions {
	/// <summary>File to write (bundle create). Null by default.</summary>
	public string? Create { get; set; } = null;
	/// <summary>Branches and tags to include in the bundle. Null (default): everything (--all).</summary>
	public string[]? References { get; set; } = null;
	/// <summary>File to check (bundle verify): Bundle returns false with the reason when the bundle cannot be applied to the repository. Null by default.</summary>
	public string? Verify { get; set; } = null;
}

/// <summary>
/// Options of Git.InstallPreCommit. Without options: the hook of the repository of the current
/// folder, missing lines added to an existing hook, made executable; what the previous extension did.
/// </summary>
public class HookOptions {
	/// <summary>Repository folder. Null (default): the current folder. The hooks folder is found with git itself, so a worktree and a configured core.hooksPath are honored.</summary>
	public string? Path { get; set; } = null;
	/// <summary>True (default): the missing required lines are added to an existing hook. False: the hook is replaced by the required lines.</summary>
	public bool Merge { get; set; } = true;
	/// <summary>True (default): the hook is made executable outside Windows.</summary>
	public bool Executable { get; set; } = true;
}

// ------------------------------------------------------------------------------------------------
// Results: what a verb found or produced, in the Last<Verb> property of Git
// ------------------------------------------------------------------------------------------------

/// <summary>
/// The installed git version, from "git --version". Every field is -1 when git is not available.
/// </summary>
public struct GitVersion {
	public int Major;
	public int Minor;
	public int Revision;
	public GitVersion() {
		Major = -1;
		Minor = -1;
		Revision = -1;
	}
}

/// <summary>
/// What Git.Clone produced, in Git.LastClone.
/// </summary>
public class CloneResult {
	/// <summary>The commit checked out.</summary>
	public string Commit { get; set; } = string.Empty;
	/// <summary>The URL the clone ran with: the one given, or the ssh form when the authentication of the source was inherited from an ssh remote.</summary>
	public string Url { get; set; } = string.Empty;
	/// <summary>True when the authentication of the source repository was applied.</summary>
	public bool Inherited { get; set; } = false;
}

/// <summary>
/// What Git.Pull found, in Git.LastPull.
/// </summary>
public class PullResult {
	/// <summary>The outcome.</summary>
	public PullStatus Status { get; set; } = PullStatus.Failed;
	/// <summary>The commit before the pull.</summary>
	public string From { get; set; } = string.Empty;
	/// <summary>The commit after the pull.</summary>
	public string To { get; set; } = string.Empty;
}

/// <summary>
/// The state of a repository, in Git.LastStatus, from the machine readable form of git status.
/// </summary>
public class StatusResult {
	/// <summary>True when nothing is modified, added, deleted, renamed, in conflict or (when listed) untracked.</summary>
	public bool Clean { get; set; } = true;
	/// <summary>Tracked files with changes in the working tree or the index.</summary>
	public KList Modified { get; set; } = new KList();
	/// <summary>Files added to the index.</summary>
	public KList Added { get; set; } = new KList();
	/// <summary>Files deleted in the working tree or the index.</summary>
	public KList Deleted { get; set; } = new KList();
	/// <summary>Files renamed in the index, as "old -> new".</summary>
	public KList Renamed { get; set; } = new KList();
	/// <summary>Untracked files, when listed.</summary>
	public KList Untracked { get; set; } = new KList();
	/// <summary>Files in conflict after a merge.</summary>
	public KList Conflicts { get; set; } = new KList();
	/// <summary>The current branch, empty when detached.</summary>
	public string Branch { get; set; } = string.Empty;
	/// <summary>The upstream branch, "origin/main", empty when none.</summary>
	public string Upstream { get; set; } = string.Empty;
	/// <summary>Commits ahead of the upstream.</summary>
	public int Ahead { get; set; } = 0;
	/// <summary>Commits behind the upstream.</summary>
	public int Behind { get; set; } = 0;
}

/// <summary>
/// The stamp of a repository, in Git.LastInfo: what a version header, a package name or an
/// artifact label needs.
/// </summary>
public class GitInfo {
	/// <summary>The commit hash.</summary>
	public string Commit { get; set; } = string.Empty;
	/// <summary>The abbreviated commit hash.</summary>
	public string ShortCommit { get; set; } = string.Empty;
	/// <summary>The branch, empty when the checkout is detached (a tag or a commit).</summary>
	public string Branch { get; set; } = string.Empty;
	/// <summary>The tag on the commit, empty when none (the first one when several).</summary>
	public string Tag { get; set; } = string.Empty;
	/// <summary>The description from the nearest tag: "v1.5.3-12-gabcdef12", "-dirty" appended when the tree has changes; the hash alone when no tag names the commit.</summary>
	public string Describe { get; set; } = string.Empty;
	/// <summary>True when the tree has uncommitted changes (untracked files count only with the Untracked option).</summary>
	public bool Dirty { get; set; } = false;
	/// <summary>True when the checkout is not on a branch.</summary>
	public bool Detached { get; set; } = false;
	/// <summary>True for a shallow clone: describe, the commit counts and merge-base are unreliable.</summary>
	public bool Shallow { get; set; } = false;
	/// <summary>The commit date.</summary>
	public DateTime CommitDate { get; set; } = DateTime.MinValue;
	/// <summary>The commit date as seconds since 1970, the value reproducible builds put in SOURCE_DATE_EPOCH.</summary>
	public long CommitEpoch { get; set; } = 0;
	/// <summary>Number of commits reachable from the commit: an always growing build number (unreliable on a shallow clone).</summary>
	public int CommitCount { get; set; } = 0;
	/// <summary>Number of commits since the nearest tag, zero on a tagged commit.</summary>
	public int CommitsSinceTag { get; set; } = 0;
	/// <summary>Commits ahead of the upstream branch, zero when detached or without upstream.</summary>
	public int Ahead { get; set; } = 0;
	/// <summary>Commits behind the upstream branch.</summary>
	public int Behind { get; set; } = 0;
	/// <summary>The author of the commit.</summary>
	public string Author { get; set; } = string.Empty;
	/// <summary>The subject line of the commit.</summary>
	public string Subject { get; set; } = string.Empty;
	/// <summary>The URL of origin, empty when none.</summary>
	public string RemoteUrl { get; set; } = string.Empty;
	/// <summary>The top level folder of the working tree.</summary>
	public string TopLevel { get; set; } = string.Empty;
	/// <summary>How the repository authenticates, from its remote URL and local configuration (see Git.Inherit).</summary>
	public AuthKind AuthKind { get; set; } = AuthKind.None;
	/// <summary>The base of that authentication, secrets masked: "git@github-foo:" for an ssh remote, "https://***@github.com/" for a token.</summary>
	public string AuthBase { get; set; } = string.Empty;
}

/// <summary>
/// What Git.Diff found, in Git.LastDiff.
/// </summary>
public class DiffResult {
	/// <summary>True when at least one file differs.</summary>
	public bool Changed { get; set; } = false;
	/// <summary>The files that differ, relative to the repository.</summary>
	public KList Files { get; set; } = new KList();
	/// <summary>The unified diff, when the Patch option was set and no Output file was given.</summary>
	public string Patch { get; set; } = string.Empty;
}

/// <summary>
/// What Git.Patch did, in Git.LastPatch.
/// </summary>
public class PatchResult {
	/// <summary>The outcome.</summary>
	public PatchStatus Status { get; set; } = PatchStatus.Failed;
	/// <summary>The files whose hunks were rejected (their .rej files sit next to them), with the Reject option.</summary>
	public KList Rejected { get; set; } = new KList();
	/// <summary>The -p value that applied.</summary>
	public int Strip { get; set; } = 1;
}

/// <summary>
/// One working folder of a repository, in Git.LastWorktrees.
/// </summary>
public class WorktreeEntry {
	/// <summary>The folder.</summary>
	public string Path { get; set; } = string.Empty;
	/// <summary>The commit checked out.</summary>
	public string Commit { get; set; } = string.Empty;
	/// <summary>The branch, empty when detached.</summary>
	public string Branch { get; set; } = string.Empty;
}

/// <summary>
/// What Git.Lfs found, in Git.LastLfs.
/// </summary>
public class LfsResult {
	/// <summary>True when the git lfs command is available.</summary>
	public bool Available { get; set; } = false;
	/// <summary>The git lfs version, empty when not available.</summary>
	public string Version { get; set; } = string.Empty;
	/// <summary>The large files of the repository (from lfs ls-files), with the List or Pull options.</summary>
	public KList Files { get; set; } = new KList();
	/// <summary>The large files still present as pointer files, not downloaded.</summary>
	public KList Missing { get; set; } = new KList();
}

// ------------------------------------------------------------------------------------------------
// Hosting service rules and the inheritance settings
// ------------------------------------------------------------------------------------------------

/// <summary>
/// How the URLs of a hosting service are read and rewritten: which host names are the service,
/// which path segment is the owner, what the https and the ssh forms of one repository look like.
/// The path templates use {owner}, {repo}, {project} and {...} (any number of middle segments);
/// literal segments must match as written.
/// </summary>
public class HostRule {
	/// <summary>The service the rule describes.</summary>
	public GitService Service { get; set; } = GitService.Generic;
	/// <summary>The host names that are this service, ssh and https forms included ("dev.azure.com", "ssh.dev.azure.com"); a leading "*." matches any subdomain. Empty for a rule assigned to one host through Git.Inherit.Hosts.</summary>
	public string[] Hosts { get; set; } = new string[0];
	/// <summary>The path template of the https form: "{owner}/{repo}", "{owner}/{project}/_git/{repo}".</summary>
	public string HttpsPath { get; set; } = "{owner}/{repo}";
	/// <summary>The path template of the ssh form: "{owner}/{repo}", "v3/{owner}/{project}/{repo}".</summary>
	public string SshPath { get; set; } = "{owner}/{repo}";
	/// <summary>Path under which the service is installed ("/gitlab"), removed before the template is applied. Empty by default.</summary>
	public string PathPrefix { get; set; } = string.Empty;

	/// <summary>
	/// The built in rule of a service, for a host name that is not in the built in table.
	/// </summary>
	/// <param name="service">The service.</param>
	/// <param name="pathPrefix">Path under which the service is installed, when it is not at the root of its host.</param>
	/// <returns>A copy of the built in rule.</returns>
	public static HostRule Of(GitService service, string? pathPrefix = null) {
		HostRule source = Git.BuiltInRule(service);
		HostRule rule = new HostRule();
		rule.Service = source.Service;
		rule.Hosts = new string[0];
		rule.HttpsPath = source.HttpsPath;
		rule.SshPath = source.SshPath;
		rule.PathPrefix = pathPrefix ?? source.PathPrefix;
		return rule;
	}

	/// <summary>
	/// A service assigned to a host converts to its built in rule, so the common case is one
	/// assignment: Git.Inherit.Hosts["git.example.com"] = GitService.Gitea.
	/// </summary>
	public static implicit operator HostRule(GitService service) {
		return Of(service);
	}
}

/// <summary>
/// The authentication inheritance policy, Git.Inherit. Nothing is inherited until Enabled is set.
/// </summary>
public class InheritSettings {
	/// <summary>Turns the inheritance on for every transfer verb (Clone, Pull, Fetch, Push, LsRemote, Submodule). False by default: nothing is inherited, as the previous extension.</summary>
	public bool Enabled { get; set; } = false;
	/// <summary>Repository the authentication is taken from. Null (default): the repository containing the running script. Read once per run.</summary>
	public string? Source { get; set; } = null;
	/// <summary>Which targets receive the profile: Owner (same host and owner, default) or Host (same host, any owner). Another host never.</summary>
	public InheritScope Scope { get; set; } = InheritScope.Owner;
	/// <summary>URL prefixes always in scope, "https://github.com/bar/lib" (any form of the URL matches), same host as the source only. Null by default.</summary>
	public string[]? Include { get; set; } = null;
	/// <summary>URL prefixes never in scope. Null by default.</summary>
	public string[]? Exclude { get; set; } = null;
	/// <summary>True (default): a target is tried anonymously first (ls-remote with prompts disabled) and the authentication is applied only when that fails with an authentication error, so a public repository is cloned plainly and a credential limited to one repository (a deploy key, a token scoped to the main repository) is not forced on targets it does not cover. One network round trip per target. False skips the probe.</summary>
	public bool Probe { get; set; } = true;
	/// <summary>True (default): the non secret part of the profile (the pin: the ssh rewrite rule for the submodules, the credential helper settings, the identity) and a marker are written into the local configuration of a cloned target, so its later operations keep the account whatever the global git configuration becomes. Tokens and headers are never written. False leaves every target untouched.</summary>
	public bool Persist { get; set; } = true;
	/// <summary>True (default): the local user.name and user.email of the source are part of the profile: passed to the commits made by the extension and written into a cloned target with Persist.</summary>
	public bool Identity { get; set; } = true;
	/// <summary>The rule of a host name that is not in the built in table, or a replacement: Hosts["git.example.com"] = GitService.Gitea for a self hosted service, HostRule.Of(GitService.GitLab, "/gitlab") for an instance under a path. Looked up before the built in table.</summary>
	public Dictionary<string, HostRule> Hosts { get; set; } = new Dictionary<string, HostRule>(StringComparer.OrdinalIgnoreCase);
}

// ------------------------------------------------------------------------------------------------
// The facility
// ------------------------------------------------------------------------------------------------

/// <summary>
/// Git verbs for build scripts. One method per git command, an options object per verb, true or
/// false as the result with the reason in LastError and the findings in the Last&lt;Verb&gt;
/// properties. See the header of the file for the overview.
/// </summary>
public static class Git {

	// ---------------------------------------------------------------- facility members

	/// <summary>
	/// Reporter of the progress line of the transfer verbs (Clone, Pull, Fetch, Push, Submodule) in the
	/// Progress output mode. Null (default): the engine default, Progress.Default. Assign a ProgressBar,
	/// ProgressDots or ProgressPlain to choose; the assigned one survives across calls.
	/// </summary>
	public static ITaskProgress? Progress { get; set; } = null;

	/// <summary>
	/// What reaches the console while git runs: Silent, Progress (default) or Detailed. Every transfer
	/// verb takes an Output property to override it for one call. The query verbs print nothing in any
	/// mode but Detailed.
	/// </summary>
	public static GitOutput Output { get; set; } = GitOutput.Progress;

	/// <summary>
	/// The reason of the last failure: a code (NotFound, AccessDenied, NetworkError, AlreadyExists,
	/// Different, NotSupported, Failed) and the first line of git's message. Reset by every verb; none
	/// while the last verb returned true.
	/// </summary>
	public static ApiError LastError { get; private set; } = ApiError.None;

	/// <summary>
	/// The full text git printed by the last command of the last verb, standard output and standard error,
	/// whatever the output mode. The internal queries of the extension (a hash, a configuration value) do
	/// not count.
	/// </summary>
	public static string LastOutput { get; private set; } = string.Empty;

	/// <summary>
	/// True makes any failing verb print its reason and abort the script (exit code 1) instead of
	/// returning false, so a script does not have to check every call. False by default. Only failures
	/// abort: the answers of a call that returned true (LastDiff.Changed, LastIgnored, AlreadyApplied in
	/// LastPatch) are not failures.
	/// </summary>
	public static bool AbortOnFailure { get; set; } = false;

	/// <summary>
	/// Milliseconds a git command may run before it is killed (see Tool.Timeout); the verbs with a
	/// Timeout option override it per call. Zero (default): no limit. A stalled network transfer is also
	/// stopped by git itself when the options give http.lowSpeedLimit and http.lowSpeedTime through Config.
	/// </summary>
	public static int Timeout { get; set; } = 0;

	/// <summary>
	/// The authentication inheritance policy: with Inherit.Enabled, the transfer verbs against a
	/// repository of the same host and owner as the source reuse the authentication of the source. See
	/// InheritSettings and the header of the file.
	/// </summary>
	public static InheritSettings Inherit { get; set; } = new InheritSettings();

	// ---------------------------------------------------------------- what the verbs found

	/// <summary>What the last Clone produced: the commit checked out, the URL used, whether the authentication was inherited.</summary>
	public static CloneResult LastClone { get; private set; } = new CloneResult();
	/// <summary>What the last Pull found: the status and the commits before and after.</summary>
	public static PullResult LastPull { get; private set; } = new PullResult();
	/// <summary>What the last Status found.</summary>
	public static StatusResult LastStatus { get; private set; } = new StatusResult();
	/// <summary>What the last Info found: the stamp record.</summary>
	public static GitInfo LastInfo { get; private set; } = new GitInfo();
	/// <summary>What the last LsRemote found: the references of the server and their commits.</summary>
	public static List<(string Reference, string Commit)> LastLsRemote { get; private set; } = new List<(string Reference, string Commit)>();
	/// <summary>What the last Diff found: whether anything changed, the files, the patch text.</summary>
	public static DiffResult LastDiff { get; private set; } = new DiffResult();
	/// <summary>What the last LsFiles found: the files.</summary>
	public static KList LastLsFiles { get; private set; } = new KList();
	/// <summary>What the last CheckIgnore found: whether the file is ignored.</summary>
	public static bool LastIgnored { get; private set; } = false;
	/// <summary>What the last RevParse found: the hash.</summary>
	public static string LastRevParse { get; private set; } = string.Empty;
	/// <summary>What the last MergeBase found: the common ancestor.</summary>
	public static string LastMergeBase { get; private set; } = string.Empty;
	/// <summary>What the last Patch did: the status, the rejected files, the strip level that applied.</summary>
	public static PatchResult LastPatch { get; private set; } = new PatchResult();
	/// <summary>What the last Clean removed, or would remove in a dry run.</summary>
	public static KList LastClean { get; private set; } = new KList();
	/// <summary>The folders in effect after the last SparseCheckout.</summary>
	public static KList LastSparseCheckout { get; private set; } = new KList();
	/// <summary>The working folders of the repository after the last Worktree.</summary>
	public static List<WorktreeEntry> LastWorktrees { get; private set; } = new List<WorktreeEntry>();
	/// <summary>What the last Lfs found: availability, version, the large files and the missing ones.</summary>
	public static LfsResult LastLfs { get; private set; } = new LfsResult();

	// ---------------------------------------------------------------- the runner

	/// <summary>
	/// One git invocation: the folder, the subcommand with its arguments and how it runs.
	/// </summary>
	private class Command {
		/// <summary>Repository folder (git -C). Null: the current folder.</summary>
		public string? Path = null;
		/// <summary>The subcommand and its arguments.</summary>
		public List<string> Args = new List<string>();
		/// <summary>Console output for this call; null takes Git.Output.</summary>
		public GitOutput? Output = null;
		/// <summary>A transfer: --progress is added in the Progress and Detailed modes and the progress line is rendered in Progress.</summary>
		public bool Transfer = false;
		/// <summary>Where --progress goes in Args: after the subcommand (1), or after "submodule update" (2).</summary>
		public int ProgressIndex = 1;
		/// <summary>Start message of the progress line, "Cloning sdl".</summary>
		public string Label = string.Empty;
		/// <summary>Attempts made again after a NetworkError.</summary>
		public int Retries = 0;
		/// <summary>True shows the detached HEAD notice.</summary>
		public bool Advice = false;
		/// <summary>Extra "key=value" configuration entries, each passed as -c.</summary>
		public List<string> Config = new List<string>();
		/// <summary>Milliseconds before the command is killed; zero takes Git.Timeout.</summary>
		public int Timeout = 0;
		/// <summary>Runs before every attempt after the first, to undo what a failed attempt left (a half cloned folder).</summary>
		public Action? BeforeRetry = null;
		/// <summary>An internal query of the extension (a hash, a configuration value): its output does not reach LastOutput, which keeps the text of the verb's own command.</summary>
		public bool Internal = false;
	}

	/// <summary>
	/// The command line of the last call, user info masked, for the failure messages and the log.
	/// </summary>
	private static string lastCommandLine = string.Empty;

	/// <summary>
	/// Runs one git command with the common arguments, the output mode, the progress line, the retries
	/// and the safe.directory retry. Sets LastOutput. Does not set LastError: the verb decides, since a
	/// non zero exit is an answer for some commands.
	/// </summary>
	private static ToolResult Run(Command c) {
		GitOutput mode = c.Output ?? Output;
		List<string> args = BuildArguments(c, mode, null);
		Tool tool = new Tool("git");
		tool.CaptureOutput = (mode == GitOutput.Detailed);
		tool.Timeout = c.Timeout > 0 ? c.Timeout : Timeout;
		Transfer? progress = null;
		if (c.Transfer && mode == GitOutput.Progress) {
			progress = new Transfer(Progress ?? Kltv.Kombine.Api.Progress.Default, c.Label);
			tool.OnStderr = (string s) => { progress.Fragment(s); };
		}
		ToolResult result = new ToolResult(new string[0], new string[0], Tool.ToolStatus.Failed, -1);
		int attempts = c.Retries + 1;
		bool safeDirectoryRetried = false;
		for (int attempt = 0; attempt < attempts; attempt++) {
			if (attempt > 0) {
				int delay = 1000 << (attempt - 1);
				Msg.Print("git: retrying in " + delay + " ms (" + (attempt + 1) + "/" + attempts + ")", Msg.LogLevels.Verbose);
				System.Threading.Thread.Sleep(delay);
				c.BeforeRetry?.Invoke();
			}
			lastCommandLine = "git " + string.Join(" ", args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a));
			Msg.Print("git: " + Mask(lastCommandLine), Msg.LogLevels.Verbose);
			progress?.Start();
			result = tool.CommandSync("git", args.ToArray());
			bool ok = result.ExitCode == 0;
			progress?.Finish(ok);
			if (!c.Internal)
				LastOutput = OutputText(result);
			if (ok)
				break;
			string errors = ErrorText(result);
			// The checkout belongs to another user: allowed once for this folder, then run again
			if (!safeDirectoryRetried && c.Path != null && errors.Contains("dubious ownership")) {
				safeDirectoryRetried = true;
				args = BuildArguments(c, mode, System.IO.Path.GetFullPath(c.Path));
				attempt--;
				continue;
			}
			if (Classify(result.ExitCode, errors) != ErrorCode.NetworkError)
				break;
		}
		return result;
	}

	/// <summary>
	/// The full argument list of a command: the common arguments, the extra configuration, the
	/// subcommand and --progress where it belongs.
	/// </summary>
	private static List<string> BuildArguments(Command c, GitOutput mode, string? safeDirectory) {
		List<string> args = new List<string>();
		if (!string.IsNullOrEmpty(c.Path)) {
			args.Add("-C");
			args.Add(c.Path);
		}
		args.Add("--no-pager");
		args.Add("-c"); args.Add("color.ui=never");
		args.Add("-c"); args.Add("core.quotepath=false");
		if (Host.IsWindows()) {
			args.Add("-c"); args.Add("core.longpaths=true");
		}
		if (!c.Advice) {
			args.Add("-c"); args.Add("advice.detachedHead=false");
		}
		if (safeDirectory != null) {
			args.Add("-c"); args.Add("safe.directory=" + safeDirectory);
		}
		foreach (string entry in c.Config) {
			args.Add("-c"); args.Add(entry);
		}
		List<string> verb = new List<string>(c.Args);
		if (c.Transfer && mode != GitOutput.Silent && c.ProgressIndex <= verb.Count)
			verb.Insert(c.ProgressIndex, "--progress");
		args.AddRange(verb);
		return args;
	}

	/// <summary>
	/// The standard output of a result as one text. The fragments carry their own line endings.
	/// </summary>
	private static string Text(ToolResult r) {
		return string.Concat(r.Stdout);
	}

	/// <summary>
	/// The standard error of a result as one text.
	/// </summary>
	private static string ErrorText(ToolResult r) {
		return string.Concat(r.Stderr);
	}

	/// <summary>
	/// Both outputs as one text, for LastOutput.
	/// </summary>
	private static string OutputText(ToolResult r) {
		string outText = Text(r);
		string errText = ErrorText(r);
		if (outText.Length > 0 && errText.Length > 0 && !outText.EndsWith("\n"))
			return outText + "\n" + errText;
		return outText + errText;
	}

	/// <summary>
	/// The standard output of a result as lines, endings removed, empty ones dropped.
	/// </summary>
	private static string[] Lines(ToolResult r) {
		return Text(r).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
	}

	/// <summary>
	/// The standard output of a result as NUL separated entries (the -z form), empty ones dropped.
	/// </summary>
	private static string[] Entries(ToolResult r) {
		return Text(r).Split('\0').Select(e => e.TrimEnd('\r', '\n')).Where(e => e.Length > 0).ToArray();
	}

	/// <summary>
	/// The first line of a text, endings removed.
	/// </summary>
	private static string FirstLine(string text) {
		foreach (string line in text.Split('\n')) {
			string t = line.Trim('\r', ' ', '\t');
			if (t.Length > 0)
				return t;
		}
		return string.Empty;
	}

	/// <summary>
	/// The line of git's error text that explains the failure: the first "fatal:" or "error:" line, the
	/// first non empty one otherwise, or the exit code when git said nothing.
	/// </summary>
	private static string Reason(ToolResult r) {
		string[] lines = ErrorText(r).Split('\n').Select(l => l.Trim('\r', ' ', '\t')).Where(l => l.Length > 0).ToArray();
		foreach (string l in lines)
			if (l.StartsWith("fatal:") || l.StartsWith("error:"))
				return l;
		if (lines.Length > 0)
			return lines[lines.Length - 1];
		return "git exited with code " + r.ExitCode;
	}

	/// <summary>
	/// Masks the user info of every http or https URL in a text (https://user:token@host becomes https://***@host); the user of an ssh URL is not a secret.
	/// </summary>
	private static readonly Regex UserInfoPattern = new Regex(@"(?<=https?://)[^/@\s""']+@", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static string Mask(string text) {
		return UserInfoPattern.Replace(text, "***@");
	}

	/// <summary>
	/// The error code of a failed command, from the exit code and git's message (English phrases; on a
	/// system where git speaks another language the code is Failed and the message is still git's).
	/// </summary>
	private static ErrorCode Classify(int exitCode, string errors) {
		string t = errors.ToLowerInvariant();
		if (exitCode == 0)
			return ErrorCode.None;
		if (Has(t, "authentication failed", "permission denied", "could not read username", "could not read password", "terminal prompts disabled", "dubious ownership", "index.lock", "access denied", "error: 403", "returned error: 401", "invalid username or password", "invalid credentials"))
			return ErrorCode.AccessDenied;
		if (Has(t, "could not resolve host", "connection timed out", "rpc failed", "early eof", "unable to access", "connection refused", "network is unreachable", "remote end hung up", "connection reset", "timeout after", "failed to connect", "timed out"))
			return ErrorCode.NetworkError;
		if (Has(t, "already exists and is not an empty directory", "already exists"))
			return ErrorCode.AlreadyExists;
		if (Has(t, "not possible to fast-forward", "diverging", "divergent branches", "non-fast-forward", "would be overwritten", "need to specify how to reconcile", "fetch first", "rejected"))
			return ErrorCode.Different;
		if (Has(t, "not a git repository", "does not appear to be a git repository", "repository not found", "couldn't find remote ref", "did not match any", "unknown revision", "no such file or directory", "does not exist", "not found", "bad revision", "no such remote", "ambiguous argument"))
			return ErrorCode.NotFound;
		return ErrorCode.Failed;
	}

	private static bool Has(string text, params string[] phrases) {
		foreach (string p in phrases)
			if (text.Contains(p))
				return true;
		return false;
	}

	/// <summary>
	/// Starts a verb: the last failure and output are reset.
	/// </summary>
	private static void Begin() {
		LastError = ApiError.None;
		LastOutput = string.Empty;
	}

	/// <summary>
	/// Records a failure: LastError is set, the reason logged at verbose level, and the script aborted
	/// when AbortOnFailure is set. Always returns false so a verb can "return Fail(...)".
	/// </summary>
	private static bool Fail(ErrorCode code, string message, string source) {
		LastError = new ApiError(code, message, source);
		Msg.PrintWarning("git: " + LastError.ToString(), Msg.LogLevels.Verbose);
		if (AbortOnFailure)
			Msg.PrintAndAbort("git failed (" + code + "): " + message + (source.Length > 0 ? " [" + source + "]" : string.Empty));
		return false;
	}

	/// <summary>
	/// Records the failure of a git command from its result.
	/// </summary>
	private static bool Fail(ToolResult r, string source) {
		return Fail(Classify(r.ExitCode, ErrorText(r)), Mask(Reason(r)), source);
	}

	/// <summary>
	/// True when the command succeeded.
	/// </summary>
	private static bool Ok(ToolResult r) {
		return r.ExitCode == 0;
	}

	/// <summary>
	/// The default timeout of a verb call: the option when given, Git.Timeout otherwise.
	/// </summary>
	private static int TimeoutOf(int option) {
		return option > 0 ? option : Timeout;
	}

	// ---------------------------------------------------------------- the progress line of a transfer

	/// <summary>
	/// Turns the progress messages git writes on standard error during a transfer into one progress
	/// line: "Cloning sdl: [bar] 45% receiving 45% done". Receiving maps to 10 to 80 percent, resolving
	/// deltas to 80 to 95, checking out to 95 to 100, the remote side counting to the first ten. A
	/// submodule clone restarts the line with the submodule name in the status.
	/// </summary>
	private class Transfer {
		private readonly ITaskProgress reporter;
		private readonly string label;
		private int clones = 0;
		private string unit = string.Empty;
		private static readonly Regex Phase = new Regex(@"(Enumerating objects|Counting objects|Compressing objects|Receiving objects|Resolving deltas|Updating files|Checking out files|Writing objects|Unpacking objects|Filtering content):\s+(\d+)%", RegexOptions.Compiled);
		private static readonly Regex Cloning = new Regex(@"Cloning into '([^']+)'", RegexOptions.Compiled);

		public Transfer(ITaskProgress reporter, string label) {
			this.reporter = reporter;
			this.label = label;
		}

		public void Start() {
			clones = 0;
			unit = string.Empty;
			reporter.Start(label);
		}

		public void Fragment(string s) {
			Match m = Cloning.Match(s);
			if (m.Success) {
				clones++;
				// The first clone is the repository of the label; the following ones are submodules
				unit = clones > 1 ? System.IO.Path.GetFileName(m.Groups[1].Value.TrimEnd('/', '\\')) + " " : string.Empty;
				return;
			}
			m = Phase.Match(s);
			if (!m.Success)
				return;
			string phase = m.Groups[1].Value;
			int pct = int.Parse(m.Groups[2].Value);
			double value;
			string what;
			switch (phase) {
				case "Receiving objects":
				case "Unpacking objects":
					value = 0.10 + pct * 0.70 / 100.0;
					what = "receiving";
					break;
				case "Writing objects":
					value = 0.10 + pct * 0.80 / 100.0;
					what = "writing";
					break;
				case "Resolving deltas":
					value = 0.80 + pct * 0.15 / 100.0;
					what = "resolving";
					break;
				case "Updating files":
				case "Checking out files":
				case "Filtering content":
					value = 0.95 + pct * 0.05 / 100.0;
					what = "checking out";
					break;
				default:
					value = 0.02 + pct * 0.08 / 100.0;
					what = "counting";
					break;
			}
			reporter.Report(value, unit + what + " " + pct + "%");
		}

		public void Finish(bool ok) {
			reporter.Finish(ok ? "done" : "failed", ok ? ProgressOutcome.Success : ProgressOutcome.Error);
		}
	}

	// ---------------------------------------------------------------- small helpers shared by the verbs

	/// <summary>
	/// The hash of a revision of a repository, or null when it cannot be resolved (no failure recorded).
	/// </summary>
	private static string? Hash(string path, string revision) {
		Command c = new Command();
		c.Path = path;
		c.Internal = true;
		c.Args.AddRange(new string[] { "rev-parse", "--verify", "--quiet", revision });
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return null;
		string[] lines = Lines(r);
		return lines.Length > 0 ? lines[0] : null;
	}

	/// <summary>
	/// True when the folder is inside a git working tree (no failure recorded).
	/// </summary>
	private static bool IsRepository(string path) {
		if (!Directory.Exists(path))
			return false;
		Command c = new Command();
		c.Path = path;
		c.Internal = true;
		c.Args.AddRange(new string[] { "rev-parse", "--is-inside-work-tree" });
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		return Ok(r) && Text(r).Trim() == "true";
	}

	/// <summary>
	/// A local configuration value of a repository, null when not set (no failure recorded).
	/// </summary>
	private static string? ConfigGet(string path, string key, bool local = true) {
		Command c = new Command();
		c.Path = path;
		c.Internal = true;
		c.Args.Add("config");
		if (local)
			c.Args.Add("--local");
		c.Args.Add("--get");
		c.Args.Add(key);
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return null;
		string[] lines = Lines(r);
		return lines.Length > 0 ? lines[0] : string.Empty;
	}

	/// <summary>
	/// Sets a local configuration value of a repository.
	/// </summary>
	private static bool ConfigSet(string path, string key, string value) {
		Command c = new Command();
		c.Path = path;
		c.Internal = true;
		c.Args.AddRange(new string[] { "config", "--local", key, value });
		c.Output = GitOutput.Silent;
		return Ok(Run(c));
	}

	/// <summary>
	/// True when a URL names a local path or a file URL: the local transport of git.
	/// </summary>
	private static bool IsLocal(string url) {
		return ParseUrl(url).Scheme == "file";
	}

	/// <summary>
	/// The verb label of a progress line: "Cloning sdl" from the target folder or URL.
	/// </summary>
	private static string LabelOf(string verb, string target) {
		string name = target.TrimEnd('/', '\\');
		int cut = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
		if (cut >= 0)
			name = name.Substring(cut + 1);
		if (name.EndsWith(".git"))
			name = name.Substring(0, name.Length - 4);
		return verb + " " + name;
	}

	// ---------------------------------------------------------------- version

	/// <summary>
	/// The installed git version, from "git --version". Every field is -1 when git is not available;
	/// LastError says so.
	/// </summary>
	/// <returns>The version.</returns>
	public static GitVersion Version() {
		Begin();
		GitVersion version = new GitVersion();
		Command c = new Command();
		c.Args.Add("--version");
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r)) {
			Fail(ErrorCode.NotFound, "git is not available: " + Reason(r), "git");
			return version;
		}
		// "git version 2.43.0.windows.1"
		string[] lines = Lines(r);
		string text = lines.Length > 0 ? lines[0] : string.Empty;
		Match m = Regex.Match(text, @"(\d+)\.(\d+)(?:\.(\d+))?");
		if (!m.Success) {
			Fail(ErrorCode.Failed, "unexpected version text: " + text, "git");
			return version;
		}
		version.Major = int.Parse(m.Groups[1].Value);
		version.Minor = int.Parse(m.Groups[2].Value);
		version.Revision = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
		return version;
	}

	/// <summary>
	/// True when the installed git is at least the given version. False, with LastError, when git is
	/// not available or older.
	/// </summary>
	/// <param name="Major">Required major version.</param>
	/// <param name="Minor">Required minor version.</param>
	/// <param name="Revision">Required revision.</param>
	/// <returns>True when the installed version is the required one or newer.</returns>
	public static bool VersionCheck(int Major, int Minor, int Revision) {
		GitVersion v = Version();
		if (v.Major < 0)
			return false;
		long installed = ((long)v.Major << 32) | ((long)v.Minor << 16) | (long)v.Revision;
		long required = ((long)Major << 32) | ((long)Minor << 16) | (long)Revision;
		if (installed >= required)
			return true;
		return Fail(ErrorCode.NotSupported, "git " + Major + "." + Minor + "." + Revision + " or newer is required, " + v.Major + "." + v.Minor + "." + v.Revision + " is installed", "git");
	}

	// ---------------------------------------------------------------- queries

	/// <summary>
	/// The first line of the output of a query command, null when the command fails (no failure recorded).
	/// </summary>
	private static string? Query(string? path, params string[] args) {
		Command c = new Command();
		c.Internal = true;
		c.Path = path;
		c.Args.AddRange(args);
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return null;
		string[] lines = Lines(r);
		return lines.Length > 0 ? lines[0] : string.Empty;
	}

	/// <summary>
	/// The output lines of a query command, null when the command fails (no failure recorded).
	/// </summary>
	private static string[]? QueryLines(string? path, params string[] args) {
		Command c = new Command();
		c.Internal = true;
		c.Path = path;
		c.Args.AddRange(args);
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		return Ok(r) ? Lines(r) : null;
	}

	/// <summary>
	/// The stamp of a repository, in LastInfo: commit, branch, tags, description, dirty state, dates,
	/// counts, remote and how it authenticates. What a version header, a package name or an artifact
	/// label needs (G32 of the design).
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See InfoOptions.</param>
	/// <returns>True with LastInfo filled; false with NotFound when the folder is not a repository or has no commit (LastInfo then still carries the remote URL, the top level folder and the authentication), unless the Environment option fills the stamp from the CI variables.</returns>
	public static bool Info(string path, InfoOptions? options = null) {
		Begin();
		InfoOptions o = options ?? new InfoOptions();
		GitInfo info = new GitInfo();
		LastInfo = info;
		if (!IsRepository(path)) {
			if (o.Environment && FillFromEnvironment(info))
				return true;
			return Fail(ErrorCode.NotFound, "not a git repository: " + path, path);
		}
		// Read first: known even for a repository without commits, which fails below
		info.RemoteUrl = Mask(ConfigGet(path, "remote.origin.url", false) ?? string.Empty);
		info.TopLevel = Query(path, "rev-parse", "--show-toplevel") ?? string.Empty;
		Profile profile = DetectProfile(path);
		info.AuthKind = profile.Kind;
		info.AuthBase = profile.Base;
		string? head = Hash(path, "HEAD");
		if (head == null)
			return Fail(ErrorCode.NotFound, "the repository has no commit yet: " + path, path);
		info.Commit = head;
		info.ShortCommit = Query(path, "rev-parse", o.Abbrev > 0 ? "--short=" + o.Abbrev : "--short", "HEAD") ?? head.Substring(0, Math.Min(8, head.Length));
		// Branch: empty when detached (a tag or a commit)
		string? branch = Query(path, "branch", "--show-current");
		if (branch == null)
			branch = Query(path, "symbolic-ref", "-q", "--short", "HEAD");
		info.Branch = branch ?? string.Empty;
		info.Detached = info.Branch.Length == 0;
		// Tags on the commit
		string[]? tags = QueryLines(path, "tag", "--points-at", "HEAD");
		if (tags != null && tags.Length > 0)
			info.Tag = tags[0];
		// Description from the nearest tag; the hash alone when no tag names the commit
		List<string> describe = new List<string> { "describe", "--tags", "--always", "--long", "--dirty" };
		describe.Add(o.Abbrev > 0 ? "--abbrev=" + o.Abbrev : "--abbrev=8");
		if (o.Match != null) {
			describe.Add("--match");
			describe.Add(o.Match);
		}
		if (o.FirstParent)
			describe.Add("--first-parent");
		info.Describe = Query(path, describe.ToArray()) ?? info.ShortCommit;
		Match since = Regex.Match(info.Describe, @"-(\d+)-g[0-9a-f]+(-dirty)?$");
		if (since.Success)
			info.CommitsSinceTag = int.Parse(since.Groups[1].Value);
		// Dirty: changes to tracked files, untracked ones only when asked
		string[]? status = QueryLines(path, "status", "--porcelain", o.Untracked ? "--untracked-files=all" : "--untracked-files=no");
		info.Dirty = status != null && status.Length > 0;
		// Shallow clone: the history is incomplete
		info.Shallow = (Query(path, "rev-parse", "--is-shallow-repository") ?? "false") == "true";
		// Dates, author and subject of the commit
		string[]? log = QueryLines(path, "log", "-1", "--format=%cI%n%ct%n%an%n%s");
		if (log != null && log.Length >= 4) {
			DateTime date;
			if (DateTime.TryParse(log[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out date))
				info.CommitDate = date;
			long epoch;
			if (long.TryParse(log[1], out epoch))
				info.CommitEpoch = epoch;
			info.Author = log[2];
			info.Subject = log[3];
		}
		// Commit count: an always growing build number
		int count;
		if (int.TryParse(Query(path, "rev-list", "--count", "HEAD") ?? "0", out count))
			info.CommitCount = count;
		// Ahead and behind the upstream branch
		string? ab = Query(path, "rev-list", "--left-right", "--count", "HEAD...@{upstream}");
		if (ab != null) {
			string[] parts = ab.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			int ahead, behind;
			if (parts.Length == 2 && int.TryParse(parts[0], out ahead) && int.TryParse(parts[1], out behind)) {
				info.Ahead = ahead;
				info.Behind = behind;
			}
		}
		return true;
	}

	/// <summary>
	/// Fills a stamp from the CI variables when there is no repository: GitHub Actions, GitLab CI and
	/// Azure Pipelines names. True when at least the commit was found.
	/// </summary>
	private static bool FillFromEnvironment(GitInfo info) {
		string? sha = System.Environment.GetEnvironmentVariable("GITHUB_SHA") ?? System.Environment.GetEnvironmentVariable("CI_COMMIT_SHA") ?? System.Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION");
		if (string.IsNullOrEmpty(sha))
			return false;
		info.Commit = sha;
		info.ShortCommit = sha.Substring(0, Math.Min(8, sha.Length));
		string? branch = System.Environment.GetEnvironmentVariable("GITHUB_HEAD_REF");
		if (string.IsNullOrEmpty(branch))
			branch = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? System.Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME") ?? System.Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");
		info.Branch = branch ?? string.Empty;
		info.Detached = info.Branch.Length == 0;
		info.Describe = info.ShortCommit;
		return true;
	}

	/// <summary>
	/// Turns a revision expression into a hash, in LastRevParse: "HEAD", "HEAD:src" (the hash of the
	/// tree of a folder, a stable key for the artifacts built from it), "v1.0^{commit}" (the commit a
	/// tag points to), "origin/main", "HEAD~3".
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="revision">The expression.</param>
	/// <param name="options">See RevParseOptions.</param>
	/// <returns>True with the hash in LastRevParse; false with NotFound when the expression does not resolve.</returns>
	public static bool RevParse(string path, string revision, RevParseOptions? options = null) {
		Begin();
		RevParseOptions o = options ?? new RevParseOptions();
		LastRevParse = string.Empty;
		Command c = new Command();
		c.Path = path;
		c.Args.AddRange(new string[] { "rev-parse", "--verify", "--quiet" });
		if (o.Short)
			c.Args.Add(o.Abbrev > 0 ? "--short=" + o.Abbrev : "--short");
		c.Args.Add(revision);
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(ErrorCode.NotFound, "the revision does not resolve: " + revision, path);
		string[] lines = Lines(r);
		LastRevParse = lines.Length > 0 ? lines[0] : string.Empty;
		return true;
	}

	/// <summary>
	/// The common ancestor of two revisions, in LastMergeBase: the base of "what changed in this
	/// branch". a is an ancestor of b when the base equals the hash of a.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="a">A revision.</param>
	/// <param name="b">Another revision.</param>
	/// <returns>True with the ancestor in LastMergeBase; false with NotFound when the revisions are unrelated or do not resolve.</returns>
	public static bool MergeBase(string path, string a, string b) {
		Begin();
		LastMergeBase = string.Empty;
		Command c = new Command();
		c.Path = path;
		c.Args.AddRange(new string[] { "merge-base", a, b });
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(ErrorCode.NotFound, "no common ancestor of " + a + " and " + b + ": " + Reason(r), path);
		string[] lines = Lines(r);
		LastMergeBase = lines.Length > 0 ? lines[0] : string.Empty;
		return true;
	}

	/// <summary>
	/// The files git tracks, or the untracked ones, honoring .gitignore and the submodule boundaries;
	/// in LastLsFiles, relative to the repository (G35 of the design).
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See LsFilesOptions.</param>
	/// <returns>True with the list in LastLsFiles.</returns>
	public static bool LsFiles(string path, LsFilesOptions? options = null) {
		Begin();
		LsFilesOptions o = options ?? new LsFilesOptions();
		LastLsFiles = new KList();
		Command c = new Command();
		c.Path = path;
		c.Args.AddRange(new string[] { "ls-files", "-z" });
		if (o.Others)
			c.Args.Add("--others");
		if (o.ExcludeStandard)
			c.Args.Add("--exclude-standard");
		if (o.RecurseSubmodules)
			c.Args.Add("--recurse-submodules");
		if (!string.IsNullOrEmpty(o.SubPath)) {
			c.Args.Add("--");
			c.Args.Add(o.SubPath);
		}
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, path);
		foreach (string f in Entries(r))
			LastLsFiles.Add(f);
		return true;
	}

	/// <summary>
	/// Whether a file is ignored by the repository (.gitignore and the other exclude files), in
	/// LastIgnored: a build can check that its outputs are ignored before writing them.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="file">File to check, relative to the repository or absolute.</param>
	/// <returns>True with the answer in LastIgnored; false when the folder is not a repository.</returns>
	public static bool CheckIgnore(string path, string file) {
		Begin();
		LastIgnored = false;
		Command c = new Command();
		c.Path = path;
		c.Args.AddRange(new string[] { "check-ignore", "-q", "--", file });
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		// 0: ignored, 1: not ignored, 128: not a repository or bad arguments
		if (r.ExitCode == 0) {
			LastIgnored = true;
			return true;
		}
		if (r.ExitCode == 1)
			return true;
		return Fail(r, path);
	}

	/// <summary>
	/// The state of a repository from the machine readable form of git status
	/// (--porcelain=v2 --branch), in LastStatus: the modified, added, deleted, renamed, untracked and
	/// conflicting files, the branch, its upstream and the commits ahead and behind it.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See StatusOptions.</param>
	/// <returns>True with LastStatus filled; false with NotFound when the folder is not a repository.</returns>
	public static bool Status(string path, StatusOptions? options = null) {
		Begin();
		StatusOptions o = options ?? new StatusOptions();
		StatusResult s = new StatusResult();
		LastStatus = s;
		Command c = new Command();
		c.Path = path;
		c.Args.AddRange(new string[] { "status", "--porcelain=v2", "--branch" });
		c.Args.Add(o.Untracked ? "--untracked-files=all" : "--untracked-files=no");
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, path);
		foreach (string line in Lines(r)) {
			if (line.StartsWith("# branch.head "))
				s.Branch = line.Substring(14) == "(detached)" ? string.Empty : line.Substring(14);
			else if (line.StartsWith("# branch.upstream "))
				s.Upstream = line.Substring(18);
			else if (line.StartsWith("# branch.ab ")) {
				Match ab = Regex.Match(line, @"\+(\d+) -(\d+)");
				if (ab.Success) {
					s.Ahead = int.Parse(ab.Groups[1].Value);
					s.Behind = int.Parse(ab.Groups[2].Value);
				}
			} else if (line.StartsWith("1 ") || line.StartsWith("2 ")) {
				// "1 XY sub mH mI mW hH hI path" and "2 XY sub mH mI mW hH hI Xscore path<tab>original"
				string[] parts = line.Split(' ');
				if (parts.Length < 9)
					continue;
				string xy = parts[1];
				int skip = line.StartsWith("1 ") ? 8 : 9;
				string rest = string.Join(" ", parts.Skip(skip));
				if (xy[0] == 'D' || xy[1] == 'D')
					s.Deleted.Add(rest);
				else if (xy[0] == 'R' || xy[0] == 'C') {
					string[] names = rest.Split('\t');
					s.Renamed.Add(names.Length == 2 ? names[1] + " -> " + names[0] : rest);
				} else if (xy[0] == 'A')
					s.Added.Add(rest);
				else
					s.Modified.Add(rest);
			} else if (line.StartsWith("u ")) {
				string[] parts = line.Split(' ');
				s.Conflicts.Add(string.Join(" ", parts.Skip(10)));
			} else if (line.StartsWith("? "))
				s.Untracked.Add(line.Substring(2));
		}
		if (o.Submodules) {
			// " sha path", "-sha path" not initialized, "+sha path" other commit, "Usha path" conflict
			string[]? subs = QueryLines(path, "submodule", "status", "--recursive");
			if (subs != null) {
				foreach (string line in subs) {
					if (line.Length < 2)
						continue;
					string[] parts = line.Substring(1).Split(' ');
					string sub = parts.Length > 1 ? parts[1] : line.Substring(1);
					if (line[0] == '+' || line[0] == '-')
						s.Modified.Add(sub);
					else if (line[0] == 'U')
						s.Conflicts.Add(sub);
				}
			}
		}
		s.Clean = s.Modified.Count() == 0 && s.Added.Count() == 0 && s.Deleted.Count() == 0 && s.Renamed.Count() == 0 && s.Conflicts.Count() == 0 && s.Untracked.Count() == 0;
		return true;
	}

	// ---------------------------------------------------------------- changes

	/// <summary>
	/// The files changed between two states, and the patch of those changes in the form Patch applies
	/// (a/ and b/ prefixes, one leading path component to strip). Without options: the index against the
	/// working tree, names only. LastDiff holds the files, whether anything changed, and the patch text
	/// when asked for and not written to a file. Untracked files are not part of a diff: Add them with
	/// IntentToAdd first.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See DiffOptions.</param>
	/// <returns>True with LastDiff filled (LastDiff.Changed says whether anything differs); false when git fails.</returns>
	public static bool Diff(string path, DiffOptions? options = null) {
		Begin();
		DiffOptions o = options ?? new DiffOptions();
		DiffResult d = new DiffResult();
		LastDiff = d;
		// The names first
		Command names = new Command();
		names.Path = path;
		names.Args.AddRange(new string[] { "diff", "--name-only", "-z" });
		AddDiffRange(names, o);
		names.Output = GitOutput.Silent;
		ToolResult r = Run(names);
		if (!Ok(r))
			return Fail(r, path);
		foreach (string f in Entries(r))
			d.Files.Add(f);
		d.Changed = d.Files.Count() > 0;
		if (!o.Patch)
			return true;
		// Then the patch itself
		Command patch = new Command();
		patch.Path = path;
		patch.Args.Add("diff");
		patch.Args.Add("-U" + o.Context);
		if (o.Binary)
			patch.Args.Add("--binary");
		if (!o.Renames)
			patch.Args.Add("--no-renames");
		if (o.IgnoreWhitespace)
			patch.Args.Add("-w");
		AddDiffRange(patch, o);
		patch.Output = GitOutput.Silent;
		r = Run(patch);
		if (!Ok(r))
			return Fail(r, path);
		string text = Text(r);
		if (o.Output != null) {
			try {
				string? folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(o.Output));
				if (!string.IsNullOrEmpty(folder))
					Directory.CreateDirectory(folder);
				File.WriteAllText(o.Output, text, new UTF8Encoding(false));
			} catch (Exception ex) {
				return Fail(ApiError.CodeOf(ex), "the patch could not be written: " + ex.Message, o.Output);
			}
		} else
			d.Patch = text;
		return true;
	}

	/// <summary>
	/// The common range arguments of the two diff commands: --cached, the filter, the revisions and the paths.
	/// </summary>
	private static void AddDiffRange(Command c, DiffOptions o) {
		if (o.Cached)
			c.Args.Add("--cached");
		if (!string.IsNullOrEmpty(o.Filter))
			c.Args.Add("--diff-filter=" + o.Filter);
		if (o.From != null)
			c.Args.Add(o.From);
		if (o.To != null)
			c.Args.Add(o.To);
		if (o.Paths != null && o.Paths.Length > 0) {
			c.Args.Add("--");
			c.Args.AddRange(o.Paths);
		}
	}

	/// <summary>
	/// The modified files of the staging area (or of the working tree with staged false) of the
	/// repository of the current folder, filtered by path prefixes and extensions; empty arrays mean no
	/// filter. The one verb that returns its list, being an existing one; implemented over Diff.
	/// </summary>
	/// <param name="paths">Path prefixes to keep, relative to the repository; empty keeps every path.</param>
	/// <param name="extensions">Extensions to keep (".c", ".h"); empty keeps every extension.</param>
	/// <param name="staged">True (default): the index against HEAD; false: the working tree against the index.</param>
	/// <returns>The files, empty when nothing matches or git fails (see LastError).</returns>
	public static KList GetModifiedFiles(string[] paths, string[] extensions, bool staged = true) {
		KList result = new KList();
		DiffOptions o = new DiffOptions();
		o.Cached = staged;
		if (!Diff(Folders.CurrentWorkingFolder, o))
			return result;
		foreach (string file in (string[])LastDiff.Files) {
			bool pathOk = paths.Length == 0 || paths.Any(p => file.StartsWith(p.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
			bool extOk = extensions.Length == 0 || extensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase));
			if (pathOk && extOk)
				result.Add(file);
		}
		return result;
	}

	// ---------------------------------------------------------------- staging, committing, tagging

	/// <summary>
	/// Adds files to the index of the repository of the current folder (or of the Path option).
	/// </summary>
	/// <param name="files">Files to add, relative to the repository; ignored with the All option.</param>
	/// <param name="options">See AddOptions.</param>
	/// <returns>True when git added them; false with the reason otherwise.</returns>
	public static bool Add(KList files, AddOptions? options = null) {
		Begin();
		AddOptions o = options ?? new AddOptions();
		Command c = new Command();
		c.Path = o.Path;
		c.Args.Add("add");
		if (o.All)
			c.Args.Add("-A");
		if (o.Update)
			c.Args.Add("-u");
		if (o.Force)
			c.Args.Add("-f");
		if (o.IntentToAdd)
			c.Args.Add("-N");
		if (!o.All) {
			string[] list = files;
			if (list.Length == 0)
				return Fail(ErrorCode.InvalidArgument, "no files to add", o.Path ?? Folders.CurrentWorkingFolder);
			c.Args.Add("--");
			c.Args.AddRange(list);
		}
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		return Ok(r) ? true : Fail(r, o.Path ?? Folders.CurrentWorkingFolder);
	}

	/// <summary>
	/// Commits the index (or every tracked change with the All option) with a message. The identity,
	/// the date and the hooks are controlled per call, so a build agent needs no global configuration.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="message">Commit message.</param>
	/// <param name="options">See CommitOptions.</param>
	/// <returns>True when the commit was made; false with the reason otherwise (nothing to commit is a failure unless AllowEmpty).</returns>
	public static bool Commit(string path, string message, CommitOptions? options = null) {
		Begin();
		CommitOptions o = options ?? new CommitOptions();
		Command c = new Command();
		c.Path = path;
		if (!string.IsNullOrEmpty(o.Name))
			c.Config.Add("user.name=" + o.Name);
		if (!string.IsNullOrEmpty(o.Email))
			c.Config.Add("user.email=" + o.Email);
		c.Args.AddRange(new string[] { "commit", "-m", message });
		if (o.All)
			c.Args.Add("-a");
		if (o.AllowEmpty)
			c.Args.Add("--allow-empty");
		if (o.NoVerify)
			c.Args.Add("--no-verify");
		if (o.Amend)
			c.Args.Add("--amend");
		if (o.Sign)
			c.Args.Add("-S");
		string? committerDate = null;
		if (o.Date.HasValue) {
			string date = o.Date.Value.ToString("o");
			c.Args.Add("--date=" + date);
			// The committer date only comes from the environment; set for this process, inherited by the command
			committerDate = System.Environment.GetEnvironmentVariable("GIT_COMMITTER_DATE");
			System.Environment.SetEnvironmentVariable("GIT_COMMITTER_DATE", date);
		}
		c.Output = GitOutput.Silent;
		ToolResult r;
		try {
			r = Run(c);
		} finally {
			if (o.Date.HasValue)
				System.Environment.SetEnvironmentVariable("GIT_COMMITTER_DATE", committerDate);
		}
		return Ok(r) ? true : Fail(r, path);
	}

	/// <summary>
	/// Creates, moves or deletes a tag. A message makes it an annotated tag, the kind releases use and
	/// --follow-tags pushes.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="name">Tag name.</param>
	/// <param name="options">See TagOptions.</param>
	/// <returns>True when git did it; false with the reason otherwise (an existing tag without Force is AlreadyExists).</returns>
	public static bool Tag(string path, string name, TagOptions? options = null) {
		Begin();
		TagOptions o = options ?? new TagOptions();
		Command c = new Command();
		c.Path = path;
		c.Args.Add("tag");
		if (o.Delete) {
			c.Args.Add("-d");
			c.Args.Add(name);
		} else {
			if (!string.IsNullOrEmpty(o.Message)) {
				c.Args.Add("-a");
				c.Args.Add("-m");
				c.Args.Add(o.Message);
			}
			if (o.Force)
				c.Args.Add("-f");
			if (o.Sign)
				c.Args.Add("-s");
			c.Args.Add(name);
			if (!string.IsNullOrEmpty(o.Commit))
				c.Args.Add(o.Commit);
		}
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		return Ok(r) ? true : Fail(r, path);
	}

	// ---------------------------------------------------------------- patching

	/// <summary>
	/// Applies a patch file (the output of Diff with the Patch option, or any unified diff) to a folder
	/// with git apply. The reverse check first detects a patch already applied, so a build can run
	/// twice; then the check, then the apply. LastPatch holds the status, the rejected files and the
	/// strip level that applied. Outside a repository git apply still works on a plain tree.
	/// </summary>
	/// <param name="patchFile">The patch file.</param>
	/// <param name="patchDir">The folder to apply it to, a repository or a plain tree.</param>
	/// <param name="options">See PatchOptions.</param>
	/// <returns>True when applied or already applied; false with the reason when the check or the apply fails.</returns>
	public static bool Patch(string patchFile, string patchDir, PatchOptions? options = null) {
		Begin();
		PatchOptions o = options ?? new PatchOptions();
		PatchResult result = new PatchResult();
		LastPatch = result;
		result.Strip = o.Strip;
		if (!File.Exists(patchFile))
			return Fail(ErrorCode.NotFound, "the patch file does not exist", patchFile);
		if (!Directory.Exists(patchDir))
			return Fail(ErrorCode.NotFound, "the folder to patch does not exist", patchDir);
		string patch = System.IO.Path.GetFullPath(patchFile);
		bool repository = IsRepository(patchDir);
		// Already applied: the reverse of the patch applies cleanly
		if (o.CheckReverse && Ok(RunApply(patchDir, patch, o.Strip, o, "--reverse", "--check"))) {
			result.Status = PatchStatus.AlreadyApplied;
			return true;
		}
		// The check, with the other strip level when allowed
		ToolResult check = RunApply(patchDir, patch, o.Strip, o, "--check");
		if (!Ok(check) && o.AutoStrip) {
			int other = o.Strip == 0 ? 1 : 0;
			ToolResult again = RunApply(patchDir, patch, other, o, "--check");
			if (Ok(again)) {
				result.Strip = other;
				check = again;
			}
		}
		if (!Ok(check)) {
			result.Status = PatchStatus.Failed;
			return Fail(ErrorCode.Failed, "the patch does not apply: " + Reason(check), patchFile);
		}
		// The apply itself
		List<string> extra = new List<string>();
		if (o.ThreeWay && repository)
			extra.Add("--3way");
		if (o.Reject)
			extra.Add("--reject");
		ToolResult apply = RunApply(patchDir, patch, result.Strip, o, extra.ToArray());
		if (!Ok(apply)) {
			result.Status = PatchStatus.Failed;
			foreach (Match m in Regex.Matches(ErrorText(apply), @"Applied patch to '([^']+)' with rejects"))
				result.Rejected.Add(m.Groups[1].Value);
			return Fail(ErrorCode.Failed, "the patch was not applied: " + Reason(apply), patchFile);
		}
		result.Status = PatchStatus.Applied;
		if (o.Commit && repository) {
			AddOptions all = new AddOptions();
			all.Path = patchDir;
			all.All = true;
			if (!Add(new KList(), all))
				return false;
			CommitOptions commit = new CommitOptions();
			commit.NoVerify = true;
			commit.Name = o.Name;
			commit.Email = o.Email;
			if (!Commit(patchDir, "kombine: patch " + System.IO.Path.GetFileName(patchFile), commit))
				return false;
		}
		return true;
	}

	/// <summary>
	/// One git apply invocation with the strip level, the whitespace options and extra switches.
	/// </summary>
	private static ToolResult RunApply(string patchDir, string patch, int strip, PatchOptions o, params string[] extra) {
		Command c = new Command();
		c.Path = patchDir;
		c.Args.Add("apply");
		c.Args.Add("-p" + strip);
		if (o.IgnoreWhitespace) {
			c.Args.Add("--ignore-whitespace");
			c.Args.Add("--whitespace=nowarn");
		}
		c.Args.AddRange(extra);
		c.Args.Add(patch);
		c.Output = GitOutput.Silent;
		return Run(c);
	}

	// ---------------------------------------------------------------- cleaning, packaging

	/// <summary>
	/// Lists or removes the untracked files of a repository (git clean). Without options nothing is
	/// removed: the files that would be are listed in LastClean. With DryRun false they are removed,
	/// the ignored ones too with Ignored, which includes build outputs.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See CleanOptions.</param>
	/// <returns>True with the files in LastClean; false with the reason otherwise.</returns>
	public static bool Clean(string path, CleanOptions? options = null) {
		Begin();
		CleanOptions o = options ?? new CleanOptions();
		LastClean = new KList();
		Command c = new Command();
		c.Path = path;
		c.Args.Add("clean");
		c.Args.Add(o.DryRun ? "-n" : "-f");
		if (o.Directories)
			c.Args.Add("-d");
		if (o.Ignored)
			c.Args.Add("-x");
		if (o.Exclude != null)
			foreach (string e in o.Exclude) {
				c.Args.Add("-e");
				c.Args.Add(e);
			}
		if (o.Paths != null && o.Paths.Length > 0) {
			c.Args.Add("--");
			c.Args.AddRange(o.Paths);
		}
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, path);
		foreach (string line in Lines(r)) {
			// "Would remove x", "Removing x", "Would skip repository x"
			if (line.StartsWith("Would remove "))
				LastClean.Add(line.Substring(13));
			else if (line.StartsWith("Removing "))
				LastClean.Add(line.Substring(9));
		}
		return true;
	}

	/// <summary>
	/// Writes the tree of a commit into an archive (git archive): a source package without .git,
	/// honoring the export-ignore and export-subst attributes. Git cannot include the submodules; the
	/// Submodules option lists the tracked files with them, copies them to a temporary folder and
	/// packs it with the Compress facility.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="output">Archive file to write; its extension gives the format unless the Format option does.</param>
	/// <param name="options">See ArchiveOptions.</param>
	/// <returns>True when the archive was written; false with the reason otherwise.</returns>
	public static bool Archive(string path, string output, ArchiveOptions? options = null) {
		Begin();
		ArchiveOptions o = options ?? new ArchiveOptions();
		string format = (o.Format ?? FormatOf(output)).ToLowerInvariant();
		if (format == "tgz")
			format = "tar.gz";
		if (format != "zip" && format != "tar" && format != "tar.gz")
			return Fail(ErrorCode.NotSupported, "unsupported archive format: " + format, output);
		string? folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output));
		if (!string.IsNullOrEmpty(folder))
			Directory.CreateDirectory(folder);
		if (!o.Submodules) {
			Command c = new Command();
			c.Path = path;
			c.Args.Add("archive");
			c.Args.Add("--format=" + format);
			if (!string.IsNullOrEmpty(o.Prefix))
				c.Args.Add("--prefix=" + o.Prefix.TrimEnd('/') + "/");
			c.Args.Add("-o");
			c.Args.Add(System.IO.Path.GetFullPath(output));
			c.Args.Add(o.Reference);
			if (o.Paths != null && o.Paths.Length > 0) {
				c.Args.Add("--");
				c.Args.AddRange(o.Paths);
			}
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			return Ok(r) ? true : Fail(r, path);
		}
		// With the submodules: the tracked files of the working tree, packed through Compress
		if (o.Reference != "HEAD")
			return Fail(ErrorCode.NotSupported, "with the submodules only the working tree (HEAD) can be archived", output);
		LsFilesOptions ls = new LsFilesOptions();
		ls.RecurseSubmodules = true;
		if (!LsFiles(path, ls))
			return false;
		string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kombine.archive." + Guid.NewGuid().ToString("N"));
		string root = string.IsNullOrEmpty(o.Prefix) ? System.IO.Path.Combine(temp, "root") : System.IO.Path.Combine(temp, o.Prefix.Trim('/', '\\'));
		try {
			foreach (string file in (string[])LastLsFiles) {
				if (o.Paths != null && o.Paths.Length > 0 && !o.Paths.Any(p => file.StartsWith(p.Replace('\\', '/').TrimEnd('/') + "/") || file == p))
					continue;
				string source = System.IO.Path.Combine(path, file);
				if (!File.Exists(source))
					continue;
				string target = System.IO.Path.Combine(root, file);
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
				File.Copy(source, target, true);
			}
			bool includeFolder = !string.IsNullOrEmpty(o.Prefix);
			bool packed;
			if (format == "zip")
				packed = Compress.Zip.CompressFolder(root, output, true, includeFolder, false);
			else
				packed = Compress.Tar.CompressFolder(root, output, true, includeFolder, format == "tar" ? TarCompressionType.None : TarCompressionType.Gzip, false);
			if (!packed)
				return Fail(ErrorCode.Failed, "the archive could not be written: " + (format == "zip" ? Compress.Zip.LastError.Message : Compress.Tar.LastError.Message), output);
			return true;
		} catch (Exception ex) {
			return Fail(ApiError.CodeOf(ex), "the archive could not be written: " + ex.Message, output);
		} finally {
			try { Directory.Delete(temp, true); } catch { }
		}
	}

	/// <summary>
	/// The archive format of an output file, from its extension.
	/// </summary>
	private static string FormatOf(string output) {
		string lower = output.ToLowerInvariant();
		if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz"))
			return "tar.gz";
		if (lower.EndsWith(".tar"))
			return "tar";
		return "zip";
	}

	// ---------------------------------------------------------------- working folders

	/// <summary>
	/// Manages the additional working folders of a repository (git worktree): a second folder checked
	/// out at another commit, to build two references side by side without a second clone. With no
	/// property set the call only lists them; LastWorktrees holds the list after every call.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See WorktreeOptions.</param>
	/// <returns>True when the operations succeeded and the list was read; false with the reason otherwise.</returns>
	public static bool Worktree(string path, WorktreeOptions? options = null) {
		Begin();
		WorktreeOptions o = options ?? new WorktreeOptions();
		LastWorktrees = new List<WorktreeEntry>();
		if (!string.IsNullOrEmpty(o.Add)) {
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "worktree", "add" });
			if (o.Detach)
				c.Args.Add("--detach");
			if (o.Force)
				c.Args.Add("--force");
			c.Args.Add(System.IO.Path.GetFullPath(o.Add));
			if (!string.IsNullOrEmpty(o.Reference))
				c.Args.Add(o.Reference);
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, path);
		}
		if (!string.IsNullOrEmpty(o.Remove)) {
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "worktree", "remove" });
			if (o.Force)
				c.Args.Add("--force");
			c.Args.Add(System.IO.Path.GetFullPath(o.Remove));
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, path);
		}
		if (o.Prune) {
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "worktree", "prune" });
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, path);
		}
		Command list = new Command();
		list.Path = path;
		list.Args.AddRange(new string[] { "worktree", "list", "--porcelain" });
		list.Output = GitOutput.Silent;
		ToolResult lr = Run(list);
		if (!Ok(lr))
			return Fail(lr, path);
		WorktreeEntry? current = null;
		foreach (string line in Text(lr).Split('\n').Select(l => l.TrimEnd('\r'))) {
			if (line.StartsWith("worktree ")) {
				current = new WorktreeEntry();
				current.Path = line.Substring(9);
				LastWorktrees.Add(current);
			} else if (current != null && line.StartsWith("HEAD "))
				current.Commit = line.Substring(5);
			else if (current != null && line.StartsWith("branch "))
				current.Branch = line.Substring(7).Replace("refs/heads/", string.Empty);
		}
		return true;
	}

	/// <summary>
	/// Restricts the working tree to the given folders (git sparse-checkout set), for a big repository
	/// of which the build needs a part; or widens it, or disables the restriction. The folders in effect
	/// after the call are in LastSparseCheckout.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="folders">Folders to keep, relative to the repository (patterns with Cone false).</param>
	/// <param name="options">See SparseCheckoutOptions.</param>
	/// <returns>True with the folders in LastSparseCheckout; false with the reason otherwise.</returns>
	public static bool SparseCheckout(string path, string[] folders, SparseCheckoutOptions? options = null) {
		Begin();
		SparseCheckoutOptions o = options ?? new SparseCheckoutOptions();
		LastSparseCheckout = new KList();
		Command c = new Command();
		c.Path = path;
		c.Args.Add("sparse-checkout");
		if (o.Disable)
			c.Args.Add("disable");
		else {
			if (folders == null || folders.Length == 0)
				return Fail(ErrorCode.InvalidArgument, "no folders given", path);
			c.Args.Add(o.Add ? "add" : "set");
			if (!o.Cone)
				c.Args.Add("--no-cone");
			c.Args.AddRange(folders);
		}
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, path);
		if (o.Disable)
			return true;
		string[]? list = QueryLines(path, "sparse-checkout", "list");
		if (list != null)
			foreach (string f in list)
				LastSparseCheckout.Add(f);
		return true;
	}

	// ---------------------------------------------------------------- hooks

	/// <summary>
	/// Installs a pre-commit hook (a script git runs before every commit) in the repository of the
	/// current folder or of the Path option, containing the given lines, the first one being the
	/// shebang. Missing lines are added to an existing hook (every line checked, duplicates removed);
	/// the hook is written with LF endings and made executable outside Windows. The hooks folder is
	/// found with git itself, so a worktree and a configured core.hooksPath are honored.
	/// </summary>
	/// <param name="requiredLines">The lines the hook must contain, the shebang first.</param>
	/// <param name="options">See HookOptions.</param>
	/// <returns>True when the hook is in place; false with the reason otherwise.</returns>
	public static bool InstallPreCommit(string[] requiredLines, HookOptions? options = null) {
		Begin();
		HookOptions o = options ?? new HookOptions();
		string repo = o.Path ?? Folders.CurrentWorkingFolder;
		if (requiredLines == null || requiredLines.Length == 0)
			return Fail(ErrorCode.InvalidArgument, "no lines given for the hook", repo);
		string? hooks = Query(repo, "rev-parse", "--git-path", "hooks");
		if (hooks == null)
			return Fail(ErrorCode.NotFound, "not a git repository: " + repo, repo);
		if (!System.IO.Path.IsPathRooted(hooks))
			hooks = System.IO.Path.Combine(repo, hooks);
		string hook = System.IO.Path.Combine(hooks, "pre-commit");
		try {
			Directory.CreateDirectory(hooks);
			List<string> lines = new List<string>();
			if (o.Merge && File.Exists(hook))
				lines.AddRange(File.ReadAllText(hook).Split('\n').Select(l => l.TrimEnd('\r')));
			// Trailing empty lines dropped, the shebang first, every required line present once
			while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
				lines.RemoveAt(lines.Count - 1);
			if (lines.Count == 0 || !lines[0].StartsWith("#!"))
				lines.Insert(0, requiredLines[0]);
			foreach (string required in requiredLines) {
				if (!lines.Any(l => l.Trim() == required.Trim()))
					lines.Add(required);
			}
			List<string> unique = new List<string>();
			foreach (string l in lines)
				if (l.Trim().Length == 0 || !unique.Any(u => u.Trim() == l.Trim()))
					unique.Add(l);
			File.WriteAllText(hook, string.Join("\n", unique) + "\n", new UTF8Encoding(false));
		} catch (Exception ex) {
			return Fail(ApiError.CodeOf(ex), "the hook could not be written: " + ex.Message, hook);
		}
		if (o.Executable && !Host.IsWindows()) {
			Tool chmod = new Tool("chmod");
			ToolResult r = chmod.CommandSync("chmod", new string[] { "+x", hook });
			if (r.ExitCode != 0)
				return Fail(ErrorCode.AccessDenied, "the hook could not be made executable", hook);
		}
		return true;
	}

	// ---------------------------------------------------------------- acquiring and updating

	/// <summary>
	/// Clones a repository. Without options: the full history, the submodules recursively, the detached
	/// HEAD notice silenced, the progress line shown, one attempt; what the previous extension did. With
	/// the Commit option the repository is pinned to a commit hash. With the inheritance enabled the
	/// authentication of the source repository is reused (see Git.Inherit). LastClone holds the commit
	/// checked out, the URL used and whether the authentication was inherited.
	/// </summary>
	/// <param name="uri">Repository URL (https, ssh, a local path or a bundle file).</param>
	/// <param name="path">Folder to clone into; must not exist or be empty.</param>
	/// <param name="branch">Branch or tag to check out; a tag gives a detached checkout. Null: the default branch.</param>
	/// <param name="options">See CloneOptions.</param>
	/// <returns>True when the clone is in place; false with the reason otherwise (AlreadyExists for a non empty folder, AccessDenied, NetworkError, NotFound).</returns>
	public static bool Clone(string uri, string path, string? branch = null, CloneOptions? options = null) {
		Begin();
		CloneOptions o = options ?? new CloneOptions();
		CloneResult result = new CloneResult();
		LastClone = result;
		result.Url = Mask(uri);
		if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(path))
			return Fail(ErrorCode.InvalidArgument, "the URL and the folder are required", uri);
		if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
			return Fail(ErrorCode.AlreadyExists, "the folder exists and is not empty", path);
		// The authentication of the source, when it applies to this target
		Plan plan = PlanFor(uri, o.Inherit, true);
		if (plan.Failed)
			return Fail(ErrorCode.NotFound, plan.Reason, uri);
		string url = plan.Url;
		result.Url = Mask(url);
		result.Inherited = plan.Apply;
		if (o.Commit != null) {
			if (!CloneByCommit(url, path, o, plan))
				return false;
		} else {
			Command c = new Command();
			c.Args.Add("clone");
			// Entries git clone writes into the new repository
			if (!o.DetachedHeadAdvice) {
				c.Args.Add("-c");
				c.Args.Add("advice.detachedHead=false");
			}
			if (o.Config != null)
				foreach (string e in o.Config) {
					c.Args.Add("-c");
					c.Args.Add(e);
				}
			if (o.SkipLfs) {
				c.Args.Add("-c"); c.Args.Add("filter.lfs.smudge=");
				c.Args.Add("-c"); c.Args.Add("filter.lfs.required=false");
			}
			if (!string.IsNullOrEmpty(branch)) {
				c.Args.Add("-b");
				c.Args.Add(branch);
			}
			if (o.Depth > 0)
				c.Args.Add("--depth=" + o.Depth);
			if (o.SingleBranch)
				c.Args.Add("--single-branch");
			if (o.NoTags)
				c.Args.Add("--no-tags");
			if (o.Submodules == SubmoduleMode.Recursive) {
				c.Args.Add("--recurse-submodules");
				if (o.SubmoduleDepth > 0)
					c.Args.Add("--shallow-submodules");
			}
			if (o.Jobs > 0)
				c.Args.Add("--jobs=" + o.Jobs);
			if (o.Filter == CloneFilter.Blobless)
				c.Args.Add("--filter=blob:none");
			else if (o.Filter == CloneFilter.Treeless)
				c.Args.Add("--filter=tree:0");
			if (o.Sparse != null && o.Sparse.Length > 0)
				c.Args.Add("--sparse");
			// A local source implies trust of local paths: the file transport its submodules need is allowed (refused by git since 2.38.1 otherwise)
			if (o.Submodules != SubmoduleMode.None && IsLocal(url))
				c.Config.Add("protocol.file.allow=always");
			if (!string.IsNullOrEmpty(o.Reference)) {
				c.Args.Add("--reference-if-able");
				c.Args.Add(o.Reference);
				c.Args.Add("--dissociate");
			}
			c.Args.Add(url);
			c.Args.Add(path);
			c.Config.AddRange(plan.Config);
			c.Advice = o.DetachedHeadAdvice;
			c.Transfer = true;
			c.Label = LabelOf("Cloning", path);
			c.Retries = o.Retries;
			c.Output = o.Output;
			c.Timeout = TimeoutOf(o.Timeout);
			// Git removes the folder it created when a clone fails; an empty one it found is left
			c.BeforeRetry = () => { try { if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); } catch { } };
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, Mask(uri));
			if (o.Sparse != null && o.Sparse.Length > 0 && !SparseCheckout(path, o.Sparse))
				return false;
			if (o.Submodules == SubmoduleMode.Init) {
				SubmoduleOptions sub = new SubmoduleOptions();
				sub.Recursive = false;
				sub.Depth = o.SubmoduleDepth;
				sub.Jobs = o.Jobs;
				sub.Inherit = o.Inherit;
				sub.Output = o.Output;
				if (!Submodule(path, sub))
					return false;
			}
		}
		result.Commit = Hash(path, "HEAD") ?? string.Empty;
		if (plan.Apply && Inherit.Persist)
			Pin(path, plan);
		return true;
	}

	/// <summary>
	/// The clone of a commit hash: git clone cannot take one, so the repository is initialized, the
	/// hash fetched with depth 1 (the whole history when the server refuses a hash), checked out
	/// detached and verified.
	/// </summary>
	private static bool CloneByCommit(string url, string path, CloneOptions o, Plan plan) {
		Directory.CreateDirectory(path);
		Command init = new Command();
		init.Path = path;
		init.Args.Add("init");
		init.Output = GitOutput.Silent;
		ToolResult r = Run(init);
		if (!Ok(r))
			return Fail(r, path);
		if (!o.DetachedHeadAdvice)
			ConfigSet(path, "advice.detachedHead", "false");
		if (o.Config != null)
			foreach (string e in o.Config) {
				int eq = e.IndexOf('=');
				if (eq > 0)
					ConfigSet(path, e.Substring(0, eq), e.Substring(eq + 1));
			}
		Command remote = new Command();
		remote.Path = path;
		remote.Args.AddRange(new string[] { "remote", "add", "origin", url });
		remote.Output = GitOutput.Silent;
		r = Run(remote);
		if (!Ok(r))
			return Fail(r, path);
		// The hash itself, shallow; the server may refuse a hash it does not advertise
		Command fetch = new Command();
		fetch.Path = path;
		fetch.Args.AddRange(new string[] { "fetch", "--depth=1", "origin", o.Commit! });
		if (o.Filter == CloneFilter.Blobless)
			fetch.Args.Add("--filter=blob:none");
		else if (o.Filter == CloneFilter.Treeless)
			fetch.Args.Add("--filter=tree:0");
		fetch.Config.AddRange(plan.Config);
		fetch.Transfer = true;
		fetch.Label = LabelOf("Cloning", path);
		fetch.Retries = o.Retries;
		fetch.Output = o.Output;
		fetch.Timeout = TimeoutOf(o.Timeout);
		r = Run(fetch);
		if (!Ok(r)) {
			Msg.Print("git: the server refused the hash, fetching everything: " + Reason(r), Msg.LogLevels.Verbose);
			Command full = new Command();
			full.Path = path;
			full.Args.AddRange(new string[] { "fetch", "origin" });
			full.Config.AddRange(plan.Config);
			full.Transfer = true;
			full.Label = LabelOf("Cloning", path);
			full.Retries = o.Retries;
			full.Output = o.Output;
			full.Timeout = TimeoutOf(o.Timeout);
			r = Run(full);
			if (!Ok(r))
				return Fail(r, Mask(url));
		}
		Command checkout = new Command();
		checkout.Path = path;
		checkout.Args.AddRange(new string[] { "checkout", "--detach", o.Commit! });
		checkout.Advice = o.DetachedHeadAdvice;
		checkout.Output = GitOutput.Silent;
		r = Run(checkout);
		if (!Ok(r))
			return Fail(r, path);
		string? head = Hash(path, "HEAD");
		if (head == null || !head.StartsWith(o.Commit!, StringComparison.OrdinalIgnoreCase))
			return Fail(ErrorCode.Different, "the checkout is not the requested commit: " + (head ?? "none"), path);
		if (o.Submodules != SubmoduleMode.None) {
			SubmoduleOptions sub = new SubmoduleOptions();
			sub.Recursive = o.Submodules == SubmoduleMode.Recursive;
			sub.Depth = o.SubmoduleDepth;
			sub.Jobs = o.Jobs;
			sub.Inherit = o.Inherit;
			sub.Output = o.Output;
			if (!Submodule(path, sub))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Updates a repository from its remote. Without options: fast forward only (the branch moves
	/// forward or the call returns false with the Diverged status, never a merge commit), submodules
	/// updated, one attempt. LastPull holds the status and the commits before and after.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See PullOptions.</param>
	/// <returns>True when the tree is up to date or was updated; false with Diverged, Dirty or Failed in LastPull and the reason in LastError.</returns>
	public static bool Pull(string path, PullOptions? options = null) {
		Begin();
		PullOptions o = options ?? new PullOptions();
		PullResult result = new PullResult();
		LastPull = result;
		if (!IsRepository(path))
			return Fail(ErrorCode.NotFound, "not a git repository: " + path, path);
		string origin = ConfigGet(path, "remote.origin.url", false) ?? string.Empty;
		if (o.ExpectedRemote != null && !SameTarget(origin, o.ExpectedRemote))
			return Fail(ErrorCode.Different, "the origin is " + Mask(origin) + ", not " + Mask(o.ExpectedRemote), path);
		if (o.RequireClean) {
			StatusOptions so = new StatusOptions();
			so.Untracked = false;
			if (!Status(path, so))
				return false;
			if (!LastStatus.Clean) {
				result.Status = PullStatus.Dirty;
				return Fail(ErrorCode.Different, "the tree has uncommitted changes", path);
			}
		}
		result.From = Hash(path, "HEAD") ?? string.Empty;
		Plan plan = PlanFor(origin, o.Inherit, false);
		if (plan.Failed)
			return Fail(ErrorCode.NotFound, plan.Reason, path);
		Command c = new Command();
		c.Path = path;
		if (o.Mode == PullMode.Reset) {
			c.Args.Add("fetch");
			if (o.Prune) {
				c.Args.Add("--prune");
				c.Args.Add("--prune-tags");
			}
			if (o.Tags) {
				c.Args.Add("--tags");
				c.Args.Add("--force");
			}
		} else {
			c.Args.Add("pull");
			if (o.Mode == PullMode.FastForward)
				c.Args.Add("--ff-only");
			else if (o.Mode == PullMode.Rebase)
				c.Args.Add("--rebase");
			else
				c.Args.Add("--no-rebase");
			if (o.Prune)
				c.Args.Add("--prune");
			if (o.Tags)
				c.Args.Add("--tags");
			if (o.Submodules)
				c.Args.Add("--recurse-submodules");
		}
		if (o.Config != null)
			c.Config.AddRange(o.Config);
		c.Config.AddRange(plan.Config);
		c.Transfer = true;
		c.Label = LabelOf(o.Mode == PullMode.Reset ? "Fetching" : "Pulling", path);
		c.Retries = o.Retries;
		c.Output = o.Output;
		c.Timeout = TimeoutOf(o.Timeout);
		ToolResult r = Run(c);
		if (!Ok(r)) {
			string errors = ErrorText(r).ToLowerInvariant();
			if (errors.Contains("would be overwritten") || errors.Contains("uncommitted changes") || errors.Contains("unstaged changes"))
				result.Status = PullStatus.Dirty;
			else if (Classify(r.ExitCode, errors) == ErrorCode.Different)
				result.Status = PullStatus.Diverged;
			else
				result.Status = PullStatus.Failed;
			return Fail(r, path);
		}
		if (o.Mode == PullMode.Reset) {
			// The remote reference of the branch, or the remote default branch when detached
			string? upstream = Query(path, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}");
			if (upstream == null) {
				string? remoteHead = Query(path, "symbolic-ref", "--short", "refs/remotes/origin/HEAD");
				upstream = remoteHead ?? "origin/HEAD";
			}
			Command reset = new Command();
			reset.Path = path;
			reset.Args.AddRange(new string[] { "reset", "--hard", upstream });
			reset.Output = GitOutput.Silent;
			ToolResult rr = Run(reset);
			if (!Ok(rr)) {
				result.Status = PullStatus.Failed;
				return Fail(rr, path);
			}
		}
		if (o.Submodules) {
			SubmoduleOptions sub = new SubmoduleOptions();
			sub.Inherit = o.Inherit;
			sub.Output = o.Output;
			if (!Submodule(path, sub)) {
				result.Status = PullStatus.Failed;
				return false;
			}
		}
		result.To = Hash(path, "HEAD") ?? string.Empty;
		result.Status = result.From == result.To ? PullStatus.UpToDate : PullStatus.Updated;
		if (plan.Apply && Inherit.Persist)
			Refresh(path, plan);
		return true;
	}

	/// <summary>
	/// Downloads commits, branches and tags from the remote without touching the working tree. Without
	/// options, plain "git fetch".
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See FetchOptions.</param>
	/// <returns>True when the fetch succeeded; false with the reason otherwise.</returns>
	public static bool Fetch(string path, FetchOptions? options = null) {
		Begin();
		FetchOptions o = options ?? new FetchOptions();
		if (!IsRepository(path))
			return Fail(ErrorCode.NotFound, "not a git repository: " + path, path);
		string origin = ConfigGet(path, "remote." + (o.Remote ?? "origin") + ".url", false) ?? string.Empty;
		Plan plan = PlanFor(origin, o.Inherit, false);
		if (plan.Failed)
			return Fail(ErrorCode.NotFound, plan.Reason, path);
		Command c = new Command();
		c.Path = path;
		c.Args.Add("fetch");
		if (o.Prune) {
			c.Args.Add("--prune");
			c.Args.Add("--prune-tags");
		}
		if (o.Tags) {
			c.Args.Add("--tags");
			c.Args.Add("--force");
		}
		if (o.Depth > 0)
			c.Args.Add("--depth=" + o.Depth);
		if (o.Deepen > 0)
			c.Args.Add("--deepen=" + o.Deepen);
		if (o.Unshallow)
			c.Args.Add("--unshallow");
		if (!string.IsNullOrEmpty(o.Remote) || !string.IsNullOrEmpty(o.Reference))
			c.Args.Add(o.Remote ?? "origin");
		if (!string.IsNullOrEmpty(o.Reference))
			c.Args.Add(o.Reference);
		c.Config.AddRange(plan.Config);
		c.Transfer = true;
		c.Label = LabelOf("Fetching", path);
		c.Retries = o.Retries;
		c.Output = o.Output;
		c.Timeout = TimeoutOf(o.Timeout);
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, path);
		if (plan.Apply && Inherit.Persist)
			Refresh(path, plan);
		return true;
	}

	/// <summary>
	/// Moves the working tree to a branch, tag or commit. A branch stays attached; a tag or a commit
	/// gives a detached checkout, the pinned dependency case; Detach detaches a branch too. Fetch
	/// brings the reference from origin first, Submodules updates the submodules after.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="reference">Branch, tag or commit hash.</param>
	/// <param name="options">See CheckoutOptions.</param>
	/// <returns>True when the tree is at the reference; false with the reason otherwise.</returns>
	public static bool Checkout(string path, string reference, CheckoutOptions? options = null) {
		Begin();
		CheckoutOptions o = options ?? new CheckoutOptions();
		if (!IsRepository(path))
			return Fail(ErrorCode.NotFound, "not a git repository: " + path, path);
		if (o.Fetch) {
			FetchOptions fo = new FetchOptions();
			fo.Reference = reference;
			// A hash may not be fetchable by name; a tag or a branch is
			if (!Fetch(path, fo)) {
				FetchOptions tags = new FetchOptions();
				tags.Tags = true;
				if (!Fetch(path, tags))
					return false;
			}
		}
		Command c = new Command();
		c.Path = path;
		c.Args.Add("checkout");
		if (o.Detach)
			c.Args.Add("--detach");
		if (o.Force)
			c.Args.Add("--force");
		c.Args.Add(reference);
		c.Advice = o.DetachedHeadAdvice;
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, path);
		if (o.Submodules && !Submodule(path))
			return false;
		return true;
	}

	/// <summary>
	/// Brings the submodules of a repository to the commits it records (git submodule update). Without
	/// options: --init --recursive, so new submodules are initialized and nested ones followed.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See SubmoduleOptions.</param>
	/// <returns>True when every submodule is at its recorded commit; false with the reason otherwise.</returns>
	public static bool Submodule(string path, SubmoduleOptions? options = null) {
		Begin();
		SubmoduleOptions o = options ?? new SubmoduleOptions();
		if (!IsRepository(path))
			return Fail(ErrorCode.NotFound, "not a git repository: " + path, path);
		string origin = ConfigGet(path, "remote.origin.url", false) ?? string.Empty;
		Plan plan = PlanFor(origin, o.Inherit, false);
		if (plan.Failed)
			return Fail(ErrorCode.NotFound, plan.Reason, path);
		if (o.Sync) {
			Command sync = new Command();
			sync.Path = path;
			sync.Args.AddRange(new string[] { "submodule", "sync", "--recursive" });
			sync.Output = GitOutput.Silent;
			ToolResult sr = Run(sync);
			if (!Ok(sr))
				return Fail(sr, path);
		}
		Command c = new Command();
		c.Path = path;
		c.Args.AddRange(new string[] { "submodule", "update" });
		if (o.Init)
			c.Args.Add("--init");
		if (o.Recursive)
			c.Args.Add("--recursive");
		if (o.Depth > 0)
			c.Args.Add("--depth=" + o.Depth);
		if (o.Jobs > 0)
			c.Args.Add("--jobs=" + o.Jobs);
		if (o.Remote)
			c.Args.Add("--remote");
		if (o.Force)
			c.Args.Add("--force");
		c.Config.AddRange(plan.Config);
		// A local origin implies trust of local paths: the file transport of local submodules is allowed (refused by git since 2.38.1 otherwise)
		if (IsLocal(origin))
			c.Config.Add("protocol.file.allow=always");
		c.Transfer = true;
		c.ProgressIndex = 2;
		c.Label = LabelOf("Updating submodules of", path);
		c.Output = o.Output;
		c.Timeout = TimeoutOf(o.Timeout);
		ToolResult r = Run(c);
		return Ok(r) ? true : Fail(r, path);
	}

	/// <summary>
	/// Pushes commits or tags to the remote. Without options, plain "git push": the current branch to
	/// its remote. Never a plain --force: ForceWithLease refuses when someone else pushed meanwhile.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See PushOptions.</param>
	/// <returns>True when the push was accepted; false with the reason otherwise (a rejected push is Different).</returns>
	public static bool Push(string path, PushOptions? options = null) {
		Begin();
		PushOptions o = options ?? new PushOptions();
		if (!IsRepository(path))
			return Fail(ErrorCode.NotFound, "not a git repository: " + path, path);
		string remoteName = o.Remote ?? "origin";
		string pushUrl = ConfigGet(path, "remote." + remoteName + ".pushurl", false) ?? ConfigGet(path, "remote." + remoteName + ".url", false) ?? string.Empty;
		Plan plan = PlanFor(pushUrl, o.Inherit, false, true);
		if (plan.Failed)
			return Fail(ErrorCode.NotFound, plan.Reason, path);
		Command c = new Command();
		c.Path = path;
		c.Args.Add("push");
		if (o.Tags)
			c.Args.Add("--tags");
		if (o.FollowTags)
			c.Args.Add("--follow-tags");
		if (o.ForceWithLease)
			c.Args.Add("--force-with-lease");
		if (o.SetUpstream)
			c.Args.Add("-u");
		if (o.Delete)
			c.Args.Add("--delete");
		if (o.DryRun)
			c.Args.Add("--dry-run");
		if (!string.IsNullOrEmpty(o.Remote) || !string.IsNullOrEmpty(o.Refspec))
			c.Args.Add(remoteName);
		if (!string.IsNullOrEmpty(o.Refspec))
			c.Args.Add(o.Refspec);
		c.Config.AddRange(plan.Config);
		c.Transfer = true;
		c.Label = LabelOf("Pushing", path);
		c.Retries = o.Retries;
		c.Output = o.Output;
		c.Timeout = TimeoutOf(o.Timeout);
		ToolResult r = Run(c);
		return Ok(r) ? true : Fail(r, path);
	}

	/// <summary>
	/// Asks a server for its branches and tags with their commit hashes, without cloning (git
	/// ls-remote): the cheap "did anything change" check against a local commit, and the way to find
	/// the newest version tag of a dependency. LastLsRemote holds the pairs.
	/// </summary>
	/// <param name="uri">Repository URL.</param>
	/// <param name="options">See LsRemoteOptions.</param>
	/// <returns>True with the pairs in LastLsRemote (empty when nothing matches); false with the reason when the server cannot be reached or refuses.</returns>
	public static bool LsRemote(string uri, LsRemoteOptions? options = null) {
		Begin();
		LsRemoteOptions o = options ?? new LsRemoteOptions();
		LastLsRemote = new List<(string Reference, string Commit)>();
		Plan plan = PlanFor(uri, o.Inherit, false);
		if (plan.Failed)
			return Fail(ErrorCode.NotFound, plan.Reason, uri);
		Command c = new Command();
		c.Args.Add("ls-remote");
		if (o.Heads)
			c.Args.Add("--heads");
		if (o.Tags)
			c.Args.Add("--tags");
		if (o.Refs)
			c.Args.Add("--refs");
		if (!string.IsNullOrEmpty(o.Sort))
			c.Args.Add("--sort=" + o.Sort);
		c.Args.Add(plan.Url);
		if (!string.IsNullOrEmpty(o.Pattern))
			c.Args.Add(o.Pattern);
		c.Config.AddRange(plan.Config);
		c.Retries = o.Retries;
		c.Output = GitOutput.Silent;
		c.Timeout = TimeoutOf(o.Timeout);
		ToolResult r = Run(c);
		if (!Ok(r))
			return Fail(r, Mask(uri));
		foreach (string line in Lines(r)) {
			int tab = line.IndexOf('\t');
			if (tab > 0)
				LastLsRemote.Add((line.Substring(tab + 1), line.Substring(0, tab)));
		}
		return true;
	}

	/// <summary>
	/// Git Large File Storage: checks that the git lfs command is available, enables it in a
	/// repository, downloads the large files of the checkout, lists them and the ones still present as
	/// pointer files. LastLfs holds what was found.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See LfsOptions.</param>
	/// <returns>True when git lfs is available and the asked operations succeeded; false with NotSupported when the command is missing, the reason otherwise.</returns>
	public static bool Lfs(string path, LfsOptions? options = null) {
		Begin();
		LfsOptions o = options ?? new LfsOptions();
		LfsResult result = new LfsResult();
		LastLfs = result;
		string? version = Query(null, "lfs", "version");
		if (version == null)
			return Fail(ErrorCode.NotSupported, "the git lfs command is not available", "git lfs");
		result.Available = true;
		Match m = Regex.Match(version, @"(\d+\.\d+\.\d+)");
		result.Version = m.Success ? m.Groups[1].Value : version;
		if (o.Install) {
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "lfs", "install", "--local" });
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, path);
		}
		if (o.Pull) {
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "lfs", "pull" });
			if (o.Include != null && o.Include.Length > 0)
				c.Args.Add("--include=" + string.Join(",", o.Include));
			if (o.Exclude != null && o.Exclude.Length > 0)
				c.Args.Add("--exclude=" + string.Join(",", o.Exclude));
			c.Output = Output;
			c.Timeout = Timeout;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, path);
		}
		if (o.List || o.Pull) {
			// "<oid> * path" downloaded, "<oid> - path" pointer only
			string[]? files = QueryLines(path, "lfs", "ls-files");
			if (files == null)
				return Fail(ErrorCode.Failed, "the large files could not be listed", path);
			foreach (string line in files) {
				Match f = Regex.Match(line, @"^\S+\s+([*-])\s+(.+)$");
				if (!f.Success)
					continue;
				result.Files.Add(f.Groups[2].Value);
				if (f.Groups[1].Value == "-")
					result.Missing.Add(f.Groups[2].Value);
			}
		}
		return true;
	}

	/// <summary>
	/// Packs a repository into one file (git bundle create), or checks a bundle file against the
	/// repository (bundle verify), for agents without network. A bundle is cloned with Clone: git
	/// accepts a bundle file as the source of a clone.
	/// </summary>
	/// <param name="path">Repository folder.</param>
	/// <param name="options">See BundleOptions; Create and Verify may both be given.</param>
	/// <returns>True when the asked operations succeeded; false with the reason otherwise.</returns>
	public static bool Bundle(string path, BundleOptions? options = null) {
		Begin();
		BundleOptions o = options ?? new BundleOptions();
		if (string.IsNullOrEmpty(o.Create) && string.IsNullOrEmpty(o.Verify))
			return Fail(ErrorCode.InvalidArgument, "nothing to do: give Create or Verify", path);
		if (!string.IsNullOrEmpty(o.Create)) {
			string? folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(o.Create));
			if (!string.IsNullOrEmpty(folder))
				Directory.CreateDirectory(folder);
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "bundle", "create", System.IO.Path.GetFullPath(o.Create) });
			if (o.References != null && o.References.Length > 0)
				c.Args.AddRange(o.References);
			else
				c.Args.Add("--all");
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, path);
		}
		if (!string.IsNullOrEmpty(o.Verify)) {
			Command c = new Command();
			c.Path = path;
			c.Args.AddRange(new string[] { "bundle", "verify", System.IO.Path.GetFullPath(o.Verify) });
			c.Output = GitOutput.Silent;
			ToolResult r = Run(c);
			if (!Ok(r))
				return Fail(r, o.Verify);
		}
		return true;
	}

	// ================================================================================================
	// Authentication inheritance
	//
	// A profile is read from the source repository (how it authenticates), every target URL is read with
	// the rule of its hosting service, the scope decides whether the profile applies, and the profile
	// is applied to the command (a rewritten URL for ssh, configuration entries otherwise). After a
	// clone the non secret part is written into the child as its account pin, with a marker.
	// ================================================================================================

	// ---------------------------------------------------------------- hosting service rules

	/// <summary>
	/// The built in rules, by service.
	/// </summary>
	private static readonly Dictionary<GitService, HostRule> BuiltInRules = new Dictionary<GitService, HostRule> {
		{ GitService.Generic, new HostRule { Service = GitService.Generic, Hosts = new string[0], HttpsPath = "{owner}/{repo}", SshPath = "{owner}/{repo}" } },
		{ GitService.GitHub, new HostRule { Service = GitService.GitHub, Hosts = new string[] { "github.com" }, HttpsPath = "{owner}/{repo}", SshPath = "{owner}/{repo}" } },
		{ GitService.GitLab, new HostRule { Service = GitService.GitLab, Hosts = new string[] { "gitlab.com" }, HttpsPath = "{owner}/{...}/{repo}", SshPath = "{owner}/{...}/{repo}" } },
		{ GitService.Bitbucket, new HostRule { Service = GitService.Bitbucket, Hosts = new string[] { "bitbucket.org" }, HttpsPath = "{owner}/{repo}", SshPath = "{owner}/{repo}" } },
		{ GitService.AzureDevOps, new HostRule { Service = GitService.AzureDevOps, Hosts = new string[] { "dev.azure.com", "ssh.dev.azure.com", "*.visualstudio.com", "vs-ssh.visualstudio.com" }, HttpsPath = "{owner}/{project}/_git/{repo}", SshPath = "v3/{owner}/{project}/{repo}" } },
		{ GitService.Gitea, new HostRule { Service = GitService.Gitea, Hosts = new string[] { "codeberg.org" }, HttpsPath = "{owner}/{repo}", SshPath = "{owner}/{repo}" } },
	};

	/// <summary>
	/// The built in rule of a service.
	/// </summary>
	/// <param name="service">The service.</param>
	/// <returns>The rule (the generic one for an unknown service).</returns>
	public static HostRule BuiltInRule(GitService service) {
		HostRule? rule;
		return BuiltInRules.TryGetValue(service, out rule) ? rule : BuiltInRules[GitService.Generic];
	}

	/// <summary>
	/// The rule of a host name: an explicit entry of Inherit.Hosts, the built in table, or the generic rule.
	/// </summary>
	private static HostRule RuleOf(string host) {
		HostRule? rule;
		if (Inherit.Hosts.TryGetValue(host, out rule))
			return rule;
		foreach (HostRule candidate in BuiltInRules.Values) {
			foreach (string name in candidate.Hosts) {
				if (name.StartsWith("*.")) {
					if (host.EndsWith(name.Substring(1), StringComparison.OrdinalIgnoreCase) && host.Length > name.Length - 1)
						return candidate;
				} else if (string.Equals(name, host, StringComparison.OrdinalIgnoreCase))
					return candidate;
			}
		}
		return BuiltInRules[GitService.Generic];
	}

	/// <summary>
	/// The name under which a host is compared: the hosts of one service compare equal (dev.azure.com
	/// and ssh.dev.azure.com), an alias compares as its real host.
	/// </summary>
	private static string CanonicalHost(string realHost, HostRule rule) {
		if (rule.Hosts.Length > 0) {
			foreach (string name in rule.Hosts) {
				bool match = name.StartsWith("*.") ? realHost.EndsWith(name.Substring(1), StringComparison.OrdinalIgnoreCase) : string.Equals(name, realHost, StringComparison.OrdinalIgnoreCase);
				if (match)
					return rule.Hosts[0].TrimStart('*', '.').ToLowerInvariant();
			}
		}
		return realHost.ToLowerInvariant();
	}

	// ---------------------------------------------------------------- URLs

	/// <summary>
	/// A repository URL read with the rule of its hosting service.
	/// </summary>
	private class GitUrl {
		/// <summary>The URL as given.</summary>
		public string Original = string.Empty;
		/// <summary>"https", "http", "ssh" (ssh:// or the scp like user@host:path form), "file" (a local path or file://), "other".</summary>
		public string Scheme = "other";
		/// <summary>True for the scp like form user@host:path.</summary>
		public bool ScpLike = false;
		/// <summary>The user info as written (user, user:token), empty when none.</summary>
		public string UserInfo = string.Empty;
		/// <summary>The host as written: a real name or an ssh alias.</summary>
		public string HostAsWritten = string.Empty;
		/// <summary>The real host, the alias resolved.</summary>
		public string Host = string.Empty;
		/// <summary>The port, zero when default.</summary>
		public int Port = 0;
		/// <summary>The path after the host, without the leading slash and the .git suffix.</summary>
		public string PathText = string.Empty;
		/// <summary>The rule of the host.</summary>
		public HostRule Rule = BuiltInRules[GitService.Generic];
		/// <summary>The owner, from the rule.</summary>
		public string Owner = string.Empty;
		/// <summary>The repository name, from the rule.</summary>
		public string Repo = string.Empty;
		/// <summary>The service specific segments of the path ({project}, {...}), from the rule.</summary>
		public Dictionary<string, string> Extra = new Dictionary<string, string>();
		/// <summary>True when the URL names a remote repository (https or ssh) with an owner and a repository.</summary>
		public bool Remote = false;
		/// <summary>The name under which the host is compared.</summary>
		public string CanonicalHost = string.Empty;
	}

	/// <summary>
	/// Cache of the ssh aliases resolved through "ssh -G".
	/// </summary>
	private static readonly Dictionary<string, string> AliasCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The real host of an ssh alias, from the "hostname" line of "ssh -G alias"; the alias itself when
	/// ssh is not available or knows nothing about it.
	/// </summary>
	private static string ResolveAlias(string alias) {
		string? real;
		if (AliasCache.TryGetValue(alias, out real))
			return real;
		real = alias;
		try {
			Tool ssh = new Tool("ssh");
			ssh.Timeout = 10000;
			ToolResult r = ssh.CommandSync("ssh", new string[] { "-G", alias });
			if (r.ExitCode == 0) {
				foreach (string line in Lines(r)) {
					if (line.StartsWith("hostname ", StringComparison.OrdinalIgnoreCase)) {
						real = line.Substring(9).Trim();
						break;
					}
				}
			}
		} catch {
		}
		AliasCache[alias] = real;
		return real;
	}

	private static readonly Regex SchemeUrl = new Regex(@"^(?<scheme>[a-zA-Z][a-zA-Z0-9+.\-]*)://(?:(?<user>[^@/]+)@)?(?<host>[^/:]+)(?::(?<port>\d+))?(?<path>/.*)?$", RegexOptions.Compiled);
	private static readonly Regex ScpUrl = new Regex(@"^(?:(?<user>[^@/]+)@)?(?<host>[^:/\\]+):(?<path>[^/].*|/.*)?$", RegexOptions.Compiled);

	/// <summary>
	/// Reads a URL: scheme, user info, host (alias resolved), port, path, and owner and repository
	/// through the rule of the host. A local path or a file URL is a valid clone source but not a
	/// remote target of the inheritance (Remote false).
	/// </summary>
	private static GitUrl ParseUrl(string url) {
		GitUrl u = new GitUrl();
		u.Original = url ?? string.Empty;
		string text = (url ?? string.Empty).Trim();
		Match m = SchemeUrl.Match(text);
		if (m.Success) {
			u.Scheme = m.Groups["scheme"].Value.ToLowerInvariant();
			u.UserInfo = m.Groups["user"].Value;
			u.HostAsWritten = m.Groups["host"].Value;
			if (m.Groups["port"].Success)
				u.Port = int.Parse(m.Groups["port"].Value);
			u.PathText = m.Groups["path"].Value;
			if (u.Scheme == "file")
				return u;
			if (u.Scheme != "https" && u.Scheme != "http" && u.Scheme != "ssh" && u.Scheme != "git")
				return u;
		} else {
			m = ScpUrl.Match(text);
			// A Windows drive letter ("C:\x") is not a host
			if (m.Success && !(m.Groups["host"].Value.Length == 1 && (m.Groups["path"].Value.StartsWith("\\") || m.Groups["path"].Value.StartsWith("/")))) {
				u.Scheme = "ssh";
				u.ScpLike = true;
				u.UserInfo = m.Groups["user"].Value;
				u.HostAsWritten = m.Groups["host"].Value;
				u.PathText = m.Groups["path"].Value;
			} else {
				u.Scheme = "file";
				return u;
			}
		}
		// The real host: an ssh alias resolves through ssh; a name with a dot is taken as real
		u.Host = u.HostAsWritten;
		if (u.Scheme == "ssh" && !u.HostAsWritten.Contains('.'))
			u.Host = ResolveAlias(u.HostAsWritten);
		u.Rule = RuleOf(u.Host);
		u.CanonicalHost = CanonicalHost(u.Host, u.Rule);
		// The path: prefix removed, .git suffix and slashes trimmed, then the template of the scheme
		string path = u.PathText.TrimStart('/');
		if (!string.IsNullOrEmpty(u.Rule.PathPrefix)) {
			string prefix = u.Rule.PathPrefix.Trim('/') + "/";
			if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				path = path.Substring(prefix.Length);
		}
		path = path.TrimEnd('/');
		if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
			path = path.Substring(0, path.Length - 4);
		u.PathText = path;
		string[] segments = path.Split('/').Where(s => s.Length > 0).ToArray();
		if (segments.Length == 0)
			return u;
		string template = (u.Scheme == "ssh") ? u.Rule.SshPath : u.Rule.HttpsPath;
		// The legacy Azure DevOps https form names the organization as the subdomain
		if (u.Rule.Service == GitService.AzureDevOps && u.Scheme != "ssh" && u.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase)) {
			u.Owner = u.Host.Substring(0, u.Host.Length - ".visualstudio.com".Length);
			template = "{project}/_git/{repo}";
			u.CanonicalHost = "dev.azure.com";
		}
		Dictionary<string, string>? values = MatchTemplate(template, segments);
		if (values == null) {
			// The generic reading: the first segment is the owner, the last the repository
			u.Owner = u.Owner.Length > 0 ? u.Owner : segments[0];
			u.Repo = segments[segments.Length - 1];
			if (segments.Length > 2)
				u.Extra["..."] = string.Join("/", segments.Skip(1).Take(segments.Length - 2));
		} else {
			if (values.ContainsKey("owner"))
				u.Owner = values["owner"];
			u.Repo = values.ContainsKey("repo") ? values["repo"] : segments[segments.Length - 1];
			foreach (KeyValuePair<string, string> kv in values)
				if (kv.Key != "owner" && kv.Key != "repo")
					u.Extra[kv.Key] = kv.Value;
		}
		u.Remote = u.Owner.Length > 0 && u.Repo.Length > 0;
		return u;
	}

	/// <summary>
	/// Matches path segments against a template ("{owner}/{project}/_git/{repo}", "{owner}/{...}/{repo}"):
	/// literal segments must match, {...} takes any number of middle segments. Null when the shape differs.
	/// </summary>
	private static Dictionary<string, string>? MatchTemplate(string template, string[] segments) {
		string[] tokens = template.Split('/').Where(t => t.Length > 0).ToArray();
		int fixedTokens = tokens.Count(t => t != "{...}");
		bool variable = tokens.Contains("{...}");
		if (segments.Length < fixedTokens || (!variable && segments.Length != fixedTokens))
			return null;
		Dictionary<string, string> values = new Dictionary<string, string>();
		int middle = segments.Length - fixedTokens;
		int s = 0;
		foreach (string token in tokens) {
			if (token == "{...}") {
				values["..."] = string.Join("/", segments.Skip(s).Take(middle));
				s += middle;
			} else if (token.StartsWith("{") && token.EndsWith("}")) {
				values[token.Substring(1, token.Length - 2)] = segments[s++];
			} else {
				if (!string.Equals(token, segments[s], StringComparison.OrdinalIgnoreCase))
					return null;
				s++;
			}
		}
		return values;
	}

	/// <summary>
	/// Formats a template with the values of a URL; with ownerOnly, only up to and including the owner
	/// (the prefix the insteadOf rule and the authorization header are scoped to).
	/// </summary>
	private static string FormatTemplate(string template, GitUrl u, bool ownerOnly) {
		List<string> parts = new List<string>();
		foreach (string token in template.Split('/').Where(t => t.Length > 0)) {
			string value;
			if (token == "{owner}")
				value = u.Owner;
			else if (token == "{repo}")
				value = u.Repo;
			else if (token == "{...}")
				value = u.Extra.ContainsKey("...") ? u.Extra["..."] : string.Empty;
			else if (token.StartsWith("{") && token.EndsWith("}")) {
				string key = token.Substring(1, token.Length - 2);
				value = u.Extra.ContainsKey(key) ? u.Extra[key] : string.Empty;
			} else
				value = token;
			if (value.Length > 0)
				parts.Add(value);
			if (ownerOnly && token == "{owner}")
				break;
		}
		return string.Join("/", parts);
	}

	/// <summary>
	/// True when two URLs name the same repository (same canonical host, owner and repository; the
	/// scheme and the .git suffix do not count). Plain text comparison for local paths.
	/// </summary>
	private static bool SameTarget(string a, string b) {
		GitUrl ua = ParseUrl(a);
		GitUrl ub = ParseUrl(b);
		if (ua.Remote && ub.Remote)
			return ua.CanonicalHost == ub.CanonicalHost && string.Equals(ua.Owner, ub.Owner, StringComparison.OrdinalIgnoreCase) && string.Equals(ua.Repo, ub.Repo, StringComparison.OrdinalIgnoreCase);
		string ta = a.Trim().TrimEnd('/', '\\');
		string tb = b.Trim().TrimEnd('/', '\\');
		if (ta.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) ta = ta.Substring(0, ta.Length - 4);
		if (tb.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) tb = tb.Substring(0, tb.Length - 4);
		return string.Equals(System.IO.Path.GetFullPath(ta), System.IO.Path.GetFullPath(tb), StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// True when a URL prefix of Include or Exclude covers a target: same canonical host, same owner,
	/// and the same repository when the prefix names one.
	/// </summary>
	private static bool PrefixCovers(string prefix, GitUrl target) {
		GitUrl p = ParseUrl(prefix);
		if (p.Scheme == "file" || p.Scheme == "other" || p.Host.Length == 0)
			return false;
		if (p.CanonicalHost != target.CanonicalHost)
			return false;
		if (p.Owner.Length > 0 && !string.Equals(p.Owner, target.Owner, StringComparison.OrdinalIgnoreCase))
			return false;
		if (p.Repo.Length > 0 && !string.Equals(p.Repo, target.Repo, StringComparison.OrdinalIgnoreCase))
			return false;
		return true;
	}

	// ---------------------------------------------------------------- the profile of the source

	/// <summary>
	/// How a repository authenticates: the kind, the base to reuse, the entries to pass, the identity.
	/// </summary>
	private class Profile {
		public AuthKind Kind = AuthKind.None;
		/// <summary>The base shown to the user, secrets masked.</summary>
		public string Base = string.Empty;
		/// <summary>The repository the profile was read from.</summary>
		public string Source = string.Empty;
		/// <summary>The remote URL of the source, read.</summary>
		public GitUrl Url = new GitUrl();
		/// <summary>Ssh: "git@github-foo:" or "ssh://git@host:2222/", the part before the path.</summary>
		public string SshBase = string.Empty;
		/// <summary>Token: the user info of the source URL, as written.</summary>
		public string UserInfo = string.Empty;
		/// <summary>Header: the http.&lt;url&gt;.extraheader entries, as "key=value".</summary>
		public List<string> Headers = new List<string>();
		/// <summary>Helper: the credential.* entries, as "key=value".</summary>
		public List<string> Helpers = new List<string>();
		/// <summary>The local identity of the source, when set.</summary>
		public string Name = string.Empty;
		public string Email = string.Empty;
	}

	/// <summary>
	/// Cache of the profiles read, by repository folder.
	/// </summary>
	private static readonly Dictionary<string, Profile> ProfileCache = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Reads how a repository authenticates from its origin URL (or push URL) and its local configuration.
	/// </summary>
	private static Profile DetectProfile(string repoPath, bool push = false) {
		string key = System.IO.Path.GetFullPath(repoPath) + (push ? "|push" : string.Empty);
		Profile? cached;
		if (ProfileCache.TryGetValue(key, out cached))
			return cached;
		Profile p = new Profile();
		p.Source = repoPath;
		ProfileCache[key] = p;
		// Every local entry: key and value separated by a newline, entries by NUL
		Command c = new Command();
		c.Path = repoPath;
		c.Internal = true;
		c.Args.AddRange(new string[] { "config", "--local", "--list", "-z" });
		c.Output = GitOutput.Silent;
		ToolResult r = Run(c);
		if (!Ok(r))
			return p;
		string url = string.Empty;
		string pushUrl = string.Empty;
		foreach (string entry in Entries(r)) {
			int nl = entry.IndexOf('\n');
			string k = nl >= 0 ? entry.Substring(0, nl) : entry;
			string v = nl >= 0 ? entry.Substring(nl + 1) : string.Empty;
			string kl = k.ToLowerInvariant();
			if (kl == "remote.origin.url")
				url = v;
			else if (kl == "remote.origin.pushurl")
				pushUrl = v;
			else if (kl == "user.name")
				p.Name = v;
			else if (kl == "user.email")
				p.Email = v;
			else if (kl.StartsWith("credential."))
				p.Helpers.Add(k + "=" + v);
			else if (kl.StartsWith("http.") && kl.EndsWith(".extraheader"))
				p.Headers.Add(k + "=" + v);
		}
		if (push && pushUrl.Length > 0)
			url = pushUrl;
		p.Url = ParseUrl(url);
		if (p.Url.Scheme == "ssh") {
			p.Kind = AuthKind.Ssh;
			string user = p.Url.UserInfo.Length > 0 ? p.Url.UserInfo + "@" : string.Empty;
			if (p.Url.ScpLike)
				p.SshBase = user + p.Url.HostAsWritten + ":";
			else
				p.SshBase = "ssh://" + user + p.Url.HostAsWritten + (p.Url.Port > 0 ? ":" + p.Url.Port : string.Empty) + "/";
			p.Base = p.SshBase;
		} else if ((p.Url.Scheme == "https" || p.Url.Scheme == "http") && p.Url.UserInfo.Length > 0) {
			p.Kind = AuthKind.Token;
			p.UserInfo = p.Url.UserInfo;
			p.Base = p.Url.Scheme + "://***@" + p.Url.HostAsWritten + "/";
		} else if (p.Headers.Count > 0) {
			p.Kind = AuthKind.Header;
			p.Base = p.Url.Scheme + "://" + p.Url.HostAsWritten + "/ (header)";
		} else if (p.Helpers.Count > 0) {
			p.Kind = AuthKind.Helper;
			string helper = p.Helpers.FirstOrDefault(h => h.ToLowerInvariant().StartsWith("credential.helper=")) ?? p.Helpers[0];
			p.Base = helper;
		}
		return p;
	}

	/// <summary>
	/// The source repository of the inheritance: Inherit.Source, or the repository containing the
	/// running script. Null when there is none.
	/// </summary>
	private static string? SourceRepository() {
		string start = Inherit.Source ?? Folders.CurrentScriptFolder;
		if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
			return null;
		string? top = Query(start, "rev-parse", "--show-toplevel");
		return string.IsNullOrEmpty(top) ? null : top;
	}

	// ---------------------------------------------------------------- the plan of a call

	/// <summary>
	/// What a transfer verb does about the authentication of one target.
	/// </summary>
	private class Plan {
		/// <summary>True when the profile applies to the call.</summary>
		public bool Apply = false;
		/// <summary>The URL the call runs with: the target as given, or its ssh form.</summary>
		public string Url = string.Empty;
		/// <summary>The configuration entries of the call ("key=value").</summary>
		public List<string> Config = new List<string>();
		/// <summary>The profile applied, when any.</summary>
		public Profile? Profile = null;
		/// <summary>The target, read.</summary>
		public GitUrl? Target = null;
		/// <summary>True when the inheritance was asked explicitly and cannot be satisfied: the verb fails with Reason.</summary>
		public bool Failed = false;
		public string Reason = string.Empty;
		/// <summary>The insteadOf entry of an ssh profile ("url.<base><owner>/.insteadOf=https://<host>/<owner>/"), for the pin.</summary>
		public string InsteadOf = string.Empty;
	}

	/// <summary>
	/// Decides whether the profile of the source applies to a target and how: the scope, the probe
	/// and the application of the kind. Logs the decision at verbose level.
	/// </summary>
	/// <param name="targetUrl">The URL of the target.</param>
	/// <param name="option">The Inherit property of the call; null takes Inherit.Enabled.</param>
	/// <param name="probeAllowed">True for a clone: the anonymous probe runs when Inherit.Probe is set.</param>
	/// <param name="push">True to use the push profile of the source.</param>
	private static Plan PlanFor(string targetUrl, bool? option, bool probeAllowed, bool push = false) {
		Plan plan = new Plan();
		plan.Url = targetUrl;
		bool enabled = option ?? Inherit.Enabled;
		if (!enabled)
			return plan;
		string? source = SourceRepository();
		if (source == null) {
			if (option == true) {
				plan.Failed = true;
				plan.Reason = "the authentication cannot be inherited: no repository around the script" + (Inherit.Source != null ? " (Inherit.Source: " + Inherit.Source + ")" : string.Empty);
			} else
				Msg.Print("git: inherit: no source repository, plain call", Msg.LogLevels.Verbose);
			return plan;
		}
		Profile profile = DetectProfile(source, push);
		if (profile.Kind == AuthKind.None) {
			Msg.Print("git: inherit: the source authenticates through the global setup, nothing to inherit", Msg.LogLevels.Verbose);
			return plan;
		}
		GitUrl target = ParseUrl(targetUrl);
		plan.Target = target;
		if (!target.Remote) {
			Msg.Print("git: inherit: " + Mask(targetUrl) + " is not a remote repository, plain call", Msg.LogLevels.Verbose);
			return plan;
		}
		// The scope, in order: excluded, included, the host, the owner, the target's own authentication
		string why;
		if (Inherit.Exclude != null && Inherit.Exclude.Any(e => PrefixCovers(e, target)))
			why = "excluded";
		else if (Inherit.Include != null && Inherit.Include.Any(i => PrefixCovers(i, target)) && target.CanonicalHost == profile.Url.CanonicalHost)
			why = string.Empty;
		else if (target.CanonicalHost != profile.Url.CanonicalHost)
			why = "another host (" + target.CanonicalHost + ", the source is on " + profile.Url.CanonicalHost + ")";
		else if (Inherit.Scope == InheritScope.Owner && !string.Equals(target.Owner, profile.Url.Owner, StringComparison.OrdinalIgnoreCase))
			why = "another owner (" + target.Owner + ", the source is " + profile.Url.Owner + ")";
		else if (target.UserInfo.Length > 0)
			why = "the target carries its own user info";
		else if (target.Scheme == "ssh" && profile.Kind != AuthKind.Ssh)
			why = "the target is already an ssh URL";
		else if (target.Scheme == "ssh" && profile.Kind == AuthKind.Ssh && !string.Equals(target.HostAsWritten, profile.Url.HostAsWritten, StringComparison.OrdinalIgnoreCase))
			why = "the target is an ssh URL with its own host";
		else
			why = string.Empty;
		if (why.Length > 0) {
			Msg.Print("git: inherit: " + Mask(targetUrl) + " out of scope: " + why, Msg.LogLevels.Verbose);
			return plan;
		}
		// The probe: a target that answers without authentication is left plain
		if (probeAllowed && Inherit.Probe && Probe(targetUrl)) {
			Msg.Print("git: inherit: " + Mask(targetUrl) + " answers without authentication, plain call", Msg.LogLevels.Verbose);
			return plan;
		}
		// Apply
		plan.Profile = profile;
		plan.Apply = true;
		string httpsOwnerPrefix = "https://" + target.CanonicalHost + "/" + FormatTemplate(target.Rule.HttpsPath, target, true) + "/";
		if (target.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
			httpsOwnerPrefix = "https://" + target.Host + "/";
		switch (profile.Kind) {
			case AuthKind.Ssh: {
				string sshOwner = profile.SshBase + FormatTemplate(target.Rule.SshPath, target, true) + "/";
				if (target.Scheme != "ssh")
					plan.Url = profile.SshBase + FormatTemplate(target.Rule.SshPath, target, false) + (target.Original.TrimEnd('/').EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? ".git" : string.Empty);
				plan.InsteadOf = "url." + sshOwner + ".insteadOf=" + httpsOwnerPrefix;
				plan.Config.Add(plan.InsteadOf);
				break;
			}
			case AuthKind.Token: {
				string userInfo = Uri.UnescapeDataString(profile.UserInfo);
				if (!userInfo.Contains(':'))
					userInfo = userInfo + ":x-oauth-basic";
				string header = "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userInfo));
				plan.Config.Add("http." + httpsOwnerPrefix + ".extraheader=" + header);
				break;
			}
			case AuthKind.Header: {
				foreach (string h in profile.Headers) {
					// "http.https://github.com/.extraheader=AUTHORIZATION: basic ..." : the URL of the key must cover the target
					Match m = Regex.Match(h, @"^http\.(.+)\.extraheader=", RegexOptions.IgnoreCase);
					if (!m.Success)
						continue;
					GitUrl scoped = ParseUrl(m.Groups[1].Value);
					if (scoped.Host.Length == 0 || scoped.CanonicalHost == target.CanonicalHost)
						plan.Config.Add(h);
				}
				break;
			}
			case AuthKind.Helper:
				plan.Config.AddRange(profile.Helpers);
				break;
		}
		Msg.Print("git: inherit: " + Mask(targetUrl) + " gets the " + profile.Kind + " authentication of " + profile.Base + " from " + profile.Source, Msg.LogLevels.Verbose);
		return plan;
	}

	/// <summary>
	/// True when the target answers an anonymous ls-remote: public, or served by the global setup.
	/// </summary>
	private static bool Probe(string targetUrl) {
		Command c = new Command();
		c.Internal = true;
		c.Args.AddRange(new string[] { "ls-remote", "--exit-code", targetUrl, "HEAD" });
		c.Output = GitOutput.Silent;
		c.Timeout = Timeout > 0 ? Timeout : 60000;
		ToolResult r = Run(c);
		// 2: the server answered but has no matching reference (an empty repository), open all the same
		return Ok(r) || r.ExitCode == 2;
	}

	// ---------------------------------------------------------------- the pin

	/// <summary>
	/// Writes the non secret part of the applied profile into a cloned target: the insteadOf rule of an
	/// ssh profile (its origin is already ssh), the credential helper entries of a helper profile, the
	/// identity; and the marker naming the keys written and the source, so a refresh rewrites only
	/// those. Tokens and headers are never written.
	/// </summary>
	private static void Pin(string path, Plan plan) {
		if (plan.Profile == null)
			return;
		List<string> keys = new List<string>();
		List<string> entries = new List<string>();
		if (plan.Profile.Kind == AuthKind.Ssh && plan.InsteadOf.Length > 0)
			entries.Add(plan.InsteadOf);
		if (plan.Profile.Kind == AuthKind.Helper)
			entries.AddRange(plan.Profile.Helpers);
		if (Inherit.Identity) {
			if (plan.Profile.Name.Length > 0)
				entries.Add("user.name=" + plan.Profile.Name);
			if (plan.Profile.Email.Length > 0)
				entries.Add("user.email=" + plan.Profile.Email);
		}
		foreach (string entry in entries) {
			int eq = entry.IndexOf('=');
			if (eq <= 0)
				continue;
			string key = entry.Substring(0, eq);
			if (ConfigSet(path, key, entry.Substring(eq + 1)) && !keys.Contains(key))
				keys.Add(key);
		}
		if (keys.Count > 0) {
			ConfigSet(path, "kombine.inherited.keys", string.Join(",", keys));
			ConfigSet(path, "kombine.inherited.source", plan.Profile.Source);
			Msg.Print("git: inherit: pinned " + string.Join(", ", keys) + " into " + path, Msg.LogLevels.Verbose);
		}
	}

	/// <summary>
	/// Rewrites the pinned keys of a target when the marker says the extension wrote them, so a source
	/// that changed its account propagates; values the user set by hand are never touched.
	/// </summary>
	private static void Refresh(string path, Plan plan) {
		string? marked = ConfigGet(path, "kombine.inherited.keys");
		if (string.IsNullOrEmpty(marked))
			return;
		Pin(path, plan);
	}
}
