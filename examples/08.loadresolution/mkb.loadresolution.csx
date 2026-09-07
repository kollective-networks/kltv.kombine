/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

	#load resolution regression test.

	Layout: two repos share the same relative helper layout (scripts/build/helper.csx) and
	repo-b is embedded inside repo-a's dependency folder (repo-a/ext/repo-b), like a build
	that clones a library repo below itself:

		repo-a/kombine.csx                        #load "scripts/build/helper.csx"
		repo-a/scripts/build/helper.csx           owner: repo-a
		repo-a/ext/mod.csx                        #load "scripts/build/helper.csx"
		repo-a/ext/repo-b/kombine.csx             #load "scripts/build/helper.csx"
		repo-a/ext/repo-b/scripts/build/helper.csx owner: repo-b
		repo-a/ext/repo-b/ext/mod.csx             #load "scripts/build/helper.csx"

	Every script asserts it bound its OWN repo's helper. The retired recursive forward search
	used to make repo-a/ext/mod.csx bind repo-b's helper (first match walking the subfolders
	of repo-a/ext), and the state cache then persisted the wrong bind.

	Run with: mkb -ksrb -kfile:mkb.loadresolution.csx test
	(the rebuild flag forces real compiles, resolution happens there)

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;


int test(string[] args){
	Msg.Print("Testing #load resolution: each script must bind its own repo's helper.");
	// Root scripts resolve through the script folder step
	Kombine("repo-a/kombine.csx", "check", new string[]{"repo-a"});
	Kombine("repo-a/ext/repo-b/kombine.csx", "check", new string[]{"repo-b"});
	// Subfolder scripts with root-relative loads resolve through the backward step to the
	// nearest enclosing root. The first one is the regression: the forward search bound
	// repo-b's helper from here.
	Kombine("repo-a/ext/mod.csx", "check", new string[]{"repo-a"});
	Kombine("repo-a/ext/repo-b/ext/mod.csx", "check", new string[]{"repo-b"});
	Msg.Print("PASS: all scripts bound their own repo's helper.");
	return 0;
}
