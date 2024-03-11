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

						using (var tar = ReaderFactory.Open(fs, new SharpCompress.Readers.ReaderOptions())) {
							// Check ExtractionOptions (can preserve mod time, attributes, etc)
							tar.WriteAllToDirectory(outputFolder, new ExtractionOptions() { ExtractFullPath = true, Overwrite = overwrite });
						}
					}
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar compressing folders: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					return false;
				}				
			}
		}
	}
}