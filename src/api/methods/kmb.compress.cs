/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Types;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;

namespace Kltv.Kombine.Api {

	public static class Compress{

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

		/// <summary>
		/// Tar compression methods
		/// </summary>
		public static class Tar {

			/// <summary>
			/// Compress a single folder into a tar.gz file.
			/// </summary>
			/// <param name="folderPath">Folder to be compressed</param>
			/// <param name="outputFile">Output tar.gz file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="includeFolder">If true, include the folder in the tar.gz file.</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFolder(string folderPath, string outputFile,bool overwrite = true,bool includeFolder = true) {
				return CompressFolders(new string[] { folderPath }, outputFile,overwrite);
			}

			/// <summary>
			/// Compress a list of folders into a tar.gz file.
			/// </summary>
			/// <param name="folderPaths">Folders to be compressed</param>
			/// <param name="outputFile">Output tar.gz file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="includeFolder">If true, include the folders in the tar.gz file.</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true,bool includeFolder = true) {
				// Check if the file exists and delete it if it does
				if (overwrite == true && Files.Exists(outputFile)) {
					Files.Delete(outputFile);
				}
				// Create the tar.gz file
				try	{
					using (FileStream fs = new FileStream(outputFile, FileMode.Create))
					using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress)) 
					using (TarWriter tar = new TarWriter(gz)){
						foreach(string folder in folderPaths) {
							string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
							// It creates entries for folders even if the folder is empty
							if (includeFolder == true)
								tar.WriteEntry(folder, folder);
							foreach (string file in files) {
								string f;
								if (includeFolder == true){
									f = Path.GetRelativePath(folder, file);
								} else{
									f = file;
								}
								// Important: In windows backslashes are used as path separator, but tar files use forward slashes
								string f2 = f.Replace("\\", "/");	
								tar.WriteEntry(file, f2);
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
			/// Compress a single file into a tar.gz file.
			/// </summary>
			/// <param name="filePath">File to be compressed</param>
			/// <param name="outputFile">Output tar.gz file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFile(string filePath, string outputFile,bool overwrite = true) {
				// Check if the file exists and delete it if it does
				if (overwrite == true && Files.Exists(outputFile)) {
					Files.Delete(outputFile);
				}
				// Create the tar.gz file
				try	{
					using (FileStream fs = new FileStream(outputFile, FileMode.Create))
					using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress)) 
					using (TarWriter tar = new TarWriter(gz)){
						// Important: In windows backslashes are used as path separator, but tar files use forward slashes
						string f2 = Path.GetFileName(filePath);	
						tar.WriteEntry(filePath, f2);
					}
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar compressing file: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					return false;
				}
			}

			/// <summary>
			/// Decompress a tar.gz file into a folder.
			/// </summary>
			/// <param name="tarPath">Tar.gz file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool Decompress(string tarPath, string outputFolder,bool overwrite = true) {
				try {
					using (FileStream fs = new FileStream(tarPath, FileMode.Open))
					using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
						TarFile.ExtractToDirectory(gz,outputFolder,overwrite);
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar decompressing folders: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					return false;
				}
			}
		}
	}
}