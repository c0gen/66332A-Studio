# Experience and visual design

## Direction: a precision instrument workspace

Use the visual language of a well-made bench instrument: a graphite enclosure, an inset measurement display, fine alignment, clear units, and luminous but restrained signal colors. The distinctive element is one wide measurement display that reads as a coherent instrument face. Its typography and composition provide the flair; the rest of the workspace is quiet and functional.

The original brief already calls for an instrument-style dark theme. Develop that idea deliberately rather than adopting a generic dark dashboard kit. Voltage and current receive separate, stable colors throughout the application. Neither channel color means success or failure.

### Visual tokens

| Resource | Value | Use |
| --- | --- | --- |
| `Enclosure` | `#0E131A` | Window background |
| `Surface` | `#151C26` | Cards and panels |
| `SurfaceRaised` | `#1D2632` | Secondary buttons, popups, hover rows |
| `Well`, `TableStripe` | `#090D13` | Readout surrounds, inputs, plots, nav rail and tables |
| `DisplayWell` | `#0B1017` to `#06090D` | Vertical readout gradient |
| `Line`, `ControlLine` | `#253140`, `#60738A` | Dividers and input boundaries |
| `Ink`, `Muted`, `DisabledInk` | `#EEF2F6`, `#A7B4C2`, `#74838F` | Primary, secondary and disabled text |
| `Action`, `ActionInk` | `#8B93FF`, `#0B1020` | Indigo-violet primary actions, selection and focus |
| `ActionHover`, `ActionPressed` | `#A3AAFF`, `#747DE8` | Action interactions |
| `ActionSoft`, `Selected` | `#232A40`, `#2A314D` | Selected rail items and chips, selected rows and text |
| `HoverOverlay`, `PressedOverlay` | `#14EEF2F6`, `#29000000` | Interaction overlays |
| `Voltage`, `Current` | `#68C9E5`, `#EFB57D` | Voltage and current readings, traces and channel markers |
| `Ready`, `ReadySoft` | `#79D5AE`, `#213236` | Ready and verified states |
| `Warning`, `WarningSoft` | `#F0CA78`, `#2F3130` | Energized, pending and acknowledgement states |
| `Fault`, `FaultSurface`, `FaultLine` | `#FF8E98`, `#312A34`, `#C56B79` | Faults and tonal Output Off |
| `Danger`, `DangerHover`, `DangerInk` | `#D9364C`, `#C8324A`, `#FFFFFF` | Live Output Off and stop actions |

Use neutral ink for estimated power. Card depth comes from surface tone and 1-DIP Line borders. Shadows belong to popups and tooltips. Glow belongs to the live digits and connection indicator.

**Typography**

