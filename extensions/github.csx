/*---------------------------------------------------------------------------------------------------------

	Kombine Github Extension

	Implements some helper functions to interact with github repositories, such as publish releases

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Helper class to interact with GitHub repositories. It provides functions to create releases, upload assets, and check if a release already exists.
/// </summary>
public class Github {

	/// <summary>
	/// Constructor to initialize the repository, owner, and token. These values can be set at the beginning of the script and then used in all functions.
	/// </summary>
	public Github() {
		this.Repository = "";
		this.Owner = "";
		this.Token = "";
	}

	/// <summary>
	/// Holds the repository name, for example "kltv.kombine". This is used to avoid passing the repository name in every function call. It can 
	/// be set at the beginning of the script, and then all the functions will use this repository by default. 
	/// </summary>
	public string Repository { get; set; }

	/// <summary>
	/// Holds the owner name, for example "kollective-networks". This is used to avoid passing the owner name in every function call. 
	/// </summary>
	public string Owner { get; set; }

	/// <summary>
	/// Holds the GitHub token, which is required to authenticate with the GitHub API. 
	/// This token should have the necessary permissions to create releases and upload assets. 
	/// It can be set at the beginning of the script, and then all the functions will use this token by default.
	/// </summary>
	public string Token { get; set; }


	/// <summary>
	/// Returns the release ID for a given tag if the release already exists on GitHub. 
	/// If the release does not exist, it returns an empty string.
	/// </summary>
	/// <param name="tag">Tag to query for</param>
	/// <returns>The release id or empty if failed or does not exists</returns>
	public string GetReleaseID(string tag) {
		//
		// Check if the release already exists on github, if it does, abort
		string apiUrl = "https://api.github.com/repos/" + this.Owner + "/" + this.Repository + "/releases/tags/" + tag;
		Dictionary<string, string> headers = new Dictionary<string, string> {
			{"User-Agent", "KombineBuildEngine/" + GetVersion()}
		};
		if (!string.IsNullOrEmpty(this.Token)) {
			headers.Add("Authorization", "token " + this.Token);
		}
		string response = Http.GetDocument(apiUrl, headers);
		//
		// If the release exists, the API will return a JSON document with the release information, including the ID
		// If the release does not exist, the API will return a 404 error (in this case, the response will be empty)
		if (response != "") {
			// Parse the JSON response to extract the release ID
			var json = System.Text.Json.JsonDocument.Parse(response);
			string releaseId = json.RootElement.GetProperty("id").GetInt32().ToString();
			Msg.Print("Release " + tag + " already exists on GitHub with ID: " + releaseId);
			return releaseId;
		}
		Msg.Print("Release " + tag + " does not exist on GitHub yet.");
		return "";
	}

	/// <summary>
	/// Creates a new release on GitHub for the specified tag. If the release already exists, it returns the existing release ID.
	/// The release is created as a draft by default, and the notes are used as the release body.
	/// </summary>
	/// <param name="tag">The tag name for the release (e.g., "v1.0.0").</param>
	/// <param name="name">The name of the release.</param>
	/// <param name="notes">The body or description of the release, which can include markdown.</param>
	/// <param name="draft">Whether to create the release as a draft (default is true).</param>
	/// <returns>The release ID if successful, or an empty string if failed.</returns>
	public string CreateRelease(string tag, string name, string notes, bool draft = true) {
		//
		// Check if the release already exists
		string existingId = GetReleaseID(tag);
		if (existingId != "") {
			return existingId;
		}
		//
		// Create the release on GitHub
		string apiUrl = "https://api.github.com/repos/" + this.Owner + "/" + this.Repository + "/releases";
		var releaseData = new { tag_name = tag, name = name, body = notes, draft = draft };
		string json = JsonSerializer.Serialize(releaseData);
		Dictionary<string, string> headers = new Dictionary<string, string> {
			{"Accept","application/vnd.github+json" },
			{"Authorization", "Bearer " + this.Token},
			{"X-GitHub-Api-Version", "2022-11-28" },
			{"User-Agent", "KombineBuildEngine/" + GetVersion()   }
		};
		bool success = Http.PostDocument(apiUrl, json, headers);
		if (!success) {
			Msg.PrintError($"Failed to create release {tag} on GitHub.");
			return "";
		}
		// Check the status code to ensure the release was created successfully (201 Created)
		if (Http.LastReturnCode != 201) {
			Msg.PrintError($"Failed to create release {tag} on GitHub. Status code: " + Http.LastReturnCode);
			return "";
		}
		//
		// Parse the response from Http.LastResponse to get the release ID
		try {
			var jsonResponse = JsonDocument.Parse(Http.LastResponse);
			string releaseId = jsonResponse.RootElement.GetProperty("id").GetInt32().ToString();
			Msg.Print($"Release {tag} created successfully with ID: " + releaseId);
			return releaseId;
		} catch (Exception ex) {
			Msg.PrintError("Failed to parse release ID from response: " + ex.Message);
			return "";
		}
	}

	/// <summary>
	/// Uploads an array of files as assets to the specified GitHub release.
	/// </summary>
	/// <param name="releaseId">The ID of the release to upload assets to.</param>
	/// <param name="filePaths">Array of file paths to upload as assets.</param>
	/// <returns>True if all files were uploaded successfully, false otherwise.</returns>
	public bool UploadAssets(string releaseId, string[] filePaths) {
		foreach (string filePath in filePaths) {
			if (!System.IO.File.Exists(filePath)) {
				Msg.PrintError("File does not exist: " + filePath);
				return false;
			}
			string fileName = System.IO.Path.GetFileName(filePath);
			string uploadUrl = "https://uploads.github.com/repos/" + this.Owner + "/" + this.Repository + "/releases/" + releaseId + "/assets?name=" + Uri.EscapeDataString(fileName);
			Dictionary<string, string> headers = new Dictionary<string, string> {
				{"Authorization", "Bearer " + this.Token},
				{"Content-Type", "application/octet-stream"},
				{"User-Agent", "KombineBuildEngine/" + GetVersion()   }
			};
			bool success = Http.PostFile(uploadUrl, filePath, headers);
			if (!success || Http.LastReturnCode != 201) {
				Msg.PrintError("Failed to upload asset: " + fileName + ". Status code: " + Http.LastReturnCode);
				return false;
			}
			Msg.Print("Asset uploaded successfully: " + fileName);
		}
		return true;
	}

	/// <summary>
	/// Publishes a draft release by setting it to non-draft and making it the latest release.
	/// </summary>
	/// <param name="releaseId">The ID of the release to publish.</param>
	/// <returns>True if the release was published successfully, false otherwise.</returns>
	public bool PublishRelease(string releaseId) {
		//
		// Update the release to publish it
		string apiUrl = "https://api.github.com/repos/" + this.Owner + "/" + this.Repository + "/releases/" + releaseId;
		var updateData = new { draft = false, make_latest = "true" };
		string json = JsonSerializer.Serialize(updateData);
		Dictionary<string, string> headers = new Dictionary<string, string> {
			{"Accept","application/vnd.github+json" },
			{"Authorization", "Bearer " + this.Token},
			{"X-GitHub-Api-Version", "2022-11-28" },
			{"User-Agent", "KombineBuildEngine/" + GetVersion()   }
		};
		bool success = Http.PostDocument(apiUrl, json, headers, usePatch: true);
		if (!success) {
			Msg.PrintError("Failed to publish release " + releaseId + " on GitHub.");
			return false;
		}
		// Check the status code to ensure the release was updated successfully (200 OK)
		if (Http.LastReturnCode != 200) {
			Msg.PrintError("Failed to publish release " + releaseId + " on GitHub. Status code: " + Http.LastReturnCode);
			return false;
		}
		Msg.Print("Release " + releaseId + " published successfully.");
		return true;
	}

	/// <summary>
	/// Gets the major.minor version from Kombine statics
	/// </summary>
	/// <returns>The version string</returns>
	private string GetVersion() {
		return MkbVersionShort();
	}
}