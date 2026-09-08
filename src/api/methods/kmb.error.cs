/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Kind of failure reported by an API call through the LastError of its facility.
	/// </summary>
	public enum ErrorCode {
		/// <summary>No error, the last call succeeded.</summary>
		None,
		/// <summary>File, folder, archive, key or object not found.</summary>
		NotFound,
		/// <summary>The target already exists, or the key or object is already registered.</summary>
		AlreadyExists,
		/// <summary>Permissions or a locked file.</summary>
		AccessDenied,
		/// <summary>Empty name, mismatched lists, unsupported combination, no script running.</summary>
		InvalidArgument,
		/// <summary>The operation is not available, for example xz compression.</summary>
		NotSupported,
		/// <summary>Any other file system or archive failure.</summary>
		IoError,
		/// <summary>Transport failure without an HTTP status.</summary>
		NetworkError,
		/// <summary>The compared items differ.</summary>
		Different,
		/// <summary>The operation ran and reported a failure: a refused entry, an HTTP error status, a tool exit code.</summary>
		Failed
	}

	/// <summary>
	/// Last failure of a facility: the kind, the reason and the item involved.
	/// Every facility exposes a LastError, reset at the start of each call and set on failure, so a
	/// script that receives a false (or an empty) result can explain it with its own message. The
	/// engine logs the same reason at verbose level and prints nothing at normal level.
	/// </summary>
	public sealed class ApiError {

		/// <summary>
		/// The value of a facility that has no error.
		/// </summary>
		public static readonly ApiError None = new ApiError(ErrorCode.None, string.Empty, string.Empty);

		/// <summary>
		/// Kind of failure.
		/// </summary>
		public ErrorCode Code { get; }

		/// <summary>
		/// Reason of the failure, ready to be shown.
		/// </summary>
		public string Message { get; }

		/// <summary>
		/// Item involved: a path, an url, a key or a name. Empty when it does not apply.
		/// </summary>
		public string Source { get; }

		/// <summary>
		/// True when this is a failure.
		/// </summary>
		public bool IsError { get { return Code != ErrorCode.None; } }

		/// <summary>
		/// Creates an error.
		/// </summary>
		/// <param name="code">Kind of failure.</param>
		/// <param name="message">Reason of the failure.</param>
		/// <param name="source">Item involved.</param>
		public ApiError(ErrorCode code, string message, string source) {
			Code = code;
			Message = message ?? string.Empty;
			Source = source ?? string.Empty;
		}

		/// <summary>
		/// Renders the error as "Code: message (source)".
		/// </summary>
		public override string ToString() {
			if (!IsError)
				return "None";
			if (Source.Length > 0)
				return Code + ": " + Message + " (" + Source + ")";
			return Code + ": " + Message;
		}

		/// <summary>
		/// Maps an exception to an error code.
		/// </summary>
		/// <param name="ex">Exception to map. An aggregate exception is unwrapped.</param>
		/// <returns>The error code.</returns>
		public static ErrorCode CodeOf(Exception ex) {
			if (ex is AggregateException aggregate && aggregate.InnerException != null)
				ex = aggregate.InnerException;
			switch (ex) {
				case FileNotFoundException:
				case DirectoryNotFoundException:
					return ErrorCode.NotFound;
				case UnauthorizedAccessException:
					return ErrorCode.AccessDenied;
				case ArgumentException:
					return ErrorCode.InvalidArgument;
				case NotSupportedException:
					return ErrorCode.NotSupported;
				case HttpRequestException:
					return ErrorCode.NetworkError;
				case IOException:
					return ErrorCode.IoError;
				default:
					return ErrorCode.Failed;
			}
		}

		/// <summary>
		/// Creates an error from an exception.
		/// </summary>
		/// <param name="ex">Exception. An aggregate exception is unwrapped.</param>
		/// <param name="source">Item involved.</param>
		/// <returns>The error.</returns>
		public static ApiError From(Exception ex, string source) {
			if (ex is AggregateException aggregate && aggregate.InnerException != null)
				ex = aggregate.InnerException;
			return new ApiError(CodeOf(ex), ex.Message, source);
		}
	}
}
