/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

using System.Net;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Http Methods API
	/// </summary>
	public class Http {

		/// <summary>
		/// Downloads a file from the given uri to the given path
		/// </summary>
		/// <param name="uri">The uri for the file to be downloaded</param>
		/// <param name="path">The resulting path for the file.</param>
		/// <param name="showprogress">If true, a progress bar will be shown</param>
		/// <returns>True if file was downloaded, false otherwise.</returns>
		public static bool DownloadFile(string uri,string path,bool showprogress = true) {
			Msg.Print("Download started: "+uri);
			HttpClient client = new HttpClient();
			
			// TODO: Credentials. User agent, headers,etc.
			// Credentials should be taken from config, never from the script as parameter
			// This way we decouple what is the script from the configuration
			// [...]

			// Create download folder if does not exists
			string? npath = Path.GetDirectoryName(path);
			if (npath != null){
				if(Folders.Create(npath) == false) {
					Msg.PrintWarningMod("Error creating folder to store the download (maybe exist): "+path,".http",Msg.LogLevels.Verbose);
				}
			}
			// And download the file.
			if (showprogress){
				bar = new ProgressBar();
				progress = new Dictionary<object, float>();
			}
			using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)) {
				try{
					if (showprogress){
						client.DownloadDataAsync(uri, file, Progress_ProgressChanged).Wait();
					} else {
						client.DownloadDataAsync(uri, file).Wait();
					}
				} catch(Exception e){
					Msg.PrintErrorMod("Error downloading file: "+e.Message,".http",Msg.LogLevels.Verbose);
					return false;
				}
			}
			bar?.Dispose();
			progress?.Clear();
			bar = null;
			Msg.Print("Download finished");
			return true;
		}

		/// <summary>
		/// Download multiple files from the given uris to the given paths
		/// </summary>
		/// <param name="uris">Arrays of uris to be used</param>
		/// <param name="paths">Array of paths+filenames to be used</param>
		/// <param name="showprogress">If the progress should be show, default true.</param>
		/// <returns>True if all files download fine, false otherwise.</returns>
		public static bool DownloadFiles(string[] uris,string[] paths,bool showprogress = true){
			if (uris.Length != paths.Length){
				Msg.PrintErrorMod("The number of uris and paths must be the same.",".http",Msg.LogLevels.Verbose);
				return false;
			}
			Msg.Print("Downloads started ");
			HttpClient client = new HttpClient();
			
			// TODO: Credentials. User agent, headers,etc.
			// Credentials should be taken from config, never from the script as parameter
			// This way we decouple what is the script from the configuration
			// [...]

			for(int i = 0; i < uris.Length; i++){
				// Create download folders if does not exists
				string? npath = Path.GetDirectoryName(paths[i]);
				if (npath != null){
					if(Folders.Create(npath) == false) {
						Msg.PrintWarningMod("Error creating folder to store the download (maybe exist): "+paths[i],".http",Msg.LogLevels.Verbose);
					}
				}
			}
			// And download the file.
			if (showprogress){
				bar = new ProgressBar();
				progress = new Dictionary<object, float>();
			}
			bool bres;
			try{
				List<Task> DownloadList = new List<Task>();
				List<Stream> StreamList = new List<Stream>();
				for (int i = 0; i < uris.Length;i++){
					Stream file = new FileStream(paths[i], FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
					StreamList.Add(file);
					if (showprogress)
						DownloadList.Add(client.DownloadDataAsync(uris[i], file, Progress_ProgressChanged));
					else
						DownloadList.Add(client.DownloadDataAsync(uris[i], file));
				}
				bres = Task.WaitAll(DownloadList.ToArray(),-1);
				// Dispose all streams
				for(int i = 0; i < StreamList.Count;i++){
					StreamList[i].Dispose();
				}
			} catch(Exception ex){
				Msg.PrintErrorMod("Error downloading file: "+ex.Message,".http",Msg.LogLevels.Verbose);
				progress?.Clear();
				bar?.Dispose();
				bar = null;
				return false;
			}
			progress?.Clear();
			bar?.Dispose();
			bar = null;
			if (bres == false){
				Msg.PrintErrorMod("Error downloading files.",".http",Msg.LogLevels.Verbose);
				return false;
			}
			Msg.Print("Download finished");
			return true;
		}

		/// <summary>
		/// Gets the document from the given uri
		/// </summary>
		/// <param name="uri">Uri for the document to be retrieved</param>
		/// <returns>The string with the document or empty</returns>
		public static string GetDocument(string uri){
			HttpClient client = new HttpClient();
			
			try{
				Task<HttpResponseMessage> result = client.GetAsync(uri);
				result.Result.EnsureSuccessStatusCode();
				result.Wait();
				if (result.Result.IsSuccessStatusCode){
					Task<string> content = result.Result.Content.ReadAsStringAsync();
					content.Wait();
					return content.Result;
				}
				Msg.PrintErrorMod("Error getting document: "+result.Result.StatusCode,".http",Msg.LogLevels.Verbose);
			} catch(Exception e) {
				Msg.PrintErrorMod("Error getting document: "+e.Message,".http",Msg.LogLevels.Verbose);
				return string.Empty;
			}
			return string.Empty;
		}

		/// <summary>
		/// A progress bar instance to show download progress
		/// </summary>
		internal static ProgressBar? bar = null; 

		/// <summary>
		///  A delegate to report progress on downloads
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="progress"></param>
		internal delegate void ProgressBarChanged(object? sender, float progress);

		/// <summary>
		/// Stores the dictionary of progress for each download to make an average.
		/// </summary>
		internal static Dictionary<object,float>? progress = null;

		/// <summary>
		/// It receives all the progress events from the download and reports them to the progress bar
		/// </summary>
		/// <param name="sender">stream sending the progress report.</param>
		/// <param name="progress">amount of progress in that stream</param>
		internal static void Progress_ProgressChanged(object? sender, float progress) {
			if (sender is null) return;
			if (Http.progress is null) return;
			if (Http.progress.ContainsKey(sender)){
				Http.progress[sender] = progress;
			} else {
				Http.progress.Add(sender,progress);
			}
			float total = 0;
			foreach(var i in Http.progress){
				total += i.Value;
			}
			total /= Http.progress.Count;
			bar?.Report((double)total / 100);
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
		internal static async Task DownloadDataAsync(this HttpClient client, string requestUrl, Stream destination, Http.ProgressBarChanged? progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			using (var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead)) {
				if ( (response.StatusCode == HttpStatusCode.Found) || 
					 (response.StatusCode == HttpStatusCode.Moved) || 
					 (response.StatusCode == HttpStatusCode.Redirect) || 
					 (response.StatusCode == HttpStatusCode.TemporaryRedirect) || 
					 (response.StatusCode == HttpStatusCode.PermanentRedirect) ) {
					Msg.PrintWarningMod("The requested url has been redirected to: "+response.Headers.Location,".http",Msg.LogLevels.Verbose);
					if (response.Headers is null){
						Msg.PrintWarningMod("The requested url has been redirected but no headers were provided.",".http",Msg.LogLevels.Verbose);
						return;
					}
					if (response.Headers.Location is null){
						Msg.PrintWarningMod("The requested url has been redirected but no location was provided.",".http",Msg.LogLevels.Verbose);
						return;
					}
					await client.DownloadDataAsync(response.Headers.Location.AbsoluteUri, destination, progress, cancellationToken);
					return;
				}  else if (response.StatusCode == HttpStatusCode.NoContent) {
					Msg.PrintWarningMod("The requested url has no content.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.NotFound) {
					Msg.PrintWarningMod("The requested url was not found.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.Unauthorized) {
					Msg.PrintWarningMod("The requested url requires authentication.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.Forbidden) {
					Msg.PrintWarningMod("The requested url is forbidden.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.InternalServerError) {
					Msg.PrintWarningMod("The requested url has an internal server error.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.ServiceUnavailable) {
					Msg.PrintWarningMod("The requested url is unavailable.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.BadGateway) {
					Msg.PrintWarningMod("The requested url has a bad gateway.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.GatewayTimeout) {
					Msg.PrintWarningMod("The requested url has a gateway timeout.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.RequestTimeout) {
					Msg.PrintWarningMod("The requested url has a request timeout.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable) {
					Msg.PrintWarningMod("The requested url has a range not satisfiable.",".http",Msg.LogLevels.Verbose);
					return;
				} else if (response.StatusCode == HttpStatusCode.NotImplemented) {
					Msg.PrintWarningMod("The requested url has not been implemented.",".http",Msg.LogLevels.Verbose);
					return;
				}
				var contentLength = response.Content.Headers.ContentLength;
				if (!contentLength.HasValue) {
					/// TODO: To be checked if this is the best way to handle this
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
										int bufferSize, Http.ProgressBarChanged? progress = null, 
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
