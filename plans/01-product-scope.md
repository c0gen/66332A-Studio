# Product scope

## Product intent

Make the 66332A feel like a current laboratory instrument without obscuring what the physical supply is doing. An operator should be able to connect, understand the present state, make a deliberate change, and preserve a useful record of a test from one well-organized desktop window.

The primary user is someone working at a Windows bench with one supply. The application is local and works offline. A professional appearance means clear hierarchy, excellent typography, cohesive charts, responsive controls, and well-designed failure states. A little visual flair belongs in the measurement display and transitions that explain changes.

## Everyday workflows

1. **Inspect and control.** Select a serial port, connect, inspect the existing output and protection settings, stage an edit, apply it, and enable or disable the output explicitly.
2. **Observe and record.** Watch voltage/current trends, inspect the age of readings, start a recording, and open the resulting files.
3. **Build and run a test.** Edit a step table, inspect its time preview, validate it, and run it with automatic recording and visible progress.
4. **Understand an interruption.** See what failed, what the app attempted, whether output-off was verified, and what action is available next.
5. **Try the app without hardware.** Enter an unmistakably labeled simulator that exercises the same application workflows.

## Release requirements

The IDs below connect scope to [verification](07-verification-and-release.md) and [implementation milestones](06-implementation-roadmap.md).

| ID | Requirement | Observable result |
| --- | --- | --- |
| R01 | Identify and connect to one 66332A. | Explicit connection, selected port/settings, model verification, and a fresh state snapshot before writes are enabled. |
| R02 | Display trustworthy telemetry. | Measured voltage, signed current, estimated power, timestamps, reading age, connection health, and achieved update rate are distinguishable. |
| R03 | Stage and verify manual changes. | Editing does not write to hardware; Apply validates, submits, and shows acknowledged values, including quantization. |
| R04 | Make output control persistent. | Separate Output On and Output Off actions; requested, verified, and unknown states have distinct presentations. |
| R05 | Manage protections and presets. | OVP, OCP, OCP-related delay, fault clearing, and named local presets follow the instrument rules in plan 04. |
| R06 | Show useful trends. | Aligned voltage/current plots, optional estimated-power plot, time-range selection, gaps for missing data, and at most one hour of live history. |
| R07 | Edit portable sequences. | Add, duplicate, delete, reorder, save, and load steps with voltage, current limit, output state, hold duration, and a finite repeat count. |
| R08 | Execute sequences predictably. | Whole-run validation, confirmed transitions, monotonic hold timing, active-row progress, cancellation, and locked manual writes. |
| R09 | Record data reliably. | Automatic sequence recording and optional manual recording; complete metadata; bounded streaming; recording failure invokes shutdown. |
| R10 | Handle communication and shutdown failures. | A run cannot resume automatically after a loss; shutdown result reports whether output-off was verified. |
| R11 | Support diagnosis without arbitrary writes. | Timestamped events, device errors, bounded command history, connection details, and copy/export actions. |
| R12 | Support development without hardware. | A deterministic simulator and transport fault fixtures cover normal and adverse conditions. |
| R13 | Deliver a polished Windows experience. | Resizable layout, keyboard access, high-DPI support, reduced motion, readable contrast, and consistent empty/error states. |
| R14 | Deliver a portable release. | Self-contained Windows x64 folder, launcher instructions, versioned dependencies, licenses, and simulator smoke test on a clean PC. |
| R15 | Keep instrument and application logic reusable. | Domain, serial client, runner, and persistence can be tested and used without loading WPF. |

## Initial defaults

| Setting | Proposed default |
| --- | --- |
| Connection | COM7 as a suggested initial value; show detected ports and allow manual entry. Never silently select a different port. |
| Serial profile | 9600 baud, 8N1, no flow control, CRLF commands, DTR and RTS asserted; validate against the actual front-panel configuration and cable. |
| Alternate baud | 4800 selectable. |
| Connection behavior | Explicit Connect and Reconnect; no automatic reconnection or output enable. |
| Monitoring | One complete measurement cycle per second when achievable; slower options of 2, 5, and 10 seconds. |
| Sequence holds | At least one second per row; finite repeats only. |
| Theme | Dark instrument workspace, with an accessible high-contrast treatment. A separately designed light theme is deferred. |
| Startup | Disconnected workspace with a clear Connect action and a separate Try simulator action. |
| Preferences/presets | Per-user local application data. |
| Recordings | A dedicated folder beneath the Windows Documents known folder; user-selectable destination. |

The connection values come from the original brief and are application defaults, not assumptions about the instrument's current configuration. [Plan 04](04-instrument-control.md) owns the connection checks.

## Intentional refinements

- Use **Monitor, Sequences, Recordings, and Diagnostics** as workspaces. Connection state and Output Off remain in the shared shell.
- Put manual setpoints beside the live plots, so applying a change does not require switching away from its effect.
- Offer a compact sequence preview showing requested levels and hold durations. It is a planning aid; actual transition times appear in run records.
- Add a lightweight recording browser: recent runs, outcome, duration, shutdown result, location, and open-folder action. Full historical replay is deferred.
- Treat protection edits more conservatively than normal setpoint changes: require verified output-off before changing protection configuration.
- Make simulator behavior a first-class part of the experience, with fault scenarios available without pretending simulated data are real measurements.
- Use concise event messages instead of modal dialogs for routine progress. Decisions that require operator attention remain persistent.

## Outside the first release

- Multiple instruments, GPIB/VISA, network control, or a plug-in marketplace.
- Cloud storage, accounts, collaboration, remote operation, or telemetry uploads.
- Sub-second sequence timing, triggered waveform capture, pulse analysis, or oscilloscope claims.
- Arbitrary SCPI consoles, calibration commands, instrument firmware updates, and saved-state recall commands.
- Scripting editors, branching/conditional sequences, infinite loops, pause/resume, and automatic recovery of interrupted runs.
- Installers, auto-update services, tray/background operation, and always-on services.
- Arbitrary skinning, decorative gauges, and multiple chart-library choices exposed to operators.

These boundaries keep a strong first release achievable. Extension points should follow actual domain boundaries; do not build unused framework machinery for every possible future feature.

## Product success criteria

- The primary readouts, output state, and last-update age are understandable at a glance.
- An operator can connect, apply a setpoint, run a three-step test, and find its recording without reading implementation details.
- There is no ambiguous action that might unexpectedly enable output.
- A lost connection cannot leave a green, apparently current dashboard behind.
- The application remains responsive while the supply or storage is slow.
- Screenshots of normal operation and failure states both look deliberately designed.
