# Implementation roadmap

Implementation status (November 3, 2026): checked tasks below have simulator/software evidence. **No milestone with an outstanding hardware, clean-PC, soak, or accessibility gate is declared complete.** See [implementation evidence](../docs/implementation-status.md) for tests and open gates.

Work in demonstrable milestones. Visual quality begins in M0 and is maintained throughout; it is not postponed until a functional prototype has hardened into the wrong layout. Completion gates distinguish simulator evidence from hardware evidence.

## M0: prove the technical and visual direction

Dependencies: none. Delivers: a small simulator-backed visual slice and a documented connection feasibility result.

- [x] M0.1 Create the .NET solution, pin a supported .NET 10 SDK and compatible package versions, and enable nullable analysis and build warnings.
- [x] M0.2 Create the six production project boundaries from plan 03 and the minimum test projects needed for the slice; add dev/test/publish scripts.
- [x] M0.3 Implement a themed shell, reusable readout, numeric editor, status badge, and a two-channel ScottPlot proof using fabricated simulator data.
- [ ] M0.4 Capture screenshots at the intended laptop/desktop sizes, including a stale/unknown state. Check typography, chart embedding, keyboard focus, and 150% DPI.
- [ ] M0.5 Publish a self-contained folder and prove startup in simulator mode on a clean user environment before adding substantial functionality.
- [ ] M0.6 Audit the exact command set, limit-query support, identification variants, and status-bit mapping against the manual. Record the supported adapter/cable/profile assumptions.
- [ ] M0.7 When the real supply is available, perform read-only identity/state probes and measure scalar-query latency. Verify the adapter's bounded-read and Break behavior under a controlled procedure before relying on it for cancellation.

Gate: the stack can render the intended composition, embed the plots, and publish a working package. Hardware feasibility has either measured evidence or a clearly recorded open gate. Do not silently swap frameworks after this point; update the decision record if evidence justifies a change.

## M1: reusable core, simulator, and protocol client

Dependencies: M0 architecture and command audit. Delivers: instrument operations testable without a GUI.

- [x] M1.1 Implement immutable domain records, capabilities, quality/freshness rules, verification results, and input validation.
- [x] M1.2 Implement the byte transport contract, bounded serial worker, incremental framing, typed SCPI parsing, and connection lifecycle.
- [x] M1.3 Implement the device profile and command mapping; add identity/readback/limits/status/error operations.
- [x] M1.4 Implement a scripted transport covering split, delayed, stale, invalid, and missing responses.
- [x] M1.5 Implement the deterministic instrument simulator and user-selectable fault scenarios.
- [x] M1.6 Implement the shutdown latch, operation generations, queue priorities, and bounded resynchronization state.

Gate: parser/transport tests pass, only one query can be outstanding, and stale completions cannot modify a newer session. The high-level simulator and real driver's scripted-transport tests both run without WPF.

## M2: verified manual control and lifecycle

Dependencies: M1; actual hardware tests require M0 hardware feasibility. Delivers: a usable monitored supply in simulator and, after acceptance, hardware mode.

- [x] M2.1 Implement session coordination, explicit connection, identity/state display, and asynchronous snapshot publication.
- [x] M2.2 Implement stage/apply/readback for setpoints, quantization handling, decrease-before-increase ordering, and partial-failure shutdown.
- [x] M2.3 Implement separate output actions and the off-before-protection-edit/clear workflows.
- [x] M2.4 Implement bounded shutdown, local-control restoration, deliberate disconnect/exit, and persistent unverified outcomes.
- [x] M2.5 Bind all normal/error states to the shell and Monitor workspace, including simulator identity and last-known values.
- [ ] M2.6 Execute controlled hardware checks for actual settings, faults/readback behavior, output-off, timeout recovery, and the chosen adapter. Record transcripts and measured shutdown latency.

Gate: no edit/preset/connect action can implicitly enable output; requests and acknowledgments are distinguishable; cancellation cannot be followed by a queued output-on. Hardware control remains unreleased until its gate is demonstrated.

## M3: monitoring, presets, and manual recording

Dependencies: M2. Delivers: a polished everyday monitoring workflow and reliable recording foundation.

- [x] M3.1 Add cadence control, timestamped voltage/current pairing, power-estimate validity, achieved rate, freshness, and setting-change detection.
- [x] M3.2 Implement bounded live buffers, chart ranges, gaps, shared cursor, Follow live, and channel/setpoint styling.
- [ ] M3.3 Add preference/preset repositories, atomic writes, staged preset loading, and protection-change routing.
- [x] M3.4 Implement recording directory/manifest/CSV/event schemas, writer health, preflight, periodic flush, and incomplete-file discovery.
- [x] M3.5 Add Start/Stop recording, recording status, recent recording list, and Open folder.
- [x] M3.6 Implement Diagnostics with bounded event/command history and useful error messages.

