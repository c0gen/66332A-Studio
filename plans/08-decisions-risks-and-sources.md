# Decisions, risks, and sources

Planning baseline: November 3, 2026. The repository initially contained only this planning set. The simulator preview now has a repository-local .NET 10.0.403 SDK, software tests, real WPF rendering/integration evidence and a self-contained publish path. See [implementation evidence](../docs/implementation-status.md). No physical instrument connection or hardware behavior was tested.

## Decision record

| ID | Decision | Reason and consequence |
| --- | --- | --- |
| D01 | Use C# / .NET 10 LTS / WPF. | Fits one Windows application with reusable instrument logic; custom templates provide the intended visual quality. See plan 03. |
| D02 | Build a distinct visual system early. | Typography, measurement hierarchy, plotting, and failure states are product requirements; the first slice must demonstrate them. |
| D03 | Use ScottPlot behind a narrow presentation adapter. | Keeps acquisition/recording independent of chart APIs and allows gap/theme behavior to be tested locally. |
| D04 | Give one worker exclusive serial ownership. | Prevents interleaved replies and gives shutdown priority a single implementation point. |
| D05 | Keep typed requests separate from acknowledged state. | Prevents optimistic UI from falsely representing hardware behavior. |
| D06 | Require output-off for protection edits and fault clearing. | Removes ambiguous protection transitions from ordinary live setpoint application. |
| D07 | Use explicit reconnect with no automatic resume. | Old commands and sequence position must not be replayed after an uncertain session. |
| D08 | Record every sequence; retain incomplete evidence. | A run without its required recording is a failure that requests output-off. |
| D09 | Separate execution, shutdown, and recording outcomes. | All rows finishing does not prove the output is off or files are complete. |
| D10 | Support seconds-scale row execution, not waveform generation. | Matches the original premise and avoids promising real-time behavior from a desktop serial loop. |
| D11 | Use local versioned JSON/CSV/JSONL files. | Portable, inspectable data without a database or service dependency. |
| D12 | Publish a self-contained folder before considering an installer. | Preserves the original portable-delivery requirement and tests deployment risk early. |
| D13 | Ship a simulator preview with read-only physical connection. | Adapter/Break/quantization acceptance has no physical evidence; desktop composition cannot enable physical output or change physical settings until that gate is implemented and validated. |
| D14 | Reuse the typed client/worker over a simulator transport. | Normal UI workflows exercise the production framing and ordering; separate scripted transports cover failures. A development-only headless harness verifies reusable boundaries. |
| D15 | Use indigo-violet for actions and selection, with 12-DIP panels and a left icon rail. | Stable channel colors and predictable navigation separate actions from instrumentation. |
| D16 | Bundle DSEG7 Classic Regular with fixed digit cells, ghost segments, and a static live glow. | Local and packaged builds use the same seven-segment face under the SIL Open Font License 1.1. |
| D17 | Use state-reactive Output Off and one looping Ready pulse. | Output Off fills red immediately while energized. Reduced motion and high contrast suppress the connection pulse and other transitions. |

## Changes and retained behavior

| Original brief | Revised plan |
| --- | --- |
| Python 3.11 / PySide6 / pyqtgraph / pySerial / PyInstaller. | C# / .NET 10 / WPF / ScottPlot / System.IO.Ports / self-contained folder; alternatives recorded rather than dismissed. |
| Dark instrument theme with large values and tabs. | Integrated measurement display, aligned channel plots, persistent command area, seven workspaces on a left icon rail, and reusable visual tokens. |
| Driver-first, then UI, sequences, packaging. | Early visual and packaging feasibility slice, followed by tested instrument logic, monitoring/recording, sequences, and release review. |
| One serial worker, verified changes, fail-to-off behavior. | Retained; detailed cancellation generations, bounded recovery, and outcome semantics added. |
| COM7 / 9600 / 8N1 / no flow / CRLF / DTR and RTS. | Retained as a proposed initial profile, made explicit as something that must match the actual instrument/cable. |
| Protection delay. | Labeled OCP delay, with the distinction from OVP made explicit. |
| Editable step table and finite repeats. | Retained; row transition/repeat behavior, immutable execution copy, and monotonic timing specified. |
| Automatic sequence CSV and manual recording. | Retained; versioned manifest, event stream, failure detection, and incomplete-run handling added. |
| Named presets. | Retained as staged requests; protection changes have a verified-off workflow. |
| Simulator and focused tests. | Retained; split high-level simulator from byte-level protocol fixtures. |
| Portable executable folder. | Retained; early clean-environment smoke test and runtime servicing responsibility added. |

## Risks and resolution gates

