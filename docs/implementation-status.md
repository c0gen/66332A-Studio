# Implementation evidence

## RS-232 coverage preview · November 15, 2026

The [command coverage matrix](command-audit.md) records the new serial settings, qualification checks, capture and output triggers, I/O, diagnostics, and service workflows. The default physical session supports monitoring until its bench evidence matches. Compatibility mode has its own documented limits. [Compatibility command map](compatibility.md)

The solution builds without warnings. All 75 compiled xUnit cases passed through direct invocation after the standard VSTest runner timed out on its local test host in this environment. The WPF simulator checks covered waveform capture and export, software trigger fire, Output Off during capture, setup readback, self-test, calibration discard, and the original sequence and recording workflows. The harness reported zero binding errors and kept Output Off visible in the new workspaces at the compact window size.

### Unloaded 66332A bench test

November 15, 2026. The HP 66332A reported firmware A.01.03 and serial `0` over the Prolific COM12 link at 9600 baud, 8N1, no flow control. The packaged driver connected and read the meter, setup, status registers, acquisition, triggers, and service. The initial Break logic timed out after an interrupted waveform. It now waits for a complete SCPI language reply before verifying identity. Three repeated waveform interruptions confirmed output off in 2.47–2.55 s with the revised driver.

An unloaded test at a 0.5 V setpoint and 20 mA current limit measured 0.501415 V in CV. The driver verified shutdown in 25 ms on the synchronized link. It retrieved a 64-sample waveform and verified immediate and serial `*TRG` behavior for both acquisition and pending output levels with output disabled. Setpoints, protection settings, and acquisition settings were restored and output off was verified. Raw transcripts are under `artifacts/bench`.

All 76 automated tests passed using VSTest with its local test host available, including the regression for waveform data prefixed to the first post-Break reply.

The bench session used a temporary qualification. A permanent profile needs the actual chassis and cable labels. Loaded current regulation, protection trip behavior, wired I/O, calibration, additional serial profiles, and other firmware remain open. This unit reported no Option 760 relay.

## Original simulator preview 0.1

Implementation date: November 3, 2026 (America/New_York). This repository began with planning files only. The root README now describes the implemented preview, development commands, workflows, and limits.

## Delivered software

| Milestone | Delivered | Gate status |
| --- | --- | --- |
| M0 | Pinned .NET 10.0.403 solution, six modular projects (five product libraries/UI plus a development CLI), IBM Plex theme, reusable WPF controls, ScottPlot integration, scripts and self-contained publishing. | Real WPF renders and packaging exercised; clean-user and hardware feasibility checks remain open. |
| M1 | Immutable domain models, validation, exclusive bounded priority worker, byte transport, incremental 4 KiB framing, typed parser/profile, simulator scenarios, cancellation generations, conservative Break recovery gate. | Headless protocol/worker tests pass. Adapter-specific recovery has no physical evidence. |
| M2 | Explicit identification/snapshot, staged apply/readback, quantization policy, decreasing-before-increasing writes, protections/clear with verified off, persistent output controls and orderly shutdown. | Simulator paths verified. Physical configuration remains gated. |
| M3 | Monotonic freshness/cadence, bounded history, aligned plots, optional power, channel visibility, shared/frozen cursor, Follow live/ranges, event markers, presets, manual recording, catalog and diagnostics. | Integration and failure checks pass. A real-time long-session soak remains open. |
| M4 | Editable rows, duplication/reorder, strict versioned JSON, finite repeats, requested-level preview, immutable run capture, automatic recording, monotonic confirmed holds, Stop and independent terminal outcomes. | Fake-clock and real simulator workflows pass. Hardware unloaded run remains open. |
| M5 | Nonmodal file workflows, visible focus, keyboard Output Off, compact layout, partial high-contrast handling, corruption/storage/communication feedback, suspend/resume invalidation. | Rendered states inspected. Full keyboard, assistive-technology, high-contrast and mixed-monitor DPI review remains open. |
| M6 | Self-contained x64 publish script, example sequence, setup/use instructions, dependency locks, license collection, checksums and isolated smoke/UI harnesses. | Development-PC package checks only; clean-PC release acceptance remains open. |

The project count in M0.2 is resolved as five product projects from the architecture diagram plus a developer-only CLI. The simulator uses the production typed client and worker over a deterministic simulated byte transport. Separate scripted/intercepted transports test malformed/late responses and worker races. This intentionally gives normal simulator use the same framing/ordering behavior without duplicating a second driver implementation.

