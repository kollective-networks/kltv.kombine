/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Kltv.Kombine {
	internal static partial class KombineMain {

		internal static class Version{
		
			public static string Major = "1";

			public static string Minor = "0";

			public static string Build = "[BUILD]";
		}

		public static string GetVersionBuildNumber() {
			DateTime currentTime = DateTime.UtcNow;
			long now = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();			
			DateTime currentYear = new DateTime(DateTime.Now.Year, 1, 1);
			long year = ((DateTimeOffset)currentYear).ToUnixTimeSeconds();
			long bn = now - year;
			string buildNumber = "24" + (bn / 60).ToString("D6");
			return buildNumber;
		}
	}
}
