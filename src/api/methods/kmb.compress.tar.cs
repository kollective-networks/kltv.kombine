/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

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
		/// Tar compression methods
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
				/// .tar.xz extension
				/// </summary>
				Lzma2,
			}

			/// <summary>
			/// Compress a single folder into a tar file.
			/// </summary>
			/// <param name="folderPath">Folder to be compressed</param>
			/// <param name="outputFile">Output tar file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="includeFolder">If true, include the folder in the tar file.</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFolder(string folderPath, string outputFile,bool overwrite = true,bool includeFolder = true, TarCompressionType compressionType = TarCompressionType.Gzip) {
				return CompressFolders(new string[] { folderPath }, outputFile,overwrite,includeFolder,compressionType);
			}

			/// <summary>
			/// Compress a list of folders into a tar file.
			/// </summary>
			/// <param name="folderPaths">Folders to be compressed</param>
			/// <param name="outputFile">Output tar file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="includeFolder">If true, include the given folders in the tar file and not only the folder contents an descentants.</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true, bool includeFolder = true, TarCompressionType compressionType = TarCompressionType.Gzip) {
				Msg.PrintMod("Compressing folders: " + string.Join(", ", folderPaths), ".compress", Msg.LogLevels.Verbose);
				// Check if the file exists and delete it if it does
				if (overwrite == true && Files.Exists(outputFile)) {
					Msg.PrintMod("Overwriting file: " + outputFile, ".compress", Msg.LogLevels.Verbose);
					Files.Delete(outputFile);
				}
				try {
					CompressionType compType = CompressionType.None;
					if (compressionType == TarCompressionType.None)
						compType = CompressionType.None;
					if (compressionType == TarCompressionType.Gzip)
						compType = CompressionType.GZip;
					if (compressionType == TarCompressionType.Bzip2)
						compType = CompressionType.BZip2;
					if (compressionType == TarCompressionType.Lzma)
						compType = CompressionType.LZip;
					if (compressionType == TarCompressionType.Lzma2) {
						Msg.PrintAndAbortMod("Lzma2 (.tar.xz) compression not supported", ".compress", Msg.LogLevels.Normal);
					}
					using (var fs = new FileStream(outputFile, FileMode.Create)) {
						using (var tar = new TarWriter(fs, new TarWriterOptions(compType, true))) {

							foreach (string folder in folderPaths) {
								string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
								foreach (string file in files) {
									string f;
									if (includeFolder == true) {
										f = file;
									} else {
										f = Path.GetRelativePath(folder, file);
									}
									Msg.PrintMod("Compressing file: " + f, ".compress", Msg.LogLevels.Verbose);
									tar.Write(f, file);
								}
							}
						}
					}
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar compressing folders: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					return false;
				}
			}

			/// <summary>
			/// Compress a single file into a tar file.
			/// </summary>
			/// <param name="filePath">File to be compressed</param>
			/// <param name="outputFile">Output tar file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			///	<param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFile(string filePath, string outputFile,bool overwrite = true,TarCompressionType compressionType = TarCompressionType.Gzip) {
				Msg.PrintMod("Compressing file: " + filePath, ".compress", Msg.LogLevels.Verbose);
				// Check if the file exists and delete it if it does
				if (overwrite == true && Files.Exists(outputFile)) {
					Msg.PrintMod("Overwriting file: " + outputFile, ".compress", Msg.LogLevels.Verbose);
					Files.Delete(outputFile);
				}
				try {
					CompressionType compType = CompressionType.None;
					if (compressionType == TarCompressionType.None)
						compType = CompressionType.None;
					if (compressionType == TarCompressionType.Gzip)
						compType = CompressionType.GZip;
					if (compressionType == TarCompressionType.Bzip2)
						compType = CompressionType.BZip2;
					if (compressionType == TarCompressionType.Lzma)
						compType = CompressionType.LZip;
					if (compressionType == TarCompressionType.Lzma2) {
						Msg.PrintAndAbortMod("Lzma2 (.tar.xz) compression not supported", ".compress", Msg.LogLevels.Normal);
					}
					using (var fs = new FileStream(outputFile, FileMode.Create)) {
						using (var tar = new TarWriter(fs, new TarWriterOptions(compType, true))) {
							string f2 = Path.GetFileName(filePath);
							tar.Write(f2, filePath);
						}
					}
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar compressing folders: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					return false;
				}
			}

			/// <summary>
			/// Decompress a tar file into a folder.
			/// </summary>
			/// <param name="tarPath">Tar file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">If archive(s) should be overwriten, default true</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool Decompress(string tarPath, string outputFolder,bool overwrite = true) {
				Msg.PrintMod("Decompressing file: " + tarPath, ".compress", Msg.LogLevels.Verbose);
				// Check if the file exists and delete it if it does
				try {
					using (var fs = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
						ReaderOptions r = new SharpCompress.Readers.ReaderOptions();
						using (var tar = ReaderFactory.Open(fs,r)) {
							ExtractionOptions exOp = new ExtractionOptions() { ExtractFullPath = true, Overwrite = overwrite };
							exOp.WriteSymbolicLink += (sender, e) => {
								Msg.PrintMod("Symbolic Links not supported: " + e, ".compress", Msg.LogLevels.Verbose);
							};
							string nextFileName = string.Empty;
							while (tar.MoveToNextEntry()) {
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
											Msg.PrintMod("Unpacking file (long): " + nextFileName, ".compress", Msg.LogLevels.Verbose);
											string? folder = Path.GetDirectoryName(outputFolder + Path.DirectorySeparatorChar + nextFileName);
											if (folder != null)
												Folders.Create(folder);
											tar.WriteEntryToFile(outputFolder + Path.DirectorySeparatorChar + nextFileName, exOp);
											nextFileName = string.Empty;
										} else {
											Msg.PrintMod("Unpacking file: " + tar.Entry.Key, ".compress", Msg.LogLevels.Verbose);
											tar.WriteEntryToDirectory(outputFolder, exOp);
										}
									} else {
										// If its a folder, just create it
										string? folder = Path.GetDirectoryName(outputFolder + Path.DirectorySeparatorChar + tar.Entry.Key);
										if (folder != null)
											Folders.Create(folder);
									}
								} catch (System.Exception ex) {
									Msg.PrintErrorMod("Error tar decompressing file: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
								}
							}
						}
					}
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar decompressing archive: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					return false;
				}				
			}
		}
	}
}