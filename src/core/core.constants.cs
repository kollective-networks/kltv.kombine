/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/


namespace Kltv.Kombine {

	/// <summary>
	/// Holds all the string constants for the tool
	/// </summary>
	internal static class Constants {

		// Cache Folder
		// ----------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		public const string Cache_Folder =			@"./kombine/cache/";

		/// <summary>
		/// 
		/// </summary>
		public const string Cache_States = 			Cache_Folder+@"states/";
		/// <summary>
		/// 
		/// </summary>
		public const string Cache_Scripts = 		Cache_Folder+@"scripts/";


		// Build Logs
		// ----------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		public const string Log_Folder =			@"./build/logs/";
		/// <summary>
		/// 
		/// </summary>
		public const string Log_FileExtension =		@".log";
		/// <summary>
		/// 
		/// </summary>
		public const string Log_FileSuccess =		@"success";
		/// <summary>
		/// 
		/// </summary>
		public const string Log_FileFailed =		@"failed";
		/// <summary>
		/// 
		/// </summary>
		public const string Log_Prefix =			@"[kmb{0}] ";

		// Extensions
		// ----------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		public const string Ext_Compiled =			@".dat";


		// Output Artifacts Folders
		// ----------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		public const string Art_Folder = @"./build/";

		/// <summary>
		/// Temporal folder in top of artifact folders
		/// </summary>
		public const string Tmp_Folder = @"./tmp/";

		/// <summary>
		/// Binaries folder in top of artifact folders
		/// </summary>
		public const string Bin_Folder = @"./bin/";

		/// <summary>
		/// Static libraries / references / facades folder in top of artifact folders
		/// </summary>
		public const string Lib_Folder = @"./lib/";

		/// <summary>
		/// Public Includes folder in top of artifact folders
		/// </summary>
		public const string Inc_Folder = @"./inc/";

		/// <summary>
		/// Publishing folder in top of artifact folders
		/// </summary>
		public const string Pub_Folder = @"./pub/";

		/// <summary>
		/// Build generated content folder in top of artifact folders
		/// </summary>
		public const string Gen_Folder = @"./gen/";

		/// <summary>
		/// Documents generated folder in top of artifact folders
		/// </summary>
		public const string Doc_Folder = @"./doc/";

		/// <summary>
		/// External dependencies / packages folder in top of artifact folders
		/// </summary>
		public const string Sdk_Folder = @"./sdk/";

		/// <summary>
		/// Default script filename 
		/// </summary>
		public const string Mak_File =	@"kombine.csx";
	}
}