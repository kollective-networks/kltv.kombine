/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using System.Data;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers;
using SharpCompress.Readers.Tar;
using SharpCompress.Readers;
using System.Reflection.PortableExecutable;


namespace Kltv.Kombine.Api {

	public static partial class Compress{

		/// <summary>
		/// Tar compression methods.
		/// Failures are reported through the return value and LastError; nothing is printed at normal level.
		/// </summary>
		public static class Tar {

			/// <summary>
			/// Tar compression types
			/// </summary>
			public enum TarCompressionType {
				/// <summary>
				/// No compression, just tar file
				/// </summary>
				None,
				/// <summary>
				/// .tar.gz extension
				/// </summary>
				Gzip,
				/// <summary>
				/// .tar.bz2 extension
				/// </summary>
				Bzip2,
				/// <summary>
				/// .tar.lz extension
				/// </summary>
				Lzma,
				/// <summary>
				/// .tar.xz extension. Extraction only: compressing with it fails with NotSupported.
				/// </summary>
				Lzma2,
			}

			/// <summary>
			/// Last failure of a Tar call. Reset at the start of every call, set when it fails.
			/// </summary>
			public static ApiError LastError { get; private set; } = ApiError.None;

			/// <summary>
			/// Records a failure and logs it at verbose level.
			/// </summary>
			private static bool Fail(ErrorCode code, string message, string source) {
				LastError = new ApiError(code, message, source);
				Msg.PrintWarningMod(LastError.ToString(), ".compress.tar", Msg.LogLevels.Verbose);
				return false;
			}

			/// <summary>
			/// Records a failure from an exception and logs it at verbose level.
			/// </summary>
			private static bool Fail(Exception ex, string source) {
				LastError = ApiError.From(ex, source);
				Msg.PrintWarningMod(LastError.ToString(), ".compress.tar", Msg.LogLevels.Verbose);
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
					Msg.PrintWarningMod("Could not remove the partial archive: " + outputFile + " " + ex.Message, ".compress.tar", Msg.LogLevels.Verbose);
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
				Msg.PrintMod("Overwriting file: " + outputFile, ".compress.tar", Msg.LogLevels.Verbose);
				if (!Files.Delete(outputFile))
					return Fail(ErrorCode.AccessDenied, "The existing output file could not be replaced: " + Files.LastError.Message, outputFile);
				return true;
			}

			/// <summary>
			/// Maps the compression type to the writer one. Lzma2 (xz) is extraction only.
			/// </summary>
			private static bool MapCompression(TarCompressionType compressionType, string outputFile, out CompressionType compType) {
				compType = CompressionType.None;
				switch (compressionType) {
					case TarCompressionType.None:
						compType = CompressionType.None;
						return true;
					case TarCompressionType.Gzip:
						compType = CompressionType.GZip;
						return true;
					case TarCompressionType.Bzip2:
						compType = CompressionType.BZip2;
						return true;
					case TarCompressionType.Lzma:
						compType = CompressionType.LZip;
						return true;
					default:
						return Fail(ErrorCode.NotSupported, "Lzma2 (.tar.xz) compression is not supported, only its extraction", outputFile);
				}
			}

			/// <summary>
			/// Compress a single folder into a tar file.
			/// </summary>
			/// <param name="folderPath">Folder to be compressed</param>
			/// <param name="outputFile">Output tar file</param>
			/// <param name="overwrite">If archive should be overwritten, default true</param>
			/// <param name="includeFolder">If true, include the folder in the tar file.</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise (see LastError).</returns>
			public static bool CompressFolder(string folderPath, string outputFile,bool overwrite = true,bool includeFolder = true, TarCompressionType compressionType = TarCompressionType.Gzip) {
				return CompressFolders(new string[] { folderPath }, outputFile,overwrite,includeFolder,compressionType);
			}

