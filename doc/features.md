[Back to the readme](../readme.md)

# Features

This is the complete (desired) feature list and current state

- Script Output<br>
	:heavy_check_mark: Print with debug levels <br>
	:heavy_check_mark: Print with color Errors and Warnings <br>
	:heavy_check_mark: Print with module information for debug <br>
	:heavy_check_mark: Print task messages with colors for done/warning/error <br>
	:heavy_check_mark: Add/Remove indentation <br>
	:heavy_check_mark: Print progress bar <br>
	:heavy_check_mark: Output to console <br>
	:x: Output to file<br>
- Builtin Properties<br>	
	:heavy_check_mark: Current Working Folder<br>
	:heavy_check_mark: Current Script Folder<br>
	:heavy_check_mark: Current Tool Folder<br>
- KValue Type<br>
    :heavy_check_mark: String initializer<br>
	:heavy_check_mark: Addition operator of KValues & strings<br>
	:heavy_check_mark: Substract operator of KValues & strings<br>
	:heavy_check_mark: Reduce redundant whitespaces<br>
	:heavy_check_mark: Comparison operators with KValue & strings<br>
	:heavy_check_mark: AsFolder returns the value as folder if can be interpreted that way<br>
	:heavy_check_mark: HasExtension returns if the value can be intepreted as file and has a given extension<br>
	:heavy_check_mark: GetHashCode64 returns an immutable hash code in 64bits for the value<br>
	:heavy_check_mark: IsEmpty returns if its empty or not<br>
	:heavy_check_mark: ToArray method to return a KList from space separated or indicating separators<br>
	:heavy_check_mark: Export KValue to environment for child scripts and processes<br>
	:heavy_check_mark: Import KValue from environment with default value if is not present<br>
- KList Type<br>
	:heavy_check_mark: Static array initializer<br>
	:heavy_check_mark: String initializer<br>
	:heavy_check_mark: Addition operator of KValues & KList<br>
	:heavy_check_mark: Substract operator of KValues<br>
	:heavy_check_mark: Substract operator of KList<br>
	:heavy_check_mark: Comparison operator<br>
	:heavy_check_mark: Add method<br>
	:heavy_check_mark: Remove method<br>
	:heavy_check_mark: Indexer operator []<br>
	:heavy_check_mark: Flatten the list into one KValue space separated <br>
	:heavy_check_mark: Flatten the list into one KValue indicating the character to be used as separator <br>
	:heavy_check_mark: Search for duplicates on the list <br>
	:heavy_check_mark: Remove duplicates from the list <br>
	:heavy_check_mark: AsFolders to retrieve a list of unique folders from a list of files. <br>
	:heavy_check_mark: WithPrefix to add a prefix to all the members in the list <br>
	:heavy_check_mark: WithExtension to change the extension in all the items if they can be consider as files <br>
	:heavy_check_mark: WithReplace to replace one substring by other in all the items <br>
	:heavy_check_mark: Explicit conversion from KList to KValue (Flatten) <br>
	:heavy_check_mark: Explicit conversion from KList to string (Flatten) <br>
	:heavy_check_mark: Explicit conversion from KList to string[]<br>
- Action Arguments API<br>
	:heavy_check_mark: Contains function for the action arguments<br>
	:heavy_check_mark: Get function for the action arguments<br>
- Sharing API<br>
	:heavy_check_mark: Share objects for child scripts<br>
- Registry API<br>
	:heavy_check_mark: Register values for script execution in all scopes (propagated for all the following executions)
- Host Environment API<br>
	:heavy_check_mark: Check if script is running under root/admin privileges<br>
	:heavy_check_mark: Check if script is running under interactive console<br>
	:heavy_check_mark: Get a friendly string from the Host OS (win/lnx/osx)<br>
	:heavy_check_mark: Check if its windows/linux/macos<br>
	:x: Check if the operating system meets some version<br>
	:heavy_check_mark: Get the number of available CPU cores<br>
- Files API <br>
	:heavy_check_mark: Check if file exists <br>
	:heavy_check_mark: Delete file <br>
	:heavy_check_mark: Get File Size <br>
	:heavy_check_mark: Read text file into one KValue <br>
	:x: Read text file into a KList of KValues (one per line) <br>
	:heavy_check_mark: Write text file from one KValue <br>
	:x: Write text file from one KList of KValues (one per line) <br>
	:heavy_check_mark: Get the modification time in seconds <br>
	:heavy_check_mark: Compare files <br>
	:heavy_check_mark: Copy a file <br>
	:heavy_check_mark: Rename a file <br>
	:heavy_check_mark: Move a file <br>
	:heavy_check_mark: Glob static function to retrieve a list of matching files from pattern and current folder<br>
	:x: Patch a file (diff)<br>
	:x: Generate patch file (diff)<br>
	:heavy_check_mark: JsonFile encapsulation (load/save)<br>
	:x: XML File encapsulation (load/save)<br>
