/*---------------------------------------------------------------------------------------------------------

	Kombine Modder Extension Example

	Implements a simple modifier that can be used to apply modifications over the downloaded sources.

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

/// <summary>
/// It provides functionality to modify source files and verify if the mod can be applied
/// 
/// It requires three folders
/// a) The folder where we downloaded the project to be modified
/// b) The folder with the original files before being modified
/// c) The folder with the modified files.
/// 
/// We use the original files folder to check before mod if the mod can be applied
/// just to address if the project has been updated and our modified file is no longer valid
/// it will trigger one error pointing the file to be reviewed.
/// 
/// The folder structure is followed so everything must be placed in the right subfolder
/// 
/// </summary>
public class Modder {

	/// <summary>
	/// Project folder to be modified
	/// </summary>
	public string Prj { get; set; } = "";

	/// <summary>
	/// Original files folder to check for discrepancies
	/// </summary>
	public string Org { get; set; } = "";

	/// <summary>
	/// Modified files folder to be applied into the project
	/// </summary>
	public string Mod { get; set; } = "";


	/// <summary>
	/// It verifies if the mod can be applied. 
	/// It should be called before mod is applied.
	/// </summary>
	/// <returns>true if the mod can be applied, false otherwise</returns>
	public bool VerifyMod() {
		Msg.Print("Verify Mod Sources in progress.");
		Msg.BeginIndent();
		Msg.Print("Getting files on: " + RealPath(Org));
		KList org = new KList();
		
		// Fetch the files in the original folder
		org += Glob(Org,"**/*.*");
		// Remove the placeholder file
		org -= "readme.md";
		Msg.Print("Verifying files against: " + RealPath(Prj));
		Msg.BeginIndent();
		bool bFailed = false;
		foreach (KValue file in org) {
			Msg.PrintTask("Checking file: " + file.ToString());
			// Check if our original file still exists in the project we downloaded
			string prjF = Prj + file.ToString();
			string orgF = Org + file.ToString();
			if (Files.Exists(prjF) == false) {
				Msg.PrintTaskError("Failed. File no longer exists in the project");
				bFailed = true;
				continue;
			}
			if (FileCompare(prjF,orgF) == false) {
				Msg.PrintTaskError("Failed. File does not match the original");
				bFailed = true;
				continue;
			}
			Msg.PrintTaskSuccess(" Passed");
		}
		Msg.EndIndent();
		if (bFailed == true) {
			Msg.PrintAndAbort("Verification Failed. Modifications cannot be applied. Review it manually and fix the mods.");
		}
		Msg.EndIndent();
		return true;
	}

	/// <summary>
	/// Applies the mod over the sources.
	/// Copies everything on the mod folder over the sources
	/// If failed, sources may require to be downloaded again
	/// </summary>
	/// <returns>true if everything fine, false otherwise</returns>
	public bool ApplyMod() {
		Msg.Print("Applying Mod Sources in progress.");
		Msg.BeginIndent();
		Msg.Print("Getting files on: " + RealPath(Org));
		KList mod = new KList();

		// Fetch the files in the modded folder
		mod += Glob(Mod, "**/*.*");
		// Remove the placeholder file
		mod -= "readme.md";
		Msg.Print("Applying files on: " + RealPath(Prj));
		Msg.BeginIndent();
		bool bFailed = false;
		foreach (KValue file in mod) {
			Msg.PrintTask("Applying file: " + file.ToString());
			// Check if our original file still exists in the project we downloaded
			string prjF = Prj + file.ToString();
			string modF = Mod + file.ToString();
			// If the file exists in the project, first delete it since copy has no overwrite
			if (Files.Exists(prjF) == true) {
				if (Files.Delete(prjF) == false) {
					Msg.PrintTaskError(" Failed deleting project file: " + file.ToString());
					bFailed = true;
					continue;
				}
			}
			// Ensure folder exists since it may be a new one.
			string? fstruc = Path.GetDirectoryName(prjF);
			if ( fstruc != null) {
				Folders.Create(fstruc);
			}
			// And copy the file
			if (Files.Copy(modF, prjF, true) == false) {
				Msg.PrintTaskError(" Failed copying file: " + file.ToString());
				bFailed = true;
				continue;
			}
			Msg.PrintTaskSuccess(" Passed");
		}
		Msg.EndIndent();
		if (bFailed == true) {
			Msg.PrintAndAbort("Failed applying modifications. Project sources may be in an inconsistent state.");
		}
		Msg.EndIndent();
		return true;
	}

	/// <summary>
	/// Removes the mod.
	/// It copies all the original files and also delete the ones missing on the original
	/// but added as part of the mod.
	/// If failed, sources may be in an inconsistent state
	/// Created folders that does not exists in the project will remain but empty
	/// </summary>
	/// <returns>True if everything fine, false otherwise</returns>
	public bool RemoveMod() {
		Msg.Print("Removing Mod Sources in progress.");
		Msg.BeginIndent();
		Msg.Print("Getting files on: " + RealPath(Org));
		KList mod = new KList();

		// Fetch the files in the modded folder
		mod += Glob(Mod, "**/*.*");
		// Remove the placeholder file
		mod -= "readme.md";
		Msg.Print("Removing files on: " + RealPath(Prj));
		Msg.BeginIndent();
		bool bFailed = false;
		foreach (KValue file in mod) {
			Msg.PrintTask("Removing on file: " + file.ToString());
			// Check if our original file still exists in the project we downloaded
			string prjF = Prj + file.ToString();
			string modF = Mod + file.ToString();
			string orgF = Org + file.ToString();
			if (Files.Exists(orgF) == false) {
				// If the mod file was not present in the original one, just delete it.
				if (Files.Delete(prjF) == true) {
					Msg.PrintTaskSuccess(" Removed");
				} else {
					Msg.PrintTaskError("Failed to remove: " + file.ToString());
					bFailed = true;
				}
				// And since it could be a custom created folder, try to remove it
				string? fstruc = Path.GetDirectoryName(prjF);
				if (fstruc != null) {
					try {
						Directory.Delete(fstruc,false);
					} catch(Exception ex) {
						if (ex is IOException) {
							// Directory is not empty. No problem.
						}
					}
				}
				continue;
			}
			// Mod file exists in the original, so, replace it with the original on the sources.
			if (Files.Delete(prjF) == false) {
				Msg.PrintTaskError(" Failed deleting project file: " + file.ToString());
				bFailed = true;
				continue;
			}
			if (Files.Copy(orgF, prjF, true) == false) {
				Msg.PrintTaskError(" Failed copying file: " + file.ToString());
				bFailed = true;
				continue;
			}
			Msg.PrintTaskSuccess(" Passed");
		}
		Msg.EndIndent();
		if (bFailed == true) {
			Msg.PrintAndAbort("Failed removing modifications. Project sources may be in an inconsistent state.");
		}
		Msg.EndIndent();
		return true;
	}


	// It compares two files and return true if they're the same
	// spaces and tabs are converted to a unique space
	// lf/cr is converted as well
	private bool FileCompare(string file1, string file2) {

		// Compare if both files are the same
		string n1 = Files.ReadTextFile(file1);
		string n2 = Files.ReadTextFile(file2);
		// Remove all formatting characters
		n1 = n1.Replace(" ", "");
		n1 = n1.Replace("\t", "");
		n1 = n1.Replace("\r", "");
		n1 = n1.Replace("\n", "");
		n2 = n2.Replace(" ", "");
		n2 = n2.Replace("\t", "");
		n2 = n2.Replace("\r", "");
		n2 = n2.Replace("\n", "");
		// Now just compare the strings
		// It will trigger different even for comments but is not a problem
		if (n1 != n2) {
			return false;
		}
		return true;
	}


}