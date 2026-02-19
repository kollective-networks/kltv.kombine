/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Simplify the access to the action arguments (current script)
	/// </summary>
	public static class Args{

		/// <summary>
		/// Returns if the script or parent was rebuilded
		/// </summary>
		public static bool WasRebuilded {
			get {
				if (KombineMain.CurrentRunningScript == null)
					return false;
				if (KombineMain.CurrentRunningScript.WasRebuilt)
					return true;
				if (KombineMain.CurrentRunningScript.ParentWasRebuilt)
					return true;
				return false;
			}
		}

		/// <summary>
		/// Returns if the value is pressent in the action arguments
		/// </summary>
		/// <param name="arg">Argument to test</param>
		/// <returns>True if was pressent, false otherwise.</returns>
		public static bool Contains(string arg){
			if (KombineMain.CurrentRunningScript == null)
				return false;
			return KombineMain.CurrentRunningScript.ActionParameters.Contains(arg);
		}

		/// <summary>
		/// Returns the value of the argument at the given index.
		/// </summary>
		/// <param name="index">Index to retrieve</param>
		/// <returns>Argument or empty if out of bounds of the array.</returns>
		public static string Get(int index){
			if (KombineMain.CurrentRunningScript == null)
				return string.Empty;
			if (index < KombineMain.CurrentRunningScript.ActionParameters.Length)
				return KombineMain.CurrentRunningScript.ActionParameters[index];
			return string.Empty;
		}
	}

	/// <summary>
	/// Simplify the access to the action arguments (starting script)
	/// </summary>
	public static class EntryArgs {

		/// <summary>
		/// Returns if the value is pressent in the action arguments
		/// </summary>
		/// <param name="arg">Argument to test</param>
		/// <returns>True if was pressent, false otherwise.</returns>
		public static bool Contains(string arg) {
			return Config.ActionParameters.Contains(arg);
		}

		/// <summary>
		/// Returns the value of the argument at the given index.
		/// </summary>
		/// <param name="index">Index to retrieve</param>
		/// <returns>Argument or empty if out of bounds of the array.</returns>
		public static string Get(int index) {
			if (index < Config.ActionParameters.Length)
				return Config.ActionParameters[index];
			return string.Empty;
		}
	}



}