using System.Text.Json;
using Agilent66332A.Application.Runs;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;
using Agilent66332A.Instruments.Devices;
using Agilent66332A.Instruments.Simulation;
using Agilent66332A.Storage.Preferences;
using Agilent66332A.Storage.Recordings;
using Agilent66332A.Storage.Sequences;

// The headless entry point deliberately supports simulation only in the preview.
string root = args.SkipWhile(a => a != "--recordings").Skip(1).FirstOrDefault() ?? UserPaths.Recordings;
string? sequencePath = args.SkipWhile(a => a != "--sequence").Skip(1).FirstOrDefault();
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
var clock = new SystemClock();
await using var instrument = new AgilentClient(new SimulatedTransport(), clock, true, "Simulator");
try
{
    await instrument.ConnectAsync(new(), cancel.Token);
    var runner = new SequenceRunner(instrument, clock, new RecordingFactory(() => root));
    runner.Progress += p => Console.WriteLine($"{p.Phase}: row {p.Row}, repeat {p.Repeat} — {p.Message}");
    var document = sequencePath is null ? SequenceDocument.Example : await SequenceRepository.LoadAsync(sequencePath, cancel.Token);
    var result = await runner.RunAsync(document, new(), 1, cancel.Token);
    Console.WriteLine(JsonSerializer.Serialize(result, JsonFiles.Options));
    Console.WriteLine("Recording: " + runner.RecordingDirectory);
    return result.ExecutionOutcome == ExecutionOutcome.Completed && result.Shutdown.Outcome == ShutdownOutcome.VerifiedOff && result.RecordingOutcome == RecordingOutcome.Complete ? 0 : 1;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
