/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine {

	/// <summary>
	/// Dicionary extensions
	/// </summary>
	public static class DictionaryExtensions {

		/// <summary>
		/// Dictionary clone method
		/// </summary>
		/// <typeparam name="TKey">Key type</typeparam>
		/// <typeparam name="TValue">Value type</typeparam>
		/// <param name="dictionary">Dictionary to clone</param>
		/// <returns>A new dictionary instance with all the contents cloned</returns>
		public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this Dictionary<TKey, TValue> dictionary) where TKey : class {
			var clone = new Dictionary<TKey, TValue>(dictionary.Count, dictionary.Comparer);
			foreach (var kvp in dictionary) {
				if (kvp.Key is ICloneable keyCloneable) {
					clone.Add((TKey)keyCloneable.Clone(), kvp.Value);
				} else {
					clone.Add(kvp.Key, kvp.Value);
				}
			}
			return clone;
		}
	}

	/// <summary>
	/// Internal exception type to signalize when the script should be aborted
	/// </summary>
	public class ScriptAbortException : Exception {
		public ScriptAbortException() : base() { }
		public ScriptAbortException(string message) : base(message) { }

		public ScriptAbortException(string message, Exception innerException) : base(message, innerException) { }
	}
}