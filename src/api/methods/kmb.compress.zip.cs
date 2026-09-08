/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using System.Data;
using System.IO;
using System.IO.Compression;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Compress methods
	/// </summary>
	public static partial class Compress{

		/// <summary>
		///  Zip compression methods.
		///  Failures are reported through the return value and LastError; nothing is printed at normal level.
		/// </summary>
		public static class Zip {

			/// <summary>
			/// Last failure of a Zip call. Reset at the start of every call, set when it fails.
			/// </summary>
			public static ApiError LastError { get; private set; } = ApiError.None;

			/// <summary>
			/// Records a failure and logs it at verbose level.
			/// </summary>
			private static bool Fail(ErrorCode code, string message, string source) {
				LastError = new ApiError(code, message, source);
				Msg.PrintWarningMod(LastError.ToString(), ".compress.zip", Msg.LogLevels.Verbose);
				return false;
			}

			/// <summary>
			/// Records a failure from an exception and logs it at verbose level.
			/// </summary>
			private static bool Fail(Exception ex, string source) {
				LastError = ApiError.From(ex, source);
				Msg.PrintWarningMod(LastError.ToString(), ".compress.zip", Msg.LogLevels.Verbose);
				return false;
			}

			/// <summary>
			/// Removes a partially written archive after a failure, so nothing misleading is left behind.
			/// </summary>
			private static void RemovePartial(string outputFile) {
				try {
					if (File.Exists(outputFile))
						File.Delete(outputFile);
				} catch (Exception ex) {
					Msg.PrintWarningMod("Could not remove the partial archive: " + outputFile + " " + ex.Message, ".compress.zip", Msg.LogLevels.Verbose);
				}
			}

			/// <summary>
			/// Checks the output file against the overwrite option, deleting it when allowed.
			/// </summary>
			private static bool PrepareOutput(string outputFile, bool overwrite) {
				if (!Files.Exists(outputFile))
					return true;
				if (!overwrite)
					return Fail(ErrorCode.AlreadyExists, "The output file already exists and overwrite is disabled", outputFile);
				if (!Files.Delete(outputFile))
					return Fail(ErrorCode.AccessDenied, "The existing output file could not be replaced: " + Files.LastError.Message, outputFile);
				return true;
			}

			/// <summary>
			/// Compress a single folder into a zip file.
			/// The folder will be compressed into the root of the zip file.
			/// </summary>
			/// <param name="folderPath">Folder to compress</param>
			/// <param name="outputFile">Output zip file</param>
			/// <param name="overwrite">If true, overwrite the output file if it exists.</param>
			/// <param name="includeFolder">If true, include the folder in the zip file.</param>
			/// <returns>True if operation was okey. False otherwise (see LastError)</returns>
			public static bool CompressFolder(string folderPath, string outputFile,bool overwrite = true,bool includeFolder = true) {
				return CompressFolders(new string[] { folderPath }, outputFile,overwrite,includeFolder);
			}

			/// <summary>
			/// Compress a list of folders into a zip file.
			/// The folders will be compressed into the root of the zip file.
			/// </summary>
			/// <param name="folderPaths">Folders to compress</param>
			/// <param name="outputFile">Output zip file</param>
			/// <param name="overwrite">If true, overwrite the output file if it exists.</param>
			/// <param name="includeFolder">If true, include the folders in the zip file.</param>
			/// <returns>True if operation was okey. False otherwise (see LastError)</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile,bool overwrite = true,bool includeFolder = true) {
				LastError = ApiError.None;
				// Validate the sources before touching the output, so a failure leaves nothing behind
				foreach (string folderPath in folderPaths) {
					if (!Directory.Exists(folderPath))
						return Fail(ErrorCode.NotFound, "The folder to compress does not exist", folderPath);
				}
				if (!PrepareOutput(outputFile, overwrite))
					return false;
				// Create the zip file
				try {
					using (var archive = ZipFile.Open(outputFile, ZipArchiveMode.Create)) {
						foreach (string folderPath in folderPaths) {
							var folder = new DirectoryInfo(folderPath);
							FileInfo[] files = folder.GetFiles("*.*", SearchOption.AllDirectories);
							foreach (var file in files) {
								string f;
								if ( (folder.Parent != null) && (includeFolder == true) )
									f = Path.GetRelativePath(folder.Parent.FullName, file.FullName);
								else
									f = Path.GetRelativePath(folder.FullName, file.FullName);
								archive.CreateEntryFromFile(
									file.FullName,
									f,
									CompressionLevel.Optimal
								);
							}
						}
					}
					return true;
				} catch (System.Exception ex) {
					RemovePartial(outputFile);
					return Fail(ex, outputFile);
				}
			}

			/// <summary>
			/// Compress a single file into a zip file.
			/// </summary>
			/// <param name="filePath">File to be compressed</param>
			/// <param name="outputFile">Output zip file</param>
			/// <param name="overwrite">True if file should be overwritten, default true.</param>
			/// <returns>True if the operation was fine, false otherwise (see LastError)</returns>
			public static bool CompressFile(string filePath, string outputFile,bool overwrite = true) {
				LastError = ApiError.None;
				if (!Files.Exists(filePath))
					return Fail(ErrorCode.NotFound, "The file to compress does not exist", filePath);
				if (!PrepareOutput(outputFile, overwrite))
					return false;
				// Create the zip file
				try {
					using (var archive = ZipFile.Open(outputFile, ZipArchiveMode.Create)) {
						archive.CreateEntryFromFile(
							filePath,
							Path.GetFileName(filePath),
							CompressionLevel.Optimal
						);
					}
					return true;
				} catch (System.Exception ex) {
					RemovePartial(outputFile);
					return Fail(ex, outputFile);
				}
			}

			/// <summary>
			/// Decompress a zip file into a folder. The folder is created if needed.
			/// </summary>
			/// <param name="zipPath">Zip file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">True if file should be overwritten, default true.</param>
			/// <returns>True if operation was okey. False otherwise (see LastError)</returns>
			public static bool Decompress(string zipPath, string outputFolder,bool overwrite = true) {
				LastError = ApiError.None;
				if (!Files.Exists(zipPath))
					return Fail(ErrorCode.NotFound, "The archive to decompress does not exist", zipPath);
				try {
					ZipFile.ExtractToDirectory(zipPath, outputFolder,overwrite);
					return true;
				} catch (System.Exception ex) {
					return Fail(ex, zipPath);
				}
			}
		}
	}
}
