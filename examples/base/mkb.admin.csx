/*---------------------------------------------------------------------------------------------------------

	Kombine Base Admin rights check Example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int test(string[] args)
{
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing version functions");
	Msg.BeginIndent();

	// To check if we've admin rights
	if (!Host.IsRoot())
	{
		Msg.Print("[!] This script is running with no root privileges.");
	}
	else
	{
		Msg.Print("[!] This script is running with root privileges.");
	}
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return 0;
}
