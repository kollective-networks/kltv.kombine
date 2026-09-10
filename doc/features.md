[Back to the readme](../readme.md)

# Features

This is the complete (planned) feature list and its current state.

Legend: :heavy_check_mark: implemented — :x: planned / not implemented yet.

## Script Output

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Print with debug levels |
| :heavy_check_mark: | Print with color for errors and warnings |
| :heavy_check_mark: | Print with module information for debug |
| :heavy_check_mark: | Print task messages with colors for done/warning/error |
| :heavy_check_mark: | Raw print without colors nor indentation |
| :heavy_check_mark: | Add/remove indentation |
| :heavy_check_mark: | Lock/unlock the output (to group messages from concurrent callbacks) |
| :heavy_check_mark: | Print progress bar |
| :heavy_check_mark: | Output to console |
| :x: | Output to file |

## Built-in Properties

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Current working folder |
| :heavy_check_mark: | Current script folder |
| :heavy_check_mark: | Current tool folder |
| :heavy_check_mark: | Parent script folder |

## KValue Type

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | String initializer and implicit conversion to/from string |
| :heavy_check_mark: | Addition operator of KValues & strings |
| :heavy_check_mark: | Subtract operator of KValues & strings |
| :heavy_check_mark: | Comparison operators with KValue & strings |
| :heavy_check_mark: | Reduce redundant whitespaces |
| :heavy_check_mark: | IsEmpty returns if it is empty or not |
| :heavy_check_mark: | AsFolder returns the value as folder if it can be interpreted that way |
| :heavy_check_mark: | GetParent returns the parent folder if the value can be interpreted as a path |
| :heavy_check_mark: | HasExtension returns if the value can be interpreted as file and has a given extension |
| :heavy_check_mark: | WithExtension returns the value as file with the extension changed |
| :heavy_check_mark: | WithNamePrefix returns the value as file with a prefix added to the filename |
| :heavy_check_mark: | GetHashCode64 returns an immutable 64-bit hash code for the value |
| :heavy_check_mark: | ToArray method to return a KList from space separated or indicating separators |
| :heavy_check_mark: | ToArgs method to return a KList from space separated respecting double quotes |
| :heavy_check_mark: | Export KValue to environment for child scripts and processes |
| :heavy_check_mark: | Import KValue from environment with default value if it is not present |
| :heavy_check_mark: | Static function to escape KValues to be used in command line |

## KList Type

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Static array initializer and collection initializer |
| :heavy_check_mark: | String initializer |
| :heavy_check_mark: | Addition operator of KValues & KList |
| :heavy_check_mark: | Subtract operator of KValues |
| :heavy_check_mark: | Subtract operator of KList |
| :heavy_check_mark: | Comparison operator |
| :heavy_check_mark: | Add method |
| :heavy_check_mark: | Remove method |
| :heavy_check_mark: | Contains method |
| :heavy_check_mark: | Count method |
| :heavy_check_mark: | Indexer operator [] |
| :heavy_check_mark: | Enumerable with foreach |
| :heavy_check_mark: | Flatten the list into one KValue space separated |
| :heavy_check_mark: | Flatten the list into one KValue indicating the separator to be used |
| :heavy_check_mark: | Search for duplicates in the list |
| :heavy_check_mark: | Remove duplicates from the list |
| :heavy_check_mark: | AsFolders to retrieve a list of unique folders from a list of files |
| :heavy_check_mark: | WithPrefix to add a prefix to all the members in the list |
| :heavy_check_mark: | WithExtension to change the extension in all the items that can be considered files |
| :heavy_check_mark: | WithReplace to replace one substring by another in all the items |
| :heavy_check_mark: | Implicit conversion from string / KValue / string[] to KList |
| :heavy_check_mark: | Implicit conversion from KList to KValue / string (Flatten) and string[] |
| :heavy_check_mark: | Copy constructor to duplicate a KList |

## Action Arguments API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Contains function for the action arguments (starting script) |
| :heavy_check_mark: | Get function for the action arguments (starting script) |
| :heavy_check_mark: | Contains function for the action arguments (current script) |
| :heavy_check_mark: | Get function for the action arguments (current script) |
| :heavy_check_mark: | WasRebuilded to know if the current script was compiled in this run (to trigger rebuild on compilations) |

