/*---------------------------------------------------------------------------------------------------------

	Kombine Log Handler Example

	(C)Kollective Networks 2026

	Msg.OnMessage receives every message of the engine and of every script of the run before it is
	written to the console, so a script filters the log and forwards it to another facility or log
	system. This example installs a handler that records what it receives and checks: the kinds of
	the script messages, the indentation, an engine message printed below the current log level, a
	message from a child script, that the handler's own messages do not come back to it, and that an
	exception thrown by the handler does not stop the script.

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
// The results of the checks, for the examples runner
#load "mkb.results.csx"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;

int passed = 0;
int failed = 0;
List<string> received = new List<string>();

/// <summary>
/// Installs the handler, prints through every method, runs a child script and checks what arrived.
/// </summary>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing the log handler");
	Msg.BeginIndent();
	// The handler of the caller, if any, is put back after each test
	Msg.MessageHandler? previous = Msg.OnMessage;
	Msg.OnMessage = (Msg.LogLevels level, Msg.MessageKind kind, string module, string message, int indent) => {
		received.Add(level + "|" + kind + "|" + module + "|" + message + "|" + indent);
	};
	received.Clear();
	int depth = Msg.Indent;
	Msg.Print("a plain line");
	Msg.PrintWarning("a warning");
	Msg.PrintError("an error");
	Msg.PrintTask("a task:");
	Msg.PrintTaskSuccess(" done");
	Msg.BeginIndent();
	Msg.Print("a nested line");
	Msg.EndIndent();
	Msg.Print("a verbose line", Msg.LogLevels.Verbose);
	// An engine message printed below the current level: the registry says at verbose level that
	// the object is missing
	Share.Get("no-such-object");
	// A child script: the same handler receives its messages
	Kombine("mkb.log.csx", "child", args);
	Msg.OnMessage = previous;
	Check("script lines received", string.Join(",", received.Where(r => r.EndsWith("|" + depth)).Select(r => r.Split('|')[1]).Take(5)), "Normal,Warning,Error,Task,TaskSuccess");
	Check("text without indentation", received.First(r => r.Contains("|a plain line|")).Split('|')[3], "a plain line");
	Check("nested line at depth + 1", received.First(r => r.Contains("|a nested line|")).Split('|')[4], (depth + 1).ToString());
	Check("verbose line received at normal level", Show(received.Any(r => r.StartsWith("Verbose|Normal||a verbose line|"))), "true");
	Check("engine message received with its module", Show(received.Any(r => r.Contains("|.share|") && r.Contains("no-such-object"))), "true");
	Check("child script message received", Show(received.Any(r => r.Contains("|from the child|"))), "true");
	// The handler prints: its message reaches the console, not the handler
	received.Clear();
	Msg.OnMessage = (Msg.LogLevels level, Msg.MessageKind kind, string module, string message, int indent) => {
		received.Add(message);
		Msg.Print("handler echo of " + message, Msg.LogLevels.Verbose);
	};
	Msg.Print("one line");
	Msg.OnMessage = previous;
	Check("handler messages not delivered again", string.Join(",", received), "one line");
	// A handler that throws does not stop the script
	Msg.OnMessage = (Msg.LogLevels level, Msg.MessageKind kind, string module, string message, int indent) => {
		throw new Exception("handler failure");
	};
	bool survived = true;
	try {
		Msg.Print("printed through a failing handler");
	} catch {
		survived = false;
	}
	Msg.OnMessage = previous;
	Check("handler exception contained", Show(survived), "true");
	int total = passed + failed;
	TestSummary(passed, failed);
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return failed == 0 ? 0 : 1;
}

/// <summary>
/// The child script action: prints one line the parent's handler must receive.
/// </summary>
int child(string[] args){
	Msg.Print("from the child");
	return 0;
}

string Show(bool value){
	return value ? "true" : "false";
}

void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-40} : {actual,-44} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-40} : {expected}");
	}
	TestResult(actual == expected ? "OK" : "FAILED", what + " : " + actual);
}
