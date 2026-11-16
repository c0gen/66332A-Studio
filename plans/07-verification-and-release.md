# Verification and release

## Evidence, not assumptions

Automated tests establish software behavior. Simulator tests establish application integration. Actual instrument/adapter tests establish the hardware behavior the release claims to support. Keep those results separate. A successful mock response is not evidence that a particular USB adapter delivers Break correctly.

Run the tests appropriate to each change. Prefer deterministic fixtures over timing-sensitive sleeps and assertions that merely repeat the implementation. Store test reports and measured acceptance notes with the release evidence, without collecting unrelated machine data.

## Focused automated tests

| ID | Area | Scenarios and required result |
| --- | --- | --- |
| T01 | Identity and parsing | Supported identity variants; unsupported model; fragmented CRLF/LF; signed/scientific values; quoted error text; malformed/oversized/nonfinite replies. Unsupported identity permits no configuration writes. |
| T02 | Transport serialization | Concurrent poll/apply requests; partial replies; multiple received chunks. Exactly one outstanding query, with bounded buffers and deadlines. |
| T03 | Late replies and recovery | Timed-out numeric reply arriving during a later query; canceled query; Break unsupported. No stale value is accepted as a new readback; failed recovery cannot resume ordinary work. |
| T04 | Bounds and quantization | Device-dependent limits, locale-aware UI input, invariant protocol formatting, rounding at boundaries, accepted/rejected readbacks. No silent clamping or arbitrary tolerance. |
| T05 | Setting order | Increases, decreases, mixed voltage/current changes, rejection halfway through. Required order is preserved; partial failure requests output-off and never restores output automatically. |
| T06 | Protection lifecycle | Dirty protection edits while on; clear with output previously on; persistent fault; unavailable state. Off verification precedes protection changes/clear and output stays off afterward. |
| T07 | Cancellation races | Off during a read, after a write, between row verification and enable, at a repeat boundary, and during shutdown. No subsequent queued command enables output. |
| T08 | Session generations | Reconnect while old completion is pending; stale snapshot; external settings change. Old work cannot update the new session; a running test aborts on unexpected settings. |
| T09 | Telemetry quality | Signed current, mismatched pair, transition between V/I, stale cycle, missing sample, wall-clock jump. Correct power validity, timestamps, gaps, and monotonic ages. |
| T10 | Sequence validation | Invalid JSON/schema, duplicate IDs, zero/fractional repeats, hold below one second, oversized files, overflow, incompatible protections. Run cannot start. |
| T11 | Sequence timing | Fake-clock hold boundaries, slow transitions, repeated rows, skipped polls, scheduling interruption. Each hold starts after confirmation and lasts at least the requested interval; no catch-up burst. |
| T12 | Run preparation/result | Preflight failure, existing output-on, manual recording already active, completion with unverified shutdown or failed finalization. No enable before preparation; separate outcome fields remain accurate. |
| T13 | Recording integrity | CSV escaping, scientific/signed numbers, optional blanks, UTC/elapsed timestamps, row/repeat indexes, metadata matching immutable sequence. Files can be independently parsed. |
| T14 | Storage failure | Full/slow writer queue, disk-full, permission loss, flush/finalization failure. Recording fails visibly; shutdown starts without waiting for a blocked disk. |
| T15 | Persistence recovery | Interrupted atomic write, corrupted preferences, unsupported schema, partial run directory, moved Documents folder, off-screen saved window. Recoverable data remain visible; startup never enables output. |
| T16 | Lifecycle | Stop, complete, disconnect, close, suspend/resume, reconnect. Terminal paths use the same shutdown rules and never automatically resume a run. |
| T17 | Presentation integration | View-model state transitions, numeric editor focus, global Output Off, disabled reasons, simulator badge, cross-thread updates. UI remains consistent and responsive. |

Include a targeted integration test at the actual serial-worker level for the shutdown latch; a mocked application service alone will not expose write-order races. Include both high-level simulator and byte-level fault fixtures, since they prove different properties.

## Visual and usability checks

Capture and inspect real WPF screenshots for disconnected, connecting, simulator, output off/on, pending edits, quantized readback, active run, stale telemetry, protection trip, lost connection, and unverified shutdown.

Verify:

- Measurement hierarchy, units, and acknowledged values remain readable without opening another view.
- Plot labels, axes, channel colors, gaps, cursor values, and setpoint overlays match the design plan.
- Window sizes and 100%, 125%, 150%, and 200% DPI do not clip required actions. Mixed-monitor DPI changes and reduced work areas remain usable.
- Output Off is available through navigation, in-window panels, editors, active runs, and error states whenever the identified session can accept a request.
- Keyboard-only use can connect, edit, apply, record, and operate the sequence table. Plain Enter cannot accidentally enable output.
- Focus and error announcements make sense with UI Automation/screen-reader inspection. Routine polling does not continually announce every value.
- High contrast, reduced motion, color-independent status, and text/control contrast meet plan 02.
- Simulator mode remains unmistakable in every workspace and in output files.

Use targeted UI automation for important command/focus/lifecycle paths. Use screenshots for visual judgment; do not create brittle pixel tests for every spacing value.

## Performance targets

These are proposed engineering targets, not measured results. M0 establishes the acceptance PC and M2/M5 record measurements. Revise a target only with a documented reason and the product impact, not simply to hide a failed check.

