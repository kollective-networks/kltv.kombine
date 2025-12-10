## mkb ##
# T:Kltv.Kombine.Api.Args
 Simplify the access to the action arguments (current script) 

##### P:Kltv.Kombine.Api.Args.WasRebuilded
 Returns if the script or parent was rebuilded 

##### M:Kltv.Kombine.Api.Args.Contains(System.String)
 Returns if the value is pressent in the action arguments 
- arg: Argument to test<br>
Returns: True if was pressent, false otherwise.


##### M:Kltv.Kombine.Api.Args.Get(System.Int32)
 Returns the value of the argument at the given index. 
- index: Index to retrieve<br>
Returns: Argument or empty if out of bounds of the array.


# T:Kltv.Kombine.Api.EntryArgs
 Simplify the access to the action arguments (starting script) 

##### M:Kltv.Kombine.Api.EntryArgs.Contains(System.String)
 Returns if the value is pressent in the action arguments 
- arg: Argument to test<br>
Returns: True if was pressent, false otherwise.


##### M:Kltv.Kombine.Api.EntryArgs.Get(System.Int32)
 Returns the value of the argument at the given index. 
- index: Index to retrieve<br>
Returns: Argument or empty if out of bounds of the array.


# T:Kltv.Kombine.Api.Compress
 Compress methods 

# T:Kltv.Kombine.Api.Compress.Tar
 Tar compression methods 

# T:Kltv.Kombine.Api.Compress.Tar.TarCompressionType
 Tar compression types 

##### F:Kltv.Kombine.Api.Compress.Tar.TarCompressionType.None
 No compression, just tar file 

##### F:Kltv.Kombine.Api.Compress.Tar.TarCompressionType.Gzip
 .tar.gz extension 

##### F:Kltv.Kombine.Api.Compress.Tar.TarCompressionType.Bzip2
 .tar.bz2 extension 

##### F:Kltv.Kombine.Api.Compress.Tar.TarCompressionType.Lzma
 .tar.lz extension 

##### F:Kltv.Kombine.Api.Compress.Tar.TarCompressionType.Lzma2
 .tar.xz extension 

##### M:Kltv.Kombine.Api.Compress.Tar.CompressFolder(System.String,System.String,System.Boolean,System.Boolean,Kltv.Kombine.Api.Compress.Tar.TarCompressionType)
 Compress a single folder into a tar file. 
- folderPath: Folder to be compressed<br>
- outputFile: Output tar file<br>
- overwrite: If archive should be overwritten, default true<br>
- includeFolder: If true, include the folder in the tar file.<br>
- compressionType: Compression type, default gzip<br>
Returns: True if fine, false otherwise.


##### M:Kltv.Kombine.Api.Compress.Tar.CompressFolders(System.String[],System.String,System.Boolean,System.Boolean,Kltv.Kombine.Api.Compress.Tar.TarCompressionType)
 Compress a list of folders into a tar file. 
- folderPaths: Folders to be compressed<br>
- outputFile: Output tar file<br>
- overwrite: If archive should be overwritten, default true<br>
- includeFolder: If true, include the given folders in the tar file and not only the folder contents an descentants.<br>
- compressionType: Compression type, default gzip<br>
Returns: True if fine, false otherwise.


##### M:Kltv.Kombine.Api.Compress.Tar.CompressFile(System.String,System.String,System.Boolean,Kltv.Kombine.Api.Compress.Tar.TarCompressionType)
 Compress a single file into a tar file. 
- filePath: File to be compressed<br>
- outputFile: Output tar file<br>
- overwrite: If archive should be overwriten, default true<br>
- compressionType: Compression type, default gzip<br>
Returns: True if fine, false otherwise.


##### M:Kltv.Kombine.Api.Compress.Tar.Decompress(System.String,System.String,System.Boolean)
 Decompress a tar file into a folder. 
- tarPath: Tar file to decompress<br>
- outputFolder: Output folder<br>
- overwrite: If archive(s) should be overwritten, default true<br>
Returns: True if fine, false otherwise.


# T:Kltv.Kombine.Api.Compress.Zip
 Zip compression methods 

##### M:Kltv.Kombine.Api.Compress.Zip.CompressFolder(System.String,System.String,System.Boolean,System.Boolean)
 Compress a single folder into a zip file. The folder will be compressed into the root of the zip file. 
- folderPath: Folder to compress<br>
- outputFile: Output zip file<br>
- overwrite: If true, overwrite the output file if it exists.<br>
- includeFolder: If true, include the folder in the zip file.<br>
Returns: True if operation was okey. False otherwise


##### M:Kltv.Kombine.Api.Compress.Zip.CompressFolders(System.String[],System.String,System.Boolean,System.Boolean)
 Compress a list of folders into a zip file. The folders will be compressed into the root of the zip file. 
- folderPaths: Folders to compress<br>
- outputFile: Output zip file<br>
- overwrite: If true, overwrite the output file if it exists.<br>
- includeFolder: If true, include the folders in the zip file.<br>
Returns: True if operation was okey. False otherwise


##### M:Kltv.Kombine.Api.Compress.Zip.CompressFile(System.String,System.String,System.Boolean)
 Compress a single file into a zip file. 
- filePath: File to be compressed<br>
- outputFile: Output zip file<br>
- overwrite: True if file should be overwritten, default true.<br>
Returns: True if the operation was fine, false otherwise


##### M:Kltv.Kombine.Api.Compress.Zip.Decompress(System.String,System.String,System.Boolean)
 Decompress a zip file into a folder. 
- zipPath: Zip file to decompress<br>
- outputFolder: Output folder<br>
- overwrite: True if file should be overwritten, default true.<br>
Returns: True if operation was okey. False otherwise


# T:Kltv.Kombine.Api.Files
 All the file related functionality 

##### M:Kltv.Kombine.Api.Files.Exists(Kltv.Kombine.Types.KValue)
 Check if the file exists 
- Filename: Filename to check<br>
Returns: True if exists, false otherwise


##### M:Kltv.Kombine.Api.Files.ReadTextFile(Kltv.Kombine.Types.KValue,System.Boolean)
 Read a text file into a single KValue 
- Filename: Filename to be fetched<br>
- ExitIfError: If the script should exit if the file is missing / trigers error. Default false<br>
Returns: A KValue with the file contents


##### M:Kltv.Kombine.Api.Files.WriteTextFile(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue,System.Boolean)
 Writes a text file with the given content 
- Filename: Filename to use<br>
- Contents: Contents to be written<br>
- ExitIfError: If it should automaticallty quit on error, default false.<br>
Returns: Returns true if okey, false otherwise.


##### M:Kltv.Kombine.Api.Files.GetModifiedTime(Kltv.Kombine.Types.KValue)
 Returns the modification time of the given file 
- Filename: File to inspect<br>
Returns: The modified time in UTC zone in seconds.


##### M:Kltv.Kombine.Api.Files.Rename(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Renames a file. 
- oldFilename: The current name of the file.<br>
- newFilename: The new name for the file.<br>
Returns: True if the file was successfully renamed, false otherwise.


##### M:Kltv.Kombine.Api.Files.Move(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Move a file from one location to another. 
- oldFilename: Current filename.<br>
- newFilename: New filename and location.<br>
Returns: True if okey, false othewise


##### M:Kltv.Kombine.Api.Files.Delete(Kltv.Kombine.Types.KValue)
 Deletes a file. 
- Filename: File to be deleted.<br>
Returns: True if okey, false otherwise


##### M:Kltv.Kombine.Api.Files.GetFileSize(Kltv.Kombine.Types.KValue)
 Returns the size of a given file. 
- Filename: Filename.<br>
Returns: The filesize or -1 if invalid.


##### M:Kltv.Kombine.Api.Files.Copy(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue,System.Boolean)
 Copy a file. 
- source: Source file.<br>
- destination: Destination file.<br>
- newerOnly: Copy only if source is newer, default true.<br>
Returns: true if the copy was okey or the file on destination is newer, false otherwise.


# T:Kltv.Kombine.Api.Files.CompareOptions
 File Comparing Options 

##### M:Kltv.Kombine.Api.Files.Compare(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue,Kltv.Kombine.Api.Files.CompareOptions)
 Compare two files using the comparisson specified in Options 
- first: First file to compare<br>
- second: Second file to compare<br>
- Options: Comparing options. By default is compared by size.<br>
Returns: True if both files are equal. False otherwise


# T:Kltv.Kombine.Api.Folders
 It wraps several folder methods to simplify use and check / add case sensitive file system support 

##### M:Kltv.Kombine.Api.Folders.Exists(Kltv.Kombine.Types.KValue)
 Check if a folder exists 
