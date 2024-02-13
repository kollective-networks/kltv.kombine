/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.Text;
using Kltv.Kombine.Api;

namespace Kltv.Kombine.Types {

	/// <summary>
	/// Encapsulate a Kombine Value
	/// </summary>
	public class KValue {

		/// <summary>
		/// Kombine Value constructor
		/// </summary>
		public KValue() {
			m_value = string.Empty;
		}

		/// <summary>
		/// Export a value to the environment
		/// </summary>
		/// <param name="name">Name to be set on the environment</param>
		public void Export(string name) {
			if (KombineMain.CurrentRunningScript != null) {
				Msg.PrintMod("Exporting variable: " + name + " to environment.", ".value", Msg.LogLevels.Verbose);
				// TODO: Warning if already exists / collisions
				KombineMain.CurrentRunningScript.State.Environment[name] = m_value;
			}
		}

		/// <summary>
		/// Import a value from the environment.
		/// If there is no default value and the variable is not found, it will abort the script.
		/// </summary>
		/// <param name="name">Name of the variable to be recovered from environment</param>
		/// <param name="defvalue">Optional default value in case the environement variable is not present.</param>
		/// <returns>The variable value</returns>
		public static KValue Import(string name,string? defvalue = null) {
			if (KombineMain.CurrentRunningScript != null) {
				if (KombineMain.CurrentRunningScript.State.Environment.ContainsKey(name)) {
					Msg.PrintMod("Fetching variable: " + name + " from environment.", ".value", Msg.LogLevels.Verbose);
					return KombineMain.CurrentRunningScript.State.Environment[name];
				}
			}
			if (defvalue != null) {
				Msg.PrintMod("Fetching variable: " + name + " to default value.", ".value", Msg.LogLevels.Verbose);
				return new KValue() { m_value = defvalue };
			}
			Msg.PrintMod("Fetching variable: " + name + " no result. Empty.", ".value", Msg.LogLevels.Verbose);
			Msg.PrintAndAbortMod("Variable not found and no default value. Aborting", ".value");
			return new KValue();
		}

