[Back to the readme](../readme.md)

## Why another build system

Nowadays we've a lot of build systems so, why we need another? Well, we just wanted to have something which fits our needs and during the research to find the appropiate tool we encountered a lot of things we don't like, ending up on the decision to create our own build system. 
We analyzed: Msbuild, Cmake, make, nmake, meson, fastbuild, Cake,Ninja,Autotools,Gradle, Gyp/GN, Premake... and others and every single tool failed with one or more items.

As a brief recap, that's what we want for a build system:

As baseline:

- Avoid build properties hell. Just same language, same description. Others incorporate different syntaxs depending on the file you need to edit for your build and even more, redundant and poorly documented build variables and properties (MSbuild, looking at you).
- Do not create empty folders without asking anything on different places even if we told you we want the files in a different place. That is, freedom to chose where you want your building artifacts.
- Maintanin your source code as clean as we can. The less files the better. Others requires a damn file on each damn folder to do its job.. who said CMake?
- We want a build system, not a generator. If i need to download your tool, and then other three tools and i need to "generate" and later "build" (creating a ton of non sense transpiled files in between) then, we don't like you since it adds extra complexity, points of failure and more. (Hello GN/Gyp/Meson!)
- Do not pray to our gods writting a simple thing (like to check for a folder presence) and even if you **make** it work, it won't do it for other platforms. This case is just weird. Let us put an example from make, in this case, create a folder / delete files:
```
ifeq ($(shell echo "check_quotes"),"check_quotes")
   WINDOWS := yes
else
   WINDOWS := no
endif

ifeq ($(WINDOWS),yes)
   mkdir = mkdir $(subst /,\,$(1)) > nul 2>&1 || (exit 0)
   rm = $(wordlist 2,65535,$(foreach FILE,$(subst /,\,$(1)),& del $(FILE) > nul 2>&1)) || (exit 0)
   rmdir = rmdir $(subst /,\,$(1)) > nul 2>&1 || (exit 0)
   echo = echo $(1)
else
   mkdir = mkdir -p $(1)
   rm = rm $(1) > /dev/null 2>&1 || true
   rmdir = rmdir $(1) > /dev/null 2>&1 || true
   echo = echo "$(1)"
endif
```
In Kombine:

Folders.CreateFolder(yourfolder here) or even Folders.CreateFolders(your folder array here). Works out of the box in all platforms. Looks like 40 years of "make" improvements was not enough to add a cross platform create folder function.

- Be crossplatform out of the box. Following the previous point. We want simplicity and ease of use. The common functionality which is desired (deal with files, folders, tools, output, etc) should work out of the box for all platforms. For example to deal with a text file creation:

```
# Generate JSON compilation database (compile_commands.json) by merging fragments
$(BUILD_DIR_ROOT)/compile_commands.json: $(COMPDBS)
	@echo "Generating: $@"
	@mkdir -p $(@D)
	@printf "[\n" > $@
	@sed -e '$$s/$$/,/' -s $(COMPDBS) | sed -e '$$s/,$$//' -e 's/^/    /' >> $@
	@printf "]\n" >> $@
```

It requires you to be on an environemnt with "print" and "sed". Also the "mkdir" will not work directly on all the platforms. You need to learn the "print" syntax and "sed" syntax (which is far to be self explanatory). You need to download those tools if you don't have it (and the rule "hey, use linux, those tools are included" is not valid since we wanted to build on windows, linux and osx), maybe there are differences between tool versions..
Maybe you think you're cool beacause you perfectly understand the above code lines but i think you're dumb because you spent time learning something just to do a simple task that can be achieved in an easy way. Don't get me wrong. There is anything against sed or print, just pointed that is dumb to use a building system that forces you to use/learn them just to achieve a simple thing like write a text file on disk..

- Just a single executable if possible or self contained. Do not ask me to download your build system and on the requirements the first line is "install python X version". I don't want python, or maybe i wanted, but what i want now is a build system. What if the version you require conflicts with something on my machine? Should i create different shell files just to initialize a bunch of environment variables to use your build tool? This is a no no. We don't want to install another language and tools just to do a build. If i code C++ i want my build tool, clang and nothing else. Less things to add, less things to maintain, update, configure and invest time to install / solve configurations isues and the rest. (Hi again Meson!)
- Be consistent across shells, that's mean, do not rely on shell because the building tool may broke in other systems / future. What if i use a different shell? or configured other way? There are a ton of shells out there, rely on the shell and even more, launch a shell for every single command is just weird in 2022. (Special mention to make here..)
- Hability to be extended to support new use cases / new tools integration. This is related to "include" definitions from your building scripts. Why i cannot fetch building scripts from http? why i cannot just put "include whatever" and give me options to locate the file in different places? 
- Hability to find files and work with folders / compress / download / upload..
- Zero config, just drop the tool, place it on path and start working at least for the normal use cases.
- Be the consistent toolchain, we don't want a different one for every single target system even if the compiler / linker or others are not the same. That means, adapt to whatever is required to be launched to achieve the build. Hability to interpret the results and act in consequence.
- Do not put constrains on the naming conventions you want to use.
- Do not fetch by default dependencies from Internet downloading a ton of non controlled dependencies that maybe you don't want. The new fashion is to create a ".whatever" folder in your home and download there a ton of things in an uncontrolled way. Perfect, maybe for you.
- No frameworks, libraries or other dependencies apart from the tools to execute your scripts (compiler, linker, etc).
- Do not send anything outside, we're tired of "Telemetry" (Hi Microsoft! Hi Apple!)
- Do not break functionality with newer versions (fastbuild, looking at you, you were promising and once you stop allowing some conditionals)

As a plus:

- Support all the different steps of the building/deploy/pack process, not only compile+link and maybe "launch things" which ends up adding more and more tools in top with its own configuration (more files in source tree) with jumps from one tool to another.
- Be reasonabily fast. To clarify, "speed" is a bit of "myth" on building systems (you could remember things like "make is faster than cmake") well, unless the build system is really really bad designed the penalty added from the build system compared to the compilers / linkers to do the build should be despreciable when we talk about a real build (not just build two files as example).
- Do distributed building out of the box.
- Hability to manage files (parse / add / change) for example for version numbering / building options and others.
- Offer an efficient and consistent way to expose building options without requiring another tool to do so.
- Hability to manage packed files or create testing images, for example qcow2 for qemu
- Hability to apply patches or other source transformations if required.
- Be consistent regarding include folders / library folders and others, avoid repeating those definitions again and again and again across the project
- Be simply and clever, we don't want 20 files in the root folder of the project because every single tool requires its own file with its own shit.
- Add the hability to launch complex debug environments from the tool itself so you just need to attach the debugger and that's all.
- No Java (this does not required any explanation at all)
- Integrate also with unit testing and other linter / coverage / security scan steps
- Be prepared to act as a Build server / CI / CD tool
- Hability to work with different technologies, avoid "one script type" for web frontend and "another script type" for the backed and so on.

[Back to the readme](../readme.md)