- Folders API<br>
	:heavy_check_mark: Create a folder<br>
	:heavy_check_mark: Check if folder exists.<br>
	:heavy_check_mark: Create folders from a list<br>
	:heavy_check_mark: Delete folder with or without recurse<br>
	:heavy_check_mark: Delete folders from a list with or withour recurse<br>
	:heavy_check_mark: Get current working folder<br>
	:heavy_check_mark: Get current script folder<br>
	:heavy_check_mark: Folder stack, push/pop <br>
	:heavy_check_mark: Set current folder with or without using the stack<br>
	:heavy_check_mark: Get kombine binary folder<br>
	:heavy_check_mark: Copy directories with multiple copy options (subfolders, only modified, only folders, sync..)<br>
	:x: Search for a file in the filesystem<br>
	:heavy_check_mark: Search for a file path back<br>
	:heavy_check_mark: Search for a file path forward<br>
	:heavy_check_mark: RealPath static function to retrieve the real underlying path of a relative/absolute path<br>
- Script execution<br>
	:heavy_check_mark: Execute child scripts without creating a new process.<br>
	:heavy_check_mark: Execute child scripts without creating a new process (with auto search).<br>
	:x: Execute child scripts creating a new process.<br>
	:x: Execute child scripts creating a new process (with auto search).<br>
	:heavy_check_mark: Include other scripts from your own from filesystem (with auto search)<br>
	:x: Include other scripts from your own from http source<br>
- Tool execution<br>
	:heavy_check_mark: Execute a process (simple, fetching only exit code)<br>
	:x Execute a process using shell<br>
	:heavy_check_mark: Execute a process in sync way fetching all the results<br>
	:heavy_check_mark: Execute a process in async way fetching all the results (in delegate)<br>
	:heavy_check_mark: Add a process into a queue to be executed<br>
	:heavy_check_mark: Limit the number of concurrent process we shall launch<br>
	:heavy_check_mark: Execute the queued processes fetching all the results (in delegate or when returned)<br>
- Compresion API<br>
	:heavy_check_mark: Compress a folder into a zip file<br>
	:heavy_check_mark: Compress folders into a zip file<br>
	:heavy_check_mark: Compress a file into a zip file<br>
	:heavy_check_mark: Compress a folder into a tar.gz file<br>
	:heavy_check_mark: Compress folders into a tar.gz file<br>
	:heavy_check_mark: Compress a file into a tar.gz file<br>
	:heavy_check_mark: Uncompress a zip file<br>
	:heavy_check_mark: Uncompress a tar.gz file<br>
- Network API<br>
	:heavy_check_mark: Download a file from HTTP source (GET)<br>
	:heavy_check_mark: Download multiple files from HTTP sources (GET)<br>
	:x: Upload a file to HTTP destination (POST/PUT/PATCH)<br>
	:x: Upload multiple files to HTTP destination (POST/PUT/PATCH)<br>
	:heavy_check_mark: Get resource from HTTP source (GET)<br>
	:x: Post resource to HTTP destination (POST)<br>
	:x: Delete a file from HTTP destination (DELETE)<br>
	:x: Support credentials injection from configuration<br>
	:x: Support configuration of headers<br>
- Tool Configuration<br>
	:x: Allow configure credentials to be used in networking<br>
	:x: Allow configure default output for the tool<br>
	:x: Allow Configure default log level for the tool<br>
	:x: Allow configure third party assemblies permited <br>
	:x: Allow configure if network api is allowed<br>
	:x: Allow configure if kombine tool can act as build server<br>
	:x: Allow configure the kombine tool in build server way (port, certificates,etc.)<br>
	:x: Allow configure the kombine tool to be used in distributed building<br>
- Cache Management<br>
	:heavy_check_mark: Allow clear the complete build scripts cache<br>
	:heavy_check_mark: Allow clear your current build script cached file<br>
	:x: Garbage collection of cached files not used anymore<br>
	:x: Garbage collection of cached files not used in the last month<br>
	:x: Allow clear downloaded scripts from http sources cache<br>
- Building options (Projects configuration)<br>
	:x: Allow expose interactive dialogs to configure the project<br>
	:x: Manage dictionaries for project configuration settings<br>
	:x: Store configuration settings / export configuration settings<br>
- Distributed building<br>
	:x: Detect and use other kombine tools in the network to distribute the build<br>
- Building server <br>
	:x: Allow the scripts to receive webhooks to execute actions in consequence<br>

[Back to the readme](../readme.md)