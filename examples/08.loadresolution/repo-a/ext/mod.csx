/*---------------------------------------------------------------------------------------------------------

	Kombine #load Resolution Example: subfolder script of repo-a

	(C)Kollective Networks 2026

	Subfolder script using a root-relative #load, with the embedded repo-b sitting in this
	same folder. It must bind repo-a's helper through the backward step; the retired forward
	search used to bind repo-b's copy from here.

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
