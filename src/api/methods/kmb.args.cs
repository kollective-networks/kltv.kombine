/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Simplify the access to the action arguments
	/// </summary>
	public static class Args{

		/// <summary>
		/// Returns if the value is pressent in the action arguments
		/// </summary>
		/// <param name="arg">Argument to test</param>
		/// <returns>True if was pressent, false otherwise.</returns>
		public static bool Contains(string arg){
			return Config.ActionParameters.Contains(arg);
		}

		/// <summary>
		/// Returns the value of the argument at the given index.
		/// </summary>
		/// <param name="index">Index to retrieve</param>
		/// <returns>Argument or empty if out of bounds of the array.</returns>
		public static string Get(int index){
			if (index < Config.ActionParameters.Length)
				return Config.ActionParameters[index];
			return string.Empty;
		}
	}
}