- folder: Folder to be checked.<br>
Returns: True if exists, false otherwise.


##### M:Kltv.Kombine.Api.Folders.Move(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Move a folder from source to destination 
- src: source path for the folder<br>
- dst: destination path for the folder<br>
Returns: 


##### M:Kltv.Kombine.Api.Folders.Create(Kltv.Kombine.Types.KValue)
 Create a single folder 
- folder: Folder to be created.<br>
Returns: True if okey. False otherwise.


##### M:Kltv.Kombine.Api.Folders.Create(System.String)
 Create a single folder 
- folder: Folder to be created.<br>
Returns: True if okey. False otherwise.


##### M:Kltv.Kombine.Api.Folders.Create(Kltv.Kombine.Types.KList)
 Create a list of folders. 
- folders: List of folders to be created.<br>
Returns: If any folder failed to be created it returns false. True pennywise. 


##### M:Kltv.Kombine.Api.Folders.Create(System.String[])
 Create a list of folders. 
- folders: List of folders to be created.<br>
Returns: If any folder failed to be created it returns false. True otherwise.


##### M:Kltv.Kombine.Api.Folders.Delete(Kltv.Kombine.Types.KList,System.Boolean)
 Delete a list of folders. 
- folders: Folder list to be deleted.<br>
- recurse: If the subfolders should be deleted as well. Default false<br>
Returns: 


##### M:Kltv.Kombine.Api.Folders.Delete(Kltv.Kombine.Types.KValue,System.Boolean)
 Delete a folder and optionally its subfolders 
- folder: Folder to delete<br>
- recurse: If subfolders should be deleted as well. Default false.<br>
Returns: 


##### M:Kltv.Kombine.Api.Folders.Delete(System.String,System.Boolean)
 Delete a folder and optionally its subfolders 
- folder: Folder to delete.<br>
- DeleteSubFolders: If subfolders should be deleted as well. Default false.<br>
Returns: 


##### P:Kltv.Kombine.Api.Folders.CurrentWorkingFolder
 Fileitem with the current working folder 

##### F:Kltv.Kombine.Api.Folders.folderStack
 Folder stack to push/pop current folder for cwd jumping operations 

##### M:Kltv.Kombine.Api.Folders.CurrentFolderPush
 Push the current working folder into the stack 

##### M:Kltv.Kombine.Api.Folders.CurrentFolderPop
 Pop a folder from the stack 

##### M:Kltv.Kombine.Api.Folders.SetCurrentFolder(System.String,System.Boolean)
 Sets a new working folder specified by CWD and optionally pushes the current one into the stack 
- CWD: New working folder<br>
- PushCurrent: If the current one should be saved<br>

##### M:Kltv.Kombine.Api.Folders.GetCurrentFolder
 Returns the current working folder 
Returns: An string with the current working folder.


##### P:Kltv.Kombine.Api.Folders.CurrentToolFolder
 Kombine Binary Folder 

##### P:Kltv.Kombine.Api.Folders.CurrentScriptFolder
 Returns the current script folder 

##### P:Kltv.Kombine.Api.Folders.ParentScriptFolder
 Returns the parent script folder if any or empty if none. 

# T:Kltv.Kombine.Api.Folders.FolderPair


##### P:Kltv.Kombine.Api.Folders.FolderPair.Source


##### P:Kltv.Kombine.Api.Folders.FolderPair.Target


##### M:Kltv.Kombine.Api.Folders.FolderPair.#ctor(System.String,System.String)

- source: <br>
- target: <br>

# T:Kltv.Kombine.Api.Folders.CopyOptions
 Copy options for folder copy operation 

##### F:Kltv.Kombine.Api.Folders.CopyOptions.Default


##### F:Kltv.Kombine.Api.Folders.CopyOptions.IncludeSubFolders
 Include subfolders when copying 

##### F:Kltv.Kombine.Api.Folders.CopyOptions.OnlyModifiedFiles
 Only copy modified files 

##### F:Kltv.Kombine.Api.Folders.CopyOptions.ShowProgress
 Show progress during copy operation 

##### F:Kltv.Kombine.Api.Folders.CopyOptions.OnlyFolders
 Copy only folders, not files 

##### F:Kltv.Kombine.Api.Folders.CopyOptions.DeleteMissingFiles
 Delete missing files in target that are not present in source (mirror copy) 

##### M:Kltv.Kombine.Api.Folders.Copy(System.String,System.String,Kltv.Kombine.Api.Folders.CopyOptions,System.String)
 Copies a folder from source to target with several options 
- Source: Source folder (required)<br>
- Target: Destination folder (required)<br>
- Options: Options to copy (optional)<br>
- FileMask: File mask to be used in the copy operation (optional)<br>
Returns: 


##### M:Kltv.Kombine.Api.Folders.SearchBackPath(System.String)
 Search for a filename (which may have relative paht) in the script folder and backwards If the script folder cannot be resolve, it will take the current working folder 
- filename: The filename to search for (may include relative path)<br>
Returns: The result or empty if nothing found.


##### M:Kltv.Kombine.Api.Folders.SearchForwardPath(System.String)
 Search for a filename (which may have relative paht) in the script folder and forwards If the script folder cannot be resolve, it will take the current working folder 
- filename: The filename to search for (may include relative paths)<br>
Returns: The result or empty if nothing found.


##### M:Kltv.Kombine.Api.Folders.ResolveFilename(System.String)
 Resolve a filename by the given order. Absolute path Relative path from current working directory Relative path from script directory Relative path from forward trace Relative path from backward trace 
- path: Path+file to look for.<br>
Returns: Place where is found or null if any.


# T:Kltv.Kombine.Api.Host
 Offers access to the host information 

##### M:Kltv.Kombine.Api.Host.IsRoot
 Returns if the script is running with administrator privileges 
Returns: True if we're running under administrator privileges. False otherwise.


##### M:Kltv.Kombine.Api.Host.IsInteractive
 Returns if the script is being executed in an interactive shell 
Returns: True if we're on an interactive shell. False otherwise


##### M:Kltv.Kombine.Api.Host.GetOSKind
 Returns a string with the kindly name of the os 
Returns: The os string


##### M:Kltv.Kombine.Api.Host.IsWindows
 Returns if the script is being executed in a Windows environment 
Returns: True if we're on windows, false otherwise


##### M:Kltv.Kombine.Api.Host.IsLinux
 Returns if the script is being executed in a Linux environment 
Returns: True if we're on linux, false otherwise.


##### M:Kltv.Kombine.Api.Host.IsMacOS
 Returns if the script is being executed in a macos environement 
Returns: True if we're on osx, false otherwise.


##### M:Kltv.Kombine.Api.Host.ProcessorCount
 Returns the number of CPU cores available 
Returns: The number of cores.


# T:Kltv.Kombine.Api.Http
 Http Methods API 

##### M:Kltv.Kombine.Api.Http.DownloadFile(System.String,System.String,System.Boolean)
 Downloads a file from the given uri to the given path 
- uri: The uri for the file to be downloaded<br>
- path: The resulting path for the file.<br>
- showprogress: If true, a progress bar will be shown<br>
Returns: True if file was downloaded, false otherwise.


##### M:Kltv.Kombine.Api.Http.DownloadFiles(System.String[],System.String[],System.Boolean)
 Download multiple files from the given uris to the given paths 
- uris: Arrays of uris to be used<br>
- paths: Array of paths+filenames to be used<br>
- showprogress: If the progress should be show, default true.<br>
Returns: True if all files download fine, false otherwise.


##### M:Kltv.Kombine.Api.Http.GetDocument(System.String)
 Gets the document from the given uri 
- uri: Uri for the document to be retrieved<br>
Returns: The string with the document or empty


##### F:Kltv.Kombine.Api.Http.bar
 A progress bar instance to show download progress 

# T:Kltv.Kombine.Api.Http.ProgressBarChanged
 A delegate to report progress on downloads 
- sender: <br>
- progress: <br>

##### F:Kltv.Kombine.Api.Http.progress
 Stores the dictionary of progress for each download to make an average. 

##### M:Kltv.Kombine.Api.Http.Progress_ProgressChanged(System.Object,System.Single)
 It receives all the progress events from the download and reports them to the progress bar 
- sender: stream sending the progress report.<br>
- progress: amount of progress in that stream<br>

# T:Kltv.Kombine.Api.HttpClientExtensions
 Extensions methods for the HttpClient class 

##### M:Kltv.Kombine.Api.HttpClientExtensions.DownloadDataAsync(System.Net.Http.HttpClient,System.String,System.IO.Stream,Kltv.Kombine.Api.Http.ProgressBarChanged,System.Threading.CancellationToken)
 Downloads a file from the given uri to the given path asynchronously 
