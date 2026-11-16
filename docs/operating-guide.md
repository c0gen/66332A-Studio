# 66332A Studio operating guide

Start with the [quick start](../README.md) to try the simulator. Before connecting a supply, review the [RS-232 coverage and qualification guide](command-audit.md).

## Manual control and sequences

1. Isolate the load and turn output off at the supply. In **Connection**, choose the adapter, match the front-panel serial settings, and click **Connect**. Proceed with basic controls once **Hardware control ready** appears. If output was on when you connected, turn it off at the supply and reconnect to run the live check.
2. Read the measured voltage and current separately from the acknowledged settings. With output off, **Apply & turn on** programs and verifies the voltage and current limit before enabling output. The header's **Output On** uses the acknowledged settings when there are no pending edits. With output on, **Apply changes** updates the setpoints. **Escape** discards the draft.
3. The app verifies output off before changing protections or clearing a fault. A preset stages values for review. Apply any protection changes separately, before enabling output.
4. In **Monitor**, choose **Start recording** for a manual session. To run a sequence, finish manual recording first. Use the path field in **Sequences** to open or save a JSON document. Check the requested levels in the preview, then choose **Run sequence**. Holds are at least one second and repeats are finite. Each run records automatically, highlights its active step, and shows the current hold's progress.
5. Use **Output Off**, **Stop run**, or **Ctrl+Shift+O** to cancel a run and request shutdown. The app locks manual writes and row editing during a run. Communication or recording failures also trigger an off request. The app never reconnects, runs an opened sequence, or enables output on its own.
6. In **Recordings**, select a session and choose **Open folder**. Use **Diagnostics** to inspect flags and register masks, acknowledge latched events, or drain the error queue. Acknowledging events can deassert a wired fault output.

Cable loss, a terminated process, or PC failure can prevent shutdown. If the app reports an unverified or unknown output state, verify the supply directly. An orderly close attempts shutdown and requires an acknowledgment if verification fails. Verifying the output-enable state does not establish that the load or a capacitor has discharged.

## Capture, setup, and service

**Capture** shows voltage or current samples and the instrument's DC, RMS, min/max, and pulse readings. Read the current digitizer settings or enter a sweep and start a measurement. An armed software trigger waits for **Fire trigger**. Use the **Output trigger** tab to stage voltage and current limits for a trigger. The device has one acquisition buffer, so the app pauses live DC polling during acquisition operations. A full waveform can take about a minute at 9600 baud. **Export capture** saves a CSV and settings under `Captures` in the recording location.

**Setup** reads the rear Normal/Fast switch, option codes, digital port, inhibit and fault settings, display, and optional relay. **Service** covers memory slots 0–3, startup policy, self-test, reset, programming language, and calibration. Isolate the load before recalling a memory slot; the workflow finishes by verifying output off. To configure startup recall, review and save a fresh slot 0 with output off. For calibration, use the manufacturer fixtures and reference readings described in the [qualification guide](command-audit.md#bench-record).

## Local files and recordings

Preferences and named presets live in `%LOCALAPPDATA%\66332A Studio`. Recordings default to `66332A Studio\Recordings` in the Windows Documents folder. You can choose a different location in **Recordings**.

Each session contains `run.json`, `measurements.csv`, and `events.jsonl`. Sequence runs also save a fixed copy of `sequence.json`. Sequence documents use schema version 1, explicit units, and unique step IDs. See the [example sequence](../examples/unloaded-check.json).

The CSV includes separate voltage and current timestamps, signed readings, estimated power, requested and acknowledged run state, step and repeat positions, and raw condition registers. Unavailable values are blank. Step and repeat indexes start at 1. UTC timestamps support correlation; durations use monotonic elapsed time.

Active files use a `.partial` suffix until finalized. Incomplete sessions remain discoverable, but abrupt power loss can lose buffered samples. Live plot history covers up to one hour or 3,602 samples. See the [implementation notes](implementation-status.md) for details about storage and failure handling.
