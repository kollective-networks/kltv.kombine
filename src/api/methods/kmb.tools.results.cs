/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Encapsulated in the tool class we have the tool results
	/// </summary>
	public partial class Tool {

		/// <summary>
		/// Holds the tool results. All the information about the tool execution
		/// </summary>
		public class ToolResult {

			public static ToolResult DefaultFailed() {
				return new ToolResult(new string[] { }, new string[] { }, ToolStatus.Failed, -1);
			}

			public static ToolResult DefaultUndefined() {
				return new ToolResult(new string[] { }, new string[] { }, ToolStatus.Undefined, 0);
			}

			public static ToolResult DefaultNoChanges() {
				return new ToolResult(new string[] { }, new string[] { }, ToolStatus.NoChanges, 0);
			}

			/// <summary>
			/// Tool results constructor
			/// </summary>
			public ToolResult(string[] stdout, string[] stderr, ToolStatus status, int exitCode, object? id = null) {
				this.Stdout = stdout;
				this.Stderr = stderr;
				this.Status = status;
				this.ExitCode = exitCode;
				this.Id = id;
			}
			/// <summary>
			/// Holds the stdoutput for the tool
			/// </summary>
			public string[] Stdout { get; private set; }
			/// <summary>
			/// Holds the stderr for the tool
			/// </summary>
			public string[] Stderr { get; private set; }
			/// <summary>
			/// Holds a copy of the status
			/// </summary>
			public ToolStatus Status { get; set; }
			/// <summary>
			/// Holds the tool exit code
			/// </summary>
			public int ExitCode { get; private set; }
			/// <summary>
			/// Holds a user given Id for the command launched
			/// </summary>
			public object? Id { get; private set; }
		}
	}
}