## Automated evidence

`tools/test.ps1` runs xUnit projects for Core, Instruments, Application and Storage. Evidence is written to `artifacts/test-results/*.trx`.

- Bounds/nonfinite input, per-field quantization, conservative setpoint order, OVP relationship, signed power and invalid pairs.
- Identity variants and unsupported models; signed/scientific replies; quoted errors; bounded fragmented framing; unsolicited bytes; synchronization loss and unavailable Break recovery.
- An actual worker race: an in-flight blocked voltage read plus queued output-on, interrupted by Output Off. The stale read and queued enable are cancelled; off is verified; no enable is dispatched afterward.
- Partial-apply readback mismatch, protection off-before-write/clear, unknown state after cable loss, and the physical-profile configuration gate.
- Fake-clock hold boundaries, repeat transitions, preflight failure, cancellation before enable, unexpected external settings, scheduling interruption, storage failure and independent outcomes.
- Independently parsed CSV and manifest, escaping, signed/unavailable fields, strict sequence imports and limits, atomic backups, corrupt preferences, abandoned recording discovery.
- Injected real recording-stream write failure, blocked writer/stall detection, and full queue. Recoverable partial evidence is retained rather than silently dropping records.

The release suite passed 54 tests (14 Core, 20 Instruments, 9 Application, 11 Storage), including unique session generations across replacement clients and pausing monitoring without aborting an in-flight wire transaction. Current TRX files are authoritative as checks are extended. Warnings are treated as errors. Test host interprocess communication requires running outside this agent's restricted execution sandbox; this is an execution-environment restriction, not an application dependency.

`tools/verify-ui.ps1` launches real WPF with isolated data under `artifacts/ui-data`, renders state screenshots to `artifacts/ui-evidence`, and checks simulator identification, dirty-draft enable guards, active-run edit lock, Stop, recording discovery, and cable-loss unknown state. It captures disconnected, simulator-off, output-on, pending-draft, active-sequence, recordings, diagnostics, and lost/unknown states. The initial check reported zero WPF binding errors and approximately 3–6 ms simulator shutdown confirmation on the development PC. This is not a hardware timing claim.

The 1120×680 and 1366×768 layouts were rendered, and a 1280×720 layout was rendered at 150% bitmap scale. Render-scale evidence does not establish actual mixed-monitor DPI or screen-reader acceptance. The plots' dark-axis contrast and dropdown styling were corrected after visual inspection.

The developer CLI also completed the example three-row simulator sequence and produced a finalized CSV/event/manifest directory with Completed / VerifiedOff / Complete. Recorded files are under `artifacts/smoke-recordings` on the development machine.

The self-contained folder passed a direct packaged startup/apply/off smoke test with global runtime lookup disabled and `DOTNET_ROOT` pointing to an unavailable location. The app reported its runtime directory inside `artifacts/66332A Studio Preview` and exited successfully after verified simulator shutdown. The complete solution also passed `restore --locked-mode` after publishing. Package checksums cover every distributed file in `SHA256SUMS.txt`. This establishes local self-contained loading, while clean-PC/non-admin acceptance remains open. The physical device was disconnected during final package verification; all execution used the simulator.

## Open release gates

- **No physical hardware acceptance:** actual identity variants/firmware, programming ranges, per-parameter resolution, status behavior, latency at 9600/4800, cable pinout, adapter/driver and Break barrier have not been measured. The UI cannot enable physical output or change physical setpoints/protections. See [command audit](command-audit.md).
- Two-hour real-time soak, memory stabilization, storage latency and actual UI response measurement under sustained load.
- Full keyboard-only and screen-reader inspection, high-contrast plot treatment, focus restoration, 100/125/150/200% native DPI and mixed monitors.
- Native startup and read/write checks in a clean non-admin Windows environment without installed .NET, with a read-only installation folder.
- Complete manufacturer-guided unloaded hardware workflow and restoration with output off, archived transcripts, and measured shutdown/query latency.
- Recording metadata does not yet persist device-error queues as a separate structured field. Errors are available in bounded protocol diagnostics; arbitrary SCPI entry remains excluded.
- Window placement persistence and restored-bound clamping are deferred; startup instead fits and centers the window in the current work area.

These gates remain unchecked in the owning plans. Simulator success does not authorize declaring the hardware product accepted.
