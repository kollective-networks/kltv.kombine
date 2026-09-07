/*---------------------------------------------------------------------------------------------------------

	Kombine #load Resolution Example: root script of the embedded repo-b

	(C)Kollective Networks 2026

	Root script of the repo embedded inside repo-a, using a root-relative #load. It must bind
	repo-b's helper through the script folder step.

---------------------------------------------------------------------------------------------------------*/
#load "scripts/build/helper.csx"

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;

/// <summary>
/// Registers the owner of the helper bound by #load in the run wide registry, under the key
/// given as action parameter, so the parent script can verify which copy was bound.
/// </summary>
/// <param name="args">The registry key (the parent uses the script path).</param>
/// <returns>0 on success, 1 if the key is missing.</returns>
int check(string[] args){
	if (args.Length < 1) {
		Msg.PrintError("Expected the registry key as the action parameter.");
		return 1;
	}
	Share.Register("loadresolution", args[0], HelperOwner);
	return 0;
}