- client: client to extend<br>
- requestUrl: requested url<br>
- destination: destionation to save<br>
- progress: progress reporting delegate.<br>
- cancellationToken: cancelation token to cancel the operation.<br>
Returns: A task that can be awaited.


##### M:Kltv.Kombine.Api.HttpClientExtensions.CopyToAsync(System.IO.Stream,System.IO.Stream,System.Int64,System.Int32,Kltv.Kombine.Api.Http.ProgressBarChanged,System.Threading.CancellationToken)
 Copies the content of the source stream to the destination stream asynchronously 
- source: source stream<br>
- destination: destination stream<br>
- totalbytes: total bytes of the stream<br>
- bufferSize: buffer size to be used.<br>
- progress: progress reporting delegate.<br>
- cancellationToken: cancelation token.<br>
Returns: A task that can be awaited.

[[T:System.ArgumentOutOfRangeException|T:System.ArgumentOutOfRangeException]]: 

[[T:System.ArgumentNullException|T:System.ArgumentNullException]]: 

[[T:System.InvalidOperationException|T:System.InvalidOperationException]]: 


# T:Kltv.Kombine.Api.JsonFile
 Encapsulates a JSON file and provides methods to read and write it. 

##### F:Kltv.Kombine.Api.JsonFile._file
 Internal filename with full path 

##### M:Kltv.Kombine.Api.JsonFile.#ctor(Kltv.Kombine.Types.KValue)
 Initializes an instance of JsonFile 
- file: Filename to be used. May contain relative path or be absolute.<br>

##### M:Kltv.Kombine.Api.JsonFile.LoadAndParseDocument(System.String)
 Loads the file and parses it into a JsonNode 
- filePath: Filename to be fetched<br>

##### M:Kltv.Kombine.Api.JsonFile.Save
 Saves the current document to the file 
Returns: True if everything okey, false otherwise.


##### P:Kltv.Kombine.Api.JsonFile.Doc
 Holds the root JsonNode 

# T:Kltv.Kombine.Api.JsonExtensions
 JSON extensions 

##### M:Kltv.Kombine.Api.JsonExtensions.Find(System.Text.Json.Nodes.JsonArray,System.String,System.Object)
 Return the node inside the array that matches one entry with the name and value. 
- i: this array parameter<br>
- name: name of the property the array entry should have<br>
- value: value of the property the array entry should have<br>
Returns: The Jsonnode which contains the given property and value. Null otherwise


# T:Kltv.Kombine.Api.Msg


##### M:Kltv.Kombine.Api.Msg.Initialize(Kltv.Kombine.Api.Msg.LogLevels)
 Initializes the Msg static class. Note that parameters can be changed at runtime during the script execution so, inside the script you can raise the log level or change the output flags 
- Level: Specify the log level<br>

##### M:Kltv.Kombine.Api.Msg.Deinitialize
 Deinitializes the Msg static class. 

# T:Kltv.Kombine.Api.Msg.OutputDevices


##### F:Kltv.Kombine.Api.Msg.OutputDevices.Default


##### F:Kltv.Kombine.Api.Msg.OutputDevices.Console


##### F:Kltv.Kombine.Api.Msg.OutputDevices.File


##### F:Kltv.Kombine.Api.Msg.OutputDevices.Debugger


# T:Kltv.Kombine.Api.Msg.LogTypes


##### F:Kltv.Kombine.Api.Msg.LogTypes.IDE


##### F:Kltv.Kombine.Api.Msg.LogTypes.Console


##### F:Kltv.Kombine.Api.Msg.LogTypes.Silent


# T:Kltv.Kombine.Api.Msg.LogLevels


##### F:Kltv.Kombine.Api.Msg.LogLevels.Silent


##### F:Kltv.Kombine.Api.Msg.LogLevels.Normal


##### F:Kltv.Kombine.Api.Msg.LogLevels.Verbose


##### F:Kltv.Kombine.Api.Msg.LogLevels.Debug


##### F:Kltv.Kombine.Api.Msg.LogLevels.Undefined


##### P:Kltv.Kombine.Api.Msg.OutputDevice


##### P:Kltv.Kombine.Api.Msg.LogType


##### P:Kltv.Kombine.Api.Msg.LogLevel


##### M:Kltv.Kombine.Api.Msg.Lock


##### M:Kltv.Kombine.Api.Msg.UnLock


##### M:Kltv.Kombine.Api.Msg.Print(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintMod(System.String,System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Mod: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintWarning(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintWarningMod(System.String,System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Mod: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintError(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintErrorMod(System.String,System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Mod: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintAndAbort(System.String,Kltv.Kombine.Api.Msg.LogLevels)
 Prints a message and aborts the script execution. 
- Message: Message to print<br>
- Level: Log level, default normal<br>

##### M:Kltv.Kombine.Api.Msg.PrintAndAbortMod(System.String,System.String,Kltv.Kombine.Api.Msg.LogLevels)
 Prints a message and aborts the script execution. 
- Message: Message to print<br>
- Mod: Module source of the message<br>
- Level: Loglevel, by default, normal.<br>

##### M:Kltv.Kombine.Api.Msg.RawPrint(System.String,Kltv.Kombine.Api.Msg.LogLevels)
 Raw print message to the console 
- Message: Message to be printed.<br>
- Level: Log level<br>

>It skips colors and indentation

##### M:Kltv.Kombine.Api.Msg.PrintTask(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintTaskSuccess(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintTaskWarning(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.PrintTaskError(System.String,Kltv.Kombine.Api.Msg.LogLevels)

- Message: <br>
- Level: <br>

##### M:Kltv.Kombine.Api.Msg.BeginIndent(System.Boolean)
 Adds another level of indentation in the log output We use the flag "used". Indentation is only added if we already used the current level. 
- bSkipNotUsed: Specify if the indentation should be skipped when not used.<br>

##### M:Kltv.Kombine.Api.Msg.EndIndent
 Removes one level of indentation in the log output 

##### M:Kltv.Kombine.Api.Msg.GetIndent
 Retrieve the current indentation. 
Returns: An empty string or an string with spaces representing the indentation level


##### M:Kltv.Kombine.Api.Msg.InternalPrint(System.String,System.String,Kltv.Kombine.Api.Msg.LogLevels,System.ConsoleColor,System.Boolean)

- Message: <br>
- Mod: <br>
- Level: <br>
- Color: <br>
- SkipIndent: Specify if the indentation should be skipped.<br>

# T:Kltv.Kombine.Api.ProgressBar
 An ASCII progress bar 

# T:Kltv.Kombine.Api.Share
 Shared object API It is intended to share instances of object across scripts. 

##### F:Kltv.Kombine.Api.Share.registry
 Registry dictionary shared across all scripts no mather the relationship 

##### M:Kltv.Kombine.Api.Share.Register(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue,System.Boolean)
 Adds a new registry to the shared registry pool It is shared across all the scripts. No matter the relationship 
- name: Name for the registry<br>
- key: Key to store<br>
- value: Value to store<br>
- ExitIfError: If the script should exit if the value is missing / trigers error. Default false<br>
Returns: True if okey, false otherwise.


##### M:Kltv.Kombine.Api.Share.Registry(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue,System.Boolean)
 Fetch a value from the registry under the given name and key 
- name: Name to resolve.<br>
- key: Key to be queried.<br>
- ExitIfError: If the script should exit if the value is missing / trigers error. Default false<br>
Returns: Value for that entry or empty if not found.


##### M:Kltv.Kombine.Api.Share.DumpRegistry
 Dumps the content of the registry for script debugging purposes 

##### M:Kltv.Kombine.Api.Share.Set(System.String,System.Object,System.Boolean)
 Set an object to be shared 
- name: name for the object to be shared<br>
- obj: object to be shared<br>
- ExitIfError: If the script should exit if the value is missing / trigers error. Default false<br>
Returns: True if the object is added. False if it was already shared.


##### M:Kltv.Kombine.Api.Share.Get(System.String,System.Boolean)
 Get an object from the shared pool 
- name: Name of the object to be fetched.<br>
- ExitIfError: If the script should exit if the value is missing / trigers error. Default false<br>
Returns: The object to use or null if doesn't exists.


##### M:Kltv.Kombine.Api.Share.DumpObjects(System.Boolean)
 Dump the content of the shared object pool for debugging purposes 

# T:Kltv.Kombine.Api.Statics
 Static methods published into the script. They can be used directly without any class decoration. 

