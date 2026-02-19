/*---------------------------------------------------------------------------------------------------------

	Kombine Yaml Parser Extension

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/


using System.Linq;
using System.Collections.Generic;

namespace Kltv.Kombine.Api
{
	public class Yaml
	{
		//
		// YamlObject
		// Key is always string
		// Value can be a string or a list of YamlObject
		//
		public class YamlObject
		{
			public YamlObject()
			{
				Key = String.Empty;
				Value = null;
			}
			public YamlObject(string key, string value)
			{
				Key = SanitizeKey(key);
				Value = SanitizeValue(value);
			}
			public YamlObject(string key)
			{
				Key = SanitizeKey(key);
				Value = null;
			}

			//
			// Holds the key
			//
			public string Key;
			//
			// Holds the value
			//
			public object? Value;

			//
			// Returns true if the value is just a simple string
			//
			public bool IsString()
			{
				return Value is string;
			}

			//
			// Returns the value is a list of objects
			//
			public bool IsList()
			{
				return Value is List<YamlObject>;
			}
		}

		//
		// Loads and parses a Yaml File
		//
		static public YamlObject? LoadFile(string filename)
		{
			if (File.Exists(filename) == false)
			{
				return null;
			}
			string[] lines = File.ReadAllLines(filename);
			YamlObject obj = new YamlObject("document");
			obj.Value = new List<YamlObject>();
			for (int i = 0; i < lines.Length;)
			{
				string line = lines[i];
				if (line.Length == 0)
				{
					i++;
					continue;
				}
				if (line[0] == '#')
				{
					i++;
					continue;
				}
				if (line.StartsWith("---"))
				{
					YamlObject entry = new YamlObject("object");
					i = parseObject(ref entry, lines, i + 1, lines.Length, 0);
					addValue(ref obj, entry);
				}
				i++;
			}
			return obj;
		}

		//
		// Get the value of a property from a yaml object
		//
		//
		public static string GetPropertyValue(YamlObject obj, string name)
		{
			// Object has no values
			//
			if (obj.Value == null)
			{
				return String.Empty;
			}
			if (obj.Value is List<YamlObject>)
			{
				var list = obj.Value as List<YamlObject>;
				if (list == null)
				{
					return String.Empty;
				}
				foreach (YamlObject o in list)
				{
					if (o.Key == name)
					{
						if (o.Value is string)
						{
							string value = (string)o.Value;
							return value.Trim();
						}
					}
				}
			}
			return String.Empty;
		}

		//
		// Return the object wich has the property with that name and optional content
		//
		public static YamlObject? FindObject(YamlObject obj, string name, bool recurse, string? content = null)
		{
			if ((obj == null) || (obj.Value == null))
			{
				return null;
			}
			// The object can contain other objects.
			// If the value is not a list, we just check the object value
			//
			if (obj.Value is not List<YamlObject>)
			{
				if (obj.Value is not YamlObject)
				{
					return null;
				}
				YamlObject? o = obj.Value as YamlObject;
				if (o == null)
				{
					return null;
				}
				if (o.Key == name)
				{
					if (content != null)
					{
						if (o.Value is string)
						{
							if ((string)o.Value == content)
							{
								return o;
							}
						}
					}
					else
					{
						return o;
					}
				}
				return null;
			}
			var list = obj.Value as List<YamlObject>;
			if (list == null)
			{
				return null;
			}
			foreach (YamlObject o in list)
			{
				// If the name matches
				//
				if (o.Key == name)
				{
					// And we've have a content to match
					//
					if (content != null)
					{
						// If the value is a string
						//
						if (o.Value is string)
						{
							// If the content matches
							//
							if ((string)o.Value == content)
							{
								// We found the node which has this name + content as property
								return o;
							}
						}
					}
					else
					{
						// If we don't have content, we just return the object
						//
						return o;
					}
				}
				// Key doesn't match, but we can do a recursive search
				if (recurse)
				{
					YamlObject? found = FindObject(o, name, recurse, content);
					if (found != null)
						return found;
				}
			}
			return null;
		}

		//
		// Returns the object in the list which meets the condition (has an element with key and optional value)
		//
		public static YamlObject? FindObjectInList(YamlObject obj, bool recurse, string key, string? value = null)
		{
			if (obj.Value == null)
			{
				return null;
			}
			if (obj.Value is not List<YamlObject>)
			{
				return null;
			}
			var list = obj.Value as List<YamlObject>;
			if (list == null)
			{
				return null;
			}
			foreach (YamlObject o in list)
			{
				if (o.Key == key)
				{
					if (value != null)
					{
						if (o.Value is string)
						{
							if ((string)o.Value == value)
							{
								return obj;
							}
						}
					}
					else
					{
						return obj;
					}
				}
				else
				{
					if (recurse)
					{
						YamlObject? found = FindObjectInList(o, recurse, key, value);
						if (found != null)
							return found;
					}
				}
			}
			return null;
		}


		//
		// Parses a yaml object. It receives the object where to set the value.
		// the list of lines, the start line, the end line and the indentation level
		//
		//
		static private int parseObject(ref YamlObject obj, string[] lines, int start, int end, int indent)
		{
			for (int i = start; i < end;)
			{
				//
				// Take the current line
				//
				string line = lines[i];
				//
				// If we're on the end of object
				//
				if (line == "...")
				{
					// We're on the ond of the object
					return i;
				}
				// Skip object begin
				if (line == "---")
				{
					// We get a new object begin, it should be parsed on the parent function 
					return i;
				}
				//
				// Get the indentation level for this line
				// Get if this line has an hippen
				//
				int newindent = countSpaces(line);
				bool isSequenceEntry = hasHippen(ref line);
				//
				// Is a sequence entry and the indentation level
				//
				if ((isSequenceEntry) && (newindent == indent))
				{
					//
					// Get this sequence entry key and possible value
					//
					string[] parts = SplitByColon(line);
					//
					// Create a new object for this sequence entry
					//
					YamlObject entry = new YamlObject(parts[0], parts[1]);
					//
					// Since is a sequence entry and we will gonna parse it we will use the real indent (without the hippen)
					// to fetch the possible members of this sequence
					//
					int indentline = countSpacesAndHipens(lines[i]);
					i = parseObject(ref entry, lines, i + 1, end, indentline);
					//
					// Insert ourselves into the list only we're not yet
					// 
					if (entry.Value is List<YamlObject>)
					{
						var list = entry.Value as List<YamlObject>;
						if (list != null)
						{
							if (list.Count > 0)
							{
								string currKey = SanitizeKey(parts[0]);
								if (list[0].Key != currKey)
								{
									insertValue(ref entry, parts[0], parts[1]);
								}
							}
						}
					}
					else
					{
						insertValue(ref entry, parts[0], parts[1]);
					}
					//
					// Since is a sequence we ensure value is a list
					//
					if (obj.Value == null)
					{
						obj.Value = new List<YamlObject>();
					}
					//
					// And add the entry to the object
					//
					addValue(ref obj, entry);
					continue;
				}
				//
				// Look if this key has lower indent
				//
				if (newindent < indent)
				{
					// if this line has a lower intentation we belong to the parent and not to this object
					// return this line to be processed on the parent object
					return i;
				}

				//
				// If we're on the same level, then we're on the same object
				// Add the key / value to the object
				//
				if (newindent == indent)
				{
					// Look for key value
					string[] parts = SplitByColon(line);
					// Prior adding the line, analize the next one to see what we are talking about
					//
					// If we are on the end of the file, just add the key / value and forget about it
					if (i + 1 == end)
					{
						// Add just the value in the object
						//
						addValue(ref obj, parts[0], parts[1]);
						i++;
						continue;
					}
					int nextindent = countSpaces(lines[i + 1]);
					//
					// If the next object has greather indentation, then is a nested object
					//
					if ((nextindent > indent))
					{
						//
						// We create a new object and parse it with the name of the key wich may hold
						// One ore more values
						//
						YamlObject newEntry = new YamlObject(parts[0]);
						// We pass the new object to be fulfilled
						int indentline = countSpaces(lines[i + 1]);
						i = parseObject(ref newEntry, lines: lines, start: i + 1, end: end, indent: indentline);
						// add it to the object
						addValue(ref obj, newEntry);
						continue;
					}
					//
					// Is just a key / value. Store it.
					//
					//
					addValue(ref obj, parts[0], parts[1]);
					i++;
					continue;

				}
				else
				{
					//
					// If doesn't have less indentantion and not the same, is just greather
					// So, is a child object, count new indentantion and add it. Set as key our parent key
					//
					YamlObject newEntry = new YamlObject(obj.Key);
					// We pass the new object to be fulfilled
					int indentline = countSpaces(lines[i]);
					i = parseObject(ref newEntry, lines, i, end, indentline);
					// add it to the object
					addValue(ref obj, newEntry);
					continue;
				}
			}
			return end;
		}

		//
		// Adds a value into a yaml object
		//
		static private void addValue(ref YamlObject obj, string key, string value)
		{
			// If there is no value add the object
			//
			if (obj.Value == null)
			{
				var newobj = new YamlObject(key, value);
				obj.Value = newobj;
				return;
			}
			// If the value is a list, add it into the list
			//
			if (obj.Value is List<YamlObject> list)
			{
				list.Add(new YamlObject(key, value));
				return;
			}
			if (obj.Value is YamlObject currObject)
			{
				// If the value is a string, create a list and add the string and the new value
				//
				var listnew = new List<YamlObject>();
				listnew.Add(currObject);
				listnew.Add(new YamlObject(key, value));
				obj.Value = listnew;
				return;
			}
			if (obj.Value is string)
			{
				// If the value was an string, we discard the value and switch it for a yaml object
				// with the new values
				var newobj = new YamlObject(key, value);
				obj.Value = newobj;
				return;
			}
		}

		//
		// Adds a value into a yaml object
		//
		static private void addValue(ref YamlObject obj, YamlObject value)
		{
			// If there is no value add the object
			//
			if (obj.Value == null)
			{
				obj.Value = value;
				return;
			}
			// If the value is a list, add it into the list
			//
			if (obj.Value is List<YamlObject> currList)
			{
				currList.Add(value);
				return;
			}
			if (obj.Value is YamlObject currObject)
			{
				// If the value is a string, create a list and add the string and the new value
				//
				var list = new List<YamlObject>();
				list.Add(currObject);
				list.Add(value);
				obj.Value = list;
				return;
			}
			if (obj.Value is string)
			{
				// If the value was an string, we discard the value and switch it for a yaml object
				obj.Value = value;
				return;
			}
		}

		static private void insertValue(ref YamlObject obj, string key, string value)
		{
			if (obj.Value == null)
			{
				var newobj = new YamlObject(key, value);
				obj.Value = newobj;
				return;
			}
			if (obj.Value is List<YamlObject> currList)
			{
				currList.Insert(0, new YamlObject(key, value));
				return;
			}
			if (obj.Value is YamlObject currObject)
			{
				var list = new List<YamlObject>();
				list.Add(new YamlObject(key, value));
				list.Add(currObject);
				obj.Value = list;
				return;
			}
		}



		static private int countSpaces(string line)
		{
			int count = 0;
			foreach (char c in line)
			{
				if (c == ' ')
				{
					count++;
				}
				else
				{
					break;
				}
			}
			return count;
		}

		static private string SanitizeValue(string value)
		{
			value = value.Replace("'", "");
			value = value.Trim();
			return value;
		}

		static private string SanitizeKey(string key)
		{
			key = key.Replace("'", "");
			key = key.Replace("-", "");
			key = key.Trim();
			return key;
		}


		static private int countSpacesAndHipens(string line)
		{
			int count = 0;
			foreach (char c in line)
			{
				if (c == ' ')
				{
					count++;
				}
				else if (c == '-')
				{
					count++;
				}
				else
				{
					break;
				}
			}
			return count;
		}


		//
		// Returns true if the line contains an hippen
		//
		static private bool hasHippen(ref string line)
		{
			int count = 0;
			char[] parseLine = line.ToCharArray();
			foreach (char c in parseLine)
			{
				if (c == '-')
				{
					return true;
				}
				count++;
			}
			return false;
		}


		//
		// Splits a line by colon
		//
		// If the line doesn't contain a colon, it returns the line as the key and an empty string as the value
		// If the line contains more than one colon, it returns the first part as the key and the rest as the value
		//
		//
		static private string[] SplitByColon(string line)
		{
			//
			// We need to split the line only by first colon since the content can be anything
			//
			string[] parts = line.Split(":");
			if (parts.Length == 2)
			{
				return parts;
			}
			if (parts.Length > 2)
			{
				//
				// If we have more than 2 parts, we need to join the rest of the parts
				//
				string[] newpart = new string[2];
				newpart[0] = parts[0];
				newpart[1] = String.Join(":", parts.Skip(1));
				return newpart;
			}
			string[] newparts = new string[2];
			newparts[0] = line;
			newparts[1] = String.Empty;
			return newparts;
		}
	}
}