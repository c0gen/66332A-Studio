using System.Globalization;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Simulation;

public sealed partial class SimulatedTransport
{
    private readonly Queue<(int Code, string Message)> simulatedErrors = new();
    private readonly SimulatedStatusRegister simulatedOperation = new();
    private readonly SimulatedStatusRegister simulatedQuestionable = new();
    private int simulatedStandardEvent;
    private int simulatedEventEnable;
    private int simulatedServiceEnable;
    private bool simulatedPowerOnClear = true;

    private bool TrySimulateDiagnostics(string command, out string? reply)
    {
        CaptureSimulatedStatus();
        if (TrySimulateStatusGroup(command, "STAT:OPER:", simulatedOperation, out reply) ||
            TrySimulateStatusGroup(command, "STAT:QUES:", simulatedQuestionable, out reply)) return true;

        switch (command)
        {
            case "STAT:PRES": simulatedOperation.Preset(); simulatedQuestionable.Preset(); return true;
            case "*CLS": ClearSimulatedEvents(); return true;
            case "*STB?": reply = StatusNumber(SimulatedStatusByte()); return true;
            case "*OPC?": reply = !acquisitionArmed && !transientArmed ? "1" : null; return true;
            case "*OPC": simulatedStandardEvent |= 1; return true;
            case "*ESR?": reply = StatusNumber(simulatedStandardEvent); simulatedStandardEvent = 0; return true;
            case "*ESE?": reply = StatusNumber(simulatedEventEnable); return true;
            case "*SRE?": reply = StatusNumber(simulatedServiceEnable); return true;
            case "*PSC?": reply = simulatedPowerOnClear ? "1" : "0"; return true;
            case "SYST:ERR?":
                var error = simulatedErrors.TryDequeue(out var entry) ? entry : (0, "No error");
                reply = StatusNumber(error.Item1) + ",\"" + error.Item2 + "\"";
                return true;
        }

        var split = command.Split(' ', 2);
        if (split.Length != 2) return false;
        switch (split[0])
        {
            case "*ESE": simulatedEventEnable = ScpiParser.Register(split[1]); return true;
            case "*SRE": simulatedServiceEnable = ScpiParser.Register(split[1]) & ~64; return true;
            case "*PSC": simulatedPowerOnClear = split[1] == "1"; return true;
            default: return false;
        }
    }

    private void CaptureSimulatedStatus()
    {
        simulatedOperation.Update(SimulatedOperationCondition);
        simulatedQuestionable.Update(questionable);
    }

    private void ClearSimulatedEvents()
    {
        simulatedOperation.Events = simulatedQuestionable.Events = simulatedStandardEvent = 0;
        simulatedErrors.Clear();
    }

    private int SimulatedStatusByte()
    {
        int status = 0;
        if ((simulatedOperation.Events & simulatedOperation.Enable) != 0) status |= 128;
        if ((simulatedQuestionable.Events & simulatedQuestionable.Enable) != 0) status |= 8;
        if ((simulatedStandardEvent & simulatedEventEnable) != 0) status |= 32;
        if ((status & simulatedServiceEnable) != 0) status |= 64;
        return status;
    }

    private static bool TrySimulateStatusGroup(string command, string prefix, SimulatedStatusRegister register, out string? reply)
    {
        reply = null;
        if (!command.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var split = command[prefix.Length..].Split(' ', 2);
        switch (split[0])
        {
            case "EVEN?": reply = StatusNumber(register.Events); register.Events = 0; return true;
            case "ENAB?": reply = StatusNumber(register.Enable); return true;
            case "PTR?": reply = StatusNumber(register.PositiveTransitions); return true;
            case "NTR?": reply = StatusNumber(register.NegativeTransitions); return true;
            case "ENAB": register.Enable = ScpiParser.Register(split[1]); return true;
            case "PTR": register.PositiveTransitions = ScpiParser.Register(split[1]); return true;
            case "NTR": register.NegativeTransitions = ScpiParser.Register(split[1]); return true;
            default: return false;
        }
    }

    private void SimulatedError(int code, string message)
    {
        if (simulatedErrors.Count < 9) simulatedErrors.Enqueue((code, message));
        else if (simulatedErrors.Count == 9) simulatedErrors.Enqueue((-350, "Too many errors"));
        simulatedStandardEvent |= code switch { <= -400 => 4, <= -300 => 8, <= -200 => 16, < 0 => 32, _ => 8 };
    }

    private static string StatusNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class SimulatedStatusRegister
    {
        public int Condition { get; private set; }
        public int Events { get; set; }
        public int Enable { get; set; }
        public int PositiveTransitions { get; set; } = 32727;
        public int NegativeTransitions { get; set; }

        public void Preset()
        {
            Enable = NegativeTransitions = 0;
            PositiveTransitions = 32727;
        }

        public void Update(int updated)
        {
            Events |= (updated & ~Condition & PositiveTransitions) | (Condition & ~updated & NegativeTransitions);
            Condition = updated;
        }
    }
}