| Risk | Impact | Resolution / evidence | Gate |
| --- | --- | --- | --- |
| Adapter or cable behaves differently from assumptions. | Reads, handshaking, or Break recovery fail. | Identify actual hardware and validate its profile, cancellation, and synchronization. | M0/M2 hardware |
| Full polling cycle exceeds one second. | Requested rate and actual readings diverge. | Measure query costs, coalesce polls, show achieved rate, and document supported cadence. | M2/M3 |
| Late numeric reply is mistaken for a new value. | False verification of a setting or output state. | Script delayed replies across recovery; require validated wire synchronization before further trust. | M1/M2 |
| Pending output-on survives cancellation. | Device re-enables after Stop. | Generation/latch checks at every dispatch and targeted serial-worker race tests. | M1/M4 |
| Fault clear or protection edits have unexpected effects. | Output or protection state differs from the UI. | Verified-off workflow, per-command readback, and specific hardware checks. | M2 |
| Chart hosting, custom styles, or fonts fail at DPI/accessibility boundaries. | Attractive screenshots conceal an unusable product. | Early real-WPF slice, screenshot/accessibility review, static font fallback. | M0/M5 |
| Disk appears writable but fails during a run. | Missing evidence while output remains enabled. | Health monitoring, bounded queues, shutdown independent of disk, incomplete manifests. | M3/M4 |
| Process termination or physical disconnect prevents shutdown. | Output may remain active. | Honest unknown-state reporting, explicit reconnect, and no false software guarantee. | M2/M5 |
| Sleep or scheduler stall breaks timing assumptions. | Holds/runs continue unexpectedly. | Detect interruption, abort, attempt shutdown, and require reconnect. | M4/M5 |
| Portable package misses native dependencies. | Works only on the development PC. | Publish in M0 and smoke-test again at release without preinstalled runtime. | M0/M6 |
| Visual polish expands into unrelated features. | Delayed useful release. | Keep the scoped signature display; defer waveform capture, scripting, and multi-device support. | Every milestone |

## Questions to resolve through implementation

These are explicit assumptions and probes, not reasons to stop preparing the plans:

1. What Windows build and display scaling are used at the bench? Initial release acceptance targets Windows 11 x64, with adaptive layout at the sizes in plan 02.
2. Which RS-232 adapter, driver, and cable are actually present? Confirm pinout/profile compatibility and Break behavior rather than inferring from a COM port name.
3. What identity string, programming bounds, quantization, measurement configuration, and status behavior does this specific unit report? Record those in the validated device profile.
4. What is the achievable complete-cycle rate at 9600 and 4800 baud, including status reads? Keep 1 Hz as a target until measured.
5. What read-slice, transaction, recovery, and shutdown budgets work reliably with that adapter? Replace proposed values with measured, documented policy.
6. Can the selected ScottPlot version satisfy gap handling, cursor interaction, DPI, and accessibility requirements? Test in M0; replace its adapter implementation only if evidence requires it.
7. Does the user later need Python scripts, other operating systems, additional instruments, or waveform capture? Reuse current domain boundaries; design those capabilities when requested.
8. Are recordings intended for long uninterrupted tests or restricted folders? The initial design streams and detects failure, but retention quotas and specialized archival policies are future requirements.

## Primary references

Links support technical premises; the architecture, visual direction, scope choices, and provisional performance budgets are design recommendations for this project.

| Reference | Where it informs the plans |
| --- | --- |
| [Keysight 66312A/66332A programming guide](https://www.keysight.com/mt/en/assets/9018-01225/programming-guides/9018-01225.pdf) | Serial configuration, command verification, protection behavior, and recovery checks in plan 04. Consult the command dictionary when implementing each mapping. |
| [Keysight 66332A user's guide](https://www.keysight.com/ph/en/assets/9018-01126/user-manuals/9018-01126.pdf) | Physical connections and front-panel setup; controlled acceptance context. |
| [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | .NET 10 LTS selection and supported servicing baseline. |
| [WPF styling and templates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview) | Custom presentation separated from application behavior. |
| [WPF in .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100) | Current WPF development context. |
| [MVVM toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) | View-model infrastructure choice. |
| [ScottPlot WPF quickstart](https://scottplot.net/quickstart/wpf/) | WPF plot-control integration; further API behavior must be validated against the pinned version. |
| [SerialPort API](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport) and [ReadTimeout](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.readtimeout) | Serial transport abstraction and bounded-read implementation considerations. |
| [.NET deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/) | Self-contained publishing and deployment choices. |
| [IBM Plex](https://github.com/IBM/plex) | Proposed bundled typography and license source. |
| [Avalonia](https://docs.avaloniaui.net/docs/welcome), [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/), [Tauri 2](https://v2.tauri.app/start/), [Qt for Python](https://doc.qt.io/qtforpython-6/) | Alternative framework context for the decision in plan 03. |

The [original brief](reference/original-plan.md) remains the source of the basic app premise and its baseline operational requirements. These plans do not replace the manufacturer's manuals for installation, electrical operation, or calibration.
