/*---------------------------------------------------------------------------------------------------------

	Kombine Base Progress Reporting Example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.Threading;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int passed = 0;
int failed = 0;

/// <summary>
/// Exercises the progress reporting: the defaults, the three renderers, the status text, the
/// outcomes, an operation of unknown size, the reuse of a reporter and the indentation.
/// The rendering is only visible on a console: when the output is redirected only the start
/// and end messages are shown, so the checks verify that every operation completes.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check passed, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing progress reporting");
	Msg.BeginIndent();
	Msg.Print("Output is " + (Console.IsOutputRedirected ? "redirected: only the start and end messages are shown" : "a console: the progress is rendered live"));
	Msg.RawPrint(Environment.NewLine);

	//
	// Defaults: the engine default depends on the output, the downloads are not configured
	//
	Banner("[1/8] Defaults");
	Check("Progress.Default", Progress.Default.GetType().Name, Console.IsOutputRedirected ? "ProgressPlain" : "ProgressBar");
	Check("Http.Progress", Http.Progress == null ? "not set (uses Progress.Default)" : Http.Progress.GetType().Name, "not set (uses Progress.Default)");
	Check("Http.ShowProgress", Show(Http.ShowProgress), "true");
	EndBanner();

	//
	// Progress bar: a plain run and a run with a status text, reusing the same reporter
	//
	Banner("[2/8] Progress bar");
	ITaskProgress bar = new ProgressBar();
	Demo("bar", () => Simulate(bar, "Packing assets", 20, 40, false));
	Demo("bar with status", () => Simulate(bar, "Packing assets", 20, 40, true));
	EndBanner();

	//
	// Dots: append only, safe when the output is piped
	//
	Banner("[3/8] Dots");
	ITaskProgress dots = new ProgressDots();
	Demo("dots", () => Simulate(dots, "Cloning repository", 10, 50, false));
	Demo("dots with status", () => Simulate(dots, "Cloning repository", 10, 50, true));
	EndBanner();

	//
	// Plain: only the messages
	//
	Banner("[4/8] Plain");
	ITaskProgress plain = new ProgressPlain();
	Demo("plain", () => Simulate(plain, "Copying files", 10, 30, true));
	EndBanner();

	//
	// Outcomes and an operation of unknown size (no progress reported: spinner only)
	//
	Banner("[5/8] Outcomes and unknown size");
	Demo("warning outcome", () => Simulate(bar, "Compiling 48 units", 12, 30, true, "done with 2 warnings", ProgressOutcome.Warning));
	Demo("error outcome", () => Simulate(bar, "Linking application", 5, 40, false, "failed", ProgressOutcome.Error));
	Demo("unknown size", () => {
		bar.Start("Waiting for the server");
		Thread.Sleep(600);
		bar.Finish("done");
	});
	EndBanner();

	//
	// Indentation and dispose: the line follows the current indentation, dispose closes an open line
	//
	Banner("[6/8] Indentation and dispose");
	Msg.Print("A nested step:");
	Msg.BeginIndent();
	Demo("nested bar", () => Simulate(bar, "Nested operation", 10, 30, false));
	Msg.EndIndent();
	Demo("dispose closes the line", () => {
		using (ITaskProgress temp = new ProgressBar()) {
			temp.Start("Interrupted operation");
			Thread.Sleep(200);
			temp.Report(0.4);
			Thread.Sleep(200);
		}
	});
	EndBanner();

	//
	// Styles: the colour of the rendering, and for the bar the glyphs and the width, are configurable
	//
	Banner("[7/8] Styles");
	ITaskProgress unicodeBar = new ProgressBar { FilledChar = '█', EmptyChar = '·', Color = ConsoleColor.Green };
	Demo("unicode green bar", () => Simulate(unicodeBar, "Building library", 15, 40, true));
	ITaskProgress narrowBar = new ProgressBar { Width = 10, FilledChar = '=', EmptyChar = ' ', Color = ConsoleColor.Cyan };
	Demo("narrow cyan bar", () => Simulate(narrowBar, "Indexing sources", 10, 40, false));
	ITaskProgress yellowBar = new ProgressBar { Width = 40, Color = ConsoleColor.Yellow };
	Demo("wide yellow bar", () => Simulate(yellowBar, "Packaging", 20, 30, true));
	ITaskProgress greenDots = new ProgressDots { Color = ConsoleColor.Green };
	Demo("green dots", () => Simulate(greenDots, "Fetching submodules", 10, 40, false));
	EndBanner();

	//
	// Facilities: the reporter of a facility is selected by assigning its Progress member.
	// Nothing is downloaded here, the network example shows the downloads reporting with dots.
	//
	Banner("[8/8] Facility configuration");
	Http.Progress = new ProgressDots();
	Check("Http.Progress set", Http.Progress == null ? "null" : Http.Progress.GetType().Name, "ProgressDots");
	Http.Progress = null;
	Check("Http.Progress reset", Http.Progress == null ? "not set (uses Progress.Default)" : "set", "not set (uses Progress.Default)");
	Http.ShowProgress = false;
	Check("Http.ShowProgress off", Show(Http.ShowProgress), "false");
	Http.ShowProgress = true;
	Check("Http.ShowProgress on", Show(Http.ShowProgress), "true");
	Folders.Progress = new ProgressDots();
	Check("Folders.Progress set", Folders.Progress == null ? "null" : Folders.Progress.GetType().Name, "ProgressDots");
	Folders.Progress = null;
	Check("Folders.Progress reset", Folders.Progress == null ? "not set (uses Progress.Default)" : "set", "not set (uses Progress.Default)");
	ITaskProgress previous = Progress.Default;
	Progress.Default = new ProgressDots { Color = ConsoleColor.Green };
	Check("Progress.Default set", Progress.Default.GetType().Name, "ProgressDots");
	Demo("default used", () => Simulate(Progress.Default, "Unconfigured facility", 10, 30, false));
	Progress.Default = previous;
	Check("Progress.Default restored", Progress.Default.GetType().Name, previous.GetType().Name);
	EndBanner();

	int total = passed + failed;
	Msg.PrintTask($"Summary : {passed} of {total} checks passed ");
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");

	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"Progress reporting tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Runs a simulated operation on the given reporter: start, a number of reports and the finish.
/// </summary>
/// <param name="progress">Reporter to use.</param>
/// <param name="message">Start message.</param>
/// <param name="steps">Number of reports.</param>
/// <param name="delay">Milliseconds between reports.</param>
/// <param name="withStatus">If a "step/steps" status is reported.</param>
/// <param name="end">End message.</param>
/// <param name="outcome">Outcome of the operation.</param>
void Simulate(ITaskProgress progress, string message, int steps, int delay, bool withStatus, string end = "done", ProgressOutcome outcome = ProgressOutcome.Success){
	progress.Start(message);
	for (int i = 1; i <= steps; i++){
		Thread.Sleep(delay);
		progress.Report(i / (double)steps, withStatus ? $"{i}/{steps}" : null);
	}
	progress.Finish(end, outcome);
}

/// <summary>
/// Runs a demonstration and reports whether it completed without exceptions.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="action">Demonstration to run.</param>
void Demo(string what, Action action){
	bool ok = true;
	string error = "";
	try {
		action();
	} catch (Exception ex) {
		ok = false;
		error = ex.Message;
	}
	Verify(what, ok ? "completed" : "threw " + error, ok, "no exception");
}

/// <summary>
/// Prints the banner of a group of checks and indents its lines.
/// </summary>
/// <param name="title">Group title.</param>
void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
}

/// <summary>
/// Closes a group of checks.
/// </summary>
void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

/// <summary>
/// Compares the actual value against the expected one and reports the outcome.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value.</param>
/// <param name="expected">Expected value.</param>
void Check(string what, string actual, string expected){
	Verify(what, actual, actual == expected, expected);
}

/// <summary>
/// Prints an aligned line with the actual value and a colored OK / FAILED tag.
/// On failure the expected value is printed below in red.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value to be shown.</param>
/// <param name="ok">Outcome of the check.</param>
/// <param name="expected">Expected value, shown when the check failed.</param>
void Verify(string what, string actual, bool ok, string expected){
	Msg.PrintTask($"{what,-24} : {actual,-36} ");
	if (ok){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-24} : {expected}");
	}
}

/// <summary>
/// Renders a boolean as true / false
/// </summary>
/// <param name="value">Value to render.</param>
/// <returns>The rendered value.</returns>
string Show(bool value){
	return value ? "true" : "false";
}