##### M:Kltv.Kombine.Api.Statics.Glob(System.String)
 Resolves a Glob pattern to a list of files. It uses the current working directory as the base path to resolve the pattern. 
- pattern: Pattern to be resolved<br>
Returns: A KList with the resolved files.


##### M:Kltv.Kombine.Api.Statics.Glob(System.String,System.String)
 Resolves a Glob pattern to a list of files. It uses the current working directory as the base path to resolve the pattern. 
- folder: Folder to be used as the base path.<br>
- pattern: Pattern to be resolved<br>
Returns: A KList with the resolved files.


##### M:Kltv.Kombine.Api.Statics.RealPath(Kltv.Kombine.Types.KValue)
 Returns the real path of the given path. It is converted to the underlying OS if required. If its a relative path, it is converted to an absolute path. 
- path: Path to be evaluated.<br>
Returns: Realpath returned


##### M:Kltv.Kombine.Api.Statics.Kombine(System.String,System.String,System.String[],System.Boolean,System.Boolean,System.Boolean)
 Executes a child Kombine script. This method do not spawn a new process, it just executes the script in the current process. To exchange information check for import/export methods on kvalues and the shared object api. 
- script: script to be executed.<br>
- action: action to be executed.<br>
- args: parameters to the action.<br>
- exitonerror: if true, exits the script on error.<br>
- changedir: if true, changes the current working directory to the script folder.<br>
- search: if true, search for the script in different routes.<br>
Returns: The return code from the script execution.


##### M:Kltv.Kombine.Api.Statics.Exec(System.String,System.String[],System.Boolean)
 Executes a command line tool. 
- command: Command to execute.<br>
- args: argument array for the command<br>
- showoutput: if true, shows the output of the command.<br>
Returns: Exitcode from the command.


##### M:Kltv.Kombine.Api.Statics.Exec(System.String,System.String,System.Boolean)
 Executes a command line tool. 
- command: Command to execute.<br>
- args: argument array for the command<br>
- showoutput: if true, shows the output of the command.<br>
Returns: Exitcode from the command.


##### M:Kltv.Kombine.Api.Statics.Shell(System.String,System.String[])
 Executes a command line tool using the system shell. 
- command: Command to execute.<br>
- args: Arguments for the command.<br>
Returns: Exitcode from the command.


##### M:Kltv.Kombine.Api.Statics.Shell(System.String,System.String)
 Executes a command line tool using the system shell. 
- command: Command to execute.<br>
- args: Arguments for the command.<br>
Returns: Exitcode from the command.


##### M:Kltv.Kombine.Api.Statics.ArgEscape(System.String)
 Function to escape parameters for command line processing 
- arg: argument to be escaped<br>
Returns: the escaped argument


##### M:Kltv.Kombine.Api.Statics.Cast``1(System.Object)
 Cast an object to another type trying to copy as much as possible. 
##### Type Parameter: T
Returned type
- myobj: Object to be casted<br>
Returns: A new created object of the new type or null if invalid.


##### M:Kltv.Kombine.Api.Statics.MkbVersion
 Retuns the Kombine version string. 
Returns: The string in dot formated


##### M:Kltv.Kombine.Api.Statics.MkbMajorVersion
 Returns the major version number. 
Returns: The major version


##### M:Kltv.Kombine.Api.Statics.MkbMinorVersion
 Returns the minor version number. 
Returns: The minor version


##### M:Kltv.Kombine.Api.Statics.MkbHexVersion
 Returns the najor+minor version as a hex value. 
Returns: The version


# T:Kltv.Kombine.Api.Tool
 Wraps a tool interaction (status / result / launch / version) 
 Encapsulated in the tool class we have the tool results 

##### M:Kltv.Kombine.Api.Tool.#ctor(System.String)
 Tool Constructor 
- ToolTag: Tag name for the tool<br>

##### M:Kltv.Kombine.Api.Tool.#ctor
 Tool Constructor 

# T:Kltv.Kombine.Api.Tool.ToolStatus
 Valid states for tools 

##### F:Kltv.Kombine.Api.Tool.ToolStatus.Success
 Tool execution was fine 

##### F:Kltv.Kombine.Api.Tool.ToolStatus.Failed
 Tool execution returned errors 

##### F:Kltv.Kombine.Api.Tool.ToolStatus.Warnings
 Tool execution returned warnings 

##### F:Kltv.Kombine.Api.Tool.ToolStatus.NoChanges
 Tool execution was fine but nothing has been done 

##### F:Kltv.Kombine.Api.Tool.ToolStatus.Pending
 Tool is executing so status is pending 

##### F:Kltv.Kombine.Api.Tool.ToolStatus.Undefined
 Undefined state before execution 

##### P:Kltv.Kombine.Api.Tool.ExpectedExitCode
 Expected exit code for the tool. Zero by default 

##### P:Kltv.Kombine.Api.Tool.Status
 Holds the tool status 

##### P:Kltv.Kombine.Api.Tool.ToolTag
 Holds the tool tag. Its used on logs but also as indexer for data stored in the state 

##### P:Kltv.Kombine.Api.Tool.UseShell
 If true, the tool will be launched using the system shell It is only available for sync commands 

##### P:Kltv.Kombine.Api.Tool.ConcurrentCommands
 Number of concurrent process launched by the tool 

##### P:Kltv.Kombine.Api.Tool.CaptureOutput
 If true, the tool will show the output in the console 

##### F:Kltv.Kombine.Api.Tool.Lock
 Internal object to lock the tool for output order. 

##### F:Kltv.Kombine.Api.Tool.CancelExecution
 Flag to indicate if we need to cancel further executions, clear and exit inmediately 

##### M:Kltv.Kombine.Api.Tool.CommandSync(System.String,System.String[],System.Object)
 Launch a tool in sync way. 
- cmd: Command to launch<br>
- args: Arguments for the tool<br>
- id: Optional, id to be attached in the child process control code.<br>
Returns: ToolResult type with all the executing information.


##### M:Kltv.Kombine.Api.Tool.CommandSync(System.String,System.String,System.Object)
 Launch a tool in sync way. 
- cmd: Command to launch<br>
- args: Arguments for the tool<br>
- id: Optional, id to be attached in the child process control code.<br>
Returns: ToolResult type with all the executing information.


# T:Kltv.Kombine.Api.Tool.AsyncCommand
 Holds an async command task to be executed 

##### F:Kltv.Kombine.Api.Tool.asyncCommands
 List of async commands to be executed 

##### M:Kltv.Kombine.Api.Tool.QueueCommand(System.String,System.String,System.Object,Kltv.Kombine.Api.Tool.CommandAsyncResults)
 Queue a command to be executed. It will be executed in the order they are queued The number of concurrent commands could be changed using the ConcurrentCommands property 
- cmd: Command to execute. It may be on path or specify the exact location<br>
- args: Arguments to pass<br>
- id: Id for this command. Useful to do the relationship when the callback is called when command is complete. Can be anything. For example the source file name passed to the tool. <br>
- callback: Callback to be called when the command is completed. See CommandAsyncResults delegate.<br>

##### M:Kltv.Kombine.Api.Tool.ExecuteCommands(System.Boolean)
 Execute all the queued commands. It will respect the concurrentcommands property It will block until all the commands are finished 
- AggregateResults: If true, it will return a global result with the status of all the commands. <br>
Returns: ToolResult object is returned. If there are no commands, result is set to nochanges.


##### F:Kltv.Kombine.Api.Tool.PendingAsyncTasks
 Internal counter for async pending tasks 

# T:Kltv.Kombine.Api.Tool.CommandAsyncResults
 Delegate to deliver tool execution results 
- results: <br>

##### M:Kltv.Kombine.Api.Tool.CommandAsync(System.String,System.String,Kltv.Kombine.Api.Tool.CommandAsyncResults,System.Object)
 Launch a tool in async way. 
- cmd: Command to launc<br>
- args: Arguments for the tool<br>
- CommandAsyncResult: Delegate to be called once the process is done<br>
- id: Optional, id to be attached in the child process control code. <br>
Returns: ToolResult type with partial execution information. 


##### M:Kltv.Kombine.Api.Tool.CommandAsyncFinished(Kltv.Kombine.ChildProcess)
 Its being called after the command execution in async way. It collects the data and calls the result delegate 
- proc: <br>

##### M:Kltv.Kombine.Api.Tool.CommandAsyncWaitAll(System.UInt32)
 Waits for all the pending tasks to complete (or by given threshold) 

# T:Kltv.Kombine.Api.Tool.ToolResult
 Holds the tool results. All the information about the tool execution 

