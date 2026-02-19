/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Collections;
using System.ComponentModel;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing;
using Kltv.Kombine.Api;

namespace Kltv.Kombine.Types {

	/// <summary>
	/// KList class.
	/// It is a regular list with helper functions to be used in build environemnts.
	/// </summary>
	public class KList: IEnumerable<KValue> {

		private List<KValue> m_list = new List<KValue>();

		/// <summary>
		/// Default constructor
		/// </summary>
		public KList() {
		}

		/// <summary>
		/// Copy constructor
		/// </summary>
		/// <param name="other">The object to be copied</param>
		public KList(KList other){
			m_list = new List<KValue>(other.m_list);
		}

		/// <summary>
		/// Flatten the list into a single string
		/// </summary>
		/// <returns>A single string with all the values space separated.</returns>
		public KValue Flatten() {
			KValue kValue = new KValue();
			foreach(KValue v in m_list) {
				kValue += v + " ";
			}
			return kValue;
		}

		/// <summary>
		/// Flatten the list into a single string specifying the separator
		/// </summary>
		/// <param name="separator">separator to use</param>
		/// <returns>A single string with all the values separated by the given separator.</returns>
		public KValue Flatten(string separator) {
			KValue kValue = new KValue();
			foreach (KValue v in m_list) {
				kValue += v + separator;
			}
			return kValue;
		}


		/// <summary>
		/// Returns the number of elements in the list.
		/// </summary>
		/// <returns>The number of elements</returns>
		public int Count() {
			return m_list.Count;
		}

		/// <summary>
		/// Returns if the list contains duplicates
		/// </summary>
		/// <returns>True if has duplicates. False otherwise.</returns>
		public bool HasDuplicates() {
			HashSet<KValue> set = new HashSet<KValue>();
			foreach (KValue v in m_list) {
				if (set.Contains(v))
					return true;
				set.Add(v);
			}
			return false;
		}

		/// <summary>
		/// Removes all the duplicates from the list.
		/// </summary>
		/// <returns>The new list without duplicates.</returns>
		public KList RemoveDuplicates(){
			KList n = new KList();
			HashSet<KValue> set = new HashSet<KValue>();
			foreach (KValue v in m_list) {
				if (set.Contains(v))
					continue;
				set.Add(v);
				n.Add(v);
			}
			return n;
		}

		/// <summary>
		/// Returns a new list with all the values being considered as files with the extension changed.
		/// </summary>
		/// <param name="n">New extension to use.</param>
		/// <returns>The newly generated list with the new file extension.</returns>
		public KList WithExtension(string n){
			KList nlist = new KList();
			for (int i = 0; i < m_list.Count; i++) {
				 KValue nv = m_list[i].WithExtension(n);
				 if (nv.IsEmpty()){
					Msg.Print("Warning: Empty value after extension change: " + m_list[i], Msg.LogLevels.Verbose);
					continue;
				 } 
				 nlist.Add(nv);
			}
			return nlist;
		}

		/// <summary>
		/// Replaces in all the elements the old values with the new ones.
		/// If some element becomes empty after the replace, it will be discarded.
		/// </summary>
		/// <param name="ov">old value to be replaced</param>
		/// <param name="nv">new value to be used.</param>
		/// <returns>The new modified KList</returns>
		public KList WithReplace(string ov,string nv){
			KList nlist = new KList();
			for (int i = 0; i < m_list.Count; i++) {
				KValue knv = m_list[i].ToString().Replace(ov,nv);
				if (knv.IsEmpty()) {
					Msg.Print("Warning: Empty value after replace: " + m_list[i], Msg.LogLevels.Verbose);
					continue;
				}
				nlist.Add(knv);
			}
			return nlist;
		}

		/// <summary>
		/// Compares two lists and returns if they are equal or not.
		/// </summary>
		/// <param name="other">The list to be compared against.</param>
		/// <returns>True if they are equal, false otherwise.</returns>
		public bool Compare(KList other) {
			if (m_list.Count != other.Count())
				return false;
			for (int i = 0; i < m_list.Count; i++) {
				if (m_list[i] != other[i])
					return false;
			}
			return true;
		}

