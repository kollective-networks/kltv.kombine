/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Kltv.Kombine {
	internal static partial class KombineMain {

		internal static class Version{

			public static string Major = "1";

			public static string Minor = "4";

			public static string Build = "[BUILD]";

			public static int HexVersion = 0x0104;
		}
	}
}
