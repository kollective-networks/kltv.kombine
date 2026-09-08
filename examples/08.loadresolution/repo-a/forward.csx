/*---------------------------------------------------------------------------------------------------------

	Kombine #load Resolution Example: reference only the forward search can satisfy

	(C)Kollective Networks 2026

	The helper is at scripts/forward/helper.forward.csx but is referenced as "forward/helper.forward.csx",
	so the deterministic resolution fails and the script cannot be compiled, unless the forward search
	is enabled (-kforward or Engine.ForwardSearch), in which case it binds.

---------------------------------------------------------------------------------------------------------*/
#load "forward/helper.forward.csx"

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;

/// <summary>
/// Registers the owner of the helper bound by #load in the run wide registry, under the key
/// given as action parameter, so the parent script can verify which copy was bound.
/// </summary>
/// <param name="args">The registry key (the parent uses the script path and the mode).</param>
/// <returns>0 on success, 1 if the key is missing.</returns>
int check(string[] args){
	if (args.Length < 1) {
		Msg.PrintError("Expected the registry key as the action parameter.");
		return 1;
	}
	Share.Register("loadresolution", args[0], ForwardOwner);
	return 0;
}
