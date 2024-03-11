[Back to the readme](../readme.md)

# Features

This is the complete (planned) feature list and current state

| Feature Area                              | Feature Description                                          |
| :---------------------------------------- | :----------------------------------------------------------- |
| Script Output                             | :heavy_check_mark: Print with debug levels                   |
|                                           | :heavy_check_mark: Print with color Errors and Warnings      |
|                                           | :heavy_check_mark: Print with module information for debug   |
|                                           | :heavy_check_mark: Print task messages with colors for done/warning/error |
|                                           | :heavy_check_mark: Add/Remove indentation                    |
|                                           | :heavy_check_mark: Print progress bar                        |
|                                           | :heavy_check_mark: Output to console                         |
|                                           | :x: Output to file                                           |
| Built-in Properties                       | :heavy_check_mark: Current Working Folder                    |
|                                           | :heavy_check_mark: Current Script Folder                     |
|                                           | :heavy_check_mark: Current Tool Folder                       |
|                                           | :heavy_check_mark: Parent Script Folder                      |
| KValue Type                               | :heavy_check_mark: String initializer                        |
|                                           | :heavy_check_mark: Addition operator of KValues & strings    |
|                                           | :heavy_check_mark: Substract operator of KValues & strings   |
|                                           | :heavy_check_mark: Reduce redundant whitespaces              |
|                                           | :heavy_check_mark: Comparison operators with KValue & strings |
|                                           | :heavy_check_mark: AsFolder returns the value as folder if can be interpreted that way |
|                                           | :heavy_check_mark: HasExtension returns if the value can be intepreted as file and has a given extension |
|                                           | :heavy_check_mark: GetHashCode64 returns an immutable hash code in 64bits for the value |
|                                           | :heavy_check_mark: IsEmpty returns if its empty or not       |
|                                           | :heavy_check_mark: ToArray method to return a KList from space separated or indicating separators |
|                                           | :heavy_check_mark: ToArgs method to return a KList from space separated respecting double quotes |
|                                           | :heavy_check_mark: Export KValue to environment for child scripts and processes |
|                                           | :heavy_check_mark: Import KValue from environment with default value if is not present |
|                                           | :heavy_check_mark: Static function to escape KValues to be used in command line |
| KList Type                                | :heavy_check_mark: Static array initializer                  |
|                                           | :heavy_check_mark: String initializer                        |
|                                           | :heavy_check_mark: Addition operator of KValues & KList      |
|                                           | :heavy_check_mark: Substract operator of KValues             |
|                                           | :heavy_check_mark: Substract operator of KList               |
|                                           | :heavy_check_mark: Comparison operator                       |
|                                           | :heavy_check_mark: Add method                                |
|                                           | :heavy_check_mark: Remove method                             |
|                                           | :heavy_check_mark: Indexer operator []                       |
|                                           | :heavy_check_mark: Flatten the list into one KValue space separated |
|                                           | :heavy_check_mark: Flatten the list into one KValue indicating the character to be used as separator |
|                                           | :heavy_check_mark: Search for duplicates on the list         |
|                                           | :heavy_check_mark: Remove duplicates from the list           |
|                                           | :heavy_check_mark: AsFolders to retrieve a list of unique folders from a list of files. |
|                                           | :heavy_check_mark: WithPrefix to add a prefix to all the members in the list |
|                                           | :heavy_check_mark: WithExtension to change the extension in all the items if they can be consider as files |
|                                           | :heavy_check_mark: WithReplace to replace one substring by other in all the items |
|                                           | :heavy_check_mark: Explicit conversion from KList to KValue (Flatten) |
|                                           | :heavy_check_mark: Explicit conversion from KList to string (Flatten) |
|                                           | :heavy_check_mark: Explicit conversion from KList to string[] |
|                                           | :heavy_check_mark: Copy Constructor to duplicate a KList     |
| Action Arguments API                      | :heavy_check_mark: Contains function for the action arguments (Starting Script) |
|                                           | :heavy_check_mark: Get function for the action arguments (Starting Script) |
|                                           | :heavy_check_mark: Contains function for the action arguments (Current Script) |
|                                           | :heavy_check_mark: Get function for the action arguments (Current Script) |
| Sharing API                               | :heavy_check_mark: Share objects for child scripts           |
|                                           | :heavy_check_mark: Object property caster for different types |
| Registry API                              | :heavy_check_mark: Register values for script execution in all scopes (propagated for all the following executions) |
| Host Environment API                      | :heavy_check_mark: Check if script is running under root/admin privileges |
|                                           | :heavy_check_mark: Check if script is running under interactive console |
|                                           | :heavy_check_mark: Get a friendly string from the Host OS (win/lnx/osx) |
|                                           | :heavy_check_mark: Check if its windows/linux/macos          |
|                                           | :x: Check if the operating system meets some version         |
|                                           | :heavy_check_mark: Get the number of available CPU cores     |
| Files API                                 | :heavy_check_mark: Check if file exists                      |
|                                           | :heavy_check_mark: Delete file                               |
|                                           | :heavy_check_mark: Get File Size                             |
|                                           | :heavy_check_mark: Read text file into one KValue            |
|                                           | :x: Read text file into a KList of KValues (one per line)    |
|                                           | :heavy_check_mark: Write text file from one KValue           |
|                                           | :x: Write text file from one KList of KValues (one per line) |
|                                           | :heavy_check_mark: Get the modification time in seconds      |
|                                           | :heavy_check_mark: Compare files                             |
|                                           | :heavy_check_mark: Copy a file                               |
|                                           | :heavy_check_mark: Rename a file                             |
|                                           | :heavy_check_mark: Move a file                               |
|                                           | :heavy_check_mark: Glob static function to retrieve a list of matching files from pattern and current folder |
|                                           | :x: Patch a file (diff)                                      |
|                                           | :x: Generate patch file (diff)                               |
|                                           | :heavy_check_mark: JsonFile encapsulation (load/save)        |
|                                           | :x: XML File encapsulation (load/save)                       |
| Folders API                               | :heavy_check_mark: Create a folder                           |
|                                           | :heavy_check_mark: Check if folder exists.                   |
|                                           | :heavy_check_mark: Create folders from a list                |
|                                           | :heavy_check_mark: Delete folder with or without recursion   |
|                                           | :heavy_check_mark: Delete folders from a list with or without recursion |
|                                           | :heavy_check_mark: Get current working folder                |
|                                           | :heavy_check_mark: Get current script folder                 |
|                                           | :heavy_check_mark: Get parent script folder                  |
|                                           | :heavy_check_mark: Folder stack, push/pop                    |
|                                           | :heavy_check_mark: Set current folder with or without using the stack |
|                                           | :heavy_check_mark: Get Kombine binary folder                 |
|                                           | :heavy_check_mark: Copy directories with multiple copy options (subfolders, only modified, only folders, sync..) |
|                                           | :x: Search for a file in the filesystem                      |
|                                           | :heavy_check_mark: Search for a file path back               |
|                                           | :heavy_check_mark: Search for a file path forward            |
|                                           | :heavy_check_mark: RealPath static function to retrieve the real underlying path of a relative/absolute path |
| Script execution                          | :heavy_check_mark: Execute child scripts without creating a new process |
|                                           | :heavy_check_mark: Execute child scripts without creating a new process (with auto search) |
|                                           | :x: Execute child scripts creating a new process             |
|                                           | :x: Execute child scripts creating a new process (with auto search) |
|                                           | :heavy_check_mark: Include other scripts from your own from filesystem (with auto search) |
|                                           | :heavy_check_mark: Include other scripts from your own from HTTP source |
| Tool execution                            | :heavy_check_mark: Execute a process (simple, fetching only exit code) |
|                                           | :x: Execute a process using shell                            |
|                                           | :heavy_check_mark: Execute a process in sync way fetching all the results |
|                                           | :heavy_check_mark: Execute a process in async way fetching all the results (in delegate) |
|                                           | :heavy_check_mark: Add a process into a queue to be executed |
|                                           | :heavy_check_mark: Limit the number of concurrent process we shall launch |
|                                           | :heavy_check_mark: Execute the queued processes fetching all the results (in delegate or when returned) |
| Compression API                           | :heavy_check_mark: Compress a folder into a zip file         |
|                                           | :heavy_check_mark: Compress folders into a zip file          |
|                                           | :heavy_check_mark: Compress a file into a zip file           |
|                                           | :heavy_check_mark: Uncompress a zip file                     |
|                                           | :heavy_check_mark: Compress a folder into a tar.gz file      |
|                                           | :heavy_check_mark: Compress folders into a tar.gz file       |
|                                           | :heavy_check_mark: Compress a file into a tar.gz file        |
|                                           | :heavy_check_mark: Uncompress a tar.gz file                  |
|                                           | :heavy_check_mark: Compress a folder into a tar.bz2 file     |
|                                           | :heavy_check_mark: Compress folders into a tar.bz2 file      |
|                                           | :heavy_check_mark: Compress a file into a tar.bz2 file       |
|                                           | :heavy_check_mark: Uncompress a tar.bz2 file                 |
|                                           | :heavy_check_mark: Compress a folder into a tar.lz file      |
|                                           | :heavy_check_mark: Compress folders into a tar.lz file       |
|                                           | :heavy_check_mark: Compress a file into a tar.lz file        |
|                                           | :heavy_check_mark: Uncompress a tar.lz file                  |
|                                           | :heavy_check_mark: Uncompress a tar.xz file                  |
| Network API                               | :heavy_check_mark: Download a file from HTTP source (GET)    |
|                                           | :heavy_check_mark: Download multiple files from HTTP sources (GET) |
|                                           | :x: Upload a file to HTTP destination (POST/PUT/PATCH)       |
|                                           | :x: Upload multiple files to HTTP destination (POST/PUT/PATCH) |
|                                           | :heavy_check_mark: Get resource from HTTP source (GET)       |
|                                           | :x: Post resource to HTTP destination (POST)                 |
|                                           | :x: Delete a file from HTTP destination (DELETE)             |
|                                           | :x: Support credentials injection from configuration         |
|                                           | :x: Support configuration of headers                         |
| Tool Configuration                        | :x: Allow configure credentials to be used in networking     |
|                                           | :x: Allow configure default output for the tool              |
|                                           | :x: Allow Configure default log level for the tool           |
|                                           | :x: Allow configure third party assemblies permitted         |
|                                           | :x: Allow configure if network API is allowed                |
|                                           | :x: Allow configure if Kombine tool can act as build server  |
|                                           | :x: Allow configure the Kombine tool in build server way: port, certificates,etc. |
|                                           | :x: Allow configure the Kombine tool to be used in distributed building |
| Cache Management                          | :heavy_check_mark: Allow clear the complete build scripts cache |
|                                           | :heavy_check_mark: Allow clear your current build script cached file |
|                                           | :x: Garbage collection of cached files not used anymore      |
|                                           | :x: Garbage collection of cached files not used in the last month |
|                                           | :x: Allow clear downloaded scripts from http sources cache   |
| Building options (Projects configuration) | :x: Allow expose interactive dialogs to configure the project |
|                                           | :x: Manage dictionaries for project configuration settings   |
|                                           | :x: Store configuration settings / export configuration settings |
| Distributed building                      | :x: Detect and use other Kombine tools in the network to distribute the build |
| Building server                           | :x: Allow the scripts to receive webhooks to execute actions in consequence |



[Back to the readme](../readme.md)