##### M:Kltv.Kombine.Api.Tool.ToolResult.#ctor(System.String[],System.String[],Kltv.Kombine.Api.Tool.ToolStatus,System.Int32,System.Object)
 Tool results constructor 

##### P:Kltv.Kombine.Api.Tool.ToolResult.Stdout
 Holds the stdoutput for the tool 

##### P:Kltv.Kombine.Api.Tool.ToolResult.Stderr
 Holds the stderr for the tool 

##### P:Kltv.Kombine.Api.Tool.ToolResult.Status
 Holds a copy of the status 

##### P:Kltv.Kombine.Api.Tool.ToolResult.ExitCode
 Holds the tool exit code 

##### P:Kltv.Kombine.Api.Tool.ToolResult.Id
 Holds a user given Id for the command launched 

# T:Kltv.Kombine.Types.KList
 KList class. It is a regular list with helper functions to be used in build environemnts. 

##### M:Kltv.Kombine.Types.KList.#ctor
 Default constructor 

##### M:Kltv.Kombine.Types.KList.#ctor(Kltv.Kombine.Types.KList)
 Copy constructor 
- other: The object to be copied<br>

##### M:Kltv.Kombine.Types.KList.Flatten
 Flatten the list into a single string 
Returns: A single string with all the values space separated.


##### M:Kltv.Kombine.Types.KList.Flatten(System.String)
 Flatten the list into a single string specifying the separator 
- separator: separator to use<br>
Returns: A single string with all the values separated by the given separator.


##### M:Kltv.Kombine.Types.KList.Count
 Returns the number of elements in the list. 
Returns: The number of elements


##### M:Kltv.Kombine.Types.KList.HasDuplicates
 Returns if the list contains duplicates 
Returns: True if has duplicates. False otherwise.


##### M:Kltv.Kombine.Types.KList.RemoveDuplicates
 Removes all the duplicates from the list. 
Returns: The new list without duplicates.


##### M:Kltv.Kombine.Types.KList.WithExtension(System.String)
 Returns a new list with all the values being considered as files with the extension changed. 
- n: New extension to use.<br>
Returns: The newly generated list with the new file extension.


##### M:Kltv.Kombine.Types.KList.WithReplace(System.String,System.String)
 Replaces in all the elements the old values with the new ones. If some element becomes empty after the replace, it will be discarded. 
- ov: old value to be replaced<br>
- nv: new value to be used.<br>
Returns: The new modified KList


##### M:Kltv.Kombine.Types.KList.Compare(Kltv.Kombine.Types.KList)
 Compares two lists and returns if they are equal or not. 
- other: The list to be compared against.<br>
Returns: True if they are equal, false otherwise.


##### M:Kltv.Kombine.Types.KList.WithPrefix(Kltv.Kombine.Types.KValue)
 Prefixes all the values in the list with the given value 
- n: The value to be used as prefix<br>
Returns: The new list generated with the prefixes.


##### M:Kltv.Kombine.Types.KList.AsFolders
 Returns a new list with all the values being considered as files/folders using only the folder paths. It does not return duplicates so if several entries in the list can be consideer as files into the same folder they will produce just one folder entry in the new list. 
Returns: The newly created list with folder values.


##### M:Kltv.Kombine.Types.KList.Contains(Kltv.Kombine.Types.KValue)
 Returns if the list contains the given value. 
- v: Value to be checked.<br>
Returns: True if its on the list, false otherwise.


# T:Kltv.Kombine.Types.KValue
 Encapsulate a Kombine Value 

##### M:Kltv.Kombine.Types.KValue.#ctor
 Kombine Value constructor 

##### M:Kltv.Kombine.Types.KValue.Export(System.String)
 Export a value to the environment 
- name: Name to be set on the environment<br>

##### M:Kltv.Kombine.Types.KValue.Import(System.String,System.String)
 Import a value from the environment. If there is no default value and the variable is not found, it will abort the script. 
- name: Name of the variable to be recovered from environment<br>
- defvalue: Optional default value in case the environement variable is not present.<br>
Returns: The variable value


##### M:Kltv.Kombine.Types.KValue.ToArray
 Returns an array from a KValue which is space separated 
Returns: The KList containing all the fetched values


##### M:Kltv.Kombine.Types.KValue.ToArray(Kltv.Kombine.Types.KValue)
 Returns an array from a KValue which is separated by the given separator 
- separator: Separator to use<br>
Returns: The KList array


##### M:Kltv.Kombine.Types.KValue.ToArray(System.String[])
 Returns an array from a KValue which is separated by the given separators 
- separators: string array of separators to use.<br>
Returns: The KList array


##### M:Kltv.Kombine.Types.KValue.ToArgs
 Converts the KValue to a list of arguments It will split the string by spaces, but it will keep the quoted strings as one argument. 
Returns: The created list with the arguments


##### M:Kltv.Kombine.Types.KValue.WithExtension(System.String)
 If the value can be considered as a file, it will return the file name with extension changed 
- n: New extension to be applied<br>
Returns: Value changed or empty if is not a file.


##### M:Kltv.Kombine.Types.KValue.HasExtension(System.String)
 Check if the value can be interpreted as file and has a given extension 
- ext: extension to be checked<br>
Returns: true if the extension was pressent, false otherwise


##### M:Kltv.Kombine.Types.KValue.IsEmpty
 Check if the value is empty. 
Returns: True if its empty, false otherwise


##### M:Kltv.Kombine.Types.KValue.AsFolder
 Returns the value as is a folder without filename in case it is a file. 
Returns: The folder value


##### M:Kltv.Kombine.Types.KValue.GetParent
 Returns the parent folder of the value if can be consideer as path 
Returns: The parent folder or empty if cannot be converted.


##### M:Kltv.Kombine.Types.KValue.WithNamePrefix(System.String)
 Returns a new KValue with the name prefixed with the indicated prefix TODO: Review, is switch the back/forwd slash 
- prefix: Prefix to add into the filename<br>
Returns: The new kvalue, changed or unchanged if not possible.


##### M:Kltv.Kombine.Types.KValue.op_Equality(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Equality operator. It compares contents, not references. 
- a: param1 to be compared<br>
- b: param2 to be compared<br>
Returns: true if equal, false otherwise


##### M:Kltv.Kombine.Types.KValue.op_Inequality(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Non equality operator. It compares contents, not references. 
- a: param1 to be compared<br>
- b: param2 to be compared<br>
Returns: true if non equal, false otherwise


##### M:Kltv.Kombine.Types.KValue.op_Addition(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Add operator 

##### M:Kltv.Kombine.Types.KValue.op_Subtraction(Kltv.Kombine.Types.KValue,Kltv.Kombine.Types.KValue)
 Substract operator 

##### M:Kltv.Kombine.Types.KValue.op_Implicit(System.String)~Kltv.Kombine.Types.KValue
 Implicit creation operator (from string to Kvalue) 

##### M:Kltv.Kombine.Types.KValue.op_Implicit(Kltv.Kombine.Types.KValue)~System.String
 Implicit creation operator (from KValue to string) 

##### F:Kltv.Kombine.Types.KValue.m_value
 Internal string value 

##### M:Kltv.Kombine.Types.KValue.Equals(System.Object)
 Object comparison operator. Compares reference and content. 

##### M:Kltv.Kombine.Types.KValue.ReduceWhitespace
 Reduces whitespaces in the KValue. Only one consecutive is allowed. 
Returns: A new KValue without duplicated whitespaces


##### M:Kltv.Kombine.Types.KValue.ToString
 Convert the kvalue to string 
Returns: The represented string


##### M:Kltv.Kombine.Types.KValue.GetHashCode
 Calculates a hash for the given object. Hash is not be fixed. Same string != same hash 
Returns: An int 64 32 returned with the hash value


##### M:Kltv.Kombine.Types.KValue.GetHashCode64
 Calculates a hash for the given string. Hash will be fixed. Same string = same hash 
Returns: An unsigned int 64 is returned with the hash value


##### M:Kltv.Kombine.Types.KValue.ConvertToUnixPath(Kltv.Kombine.Types.KValue)
 Replace slashes from windows to unix style 
- path: path to change<br>
Returns: the new value


##### P:Kltv.Kombine.Cache.CacheFolder
 Base cache folder 

##### P:Kltv.Kombine.Cache.CacheStates
 Folder for cached states (precompiled scripts) 

##### P:Kltv.Kombine.Cache.CacheScripts
 Folder for downloaded scripts 

##### M:Kltv.Kombine.Cache.Action(System.String[])
 Cache actions. Execute cache related actions. 
- args: Arguments for the action.<br>

##### M:Kltv.Kombine.Cache.Init
 Initializes the cache folders. 

