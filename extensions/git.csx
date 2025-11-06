/*---------------------------------------------------------------------------------------------------------

	Kombine Git Extension Example

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

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
	/// To get the Status of the repo
	/// Not ready yet. Not done yet.
	/// </summary>
	/// <param name="path"></param>
	public static void Status(string path) {
		// git rev-parse --git-dir
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
	public static bool Clone(string uri,string path,string? branch = null,bool showdettach = false) {
		KList args = "clone";
		if (branch != null) {
			args += "-b";
			args += branch;
		}
		if (!showdettach)
			args += "-c advice.detachedHead=false";
		args += "-q";
		args += "--progress";
		args += " --recurse-submodules";
		args += uri;
		args += path;
		Exec("git",args.Flatten(),true);
		return true;
	}

	public static bool Pull(string path) {
		Folders.SetCurrentFolder(path,true);
		Exec("git","pull  --recurse-submodules",true);
		Folders.CurrentFolderPop();
		return true;
	}

	/// <summary>
	/// Apply a patch file over a directory
	/// </summary>
	/// <param name="patchFile">Patch file to be applied</param>
	/// <param name="patchDir">Base directory to apply the patch</param>
	/// <returns>true if was applied or already applied, false otherwise</returns>
	public static bool Patch(string patchFile, string patchDir) {
		if (Files.Exists(patchFile) == false) {
			Msg.PrintWarning("Patch file does not exist");
			return false;
		}
		string patchText = Files.ReadTextFile(patchFile);
		// Convert the patch to Unix line endings. This is necessary to avoid
		// whitespace errors with git apply.
		patchText = patchText.Replace("\r\n","\n");
		// Git apply fails silently if not run relative to a respository root.
		// if not is_checkout(patch_dir):
		// 		sys.stdout.write('... patch directory is not a repository root.\n')
		// def is_checkout(path):
		// """ Returns true if the path represents a git checkout. """
		// return os.path.exists(os.path.join(path, '.git'))
		//
		//

		// git apply -p0 --ignore-whitespace
		// git apply -p0 --numstat
		// git apply -p0 --reverse --check
		// git apply -p0 --check
		// git apply -p0
		return true;
	}

	/// <summary>
	///  Returns the list of modified files for the given patsh
	///  If paths is empty, it will return the list of all modified files
	/// </summary>
	/// <param name="paths">List of paths to fetch the modified files or empty for everything</param>
	/// <param name="extensions">List of extensions to filter the files</param>
	/// <param name="staged">If true, it will return the staged files, otherwise the working tree files</param>
	/// <returns></returns>
	public static KList GetModifiedFiles(string[] paths,string[] extensions,bool staged = true) {
		KList list = new KList();
		List<string> filter1 = new List<string>();
		List<string> filter2 = new List<string>();

		Tool tool = new Tool("git");
		if (staged) {
			// Get list of modified files from the Staging area
			string[] gitStagingArgs = { "diff", "--cached", "--name-only", "--diff-filter=ACMR" };
			ToolResult resStaging = tool.CommandSync("git", gitStagingArgs, null);
			foreach (string file in resStaging.Stdout) {
				string filet = file.Trim();
				filter1.Add(filet);
			}
		} else {
			// Get list of modified files from the Working tree
			string[] gitWorkingArgs = { "diff", "--name-only", "--diff-filter=ACMR" };
			ToolResult resWorkingTree = tool.CommandSync("git", gitWorkingArgs, null);
			foreach (string file in resWorkingTree.Stdout) {
				string filet = file.Trim();
				filter1.Add(filet);
			}
		}
		//
		// Filter out
		//
		foreach (string file in filter1) {
			bool add = false;
			if (extensions.Length == 0) {
				add = true;
			} else {
				foreach (string ext in extensions) {
					if (file.EndsWith(ext,StringComparison.InvariantCultureIgnoreCase)) {
						add = true;
						break;
					}
				}
			}
			if (add) {
				filter2.Add(file);
			}
		}
		// Filter out the paths
		//
		foreach (string file in filter2) {
			bool add = false;
			if (paths.Length == 0) {
				add = true;
			} else {
				foreach (string path in paths) {
					if (file.StartsWith(path)) {
						add = true;
						break;
					}
				}
			}
			if (add) {
				list.Add(file);
			}
		}
		return list;
	}

	/// <summary>
	/// Add files to the staging area
	/// </summary>
	/// <param name="files">files to be added</param>
	public static void Add(KList files){
		foreach (KValue file in files) {
			var gitAdd = $"git add \"{file}\"";
			Shell("cmd", $"/C {gitAdd}");
		}
	}

	/// <summary>
	/// Installs the pre-commit hook in the .git/hooks directory.
	/// Ensures the hook file contains the required commands.
	/// You should specify in requiredLines the lines you want to ensure are in the hook.
	/// for example: { "#!/bin/sh", "mkb bla bla" };
	/// </summary>
	public static void InstallPreCommit(string[] requiredLines)
	{
		// Get the repository root directory
		string repoRoot = Directory.GetCurrentDirectory();
		string hooksDir = Path.Combine(repoRoot, ".git", "hooks");
		string preCommitFile = Path.Combine(hooksDir, "pre-commit");

		try
		{
			// Ensure the ".git/hooks" directory exists
			if (!Directory.Exists(hooksDir))
			{
				Msg.Print($"Creating hooks directory at {hooksDir}...");
				Directory.CreateDirectory(hooksDir);
			}
			// Check if the pre-commit file exists
			if (File.Exists(preCommitFile))
			{
				Msg.Print("Found existing pre-commit file. Validating content...");

				// Read the current file contents
				var lines = File.ReadAllLines(preCommitFile).ToList();

				// Ensure the first line is "#!/bin/sh"
				if (lines.Count == 0 || lines[0].Trim() != requiredLines[0])
				{
					Msg.Print("Adding missing shebang line...");
					lines.Insert(0, requiredLines[0]);
				}

				// Ensure the required lines are present
				if (!lines.Contains(requiredLines[1]))
				{
					Msg.Print("Adding missing required line...");
					lines.Add(requiredLines[1]);
				}

				// Write the validated/updated content back to the file
				File.WriteAllLines(preCommitFile, lines);
			}
			else
			{
				// Create a new pre-commit file with the required content
				Msg.Print($"Creating new pre-commit file at {preCommitFile}...");
				File.WriteAllLines(preCommitFile, requiredLines);
			}

			// Make the file executable (important for Git hooks on Unix-like systems)
			if (!Host.IsWindows())
			{
				Tool tool = new Tool("chmod");

				Msg.Print("Making pre-commit file executable...");
				// Get list of modified files from the Staging area
				string[] Args = { "+x", $"{preCommitFile}" };
				ToolResult res = tool.CommandSync("chmod", Args, null);
				if ( res.ExitCode != 0) {
					Msg.PrintWarning($"Failed to make {preCommitFile} executable.");
				}
			}

			Msg.Print("Pre-commit hook installed successfully.");
		}
		catch (Exception ex)
		{
			Msg.PrintAndAbort($"Error installing pre-commit hook: {ex.Message}");
		}
	}
}
