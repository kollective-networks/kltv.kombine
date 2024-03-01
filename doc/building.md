[Back to the readme](../readme.md)

# Building Kombine

This documented is intended for people which wants to build Kombine by themselves, to modify it or to add new features.
First, Kombine is not being built with Kombine. Even if its totally possible we had no intentions to use C# as a production language, hence we decided to not spent time creating the corresponding Kombine extensions to deal with [the CSC](https://learn.microsoft.com/en-us/answers/questions/1138661/how-can-i-use-csc-exe--net-framework-executable).

In case of CSC is not a easy task since you need to append all the different references to be consideer into the assembly building. Another option is just call "dotnet build" but just to execute that a Kombine script is not required. If you add a CSC extension to build directly C# without mess with the constrainst of Dotnet we encourage you to share it (so all the rest can tweak the build process).

Anyway Kombine is used in the Kombine building process partially, see [generating the packages](#generating-the-packages)

## Requisites and recomended environment

In order to build Kombine then you need Dotnet SDK, to be more specific, Dotnet 8. 
You can gran your copy from [here at Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Anything else is required since Kombine is only pure C# managed code.

In the root of the repository a .vscode folder is present, so, if you use Visual Studio Code you can benefit from the tasks / launch we already have.

Once you have cloned this repository and you have Dotnet 8 installed, build Kombine is easy as:
```dotnet build``` or ```dotnet build -r yourplatform here```
We provided a *"directory.build.props"* file so everything from build is stored into an "out" folder with the following structure:

```
out/bin
out/bin/linux-x64/debug
out/bin/linux-x64/release
out/bin/osx-64/debug
out/bin/osx-64/release
out/bin/win-64/debug
out/bin/win-64/release
```

By default it will build the debug configuration, you can pass -c Release to build the release one to the Dotnet command line. By default it will build with the OS you're using but you can pass the -r your-runtime to specify which target OS you want to generate.

## Generating the packages

For this case we use Kombine. There is one Kombine script in the root of the repository which supports two actions:

- "intellisense": This one copies the reference assembly to the example folders just to automatize when we're adding things to the API and we wanted to have them on intellisense quickly as well.
- "publish": This one builds in release for the three target OS (Windows, Linux and Mac OSX) the two flavors (unpacked and single file). It generates the diferent packages (.tar.gz / zip) including the reference assembly as well.

All the packages are dropped into /out/pkg/

Version build number is generated automatically.

The publish action generates the configuration and updates the doc/api.md file with the latest's changes using an xml to markdown extension which is on the scripts folder (pretty simple and ugly yet)

## Source code structure

Source structure is very intuitive.

```
src/api contains what is exposed to the scripts (types and methods)
src/cache the tiny code to manage built assemblies cache
src/core the tool configuration and command line / script state
src/exec the tool executor and script executor
util/ has some extension methods and other things are not being used but lying there just in case.
```

Any extension is welcome.

[Back to the readme](../readme.md)