##### M:Kltv.Kombine.Cache.IsScriptCached(System.String)
 Returns if the script is cached and it could be used. 
- scriptname: Scriptname to check on the cache.<br>
Returns: True if we can use it. False otherwise.


##### M:Kltv.Kombine.Cache.IsIncludeCached(System.String)
 Returns if the script is cached and it could be used. 
- includename: URI for the script to lookup in the cache<br>
Returns: true if we've one copy there. false otherwise


##### M:Kltv.Kombine.Cache.GetIncludeCached(System.String)
 Get the include name from the cache to be used in source resolver. 
- includename: URI for the script<br>
Returns: filename to use


##### M:Kltv.Kombine.Cache.SetIncludeCached(System.String,System.String)
 Set the include in the cache. 
- includename: URI origin of the script.<br>
- content: Content of the script<br>
Returns: The filename used to store it on the cache or empty if error.


##### M:Kltv.Kombine.Cache.LoadScriptCached(System.String)
 Load a cached script. 
- scriptname: Filename for the script to be loaded.<br>
Returns: A byte array with the contents or null if failed.


##### M:Kltv.Kombine.Cache.SaveScriptCached(System.String,System.Byte[])
 Saves a cached script. 
- scriptname: Filename for the script to be saved<br>
- content: Byte array with the contents.<br>
Returns: True if saving was okey. False otherwise.


##### M:Kltv.Kombine.Cache.ConvertFilename(System.String,System.Boolean)
 Converts the given scriptname into a filename for the cache. 
- scriptname: Script name to be converted<br>
- include: If true, the filename indicates cached include. Script object otherwise.<br>
Returns: The resulting name including cache path


# T:Kltv.Kombine.Config


##### P:Kltv.Kombine.Config.ScriptFile
 Script file to be executed. By default "kombine.csx" in the current working directory 

##### P:Kltv.Kombine.Config.ScriptPath
 Script path. Taken from script file command line if specified or current working directory 

##### P:Kltv.Kombine.Config.BuildDebug
 Debug script build 

##### P:Kltv.Kombine.Config.Rebuild
 If the script should be rebuilded 

##### P:Kltv.Kombine.Config.BuildOnly


##### P:Kltv.Kombine.Config.AllowAssemblyLoad


##### P:Kltv.Kombine.Config.SaveAlwaysOnExit
 Always save the state when the script terminates 

##### P:Kltv.Kombine.Config.Action
 Action to be executed 

##### P:Kltv.Kombine.Config.ActionParameters
 Action parameters to be attached in action execution 

##### F:Kltv.Kombine.Config.LogLevel
 Log level for the output 

##### M:Kltv.Kombine.Config.Initialize
 Initializes this module, executes command line and initializes rest of modules 

##### M:Kltv.Kombine.Config.ParseCommandLine
 Kombine Parameters: mkb [parameters] [action] [action parameters] [parameters] They are optional and can be any of the following: -ksdbg: Script will include debug information so script debugging will be possible -ko:silent or -ko:s : Output will be silent -ko:normal or -ko:n : Output will be normal -ko:verbose or -ko:v : Output will be verbose -ko:debug or -ko:d : Output will be debug -kfile: Indicates which script file we should execute (default kombine.csx) [action] Action to be executed. If not specified the default action is "khelp" The action is used to specify which function in the script should be called after evaluation but there are some reserved actions for the tool itself which cannot be used for the scripts: kversion: Shows tool version and exit khelp: Show this help and exit kconfig: Manages the tool configuration kcache: Manages the tool cache [action parameters] They are optional and belongs to the specified action. In case of scripts,they are passed to the executed function as parameters. 

##### M:Kltv.Kombine.Config.ParseKombineParameters(System.String)
 Parses and sets the kombine parameters 
- cmd: parameter to be evaluated<br>
Returns: true if was a kombine parameter, false otherwise


##### M:Kltv.Kombine.Config.ShowBanner


##### M:Kltv.Kombine.Config.ShowHelp


# T:Kltv.Kombine.Constants
 Holds all the string constants for the tool 

##### F:Kltv.Kombine.Constants.Cache_Folder


##### F:Kltv.Kombine.Constants.Cache_States


##### F:Kltv.Kombine.Constants.Cache_Scripts


##### F:Kltv.Kombine.Constants.Log_Folder


##### F:Kltv.Kombine.Constants.Log_FileExtension


##### F:Kltv.Kombine.Constants.Log_FileSuccess


##### F:Kltv.Kombine.Constants.Log_FileFailed


##### F:Kltv.Kombine.Constants.Log_Prefix


##### F:Kltv.Kombine.Constants.Ext_Compiled


##### F:Kltv.Kombine.Constants.Ext_IncludeCache


##### F:Kltv.Kombine.Constants.Art_Folder


##### F:Kltv.Kombine.Constants.Tmp_Folder
 Temporal folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Bin_Folder
 Binaries folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Lib_Folder
 Static libraries / references / facades folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Inc_Folder
 Public Includes folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Pub_Folder
 Publishing folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Gen_Folder
 Build generated content folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Doc_Folder
 Documents generated folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Sdk_Folder
 External dependencies / packages folder in top of artifact folders 

##### F:Kltv.Kombine.Constants.Mak_File
 Default script filename 

# T:Kltv.Kombine.KombineState
 Holds the functions to serialize / deserialize a kombine script state 

##### F:Kltv.Kombine.KombineState.scriptfilename
 The script filename which belongs to this instance 

##### M:Kltv.Kombine.KombineState.#ctor
 Initializes one Kombine script instance. It initializes the environment variables with the current system environment 

##### M:Kltv.Kombine.KombineState.FetchCache(System.String)
 Fetch and deserialize the cache. Returns true if the script is available to be executed false otherwise (non existent, outdated) 
- filename: Script name to try recover state<br>
Returns: True if ready. False it should be rebuilt.


##### M:Kltv.Kombine.KombineState.SetCompiledScript(System.IO.MemoryStream,System.IO.MemoryStream)
 Saves the memory stream for a newly built script 
- ms: Object with the assembly<br>
- ds: Object with the debug information<br>

##### M:Kltv.Kombine.KombineState.Serialize
 Saves the state file 

##### M:Kltv.Kombine.KombineState.Deserialize
 Loads the state file 

##### P:Kltv.Kombine.KombineState.Environment
 Environment variables for the script 

##### P:Kltv.Kombine.KombineState.SharedObjects
 Shared objects for the script 

##### P:Kltv.Kombine.KombineState.Data
 Return the data object which belongs to this state. 

# T:Kltv.Kombine.KombineState.StateFile
 State file holds the full structure state for building serialized to disk It includes among signatures and co, the file time for the script (just to rebuild if something changed) and the project list with units, sources,etc, everything with modification dates to build the DAG 

##### F:Kltv.Kombine.KombineState.StateFile.Signature
 Signature to recognize a state file (by default 0x000020001) 

##### F:Kltv.Kombine.KombineState.StateFile.Version
 Version of the state file (just in case we need to discard older ones due to update) Current: 0x00010000; 

##### F:Kltv.Kombine.KombineState.StateFile.ScriptModifiedTime
 Script modification time in EPOCH 

##### F:Kltv.Kombine.KombineState.StateFile.BuildWithDebug
 If the script was built with debug information 

##### F:Kltv.Kombine.KombineState.StateFile.CompiledScript
 Bynary blob holding the compiled script assembly 

##### F:Kltv.Kombine.KombineState.StateFile.CompiledScriptPDB
 Binary blob holding the compiled script debug information (if any) 

# T:Kltv.Kombine.KombineScript
 Compiles and executes a Kombine script 
 Compiles and executes a Kombine script 
 Compiles and executes a Kombine script Since is not correctly documented, some references has been taken from: https://www.strathweb.com/2016/06/implementing-custom-load-behavior-in-roslyn-scripting/ 

# T:Kltv.Kombine.KombineScript.AssemblyResolver


##### M:Kltv.Kombine.KombineScript.AssemblyResolver.ResolveMissingAssembly(Microsoft.CodeAnalysis.MetadataReference,Microsoft.CodeAnalysis.AssemblyIdentity)

- definition: <br>
- referenceIdentity: <br>
Returns: 


##### M:Kltv.Kombine.KombineScript.AssemblyResolver.ResolveReference(System.String,System.String,Microsoft.CodeAnalysis.MetadataReferenceProperties)

- reference: <br>
- baseFilePath: <br>
- properties: <br>
Returns: 


##### M:Kltv.Kombine.KombineScript.GetPEReferenceFromMemoryAssembly(System.Reflection.Assembly)
 Return a portable executable reference for an in memory assembly. It also fakes the reference data for the filename and co to allow use assemblies which are not in disk 