## Sharing API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Share objects with child scripts |
| :heavy_check_mark: | Object property caster for different types (Cast) |
| :heavy_check_mark: | Dump the shared object pool for debugging |

## Registry API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Register values for the whole script execution in all scopes (propagated to all the following executions) |
| :heavy_check_mark: | Dump the registry content for debugging |

## Host Environment API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Check if the script is running under root/admin privileges |
| :heavy_check_mark: | Check if the script is running in an interactive console |
| :heavy_check_mark: | Get a friendly string for the host OS (win/lnx/osx) |
| :heavy_check_mark: | Check if it is Windows / Linux / macOS |
| :x: | Check if the operating system meets some version |
| :heavy_check_mark: | Get the number of available CPU cores |

## Version & Build Helpers

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Get the Kombine version (full, short, major, minor, hex) |
| :heavy_check_mark: | Generate a time-based build number |

## Files API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Check if a file exists |
| :heavy_check_mark: | Delete a file |
| :heavy_check_mark: | Get file size |
| :heavy_check_mark: | Read a text file into one KValue |
| :x: | Read a text file into a KList of KValues (one per line) |
| :heavy_check_mark: | Write a text file from one KValue |
| :x: | Write a text file from a KList of KValues (one per line) |
| :heavy_check_mark: | Get the modification time in seconds |
| :heavy_check_mark: | Compare files (by size, time or contents) |
| :heavy_check_mark: | Copy a file (optionally only if newer) |
| :heavy_check_mark: | Rename a file |
| :heavy_check_mark: | Move a file |
| :heavy_check_mark: | Glob static function to retrieve a list of matching files from a pattern (current or given folder) |
| :x: | Patch a file (diff) |
| :x: | Generate a patch file (diff) |
| :heavy_check_mark: | JSON file encapsulation (load/save) |
| :x: | XML file encapsulation (load/save) |
| :heavy_check_mark: | YAML file encapsulation (load) |

## Folders API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Create a folder |
| :heavy_check_mark: | Check if a folder exists |
| :heavy_check_mark: | Create folders from a list |
| :heavy_check_mark: | Delete a folder with or without recursion |
| :heavy_check_mark: | Delete folders from a list with or without recursion |
| :heavy_check_mark: | Move a folder |
| :heavy_check_mark: | Copy directories with multiple copy options (subfolders, only modified, only folders, mirror...) |
| :heavy_check_mark: | Get the current working folder |
| :heavy_check_mark: | Get the current script folder |
| :heavy_check_mark: | Get the parent script folder |
| :heavy_check_mark: | Get the Kombine binary folder |
| :heavy_check_mark: | Folder stack, push/pop |
| :heavy_check_mark: | Set the current folder with or without using the stack |
| :x: | Search for a file in the filesystem |
| :heavy_check_mark: | Search for a file walking the path backwards |
| :heavy_check_mark: | Search for a file walking the path forward |
| :heavy_check_mark: | RealPath static function to retrieve the real underlying path of a relative/absolute path |

## Script Execution

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Execute child scripts without creating a new process |
| :heavy_check_mark: | Execute child scripts without creating a new process (with auto search) |
| :x: | Execute child scripts creating a new process |
| :x: | Execute child scripts creating a new process (with auto search) |
| :heavy_check_mark: | Include other scripts from the filesystem (with auto search) |
| :heavy_check_mark: | Include other scripts from an HTTP source (with cache) |
| :heavy_check_mark: | Transparent cache of compiled scripts |
| :heavy_check_mark: | Rebuild the script when it or its loaded dependencies changed |
| :heavy_check_mark: | Script debugging with the dotnet debugger (-ksdbg) |
| :heavy_check_mark: | Wait for a debugger to attach before executing the action (-ksdbgw) |

## Tool Execution

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Execute a process (simple, fetching only the exit code) |
| :heavy_check_mark: | Execute a process using the shell |
| :heavy_check_mark: | Execute a process in a sync way fetching all the results |
| :heavy_check_mark: | Execute a process in an async way fetching all the results (in delegate) |
| :heavy_check_mark: | Add a process into a queue to be executed |
| :heavy_check_mark: | Limit the number of concurrent processes to launch |
| :heavy_check_mark: | Execute the queued processes fetching all the results (in delegate or when returned) |
| :heavy_check_mark: | Echo the tool output to the console while it runs (CaptureOutput) |
| :heavy_check_mark: | Configure the expected exit code for success |
| :heavy_check_mark: | Child processes inherit the script environment (including exported values) |

