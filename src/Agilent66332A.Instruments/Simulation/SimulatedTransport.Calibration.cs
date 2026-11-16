using System.Globalization;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Simulation;

public sealed partial class SimulatedTransport
{
    private decimal simulatedCalibrationPasscode;
    private bool simulatedCalibrationEnabled;
    private string? simulatedCalibrationRoutine;
    private int simulatedCalibrationExpectedPoints;
    private int simulatedCalibrationAcceptedPoints;
    private bool simulatedCalibrationLevelSelected;

    private bool TrySimulateCalibration(string command, out string? reply)
    {
        reply = null;
        if (!command.StartsWith("CAL:", StringComparison.Ordinal)) return false;
        if (command == "CAL:STAT?") { reply = simulatedCalibrationEnabled ? "1" : "0"; return true; }
        if (command == "CAL:STAT 0") { EndSimulatedCalibration(); return true; }
        if (command == "CAL:STAT 1" || command.StartsWith("CAL:STAT 1,", StringComparison.Ordinal))
        {
            int separator = command.IndexOf(',');
            var entered = separator < 0 ? 0m : decimal.Parse(command.AsSpan(separator + 1), CultureInfo.InvariantCulture);
            if (entered != simulatedCalibrationPasscode) SimulatedError(402, "CAL password is incorrect");
            else simulatedCalibrationEnabled = true;
            return true;
        }
        if (!simulatedCalibrationEnabled) { SimulatedError(403, "CAL not enabled"); return true; }
        if (command.StartsWith("CAL:PASS ", StringComparison.Ordinal))
        {
            simulatedCalibrationPasscode = decimal.Parse(command.AsSpan(9), CultureInfo.InvariantCulture);
            return true;
        }
        if (command == "CAL:SAVE")
        {
            if (simulatedCalibrationRoutine is null || simulatedCalibrationExpectedPoints != simulatedCalibrationAcceptedPoints)
                SimulatedError(406, "Incorrect sequence of calibration commands");
            return true;
        }
        if (!output) { SimulatedError(407, "CV or CC status is incorrect for this command"); return true; }
        if (command is "CAL:VOLT" or "CAL:VOLT:PROT" or "CAL:CURR" or "CAL:CURR:NEG" or "CAL:CURR:MEAS:LOWR" or "CAL:CURR:MEAS:AC")
        {
            if (command == "CAL:CURR:MEAS:AC" && FastOutputMode)
                SimulatedError(408, "Output mode switch must be in NORMAL position");
            else
            {
                simulatedCalibrationRoutine = command;
                simulatedCalibrationExpectedPoints = command switch { "CAL:VOLT" or "CAL:CURR" => 2, "CAL:CURR:MEAS:LOWR" => 1, _ => 0 };
            }
            return true;
        }
        if (command.StartsWith("CAL:LEV ", StringComparison.Ordinal) && simulatedCalibrationRoutine is not null &&
            !simulatedCalibrationLevelSelected && simulatedCalibrationAcceptedPoints < simulatedCalibrationExpectedPoints &&
            command.EndsWith(simulatedCalibrationAcceptedPoints == 0 ? "P1" : "P2", StringComparison.Ordinal))
        {
            simulatedCalibrationLevelSelected = true;
            return true;
        }
        if (command.StartsWith("CAL:DATA ", StringComparison.Ordinal) && simulatedCalibrationLevelSelected)
        {
            _ = ScpiParser.Number(command[9..]);
            simulatedCalibrationAcceptedPoints++;
            simulatedCalibrationLevelSelected = false;
            return true;
        }
        SimulatedError(406, "Incorrect sequence of calibration commands");
        return true;
    }

    private void EndSimulatedCalibration()
    {
        simulatedCalibrationEnabled = simulatedCalibrationLevelSelected = false;
        simulatedCalibrationRoutine = null;
        simulatedCalibrationExpectedPoints = simulatedCalibrationAcceptedPoints = 0;
    }
}
