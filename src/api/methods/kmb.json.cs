/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kltv.Kombine.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Encapsulates a JSON file and provides methods to read and write it.
	/// </summary>
	public class JsonFile {

		/// <summary>
		/// Internal filename with full path
		/// </summary>
		private KValue _file;
		
		/// <summary>
		/// Initializes an instance of JsonFile
		/// </summary>
		/// <param name="file">Filename to be used. May contain relative path or be absolute.</param>
		public JsonFile(KValue file) {
			_file = Path.GetFullPath(file);
			LoadAndParseDocument(_file);
		}

		/// <summary>
		/// Loads the file and parses it into a JsonNode
		/// </summary>
		/// <param name="filePath">Filename to be fetched</param>
		private void LoadAndParseDocument(string filePath) {
			if (Files.Exists(filePath) == false) {
				Msg.PrintWarningMod("JSON file not found: " + filePath, ".json", Msg.LogLevels.Verbose);
				Msg.PrintWarningMod("A new document will be created.", ".json", Msg.LogLevels.Verbose);
				Doc = null;
				return;
			}
			KValue json = Files.ReadTextFile(filePath,false);
			var options = new JsonSerializerOptions {
				PropertyNameCaseInsensitive = true
				// TODO: We may want to add a public option to tweak this
			};
			try{
				JsonNode? _root  = JsonSerializer.Deserialize<JsonNode>(json, options);
				if (_root != null){
					Doc = _root;
				}
			} catch(Exception ex){
				Msg.PrintWarningMod("Failed to parse JSON file: "+ex.Message,".json",Msg.LogLevels.Verbose);
				Msg.PrintWarningMod("A new document will be created.",".json",Msg.LogLevels.Verbose);
			}
		}

		/// <summary>
		/// Saves the current document to the file
		/// </summary>
		/// <returns>True if everything okey, false otherwise.</returns>
		public bool Save() {
			if (Doc != null) {
				// Create the folder for the file to be stored
				string? folder = Path.GetDirectoryName(_file);
				if (folder != null)
					Folders.Create(folder);
				JsonSerializerOptions options = new JsonSerializerOptions {
					WriteIndented = true // Enable formatting for better readability
					// TODO: We may want to add a public option to tweak this 
				};
				string json = JsonSerializer.Serialize<JsonNode>(Doc, options);
				if (Files.WriteTextFile(_file, json) == false){
					Msg.PrintErrorMod("Failed to save JSON file: " + _file, ".json", Msg.LogLevels.Verbose);
					return false;
				}
				Msg.PrintMod("JSON file saved: " + _file, ".json", Msg.LogLevels.Verbose);
				return true;
			}
			Msg.PrintWarningMod("No document to save.",".json",Msg.LogLevels.Verbose);
			return false;
		}

		/// <summary>
		/// Holds the root JsonNode
		/// </summary>
		public JsonNode? Doc {get; set;} = null;
	}

	/// <summary>
	/// JSON extensions
	/// </summary>
	public static class JsonExtensions {

		/// <summary>
		/// Return the node inside the array that matches one entry with the name and value.
		/// </summary>
		/// <param name="i">this array parameter</param>
		/// <param name="name">name of the property the array entry should have</param>
		/// <param name="value">value of the property the array entry should have</param>
		/// <returns>The Jsonnode which contains the given property and value. Null otherwise</returns>
		public static JsonNode? Find(this JsonArray i,string name,object value) {
			foreach (JsonNode? item in i) {
					if (item == null)
						continue;
					if (item is JsonObject){
						JsonObject o = item.AsObject();
						if (o.ContainsKey(name)) {
							 JsonValue? n = o[name]?.AsValue();
							 // Compare the value using the string representation
							 if ( (n != null) && ( n.ToString() == value.ToString())){
								 return item;
							 }
						}
					}
				}			
			return null;
		}


	}
}
