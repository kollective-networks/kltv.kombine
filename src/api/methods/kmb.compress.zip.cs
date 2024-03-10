/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using ICSharpCode.SharpZipLib.BZip2;


namespace Kltv.Kombine.Api {

	/// <summary>
	/// Compress methods
	/// </summary>
	public static partial class Compress{

		/// <summary>
		///  Zip compression methods
		/// </summary>
		public static class Zip {
			/// <summary>
			/// Compress a single folder into a zip file.
			/// The folder will be compressed into the root of the zip file.
			/// </summary>
			/// <param name="folderPath">Folder to compress</param>
			/// <param name="outputFile">Output zip file</param>
			/// <param name="overwrite">If true, overwrite the output file if it exists.</param>
			/// <param name="includeFolder">If true, include the folder in the zip file.</param>
			/// <returns>True if operation was okey. False otherwise</returns>
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
			/// <returns>True if operation was okey. False otherwise</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile,bool overwrite = true,bool includeFolder = true) {
				// Check if the file exists and delete it if it does
				// 
				if (overwrite == true && Files.Exists(outputFile)){
					Files.Delete(outputFile);
				}
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
					Msg.PrintErrorMod("Error zip compressing folders: "+ex.Message,".compress",Msg.LogLevels.Verbose);
					return false;
				}
			}

			/// <summary>
			/// Compress a single file into a zip file.
			/// </summary>
			/// <param name="filePath">File to be compressed</param>
			/// <param name="outputFile">Output zip file</param>
			/// <param name="overwrite">True if file should be overwritten, default true.</param>
			/// <returns>True if the operation was fine, false otherwise</returns>
			public static bool CompressFile(string filePath, string outputFile,bool overwrite = true) {
				// Check if the file exists and delete it if it does
				// 
				if (overwrite == true && Files.Exists(outputFile)){
					Files.Delete(outputFile);
				}
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
					Msg.PrintErrorMod("Error zip compressing file: "+ex.Message,".compress",Msg.LogLevels.Verbose);
					return false;
				}
			}

			/// <summary>
			/// Decompress a zip file into a folder.
			/// </summary>
			/// <param name="zipPath">Zip file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">True if file should be overwritten, default true.</param>
			/// <returns>True if operation was okey. False otherwise</returns>
			public static bool Decompress(string zipPath, string outputFolder,bool overwrite = true) {
				try {
					ZipFile.ExtractToDirectory(zipPath, outputFolder,overwrite);
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error zip decompressing folders: "+ex.Message,".compress",Msg.LogLevels.Verbose);
					return false;
				}
			}
		}
	}
}