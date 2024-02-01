/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Kltv.Kombine.Api;

namespace Kltv.Kombine {

	/// <summary>
	/// 
	/// Compiles and executes a Kombine script
	/// Since is not correctly documented, some references has been taken from:
	/// https://www.strathweb.com/2016/06/implementing-custom-load-behavior-in-roslyn-scripting/
	/// 
	/// </summary>
	internal partial class KombineScript {

		/// <summary>
		/// 
		/// </summary>
		private class SourceResolver : SourceReferenceResolver {

			private string ScriptPath { get; set; } = string.Empty;


			public SourceResolver(string scriptPath) {
				ScriptPath = scriptPath;
			}


			/// <summary>
			/// 
			/// </summary>
			/// <param name="path"></param>
			/// <param name="baseFilePath"></param>
			/// <returns></returns>
			public override string? NormalizePath(string path, string? baseFilePath) {
				Msg.PrintMod("NormalizePath:"+path,".exec.script.sourceresolver", Msg.LogLevels.Debug);
				SourceFileResolver r = new SourceFileResolver(new string[0], AppContext.BaseDirectory);
				r.NormalizePath(path, baseFilePath);
				return null;
			}


			/// <summary>
			/// 
			/// </summary>
			/// <param name="resolvedPath"></param>
			/// <returns></returns>
			public override Stream OpenRead(string resolvedPath) {
				Msg.PrintMod("OpenRead:"+resolvedPath,".exec.script.sourceresolver", Msg.LogLevels.Debug);
				FileStream fs = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
				return fs;
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="resolvedPath"></param>
			/// <returns></returns>
			public override SourceText ReadText(string resolvedPath) {
				Msg.PrintMod("ReadText:"+resolvedPath,".exec.script.sourceresolver", Msg.LogLevels.Debug);

				try {
					using (Stream stream = OpenRead(resolvedPath)) {
						return SourceText.From(stream, null, SourceHashAlgorithm.Sha1, true, true);
					}
				} catch (Exception ex) {
					Msg.PrintWarningMod("Source cannot be read. Exception:"+ex.Message,".exec.script.sourceresolver");
					return SourceText.From("");
				}
			}

			/// <summary>
			/// Resolve a source reference.
			/// We will resolve following an order (in case is relative):
			/// 
			/// CurrentDirectory
			/// ScriptDirectory
			/// Backtrace directories
			/// Tool directory
			/// 
			/// In case the given path is absolute, we will just return it if it exists
			/// In case the given path is a URL, we will just return it.
			/// The resolved path is converted into an absolute path following the host os conventions.
			/// 
			/// </summary>
			/// <param name="path">The path and filename for the reference to be resolved</param>
			/// <param name="baseFilePath">The base file path in case is used.</param>
			/// <returns>The resolved path or null if cannot be resolved.</returns>
			public override string? ResolveReference(string path, string? baseFilePath) {
				// Note:
				// Is not clear when baseFilePath has something. Under the testing always was null.
				// Also it is not clear what is the purpose of baseFilePath or which kind of normalization
				// should take place. 
				//
				// From: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.xmlfileresolver.resolvereference?view=roslyn-dotnet-4.7.0
				//
				// Path of the source file(FilePath) or XML document that contains the path.
				// If not null used as a base path of path, if path is relative.
				// If baseFilePath is relative BaseDirectory is used as the base path of baseFilePath.
				//
				// Perfect explained, right?
				//
				//
				string? resolvedPath = Folders.ResolveFilename(path);
				if (resolvedPath != null)
					return resolvedPath;
				Msg.PrintMod("ResolveReference: Could not resolve "+path, ".exec.script.sourceresolver", Msg.LogLevels.Debug);
				return null;
			}

			public override bool Equals(object? other) {
				// TODO: Even looks like is not used, we should implement it.
				throw new NotImplementedException();
			}

			public override int GetHashCode() {
				// TODO: Even looks like is not used, we should implement it.
				throw new NotImplementedException();
			}


		}
	}
}