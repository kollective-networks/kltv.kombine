/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
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
		///  Every operation shows its progress through Compress.Progress unless silenced.
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
				return Fail(new ApiError(code, message, source));
			}

			/// <summary>
			/// Records a failure from an exception and logs it at verbose level.
			/// </summary>
			private static bool Fail(Exception ex, string source) {
				return Fail(ApiError.From(ex, source));
			}

			/// <summary>
			/// Records a failure and logs it at verbose level.
			/// </summary>
			private static bool Fail(ApiError error) {
				LastError = error;
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
			/// <param name="showprogress">If the progress line should be shown. Null takes Compress.ShowProgress.</param>
			/// <returns>True if operation was okey. False otherwise (see LastError)</returns>
			public static bool CompressFolder(string folderPath, string outputFile, bool overwrite = true, bool includeFolder = true, bool? showprogress = null) {
				return CompressFolders(new string[] { folderPath }, outputFile, overwrite, includeFolder, showprogress);
			}

			/// <summary>
			/// Compress a list of folders into a zip file.
			/// The folders will be compressed into the root of the zip file.
			/// </summary>
			/// <param name="folderPaths">Folders to compress</param>
			/// <param name="outputFile">Output zip file</param>
			/// <param name="overwrite">If true, overwrite the output file if it exists.</param>
			/// <param name="includeFolder">If true, include the folders in the zip file.</param>
			/// <param name="showprogress">If the progress line should be shown. Null takes Compress.ShowProgress.</param>
			/// <returns>True if operation was okey. False otherwise (see LastError)</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true, bool includeFolder = true, bool? showprogress = null) {
				LastError = ApiError.None;
				// Validate the sources before touching the output, so a failure leaves nothing behind
				foreach (string folderPath in folderPaths) {
					if (!Directory.Exists(folderPath))
						return Fail(ErrorCode.NotFound, "The folder to compress does not exist", folderPath);
				}
				// The files are collected first: the totals feed the progress line
				List<ArchiveFile>? files = CollectFiles(folderPaths, includeFolder, out long totalBytes, out ApiError error);
				if (files == null)
					return Fail(error);
				if (!PrepareOutput(outputFile, overwrite))
					return false;
				ArchiveProgress progress = new ArchiveProgress(Reporter(showprogress), "file", "files");
				progress.Start("Compressing", outputFile, totalBytes, files.Count);
				try {
					using (ZipArchive archive = ZipFile.Open(outputFile, ZipArchiveMode.Create)) {
						foreach (ArchiveFile file in files) {
							archive.CreateEntryFromFile(file.Path, file.Entry, CompressionLevel.Optimal);
							progress.Entry(file.Size);
						}
					}
					progress.Done();
					return true;
				} catch (System.Exception ex) {
					progress.Failed();
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
			/// <param name="showprogress">If the progress line should be shown. Null takes Compress.ShowProgress.</param>
			/// <returns>True if the operation was fine, false otherwise (see LastError)</returns>
			public static bool CompressFile(string filePath, string outputFile, bool overwrite = true, bool? showprogress = null) {
				LastError = ApiError.None;
				if (!Files.Exists(filePath))
					return Fail(ErrorCode.NotFound, "The file to compress does not exist", filePath);
				if (!PrepareOutput(outputFile, overwrite))
					return false;
				long size = FileSize(filePath);
				ArchiveProgress progress = new ArchiveProgress(Reporter(showprogress), "file", "files");
				progress.Start("Compressing", outputFile, size, 1);
				try {
					using (ZipArchive archive = ZipFile.Open(outputFile, ZipArchiveMode.Create)) {
						archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath), CompressionLevel.Optimal);
						progress.Entry(size);
					}
					progress.Done();
					return true;
				} catch (System.Exception ex) {
					progress.Failed();
					RemovePartial(outputFile);
					return Fail(ex, outputFile);
				}
			}

			/// <summary>
			/// Decompress a zip file into a folder. The folder is created if needed.
			/// The entries are extracted one by one: an entry that tries to escape the destination folder
			/// (path traversal) is refused, a file that exists when overwrite is disabled is skipped, and
			/// the remaining entries are still extracted. Any skipped entry makes the call return false:
			/// AlreadyExists when only existing files were skipped, Failed otherwise.
			/// </summary>
			/// <param name="zipPath">Zip file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">True if existing files should be overwritten, default true.</param>
			/// <param name="showprogress">If the progress line should be shown. Null takes Compress.ShowProgress.</param>
			/// <returns>True if every entry was extracted, false otherwise (see LastError).</returns>
			public static bool Decompress(string zipPath, string outputFolder, bool overwrite = true, bool? showprogress = null) {
				LastError = ApiError.None;
				if (!Files.Exists(zipPath))
					return Fail(ErrorCode.NotFound, "The archive to decompress does not exist", zipPath);
				if (!Folders.Create(outputFolder))
					return Fail(ErrorCode.IoError, "The destination folder could not be created: " + Folders.LastError.Message, outputFolder);
				int refused = 0;
				int failed = 0;
				int existing = 0;
				ArchiveProgress progress = new ArchiveProgress(Reporter(showprogress), "entry", "entries");
				try {
					using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
						// The central directory gives the totals up front
						long totalBytes = 0;
						foreach (ZipArchiveEntry entry in archive.Entries)
							totalBytes += entry.Length;
						progress.Start("Decompressing", zipPath, totalBytes, archive.Entries.Count);
						foreach (ZipArchiveEntry entry in archive.Entries) {
							// Entry names use '/', a foreign archive may use '\': both are separators here
							string relative = entry.FullName.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
							string target = Path.Combine(outputFolder, relative);
							// Safe-extract: refuse an entry that escapes the destination folder (path traversal / zip-slip)
							if (!IsInsideOutputFolder(outputFolder, target)) {
								Msg.PrintWarningMod("Refusing entry outside destination (path traversal): " + entry.FullName, ".compress.zip", Msg.LogLevels.Verbose);
								refused++;
								progress.Entry(entry.Length);
								continue;
							}
							try {
								if (string.IsNullOrEmpty(entry.Name)) {
									// A directory entry: just create it
									Directory.CreateDirectory(target);
								} else {
									string? folder = Path.GetDirectoryName(target);
									if (folder != null)
										Directory.CreateDirectory(folder);
									if (!overwrite && File.Exists(target)) {
										Msg.PrintMod("Entry already exists, overwrite is disabled: " + entry.FullName, ".compress.zip", Msg.LogLevels.Verbose);
										existing++;
									} else {
										Msg.PrintMod("Unpacking file: " + entry.FullName, ".compress.zip", Msg.LogLevels.Verbose);
										entry.ExtractToFile(target, overwrite);
									}
								}
							} catch (System.Exception ex) {
								Msg.PrintWarningMod("Entry not extracted: " + entry.FullName + " " + ex.Message, ".compress.zip", Msg.LogLevels.Verbose);
								failed++;
							}
							progress.Entry(entry.Length);
						}
					}
				} catch (System.Exception ex) {
					progress.Failed();
					return Fail(ex, zipPath);
				}
				if (refused > 0 || failed > 0 || existing > 0) {
					progress.Failed();
					return Fail(ExtractionError(refused, failed, existing, zipPath));
				}
				progress.Done();
				return true;
			}
		}
	}
}
