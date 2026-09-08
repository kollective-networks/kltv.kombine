/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Compression facility, split into the Zip and Tar formats. This part holds what is common to
	/// every format: the progress line, the file collection and the extraction guard.
	/// </summary>
	public static partial class Compress {

		/// <summary>
		/// Reporter used by every format to show the progress line of a compression or an extraction.
		/// When not set, Progress.Default is used. Assign an instance to select the renderer.
		/// </summary>
		public static ITaskProgress? Progress { get; set; } = null;

		/// <summary>
		/// If the progress line is shown. Each call can override it with its showprogress argument.
		/// </summary>
		public static bool ShowProgress { get; set; } = true;

		/// <summary>
		/// Resolves the reporter of a call: the configured one or the engine default, unless silenced.
		/// </summary>
		/// <param name="showprogress">The showprogress argument of the call. Null takes ShowProgress.</param>
		/// <returns>The reporter to use, or null when the line is silenced.</returns>
		private static ITaskProgress? Reporter(bool? showprogress) {
			bool show = showprogress ?? ShowProgress;
			return show ? (Progress ?? Api.Progress.Default) : null;
		}

		/// <summary>
		/// A file to archive: its path, the name of its entry and its size.
		/// </summary>
		private readonly record struct ArchiveFile(string Path, string Entry, long Size);

		/// <summary>
		/// Collects the files of the folders with their entry names and the total size, which feeds the
		/// progress line. Entry names use '/' so the archives are portable whatever the host.
		/// </summary>
		/// <param name="folderPaths">Folders to collect.</param>
		/// <param name="includeFolder">If the folder itself goes in the archive (entries relative to its parent) or only its contents.</param>
		/// <param name="totalBytes">Sum of the file sizes.</param>
		/// <param name="error">The failure when the collection fails.</param>
		/// <returns>The files, or null when a folder could not be enumerated.</returns>
		private static List<ArchiveFile>? CollectFiles(string[] folderPaths, bool includeFolder, out long totalBytes, out ApiError error) {
			List<ArchiveFile> files = new List<ArchiveFile>();
			totalBytes = 0;
			error = ApiError.None;
			foreach (string folderPath in folderPaths) {
				try {
					DirectoryInfo folder = new DirectoryInfo(folderPath);
					// Entry names are relative to the folder parent when the folder itself goes in, never absolute
					string root = (includeFolder && folder.Parent != null) ? folder.Parent.FullName : folder.FullName;
					foreach (FileInfo file in folder.GetFiles("*", SearchOption.AllDirectories)) {
						string entry = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
						files.Add(new ArchiveFile(file.FullName, entry, file.Length));
						totalBytes += file.Length;
					}
				} catch (Exception ex) {
					error = ApiError.From(ex, folderPath);
					return null;
				}
			}
			return files;
		}

		/// <summary>
		/// Size of a file, zero when it cannot be read: only used to weight the progress line.
		/// </summary>
		private static long FileSize(string path) {
			try {
				return new FileInfo(path).Length;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Safe-extraction boundary check. Mirrors the guard the libraries apply on their own paths: an
		/// entry whose composed path resolves outside the destination folder (archive path traversal,
		/// "zip-slip") is rejected. Used for every entry written through a path composed by us.
		/// </summary>
		/// <param name="outputFolder">Destination root folder.</param>
		/// <param name="candidatePath">Composed target path to validate.</param>
		/// <returns>True if candidatePath resolves inside outputFolder, false otherwise.</returns>
		private static bool IsInsideOutputFolder(string outputFolder, string candidatePath) {
			string root = Path.GetFullPath(outputFolder);
			if (!root.EndsWith(Path.DirectorySeparatorChar) && !root.EndsWith(Path.AltDirectorySeparatorChar))
				root += Path.DirectorySeparatorChar;
			string full = Path.GetFullPath(candidatePath);
			StringComparison cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			if (full.StartsWith(root, cmp))
				return true;
			return string.Equals(full, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), cmp);
		}

		/// <summary>
		/// Builds the error of an extraction that skipped entries. AlreadyExists when the only problem
		/// was existing files with overwrite disabled, Failed when entries were refused or not extracted.
		/// </summary>
		/// <param name="refused">Entries refused because they escape the destination folder.</param>
		/// <param name="failed">Entries that could not be extracted.</param>
		/// <param name="existing">Entries skipped because the file exists and overwrite is disabled.</param>
		/// <param name="archive">The archive, as the error source.</param>
		/// <returns>The error to record.</returns>
		private static ApiError ExtractionError(int refused, int failed, int existing, string archive) {
			List<string> reasons = new List<string>();
			if (existing > 0)
				reasons.Add(existing + " entries already exist and overwrite is disabled");
			if (refused > 0)
				reasons.Add(refused + " entries refused (path traversal)");
			if (failed > 0)
				reasons.Add(failed + " entries not extracted");
			ErrorCode code = (refused == 0 && failed == 0) ? ErrorCode.AlreadyExists : ErrorCode.Failed;
			return new ApiError(code, string.Join(", ", reasons), archive);
		}

		/// <summary>
		/// Progress of one archive operation: opens the line "verb archive: ", reports every entry
		/// weighted by its size (or by count when the sizes are unknown) with the entry count as
		/// status, and closes the line with the outcome. Every call is a no-op when the line is
		/// silenced or not started, so the format code needs no checks.
		/// </summary>
		private sealed class ArchiveProgress {
			private readonly ITaskProgress? reporter;
			private readonly string singular;
			private readonly string plural;
			private long total = 0;
			private int expected = 0;
			private long done = 0;
			private int count = 0;
			private bool started = false;

			/// <summary>
			/// Creates the tracker of one operation.
			/// </summary>
			/// <param name="reporter">Reporter to render on, null when the line is silenced.</param>
			/// <param name="singular">Name of one entry, "file" or "entry".</param>
			/// <param name="plural">Name of several entries.</param>
			public ArchiveProgress(ITaskProgress? reporter, string singular, string plural) {
				this.reporter = reporter;
				this.singular = singular;
				this.plural = plural;
			}

			/// <summary>
			/// Opens the line.
			/// </summary>
			/// <param name="verb">"Compressing" or "Decompressing".</param>
			/// <param name="archive">Archive path, shown by its file name.</param>
			/// <param name="totalBytes">Total size to process, zero when unknown.</param>
			/// <param name="expectedEntries">Number of entries, zero when unknown.</param>
			public void Start(string verb, string archive, long totalBytes, int expectedEntries) {
				if (reporter == null)
					return;
				total = totalBytes;
				expected = expectedEntries;
				done = 0;
				count = 0;
				reporter.Start(verb + " " + Path.GetFileName(archive));
				started = true;
			}

			/// <summary>
			/// One entry processed.
			/// </summary>
			/// <param name="bytes">Size of the entry.</param>
			public void Entry(long bytes) {
				if (!started)
					return;
				done += bytes;
				count++;
				Report();
			}

			/// <summary>
			/// One entry reached in an archive read forward only: the progress is the position in it.
			/// </summary>
			/// <param name="position">Position in the archive being read.</param>
			/// <param name="length">Length of the archive.</param>
			public void Position(long position, long length) {
				if (!started)
					return;
				done = position;
				total = length;
				count++;
				Report();
			}

			/// <summary>
			/// Closes the line as done.
			/// </summary>
			public void Done() {
				if (!started)
					return;
				reporter!.Finish("done");
				started = false;
			}

			/// <summary>
			/// Closes the line as failed. The reason is in the LastError of the format.
			/// </summary>
			public void Failed() {
				if (!started)
					return;
				reporter!.Finish("failed", ProgressOutcome.Error);
				started = false;
			}

			private void Report() {
				double value = 0;
				if (total > 0)
					value = (double)done / total;
				else if (expected > 0)
					value = (double)count / expected;
				string status;
				if (expected > 0)
					status = count + "/" + expected + " " + plural;
				else
					status = count + " " + (count == 1 ? singular : plural);
				reporter!.Report(Math.Clamp(value, 0.0, 1.0), status);
			}
		}
	}
}
