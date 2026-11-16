# Agilent 66332A Desktop Control App

## Summary

Build a Windows desktop app for live monitoring, manual control, CSV logging, and automated tests defined in an editable step table.

Use **Python 3.11, PySide6, pyqtgraph, and pySerial**, with a portable Windows executable produced by PyInstaller. Keep the instrument driver, sequence runner, storage, and UI modular so the driver can later support standalone scripts.

The default connection will be **COM7, 9600 baud, 8N1, no flow control, SCPI, CRLF**, with DTR and RTS asserted. Keep 4800 selectable. The supply’s front-panel baud rate must match. [Keysight programming guide](https://www.keysight.com/mt/en/assets/9018-01225/programming-guides/9018-01225.pdf)

## Display and Controls

- Use a resizable window with a connection bar, large voltage/current/calculated-power readings, trend plots, and tabs for manual controls, sequences, and diagnostics. Use a dark instrument-style theme with text labels alongside status colors.
- Show measured values separately from voltage and current-limit setpoints. Display output state, CV/CC mode, protection faults, connection health, and reading age.
- Provide explicit **Apply**, **Output On**, and prominent **Output Off** controls. Editing a field stages a change until Apply is pressed.
- Include overvoltage threshold, overcurrent protection, protection delay, and fault clearing. Clear faults with output explicitly off first.
- Support named local presets for setpoints and protections. Loading a preset populates the controls for review and Apply.
- Target one complete measurement update per second, with selectable slower intervals. Show achieved update rate, timestamp sequential voltage/current readings, and label their product as a power estimate.
- On connection, verify the model and display existing settings and output state. Mark stale readings visibly and introduce gaps in plots when measurements are unavailable.

## Instrument and Application Architecture

- Implement a reusable instrument client with typed connection settings, device snapshots, requested settings, measurements, and sequence steps. Expose connect/disconnect, read snapshot, apply settings, set output, and clear protection operations.
- Give one background worker exclusive ownership of the serial connection. Serialize commands and replies, prioritize Output Off and cancellation, and keep serial waits outside the GUI thread.
- Use bounded transactions, incremental line parsing, scientific-notation support, and explicit handling of partial replies. After a timeout, resynchronize before accepting further replies. Use the documented serial Break behavior when an outstanding operation must be aborted. [Keysight programming guide](https://www.keysight.com/mt/en/assets/9018-01225/programming-guides/9018-01225.pdf)
- Read back applied values and display the acknowledged settings, accounting for instrument quantization. Apply decreases before increases when changing an active output. A partial or rejected update stops the operation and requests output off.
- Query the device’s programming limits and validate finite numeric inputs. Preserve signed current readings. Poll condition registers for status and capture device errors in the diagnostics history.
- Abort an active run on communication failure and mark output state unknown until verified. Reconnection requires an explicit action, re-identification, and fresh state readback.
- Include a clearly labeled simulator using the same driver interface, supporting delayed replies, faults, and disconnects for development and testing.

## Automation and Recording

- Provide a table with **voltage, current limit, output enabled, and hold time** per row. Support add, duplicate, delete, reorder, finite repeat count, and JSON save/load.
- Require holds of at least one second. Validate the whole sequence against device limits and active protection settings before starting.
- Start with output off, apply and verify the first row, then enable output if requested. Between rows, honor the requested output state and verify changes.
- Start each hold after the row’s state is confirmed. Use a monotonic clock and record actual transition times. Schedule polling around transitions without accumulating overdue work.
- Show active row, repeat number, remaining hold time, and run status. Lock manual setting changes during a run. Stop and Output Off cancel pending steps.
- **Request and verify output off on completion, cancellation, failure, intentional disconnect, and orderly exit.** Report an unverified shutdown if communication prevents confirmation. After verified shutdown, return the front panel to local control.
- Automatically record every sequence, and offer manual Start/Stop Recording. CSV includes timestamps, elapsed time, row/repeat, requested and acknowledged settings, measured voltage/current, power estimate, output state, and status.
- Check log-file availability before starting a run. A recording failure aborts the run and requests output off. Stream records to disk and bound plot history to one hour.
- Store preferences and presets under the user’s local application-data directory. Save sequences as versioned JSON and default recordings to a dedicated Documents subfolder.

## Delivery and Acceptance

Implement in order: instrument client and simulator, live dashboard and controls, sequence execution and recording, then packaging.

Deliver source, isolated pinned dependencies, a development launcher, and a portable executable folder with brief connection instructions.

Verify with focused automated tests covering parsing, limits, quantized readbacks, command ordering, partial updates, sequence timing/repetition, cancellation, stale responses, disconnects, and recording failures. Test GUI responsiveness and packaged startup in simulator mode.

For hardware acceptance:

1. Match the selected baud rate and verify identity, readings, protections, and setpoint readbacks.
2. With output leads disconnected from any load, free ends separated, and normal local sensing configured, run a brief **1 V, 10 mA-limit** test and verify output off afterward. Unloaded voltage testing is supported by the manufacturer’s procedure. [Keysight user’s guide](https://www.keysight.com/ph/en/assets/9018-01126/user-manuals/9018-01126.pdf)
3. Run a short unloaded sequence and verify timestamps, CSV contents, completion, Stop, disconnect, and exit behavior.
4. Restore the initial setpoints and protections with output off.

Defaults assume one 66332A on this Windows PC, 9600 baud for normal use, one-second dashboard polling, and seconds-scale automation. A lost cable or terminated process can prevent an output-off command from reaching the supply, so the app must distinguish requested shutdown from verified shutdown.
