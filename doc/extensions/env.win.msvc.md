[Back to the extensions index](../extensions.md)

# env.win.msvc.csx

The environment of MSVC on Windows. The extension leaves the script environment with the
variables Visual Studio provides for a given version, and nothing of another one: `INCLUDE`,
`LIB`, `LIBPATH`, the `PATH` entries of the tools and the variables of the toolset and of the
Windows SDK, composed from the folders it found, never by running `vcvarsall.bat`. What runs on
that environment afterwards (cl, clang-cl, gn, ninja, msbuild) is the script's business. Requires
Kombine 1.7 (the file declares it with `#pragma kombine requires 1.7`). Demonstrated and tested by
[examples/06.extensions/06.env.win.msvc](../../examples/06.extensions/06.env.win.msvc/).

The name follows the scheme `env.<platform>.<toolchain>.csx` and the class mirrors it,
`EnvWinMsvc`: every environment extension owns one toolchain on one platform, on top of the
generic [`Env`](../api.md#environment-env) facility of the engine, and `Msvc` stays free for a
compiler extension, as `Clang` is the class of `clang.csx`.

```csharp
#load "extensions/env.win.msvc.csx"

EnvWinMsvc env = new EnvWinMsvc();
env.Options.Version = "17";               // Visual Studio 2022, any product; unset: the newest installed
env.Options.Sdk = "10.0.22621.0";         // this SDK exactly; unset: the newest installed
// Options.VcTools unset: the newest toolset of the instance, since this script does not care
env.Options.TargetArch = "x64";           // unset: the host architecture
env.Apply();                              // a requirement not met: a warning and the abort
Msg.Print("Toolchain: " + env.LastEnvironment.Toolset + " with SDK " + env.LastEnvironment.Sdk.Version);
```

## How it works

- **Requirements and defaults.** An option set is a requirement: met, or the call fails with a
  warning naming it and what exists. A version is required strict at the precision given
  (`17`, `14.38`, `10.0.22621.0`), as that or newer with a trailing plus (`17+`, `14.38+`), or as
  a range; the newest that meets it is taken. An option unset is ignored only as far as errors and
  warnings go: the extension still looks for that part, takes the newest installed when there is
  one and sets its variables, and sets nothing for it when there is none, silently. A script
  that does not care about the Visual Studio version leaves `Version` unset: version 18 is
  installed, so the variables of version 18 are set; on a machine without Visual Studio nothing
  is set for it and that is not an error. `SetUnspecified`, true by default, is the switch of
  that behaviour: false sets only the parts of the options the script set. `LastEnvironment`
  says which parts were found and set.
- **Not the batch file.** `vcvarsall.bat` decides on its own (the newest toolset without
  `-vcvars_ver`), is opaque and slow, misbehaves in a prompt that already ran it, and prints
  text to parse. What Visual Studio sets is a known function of the installation folder, the
  toolset version, the SDK folder and version and the two architectures: the extension composes
  the variables from them and checks that every folder it names exists (`cl.exe` and `link.exe`
  in the host tools, `windows.h`, `stdio.h`, `kernel32.lib` and `libucrt.lib` in the SDK,
  `msvcrt.lib` in the toolset). A missing file is an unmet requirement even when the version
  folder exists.
- **Clearing.** Before setting, `Apply` removes every variable of its own list that the
  environment already has (`INCLUDE`, `LIB`, `LIBPATH`, the `VS*`, `VC*`, `WindowsSdk*`, `UCRT*`,
  `Framework*` and `VSCMD_*` ones, `DevEnvDir`, `Platform`, ...) and every `PATH` entry under a
  Visual Studio or Windows Kits folder, so nothing of a previous toolchain survives next to the
  new one. The list is the extension's (`EnvWinMsvc.Variables`, `EnvWinMsvc.PathPatterns`),
  passed to the generic `Env.Clean` and `Env.RemovePath`; what was removed is in
  `LastEnvironment.Removed`, and `Restore` puts the whole environment back.
- **Discovery: several methods per component, never vswhere alone.** vswhere knows the products
  of the Visual Studio Installer and nothing else, the registry knows the SDKs, the folders know
  what is on disk, the environment knows what the current prompt selected, and a script knows
  what nobody registered. The extension queries every source, merges the candidates by their
  installation folder, keeps each one only if its key files exist, and records the sources that
  reported it; `List()` shows them all.

| Component | Sources, all of them queried |
| --- | --- |
| Visual Studio instances and their toolsets | `vswhere -products *` (the Build Tools are invisible without `-products *`); the instance records of the installer under `%ProgramData%\Microsoft\VisualStudio\Packages\_Instances`, what vswhere reads, usable without it; a folder scan of `Microsoft Visual Studio\*\*\VC\Tools\MSVC` under both Program Files; `VSINSTALLDIR` of the current environment; the `Path` option |
| Windows SDK and UCRT | the registry (`Microsoft SDKs\Windows\v10.0` and `Windows Kits\Installed Roots`, read with `reg.exe`); the version folders under the `Include` folder of the root; `WindowsSdkDir` of the current environment. The UCRT ships with the SDK (`Include\<version>\ucrt`) |
| Enterprise WDK and portable toolchains | the `Path` option; nothing is registered |

## Settings

| Member | Default | Meaning |
| --- | --- | --- |
| `EnvWinMsvcOptions Options` | see below | The requirements. |
| `bool AbortOnFailure` | true | A failing call prints its warning and aborts the script; false returns false with the reason in `LastError`. |
| `ApiError LastError` | none | The reason of the last failure: `NotFound` for an unmet requirement, `InvalidArgument` for a bad option or a `Restore` without `Apply`, `NotSupported` outside Windows. |
| `EnvWinMsvcResult? LastEnvironment` | null | What the last `Find` or `Apply` selected: `Instance`, `Toolset`, `ToolsetPath`, `Sdk`, `HostArch`, `TargetArch`, `Variables` (name and value), `PathEntries`, `Removed`, `HasToolset`, `HasSdk`. |
| `static string[] Variables` | the list | The variables cleared before setting. |
| `static string[] PathPatterns` | the list | The `PATH` entries removed before setting. |

| Option | Unset (no error, no warning: the newest installed is set when there is one, nothing when there is none) | Set (a requirement: met, or the call fails with a warning) |
| --- | --- | --- |
| `Version` | the newest Visual Studio installed | strict at the precision given (`17` is any 17.x, `17.8` any 17.8.x), that or newer with a trailing plus (`17+`), or a range (`[17.0,18.0)`, `(16.0,]`); the newest that meets it |
| `Product` | any: Enterprise, Professional, Community or BuildTools | that product |
| `VcTools` | the newest toolset of the instance found | strict at the precision given (`14.38` is any 14.38.x, `14.38.33130` only that), that or newer (`14.38+`), or a range; the newest of the instance that meets it |
| `Sdk` | the newest Windows SDK installed | strict at the precision given (`10.0.22621.0`, `10.0.22621`), that or newer (`10.0.22621+`), or a range; the newest that meets it, with its UCRT for the target |
| `HostArch` | the architecture of the machine | `x64`, `x86` or `arm64`, which must have host tools |
| `TargetArch` | the host architecture | `x64`, `x86`, `arm` or `arm64`, which the toolset and the SDK must provide |
| `Atl` | not required | `true`: the toolset must ship `atlmfc` |
| `Path` | discovery | an installation folder taken as it is, which must exist, for a layout nothing registers |
| `SetUnspecified` | `true`: the parts found for the unset options are set as well | `false`: only the parts of the options the script set are set |

## Verbs

| Method | Description |
| --- | --- |
| `bool Find()` | Discovers and selects without touching the environment. `LastEnvironment` holds the selection and the variables it would set. |
| `bool Apply()` | `Find`, then clears the variables of a previous toolchain and sets the selected ones. Nothing is touched when a requirement is not met. |
| `bool Restore()` | Puts back the environment `Apply` replaced. `InvalidArgument` when `Apply` was not called. |
| `void List()` | Prints every installation with its toolsets and every SDK found, with the sources that reported each one. |
| `List<VsInstance> Instances()` | The installations found, newest first: `Path`, `Version`, `Product`, `Toolsets`, `Sources`. |
| `List<WinSdk> Sdks()` | The SDK versions found, newest first: `Path`, `Version`, `Sources`. |

## The variables composed

For an instance `INST`, a toolset `TOOLS` (`INST\VC\Tools\MSVC\<version>`), an SDK root `SDK`
with version `V`, host `H` and target `T`:

| Variable | Value |
| --- | --- |
| `VSINSTALLDIR`, `VCINSTALLDIR` | `INST\`, `INST\VC\` |
| `VCToolsInstallDir`, `VCToolsVersion` | `TOOLS\`, the toolset version |
| `VCToolsRedistDir` | the redistributable Visual Studio names as default, when present |
| `DevEnvDir`, `VisualStudioVersion` | `INST\Common7\IDE\` when present, the major with `.0` |
| `WindowsSdkDir`, `UniversalCRTSdkDir` | `SDK\` |
| `WindowsSDKVersion`, `WindowsSDKLibVersion`, `UCRTVersion` | `V\`, `V\`, `V` |
| `WindowsSdkBinPath`, `WindowsSdkVerBinPath` | `SDK\bin\`, `SDK\bin\V\` |
| `INCLUDE` | `TOOLS\include`, `TOOLS\atlmfc\include` when present, `SDK\Include\V\ucrt`, `um`, `shared`, `winrt`, `cppwinrt` |
| `LIB` | `TOOLS\lib\T`, `TOOLS\atlmfc\lib\T` when present, `SDK\Lib\V\ucrt\T`, `SDK\Lib\V\um\T` |
| `LIBPATH` | `TOOLS\lib\T`, `TOOLS\atlmfc\lib\T` when present, `SDK\UnionMetadata\V`, `SDK\References\V` when present |
| `PATH`, put first | `TOOLS\bin\HostH\T`, `TOOLS\bin\HostH\H` when `T` differs, `SDK\bin\V\H`, `SDK\bin\H`, `INST\Common7\IDE`, `INST\Common7\Tools`, `INST\MSBuild\Current\Bin`, those that exist |
| `Platform`, `VSCMD_ARG_HOST_ARCH`, `VSCMD_ARG_TGT_ARCH` | `x64`, `x86`, `ARM` or `ARM64`; `H`; `T` |

[Back to the extensions index](../extensions.md)
