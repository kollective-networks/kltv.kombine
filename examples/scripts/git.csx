/*---------------------------------------------------------------------------------------------------------

	Kombine Git Extension Example

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.Text.RegularExpressions;


public static class Git {

	public struct GitVersion {
		public int Major;
		public int Minor;
		public int Revision;
		public GitVersion(){
			Major = -1;
			Minor = -1;
			Revision = -1;
		}
	}

	private static GitVersion version;

	//
	// Just checks for the git version and return true / false
	//
	public static bool VersionCheck(int Major, int Minor, int Revision) {

		GitVersion v = Version();
		if (v.Major > Major)
			return true;
		if (v.Major < Major)
			return false;
		if (v.Minor > Minor)
			return true;
		if (v.Minor < Minor)
			return false;
		if (v.Revision < Revision)
			return false;
		return true;
	}


	//
	// Launches git and obtain+parse version
	//
	public static GitVersion Version() {

		Tool tool = new Tool("git");
		ToolResult res = tool.CommandSync("git","--version");
		if (res.ExitCode != 0){
			Msg.PrintWarning("Error launching Git");
			return version;
		}
		if (res.Stdout.Length > 0) {
			Regex pattern = new Regex("\\d+(\\.\\d+)+");
			Match m = pattern.Match(res.Stdout[0]);
			string value = m.Value;
			string[] parts = value.Split(".");
			version.Major = int.Parse(parts[0]);
			version.Minor = int.Parse(parts[1]);
			version.Revision = int.Parse(parts[2]);
		}
		return version;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="path"></param>
	public static void Status(string path) {
		Folders.SetCurrentFolder(path,true);
		Tool tool = new Tool("git");
		ToolResult res = tool.CommandSync("git","status");
		// Add your checks here
		Folders.CurrentFolderPop();
	}

	/// <summary>
	/// Clone a repository
	/// Credentials will be taken depending on your Git configuration
	/// </summary>
	/// <param name="uri">Uri for the repository</param>
	/// <param name="path">Path to drop the files</param>
	/// <param name="branch">Branch name to clone if required(optional).</param>
	/// <returns></returns>
	public static bool Clone(string uri,string path,string? branch = null) {
		KList args = "clone";
		if (branch != null) {
			args += "-b";
			args += branch;
		}
		args += "-q";
		args += "--progress";
		args += " --recurse-submodules";
		args += uri;
		args += path;
		Exec("git",args.Flatten(),true);
		return true;
	}
}
