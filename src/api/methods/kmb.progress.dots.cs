/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Dots reporter: one dot per completed step, ten steps in total, appended to the line.
	/// It never moves the cursor, so the output stays clean when it is piped or logged.
	/// The status text is shown only in the final rendering.
	/// </summary>
	public sealed class ProgressDots : ProgressBase {

		private const int Steps = 10;
		private int printed = 0;

		/// <inheritdoc/>
		protected override bool Animated { get { return false; } }

		/// <inheritdoc/>
		protected override bool RedirectSafe { get { return true; } }

		/// <inheritdoc/>
		protected override void OnStart() {
			printed = 0;
		}

		/// <inheritdoc/>
		protected override string Render(double value, string? status, bool reported, bool final, int frame) {
			// Dots are never taken back, so the rendering is append only
			int target = (int)Math.Round(value * Steps);
			if (target > printed)
				printed = target;
			string text = new string('.', printed);
			if (final && !string.IsNullOrEmpty(status))
				text += " " + status;
			return text;
		}
	}
}