		/// <summary>
		/// Prefixes all the values in the list with the given value
		/// </summary>
		/// <param name="n">The value to be used as prefix</param>
		/// <returns>The new list generated with the prefixes.</returns>
		public KList WithPrefix(KValue n) {
			KList nlist = new KList();
			for (int i = 0; i < m_list.Count; i++) {
				string? item = m_list[i].ToString();
				if (item != null) {
					item = n + item;
					nlist.Add(item);
				}
			}
			return nlist;
		}

		/// <summary>
		/// Returns a new list with all the values being considered as files/folders using only the folder paths.
		/// It does not return duplicates so if several entries in the list can be consideer as files into the same folder
		/// they will produce just one folder entry in the new list.
		/// </summary>
		/// <returns>The newly created list with folder values.</returns>
		public KList AsFolders() {
			KList list = new KList();
			foreach(KValue v in m_list) {
				KValue n = v.AsFolder();
				if ( (n.IsEmpty() == false) && (list.Contains(n) == false))
					list.Add(n);
			}
			return list;
		}

		/// <summary>
		/// Returns if the list contains the given value.
		/// </summary>
		/// <param name="v">Value to be checked.</param>
		/// <returns>True if its on the list, false otherwise.</returns>
		public bool Contains(KValue v) {
			foreach(KValue item in m_list) {
				if (item == v)
					return true;
			}
			return false;
		}

		public IEnumerator GetEnumerator() {
			return m_list.GetEnumerator();
		}

		public KValue this[int index] { 
			get => m_list[index];
		}

		IEnumerator<KValue> IEnumerable<KValue>.GetEnumerator() {
			return m_list.GetEnumerator();
		}

		public void Add(KValue v) {
			m_list.Add(v);
		}

		public void Add(string v) {
			m_list.Add(v);
		}

		public void Add(KList v) {
			foreach (KValue item in v) {
				Add(item);
			}
		}

		public void Remove(KValue v) {
			m_list.Remove(v);
		}

		public void Remove(string v) {
			m_list.Remove(v);
		}

		public void Remove(KList v) {
			foreach (KValue item in v) {
				Remove(item);
			}
		}

	
		public static KList operator+(KList a, KList b) {
			a.m_list.AddRange(b.m_list);
			return a;
		}

		public static KList operator+(KList a, KValue b) {
			a.Add(b);
			return a;
		}

		public static KList operator+(KList a, string b) {
			a.Add(b);
			return a;
		}

		public static KList operator-(KList a, KValue b) {
			a.m_list.Remove(b);
			return a;
		}

		public static KList operator-(KList a,string b){
			a.m_list.Remove(b);
			return a;
		}

		public static KList operator-(KList a,KList b){
			foreach(KValue item in b){
				a.m_list.Remove(item);
			}
			return a;
		}

		public static bool operator==(KList a,KList b){
			return a.Compare(b);
		}

		public static bool operator!=(KList a,KList b){
			return !a.Compare(b);
		}

		public static implicit operator KList(string v) {
			KList kList = new KList();
			kList.Add(v);
			return kList;
		}

		public static implicit operator KList(KValue v) {
			KList kList = new KList();
			kList.Add(v);
			return kList;
		}

		public static implicit operator KList(string[] strings) {
			KList kList = new KList();
			foreach (string s in strings) {
				kList.Add(s);
			}
			return kList;
		}

		public static implicit operator KValue(KList kList) {
			return kList.Flatten();
		}

		public static implicit operator string(KList kList){
			return kList.Flatten();
		}

		public static implicit operator string[](KList kList) {
			string[] strings = new string[kList.Count()];
			for (int i = 0; i < kList.Count(); i++) {
				strings[i] = kList[i];
			}
			return strings;
		}

		public static implicit operator KList(Array array) {
			KList kList = new KList();
			foreach (object o in array) {
				string? s = o.ToString();
				if (s != null)
					kList.Add(s);
			}
			return kList;
		}

		public override bool Equals(object? obj) {
			if (obj == null || GetType() != obj.GetType()) {
				return false;
			}
			KList other = (KList)obj;
			return Compare(other);
		}

		public override int GetHashCode() {
			return base.GetHashCode();
		}

	}
}