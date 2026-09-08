/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Engine switches a script can read and change. They mirror the tool parameters and apply to the
	/// scripts compiled from then on, that is, the child scripts run with Kombine(): the running script
	/// is already compiled.
	/// </summary>
	public static class Engine {

		/// <summary>
		/// Last failure of a child script run with Kombine(): the script could not be found, its
		/// references could not be resolved, it did not compile, the action was not found or did not
		/// return an int, or it threw or aborted. Reset when a child script is run and set on failure,
		/// so the calling script can explain a non zero return code with its own message.
		/// </summary>
		public static ApiError LastError { get; internal set; } = ApiError.None;

		/// <summary>
		/// Allows the forward search of the subfolders when resolving #load and child script references.
		/// Mirrors the -kforward parameter. It is disabled by default because it can bind a foreign copy
		/// of a helper when repositories are nested.
		/// </summary>
		public static bool ForwardSearch {
			get { return Config.ResolveForward; }
			set { Config.ResolveForward = value; }
		}

		/// <summary>
		/// Forces the rebuild of the scripts compiled from now on, even if a compiled version is cached.
		/// Mirrors the -ksrb parameter. Needed to make a change of ForwardSearch effective on a cached
		/// child script, since the resolution happens at compile time.
		/// </summary>
		public static bool RebuildScripts {
			get { return Config.Rebuild; }
			set { Config.Rebuild = value; }
		}
	}
}
