/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Net;
using System.Net.Http.Headers;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Http Methods API
	/// </summary>
	public class Http {

		/// <summary>
		/// Contains the last return code for the last transaction
		/// </summary>
		public static int LastReturnCode { get; set; } = 0;

		/// <summary>
		/// Contains the last return response for the last transaction
		/// </summary>
		public static string LastResponse { get; set; } = "";


		/// <summary>
		/// Reporter used by the downloads to show their progress line. When not set, Progress.Default is used.
		/// Assign an instance (ProgressBar, ProgressDots, ProgressPlain or a custom one) to select the renderer.
		/// </summary>
		public static ITaskProgress? Progress { get; set; } = null;

		/// <summary>
		/// If the downloads should show their progress line. When false the downloads print nothing.
		/// It is the default for the showprogress parameter of the download methods.
		/// </summary>
		public static bool ShowProgress { get; set; } = true;

		/// <summary>
		/// Last failure of an Http call: NetworkError for a transport failure, Failed for an HTTP error
		/// status (the status itself is in LastReturnCode). Reset at the start of every call.
		/// </summary>
		public static ApiError LastError { get; private set; } = ApiError.None;

		/// <summary>
		/// Records the failure of a call from its exception: an HTTP error status is a Failed, anything else a NetworkError.
		/// </summary>
		private static void FailWith(Exception cause, string source) {
			if (cause is HttpRequestException request && request.StatusCode.HasValue)
				LastError = new ApiError(ErrorCode.Failed, cause.Message, source);
			else
				LastError = new ApiError(ErrorCode.NetworkError, cause.Message, source);
		}

		/// <summary>
		/// Downloads a file from the given uri to the given path
		/// </summary>
		/// <param name="uri">The uri for the file to be downloaded</param>
		/// <param name="path">The resulting path for the file.</param>
		/// <param name="headers">Optional dictionary of headers to inject in the request</param>
		/// <param name="showprogress">If the progress line should be shown. Null takes Http.ShowProgress.</param>
		/// <param name="executable">If the downloaded file must be executable (Files.SetExecutable; nothing to do on Windows).</param>
		/// <returns>True if file was downloaded, false otherwise.</returns>
		public static bool DownloadFile(string uri, string path, Dictionary<string, string>? headers = null, bool? showprogress = null, bool executable = false) {
			LastError = ApiError.None;
			HttpClient client = new HttpClient();
			if (headers != null) {
				foreach (var header in headers) {
					client.DefaultRequestHeaders.Add(header.Key, header.Value);
				}
			}
			// Create download folder if does not exists
			string? npath = Path.GetDirectoryName(path);
			if (npath != null){
				if(Folders.Create(npath) == false) {
					Msg.PrintWarningMod("Error creating folder to store the download (maybe exist): "+path,".http",Msg.LogLevels.Verbose);
				}
			}
			// Progress line: the configured reporter or the engine default, unless silenced
			bool show = showprogress ?? ShowProgress;
			ITaskProgress? reporter = show ? (Progress ?? Api.Progress.Default) : null;
			DownloadProgress tracker = new DownloadProgress(reporter, 1);
			reporter?.Start("Downloading " + Path.GetFileName(path));
			// And download the file.
			bool ok = true;
			try {
				using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)) {
					client.DownloadDataAsync(uri, file, tracker.Changed).Wait();
				}
				LastReturnCode = 200;
				LastResponse = "";
			} catch(Exception e){
				// Unwrap the aggregate exception to reach the real cause and, if it carries
				// an HTTP status code, store it so the script can inspect LastReturnCode.
				Exception cause = e;
				if (e is AggregateException ae && ae.InnerException != null)
					cause = ae.InnerException;
				Msg.PrintErrorMod("Error downloading file: "+cause.Message,".http",Msg.LogLevels.Verbose);
				if (cause is HttpRequestException hre && hre.StatusCode.HasValue)
					LastReturnCode = (int)hre.StatusCode.Value;
				else
					LastReturnCode = -1;
				LastResponse = "";
				FailWith(cause, uri);
				ok = false;
			}
			if (!ok) {
				reporter?.Finish("failed", ProgressOutcome.Error);
				// Do not leave a partial/empty file behind on a failed download
				if (Files.Exists(path))
					Files.Delete(path);
				return false;
			}
			if (executable && !Files.SetExecutable(path)) {
				LastError = Files.LastError;
				reporter?.Finish("failed", ProgressOutcome.Error);
				return false;
			}
			reporter?.Report(1.0);
			reporter?.Finish("done");
			return true;
		}

		/// <summary>
		/// Download multiple files from the given uris to the given paths
		/// </summary>
		/// <param name="uris">Arrays of uris to be used</param>
		/// <param name="paths">Array of paths+filenames to be used</param>
		/// <param name="headers">Optional dictionary of headers to inject in the request</param>
		/// <param name="showprogress">If the progress line should be shown. Null takes Http.ShowProgress.</param>
		/// <param name="executable">If the downloaded files must be executable (Files.SetExecutable; nothing to do on Windows).</param>
		/// <returns>True if all files download fine, false otherwise.</returns>
		public static bool DownloadFiles(string[] uris,string[] paths, Dictionary<string, string>? headers = null, bool? showprogress = null, bool executable = false){
			LastError = ApiError.None;
			if (uris.Length != paths.Length){
				Msg.PrintErrorMod("The number of uris and paths must be the same.",".http",Msg.LogLevels.Verbose);
				LastReturnCode = -1;
				LastResponse = "";
				LastError = new ApiError(ErrorCode.InvalidArgument, "The number of uris and paths must be the same", uris.Length + " uris, " + paths.Length + " paths");
				return false;
			}
			HttpClient client = new HttpClient();
			if (headers != null) {
				foreach (var header in headers) {
					client.DefaultRequestHeaders.Add(header.Key, header.Value);
				}
			}
			for(int i = 0; i < uris.Length; i++){
				// Create download folders if does not exists
				string? npath = Path.GetDirectoryName(paths[i]);
				if (npath != null){
					if(Folders.Create(npath) == false) {
						Msg.PrintWarningMod("Error creating folder to store the download (maybe exist): "+paths[i],".http",Msg.LogLevels.Verbose);
					}
				}
			}
			// Progress line: the configured reporter or the engine default, unless silenced.
			// One line for the whole batch, reporting the average of the streams.
			bool show = showprogress ?? ShowProgress;
			ITaskProgress? reporter = show ? (Progress ?? Api.Progress.Default) : null;
			DownloadProgress tracker = new DownloadProgress(reporter, uris.Length);
			reporter?.Start("Downloading " + uris.Length + " files");
			// And download the files.
			bool bres;
			List<Stream> StreamList = new List<Stream>();
			try{
				List<Task> DownloadList = new List<Task>();
				for (int i = 0; i < uris.Length;i++){
					Stream file = new FileStream(paths[i], FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
					StreamList.Add(file);
					DownloadList.Add(client.DownloadDataAsync(uris[i], file, tracker.Changed));
				}
				bres = Task.WaitAll(DownloadList.ToArray(),-1);
				if (bres) {
					LastReturnCode = 200;
					LastResponse = "";
				} else {
					LastReturnCode = -1;
					LastResponse = "";
				}
			} catch(Exception ex){
				// Unwrap the aggregate exception to reach the real cause and, if it carries
				// an HTTP status code, store it so the script can inspect LastReturnCode.
				Exception cause = ex;
				if (ex is AggregateException ae && ae.InnerException != null)
					cause = ae.InnerException;
				Msg.PrintErrorMod("Error downloading files: "+cause.Message,".http",Msg.LogLevels.Verbose);
				if (cause is HttpRequestException hre && hre.StatusCode.HasValue)
					LastReturnCode = (int)hre.StatusCode.Value;
				else
					LastReturnCode = -1;
				LastResponse = "";
				FailWith(cause, string.Join(", ", uris));
				reporter?.Finish("failed", ProgressOutcome.Error);
				return false;
			} finally {
				// Dispose all streams, also on failure, to not leave the files locked
				for(int i = 0; i < StreamList.Count;i++){
					StreamList[i].Dispose();
				}
			}
			if (bres == false){
				Msg.PrintErrorMod("Error downloading files.",".http",Msg.LogLevels.Verbose);
				LastError = new ApiError(ErrorCode.Failed, "The downloads did not complete", string.Join(", ", uris));
				reporter?.Finish("failed", ProgressOutcome.Error);
				return false;
			}
			if (executable) {
				foreach (string path in paths) {
					if (!Files.SetExecutable(path)) {
						LastError = Files.LastError;
						reporter?.Finish("failed", ProgressOutcome.Error);
						return false;
					}
				}
			}
			reporter?.Report(1.0);
			reporter?.Finish("done");
			return true;
		}

		/// <summary>
		/// Gets the document from the given uri
		/// </summary>
		/// <param name="uri">Uri for the document to be retrieved</param>
		/// <param name="headers">Optional dictionary of headers to inject in the request</param>
		/// <returns>The string with the document or empty</returns>
		public static string GetDocument(string uri, Dictionary<string, string>? headers = null){
			LastError = ApiError.None;
			HttpClient client = new HttpClient();
			if (headers != null) {
				foreach (var header in headers) {
					client.DefaultRequestHeaders.Add(header.Key, header.Value);
				}
			}
			try{
				Task<HttpResponseMessage> result = client.GetAsync(uri);
				result.Wait();
				// Always store the real status code so the script can inspect it on failures too
				LastReturnCode = (int)result.Result.StatusCode;
				if (result.Result.IsSuccessStatusCode){
					Task<string> content = result.Result.Content.ReadAsStringAsync();
					content.Wait();
					LastResponse = content.Result;
					return content.Result;
				}
				LastResponse = "";
				LastError = new ApiError(ErrorCode.Failed, "HTTP status " + LastReturnCode + " (" + result.Result.StatusCode + ")", uri);
				Msg.PrintErrorMod("Error getting document: "+result.Result.StatusCode,".http",Msg.LogLevels.Verbose);
			} catch(Exception e) {
				Msg.PrintErrorMod("Error getting document: "+e.Message,".http",Msg.LogLevels.Verbose);
				LastReturnCode = -1;
				LastResponse = "";
				FailWith(e is AggregateException ae && ae.InnerException != null ? ae.InnerException : e, uri);
				return string.Empty;
			}
			return string.Empty;
		}

		/// <summary>
		/// Post or patch a document to the given uri
		/// </summary>
		/// <param name="uri">Uri for the document to be posted or patched</param>
		/// <param name="content">The content to be posted or patched</param>
		/// <param name="headers">Optional dictionary of headers to inject in the request</param>
		/// <param name="usePatch">If true, use PATCH method; otherwise, use POST</param>
		/// <returns>True if the document was sent successfully, false otherwise.</returns>
		public static bool PostDocument(string uri, string content, Dictionary<string, string>? headers = null, bool usePatch = false) {
			LastError = ApiError.None;
			HttpClient client = new HttpClient();
			string contentType = "application/json"; // Default for JSON
			if (headers != null) {
				foreach (var header in headers) {
					if (header.Key.ToLower() == "content-type") {
						contentType = header.Value;
					} else {
						client.DefaultRequestHeaders.Add(header.Key, header.Value);
					}
				}
			}
			try {
				var httpContent = new StringContent(content);
				httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
				Task<HttpResponseMessage> result;
				if (usePatch) {
					var request = new HttpRequestMessage(HttpMethod.Patch, uri) { Content = httpContent };
					result = client.SendAsync(request);
				} else {
					result = client.PostAsync(uri, httpContent);
				}
				result.Wait();
				// Fetch the response content to ensure the request is fully completed before checking the status code
				Task<string> response = result.Result.Content.ReadAsStringAsync();
				response.Wait();
				LastReturnCode = (int)result.Result.StatusCode;
				LastResponse = response.Result;
				if (result.Result.IsSuccessStatusCode) {
					return true;
				}
				LastError = new ApiError(ErrorCode.Failed, "HTTP status " + LastReturnCode + " (" + result.Result.StatusCode + ")", uri);
				Msg.PrintErrorMod("Error sending document: " + result.Result.StatusCode, ".http", Msg.LogLevels.Verbose);
			} catch (Exception e) {
				Msg.PrintErrorMod("Error sending document: " + e.Message, ".http", Msg.LogLevels.Verbose);
				LastReturnCode = -1;
				LastResponse = "";
				FailWith(e is AggregateException ae && ae.InnerException != null ? ae.InnerException : e, uri);
				return false;
			}
			return false;
		}

		/// <summary>
		/// Post or patch a file to the given uri
		/// </summary>
		/// <param name="uri">Uri for the file to be posted or patched</param>
		/// <param name="filePath">The path to the file to be posted or patched</param>
		/// <param name="headers">Optional dictionary of headers to inject in the request</param>
		/// <param name="usePatch">If true, use PATCH method; otherwise, use POST</param>
		/// <returns>True if the file was sent successfully, false otherwise.</returns>
		public static bool PostFile(string uri, string filePath, Dictionary<string, string>? headers = null, bool usePatch = false) {
			LastError = ApiError.None;
			HttpClient client = new HttpClient();
			if (headers != null) {
				foreach (var header in headers) {
					if (header.Key.ToLower() != "content-type") { // Filter out Content-Type for multipart
						client.DefaultRequestHeaders.Add(header.Key, header.Value);
					}
				}
			}
			try {
				using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
					var content = new StreamContent(fileStream);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					Task<HttpResponseMessage> result;
					if (usePatch) {
						var request = new HttpRequestMessage(HttpMethod.Patch, uri) { Content = content };
						result = client.SendAsync(request);
					} else {
						result = client.PostAsync(uri, content);
					}
					result.Wait();
					LastReturnCode = (int)result.Result.StatusCode;
					LastResponse = "";
					if (result.Result.IsSuccessStatusCode) {
						return true;
					}
					LastError = new ApiError(ErrorCode.Failed, "HTTP status " + LastReturnCode + " (" + result.Result.StatusCode + ")", uri);
					Msg.PrintErrorMod("Error sending file: " + result.Result.StatusCode, ".http", Msg.LogLevels.Verbose);
				}
			} catch (Exception e) {
				Msg.PrintErrorMod("Error sending file: " + e.Message, ".http", Msg.LogLevels.Verbose);
				LastReturnCode = -1;
				LastResponse = "";
				// A missing local file is a file error on the path, anything else a network failure on the url
				if (e is FileNotFoundException || e is DirectoryNotFoundException)
					LastError = ApiError.From(e, filePath);
				else
					FailWith(e is AggregateException ae && ae.InnerException != null ? ae.InnerException : e, uri);
				return false;
			}
			return false;
		}

		/// <summary>
		/// Deletes a document from the given uri
		/// </summary>
		/// <param name="uri">Uri for the document to be deleted</param>
		/// <param name="headers">Optional dictionary of headers to inject in the request</param>
		/// <returns>True if the document was deleted successfully, false otherwise.</returns>
		public static bool DeleteDocument(string uri, Dictionary<string, string>? headers = null) {
			LastError = ApiError.None;
			HttpClient client = new HttpClient();
			if (headers != null) {
				foreach (var header in headers) {
					client.DefaultRequestHeaders.Add(header.Key, header.Value);
				}
			}
			try {
				Task<HttpResponseMessage> result = client.DeleteAsync(uri);
				result.Wait();
				LastReturnCode = (int)result.Result.StatusCode;
				LastResponse = "";
				if (result.Result.IsSuccessStatusCode) {
					return true;
				}
				LastError = new ApiError(ErrorCode.Failed, "HTTP status " + LastReturnCode + " (" + result.Result.StatusCode + ")", uri);
				Msg.PrintErrorMod("Error deleting document: " + result.Result.StatusCode, ".http", Msg.LogLevels.Verbose);
			} catch (Exception e) {
				Msg.PrintErrorMod("Error deleting document: " + e.Message, ".http", Msg.LogLevels.Verbose);
				LastReturnCode = -1;
				LastResponse = "";
				FailWith(e is AggregateException ae && ae.InnerException != null ? ae.InnerException : e, uri);
				return false;
			}
			return false;
		}



		/// <summary>
		/// Delegate used by the download stream copy to report its progress.
		/// </summary>
		/// <param name="sender">Stream sending the progress report.</param>
		/// <param name="progress">Percentage of that stream, from 0 to 100.</param>
		internal delegate void DownloadProgressChanged(object? sender, float progress);

		/// <summary>
		/// Progress of one download operation: collects the percentage of every stream and reports
		/// the average over the expected number of streams to the reporter of the operation, if any.
		/// </summary>
		private sealed class DownloadProgress {

			private readonly ITaskProgress? reporter;
			private readonly int expected;
			private readonly Dictionary<object, float> streams = new Dictionary<object, float>();

			/// <summary>
			/// Creates the tracker of an operation.
			/// </summary>
			/// <param name="reporter">Reporter to feed, or null when no progress is shown.</param>
			/// <param name="expected">Number of streams the operation downloads.</param>
			public DownloadProgress(ITaskProgress? reporter, int expected) {
				this.reporter = reporter;
				this.expected = Math.Max(1, expected);
			}

			/// <summary>
			/// Receives the progress of one stream and reports the average of the operation.
			/// </summary>
			/// <param name="sender">Stream sending the progress report.</param>
			/// <param name="percent">Percentage of that stream, from 0 to 100.</param>
			public void Changed(object? sender, float percent) {
				if (sender == null || reporter == null)
					return;
				float total = 0;
				lock (streams) {
					streams[sender] = percent;
					foreach (float value in streams.Values)
						total += value;
					total /= Math.Max(expected, streams.Count);
				}
				reporter.Report(total / 100.0);
			}
		}
	}

	/// <summary>
	/// Extensions methods for the HttpClient class
	/// </summary>
	internal static class HttpClientExtensions {

		/// <summary>
		/// Downloads a file from the given uri to the given path asynchronously
		/// </summary>
		/// <param name="client">client to extend</param>
		/// <param name="requestUrl">requested url</param>
		/// <param name="destination">destionation to save</param>
		/// <param name="progress">progress reporting delegate.</param>
		/// <param name="cancellationToken">cancelation token to cancel the operation.</param>
		/// <returns>A task that can be awaited.</returns>
		internal static async Task DownloadDataAsync(this HttpClient client, string requestUrl, Stream destination, Http.DownloadProgressChanged? progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			using (var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead)) {
				if ( (response.StatusCode == HttpStatusCode.Found) || 
					 (response.StatusCode == HttpStatusCode.Moved) || 
					 (response.StatusCode == HttpStatusCode.Redirect) || 
					 (response.StatusCode == HttpStatusCode.TemporaryRedirect) || 
					 (response.StatusCode == HttpStatusCode.PermanentRedirect) ) {
					Msg.PrintWarningMod("The requested url has been redirected to: "+response.Headers.Location,".http",Msg.LogLevels.Verbose);
					if (response.Headers is null || response.Headers.Location is null){
						Msg.PrintWarningMod("The requested url has been redirected but no location was provided.",".http",Msg.LogLevels.Verbose);
						throw new HttpRequestException("Download redirected without location.", null, response.StatusCode);
					}
					await client.DownloadDataAsync(response.Headers.Location.AbsoluteUri, destination, progress, cancellationToken);
					return;
				}
				// Any non success status is a failed download. We throw carrying the status code so the
				// callers (DownloadFile / DownloadFiles) can report the error and store the status,
				// instead of silently reporting success.
				if (!response.IsSuccessStatusCode) {
					Msg.PrintWarningMod("The requested url returned an error status: " + (int)response.StatusCode + " (" + response.StatusCode + ")", ".http", Msg.LogLevels.Verbose);
					throw new HttpRequestException("Download failed with status: " + (int)response.StatusCode + " (" + response.StatusCode + ")", null, response.StatusCode);
				}
				var contentLength = response.Content.Headers.ContentLength;
				if (!contentLength.HasValue) {
					// TODO: To be checked if this is the best way to handle this
					Msg.PrintWarningMod("Progress reporting is not available for this download.",".http",Msg.LogLevels.Verbose);
					using (var download = response.Content.ReadAsStream()) {
						download.CopyTo(destination);
						return;
					}
				} else {
					using (var download = await response.Content.ReadAsStreamAsync()) {
						await download.CopyToAsync(destination,contentLength.Value, 81920, progress, cancellationToken);
					}
				}
			}
		}

		/// <summary>
		/// Copies the content of the source stream to the destination stream asynchronously
		/// </summary>
		/// <param name="source">source stream</param>
		/// <param name="destination">destination stream</param>
		/// <param name="totalbytes">total bytes of the stream</param>
		/// <param name="bufferSize">buffer size to be used.</param>
		/// <param name="progress">progress reporting delegate.</param>
		/// <param name="cancellationToken">cancelation token.</param>
		/// <returns>A task that can be awaited.</returns>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		internal static async Task CopyToAsync(	this Stream source, Stream destination, long totalbytes,
										int bufferSize, Http.DownloadProgressChanged? progress = null, 
										CancellationToken cancellationToken = default(CancellationToken)) {
			if (bufferSize < 0)
				throw new ArgumentOutOfRangeException(nameof(bufferSize));
			if (source is null)
				throw new ArgumentNullException(nameof(source));
			if (!source.CanRead)
				throw new InvalidOperationException($"'{nameof(source)}' is not readable.");
			if (destination == null)
				throw new ArgumentNullException(nameof(destination));
			if (!destination.CanWrite)
				throw new InvalidOperationException($"'{nameof(destination)}' is not writable.");

			var buffer = new byte[bufferSize];
			long totalBytesRead = 0;
			int bytesRead;
			while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0) {
				await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
				totalBytesRead += bytesRead;
				float amount = totalBytesRead;
				amount /= totalbytes;
				amount *= 100.0f;
				progress?.Invoke(source, amount);
			}
		}
	}
}