## Compression API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Compress a folder / folders / a file into a zip file |
| :heavy_check_mark: | Uncompress a zip file |
| :heavy_check_mark: | Compress a folder / folders / a file into a plain tar file |
| :heavy_check_mark: | Compress a folder / folders / a file into a tar.gz file |
| :heavy_check_mark: | Compress a folder / folders / a file into a tar.bz2 file |
| :heavy_check_mark: | Compress a folder / folders / a file into a tar.lz file |
| :x: | Compress into a tar.xz file |
| :heavy_check_mark: | Uncompress tar files with format auto-detection (tar, tar.gz, tar.bz2, tar.lz, tar.xz) |

## Network API

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Download a file from an HTTP source (GET) |
| :heavy_check_mark: | Download multiple files in parallel from HTTP sources (GET) |
| :heavy_check_mark: | Show a progress bar during downloads |
| :heavy_check_mark: | Upload a file to an HTTP destination (POST/PATCH) |
| :x: | Upload multiple files to an HTTP destination (POST/PATCH) |
| :heavy_check_mark: | Get a resource from an HTTP source (GET) |
| :heavy_check_mark: | Post/patch a resource to an HTTP destination (POST/PATCH) |
| :heavy_check_mark: | Delete a document from an HTTP destination (DELETE) |
| :heavy_check_mark: | Support configuration of headers (valid for credentials also) |
| :heavy_check_mark: | Expose the last HTTP status code and response body |
| :x: | Support credentials injection from configuration |

## Tool Configuration

| State | Feature |
| :---: | :--- |
| :x: | Allow configuring credentials to be used in networking |
| :x: | Allow configuring the default output for the tool |
| :x: | Allow configuring the default log level for the tool |
| :x: | Allow configuring the third-party assemblies permitted |
| :x: | Allow configuring if the network API is allowed |
| :x: | Allow configuring if the Kombine tool can act as build server |
| :x: | Allow configuring the Kombine tool in build server mode: port, certificates, etc. |
| :x: | Allow configuring the Kombine tool to be used in distributed building |

## Cache Management

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | Clear the complete cache (kcache clear) |
| :heavy_check_mark: | Rebuild every script and module of the run ignoring the cached files (-ksrb) |
| :heavy_check_mark: | Scripts cached by content: a touched or moved script is not compiled again |
| :heavy_check_mark: | Loaded files compiled once per run as modules (#pragma kombine module), cached by content and shared by every script |
| :x: | Clear only the downloaded scripts (HTTP sources) cache |
| :x: | Garbage collection of cached files not used anymore |
| :x: | Garbage collection of cached files not used in the last month |

## Building Options (Projects Configuration)

| State | Feature |
| :---: | :--- |
| :x: | Allow exposing interactive dialogs to configure the project |
| :x: | Manage dictionaries for project configuration settings |
| :x: | Store / export configuration settings |

## Distributed Building

| State | Feature |
| :---: | :--- |
| :x: | Detect and use other Kombine tools in the network to distribute the build |

## Building Server

| State | Feature |
| :---: | :--- |
| :x: | Allow the scripts to receive webhooks to execute actions in consequence |

## Provided Extensions

Reusable build scripts shipped in the [extensions](../extensions/) folder — see
[extensions.md](extensions.md) for the documentation.

| State | Feature |
| :---: | :--- |
| :heavy_check_mark: | clang: compile, static libraries, executables and shared libraries, clang-format, compile_commands.json; output modes, results per verb, run wide counters, up to date checks by content hash and recorded command line |
| :heavy_check_mark: | clang.doc: Markdown documentation from clang-doc output |
| :heavy_check_mark: | git: clone, pull, status, hooks (patch not implemented yet) |
| :heavy_check_mark: | github: create releases, upload assets, publish |
| :heavy_check_mark: | bin2cpp: embed binary files into C++ sources, generated again by content |
| :heavy_check_mark: | bin2obj: embed binary files into COFF, ELF and Mach-O object files, the format and the machine of the host by default or set, generated again by content, reproducible objects |
| :heavy_check_mark: | modder: apply and verify modifications over third-party sources |
| :heavy_check_mark: | dotnet.doc: convert C# XML documentation to Markdown |

[Back to the readme](../readme.md)
