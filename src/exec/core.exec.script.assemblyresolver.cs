/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {


	/// <summary>
	/// 
	/// Compiles and executes a Kombine script
	/// 
	/// </summary>
	internal partial class KombineScript {

		/// <summary>
		/// 
		/// </summary>
		private class AssemblyResolver : MetadataReferenceResolver {
			public AssemblyResolver() {}

			public override bool Equals(object? other) {
				throw new NotImplementedException();
			}

			public override int GetHashCode() {
				throw new NotImplementedException();
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="definition"></param>
			/// <param name="referenceIdentity"></param>
			/// <returns></returns>
			public override PortableExecutableReference? ResolveMissingAssembly(MetadataReference definition, AssemblyIdentity referenceIdentity) {
				Msg.PrintMod("Resolve Missing Assembly called: ",".exec.assembly",Msg.LogLevels.Debug);
				return base.ResolveMissingAssembly(definition, referenceIdentity);
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="reference"></param>
			/// <param name="baseFilePath"></param>
			/// <param name="properties"></param>
			/// <returns></returns>
			public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string? baseFilePath, MetadataReferenceProperties properties) {
				// Prepare the list that could be returned
				//
				var references = ImmutableArray.CreateBuilder<PortableExecutableReference>();
				Msg.PrintMod("Script wants: '" + reference + "' to be included.", ".exec.script.assemblyresolver", Msg.LogLevels.Debug);
				string? referencename = Path.GetFileNameWithoutExtension(reference);
				if (referencename == null){
					Msg.PrintWarningMod("The reference provided does not belong to a filename, lacks extension. We will use the complete reference",".exec.script.assemblyresolver",Msg.LogLevels.Verbose);
					referencename = reference;
				}
				// Look on the current loaded assemblies
				//	
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				// First look into the current loaded assemblies
				//
				// TODO: Maybe we need to distinguish here about our assembly and the rest
				// or match against the list of authorized assemblies to be referenced.
				//
				foreach (Assembly assem in assemblies) {
					// If we can fetch name
					string? name = assem.GetName().Name;
					if (name != null){
						// And the loaded assembly name matches the one we're looking for.
						Msg.PrintMod("Looking to match: "+referencename+" with loaded: "+name,".exec.script.assemblyresolver",Msg.LogLevels.Debug);
						if (assem.GetName().Name == referencename) {
							// Try to get a fake portable executable reference from that assembly which is on memory (and maybe is not in disk)
							Msg.PrintMod("A referenced assembly '" + reference + "' has been added from the current loaded.", ".exec.script.assemblyresolver", Msg.LogLevels.Debug);
							PortableExecutableReference? fref = GetPEReferenceFromMemoryAssembly(assem);
							if (fref != null){
								// Add the reference and return.
								references.Add(fref);
								return references.ToImmutable();
							}
						}
					}
				}
				if (Config.AllowAssemblyLoad) {
					Msg.PrintMod("Trying to load assembly '" + reference + "' .", ".exec.script.assemblyresolver", Msg.LogLevels.Debug);
					try {
						Assembly ase = Assembly.Load(reference);
						PortableExecutableReference? fref = GetPEReferenceFromMemoryAssembly(ase);
						if (fref != null){
							references.Add(fref);
							return references.ToImmutable();
						}
					} catch (Exception ex) {
						Msg.PrintWarningMod("Failed loading the assembly: " + ex.Message, ".exec.script.assemblyresolver");
					}
					Msg.PrintWarningMod("A referenced assembly '" + reference + "' could not be found.", ".exec.script.assemblyresolver");
					return ImmutableArray<PortableExecutableReference>.Empty;
				}
				Msg.PrintWarningMod("A referenced assembly '" + reference + "' will not be loaded. Load is disabled.", ".exec.script.assemblyresolver", Msg.LogLevels.Verbose);
				return ImmutableArray<PortableExecutableReference>.Empty;
			}
			}
			
			/// <summary>
			/// Return a portable executable reference for an in memory assembly.
			/// It also fakes the reference data for the filename and co to allow use assemblies which are not in disk
			/// </summary>
			/// <param name="assembly">Assembly to get the reference</param>
			/// <returns>PortableExecutableReference or null if was not posible.</returns>
			public static PortableExecutableReference? GetPEReferenceFromMemoryAssembly(Assembly assembly) {
				Msg.PrintMod("Get PortableExecutableReference for:"+assembly.GetName(),".exec.script.assemblyresolver", Msg.LogLevels.Debug);
				unsafe {
					if (assembly.TryGetRawMetadata(out byte* blob, out int Lenght) == false) {
						return null;
					}
					ModuleMetadata moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, Lenght);
					AssemblyMetadata assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
					// Fake the path and file name
					PortableExecutableReference reference = assemblyMetadata.GetReference(null, default, false, assembly.FullName+".dll", assembly.FullName+".dll");
					return reference;
				}
			}
		}
	}


				/*
				List<MetadataReference> references = new List<MetadataReference>();
				Assembly[] refs = AppDomain.CurrentDomain.GetAssemblies(); 
				foreach (Assembly asm in refs) {
					unsafe {
						Msg.PrintMod("Adding reference for: " + asm.GetName(), ".exec.script", Msg.LogLevels.Debug);
						if (asm.TryGetRawMetadata(out var blob, out var length))
							references.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
					}
				}
				*/

/*


			/// <summary>
			/// 
			/// </summary>
			/// <param name="assembly"></param>
			/// <returns></returns>
			public static MetadataReference GetMetadataReference(Assembly assembly) {
				Msg.PrintMod("Get MetadataReference for:"+assembly.GetName(),".exec.script.assemblyresolver", Msg.LogLevels.Debug);
#if USEHACK
				unsafe {
					assembly.TryGetRawMetadata(out byte* blob, out int length);
					return AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference();
				}
#else
				// TODO: Warning on single file
				return MetadataReference.CreateFromFile(assembly.Location);
			#endif
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="assembly"></param>
			/// <returns></returns>
			public static PortableExecutableReference GetPEReference(Assembly assembly) {
				Msg.PrintMod("Get PortableExecutableReference for:"+assembly.GetName(),".exec.script.assemblyresolver", Msg.LogLevels.Debug);
			#if USEHACK
				unsafe {
					if (assembly.TryGetRawMetadata(out byte* blob, out int Lenght) == false) {

					}
					ModuleMetadata moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, Lenght);
					AssemblyMetadata assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
					// Fake the path and file name
					PortableExecutableReference reference = assemblyMetadata.GetReference(null, default, false, assembly.FullName+".dll", assembly.FullName+".dll");
					return reference;
				}
			#else
				// TODO: Warning on single file
				return PortableExecutableReference.CreateFromFile(assembly.Location);
			#endif
			}
		*/