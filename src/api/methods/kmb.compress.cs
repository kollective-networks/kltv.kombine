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

			public enum TarCompressionType {
				Gzip,
				Bzip2
			}

			/// <summary>
			/// Compress a single folder into a tar.gz file.
			/// </summary>
			/// <param name="folderPath">Folder to be compressed</param>
			/// <param name="outputFile">Output tar.gz file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="includeFolder">If true, include the folder in the tar.gz file.</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFolder(string folderPath, string outputFile,bool overwrite = true,bool includeFolder = true, TarCompressionType compressionType = TarCompressionType.Gzip) {
				return CompressFolders(new string[] { folderPath }, outputFile,overwrite,includeFolder,compressionType);
			}

			/// <summary>
			/// Compress a list of folders into a tar.gz file.
			/// </summary>
			/// <param name="folderPaths">Folders to be compressed</param>
			/// <param name="outputFile">Output tar.gz file</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="includeFolder">If true, include the given folders in the tar.gz file and not only the folder contents an descentants.</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool CompressFolders(string[] folderPaths, string outputFile, bool overwrite = true,bool includeFolder = true, TarCompressionType compressionType = TarCompressionType.Gzip) {
				Msg.PrintMod("Compressing folders: " + string.Join(", ", folderPaths), ".compress", Msg.LogLevels.Verbose);
				// Check if the file exists and delete it if it does
				if (overwrite == true && Files.Exists(outputFile)) {
					Msg.PrintMod("Overwriting file: " + outputFile, ".compress", Msg.LogLevels.Verbose);
					Files.Delete(outputFile);
				}
				// Create the tar.gz file
				Stream? gz = null; 
				FileStream? fs = null;
				try	{
					fs = new FileStream(outputFile, FileMode.Create);
					if (compressionType == TarCompressionType.Gzip)
						gz = new GZipStream(fs, CompressionMode.Compress) as Stream;
					if (compressionType == TarCompressionType.Bzip2)
						gz = new BZip2OutputStream(fs) as Stream;
					if (gz == null){
						Msg.PrintErrorMod("Error tar compressing folders: Unknown compression type", ".compress", Msg.LogLevels.Verbose);
						if (fs != null){
							fs.Close();
							fs.Dispose();
						}
						return false;
					}
					using (TarWriter tar = new TarWriter(gz)){
						foreach(string folder in folderPaths) {
							string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
							// It creates entries for folders even if the folder is empty
							if (includeFolder == true)
								tar.WriteEntry(folder, folder);
							foreach (string file in files) {
								string f;
								if (includeFolder == true){
									f = file;
								} else{
									f = Path.GetRelativePath(folder, file);
								}
								// Important: In windows backslashes are used as path separator, but tar files use forward slashes
								string f2 = f.Replace("\\", "/");	
								Msg.PrintMod("Compressing file: " + f2, ".compress", Msg.LogLevels.Verbose);
								tar.WriteEntry(file, f2);
							}					
						}
					}
					gz.Close();
					gz.Dispose();
					fs.Close();
					fs.Dispose();
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar compressing folders: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					if (gz != null) {
						gz.Close();
						gz.Dispose();
					}
					if (fs != null) {
						fs.Close();
						fs.Dispose();
					}					
					return false;
				}
			}

			/// <summary>
			/// Compress a single file into a tar.gz file.
			/// </summary>
			/// <param name="filePath">File to be compressed</param>
			/// <param name="outputFile">Output tar.gz file</param>
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
				// Create the tar.gz file
				FileStream? fs = null;
				Stream? gz = null;
				try	{
					fs = new FileStream(outputFile, FileMode.Create);
					if (compressionType == TarCompressionType.Gzip)
						gz = new GZipStream(fs, CompressionMode.Compress) as Stream;
					if (compressionType == TarCompressionType.Bzip2)
						gz = new BZip2OutputStream(fs) as Stream;
					if (gz == null){
						Msg.PrintErrorMod("Error tar compressing folders: Unknown compression type", ".compress", Msg.LogLevels.Verbose);
						if (fs != null){
							fs.Close();
							fs.Dispose();
						}
						return false;
					}					
					using (TarWriter tar = new TarWriter(gz)){
						// Important: In windows backslashes are used as path separator, but tar files use forward slashes
						string f2 = Path.GetFileName(filePath);	
						Msg.PrintMod("Compressing file: " + f2, ".compress", Msg.LogLevels.Verbose);
						tar.WriteEntry(filePath, f2);
					}
					gz.Close();
					gz.Dispose();
					fs.Close();
					fs.Dispose();
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar compressing file: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					if (gz != null) {
						gz.Close();
						gz.Dispose();
					}
					if (fs != null) {
						fs.Close();
						fs.Dispose();
					}					
					return false;
				}
			}

			/// <summary>
			/// Decompress a tar.gz file into a folder.
			/// </summary>
			/// <param name="tarPath">Tar.gz file to decompress</param>
			/// <param name="outputFolder">Output folder</param>
			/// <param name="overwrite">If archive should be overwriten, default true</param>
			/// <param name="compressionType">Compression type, default gzip</param>
			/// <returns>True if fine, false otherwise.</returns>
			public static bool Decompress(string tarPath, string outputFolder,bool overwrite = true,TarCompressionType compressionType = TarCompressionType.Gzip) {
				// Create the tar.gz file
				FileStream? fs = null;
				Stream? gz = null;			
				try {
					//using (FileStream fs = new FileStream(tarPath, FileMode.Open))
					//using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))

					fs = new FileStream(tarPath, FileMode.Open);
					if (compressionType == TarCompressionType.Gzip)
						gz = new GZipStream(fs, CompressionMode.Decompress) as Stream;
					if (compressionType == TarCompressionType.Bzip2)
						gz = new BZip2InputStream(fs) as Stream;
					if (gz == null){
						Msg.PrintErrorMod("Error tar decompressing file: Unknown compression type", ".compress", Msg.LogLevels.Verbose);
						if (fs != null){
							fs.Close();
							fs.Dispose();
						}
						return false;
					}						
					TarFile.ExtractToDirectory(gz,outputFolder,overwrite);
					gz.Close();
					gz.Dispose();
					fs.Close();
					fs.Dispose();					
					return true;
				} catch (System.Exception ex) {
					Msg.PrintErrorMod("Error tar decompressing file: " + ex.Message, ".compress", Msg.LogLevels.Verbose);
					if (gz != null) {
						gz.Close();
						gz.Dispose();
					}
					if (fs != null) {
						fs.Close();
						fs.Dispose();
					}							
					return false;
				}
			}
		}
	}
}