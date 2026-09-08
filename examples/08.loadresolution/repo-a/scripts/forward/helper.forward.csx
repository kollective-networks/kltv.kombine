/*---------------------------------------------------------------------------------------------------------

	Kombine #load Resolution Example: helper only reachable through the forward search

	(C)Kollective Networks 2026

	This helper lives in scripts/forward/, but repo-a/forward.csx loads it as "forward/helper.forward.csx":
	no step of the deterministic resolution finds it, only the forward search of the subfolders does.

---------------------------------------------------------------------------------------------------------*/

string ForwardOwner = "forward";