			/// <summary>
			/// Compress a list of folders into a tar file.
			/// </summary>
			/// <param name="folderPaths">Folders to be compressed</param>
			/// <param name="outputFile">Output tar file</param>
			/// <param name="overwrite">If archive should be overwritten, default true</param>
			/// <param name="includeFolder">If true, include the given folders in the tar file and not only the folder contents an descentants.</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise (see LastError).</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true, bool includeFolder = true, TarCompressionType compressionType = TarCompressionType.Gzip) {
				LastError = ApiError.None;
				Msg.PrintMod("Compressing folders: " + string.Join(", ", folderPaths), ".compress.tar", Msg.LogLevels.Verbose);
				// Validate everything before touching the output, so a failure leaves nothing behind
				foreach (string folderPath in folderPaths) {
					if (!Directory.Exists(folderPath))
						return Fail(ErrorCode.NotFound, "The folder to compress does not exist", folderPath);
				}
				if (!MapCompression(compressionType, outputFile, out CompressionType compType))
					return false;
				if (!PrepareOutput(outputFile, overwrite))
					return false;
				try {
					using (var fs = new FileStream(outputFile, FileMode.Create)) {
						using (var tar = new TarWriter(fs, new TarWriterOptions(compType, true))) {

							foreach (string folder in folderPaths) {
								DirectoryInfo dirInfo = new DirectoryInfo(folder);
								string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
								foreach (string file in files) {
									string f;
									if (includeFolder == true) {
										// Entry names must be relative to the folder parent so the folder
										// itself is included, never absolute paths
										if (dirInfo.Parent != null)
											f = Path.GetRelativePath(dirInfo.Parent.FullName, Path.GetFullPath(file));
										else
											f = Path.GetRelativePath(dirInfo.FullName, Path.GetFullPath(file));
									} else {
										f = Path.GetRelativePath(folder, file);
									}
									Msg.PrintMod("Compressing file: " + f, ".compress.tar", Msg.LogLevels.Verbose);
									tar.Write(f, file);
								}
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
			/// Compress a single file into a tar file.
			/// </summary>
			/// <param name="filePath">File to be compressed</param>
			/// <param name="outputFile">Output tar file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			///	<param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise (see LastError).</returns>
			public static bool CompressFile(string filePath, string outputFile,bool overwrite = true,TarCompressionType compressionType = TarCompressionType.Gzip) {
				LastError = ApiError.None;
				Msg.PrintMod("Compressing file: " + filePath, ".compress.tar", Msg.LogLevels.Verbose);
				if (!Files.Exists(filePath))
					return Fail(ErrorCode.NotFound, "The file to compress does not exist", filePath);
				if (!MapCompression(compressionType, outputFile, out CompressionType compType))
					return false;
				if (!PrepareOutput(outputFile, overwrite))
					return false;
				try {
					using (var fs = new FileStream(outputFile, FileMode.Create)) {
						using (var tar = new TarWriter(fs, new TarWriterOptions(compType, true))) {
							string f2 = Path.GetFileName(filePath);
							tar.Write(f2, filePath);
						}
					}
					return true;
				} catch (System.Exception ex) {
					RemovePartial(outputFile);
					return Fail(ex, outputFile);
				}
			}

			/// <summary>
			/// Safe-extraction boundary check. Mirrors the guard SharpCompress applies to
			/// WriteEntryToDirectory: an entry whose composed path resolves outside the destination
			/// folder (archive path traversal, "zip-slip") is rejected. Used for the manual-path
			/// branches (long @PaxHeader names and directory entries) that bypass the library guard.
			/// </summary>
			/// <param name="outputFolder">Destination root folder.</param>
			/// <param name="candidatePath">Composed target path to validate.</param>
			/// <returns>True if candidatePath resolves inside outputFolder, false otherwise.</returns>
			private static bool IsInsideOutputFolder(string outputFolder, string candidatePath) {
				string root = Path.GetFullPath(outputFolder);
				if (!root.EndsWith(Path.DirectorySeparatorChar) && !root.EndsWith(Path.AltDirectorySeparatorChar))
					root += Path.DirectorySeparatorChar;
				string full = Path.GetFullPath(candidatePath);
				StringComparison cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				if (full.StartsWith(root, cmp))
					return true;
				return string.Equals(full, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), cmp);
			}

			/// <summary>
			/// Decompress a tar file into a folder. The folder is created if needed.
			/// Entries that try to escape the destination folder (path traversal) are refused, and any
			/// refused or failed entry makes the call return false with LastError set to Failed; the
			/// remaining entries are still extracted.
			/// </summary>
			/// <param name="tarPath">Tar file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">If archive(s) should be overwritten, default true</param>
			/// <returns>True if every entry was extracted, false otherwise (see LastError).</returns>
			public static bool Decompress(string tarPath, string outputFolder,bool overwrite = true) {
				LastError = ApiError.None;
				Msg.PrintMod("Decompressing file: " + tarPath, ".compress.tar", Msg.LogLevels.Verbose);
				if (!Files.Exists(tarPath))
					return Fail(ErrorCode.NotFound, "The archive to decompress does not exist", tarPath);
				// The extraction library needs the destination folder to exist
				if (!Folders.Create(outputFolder))
					return Fail(ErrorCode.IoError, "The destination folder could not be created: " + Folders.LastError.Message, outputFolder);
				int failed = 0;
				int refused = 0;
				try {
					using (var fs = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
						ReaderOptions r = new SharpCompress.Readers.ReaderOptions();
						using (var tar = ReaderFactory.OpenReader(fs,r)) {
							ExtractionOptions exOp = new ExtractionOptions() { ExtractFullPath = true, Overwrite = overwrite };
							exOp.SymbolicLinkHandler = (sender, e) => {
								Msg.PrintMod("Symbolic Links not supported: " + e, ".compress.tar", Msg.LogLevels.Verbose);
							};
							string nextFileName = string.Empty;
							while (tar.MoveToNextEntry()) {
								// Skip pax global header
								if  ( (tar.Entry == null) || (tar.Entry.Key == null))
									continue;
								if (tar.Entry.Key.Contains("pax_global_header"))
									continue;
								// @PaxHeader contains attributes and also the filename is its bigger than 100 chars
								// It may contain multiple file names just to create empty folders
								if (tar.Entry.Key.Contains("@PaxHeader")) {
									// Stream the file to memory stream
									using (MemoryStream ms = new MemoryStream()) {
										tar.WriteEntryTo(ms);
										ms.Position = 0;
										// Read the memory stream
										using (StreamReader? sr = new StreamReader(ms)) {
											string? line;
											while ((line = sr.ReadLine()) != null) {
												if (line.Contains("path=")) {
													nextFileName = line.Substring(line.IndexOf("path=") + 5);
													if (nextFileName.EndsWith("/")) {
														// Is a empty folder. Create it
														string? folder = Path.GetDirectoryName(outputFolder + Path.DirectorySeparatorChar + nextFileName);
														if (folder != null)
															Folders.Create(folder);
														nextFileName = string.Empty;
													}
												}
											}
											sr.Close();
										}
										ms.Close();
									}
									continue;
								}
								try {
									if (!tar.Entry.IsDirectory) {
										// Beware, check the existence of a defined filename but also if it matches the entry
										if ( (nextFileName != string.Empty) && (nextFileName.StartsWith(tar.Entry.Key) ) ) {
											string longTarget = outputFolder + Path.DirectorySeparatorChar + nextFileName;
											// Safe-extract: refuse an entry whose (long) name escapes the destination folder (path traversal / zip-slip).
											if (!IsInsideOutputFolder(outputFolder, longTarget)) {
												Msg.PrintWarningMod("Refusing entry outside destination (path traversal): " + nextFileName, ".compress.tar", Msg.LogLevels.Verbose);
												refused++;
												nextFileName = string.Empty;
												continue;
											}
											Msg.PrintMod("Unpacking file (long): " + nextFileName, ".compress.tar", Msg.LogLevels.Verbose);
											string? folder = Path.GetDirectoryName(longTarget);
											if (folder != null)
												Folders.Create(folder);
											tar.WriteEntryToFile(longTarget, exOp);
											nextFileName = string.Empty;
										} else {
											Msg.PrintMod("Unpacking file: " + tar.Entry.Key, ".compress.tar", Msg.LogLevels.Verbose);
											tar.WriteEntryToDirectory(outputFolder, exOp);
										}
									} else {
										// If its a folder, just create it
										string dirTarget = outputFolder + Path.DirectorySeparatorChar + tar.Entry.Key;
										// Safe-extract: refuse a directory entry that escapes the destination folder.
										if (!IsInsideOutputFolder(outputFolder, dirTarget)) {
											Msg.PrintWarningMod("Refusing directory entry outside destination (path traversal): " + tar.Entry.Key, ".compress.tar", Msg.LogLevels.Verbose);
											refused++;
											continue;
										}
										string? folder = Path.GetDirectoryName(dirTarget);
										if (folder != null)
											Folders.Create(folder);
									}
								} catch (System.Exception ex) {
									// The library refuses traversal entries on its own path as well
									Msg.PrintWarningMod("Entry not extracted: " + tar.Entry.Key + " " + ex.Message, ".compress.tar", Msg.LogLevels.Verbose);
									failed++;
								}
							}
						}
					}
				} catch (System.Exception ex) {
					return Fail(ex, tarPath);
				}
				if (refused > 0 || failed > 0) {
					string reason = refused > 0 ? refused + " entries refused (path traversal)" : string.Empty;
					if (failed > 0)
						reason += (reason.Length > 0 ? ", " : string.Empty) + failed + " entries not extracted";
					return Fail(ErrorCode.Failed, reason, tarPath);
				}
				return true;
			}
		}
	}
}