Gate: a one-hour simulator session maintains bounded memory and responsive input; slow or failed storage triggers the specified shutdown policy. Exported samples preserve timestamps, signed current, and unavailable fields correctly. Measured polling performance is shown honestly.

## M4: sequence editing and execution

Dependencies: M2 verified control and M3 recording foundation. Delivers: editable, reusable, automatically recorded tests.

- [x] M4.1 Implement the step table, add/duplicate/delete/move commands, document dirty state, JSON save/load, and per-cell validation.
- [x] M4.2 Add requested-level/time preview, finite repeats, nominal-duration calculation, and whole-document validation.
- [x] M4.3 Implement immutable run capture, recording preflight, output-off preparation, row transition rules, monotonic holds, and repeat boundaries.
- [x] M4.4 Bind active row, repeat, transition/hold state, elapsed time, remaining hold, and Stop. Lock conflicting edits and manual writes.
- [x] M4.5 Integrate cancellation, output-off, faults, disconnects, storage failure, and sleep/interruption handling through one terminal path.
- [x] M4.6 Add final outcome presentation with separate execution, shutdown, and recording results.

Gate: fake-clock and scripted-driver scenarios prove ordering and timing without real sleeps. A three-step simulator run and controlled unloaded hardware run both produce inspectable records; unsupported or invalid sequences cannot start.

## M5: visual, accessibility, and resilience review

Dependencies: M3 and M4. Delivers: the release candidate experience.

- [ ] M5.1 Review the full screenshot state set in plan 02. Resolve spacing, clipping, axis contrast, and inconsistent component styles.
- [ ] M5.2 Exercise keyboard-only navigation, focus restoration, numeric editing, nonmodal panels, and the global Output Off shortcut.
- [ ] M5.3 Verify supported DPI/work-area layouts, high contrast, reduced motion, and screen-reader labels/status behavior.
- [ ] M5.4 Add bounded recovery messages for corrupted local files, unavailable folders, busy ports, unplugged adapters, and unverified shutdown.
- [ ] M5.5 Measure startup, UI response, polling cadence, shutdown request/verification, and memory behavior on the designated acceptance PC.
- [ ] M5.6 Run the failure matrix and fix issues; repeat affected checks after changes rather than rerunning unrelated tests without reason.

Gate: all release-blocking acceptance checks pass, no mandatory action is inaccessible, and no fault state reports success based only on a requested command.

## M6: package and hand over

Dependencies: M5 plus completed hardware acceptance for a hardware-enabled release. Delivers: source and a portable release folder/ZIP.

- [x] M6.1 Publish `win-x64` self-contained, without trimming/AOT initially; include icons, fonts, licenses, version/build metadata, and dependency lock files.
- [x] M6.2 Provide concise setup/use instructions: cable/front-panel profile, port selection, simulator, first connection, setpoints, sequences, recordings, and shutdown outcomes.
- [ ] M6.3 Smoke-test from a path containing spaces and a non-admin account, with no preinstalled .NET runtime; verify all writable data uses user locations.
- [x] M6.4 Include example sequences that open in the editor only, plus a short troubleshooting reference and known limitations.
- [ ] M6.5 Record the release's tested Windows build, instrument identity/firmware information if reported, adapter, serial profile, test results, and artifact checksum.
- [ ] M6.6 Archive acceptance evidence and update the decision/risk record. Label any simulator-only preview explicitly.

Gate: someone using the release folder can start the simulator, connect with the documented configuration, run the accepted workflow, and locate recordings without a development environment.

## Dependency summary

```mermaid
flowchart LR
    M0[Direction and feasibility] --> M1[Core and protocol]
    M1 --> M2[Verified control]
    M2 --> M3[Monitoring and recording]
    M3 --> M4[Sequences]
    M4 --> M5[Experience and resilience]
    M5 --> M6[Portable release]
```

Small UI and persistence tasks can proceed independently once their contracts are stable, but milestone gates remain ordered. Avoid adding features merely to fill time while hardware evidence is unavailable; use that time for simulator behavior, accessibility, and protocol failure tests.

## Working discipline

Keep changes focused by responsibility. Add tests for ordering, parsing, failure, and timing behavior rather than tests that merely mirror getters or colors. Do not write all features into MainWindow, one view model, or one driver class. Keep the implementation and these plans aligned when real hardware evidence changes assumptions.