		/// <summary>
		/// Returns an array from a KValue which is space separated
		/// </summary>
		/// <returns>The KList containing all the fetched values</returns>
		public KList ToArray(){
			KList list = new KList();
			string[] delimiters = { " " };
			string[] b = m_value.Split(delimiters,StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach(string item in b){
				list.Add(item);
			}
			return list;
		}

		/// <summary>
		/// Returns an array from a KValue which is separated by the given separator
		/// </summary>
		/// <param name="separator">Separator to use</param>
		/// <returns>The KList array</returns>
		public KList ToArray(KValue separator){
			KList list = new KList();
			string[] delimiters = { separator };
			string[] b = m_value.Split(delimiters,StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach(string item in b){
				list.Add(item);
			}
			return list;
		}

		/// <summary>
		/// Returns an array from a KValue which is separated by the given separators
		/// </summary>
		/// <param name="separators">string array of separators to use.</param>
		/// <returns>The KList array</returns>
		public KList ToArray(string[] separators){
			KList list = new KList();
			string[] b = m_value.Split(separators,StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach(string item in b){
				list.Add(item);
			}
			return list;
		}

		/// <summary>
		/// Converts the KValue to a list of arguments
		/// It will split the string by spaces, but it will keep the quoted strings as one argument.
		/// </summary>
		/// <returns>The created list with the arguments</returns>
		public KList ToArgs(){
			KList list = new KList();
			string[] delimiters = { " " };
			string[] b = m_value.Split(delimiters,StringSplitOptions.RemoveEmptyEntries);
			bool inquote = false;
			string current = "";
			foreach(string item in b){
				if (item.Contains("\"")){
					if (inquote){
						current += " "+item;
						list.Add(current);
						current = "";
						inquote = false;
						continue;
					} else{
						current = item;
						inquote = true;
						continue;
					}
				}
				if (!inquote){
					list.Add(item);
				} else{
					current += " "+item;
				}
			}
			return list;
		}


		/// <summary>
		/// If the value can be considered as a file, it will return the file name with extension changed
		/// </summary>
		/// <param name="n">New extension to be applied</param>
		/// <returns>Value changed or empty if is not a file.</returns>
		public KValue WithExtension(string n) {
			string? item = Path.ChangeExtension(m_value, n);
			if (item != null) {
				item = ConvertToUnixPath(item);
				return new KValue() { m_value = item };
			}
			return new KValue();
		}

		/// <summary>
		/// Check if the value can be interpreted as file and has a given extension
		/// </summary>
		/// <param name="ext">extension to be checked</param>
		/// <returns>true if the extension was pressent, false otherwise</returns>
		public bool HasExtension(string ext) {
			return m_value.EndsWith(ext);
		}

		/// <summary>
		/// Check if the value is empty.
		/// </summary>
		/// <returns>True if its empty, false otherwise</returns>
		public bool IsEmpty() {
			return m_value == string.Empty;
		}
		
		/// <summary>
		/// Returns the value as is a folder without filename in case it is a file.
		/// </summary>
		/// <returns>The folder value</returns>
		public KValue AsFolder() {
			string? item = Path.GetDirectoryName(m_value);
			if (item != null) {
				item = ConvertToUnixPath(item);
				return new KValue() { m_value = item };
			}
			return new KValue();
		}

		/// <summary>
		/// Returns the parent folder of the value if can be consideer as path
		/// </summary>
		/// <returns>The parent folder or empty if cannot be converted.</returns>
		public KValue GetParent(){
			try{
				DirectoryInfo? di = new DirectoryInfo(m_value);
				if (di != null){
					di = di.Parent;
					if (di != null) {
						string? item = di.FullName;
						if (item != null) {
							item = ConvertToUnixPath(item);
							return new KValue() { m_value = item };
						}
					}
				}
			} catch (Exception ex){
				Msg.PrintErrorMod("Error getting parent folder: "+ex.Message, ".value", Msg.LogLevels.Verbose);
			}
			return new KValue();
		}


		/// <summary>
		/// Returns a new KValue with the name prefixed with the indicated prefix
		/// TODO:  Review, is switch the back/forwd slash
		/// </summary>
		/// <param name="prefix">Prefix to add into the filename</param>
		/// <returns>The new kvalue, changed or unchanged if not possible.</returns>
		public KValue WithNamePrefix(string prefix) {
			string? path = Path.GetDirectoryName(m_value);
			string? file = Path.GetFileName(m_value);
			if (path != null && file != null) {
				Msg.PrintMod("Adding the prefix to the filename: " + prefix, ".value", Msg.LogLevels.Debug);
				return new KValue() { m_value = Path.Combine(path, prefix + file) };
			}
			Msg.PrintWarningMod("Could not add the prefix to the filename."+prefix, ".value");
			KValue n = new KValue();
			n.m_value = m_value;
			return n;
		}

		/// <summary>
		/// Equality operator. It compares contents, not references.
		/// </summary>
		/// <param name="a">param1 to be compared</param>
		/// <param name="b">param2 to be compared</param>
		/// <returns>true if equal, false otherwise</returns>
		public static bool operator==(KValue a, KValue b) {
			return a.m_value == b.m_value;
		}

		/// <summary>
		/// Non equality operator. It compares contents, not references.
		/// </summary>
		/// <param name="a">param1 to be compared</param>
		/// <param name="b">param2 to be compared</param>
		/// <returns>true if non equal, false otherwise</returns>
		public static bool operator!=(KValue a, KValue b) {
			return a.m_value != b.m_value;
		}

		///<summary>
		/// Add operator
		///</summary>
		public static KValue operator+(KValue a, KValue b) {
			KValue c = new KValue();
			c.m_value = a.m_value + b.m_value;
			return c;
		}

		/// <summary>
		/// Substract operator
		/// </summary>
		public static KValue operator-(KValue a, KValue b) {
			KValue c = new KValue();
			c.m_value = a.m_value.Replace(b.m_value, "");
			return c;
		}

		///<summary>
		/// Implicit creation operator (from string to Kvalue)
		///</summary>
		public static implicit operator KValue(string v) {
			return new KValue() { m_value = v };
		}

		/// <summary>
		/// Implicit creation operator (from KValue to string)
		/// </summary>
		public static implicit operator string(KValue v) {
			return v.m_value;
		}	

		/// <summary>
		/// Internal string value
		/// </summary>
		private string m_value = string.Empty;

		/// <summary>
		/// Object comparison operator. Compares reference and content.
		/// </summary>
		public override bool Equals(object? obj) {
			if (ReferenceEquals(obj, null)) {
				return false;
			}
			if (m_value == ((KValue)obj).m_value) {
				return true;
			}
			return false;
		}

		/// <summary>
		/// Reduces whitespaces in the KValue. Only one consecutive is allowed.
		/// </summary>
		/// <returns>A new KValue without duplicated whitespaces</returns>
		public KValue ReduceWhitespace() {
			var newString = new StringBuilder();
			bool previousIsWhitespace = false;
			for (int i = 0; i < m_value.Length; i++) {
				if (Char.IsWhiteSpace(m_value[i])) {
					if (previousIsWhitespace) {
						continue;
					}
					previousIsWhitespace = true;
				} else {
					previousIsWhitespace = false;
				}
				newString.Append(m_value[i]);
			}
			return new KValue() { m_value = newString.ToString() };
		}

		/// <summary>
		/// Convert the kvalue to string
		/// </summary>
		/// <returns>The represented string</returns>
		public override string ToString() {
			return m_value;
		}

		/// <summary>
		/// Calculates a hash for the given object. Hash is not be fixed. Same string != same hash
		/// </summary>
		/// <returns>An int 64 32 returned with the hash value</returns>
		public override int GetHashCode() {
			return m_value.GetHashCode();
		}

		/// <summary>
		/// Calculates a hash for the given string. Hash will be fixed. Same string = same hash
		/// </summary>
		/// <returns>An unsigned int 64 is returned with the hash value</returns>
		public UInt64 GetHashCode64() {
			UInt64 hashedValue = 3074457345618258791ul;
			for (int i = 0; i < m_value.Length; i++) {
				hashedValue += m_value[i];
				hashedValue *= 3074457345618258799ul;
			}
			return hashedValue;
		}

		/// <summary>
		/// Replace slashes from windows to unix style
		/// </summary>
		/// <param name="path">path to change</param>
		/// <returns>the new value</returns>
		private static KValue ConvertToUnixPath(KValue path) {
			string a = path;
			a.Replace("\\", "/");
			return new KValue() { m_value = a };
		}

	}
}