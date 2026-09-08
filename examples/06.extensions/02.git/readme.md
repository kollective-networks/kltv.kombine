# git extension example

`mkb.ext.git.csx` tests the git extension (`extensions/git.csx`) by groups, so a group whose
environment cannot be satisfied is skipped with a visible line and the rest still runs:

| Group | Needs |
| --- | --- |
| 1 Version and failures | git |
| 2 to 6 Repositories, changes and patches, commits and publishing, worktrees and bundles, hooks | fixture repositories built in the `.tmp.git` sandbox, no network except one clone from GitHub with the progress bar, skipped offline |
| 7 Large files | the `git lfs` command |
| 8 Authentication inheritance, decisions | nothing: the hosts live under the reserved `.invalid` domain, every clone attempt fails at once and the decision taken before git runs is what is checked |
| 9 Authentication inheritance, private repository | `kltv_token`, a GitHub token for `kollective-networks/kltv.kombine.test.repo` (the source that authenticates is made with it); or, for any other setup, `KOMBINE_GIT_TEST_SOURCE` (folder of a clone that authenticates) and `KOMBINE_GIT_TEST_PRIVATE` (URL of a private repository of the same owner), `KOMBINE_GIT_TEST_PUBLIC` and `KOMBINE_GIT_TEST_OTHER` optional |

```
mkb test     runs every group
mkb clean    removes the sandbox
```

The example is run by the `extensions` action of `examples/kombine.csx`.
