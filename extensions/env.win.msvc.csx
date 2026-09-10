#pragma kombine requires 1.7
#pragma kombine module
/*---------------------------------------------------------------------------------------------------------

	Kombine Environment Extension: MSVC on Windows

	(C) Kollective Networks 2026

	Leaves the script environment with the variables Visual Studio provides for a given version, and
	nothing of another one: INCLUDE, LIB, LIBPATH, the PATH entries of the tools and the variables of
	the toolset and of the Windows SDK, composed by the extension from the folders it found, never by
	running vcvarsall.bat. What runs on that environment afterwards (cl, clang-cl, gn, ninja, msbuild)
	is the script's business, not the extension's. Every public member is documented in place; this
	header is the overview.

	Requirements. Every option set is a requirement: met, or the call fails with a warning naming it
	and what exists. A version requirement is strict at the precision given ("17", "14.38",
	"10.0.22621.0"), that or newer with a trailing plus ("17+", "14.38+"), or a range ("[17.0,18.0)");
	the newest that meets it is taken. Every option unset is ignored as far as errors and warnings
	go: the newest installed is taken and set when there is one, nothing is set for it when there is
	none, silently.
	A machine without Visual Studio is a normal case for a script that required nothing.
	SetUnspecified, true by default, sets the parts found for the unset options as well; false sets
	only the parts of the options the script set.

	Verbs. Find() discovers and selects without touching the environment; Apply() finds, clears the
	variables of a previous toolchain and sets the selected ones; Restore() puts back the environment
	Apply replaced; List() prints every installation, toolset and SDK found with its sources. The
	result of Find and Apply is in LastEnvironment, the reason of a failure in LastError; a failure
	prints a warning and, with AbortOnFailure (true by default), aborts the script.

	Discovery, several methods per component, never one alone: vswhere (-products *), the instance
	records of the installer, a folder scan, the environment and the explicit Path option for the
	Visual Studio instances; the registry, the folders and the environment for the Windows SDK and
	its UCRT. Every candidate is kept only when its key files exist, and List() shows them all.

	The name follows the scheme env.<platform>.<toolchain>.csx and the class mirrors it, EnvWinMsvc:
	Msvc stays free for a compiler extension, as Clang is the class of clang.csx.

---------------------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

if (MkbHexVersion() < 0x0107)
	Msg.PrintAndAbort("The env.win.msvc extension requires Kombine 1.7 or newer (running " + MkbVersion() + ")");

/// <summary>
/// The options of EnvWinMsvc: what the script requires. An option set is a requirement; an option
/// unset takes the newest installed, or nothing when there is none, without an error.
/// </summary>
public class EnvWinMsvcOptions {
	/// <summary>Visual Studio: strict at the precision given ("17", "17.8"), that or newer with a trailing plus ("17+"), or a range ("[17.0,18.0)"). Unset: the newest installed.</summary>
	public string? Version { get; set; } = null;
	/// <summary>The product: Enterprise, Professional, Community or BuildTools. Unset: any.</summary>
	public string? Product { get; set; } = null;
	/// <summary>The toolset: strict at the precision given ("14.38" is any 14.38.x, "14.38.33130" only that), that or newer with a trailing plus ("14.38+"), or a range. Unset: the newest of the instance.</summary>
	public string? VcTools { get; set; } = null;
	/// <summary>The Windows SDK: strict at the precision given ("10.0.22621.0", "10.0.22621"), that or newer with a trailing plus ("10.0.22621+"), or a range. Unset: the newest installed.</summary>
	public string? Sdk { get; set; } = null;
	/// <summary>The host architecture of the tools: x64, x86 or arm64. Unset: the architecture of the machine.</summary>
	public string? HostArch { get; set; } = null;
	/// <summary>The target architecture: x64, x86, arm or arm64. Unset: the host architecture.</summary>
	public string? TargetArch { get; set; } = null;
	/// <summary>True requires the ATL and MFC folders in the toolset.</summary>
	public bool Atl { get; set; } = false;
	/// <summary>An installation folder taken as it is, for a layout nothing registers. Unset: discovery.</summary>
	public string? Path { get; set; } = null;
	/// <summary>True, the default, sets the parts found for the options left unset as well; false sets only the parts of the options set.</summary>
	public bool SetUnspecified { get; set; } = true;
}

/// <summary>
/// A Visual Studio installation found.
/// </summary>
public class VsInstance {
	/// <summary>The installation folder, without a trailing separator.</summary>
	public string Path { get; internal set; } = string.Empty;
	/// <summary>The installation version ("18.6.11806.211"), or the major alone when only the folder is known.</summary>
	public string Version { get; internal set; } = string.Empty;
	/// <summary>Enterprise, Professional, Community, BuildTools, or empty when unknown.</summary>
	public string Product { get; internal set; } = string.Empty;
	/// <summary>The toolset versions under VC\Tools\MSVC that have a compiler, newest first.</summary>
	public List<string> Toolsets { get; internal set; } = new List<string>();
	/// <summary>Where it was found: vswhere, installer records, folders, environment, option.</summary>
	public List<string> Sources { get; internal set; } = new List<string>();
}

/// <summary>
/// A Windows SDK version found.
/// </summary>
public class WinSdk {
	/// <summary>The root of the kits ("C:\Program Files (x86)\Windows Kits\10"), without a trailing separator.</summary>
	public string Path { get; internal set; } = string.Empty;
	/// <summary>The version ("10.0.26100.0").</summary>
	public string Version { get; internal set; } = string.Empty;
	/// <summary>Where the root was found: registry, folders, environment.</summary>
	public List<string> Sources { get; internal set; } = new List<string>();
}

/// <summary>
/// What Find selected and what Apply set, in LastEnvironment.
/// </summary>
public class EnvWinMsvcResult {
	/// <summary>The instance selected, null when none.</summary>
	public VsInstance? Instance { get; internal set; } = null;
	/// <summary>The toolset version selected, empty when none.</summary>
	public string Toolset { get; internal set; } = string.Empty;
	/// <summary>The folder of the toolset, empty when none.</summary>
	public string ToolsetPath { get; internal set; } = string.Empty;
	/// <summary>The SDK selected, null when none.</summary>
	public WinSdk? Sdk { get; internal set; } = null;
	/// <summary>The host architecture of the tools.</summary>
	public string HostArch { get; internal set; } = string.Empty;
	/// <summary>The target architecture.</summary>
	public string TargetArch { get; internal set; } = string.Empty;
	/// <summary>The variables composed, name and value, PATH aside.</summary>
	public Dictionary<string, string> Variables { get; internal set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	/// <summary>The PATH entries put first, in order.</summary>
	public KList PathEntries { get; internal set; } = new KList();
	/// <summary>The variables and the PATH entries Apply removed ("PATH: folder" for an entry).</summary>
	public KList Removed { get; internal set; } = new KList();
	/// <summary>True when a toolset was selected.</summary>
	public bool HasToolset { get { return ToolsetPath.Length > 0; } }
	/// <summary>True when an SDK was selected.</summary>
	public bool HasSdk { get { return Sdk != null; } }
}

/// <summary>
/// The environment of MSVC on Windows. See the header of the file for the overview.
/// </summary>
public class EnvWinMsvc {

	/// <summary>The requirements of the script.</summary>
	public EnvWinMsvcOptions Options { get; set; } = new EnvWinMsvcOptions();

	/// <summary>True, the default, makes a failing call abort the script after its warning; false returns false with the reason in LastError.</summary>
	public bool AbortOnFailure { get; set; } = true;

	/// <summary>The reason of the last failure: NotFound for an unmet requirement, InvalidArgument for a bad option, NotSupported outside Windows. Reset by every call.</summary>
	public ApiError LastError { get; private set; } = ApiError.None;

	/// <summary>What the last Find or Apply selected and set. Null before the first call and after a failure.</summary>
	public EnvWinMsvcResult? LastEnvironment { get; private set; } = null;

	/// <summary>
	/// The variables of Visual Studio the extension clears before setting its own: the extension's
	/// list, passed to the generic Env.Clean.
	/// </summary>
	public static readonly string[] Variables = {
		"INCLUDE", "LIB", "LIBPATH",
		"VSINSTALLDIR", "VCINSTALLDIR", "VCToolsInstallDir", "VCToolsVersion", "VCToolsRedistDir", "VCIDEInstallDir",
		"VisualStudioVersion", "DevEnvDir", "ExtensionSdkDir", "Platform",
		"WindowsSdkDir", "WindowsSDKVersion", "WindowsSDKLibVersion", "WindowsSdkBinPath", "WindowsSdkVerBinPath",
		"WindowsLibPath", "WindowsSDK_ExecutablePath_x64", "WindowsSDK_ExecutablePath_x86",
		"UCRTVersion", "UniversalCRTSdkDir", "NETFXSDKDir", "HTMLHelpDir",
		"Framework40Version", "FrameworkDir", "FrameworkDir32", "FrameworkDir64", "FrameworkVersion", "FrameworkVersion32", "FrameworkVersion64",
		"VSCMD_*", "__VSCMD_*", "__DOTNET_*"
	};

	/// <summary>
	/// The PATH entries the extension removes before adding its own: every folder of a Visual Studio
	/// installation or of the Windows Kits.
	/// </summary>
	public static readonly string[] PathPatterns = { "*\\Microsoft Visual Studio\\*", "*\\Windows Kits\\*" };

	/// <summary>The environment Apply replaced, for Restore.</summary>
	private Dictionary<string, string>? snapshot = null;

	// --------------------------------------------------------------------------------------------
	// Verbs
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Discovers the installations and selects what the options ask for, without touching the
	/// environment. The selection, with the variables it would set, is in LastEnvironment.
	/// </summary>
	/// <returns>True when every requirement is met; false with the reason in LastError.</returns>
	public bool Find() {
		LastError = ApiError.None;
		LastEnvironment = null;
		if (!Host.IsWindows())
			return Fail(ErrorCode.NotSupported, "the Visual Studio environment exists on Windows only", "Find");
		string? host = NormalizeArch(Options.HostArch ?? MachineArch());
		if (host == null || host == "arm")
			return Fail(ErrorCode.InvalidArgument, "unknown host architecture: " + Options.HostArch + " (x64, x86 or arm64)", "Find");
		string? target = NormalizeArch(Options.TargetArch ?? host);
		if (target == null)
			return Fail(ErrorCode.InvalidArgument, "unknown target architecture: " + Options.TargetArch + " (x64, x86, arm or arm64)", "Find");
		if (Options.Path != null && !Directory.Exists(Options.Path))
			return Fail(ErrorCode.NotFound, "the installation folder given does not exist: " + Options.Path, "Find");
		bool wantInstance = Options.Version != null || Options.Product != null || Options.VcTools != null || Options.Atl || Options.Path != null;
		bool wantSdk = Options.Sdk != null;
		EnvWinMsvcResult result = new EnvWinMsvcResult { HostArch = host, TargetArch = target };
		List<VsInstance> instances = Instances();
		List<WinSdk> sdks = Sdks();
		// The instance and its toolset, newest first, the first that meets every requirement
		if (wantInstance || Options.SetUnspecified) {
			foreach (VsInstance i in instances) {
				if (i.Toolsets.Count == 0)
					continue;
				if (Options.Path != null && !SamePath(i.Path, Options.Path))
					continue;
				if (Options.Version != null && !Matches(i.Version, Options.Version))
					continue;
				if (Options.Product != null && !i.Product.Equals(Options.Product, StringComparison.OrdinalIgnoreCase))
					continue;
				string? toolset = i.Toolsets.FirstOrDefault(t =>
					(Options.VcTools == null || Matches(t, Options.VcTools))
					&& ToolsetSupports(ToolsetPath(i, t), host, target)
					&& (!Options.Atl || Directory.Exists(Path.Combine(ToolsetPath(i, t), "atlmfc", "include"))));
				if (toolset == null)
					continue;
				result.Instance = i;
				result.Toolset = toolset;
				result.ToolsetPath = ToolsetPath(i, toolset);
				break;
			}
			if (result.Instance == null && wantInstance)
				return Fail(ErrorCode.NotFound, "Visual Studio " + Wanted(host, target) + " not found; installed: " + Installed(instances), "Find");
		}
		// The SDK, newest first, the first that meets the requirement and has the target
		if (wantSdk || Options.SetUnspecified) {
			result.Sdk = sdks.FirstOrDefault(s => (Options.Sdk == null || Matches(s.Version, Options.Sdk)) && SdkSupports(s, target));
			if (result.Sdk == null && wantSdk)
				return Fail(ErrorCode.NotFound, "Windows SDK " + Options.Sdk + " for " + target + " not found; installed: " + InstalledSdks(sdks), "Find");
		}
		Compose(result);
		LastEnvironment = result;
		return true;
	}

	/// <summary>
	/// Finds, clears the variables of a previous toolchain and sets the selected ones. The environment
	/// replaced is kept for Restore. Nothing is touched when a requirement is not met.
	/// </summary>
	/// <returns>True when the environment is set; false with the reason in LastError.</returns>
	public bool Apply() {
		if (!Find())
			return false;
		EnvWinMsvcResult result = LastEnvironment!;
		snapshot = Env.Snapshot();
		KList removed = Env.Clean(Variables);
		foreach (string pattern in PathPatterns) {
			foreach (KValue entry in Env.RemovePath(pattern))
				removed.Add("PATH: " + entry);
		}
		foreach (KeyValuePair<string, string> kv in result.Variables)
			Env.Set(kv.Key, kv.Value);
		for (int i = result.PathEntries.Count() - 1; i >= 0; i--)
			Env.PrependPath(result.PathEntries[i]);
		result.Removed = removed;
		Msg.Print("env.win.msvc: " + Describe(result), Msg.LogLevels.Verbose);
		return true;
	}

	/// <summary>
	/// Puts back the environment Apply replaced.
	/// </summary>
	/// <returns>True when restored; false when Apply was not called (see LastError).</returns>
	public bool Restore() {
		LastError = ApiError.None;
		if (snapshot == null)
			return Fail(ErrorCode.InvalidArgument, "nothing to restore, Apply was not called", "Restore");
		Env.Restore(snapshot);
		snapshot = null;
		return true;
	}

	/// <summary>
	/// Prints every Visual Studio installation with its toolsets and every Windows SDK found, with the
	/// sources that reported each one.
	/// </summary>
	public void List() {
		if (!Host.IsWindows()) {
			Msg.Print("env.win.msvc: the Visual Studio environment exists on Windows only");
			return;
		}
		List<VsInstance> instances = Instances();
		Msg.Print("Visual Studio installations: " + instances.Count);
		Msg.BeginIndent();
		foreach (VsInstance i in instances) {
			Msg.Print(i.Version + "  " + (i.Product.Length > 0 ? i.Product : "?") + "  " + i.Path + "  (" + string.Join(", ", i.Sources) + ")");
			Msg.BeginIndent();
			Msg.Print(i.Toolsets.Count > 0 ? "toolsets: " + string.Join(", ", i.Toolsets) : "no C++ toolset");
			Msg.EndIndent();
		}
		Msg.EndIndent();
		List<WinSdk> sdks = Sdks();
		Msg.Print("Windows SDKs: " + sdks.Count);
		Msg.BeginIndent();
		foreach (WinSdk s in sdks)
			Msg.Print(s.Version + "  " + s.Path + "  (" + string.Join(", ", s.Sources) + ")");
		Msg.EndIndent();
	}

	// --------------------------------------------------------------------------------------------
	// Discovery
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// The Visual Studio installations, newest first, from every source: vswhere, the instance records
	/// of the installer, a folder scan, the environment and the Path option, merged by folder.
	/// </summary>
	public List<VsInstance> Instances() {
		List<VsInstance> list = new List<VsInstance>();
		if (!Host.IsWindows())
			return list;
		string programFiles = Env.Get("ProgramFiles", @"C:\Program Files");
		string programFilesX86 = Env.Get("ProgramFiles(x86)", @"C:\Program Files (x86)");
		// vswhere: every product, the Build Tools included
		string vswhere = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
		if (File.Exists(vswhere)) {
			ToolResult r = Run(vswhere, "-products * -format json -utf8");
			if (r.ExitCode == 0) {
				try {
					if (JsonNode.Parse(string.Join("\n", Lines(r))) is JsonArray array) {
						foreach (JsonNode? node in array) {
							if (node is JsonObject o)
								Merge(list, o["installationPath"]?.ToString(), o["installationVersion"]?.ToString(), ProductOf(o["productId"]?.ToString()), "vswhere");
						}
					}
				} catch (Exception ex) {
					Msg.Print("env.win.msvc: vswhere output not understood: " + ex.Message, Msg.LogLevels.Verbose);
				}
			}
		}
		// The instance records of the installer, what vswhere reads
		string records = Path.Combine(Env.Get("ProgramData", @"C:\ProgramData"), "Microsoft", "VisualStudio", "Packages", "_Instances");
		if (Directory.Exists(records)) {
			foreach (string dir in Directory.GetDirectories(records)) {
				string state = Path.Combine(dir, "state.json");
				if (!File.Exists(state))
					continue;
				try {
					if (JsonNode.Parse(File.ReadAllText(state)) is JsonObject o)
						Merge(list, o["installationPath"]?.ToString(), o["installationVersion"]?.ToString(), ProductOf(o["product"]?["id"]?.ToString()), "installer records");
				} catch (Exception ex) {
					Msg.Print("env.win.msvc: " + state + " not understood: " + ex.Message, Msg.LogLevels.Verbose);
				}
			}
		}
		// The folders on disk
		foreach (string pf in new[] { programFiles, programFilesX86 }) {
			string root = Path.Combine(pf, "Microsoft Visual Studio");
			if (!Directory.Exists(root))
				continue;
			foreach (string year in Directory.GetDirectories(root)) {
				foreach (string edition in Directory.GetDirectories(year)) {
					if (Directory.Exists(Path.Combine(edition, "VC", "Tools", "MSVC")))
						Merge(list, edition, MajorOfFolder(Path.GetFileName(year)), Path.GetFileName(edition), "folders");
				}
			}
		}
		// The environment of the current prompt
		string vsdir = Env.Get("VSINSTALLDIR");
		if (vsdir.Length > 0 && Directory.Exists(vsdir))
			Merge(list, vsdir, string.Empty, string.Empty, "environment");
		// The folder given
		if (Options.Path != null && Directory.Exists(Options.Path))
			Merge(list, Options.Path, string.Empty, string.Empty, "option");
		foreach (VsInstance i in list)
			i.Toolsets = Toolsets(i.Path);
		list.Sort((a, b) => CompareVersions(b.Version, a.Version));
		return list;
	}

	/// <summary>
	/// The Windows SDK versions installed, newest first, from every source: the registry, the folders
	/// and the environment, merged by root. A version counts when its um\windows.h exists.
	/// </summary>
	public List<WinSdk> Sdks() {
		List<WinSdk> list = new List<WinSdk>();
		if (!Host.IsWindows())
			return list;
		List<(string root, string source)> roots = new List<(string, string)>();
		foreach (string key in new[] { @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\Windows\v10.0", @"HKLM\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v10.0" }) {
			string? folder = RegistryValue(key, "InstallationFolder");
			if (folder != null)
				roots.Add((folder, "registry"));
		}
		string? kits = RegistryValue(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots", "KitsRoot10");
		if (kits != null)
			roots.Add((kits, "registry"));
		roots.Add((Path.Combine(Env.Get("ProgramFiles(x86)", @"C:\Program Files (x86)"), "Windows Kits", "10"), "folders"));
		string sdkdir = Env.Get("WindowsSdkDir");
		if (sdkdir.Length > 0)
			roots.Add((sdkdir, "environment"));
		// Merge the roots by folder, keeping every source
		List<(string root, List<string> sources)> merged = new List<(string, List<string>)>();
		foreach ((string root, string source) in roots) {
			string normalized = Normalize(root);
			if (!Directory.Exists(normalized))
				continue;
			int at = merged.FindIndex(m => SamePath(m.root, normalized));
			if (at < 0)
				merged.Add((normalized, new List<string> { source }));
			else if (!merged[at].sources.Contains(source))
				merged[at].sources.Add(source);
		}
		foreach ((string root, List<string> sources) in merged) {
			string include = Path.Combine(root, "Include");
			if (!Directory.Exists(include))
				continue;
			foreach (string dir in Directory.GetDirectories(include)) {
				string version = Path.GetFileName(dir);
				if (!IsVersion(version) || !File.Exists(Path.Combine(dir, "um", "windows.h")))
					continue;
				if (list.Any(s => s.Version == version && SamePath(s.Path, root)))
					continue;
				list.Add(new WinSdk { Path = root, Version = version, Sources = new List<string>(sources) });
			}
		}
		list.Sort((a, b) => CompareVersions(b.Version, a.Version));
		return list;
	}

	/// <summary>Adds an instance to the list or completes the one already there with the same folder.</summary>
	private static void Merge(List<VsInstance> list, string? path, string? version, string? product, string source) {
		if (string.IsNullOrWhiteSpace(path))
			return;
		string normalized = Normalize(path);
		if (!Directory.Exists(normalized))
			return;
		VsInstance? existing = list.FirstOrDefault(i => SamePath(i.Path, normalized));
		if (existing == null) {
			existing = new VsInstance { Path = normalized };
			list.Add(existing);
		}
		if (!existing.Sources.Contains(source))
			existing.Sources.Add(source);
		// A full version beats a major alone, a product beats none
		if (!string.IsNullOrEmpty(version) && (existing.Version.Length == 0 || (!existing.Version.Contains('.') && version.Contains('.'))))
			existing.Version = version;
		if (!string.IsNullOrEmpty(product) && existing.Product.Length == 0)
			existing.Product = product;
	}

	/// <summary>The toolset versions of an installation that have a compiler, newest first.</summary>
	private static List<string> Toolsets(string instance) {
		List<string> list = new List<string>();
		string root = Path.Combine(instance, "VC", "Tools", "MSVC");
		if (!Directory.Exists(root))
			return list;
		foreach (string dir in Directory.GetDirectories(root)) {
			string version = Path.GetFileName(dir);
			if (!IsVersion(version))
				continue;
			string bin = Path.Combine(dir, "bin");
			if (Directory.Exists(bin) && Directory.GetFiles(bin, "cl.exe", SearchOption.AllDirectories).Length > 0)
				list.Add(version);
		}
		list.Sort((a, b) => CompareVersions(b, a));
		return list;
	}

	/// <summary>The folder of a toolset of an instance.</summary>
	private static string ToolsetPath(VsInstance instance, string toolset) {
		return Path.Combine(instance.Path, "VC", "Tools", "MSVC", toolset);
	}

	/// <summary>True when the toolset has the host tools for the target and the runtime library of the target.</summary>
	private static bool ToolsetSupports(string toolset, string host, string target) {
		return File.Exists(Path.Combine(toolset, "bin", "Host" + host, target, "cl.exe"))
			&& File.Exists(Path.Combine(toolset, "bin", "Host" + host, target, "link.exe"))
			&& File.Exists(Path.Combine(toolset, "lib", target, "msvcrt.lib"));
	}

	/// <summary>True when the SDK has the headers and the libraries of the target, UCRT included.</summary>
	private static bool SdkSupports(WinSdk sdk, string target) {
		return File.Exists(Path.Combine(sdk.Path, "Include", sdk.Version, "um", "windows.h"))
			&& File.Exists(Path.Combine(sdk.Path, "Include", sdk.Version, "ucrt", "stdio.h"))
			&& File.Exists(Path.Combine(sdk.Path, "Lib", sdk.Version, "um", target, "kernel32.lib"))
			&& File.Exists(Path.Combine(sdk.Path, "Lib", sdk.Version, "ucrt", target, "libucrt.lib"));
	}

	/// <summary>The product of a vswhere or installer product id ("Microsoft.VisualStudio.Product.Enterprise").</summary>
	private static string ProductOf(string? id) {
		if (string.IsNullOrEmpty(id))
			return string.Empty;
		int dot = id.LastIndexOf('.');
		return dot >= 0 ? id.Substring(dot + 1) : id;
	}

	/// <summary>The major version of a Visual Studio folder name: "2022" is 17, "2019" 16, "2017" 15, a number itself.</summary>
	private static string MajorOfFolder(string folder) {
		switch (folder) {
			case "2017": return "15";
			case "2019": return "16";
			case "2022": return "17";
		}
		return int.TryParse(folder, out int major) ? major.ToString() : string.Empty;
	}

	/// <summary>A value of the registry through reg.exe, null when the key or the value does not exist.</summary>
	private static string? RegistryValue(string key, string name) {
		ToolResult r = Run("reg.exe", "query \"" + key + "\" /v " + name);
		if (r.ExitCode != 0)
			return null;
		foreach (string line in Lines(r)) {
			int at = line.IndexOf("REG_SZ", StringComparison.OrdinalIgnoreCase);
			if (at < 0 || !line.TrimStart().StartsWith(name, StringComparison.OrdinalIgnoreCase))
				continue;
			string value = line.Substring(at + 6).Trim();
			return value.Length > 0 ? value : null;
		}
		return null;
	}

	// --------------------------------------------------------------------------------------------
	// The environment
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Composes the variables of a selection: what Visual Studio sets for the instance, the toolset,
	/// the SDK and the architectures, every folder named checked to exist.
	/// </summary>
	private void Compose(EnvWinMsvcResult result) {
		Dictionary<string, string> v = result.Variables;
		List<string> includes = new List<string>();
		List<string> libs = new List<string>();
		List<string> libpaths = new List<string>();
		KList paths = new KList();
		string host = result.HostArch;
		string target = result.TargetArch;
		if (result.Instance != null) {
			string inst = result.Instance.Path;
			string tools = result.ToolsetPath;
			v["VSINSTALLDIR"] = inst + "\\";
			v["VCINSTALLDIR"] = Path.Combine(inst, "VC") + "\\";
			v["VCToolsInstallDir"] = tools + "\\";
			v["VCToolsVersion"] = result.Toolset;
			string? redist = RedistDir(inst);
			if (redist != null)
				v["VCToolsRedistDir"] = redist + "\\";
			string ide = Path.Combine(inst, "Common7", "IDE");
			if (Directory.Exists(ide))
				v["DevEnvDir"] = ide + "\\";
			string major = result.Instance.Version.Split('.')[0];
			if (major.Length > 0)
				v["VisualStudioVersion"] = major + ".0";
			AddIfExists(includes, Path.Combine(tools, "include"));
			AddIfExists(includes, Path.Combine(tools, "atlmfc", "include"));
			AddIfExists(libs, Path.Combine(tools, "lib", target));
			AddIfExists(libs, Path.Combine(tools, "atlmfc", "lib", target));
			AddIfExists(libpaths, Path.Combine(tools, "lib", target));
			AddIfExists(libpaths, Path.Combine(tools, "atlmfc", "lib", target));
			paths.Add(Path.Combine(tools, "bin", "Host" + host, target));
			if (target != host)
				AddPathIfExists(paths, Path.Combine(tools, "bin", "Host" + host, host));
			AddPathIfExists(paths, ide);
			AddPathIfExists(paths, Path.Combine(inst, "Common7", "Tools"));
			AddPathIfExists(paths, Path.Combine(inst, "MSBuild", "Current", "Bin"));
		}
		if (result.Sdk != null) {
			string sdk = result.Sdk.Path;
			string version = result.Sdk.Version;
			v["WindowsSdkDir"] = sdk + "\\";
			v["WindowsSDKVersion"] = version + "\\";
			v["WindowsSDKLibVersion"] = version + "\\";
			v["WindowsSdkBinPath"] = Path.Combine(sdk, "bin") + "\\";
			v["WindowsSdkVerBinPath"] = Path.Combine(sdk, "bin", version) + "\\";
			v["UniversalCRTSdkDir"] = sdk + "\\";
			v["UCRTVersion"] = version;
			foreach (string part in new[] { "ucrt", "um", "shared", "winrt", "cppwinrt" })
				AddIfExists(includes, Path.Combine(sdk, "Include", version, part));
			AddIfExists(libs, Path.Combine(sdk, "Lib", version, "ucrt", target));
			AddIfExists(libs, Path.Combine(sdk, "Lib", version, "um", target));
			AddIfExists(libpaths, Path.Combine(sdk, "UnionMetadata", version));
			AddIfExists(libpaths, Path.Combine(sdk, "References", version));
			AddPathIfExists(paths, Path.Combine(sdk, "bin", version, host));
			AddPathIfExists(paths, Path.Combine(sdk, "bin", host));
		}
		if (includes.Count > 0)
			v["INCLUDE"] = string.Join(";", includes);
		if (libs.Count > 0)
			v["LIB"] = string.Join(";", libs);
		if (libpaths.Count > 0)
			v["LIBPATH"] = string.Join(";", libpaths);
		if (result.Instance != null || result.Sdk != null) {
			v["Platform"] = target == "arm64" ? "ARM64" : (target == "arm" ? "ARM" : target);
			v["VSCMD_ARG_HOST_ARCH"] = host;
			v["VSCMD_ARG_TGT_ARCH"] = target;
		}
		result.PathEntries = paths;
	}

	/// <summary>The redistributable folder of an installation, the version Visual Studio names as default; null when there is none.</summary>
	private static string? RedistDir(string instance) {
		string file = Path.Combine(instance, "VC", "Auxiliary", "Build", "Microsoft.VCRedistVersion.default.txt");
		if (!File.Exists(file))
			return null;
		string version = File.ReadAllText(file).Trim();
		string folder = Path.Combine(instance, "VC", "Redist", "MSVC", version);
		return Directory.Exists(folder) ? folder : null;
	}

	private static void AddIfExists(List<string> list, string folder) {
		if (Directory.Exists(folder))
			list.Add(folder);
	}

	private static void AddPathIfExists(KList list, string folder) {
		if (Directory.Exists(folder))
			list.Add(folder);
	}

	/// <summary>One line with the selection, for the verbose log.</summary>
	private static string Describe(EnvWinMsvcResult r) {
		string instance = r.Instance != null ? "Visual Studio " + r.Instance.Version + " " + r.Instance.Product + ", toolset " + r.Toolset : "no Visual Studio";
		string sdk = r.Sdk != null ? "SDK " + r.Sdk.Version : "no SDK";
		return instance + ", " + sdk + ", " + r.HostArch + " to " + r.TargetArch + ", " + r.Variables.Count + " variables, " + r.PathEntries.Count() + " path entries";
	}

	// --------------------------------------------------------------------------------------------
	// Helpers
	// --------------------------------------------------------------------------------------------

	/// <summary>The requirements, in words, for a failure message.</summary>
	private string Wanted(string host, string target) {
		List<string> parts = new List<string>();
		if (Options.Version != null)
			parts.Add("version " + Options.Version);
		if (Options.Product != null)
			parts.Add("product " + Options.Product);
		if (Options.VcTools != null)
			parts.Add("toolset " + Options.VcTools);
		if (Options.Atl)
			parts.Add("with ATL");
		if (Options.Path != null)
			parts.Add("at " + Options.Path);
		parts.Add("for " + host + " to " + target);
		return string.Join(", ", parts);
	}

	/// <summary>The installations, in words, for a failure message.</summary>
	private static string Installed(List<VsInstance> instances) {
		if (instances.Count == 0)
			return "none";
		return string.Join("; ", instances.Select(i => i.Version + " " + (i.Product.Length > 0 ? i.Product : "?") + (i.Toolsets.Count > 0 ? " (toolsets " + string.Join(", ", i.Toolsets) + ")" : " (no C++ toolset)")));
	}

	/// <summary>The SDKs, in words, for a failure message.</summary>
	private static string InstalledSdks(List<WinSdk> sdks) {
		return sdks.Count == 0 ? "none" : string.Join(", ", sdks.Select(s => s.Version));
	}

	/// <summary>The architecture of the machine as Visual Studio names it.</summary>
	private static string MachineArch() {
		switch (RuntimeInformation.OSArchitecture) {
			case Architecture.X86: return "x86";
			case Architecture.Arm64: return "arm64";
			case Architecture.Arm: return "arm";
			default: return "x64";
		}
	}

	/// <summary>The architecture in the form of the folders (x64, x86, arm, arm64), null when unknown.</summary>
	private static string? NormalizeArch(string arch) {
		string a = arch.Trim().ToLowerInvariant();
		if (a == "amd64" || a == "x86_64")
			a = "x64";
		if (a == "aarch64")
			a = "arm64";
		if (a == "win32" || a == "i386")
			a = "x86";
		return (a == "x64" || a == "x86" || a == "arm" || a == "arm64") ? a : null;
	}

	/// <summary>
	/// True when a version meets what was asked: that or newer with a trailing plus ("17+", "14.38+"),
	/// a range "[a,b)" / "(a,b]", or strict at the precision given ("17" is any 17.x, "14.38.33130"
	/// only that).
	/// </summary>
	private static bool Matches(string installed, string wanted) {
		string w = wanted.Trim();
		if (w.EndsWith("+"))
			return CompareVersions(installed, w.TrimEnd('+').Trim()) >= 0;
		if (w.StartsWith("[") || w.StartsWith("(")) {
			bool lowerInclusive = w.StartsWith("[");
			bool upperInclusive = w.EndsWith("]");
			string[] bounds = w.Substring(1, w.Length - 2).Split(',');
			if (bounds.Length != 2)
				return false;
			string lower = bounds[0].Trim();
			string upper = bounds[1].Trim();
			if (lower.Length > 0) {
				int c = CompareVersions(installed, lower);
				if (c < 0 || (c == 0 && !lowerInclusive))
					return false;
			}
			if (upper.Length > 0) {
				int c = CompareVersions(installed, upper);
				if (c > 0 || (c == 0 && !upperInclusive))
					return false;
			}
			return true;
		}
		return PrefixMatches(installed, w);
	}

	/// <summary>True when the first parts of the version are those asked: "14.38" matches "14.38.33130", "14.38.33130" only itself.</summary>
	private static bool PrefixMatches(string installed, string wanted) {
		string[] a = installed.Split('.');
		string[] b = wanted.Trim().Split('.');
		if (b.Length > a.Length)
			return false;
		for (int i = 0; i < b.Length; i++) {
			if (!int.TryParse(a[i], out int x) || !int.TryParse(b[i], out int y) || x != y)
				return false;
		}
		return true;
	}

	/// <summary>Compares two dotted versions numerically, a missing part counting as zero.</summary>
	private static int CompareVersions(string a, string b) {
		string[] pa = a.Split('.');
		string[] pb = b.Split('.');
		for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++) {
			int x = i < pa.Length && int.TryParse(pa[i], out int vx) ? vx : 0;
			int y = i < pb.Length && int.TryParse(pb[i], out int vy) ? vy : 0;
			if (x != y)
				return x.CompareTo(y);
		}
		return 0;
	}

	/// <summary>True for a dotted number ("10.0.26100.0", "14.44.35207").</summary>
	private static bool IsVersion(string text) {
		string[] parts = text.Split('.');
		return parts.Length >= 2 && parts.All(p => p.Length > 0 && p.All(char.IsDigit));
	}

	/// <summary>A full path without a trailing separator.</summary>
	private static string Normalize(string path) {
		try {
			return Path.GetFullPath(path.Trim().Trim('"')).TrimEnd('\\', '/');
		} catch {
			return path.Trim();
		}
	}

	/// <summary>True when two folders are the same, the case aside.</summary>
	private static bool SamePath(string a, string b) {
		return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Runs a tool with its output captured and nothing shown.</summary>
	private static ToolResult Run(string tool, string args) {
		Tool t = new Tool("env.win.msvc");
		t.CaptureOutput = false;
		return t.CommandSync(tool, args, null);
	}

	/// <summary>The lines of a tool output, the line endings and their fragments dropped.</summary>
	private static List<string> Lines(ToolResult r) {
		List<string> lines = new List<string>();
		foreach (string raw in r.Stdout) {
			string line = raw.TrimEnd('\r', '\n');
			if (line.Length > 0)
				lines.Add(line);
		}
		return lines;
	}

	/// <summary>
	/// Records a failure: LastError is set, a warning printed with the reason, and the script aborted
	/// when AbortOnFailure is set. Always returns false.
	/// </summary>
	private bool Fail(ErrorCode code, string message, string source) {
		LastError = new ApiError(code, message, source);
		Msg.PrintWarning("env.win.msvc: " + message);
		if (AbortOnFailure)
			Msg.PrintAndAbort("env.win.msvc " + source + " failed (" + code + "): " + message);
		return false;
	}
}