- IBM Plex Sans Regular for 14-DIP body text. Use 20-DIP SemiBold page titles, 16-DIP SemiBold card titles, 13-DIP Medium field labels, and 12-DIP Regular captions.
- IBM Plex Mono Regular and Medium for 13-DIP numeric fields and transcripts. ScottPlot uses the bundled Plex Sans Regular font. [IBM Plex source](https://github.com/IBM/plex)
- The Technology font renders the seven-segment display. Fixed-width cells right-align each digit over faint unlit segments. Use 64-DIP voltage/current and 44-DIP power digits, dropping to 44 and 32 in compact mode. Units are separate 20-DIP Plex Sans Medium labels.
- Use sentence case in the workspace and uppercase for instrument annunciators.

**Geometry**

Use the 4/8/12/16/24-DIP spacing scale, 6-DIP control radii, `12`-DIP panel radii and fully rounded pills. Standard controls are at least 36 DIPs high. Table editors can use 30 DIPs. Cards use 16-DIP padding and 12-DIP gaps.

### Design review before implementation

Two layouts were considered: equally sized meter cards above tabbed tools, and an integrated measurement strip above a plotting workspace with side controls. Select the latter because it emphasizes relationships between readings and keeps control next to feedback. Keep estimated power visually subordinate because it is derived from sequential readings.

Reject ornamental radial gauges and scrolling digital-number animations. They add attention without helping the operator inspect a slowly sampled supply. The approved emphasis is the measurement strip plus coordinated chart crosshairs. Implement a simulator-backed screenshot of this composition in M0 and review it before extending the interface.

## Shell and navigation

Keep the standard Windows window behavior, including title-bar drag, resize, minimize, close, and snapping. The native title bar uses the dark enclosure palette on supported Windows versions.

Default size is approximately 1440 × 900 DIPs, fitted to the current monitor work area, with a 1000 × 620 DIP minimum. The compact display scales the digits at shorter heights. The right-hand manual panel scrolls internally.

A 76-DIP icon rail exposes Monitor, Sequences, Recordings, Diagnostics, Capture, Setup and Service. The shared header shows the instrument identity, a connection status button, and a permanent **Output Off** button. Output Off uses a solid red fill while the output is live, a tonal treatment while connected with output off or unknown, and the disabled style when unavailable. The status footer shows the latest message and session details.

Output Off stays available according to the session's safety rules throughout the workspaces and during edits and runs. The status button opens a modeless Connection settings window with the serial controls, bench qualification, and another Output Off button. File workflows use in-window controls.

### Monitor wireframe

```text
+--------------------------------------------------------------------------------------+
| 66332A Studio           Agilent 66332A / COM7          Connected        [Output Off]    |
+-------------+------------------------------------------------------------------------+
| Monitor     | Voltage                 Current                    Power estimate       |
| Sequences   |  5.0012 V                0.02481 A                   0.1241 W             |
| Recordings  | Set 5.000 V              Limit 0.100 A              CV / Output on       |
| Diagnostics | Updated 0.3 s ago        V/I sampled separately                         |
|             +-------------------------------------------+----------------------------+
|             | Trends             1m  5m  15m  60m      | Manual control             |
|             | Voltage                                  | Voltage       [5.000] V    |
|             |  5 V  --------------------------------   | Current limit [0.100] A    |
|             |                                          | [Apply changes]            |
|             | Current                                  | [Output On]                |
|             | .02 A  __/---\_______                     |                            |
|             |       14:30       14:31       14:32        | Protections      [Edit]    |
|             | Shared cursor: V/time and I/time           | OVP / OCP / OCP delay      |
|             +-------------------------------------------+ Preset          [Load...] |
|             | Recording off          [Start recording]  | Requested / acknowledged   |
+-------------+-------------------------------------------+----------------------------+
| Ready      Actual 0.98 updates/s       No active faults       Last command verified    |
+--------------------------------------------------------------------------------------+
```

All values above are illustrative. Significant digits in the product must reflect the validated measurement/formatting policy, not the number of characters that fit on screen.

## Measurement display and plots

- Voltage and current are peers. Estimated power is smaller and always labeled as an estimate.
- Show measured values above acknowledged setpoints. Pending edits appear only in the editor with a clear Pending label; they never masquerade as device settings.
- Show both acquisition timestamps in the cursor details. The main reading age uses the older member of a complete pair. A partial pair cannot produce a new power estimate.
- Unavailable readings use low-contrast dashed placeholders. On a lost connection, the age strip shows stale timing and a last-known value when available.
- The annunciator group shows the verified output state, reported CV or CC, protection, and simulator status. The connection pill shows session state independently.
- Use two aligned plots with a shared time axis and synchronized inspection cursor. An optional third plot shows estimated power. Avoid putting V and A on one unlabeled scale.
- Offer 1, 5, 15, and 60 minute views, pan and zoom, visible channel toggles, Reset view and Follow live. Panning disables Follow live.
- Use straight sample connections, no smoothing or fabricated intermediate readings, and visible discontinuities for invalid/missing periods. Show setpoint traces as dashed lines with labels. Any display decimation must retain gaps and extrema.
- Add unobtrusive markers for output changes, row transitions, and faults. A visible cursor freezes its inspected location without freezing acquisition.

## Manual controls and protections

Numeric editors show units, valid bounds, and focused-field help. Support typed scientific notation and display-unit conversion; validate finite values and retain full internal precision. Support the user's numeric culture in editors while keeping file/protocol formats invariant. Reject ambiguous input rather than guessing.

**Apply changes** is enabled only for a valid dirty setpoint draft. During application, show Applying, then the readback values or an actionable failure. Escape restores the editor draft from the last acknowledged state. Plain Enter commits an editor cell; it does not apply to hardware or enable output. No output-on keyboard shortcut ships in the first release.

**Output On** enables only the currently acknowledged settings. Disable it when there are pending setpoint edits, unverified state, faults, an active run, or an application transaction. Show the disabled reason below the full-width Output On action. A preset fills the draft; it never applies or enables output automatically.

Protection configuration uses an in-window editor. Require verified output-off before applying protection changes. Use the label **OCP delay** with explanatory help, not a generic promise that all protection is delayed. Clear fault is a distinct action with its own progress and result; clearing must not enable output. [Instrument behavior](04-instrument-control.md)

## Sequence workspace

```text
+--------------------------------------------------------------------------------------+
| Connection and identity remain visible                                [Output Off]   |
+-------------+------------------------------------------------------------------------+
| Sequences   | Startup check                        [Open] [Save] [Validate] [Run]      |
|             | Repeat [3]    Nominal holds: 45 s plus transitions                       |
|             | Step preview: requested voltage / requested current limit               |
|             +-----+----------------+---------------+---------+----------+-------------+
|             | Row | Voltage (V)    | Limit (A)     | Output  | Hold (s) | Validation  |
|             +-----+----------------+---------------+---------+----------+-------------+
|             |  1  | 1.000          | 0.010         | On      | 5        | Ready       |
|             |  2  | 2.000          | 0.010         | On      | 5        | Ready       |
|             |  3  | 0.000          | 0.010         | Off     | 5        | Ready       |
|             +-----+----------------+---------------+---------+----------+-------------+
|             | [Add row] [Duplicate] [Move up] [Move down] [Delete]                     |
|             | Running row 2 of 3 / Repeat 1 of 3 / Hold remaining 3.2 s        [Stop]   |
|             | Recording: Startup check / Current readings / Recent run events         |
+-------------+------------------------------------------------------------------------+
```

Use a genuine editable table with visible keyboard focus, cell validation, and sticky headings. Drag reorder is optional convenience; Move up/down is always available. During execution, lock the sequence editor and manual writes. Highlight the active row with a side marker and the corresponding span in the always-visible preview.

Show Transitioning and Holding in the status card, with a progress bar for the current hold. Overall duration is an estimate because instrument transactions add time. Stop run and Output Off both cancel the run and initiate output-off verification.

## Recordings and diagnostics

Recordings use a compact list of name, start time, elapsed time, outcome, shutdown result, and location. Provide Open folder and View metadata. Treat incomplete recordings as visible items with an Incomplete badge; never silently discard them. The initial implementation does not load entire recordings into live plot memory.

Diagnostics separates an operator-friendly event list from expandable technical details. Useful messages include "Voltage read timed out", "Output off requested", and "Output off verified". Raw SCPI transcripts are opt-in views and bounded in memory. There is no arbitrary command entry in v1.

## State presentation

| State | What the operator sees | Available next action |
| --- | --- | --- |
| Disconnected | Dashed readings, a connection status button and guidance in the plot area. | Connect or change profile. |
| Connecting | Explicit progress stages; no live values yet. | Cancel connection; Output Off becomes usable after model verification. |
| Ready, output off | Neutral Off label and fresh measurement age. | Stage/apply settings or enable acknowledged settings. |
| Output on | Amber OUTPUT ON, CV/CC, acknowledged settings and a solid red Output Off button. | Apply setpoints, record, or Output Off. |
| Simulator | Persistent Simulator badge and different identity; all recordings carry simulator metadata. | Exercise workflows or disconnect. |
| Stale | Age and Stale label; interrupted plots. | Inspect diagnostics; no new control operation until state is fresh. |
| Protection fault | Persistent fault description and separate output command state. | Output Off, inspect cause, then clear when eligible. |
| Lost connection | Unavailable live values; last verified state/time; Output state unknown. | Explicit reconnect; inspect physical instrument. |
| Stopping | Stop request acknowledged immediately; output-off verification progress. | Keep viewing progress; no new run or output-on action. |
| Unverified shutdown | Persistent warning with the failed verification and last known state. | Retry if connected, reconnect, or close after acknowledging the unresolved state. |

## Motion, keyboard, and accessibility

Use 100–160 ms transitions for hover, toggles, expanding panels and switching workspaces. The Ready connection pill has a 1.8-second dot pulse as the sole repeating animation. Measurements, output, faults and connection labels update immediately. Windows Animation effects, Windows high contrast and screenshot verification disable motion. High contrast uses system colors and removes the gradient and glow.

All meaningful states have text or icons as well as color. Meet at least 4.5:1 text contrast for normal text and 3:1 for large text and essential control boundaries. Provide visible focus, accessible names, logical tab order, UI Automation properties, and useful validation announcements. High contrast replaces decorative treatments with system-friendly colors.

Provide a documented Ctrl+Shift+O shortcut for Output Off throughout the app, including editors; it never requires a confirmation. Do not rely on that shortcut as a hardware emergency stop. Routine polling must not flood screen-reader announcements. Chart information is also available as current-value/cursor text.

## Visual acceptance

Review real WPF screenshots of disconnected, simulator, normal monitoring, pending edits, active sequence, stale readings, protection fault, and unverified shutdown states. Confirm no clipping at supported DPI settings; aligned numeric columns; distinct measured/pending/acknowledged values; readable plot axes; and an always-accessible Output Off action. The acceptance checklist belongs to [plan 07](07-verification-and-release.md).
