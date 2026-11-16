using System.Globalization;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient
{
    private long calibrationOwner;
    private long nextCalibrationOwner;
    private bool calibrationModeMayBeActive;
    private bool CalibrationInProgress => Interlocked.Read(ref calibrationOwner) != 0;

    public async Task<CalibrationResult> RunCalibrationAsync(CalibrationProcedure procedure, decimal? passcode,
        Func<CalibrationPrompt, CancellationToken, Task<CalibrationAnswer>> prompt, CancellationToken token = default)
    {
        var recipe = CalibrationRecipe.For(procedure);
        string? password = FormatCalibrationPasscode(passcode);
        ArgumentNullException.ThrowIfNull(prompt);
        long owner = Interlocked.Increment(ref nextCalibrationOwner);
        try
        {
            await OwnCalibrationAsync(owner, token).ConfigureAwait(false);
            var fixture = await prompt(new(CalibrationPromptKind.Fixture, recipe.Fixture), token).ConfigureAwait(false);
            if (!fixture.Confirmed) throw new OperationCanceledException("Calibration cancelled before energizing the fixture.");
            await CalibrationCommandAsync(owner, t =>
            {
                if (procedure == CalibrationProcedure.AcCurrent && ReadOutputBandwidth(t) != OutputBandwidth.Normal)
                    throw new InvalidOperationException("Set the rear switch to Normal before calibrating AC current.");
                SendCheckedAdvanced("SYST:REM", t);
                SendCheckedAdvanced("*RST", t);
                SetOffVerified(t);
                EnableCalibration(password, t);
                SendCheckedAdvanced("OUTP 1", t);
                if (!ScpiParser.Boolean(channel.Query("OUTP?", t))) throw new IOException("Calibration output enable was not verified.");
                SendCalibrationCommand(recipe.Command, t);
                return true;
            }, token).ConfigureAwait(false);

            for (int point = 1; point <= recipe.ReferencePoints; point++)
            {
                string level = point == 1 ? "P1" : "P2";
                await CalibrationCommandAsync(owner, t =>
                {
                    SendCalibrationCommand("CAL:LEV " + level, t);
                    return true;
                }, token).ConfigureAwait(false);
                double reading = await ReferenceReadingAsync(prompt, recipe, level, token).ConfigureAwait(false);
                await CalibrationCommandAsync(owner, t =>
                {
                    SendCalibrationCommand("CAL:DATA " + ScpiParser.Format(reading), t);
                    return true;
                }, token).ConfigureAwait(false);
            }

            await CalibrationCommandAsync(owner, t => { SetOffVerified(t); return true; }, token).ConfigureAwait(false);
            if (procedure == CalibrationProcedure.NegativeCurrent)
            {
                var disconnected = await prompt(new(CalibrationPromptKind.Fixture,
                    "The 66332A output is off. Switch off and disconnect the external supply before continuing."), token).ConfigureAwait(false);
                if (!disconnected.Confirmed) throw new OperationCanceledException("Calibration cancelled before external supply removal was confirmed.");
            }
            var save = await prompt(new(CalibrationPromptKind.Save,
                "The calibration procedure completed and the output is off. Save overwrites the stored constants. Discard exits calibration and keeps the stored constants."), token).ConfigureAwait(false);
            return await CalibrationCommandAsync(owner, t =>
            {
                if (save.Confirmed) SendCalibrationCommand("CAL:SAVE", t);
                EndCalibrationForShutdown(t);
                if (IsQualified(QualifiedFeature.Acquisition)) RefreshMeasurementDuration(t);
                return new CalibrationResult(procedure, save.Confirmed, clock.UtcNow);
            }, token).ConfigureAwait(false);
        }
        finally
        {
            await EndFailedCalibrationAsync(owner).ConfigureAwait(false);
        }
    }

    public async Task ChangeCalibrationPasscodeAsync(decimal? currentPasscode, decimal newPasscode, CancellationToken token = default)
    {
        string? current = FormatCalibrationPasscode(currentPasscode);
        string updated = FormatCalibrationPasscode(newPasscode)!;
        long owner = Interlocked.Increment(ref nextCalibrationOwner);
        try
        {
            await OwnCalibrationAsync(owner, token).ConfigureAwait(false);
            await CalibrationCommandAsync(owner, t =>
            {
                SendCheckedAdvanced("SYST:REM", t);
                EnableCalibration(current, t);
                channel.Write("CAL:PASS " + updated, t, sensitive: true);
                CheckErrors(t); // A new password is stored immediately. There is no password readback.
                EndCalibrationForShutdown(t);
                return true;
            }, token).ConfigureAwait(false);
        }
        finally
        {
            await EndFailedCalibrationAsync(owner).ConfigureAwait(false);
        }
    }

    private Task OwnCalibrationAsync(long owner, CancellationToken token) => Queue(t =>
    {
        EnsureAdvancedScpi(QualifiedFeature.Calibration);
        if (Guard(() => ScpiParser.Boolean(channel.Query("CAL:STAT?", t))))
            throw new InvalidOperationException("The instrument is already in calibration mode. Exit calibration at the front panel before starting.");
        Interlocked.Exchange(ref calibrationOwner, owner);
        return Guard(() => { SetOffVerified(t); return true; });
    }, token, configuration: true);

    private Task<T> CalibrationCommandAsync<T>(long owner, Func<CancellationToken, T> command, CancellationToken token) => Queue(t =>
    {
        EnsureReady();
        if (Interlocked.Read(ref calibrationOwner) != owner) throw new OperationCanceledException("Calibration no longer owns the session.");
        return Guard(() => command(t));
    }, token, configuration: true);

    private async Task EndFailedCalibrationAsync(long owner)
    {
        if (Interlocked.Read(ref calibrationOwner) == owner && !shutdownPending)
            await RequestOutputOffAsync("Incomplete calibration").ConfigureAwait(false);
    }

    private void EnableCalibration(string? password, CancellationToken token)
    {
        calibrationModeMayBeActive = true;
        channel.Write(password is null ? "CAL:STAT 1" : "CAL:STAT 1," + password, token, sensitive: password is not null);
        CheckErrors(token);
        if (!ScpiParser.Boolean(channel.Query("CAL:STAT?", token))) throw new IOException("Calibration mode was not enabled.");
    }

    // Called after verified output off, including the high-priority shutdown while a prompt is open.
    private void EndCalibrationForShutdown(CancellationToken token)
    {
        try
        {
            if (!calibrationModeMayBeActive) return;
            channel.Write("CAL:STAT 0", token);
            if (ScpiParser.Boolean(channel.Query("CAL:STAT?", token))) throw new IOException("Calibration mode remains active.");
            calibrationModeMayBeActive = false;
        }
        finally { Interlocked.Exchange(ref calibrationOwner, 0); }
    }

    private void SendCalibrationCommand(string command, CancellationToken token)
    {
        channel.Write(command, token);
        if (channel.Query("*OPC?", token, 30).Trim() != "1") throw new IOException("Calibration command completion could not be verified.");
        CheckErrors(token);
    }

    private static async Task<double> ReferenceReadingAsync(Func<CalibrationPrompt, CancellationToken, Task<CalibrationAnswer>> prompt,
        CalibrationRecipe recipe, string level, CancellationToken token)
    {
        var answer = await prompt(new(CalibrationPromptKind.ReferenceReading, $"{level}: {recipe.ReferenceInstruction}", recipe.Unit), token).ConfigureAwait(false);
        if (!answer.Confirmed) throw new OperationCanceledException("Calibration cancelled before submitting a reference reading.");
        if (answer.Reading is not { } reading || !double.IsFinite(reading) || Math.Abs(reading) > recipe.MaximumReference)
            throw new ArgumentException($"Enter a finite reference reading within ±{recipe.MaximumReference:G} {recipe.Unit}.");
        return reading;
    }

    private static string? FormatCalibrationPasscode(decimal? passcode)
    {
        if (passcode is null) return null;
        string formatted = passcode.Value.ToString("0.######", CultureInfo.InvariantCulture);
        if (decimal.Parse(formatted, CultureInfo.InvariantCulture) != passcode ||
            formatted.TrimStart('-').Replace(".", "").TrimStart('0').Length > 6)
            throw new ArgumentException("Use no more than six digits and an optional decimal point for the calibration passcode.");
        return formatted;
    }

    private sealed record CalibrationRecipe(string Command, string Fixture, int ReferencePoints = 0,
        string? ReferenceInstruction = null, string? Unit = null, double MaximumReference = 0)
    {
        private const string Intro = "This procedure resets the instrument and energizes the calibration fixture. Remove the device under test. Follow the Keysight User’s Guide Appendix B and verify fixture wiring before continuing. ";

        public static CalibrationRecipe For(CalibrationProcedure procedure) => procedure switch
        {
            CalibrationProcedure.Voltage => new("CAL:VOLT", Intro + "Connect the reference DMM in DC volts directly to the output with the load and shunt removed.",
                2, "Wait for the reference DMM to settle, then enter its DC voltage in volts.", "V", 30),
            CalibrationProcedure.Overvoltage => new("CAL:VOLT:PROT", Intro + "Leave the output unloaded. The instrument computes the OVP constant internally."),
            CalibrationProcedure.PositiveCurrent => new("CAL:CURR", Intro + "Connect the reference current monitor and DMM as shown in Figure B-1A.",
                2, "Divide the settled reference DMM voltage by the calibrated shunt resistance and enter the result in amperes.", "A", 7),
            CalibrationProcedure.NegativeCurrent => new("CAL:CURR:NEG", Intro + "Connect only the external supply as specified under Negative Current Programming Calibration. Confirm the external supply is limited, isolated from the usual load, and correctly wired."),
            CalibrationProcedure.LowRangeCurrent => new("CAL:CURR:MEAS:LOWR", Intro + "Use the low-range load and reference DMM in current mode as shown in Figure B-1B.",
                1, "Wait for the DMM to settle, then enter the reference current in amperes.", "A", .03),
            CalibrationProcedure.AcCurrent => new("CAL:CURR:MEAS:AC", Intro + "Disconnect all loads and set the rear output mode switch to Normal."),
            _ => throw new ArgumentOutOfRangeException(nameof(procedure))
        };
    }
}