- assembly: Assembly to get the reference<br>
Returns: PortableExecutableReference or null if was not posible.


##### P:Kltv.Kombine.KombineScript.ParentScript
 Holds the parent script if any 

##### F:Kltv.Kombine.KombineScript.State
 Holds the script state That includes: -Precompiled binary -Custom script data 

##### P:Kltv.Kombine.KombineScript.ClassName
 Class name for the script. It will be the script name plus _class sufix 

##### P:Kltv.Kombine.KombineScript.AssemblyName
 Assembly name for the script. It will be the script name (metadata reference) 

##### P:Kltv.Kombine.KombineScript.ModuleName
 Module name for the script. It will be the script name plus .kscript.dll (binary name) 

##### P:Kltv.Kombine.KombineScript.Scriptfile
 The script filename to be executed 

##### P:Kltv.Kombine.KombineScript.ActionParameters
 Action parameters to be attached in action execution 

##### P:Kltv.Kombine.KombineScript.ScriptPath
 The script path. Used to resolve #load directives in the script among others 

##### P:Kltv.Kombine.KombineScript.DebugBuild
 If we should execute a debug build 

##### P:Kltv.Kombine.KombineScript.WasRebuilt
 Signals if the script was rebuilt (due to script changed or by version, not forced) 

##### P:Kltv.Kombine.KombineScript.ParentWasRebuilt
 Signals if the parent script was rebuilt (due to script changed or by version, not forced) 

##### P:Kltv.Kombine.KombineScript.Usings
 Automatic usings for the script to be included 

##### P:Kltv.Kombine.KombineScript.Assemblies
 Allowed assemblies to be referenced from the script 

##### P:Kltv.Kombine.KombineScript.InjectedCode
 Properties to be added in all the scripts 

##### M:Kltv.Kombine.KombineScript.#ctor(System.String,System.String,System.Boolean)
 Creates an instance for one Kombine script 
- Script: Filename for the script<br>
- Scriptpath: Path where the script is.<br>
- Debug: If we should activate debug options for the script.<br>

##### M:Kltv.Kombine.KombineScript.LoadReferences
 Load all the references for the script 

##### M:Kltv.Kombine.KombineScript.Execute(System.String,System.String[])
 Executes the script given by filename. It will try first to load it from the saved state (precompiled one) If not, it will be compiled. 
- Action: Action to be executed in the script.<br>
- ActionParameters: The parameters for the given action.<br>
Returns: Returns the exitcode from script execution


##### M:Kltv.Kombine.KombineScript.EvaluateResult(System.Object)
 Evaluates the result from a top level statement task execution. Since is a bit more tedious in code, is here, in this separate function 
- result: the resulting object to be evaluated<br>
Returns: True if the execution was fine, false otherwise.


##### M:Kltv.Kombine.KombineScript.Compile(System.String,System.Boolean)
 Compiles a script given by filename. Assembly Name, Class Name and Module Name should be defined before calling this function. State should be initialized before calling this function. 
- filename: <br>
- Debug: <br>
Returns: True if compilation was okey. False otherwise


##### M:Kltv.Kombine.KombineScript.FetchScriptText(System.String,System.Boolean)
 Fetches the script text from the given filename. 
- Debug: <br>
- filename: <br>
Returns: 


##### M:Kltv.Kombine.KombineScript.LoadReferencedAssembly(System.Reflection.Assembly)
 Forces the loading of all the referenced assemblies from kombine to be available for the scripts This could be tweaked to force only the loading of the desired assemblies to be available for the scripts TODO: To be optimized 
- assembly: Assembly to get the references<br>

# T:Kltv.Kombine.KombineScript.SourceResolver


##### M:Kltv.Kombine.KombineScript.SourceResolver.NormalizePath(System.String,System.String)

- path: <br>
- baseFilePath: <br>
Returns: 


##### M:Kltv.Kombine.KombineScript.SourceResolver.OpenRead(System.String)

- resolvedPath: <br>
Returns: 


##### M:Kltv.Kombine.KombineScript.SourceResolver.ReadText(System.String)

- resolvedPath: <br>
Returns: 


##### M:Kltv.Kombine.KombineScript.SourceResolver.ResolveReference(System.String,System.String)
 Resolve a source reference. We will resolve following an order (in case is relative): CurrentDirectory ScriptDirectory Backtrace directories Tool directory In case the given path is absolute, we will just return it if it exists In case the given path is a URL, we will just return it. The resolved path is converted into an absolute path following the host os conventions. 
- path: The path and filename for the reference to be resolved<br>
- baseFilePath: The base file path in case is used.<br>
Returns: The resolved path or null if cannot be resolved.


# T:Kltv.Kombine.ChildProcess
 Helper class to execute and obtain results from third party child processes 

##### P:Kltv.Kombine.ChildProcess.AbortExecution
 This will be triggered if the user pressed ctrl+z so all executions will be skipped 

##### F:Kltv.Kombine.ChildProcess.CurrentRunningProcessesLock
 Static lock object to protect the list of running processes 

##### F:Kltv.Kombine.ChildProcess.CurrentRunningProcesses
 Static list of running processes 

##### M:Kltv.Kombine.ChildProcess.KillAllChilds
 Destroys a childprocess instance. Is just intended to kill the child process if we exit 

##### P:Kltv.Kombine.ChildProcess.Arguments
 Arguments that will be passed to the new process Arguments should be space separated in the same way you specify in a command line 

##### P:Kltv.Kombine.ChildProcess.CmdLine
 Commandline to execute 

##### P:Kltv.Kombine.ChildProcess.Name
 Name to identify this process (its extracted from the commandline and represent the filename without extension) 

##### P:Kltv.Kombine.ChildProcess.UseShell
 Indicates if the process should be launched through a shell. By default is false and use it on true is not recomended unless the process you want to run is a shell itself with an script or a link with schema for another application Anyway, always is better to use as command the self itself and pass the parameters as arguments This property should be avoided unless you know what you're doing and you're sure that you need it 

##### P:Kltv.Kombine.ChildProcess.ExitCode
 Exitcode value returned by the execution if its completed 

##### P:Kltv.Kombine.ChildProcess.ProcessTime
 Time elapsed in milliseconds comprising the execution time from start to end. 

##### P:Kltv.Kombine.ChildProcess.OutputCapture
 If set to false we will let the application output without any control 

##### P:Kltv.Kombine.ChildProcess.OutputCharCapture
 If set, we will capture using the streams at char level without any modification (no encoding) 

##### P:Kltv.Kombine.ChildProcess.Id
 A Public object Id which may be used to identify concurrent process operations (user property) 

##### P:Kltv.Kombine.ChildProcess.UserData
 A Public object for user data which may be used to hold any class / callback reference 

# T:Kltv.Kombine.ChildProcess.pOnProcessExit
 Delegate type to be called when the process is exited 
- proc: <br>

##### P:Kltv.Kombine.ChildProcess.OnProcessExit
 Delegate property to be called once the process is finished 

##### F:Kltv.Kombine.ChildProcess.ProcessInfo


##### F:Kltv.Kombine.ChildProcess.ProcessHandle


##### F:Kltv.Kombine.ChildProcess.OutputStd


##### F:Kltv.Kombine.ChildProcess.OutputErr


##### F:Kltv.Kombine.ChildProcess.ProcessExited


##### F:Kltv.Kombine.ChildProcess.Outstreams
 Class to intercept the output streams in raw format without altering the contents. 

##### M:Kltv.Kombine.ChildProcess.#ctor(System.String,System.String)
 Constructs a new Child process object 
- CmdLine: Command line to execute. May include absolute/relative path and/or process name.<br>
- Arguments: Arguments to be passed into the new process<br>

##### M:Kltv.Kombine.ChildProcess.GetOutput
 Fetch the contents of the stdout of the child process 
Returns: A string array with stdout content line by line


##### M:Kltv.Kombine.ChildProcess.GetErrors
 Fetch the contents of the stderr of the child process 
Returns: A string array with stderr content line by line


##### M:Kltv.Kombine.ChildProcess.WaitExit
 Wait for the process to exit without fetching the exit code 
Returns: 


##### M:Kltv.Kombine.ChildProcess.WaitExit(System.Int32@)
 Wait for the process to exit fetching the exit code 
- pExitCode: Variable to receive the exit code<br>
Returns: true if the process was launched, false otherwise


##### P:Kltv.Kombine.ChildProcess.Environment
 Environment variables to be passed to the child process 

##### M:Kltv.Kombine.ChildProcess.IsRunning
 Returns if the process is running or not 
