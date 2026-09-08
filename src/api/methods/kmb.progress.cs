/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

using System.Text;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Outcome of a task shown by a progress reporter. It selects the colour of the end message,
	/// the same used by Msg.PrintTaskSuccess, Msg.PrintTaskWarning and Msg.PrintTaskError.
	/// </summary>
	public enum ProgressOutcome {
		Success,
		Warning,
		Error
	}

	/// <summary>
	/// Console progress reporter.
	/// Start opens the line "message: " at the current message indentation and keeps it open,
	/// Report updates the progress rendering and Finish renders the final state, prints the end
	/// message coloured by the outcome and closes the line. The line stays on screen, so the output
	/// reads "start message: progress end message".
	/// A reporter serves one operation at a time and can be reused: Finish resets it, so the next
	/// Start opens a new line on the same instance. Every facility that reports progress exposes a
	/// member of this type which the user assigns to select the renderer.
	/// </summary>
	public interface ITaskProgress : IDisposable {
		/// <summary>
		/// Opens the progress line printing the start message at the current indentation.
		/// </summary>
		/// <param name="message">Start message, printed followed by ": ".</param>
		void Start(string message);
		/// <summary>
		/// Reports the progress of the operation.
		/// </summary>
		/// <param name="value">Progress from 0.0 to 1.0.</param>
		/// <param name="status">Optional status text shown after the progress, for example "12/48".</param>
		void Report(double value, string? status = null);
		/// <summary>
		/// Renders the final state, prints the end message with the colour of the outcome and closes the line.
		/// </summary>
		/// <param name="message">End message. Empty closes the line without message.</param>
		/// <param name="outcome">Outcome of the operation. Success snaps the progress to 100%.</param>
		void Finish(string message = "", ProgressOutcome outcome = ProgressOutcome.Success);
	}

	/// <summary>
	/// Engine wide default reporter, used by every facility whose own Progress member was not set.
	/// It is a progress bar, or the plain renderer when the output is redirected since nobody would
	/// see a progress rendering there. A script may replace it to change every unconfigured facility.
	/// </summary>
	public static class Progress {
		/// <summary>
		/// Reporter used by the facilities that were not configured with their own.
		/// </summary>
		public static ITaskProgress Default { get; set; } = Console.IsOutputRedirected ? new ProgressPlain() : new ProgressBar();
	}

	/// <summary>
	/// Shared behaviour of the reporters: prefix, animation timer, visibility, diff based redraw,
	/// finish and dispose rules. Renderers only provide the text drawn after the prefix.
	/// </summary>
	public abstract class ProgressBase : ITaskProgress {

		private static readonly TimeSpan AnimationInterval = TimeSpan.FromSeconds(1.0 / 8);
		private readonly object sync = new object();
		private Timer? timer = null;
		private bool open = false;
		private bool visible = false;
		private bool reported = false;
		private double value = 0;
		private string? status = null;
		private string rendered = string.Empty;
		private int frame = 0;

		/// <summary>
		/// Colour of the progress rendering. Null draws it with the current console colour.
		/// The end message keeps the colour of its outcome.
		/// </summary>
		public ConsoleColor? Color { get; set; } = null;

		/// <summary>
		/// True when the rendering animates: it is redrawn by a timer at 8 frames per second.
		/// False when it is drawn on every report.
		/// </summary>
		protected abstract bool Animated { get; }

		/// <summary>
		/// True when the rendering is safe on a redirected output: append only, no cursor movement.
		/// A renderer that is not safe prints only the start and end messages when redirected.
		/// </summary>
		protected abstract bool RedirectSafe { get; }

		/// <summary>
		/// Produces the text drawn after the prefix. It replaces the previous text: only the
		/// difference is written to the console.
		/// </summary>
		/// <param name="value">Current progress from 0.0 to 1.0.</param>
		/// <param name="status">Current status text or null.</param>
		/// <param name="reported">False while no progress was reported (indeterminate operation).</param>
		/// <param name="final">True for the last frame, drawn before the end message.</param>
		/// <param name="frame">Frame counter, for animations.</param>
		/// <returns>The text to show after the prefix.</returns>
		protected abstract string Render(double value, string? status, bool reported, bool final, int frame);

		/// <summary>
		/// Called when a new operation starts, so renderers can reset their own state.
		/// </summary>
		protected virtual void OnStart() {
		}

		/// <inheritdoc/>
		public void Start(string message) {
			lock (sync) {
				// A line left open by a previous operation is closed without message
				if (open)
					CloseLine();
				open = true;
				reported = false;
				value = 0;
				status = null;
				rendered = string.Empty;
				frame = 0;
				// Nothing is rendered when the messages are not shown, or when nobody can see it
				visible = (Msg.LogLevel >= Msg.LogLevels.Normal) && (RedirectSafe || !Console.IsOutputRedirected);
				OnStart();
				Msg.PrintTask(message + ": ");
				if (!visible)
					return;
				if (Animated)
					timer = new Timer(TimerHandler, null, AnimationInterval, AnimationInterval);
				else
					Draw(false);
			}
		}

		/// <inheritdoc/>
		public void Report(double value, string? status = null) {
			lock (sync) {
				if (!open)
					return;
				this.value = Math.Max(0, Math.Min(1, value));
				this.status = status;
				reported = true;
				if (visible && !Animated)
					Draw(false);
			}
		}

		/// <inheritdoc/>
		public void Finish(string message = "", ProgressOutcome outcome = ProgressOutcome.Success) {
			lock (sync) {
				if (!open)
					return;
				StopTimer();
				if (outcome == ProgressOutcome.Success)
					value = 1;
				if (visible)
					Draw(true);
				open = false;
				if (string.IsNullOrEmpty(message)) {
					Msg.RawPrint(Environment.NewLine);
					return;
				}
				// The end message is separated from the rendering, when there is one
				string text = (rendered.Length > 0 ? " " : "") + message;
				switch (outcome) {
					case ProgressOutcome.Warning:
						Msg.PrintTaskWarning(text);
						break;
					case ProgressOutcome.Error:
						Msg.PrintTaskError(text);
						break;
					default:
						Msg.PrintTaskSuccess(text);
						break;
				}
			}
		}

		/// <summary>
		/// Closes the line if the operation was not finished, keeping the last rendering and
		/// printing no message. The instance can still be reused afterwards.
		/// </summary>
		public void Dispose() {
			lock (sync) {
				if (open)
					CloseLine();
			}
		}

		private void CloseLine() {
			StopTimer();
			open = false;
			Msg.RawPrint(Environment.NewLine);
		}

		private void TimerHandler(object? state) {
			lock (sync) {
				if (!open || timer == null)
					return;
				Draw(false);
			}
		}

		private void StopTimer() {
			timer?.Dispose();
			timer = null;
		}

		/// <summary>
		/// Draws the rendering writing only the difference with the text already on screen:
		/// the common prefix is kept, the rest is erased with backspaces and the new suffix is
		/// written. An append only renderer never produces backspaces.
		/// </summary>
		private void Draw(bool final) {
			string text = Render(value, status, reported, final, frame++);
			if (text == rendered)
				return;
			int common = 0;
			int max = Math.Min(rendered.Length, text.Length);
			while (common < max && text[common] == rendered[common])
				common++;
			StringBuilder output = new StringBuilder();
			output.Append('\b', rendered.Length - common);
			output.Append(text, common, text.Length - common);
			int overlap = rendered.Length - text.Length;
			if (overlap > 0) {
				output.Append(' ', overlap);
				output.Append('\b', overlap);
			}
			if (Color.HasValue) {
				Console.ForegroundColor = Color.Value;
				Console.Write(output.ToString());
				Console.ResetColor();
			} else {
				Console.Write(output.ToString());
			}
			rendered = text;
		}
	}

	/// <summary>
	/// Reporter without progress rendering: only the start and end messages are printed.
	/// It is the default when the output is redirected.
	/// </summary>
	public sealed class ProgressPlain : ProgressBase {

		/// <inheritdoc/>
		protected override bool Animated { get { return false; } }

		/// <inheritdoc/>
		protected override bool RedirectSafe { get { return true; } }

		/// <inheritdoc/>
		protected override string Render(double value, string? status, bool reported, bool final, int frame) {
			return string.Empty;
		}
	}
}
