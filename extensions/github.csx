/*---------------------------------------------------------------------------------------------------------

	Kombine Github Extension

	Implements some helper functions to interact with github repositories, such as publish releases

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;
using System.Collections.Generic;

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

	public string GetReleaseID(string tag) {
		//
		// Check if the release already exists on github, if it does, abort
		string apiUrl = "https://api.github.com/repos/" + this.Owner + "/" + this.Repository + "/releases/tags/" + tag;
		Dictionary<string, string> headers = new Dictionary<string, string>();
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
		string json = "{\"tag_name\":\"" + tag + "\",\"name\":\"" + name + "\",\"body\":\"" + notes.Replace("\"", "\\\"").Replace("\n", "\\n") + "\",\"draft\":" + draft.ToString().ToLower() + "}";
		Dictionary<string, string> headers = new Dictionary<string, string> {
			{"Accept","application/vnd.github+json" },
			{"Authorization", "Bearer " + this.Token},
			{"X-GitHub-Api-Version", "2022-11-28" },
			{"User-Agent", "KombineBuildEngine"   }
		};
		bool success = Http.PostDocument(apiUrl, json, headers);
		if (!success) {
			Msg.PrintError("Failed to create release on GitHub.");
			return "";
		}
		//
		// Get the release ID from the response (assuming PostDocument returns the response, but wait, it returns bool)
		// Actually, Http.PostDocument returns bool, not the response. We need to modify or use a different approach.
		// For now, after creation, call GetReleaseID to get the ID.
		string releaseId = GetReleaseID(tag);
		if (releaseId == "") {
			Msg.PrintError("Failed to retrieve release ID after creation.");
			return "";
		}
		Msg.Print("Release created successfully with ID: " + releaseId);
		return releaseId;
	}

}