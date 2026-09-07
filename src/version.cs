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

			// Raw build number. The release script rewrites the bracketed BUILD
			// placeholder below with the numeric build. Local and debug
			// builds leave it untouched.
			private static readonly string rawBuild = "[BUILD]";

			// Build identifier. Returns the injected build number on release builds,
			// or "development" when the placeholder has not been substituted.
			public static string Build {
				get { return rawBuild.StartsWith('[') ? "development" : rawBuild; }
			}

			public static int HexVersion = 0x0104;
		}
	}
}
