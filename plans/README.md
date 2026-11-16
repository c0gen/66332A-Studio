# 66332A Studio: project plans

Planning date: November 3, 2026. Implementation began from this index. Status: simulator preview implemented; hardware validation and the remaining release gates are open. See the [application README](../README.md) and [implementation evidence](../docs/implementation-status.md).

Build a polished Windows desktop console for one Agilent 66332A: live measurements, deliberate manual control, editable test sequences, and dependable local recordings. The working product name is **66332A Studio**.

The recommended implementation is **C# on .NET 10 LTS, WPF, CommunityToolkit.Mvvm, and ScottPlot.WPF**. A custom visual system supplies the modern appearance. Python remains a reasonable alternative, but the Windows-only scope favors one C# application with a reusable instrument library. The rationale and alternatives are in [the architecture plan](03-technology-and-architecture.md).

## Read in this order

| Plan | Purpose |
| --- | --- |
| [01: Product scope](01-product-scope.md) | Product goals, requirements, scope boundaries, and changes to the original premise. |
| [02: Experience and visual design](02-experience-and-visual-design.md) | Visual direction, tokens, screen wireframes, interactions, and accessibility. |
| [03: Technology and architecture](03-technology-and-architecture.md) | Framework decision, module boundaries, execution model, and reusable contracts. |
| [04: Instrument control](04-instrument-control.md) | Serial communication, verified state, command ordering, protection handling, and shutdown. |
| [05: Sequences and recording](05-sequences-and-recording.md) | Execution semantics, file schemas, recording durability, and settings storage. |
| [06: Implementation roadmap](06-implementation-roadmap.md) | Dependency-ordered milestones with concrete completion gates. |
| [07: Verification and release](07-verification-and-release.md) | Automated checks, visual review, hardware acceptance, and portable delivery. |
| [08: Decisions, risks, and sources](08-decisions-risks-and-sources.md) | Decision record, unresolved measurements, and primary references. |
| [09: UI polish pass](09-ui-polish-pass.md) | Appearance deficiencies, updated design tokens, and ordered work packages for the visual refresh. Implemented. |
| [10: UI refinement pass 2](10-ui-refinement-pass.md) | Second-pass deficiencies in the refreshed build and ordered work packages for state legibility, charts, consistency and depth. Not yet implemented. |
| [Original brief](reference/original-plan.md) | Preserved user-supplied starting point; historical reference. |

## What changes from the original

- Replace the Python-first assumption with a Windows-focused C# stack, selected for this project's needs rather than appearance alone.
- Design a cohesive instrument workspace: a distinctive measurement display, aligned trend plots, and a persistent command area instead of a collection of generic dashboard cards.
- Establish visual quality in the first working slice, then retain it through implementation.
- Make device state, requested changes, acknowledged settings, and uncertain output state separate concepts throughout the app.
- Specify the behavior of presets, recording failures, shutdown races, sequence timing, and reconnects before implementation.
- Keep the original one-second monitoring and seconds-scale automation scope. Fast waveform acquisition is a later feature with its own design.

## How to use these plans

Start with milestone M0 in [the roadmap](06-implementation-roadmap.md). Implement one demonstrable slice at a time; check off work only after its completion gate is met. Hardware-dependent gates remain open until tested on the actual supply and adapter. A simulator release can precede those gates, but must be labeled accordingly.

These documents remain the product specification. Roadmap checkboxes record implemented software; milestone gates distinguish simulator results from physical acceptance. The original brief is preserved. If later evidence changes a decision, update its owning plan and the decision record together. The preview is not approved for hardware output control.

Each subject has one owner: visual behavior in 02, module responsibilities in 03, instrument invariants in 04, run/file semantics in 05, and acceptance evidence in 07. Cross-reference those documents instead of duplicating implementation rules across files.
