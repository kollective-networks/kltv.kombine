/*---------------------------------------------------------------------------------------------------------

	Kombine Build Engine

	(C) Kollective Networks 2026

	Original progress bar code: https://gist.github.com/DanielSWolf/0ab6a96899cc5377bf54

---------------------------------------------------------------------------------------------------------*/

using System.Text;

namespace Kltv.Kombine.Api {

	/// <summary>
	/// Progress bar reporter: [##########----------]  50% /
	/// A spinner alone is shown while no progress was reported, so an operation with unknown size
	/// still shows activity. The bar is drawn in ASCII by default; the width and the characters
	/// can be changed. It prints only the start and end messages when the output is redirected.
	/// </summary>
	public sealed class ProgressBar : ProgressBase {

		private const string Animation = @"|/-\";

		/// <summary>
		/// Number of blocks of the bar.
		/// </summary>
		public int Width { get; set; } = 20;

		/// <summary>
		/// Character used for the completed part of the bar.
		/// </summary>
		public char FilledChar { get; set; } = '#';

		/// <summary>
		/// Character used for the pending part of the bar.
		/// </summary>
		public char EmptyChar { get; set; } = '-';

		/// <inheritdoc/>
		protected override bool Animated { get { return true; } }

		/// <inheritdoc/>
		protected override bool RedirectSafe { get { return false; } }

		/// <inheritdoc/>
		protected override string Render(double value, string? status, bool reported, bool final, int frame) {
			// Unknown progress: spinner only, nothing at the end
			if (!reported)
				return final ? string.Empty : Animation[frame % Animation.Length].ToString();
			int width = Math.Max(1, Width);
			int filled = (int)(value * width);
			int percent = (int)(value * 100);
			StringBuilder text = new StringBuilder();
			text.Append('[');
			text.Append(FilledChar, filled);
			text.Append(EmptyChar, width - filled);
			text.Append("] ");
			text.Append(percent.ToString().PadLeft(3));
			text.Append('%');
			if (!string.IsNullOrEmpty(status)) {
				text.Append(' ');
				text.Append(status);
			}
			if (!final) {
				text.Append(' ');
				text.Append(Animation[frame % Animation.Length]);
			}
			return text.ToString();
		}
	}
}