Returns: True if stills running. False otherwise


##### M:Kltv.Kombine.ChildProcess.Kill
 Kills the running instance and cancel the reading. 
Returns: 


##### M:Kltv.Kombine.ChildProcess.GetExitCode(System.Int32@)
 Returns the exit code of the process If process has not been launched or not exited it returns false and -1 Valid exit code and true otherwise 
- pExitCode: Exit code or -1<br>
Returns: False if process has not been launched nor finished. True otherwise


##### M:Kltv.Kombine.ChildProcess.Launch(System.Boolean)
 Launch a configured process 
- KillPrevious: If previous process instances should be killed<br>
Returns: True if the process was launched, false otherwise.


##### F:Kltv.Kombine.ChildProcess.ProcessStartTime
 Holds the start time since cannot be fetched on exiting delegate It raises an exception at least on macos 

##### M:Kltv.Kombine.ChildProcess.ExitedExecutionHandler(System.Object,System.EventArgs)
 Handle Exited event Obtain exit code, signal process termination and dispose the process handle 
- sender: Sender object<br>
- e: Event arguments<br>

# T:Kltv.Kombine.ChildProcess.OnOutContent
 Delegate type to be called when a new line is received from the child process 
- line: <br>

##### P:Kltv.Kombine.ChildProcess.OnStderrLine
 Set a delegate to be called when a new line is received from the child process 

##### P:Kltv.Kombine.ChildProcess.OnStdoutLine
 Set a delegate to be called when a new line is received from the child process 

##### M:Kltv.Kombine.ChildProcess.StdoutOutputHandler(System.Object,System.Diagnostics.DataReceivedEventArgs)
 Std Output Handler 
- sendingProcess: object representing the process<br>
- outLine: data<br>

##### M:Kltv.Kombine.ChildProcess.StderrOutputHandler(System.Object,System.Diagnostics.DataReceivedEventArgs)
 Std Error Handler 
- sendingProcess: object representing the process<br>
- outLine: data<br>

##### M:Kltv.Kombine.ChildProcess.KillPrevious
 Kill all the previous instances which process name equals to this one 

##### M:Kltv.Kombine.ChildProcessOutput.#ctor(System.Diagnostics.Process)
 Initializes the IO manager with the supplied process. 
- process: Process to be captured.<br>

##### M:Kltv.Kombine.ChildProcessOutput.StartProcessOutputRead
 Starts the background threads reading any output produced (standard output, standard error) that is produces by the running process. 

##### M:Kltv.Kombine.ChildProcessOutput.CheckForValidProcess
 Checks for valid (non-null Process), and optionally check to see if the process has exited. 
Returns: True, valid process was checked, false otherwise.


##### M:Kltv.Kombine.ChildProcessOutput.ReadStream(System.Int32,System.IO.StreamReader,System.Boolean)
 Read characters from the supplied stream, and accumulate them in the 'streambuffer' variable until there are no more characters to read. 
- firstCharRead: The first character that has already been read.<br>
- streamReader: The stream reader to read text from.<br>
- isstdout: if set to DefAttribute the stream is assumed to be standard output, otherwise assumed to be standard error.<br>

##### M:Kltv.Kombine.ChildProcessOutput.NotifyAndFlushBufferText(System.Text.StringBuilder,System.Boolean)
 Invokes the OnStdoutTextRead (if isstdout==true)/ OnStderrTextRead events with the supplied streambuilder 'textbuffer', then clears textbuffer after event is invoked. 
- textbuffer: The textbuffer containing the text string to pass to events.<br>
- isstdout: if set to true, the stdout event is invoked, otherwise stedrr event is invoked.<br>

##### M:Kltv.Kombine.ChildProcessOutput.ReadStandardOutputThreadMethod
 Method started in a background thread (stdoutThread) to manage the reading and reporting of standard output text produced by the running process. 

##### M:Kltv.Kombine.ChildProcessOutput.ReadStandardErrorThreadMethod
 Method started in a background thread (stderrThread) to manage the reading and reporting of standard error text produced by the running process. 

##### M:Kltv.Kombine.ChildProcessOutput.StopMonitoringProcessOutput
 Stops both the standard input and stardard error background reader threads (via the Abort() method) 

# T:Kltv.Kombine.KombineMain
 Kombine main class 

##### M:Kltv.Kombine.KombineMain.Main
 Kombine Parameters: mkb [parameters] [action] [action parameters] [parameters] They are optional and can be any of the following: -ksdbg: Script will include debug information so script debugging will be possible -ksrb or -ksrebuild: Script will be rebuilded even if it is cached -ko:silent or -ko:s : Output will be silent -ko:normal or -ko:n : Output will be normal -ko:verbose or -ko:v : Output will be verbose -ko:debug or -ko:d : Output will be debug -kfile: Indicates which script file we should execute (default kombine.csx) [action] Action to be executed. If not specified the default action is "khelp" The action is used to specify which function in the script should be called after evaluation but there are some reserved actions for the tool itself which cannot be used for the scripts: kversion: Shows tool version and exit khelp: Show this help and exit kconfig: Manages the tool configuration (not yet implemented) kcache: Manages the tool cache (not fully implemented) [action parameters] They are optional and belongs to the specified action. In case of scripts,they are passed to the executed function as parameters. 
 ------------------------------------------------------------------------------------------------------------- 
##### M:Kltv.Kombine.KombineMain.RunScript(System.String,System.String,System.String[],System.Boolean)
 Executes one script 
- script: Script to be executed<br>
- action: Action to be executed in the script<br>
- args: Arguments for the action.<br>
- changedir: If the current folder should be changed to enter where the script is.<br>
Returns: 


##### M:Kltv.Kombine.KombineMain.CancelExecutionHandler(System.Object,System.ConsoleCancelEventArgs)
 This handler is used to trap from the actual Kombine console an exit event that means, ctrl+c ... the idea is to add a gracefully exit 
- sender: <br>
- args: <br>

##### P:Kltv.Kombine.KombineMain.CurrentRunningScript
 Holds the current running script 

# T:Kltv.Kombine.DictionaryExtensions
 Dicionary extensions 

##### M:Kltv.Kombine.DictionaryExtensions.Clone``2(System.Collections.Generic.Dictionary{``0,``1})
 Dictionary clone method 
##### Type Parameter: TKey
Key type
##### Type Parameter: TValue
Value type
- dictionary: Dictionary to clone<br>
Returns: A new dictionary instance with all the contents cloned


# T:Kltv.Kombine.ScriptAbortException
 Internal exception type to signalize when the script should be aborted 

# T:Kltv.Kombine.FSAPI
 This class implements the calls to FileSystem API wrapping exception handling but also Native PInvoke calls that may be required to achieve operations in some OS. 

##### M:Kltv.Kombine.FSAPI.SetCurrentFolder(System.String)

- folder: <br>
Returns: 


##### M:Kltv.Kombine.FSAPI.GetCurrentFolder
 Returns the current working folder 
Returns: The working folder as string


##### M:Kltv.Kombine.FSAPI.GetProcessFolder
 Returns the folder where the process is running 
Returns: Path to the folder or empty string if error.


##### M:Kltv.Kombine.FSAPI.FileExists(System.String)

- filename: <br>
Returns: 


##### M:Kltv.Kombine.FSAPI.FolderExist(System.String)
 Returns if a folder exists 
- folder: Folder to be checked<br>
Returns: True if it exists. False otherwise


##### M:Kltv.Kombine.FSAPI.GetModifiedTimeUTC(System.String)
 Returns the unix timestamp of the last modified time of a given file in UTC 
- filename: Filename to retrieve the modified time.<br>
Returns: The file modification time.


##### M:Kltv.Kombine.FSAPI.GetFileSize(System.String)
 Returns the size of a given file or -1 if invalid 
- filename: filename to fetch the size<br>
Returns: The size of the file.


##### M:Kltv.Kombine.FSAPI.ReadTextFile(System.String,System.Boolean)
 Read a text file and returns the contents as string 
- filename: The filename to use<br>
- ExitIfError: If the function should automatically quit on error. Default false.<br>
Returns: A string with the file contents or empty.


##### M:Kltv.Kombine.FSAPI.WriteTextFile(System.String,System.String,System.Boolean)
 Writes a text file with the given content 
- filename: The filename to use<br>
- content: The content to be written.<br>
- ExitIfError: If the function should automatically quit on error. Default false<br>
Returns: True if operation was okey, false otherwise. 


##### M:Kltv.Kombine.FSAPI.InterpretException(System.Exception)
 Prints a warning message with the exception 
- ex: Exception to be show.<br>