| Measure | Initial target / acceptance method |
| --- | --- |
| UI acknowledgment | Visible response within 100 ms for Output Off/Stop under simulator load; no synchronous serial or disk work on the dispatcher. |
| Cancellation dispatch | Worker observes the latch within one bounded read slice plus scheduling overhead; verify with delayed-byte fixtures and real adapter timing. |
| Healthy shutdown | Aim for output-off confirmation within 1 second on the accepted profile; report actual latency and use the bounded overall policy in plan 04. This is not a physical emergency-stop guarantee. |
| Monitoring | Aim for one complete V/I pair per second at 9600 baud in steady monitoring. Record achieved cadence, pair skew, status-query cost, and behavior at 4800. |
| Startup | Aim for a usable disconnected/simulator window within 3 seconds on the acceptance PC; connection initialization is separate. |
| Long session | Two-hour simulator soak at the fastest supported cadence. Plot history remains one hour; buffers/queues stay within declared capacities; retained memory stabilizes after buffers fill. |
| Storage stall | Inject stalled/full/failed writes; prove the configured health threshold and bounded shutdown behavior without UI freezing. |

Use measurements to identify where query cost prevents a target cadence. Show the achieved rate in the product, skip overdue polls, and document the limitation. Do not fake the requested rate by duplicating readings or drawing interpolated points.

## Controlled hardware acceptance

This plan describes future operator-supervised acceptance. Writing these planning files does not connect to or operate the instrument.

1. Record the Windows build, adapter/driver, cable, instrument identity, selected profile, and relevant initial device settings. Confirm front-panel RS-232/SCPI and matching settings. The user's guide describes the connection and front-panel configuration. [Keysight user's guide, printed pages 30-31 and 46](https://www.keysight.com/ph/en/assets/9018-01126/user-manuals/9018-01126.pdf)
2. Identify and read state without resetting the device. Confirm displayed settings, limits, measured values, mode, and output status against the front panel. Record any already-present faults before clearing them.
3. With output disconnected from any load, free lead ends separated, and normal local sensing correctly configured, perform the original brief's short 1 V / 10 mA-limit check. Verify acknowledged settings and measured voltage, then request and verify output-off. This is a functional connection check, not calibration.
4. Run a brief unloaded sequence. Check transition order, actual hold timestamps, row/repeat indexes, signed/available sample fields, automatic recording, and verified output-off at completion.
5. Repeat bounded unloaded cases for Stop, Output Off, deliberate app disconnect, and orderly close. Inspect the transcript and manifest; confirm local-control restoration after shutdown.
6. Validate delayed-response recovery and Break behavior under controlled conditions. Test unplug/replug with output already verified off. Use simulator/transport fixtures for hazardous or uncontrolled failures rather than deliberately leaving an energized load unmanaged.
7. Validate selected slower polling and 4800 baud. Test a configured protection readback/clear workflow only when its cause is understood and output is off; do not induce destructive faults to exercise the UI.
8. Restore initial setpoints and protections **with output off**, verify the restored settings, and return local control. Never restore an initially enabled output automatically.

Any failure to confirm shutdown is a release-blocking defect or an explicitly documented physical communication limitation with the required unknown-state behavior. Hardware acceptance must not replace that behavior with a claim that disconnect guarantees output-off.

## Requirement traceability

| Requirement | Main milestone | Evidence |
| --- | --- | --- |
| R01 Connection | M1-M2 | T01-T03, hardware steps 1-2. |
| R02 Telemetry | M3 | T09, cadence/skew measurements. |
| R03 Manual changes | M2 | T04-T05, hardware step 3. |
| R04 Output control | M2 | T07/T16/T17, hardware step 5. |
| R05 Protections/presets | M2-M3 | T06/T15, hardware steps 2 and 7. |
| R06 Trends | M3 | T09, screenshot review, soak. |
| R07 Sequence editor | M4 | T10/T17, keyboard/JSON checks. |
| R08 Execution | M4 | T07/T08/T11/T12, hardware step 4. |
| R09 Recording | M3-M4 | T12-T15, independently parsed run files. |
| R10 Failure/shutdown | M2/M4/M5 | T03/T07/T14/T16, hardware steps 5-6. |
| R11 Diagnostics | M3 | Transcript/event inspection during failure tests. |
| R12 Simulator | M1 | Deterministic scenario checks and UI integration. |
| R13 Windows experience | M0/M5 | DPI, accessibility, keyboard, screenshots, latency. |
| R14 Portable delivery | M0/M6 | Clean-machine package smoke test. |
| R15 Reusable modules | M1-M4 | Headless tests and dependency review. |

## Release checklist

- [ ] All applicable automated checks pass; no unresolved output-ordering or false-verification defect.
- [ ] Accepted visual states, keyboard paths, DPI settings, and accessibility checks are recorded.
- [ ] Hardware claims name the tested instrument/profile/adapter and have actual acceptance evidence.
- [ ] The publish script creates a reproducible self-contained `win-x64` folder with native chart dependencies included.
- [ ] A clean non-admin environment without installed .NET launches simulator mode and reads/writes user data correctly.
- [ ] Packaging works from a path with spaces and an installation folder the app cannot write to.
- [ ] Source, SDK/package locks, build instructions, font/dependency licenses, version, known limitations, and checksum accompany the release.
- [ ] Quick-start documentation explains matching front-panel settings, explicit output control, recording locations, and unverified shutdown.
- [ ] Updates do not overwrite preferences or recordings and do not run during an active test. Automatic updating is outside v1.
- [ ] A simulator-only preview is labeled as such if any hardware gate remains open.
