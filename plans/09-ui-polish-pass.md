# 09: UI polish pass (handoff spec)

## Context

The app works and its safety model is sound, but it reads as a competent engineering prototype, not a commercial product: one washed-out accent, every control drawn as an outlined box, text-only buttons, folder tabs, an undersized hero readout, and a font bug that makes most text look smeared-bold. The goal is **clean, commercial, professional, very modern, a little flashy**.

This document records the deficiencies and work packages for the WPF polish pass. The design keeps the seven-segment readout, adds a left icon rail, and uses indigo-violet for actions and selection.

**Status (November 15, 2026).** The work packages are implemented. The current visual system is documented in [plan 02](02-experience-and-visual-design.md). The WPF harness regenerated all 14 screenshots with zero binding errors and checked Output Off visibility in every workspace at 1000 × 620. All 76 automated tests passed. [Simulator verification report](../artifacts/ui-evidence/verification.txt)

Live simulator checks covered the dark native title bar, keyboard rail navigation, keyboard focus, minimum window sizing, representative popups, and relaunches with Windows contrast and Animation effects changed. Both Windows settings were restored.

Baseline evidence included all 14 screenshots in `artifacts/ui-evidence/`, every XAML file under `src/Agilent66332A.Desktop/`, `Verification/DesktopVerification.cs`, `plans/02-experience-and-visual-design.md`.

---

## Ground rules for the implementing model (read before every WP)

1. **Appearance only.** Do not change command logic, enable/disable rules, safety gating, or view-model behavior. Small read-only view-model properties are allowed only where a WP names them.
2. **The screenshot harness finds buttons by exact `Content` string** (`DesktopVerification.FindVisibleButton`). These captions must stay plain strings, stay visible (not inside a collapsed expander), and keep an `IAsyncRelayCommand`: `Output Off`, `Start recording`, `Stop recording`, `Stop run`, `Start capture`, `Export capture`, `Read instrument`, `Read service settings`, `Run self-test`. Add icons through the `c:Icon.Geometry` attached property (WP3), never by replacing `Content`.
3. **`Output Off` must be visible and unclipped in every state and size** (harness asserts this on every capture). Minimum window stays 1000×620.
4. Keep `WorkspaceTabs` as a `TabControl` with the seven `TabItem`s in the current order (`MainWindow.SelectWorkspace(int)` and `WorkspaceChanged` depend on it). Keep `x:Name`s used by code-behind: `ConnectionToggle`, `MeasurementDisplay`, `WorkspaceTabs`, `WaveformPlot`, `StatisticsText`, `Passcode`, `NewPasscode`, `VoltagePlot`, `CurrentPlot`, `PowerPlot`, `VoltageRow`, `CurrentRow`, `PowerRow`, `NoChannels`, `CursorText`.
5. Keep every `AutomationProperties.*`, `Label Target=`, `ToolTip`, and `LiveSetting`. Binding errors must remain **0**.
6. Color meaning is fixed: `Voltage` cyan and `Current` amber are channel colors only. Red (`Fault`, `Danger`) is only for faults and Output Off/Stop. `Warning` amber = energized/pending. `Action` indigo = primary action, selection, focus.
7. Every new brush key must be added to `MainWindow.ApplyHighContrastPalette()`. Glows, gradients and motion are off in high contrast.
8. No new NuGet packages. No modal dialogs. No count-up number animation. Never delay or animate a safety-state change (output/fault/connection text must update instantly).
9. After each WP: run `tools\verify-ui.ps1` (must print `Binding errors: 0`) and `tools\test.ps1`, then open the regenerated PNGs in `artifacts/ui-evidence/` and check the WP's acceptance bullets.

---

## Deficiencies (prioritized)

| ID | Sev | Deficiency (evidence) | Fixed in |
|---|---|---|---|
| D1 | High | **Everything inside a workspace renders faux-bold.** `Workspace.xaml` sets `FontWeight=SemiBold` on the selected `TabItem`; the tab's content is its logical child and inherits it. Compare "Setpoint 5 V" (regular, outside tabs) with "Range 0–20.475 V" (bold, inside) in `03-output-on.png`. | WP1 |
| D2 | High | **Only Regular font files are bundled** (`Assets/Fonts`: Plex Sans Regular, Plex Mono Regular). Every Medium/SemiBold is synthesized by WPF, which looks smeared. | WP1 |
| D3 | High | **Seven-segment digits are proportional** (`Technology.ttf`: "1" is 145 units wide, "0" is 447). The readout jitters sideways as values change and digits read poorly ("5.0001" looks like "5.000I"). No unlit segments, no glow; 48 px digits float in a 1400 px strip. | WP6 |
| D4 | High | **No brand accent; weak button hierarchy.** `Action` is pale grey-blue `#B8D3EA`. Primary, secondary and disabled buttons are all dark outlined boxes; disabled looks almost identical to enabled (`01-disconnected.png`). | WP2, WP3 |
| D5 | High | **Wireframe look.** Every button, input and panel carries a bright 1 px outline (`ControlLine #657A8F`) on muddy navy surfaces with little tonal separation. | WP2–WP4 |
| D6 | High | **Output state is nearly invisible.** The most safety-relevant status is a 12 px caption under "Power estimate". `Output On` is a small generic button; `Output Off` looks the same whether the output is live or not. | WP5, WP6, WP8 |
| D7 | Med | **Dated navigation.** Seven folder-style top tabs, text only; Capture nests a second tab row directly under the first (`12-waveform`). | WP5, WP4 |
| D8 | Med | **No icons anywhere**; no logo in the window (the `.ico` mark is never shown in-app). | WP3, WP5 |
| D9 | Med | **Light native title bar** on a dark app (not visible in the harness PNGs, visible on screen). | WP5 |
| D10 | Med | **Charts look like library defaults**: boxed frame on all sides, cramped default tick font, no fill, autoscale to −10…10 when empty, visibility toggles hidden in a "Channels" expander. Styling code is duplicated in `TrendPanel.xaml.cs` and `CaptureView.xaml.cs`. | WP7 |
| D11 | Med | **Bleak first-run screen**: dashes, two empty grids, a wall of disabled buttons, no guidance (`01-disconnected.png`). | WP12 |
| D12 | Med | **Unstyled system controls**: `ToolTip` and `ContextMenu` use the light Windows default; `PasswordBox` has a partial local style with square default chrome; no `ProgressBar` style. | WP4 |
| D13 | Med | **Ragged forms** in Instrument Setup / Service / Diagnostics registers: inline labels in `WrapPanel`s, nothing aligns, long muted paragraphs, safety acknowledgements look like ordinary checkboxes (`13-`, `14-` PNGs). | WP11 |
| D14 | Med | **Tables and logs are plain**: Recordings shows raw strings ("Cancelled / Complete", "Unavailable"), heavy gridlines, square header corners poking out of the rounded panel; Diagnostics lists are undifferentiated strings with a fat horizontal scrollbar. | WP10 |
| D15 | Med | **Sequence run feedback is one line of text** below the table; the nice step preview is hidden in a collapsed expander and draws its text in hard-coded Segoe UI. | WP9 |
| D16 | Med | **No motion at all**: instant hover, instant panel open, instant workspace switch. | WP3, WP4, WP12 |
| D17 | Low | Inconsistent geometry: radii 3/5/6/8/15, paddings 6/7/8/10/12/14/16, card titles 18 px inside small cards, page titles sitting bare on the window background. | WP2, WP8–WP11 |
| D18 | Low | Focus ring is a dashed white rectangle (Win32 look). | WP2 |
| D19 | Low | Chunky 12 px always-opaque scrollbars. | WP4 |
| D20 | Low | Header uses two separate controls ("Connection ⌄" button + status badge) for one concept. | WP5 |
| D21 | Low | Capture statistics are one dot-separated text line. | WP11 |
| D22 | Info | `Technology.ttf` embeds "Copyright (c) 2024 by Vladimir Nikolic. All rights reserved." and is absent from `THIRD-PARTY-NOTICES.md`. **Decision (November 15, 2026): keep it.** The app is not sold, so the font stays as the readout face. Its terms have not been read; see the font note at the end before sharing the package or publishing the repository. | WP1 (notice only) |

---

## Design tokens (final values; contrast computed, WCAG)

**Palette** (`Themes/Palette.xaml`). Existing keys keep their names so templates keep working; `+` marks new keys.

| Key | Value | Notes |
|---|---|---|
| `Enclosure` | `#0E131A` | window background |
| `Surface` | `#151C26` | cards/panels |
| `+SurfaceRaised` | `#1D2632` | secondary buttons, popups, hover rows |
| `Well` | `#090D13` | display, plots, inputs, nav rail |
| `TableStripe` | `#090D13` | stripes removed; key kept |
| `Line` | `#253140` | dividers, card borders |
| `ControlLine` | `#60738A` | input borders only (3.5:1 on Surface) |
| `Ink` | `#EEF2F6` | 15.2:1 on Surface |
| `Muted` | `#A7B4C2` | 8.1:1 on Surface |
| `DisabledInk` | `#74838F` | |
| `Action` | `#8B93FF` | 6.3:1 on Surface |
| `ActionInk` | `#0B1020` | 7.0:1 on Action |
| `+ActionHover` / `+ActionPressed` | `#A3AAFF` / `#747DE8` | |
| `+ActionSoft` | `#232A40` | selected nav item, chips |
| `Selected` | `#2A314D` | selected rows, text selection |
| `HoverOverlay` / `PressedOverlay` | `#14EEF2F6` / `#29000000` | |
| `Voltage` / `Current` | `#68C9E5` / `#EFB57D` | unchanged |
| `Ready` / `+ReadySoft` | `#79D5AE` / `#213236` | |
| `Warning` / `+WarningSoft` | `#F0CA78` / `#2F3130` | |
| `Fault` / `FaultSurface` / `FaultLine` | `#FF8E98` / `#312A34` / `#C56B79` | |
| `+Danger` / `+DangerHover` / `+DangerInk` | `#D9364C` / `#C8324A` / `#FFFFFF` | white on Danger 4.6:1 |

**Geometry:** `+RadiusControl` = `CornerRadius 6`, `+RadiusPanel` = `CornerRadius 12`, pills fully round. Spacing steps 4/8/12/16/24 only. Card padding 16, gap between cards 12, control height 36 (compact inputs in tables 30).

**Type** (IBM Plex Sans; Plex Mono for numbers/logs): `PageTitle` 20 SemiBold · `Title` (card) 16 SemiBold · body 14 Regular · `FieldLabel` 13 Medium Muted · `Caption` 12 Regular Muted · mono 13. Sentence case everywhere; the only all-caps text is instrument annunciators (`CV`, `CC`, `PROT`, `SIM`).

**Depth:** tone + 1 px `Line` borders. Drop shadows only on popups/tooltips. Glow only on the seven-segment digits and the live dot.

**Motion:** 100–160 ms ease-out. Opacity fades in templates; anything that moves or loops goes through the `Motion` helper (WP12), which is off for reduced-motion, high contrast, and harness runs.

---

## Work packages

Order matters for WP1–WP4 (foundation). WP5–WP11 touch separate files and can be done in any order afterwards. WP12–WP13 last.

### WP1 — Fonts and the bold bug (D1, D2)

Files: `Themes/Workspace.xaml`, `Themes/Palette.xaml`, `Assets/Fonts/`, `Agilent66332A.Desktop.csproj`, `Features/Sequences/SequencePreview.cs`, `Verification/DesktopVerification.cs`, `THIRD-PARTY-NOTICES.md`.

1. In the `TabItem` template, name the header presenter (`x:Name="HeaderHost"`). Delete `<Setter Property="FontWeight" Value="SemiBold" />` from the `IsSelected` trigger and replace it with `<Setter TargetName="HeaderHost" Property="TextElement.FontWeight" Value="SemiBold" />`. Same for `Foreground` (set `TextElement.Foreground` on `HeaderHost`, not on the `TabItem`).
2. Add static fonts from the IBM Plex repository (github.com/IBM/plex, folders `packages/plex-sans/fonts/complete/ttf/` and `packages/plex-mono/fonts/complete/ttf/`): `IBMPlexSans-Medium.ttf`, `IBMPlexSans-SemiBold.ttf`, `IBMPlexMono-Medium.ttf` into `Assets/Fonts/`. The existing `<Resource Include="Assets/Fonts/*.ttf" />` glob picks them up. If there is no network access, stop and ask the user to drop the three files in.
3. Also copy `IBMPlexSans-Regular.ttf` to the output folder for ScottPlot (WP7): add `<Content Include="Assets/Fonts/IBMPlexSans-Regular.ttf" CopyToOutputDirectory="PreserveNewest" />`.
4. `Palette.xaml`: change `Title` to 16; add `PageTitle` (20 SemiBold); set `FieldLabel` (in `Inputs.xaml`) to 13 Medium.
5. `SequencePreview.cs`: replace `new Typeface("Segoe UI")` with a typeface built from `(FontFamily)FindResource("BodyFont")`.
6. Add a deterministic check at the top of `DesktopVerification.RunAsync`:
   ```csharp
   var plex = new FontFamily(new Uri("pack://application:,,,/Agilent66332A.Desktop;component/Assets/Fonts/"), "./#IBM Plex Sans");
   foreach (var weight in new[] { FontWeights.Regular, FontWeights.Medium, FontWeights.SemiBold })
       Require(new Typeface(plex, FontStyles.Normal, weight, FontStretches.Normal).TryGetGlyphTypeface(out var face)
           && face.Weight == weight && face.StyleSimulations == StyleSimulations.None, $"IBM Plex Sans {weight} is not bundled.");
   ```
7. Note the added weights in `THIRD-PARTY-NOTICES.md`. In the same table add a row for the readout font: "Technology (seven-segment display font) | `font/technology_2/Technology.ttf` | Copyright (c) 2024 Vladimir Nikolic; terms not verified — check before public distribution". Do not replace or remove the font.

Accept: in `03-output-on.png`, "Range 0–20.475 V…", table rows and list rows are Regular weight; only titles, buttons and the selected nav label are heavier; the new `Require` passes.

### WP2 — Palette, geometry, focus (D4, D5, D17, D18)

Files: `Themes/Palette.xaml`, `Shell/MainWindow.xaml.cs`.

1. Replace brush values and add the `+` keys from the token table. Add `<CornerRadius x:Key="RadiusControl">6</CornerRadius>` and `RadiusPanel` 12.
2. `PanelFrame`: `CornerRadius="{StaticResource RadiusPanel}"`, border `Line`, background `Surface`.
3. `FocusRing`: solid ring — `Rectangle Margin="-2" RadiusX="8" RadiusY="8" Stroke="{DynamicResource Action}" StrokeThickness="2"` (no dash array).
4. `ApplyHighContrastPalette`: map `SurfaceRaised`, `ActionSoft`, `ReadySoft`, `WarningSoft` → `SystemColors.WindowBrush`; `ActionHover`, `ActionPressed`, `Danger`, `DangerHover` → `SystemColors.HighlightBrush`; `DangerInk` → `SystemColors.HighlightTextBrush`.

Accept: all PNGs are visibly darker with clear three-step layering (window < card < raised); nothing still uses `#B8D3EA`.

### WP3 — Icons and the button system (D4, D5, D8, D16)

Files: new `Controls/Icon.cs`, new `Themes/Icons.xaml` (merge in `Instrument.xaml` after `Palette.xaml`), `Themes/Actions.xaml`, then every view to assign styles/icons.

1. Attached property:
   ```csharp
   namespace Agilent66332A.Desktop.Controls;
   public static class Icon
   {
       public static readonly DependencyProperty GeometryProperty = DependencyProperty.RegisterAttached(
           "Geometry", typeof(Geometry), typeof(Icon), new PropertyMetadata(null));
       public static Geometry? GetGeometry(DependencyObject o) => (Geometry?)o.GetValue(GeometryProperty);
       public static void SetGeometry(DependencyObject o, Geometry? value) => o.SetValue(GeometryProperty, value);
   }
   ```
2. `Icons.xaml` — stroke icons on a 16×16 grid, declared as `<Geometry x:Key="…">data</Geometry>`:

   | Key | Data |
   |---|---|
   | `IconMonitor` | `M1.5,8.5 H4.5 L6.5,3 L9.5,13 L11.5,8.5 H14.5` |
   | `IconSequence` | `M2,13 H6 V9 H10 V5 H14` |
   | `IconRecordings` | `M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,6 A2,2 0 1 1 7.99,6 Z` |
   | `IconDiagnostics` | `M2.5,4.5 L6.5,8 L2.5,11.5 M8.5,12 H13.5` |
   | `IconCapture` | `M1.5,11.5 H4.5 V4.5 H8 V11.5 H11.5 V4.5 H14.5` |
   | `IconSetup` | `M3.5,2 V14 M8,2 V14 M12.5,2 V14 M2,5.5 H5 M6.5,10.5 H9.5 M11,4.5 H14` |
   | `IconService` | `M8,1.8 L13.4,4.9 V11.1 L8,14.2 L2.6,11.1 V4.9 Z M8,5.8 A2.2,2.2 0 1 1 7.99,5.8 Z` |
   | `IconPlay` | `M5,3 L12.5,8 L5,13 Z` |
   | `IconStop` | `M4,4 H12 V12 H4 Z` |
   | `IconPower` | `M8,1.8 V7.5 M4.4,4.2 A5.2,5.2 0 1 0 11.6,4.2` |
   | `IconPlus` | `M8,3 V13 M3,8 H13` |
   | `IconCopy` | `M5.5,5.5 H13.5 V13.5 H5.5 Z M3,10.5 H2.5 V2.5 H10.5 V3` |
   | `IconArrowUp` | `M8,13 V3 M4,7 L8,3 L12,7` |
   | `IconArrowDown` | `M8,3 V13 M4,9 L8,13 L12,9` |
   | `IconTrash` | `M2.5,4.5 H13.5 M6,4.5 V2.5 H10 V4.5 M4,4.5 L4.7,13.5 H11.3 L12,4.5 M6.7,7 V11 M9.3,7 V11` |
   | `IconFolder` | `M1.5,12.5 V3.5 H6 L7.5,5.5 H14.5 V12.5 Z` |
   | `IconSave` | `M2.5,2.5 H11 L13.5,5 V13.5 H2.5 Z M5,2.5 V6 H10 V2.5 M4.5,13.5 V9.5 H11.5 V13.5` |
   | `IconRefresh` | `M13.5,8 A5.5,5.5 0 1 1 11.9,4.1 M12,1.5 V4.2 H9.3` |
   | `IconCheck` | `M3,8.5 L6.5,12 L13,4.5` |
   | `IconExport` | `M8,2 V10 M4.5,6.5 L8,10 L11.5,6.5 M2.5,11.5 V13.5 H13.5 V11.5` |
   | `IconBolt` | `M9,1.5 L3.5,9 H7.5 L7,14.5 L12.5,7 H8.5 Z` |
   | `IconPlug` | `M5.5,1.5 V5 M10.5,1.5 V5 M3.5,5 H12.5 V7.5 A4.5,4.5 0 0 1 3.5,7.5 Z M8,12 V14.5` |
   | `IconWarning` | `M8,2 L14.5,13.5 H1.5 Z M8,6.5 V9.5 M8,11.4 V11.6` |
   | `IconFit` | `M2.5,6 V2.5 H6 M10,2.5 H13.5 V6 M13.5,10 V13.5 H10 M6,13.5 H2.5 V10` |

3. Base `Button` template (`Actions.xaml`): background border (`RadiusControl`), an `Interaction` overlay border with `Background=HoverOverlay` and `Opacity="0"`, and content laid out as:
   ```xml
   <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}">
     <Path x:Name="Glyph" Width="16" Height="16" Stretch="None" Margin="0,0,8,0" VerticalAlignment="Center"
           Data="{Binding (c:Icon.Geometry), RelativeSource={RelativeSource TemplatedParent}}"
           Stroke="{TemplateBinding Foreground}" StrokeThickness="1.5"
           StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" />
     <ContentPresenter x:Name="Caption" VerticalAlignment="Center" RecognizesAccessKey="True" />
   </StackPanel>
   ```
   Triggers: `c:Icon.Geometry` = `{x:Null}` → collapse `Glyph`; `HasContent` false (icon-only) → `Glyph` margin 0. Hover: animate `Interaction.Opacity` to 1 over 0.1 s in `Trigger.EnterActions`, back to 0 over 0.15 s in `ExitActions` (literal durations; never `DynamicResource` inside a Storyboard). Pressed: `Interaction.Background = PressedOverlay`, opacity 1.
4. Variants (all `BasedOn` the base):
   - **default (secondary):** bg `SurfaceRaised`, border `Line`, ink `Ink`, weight Medium. Disabled: bg `Transparent`, border `Line`, ink `DisabledInk`.
   - **`PrimaryButton`:** bg/border `Action`, ink `ActionInk`, SemiBold; hover bg `ActionHover`; pressed `ActionPressed`. Disabled = same as default disabled.
   - **`GhostButton`** (new): transparent bg and border, ink `Muted` → `Ink` on hover. For Discard, Reset view, row-level actions.
   - **`OffButton`:** bg `FaultSurface`, border `FaultLine`, ink `Fault`, SemiBold (tonal). Add **`DangerButton`** (new): bg `Danger`, ink `DangerInk`, hover `DangerHover`.
   - **`OnButton`** (new): bg `ReadySoft`, border and ink `Ready`, SemiBold, `MinHeight` 44.
5. `DisclosureToggle`: `RadiusControl`, same hover fade; rotate the chevron with a 0.12 s `DoubleAnimation` on a named `RotateTransform`.
6. Assign icons (Content strings unchanged): Output Off → `IconStop` (delete its custom `ContentTemplate`); Connect → `IconPlug`; Refresh ports / Refresh / Read instrument / Read service settings / Read → `IconRefresh`; Apply changes / Apply… → `IconCheck`; Output On → `IconPower`; Start recording → `IconRecordings`; Stop recording / Stop run / Stop → `IconStop`; Run sequence / Start capture / Arm → `IconPlay`; Fire trigger → `IconBolt`; Open / Open folder → `IconFolder`; Save / Save preset → `IconSave`; Export / Export capture → `IconExport`; Add step → `IconPlus`; Duplicate / Copy all → `IconCopy`; Move up/down → arrows; Delete → `IconTrash`; Reset view → `IconFit`.
7. Hierarchy rule: at most one `PrimaryButton` per card. `Discard`, `Reset view`, `Validate`, `Move up/down`, `Duplicate`, `Delete`, `Copy all` become `GhostButton`.

Accept: disabled vs enabled vs primary are distinguishable at a glance in `01-disconnected.png` and `04-pending-draft.png`; all harness clicks still pass.

### WP4 — Inputs, toggles, popups, lists, tables (D5, D7, D12, D19)

Files: `Themes/Inputs.xaml`, `Themes/Collections.xaml`, `Themes/Workspace.xaml`, new `Themes/Feedback.xaml`, `Features/Service/ServiceView.xaml` (remove local `PasswordBox` style).

- **TextBox / ComboBox / PasswordBox:** bg `Well`, border `ControlLine`, `RadiusControl`; hover border `Muted`; focus border `Action` 1 px plus an outer 2 px ring (extra `Border` in the template, `BorderBrush=Action`, `Opacity=.35`, `Margin=-2`, `CornerRadius=8`, shown on keyboard focus). Global `PasswordBox` style with a full template (`PART_ContentHost` ScrollViewer) matching `TextBox`, keeping `MinWidth 112`, `MaxLength 8`.
- **ComboBox popup and ToolTip:** container bg `SurfaceRaised`, border `Line`, radius 8, `Margin="8"` with `DropShadowEffect` (BlurRadius 16, ShadowDepth 4, Direction 270, Opacity .45, black). ToolTip: ink `Ink`, 12 px, padding `8,5`, `MaxWidth` 360 with wrapping. Add a matching `ContextMenu`/`MenuItem` style (bg `SurfaceRaised`, hover `HoverOverlay`).
- **CheckBox:** box 18 px, radius 4; checked fill `Action`, check stroke `ActionInk`.
- **`ToggleSwitch`** (new keyed `CheckBox` style): 36×20 track, radius 10, 14 px thumb; off = track `SurfaceRaised` with `ControlLine` border and `Muted` thumb; on = track `Action`, thumb `ActionInk` shifted 16 px (0.12 s `DoubleAnimation` on a `TranslateTransform.X`). Use for `Follow live`, `Display on`, `Enable fault output`, `Close contacts`, `Overcurrent protection`, `Rearm after each trigger`. Acknowledgement checkboxes and grid cells stay checkboxes.
- **`Segmented`** (new keyed `ListBox` + item style): horizontal, container bg `Well`, border `Line`, radius 8, padding 2; item padding `12,5`, radius 6, `Muted`; selected item bg `SurfaceRaised`, ink `Ink`, Medium.
- **Default `TabControl`/`TabItem`** (inner tabs, i.e. Capture): transparent strip, no folder shape; selected = `Ink` + 2 px `Action` underline; others `Muted`. Keep the WP1 fix.
- **`HeaderlessTabs`** (new keyed `TabControl` style): template is only `<ContentPresenter x:Name="PART_SelectedContentHost" ContentSource="SelectedContent" />` (used by the shell in WP5).
- **ScrollBar:** 8 px, transparent track, thumb `ControlLine` at 60 % → `Muted` on hover, radius 4.
- **DataGrid:** bg transparent, no alternating color, row height 40, horizontal lines `Line`; column headers transparent, 12 px Medium `Muted`, bottom border `Line`; add a `DataGridRow` style: hover bg `HoverOverlay`, selected bg `Selected`. Cell focus border `Action`.
- **ListBoxItem:** radius 6, hover `HoverOverlay`, selected `Selected`.
- **`Feedback.xaml`:** `ProgressBar` (4 px track `Line`, fill `Action`, radius 2) and `Chip` styles: a `Border` style (radius 11, padding `8,2`, 12 px Medium text) with variants `ChipNeutral` (bg `SurfaceRaised`, ink `Muted`), `ChipReady` (`ReadySoft`/`Ready`), `ChipWarning` (`WarningSoft`/`Warning`), `ChipFault` (`FaultSurface`/`Fault`), `ChipAccent` (`ActionSoft`/`Action`). Restyle `Controls/StatusBadge.xaml` to use tinted backgrounds the same way.

Accept: hover any control in the running app (`tools\dev.ps1 -Simulator`) → dark tooltip; combo popups float with a shadow; Service passcode boxes match text boxes; `Binding errors: 0`.

### WP5 — Shell: title bar, nav rail, header, status bar (D6, D7, D8, D9, D20)

Files: `Shell/MainWindow.xaml`, `Shell/MainWindow.xaml.cs`, `Themes/Workspace.xaml`, `Shell/ConnectionPanel.xaml`.

1. **Dark native title bar** (keeps snap/resize behavior):
   ```csharp
   [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
   protected override void OnSourceInitialized(EventArgs e)
   {
       base.OnSourceInitialized(e);
       if (SystemParameters.HighContrast) return;
       IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
       int on = 1; DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));           // immersive dark mode
       int Rgb(string key) { var c = ((SolidColorBrush)FindResource(key)).Color; return c.R | c.G << 8 | c.B << 16; }
       int caption = Rgb("Well"); DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));   // caption color (Windows 11)
       int border = Rgb("Line"); DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));     // border color (Windows 11)
   }
   ```
   Ignore return codes (older Windows rejects 34/35).
2. **Layout.** Root grid: column 0 = 76 px rail, column 1 = main; row 0 = `*`, row 1 = status bar spanning both columns.
   - **Rail** (`Border` bg `Well`, right border `Line`): top = 32 px logo (vector copy of the app icon inside a `Viewbox`: 64×64 canvas, rounded rect radius 14 fill `Surface` stroke `Line`; `Path Data="M10,39 H21 V23 H43 V39 H54"` stroke `Voltage` thickness 4; `Path Data="M10,49.5 H54"` stroke `Current` thickness 3), margin `0,14,0,10`. Below it:
     ```xml
     <ListBox x:Name="WorkspaceNav" Style="{StaticResource NavRail}" AutomationProperties.Name="Workspaces"
              SelectedIndex="{Binding SelectedIndex, ElementName=WorkspaceTabs, Mode=TwoWay}">
       <ListBoxItem Content="Monitor" c:Icon.Geometry="{StaticResource IconMonitor}" />
       <ListBoxItem Content="Sequences" c:Icon.Geometry="{StaticResource IconSequence}" />
       <ListBoxItem Content="Recordings" c:Icon.Geometry="{StaticResource IconRecordings}" />
       <ListBoxItem Content="Diagnostics" c:Icon.Geometry="{StaticResource IconDiagnostics}" />
       <ListBoxItem Content="Capture" c:Icon.Geometry="{StaticResource IconCapture}" />
       <ListBoxItem Content="Setup" ToolTip="Instrument Setup" AutomationProperties.Name="Instrument Setup" c:Icon.Geometry="{StaticResource IconSetup}" />
       <ListBoxItem Content="Service" c:Icon.Geometry="{StaticResource IconService}" />
     </ListBox>
     ```
     `NavRail` style (in `Workspace.xaml`): transparent, no border/scrollbars. Item template: 68×58 cell, radius 8, margin `4,2`; 20 px icon (`Path` in a `Viewbox`, stroke bound to item foreground) above an 11 px label, both centered. Normal `Muted`; hover bg `HoverOverlay` + `Ink`; selected bg `ActionSoft`, icon and label `Action`, label Medium, plus a 3×20 px `Action` bar (radius 2) at the left edge. Focus visual = `FocusRing`.
   - **Main column** rows: header, `ConnectionPanel`, `InstrumentDisplay`, `TabControl x:Name="WorkspaceTabs" Style="{StaticResource HeaderlessTabs}"` (same seven `TabItem`s, same order, keep `Header` text and `SelectionChanged`). Outer margins 20 → keep; gaps 12.
3. **Header** (one row, height ~52): left = "66332A Studio" (18 SemiBold) with `IdentityLabel` caption beneath; right = connection pill + Output Off.
   - **Connection pill:** make `ConnectionToggle` itself the status display. Keep `x:Name`, add `AutomationProperties.Name="Connection"`, keep `ToolTip="{Binding ProfileLabel}"`. Content = 8 px dot + `{Binding ConnectionLabel}`; `DisclosureToggle` supplies the chevron. Pill radius 18, bg `SurfaceRaised`. Move the existing `ConnectionBadge` data triggers onto it so dot and text are `Muted` / `Warning` (Connecting) / `Ready` (Ready) / `Fault` (Lost). Remove the separate `StatusBadge` from the header.
   - **Output Off:** `Content="Output Off"`, `c:Icon.Geometry=IconStop`, `MinWidth` 136, `MinHeight` 40. Style = `OffButton` (tonal) by default with a `DataTrigger` on `OutputStatus == OutputState.On` switching background to `Danger`, foreground `DangerInk`, border `Danger` — the stop control goes solid red exactly when the output is live. Instant, no animation.
4. **Status bar:** height 32, bg `Well`, top border `Line`. Left: 8 px state dot (same color triggers as the pill; `Fault` when `CloseUnverified`) + `Message`. Right: `Footer` as `Caption`. Keep the existing `StatusFooter` fault triggers and the "Close with output unverified" button (`DangerButton`).
5. **ConnectionPanel:** `PanelFrame`, padding 16. Row 1: Port and Baud with labels **above** the fields (13 px `FieldLabel`), actions right-aligned (`Connect` primary, `Disconnect` default, `Refresh ports` ghost). Expander content: a 4-column grid (Parity, Flow control, Cable label, Chassis label — labels above), DTR/RTS as two checkboxes on one line, then the two read-only detail boxes.

Accept: every PNG shows the rail with one highlighted item matching the workspace; no top tab strip; `Output Off` is solid red in `03-`, `04-`, `08-`, `12-` (output on) and tonal in `02-`, `09-`, `10-`; title bar is dark on screen; at 1120×680 nothing is clipped.

### WP6 — Instrument display (D3, D6)

Files: new `Controls/SegmentDisplay.cs`, `Controls/Readout.xaml(.cs)`, `Shell/InstrumentDisplay.xaml`, `Shell/ShellViewModel.cs`.

1. **`SegmentDisplay`** — fixed-width digit cells over dim "8"s. Glyph right side bearings in `Technology.ttf` are uniform (34–38/1000 em), so right-aligning each glyph in an "8"-wide cell lines up with the ghost segments.
   ```csharp
   public sealed class SegmentDisplay : FrameworkElement
   {
       private static DependencyProperty Register(string name, Type type, object value) => DependencyProperty.Register(name, type, typeof(SegmentDisplay),
           new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
       public static readonly DependencyProperty TextProperty = Register(nameof(Text), typeof(string), "");
       public static readonly DependencyProperty PlaceholderProperty = Register(nameof(Placeholder), typeof(string), "-.----");
       public static readonly DependencyProperty DigitSizeProperty = Register(nameof(DigitSize), typeof(double), 64.0);
       public static readonly DependencyProperty MinIntegerDigitsProperty = Register(nameof(MinIntegerDigits), typeof(int), 1);
       public static readonly DependencyProperty ForegroundProperty = Register(nameof(Foreground), typeof(Brush), Brushes.White);
       public static readonly DependencyProperty IdleBrushProperty = Register(nameof(IdleBrush), typeof(Brush), Brushes.Gray);
       public static readonly DependencyProperty FontFamilyProperty = Register(nameof(FontFamily), typeof(FontFamily), new FontFamily("Consolas"));
       // CLR wrappers for each property omitted here; add them.

       private readonly record struct Cell(string Glyph, double Width, bool HasGhost);
       private static bool IsCell(char c) => char.IsDigit(c) || c == '-';
       private bool Live => !string.IsNullOrEmpty(Text) && Text != "—";
       private FormattedText Glyph(string text, Brush brush) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
           new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), DigitSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

       private List<Cell> Cells(out double height)
       {
           var eight = Glyph("8", Foreground); height = eight.Height;
           double digit = eight.WidthIncludingTrailingWhitespace;
           string shown = Live ? Text : Placeholder;
           var cells = new List<Cell>();
           for (int i = shown.TakeWhile(IsCell).Count(); i < MinIntegerDigits; i++) cells.Add(new(" ", digit, true));   // unlit leading digits
           foreach (char c in shown)
               cells.Add(IsCell(c) ? new(c.ToString(), digit, true) : new(c.ToString(), Glyph(c.ToString(), Foreground).WidthIncludingTrailingWhitespace, false));
           return cells;
       }
       protected override Size MeasureOverride(Size available) { var cells = Cells(out double height); return new(cells.Sum(c => c.Width), height); }
       protected override void OnRender(DrawingContext dc)
       {
           var ghost = Foreground.Clone(); ghost.Opacity = SystemParameters.HighContrast ? 0 : 0.07; ghost.Freeze();
           Brush lit = Live ? Foreground : IdleBrush;
           double x = 0;
           foreach (var cell in Cells(out _))
           {
               if (cell.HasGhost) dc.DrawText(Glyph("8", ghost), new Point(x, 0));
               if (cell.Glyph != " ")
               {
                   var glyph = Glyph(cell.Glyph, lit);
                   double free = cell.Width - glyph.WidthIncludingTrailingWhitespace;
                   dc.DrawText(glyph, new Point(x + (cell.Glyph == "-" ? free / 2 : free), 0));   // dashes centered, digits right-aligned
               }
               x += cell.Width;
           }
       }
   }
   ```
   In the constructor, unless `SystemParameters.HighContrast`, bind a glow: after `Foreground` is known (override `OnPropertyChanged` for `ForegroundProperty`), set `Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 18, Opacity = 0.45, Color = ((SolidColorBrush)Foreground).Color }` when `Foreground` is a `SolidColorBrush`; set `Effect = null` when not `Live`.
2. **`Readout`**: add `Placeholder` (string) and `MinIntegerDigits` (int) dependency properties and pass them through. Replace the digit `Run` with `<c:SegmentDisplay Text=… DigitSize=… Foreground="{Binding Accent}" IdleBrush="{DynamicResource DisabledInk}" FontFamily="{StaticResource MeasurementFont}" />`, followed by the unit as a separate `TextBlock` (Plex Sans Medium 20, accent color, `Opacity .8`, bottom-aligned, margin `8,0,0,6`). Label row: 8 px accent dot + label (13 Medium `Muted`). Detail row unchanged.
3. **`InstrumentDisplay`**: sizes — Voltage/Current `DigitSize` 64 (compact 44), Power 44 (compact 32). `MinIntegerDigits`: Voltage 2, Current 1, Power 3. `Placeholder`: `-.----`, `-.-----`, `-.----`. Container: `RadiusPanel`, border `Line`, background a vertical `LinearGradientBrush` `#0B1017` → `#06090D` (key `+DisplayWell`; high contrast → `WindowBrush`). Give each channel cell a 2 px top edge in its channel color, visible only while `ConnectionStatus == Ready`.
4. **Annunciators** replace the caption under Power. Add to `ShellViewModel` and raise in `Refresh()`: `public string RegulationMode => Session.Snapshot?.Status.RegulationMode ?? "";`, `public bool HasFault => Session.Snapshot?.Status.HasFault == true;`, `public bool IsSimulator => Session.SourceKind == "Simulator" && Session.State != ConnectionState.Disconnected;`. Add a fourth column (Auto width, left divider) with a 2×2 grid of fixed-position chips, each dim (`ChipNeutral` at 45 % opacity) until lit:
   - `OUTPUT ON` — lit `ChipWarning` when `OutputStatus == On`; text switches to `OUTPUT OFF` (lit `ChipNeutral`, full opacity) when Off and `OUTPUT ?` (`ChipFault`) when Unknown.
   - `CV` / `CC` — one chip showing `RegulationMode` when output is on, lit `ChipAccent`; dim "CV" otherwise.
   - `PROT` — lit `ChipFault` when `HasFault`.
   - `SIM` — lit `ChipAccent` when `IsSimulator`.
   Keep `OutputLabel` as `AutomationProperties.Name` on the chip group so screen readers still get "Output on / CV".
5. Age strip: 6 px dot (`Ready` when the text starts with "Updated", `Warning` otherwise — bind via a `DataTrigger` on a new `bool IsFresh => Available;` property raised in `RefreshTelemetry()`), then `AgeLabel`; `LastKnown` right-aligned.

Accept: in `03-output-on.png` digits are large, glowing, with faint "8" ghosts including one unlit leading voltage digit; "5.0001" → "5.0000" does not move the unit; `01-disconnected.png` shows grey `-.----` dashes with no glow; `OUTPUT ON` chip is lit amber in `03-` and `OUTPUT ?` red in `11-lost-unknown.png`.

### WP7 — Charts (D10)

Files: new `Controls/PlotTheme.cs`, `Controls/TrendPanel.xaml(.cs)`, `Features/Monitor/MonitorView.xaml`, `Features/Capture/CaptureView.xaml.cs`.

1. **`PlotTheme`** (shared; delete the duplicated styling in both code-behinds). All members below exist in the pinned ScottPlot 5.0.55; if one line fails to compile, drop that line — do not upgrade the package.
   ```csharp
   internal static class PlotTheme
   {
       private const string FontName = "IBM Plex Sans";
       private static bool? fontReady;
       public static ScottPlot.Color Color(FrameworkElement owner, string key)
       { var c = ((SolidColorBrush)owner.FindResource(key)).Color; return new(c.R, c.G, c.B, c.A); }
       public static void Apply(ScottPlot.Plot plot, FrameworkElement owner)
       {
           if (fontReady is null)
           {
               string file = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "IBMPlexSans-Regular.ttf");
               fontReady = File.Exists(file);
               if (fontReady == true) ScottPlot.Fonts.AddFontFile(FontName, file);
           }
           plot.FigureBackground.Color = plot.DataBackground.Color = Color(owner, "Well");
           plot.Axes.Color(Color(owner, "Muted"));
           plot.Axes.FrameColor(Color(owner, "Line"));
           plot.Axes.Top.FrameLineStyle.Width = 0; plot.Axes.Right.FrameLineStyle.Width = 0;
           plot.Grid.MajorLineColor = Color(owner, "Line").WithAlpha(.6);
           plot.Axes.Bottom.MinorTickStyle.Length = 0; plot.Axes.Left.MinorTickStyle.Length = 0;
           plot.Axes.Bottom.MajorTickStyle.Length = 4; plot.Axes.Left.MajorTickStyle.Length = 4;
           if (fontReady == true) plot.Font.Set(FontName);
           plot.Axes.Bottom.TickLabelStyle.FontSize = 12; plot.Axes.Left.TickLabelStyle.FontSize = 12;
       }
   }
   ```
2. `TrendPanel.Draw`: line width 2; when the data has no `NaN` (`!y.Any(double.IsNaN)`) set `line.FillY = true; line.FillYColor = channel.WithAlpha(.10);`; if the last sample is finite add `plot.Add.Marker(x[^1], y[^1])` (filled circle, size 7, channel color); call `plot.Axes.Margins(0, .15)` **before** the existing `AutoScaleY()` so traces don't touch the frame. Setpoint line alpha .5, cursor line `Ink` alpha .5.
3. `TrendPanel.xaml`: channel titles become a 6 px colored dot + "Voltage · V" (12 Medium `Muted`). Remove the "Channels" `Expander`; keep `CursorText` in a slim 28 px bar at the bottom (mono 12, `Muted`).
4. `MonitorView.xaml` Trends header: title; then toggle chips for `ShowVoltage`, `ShowCurrent`, `ShowPower` (a keyed `ToggleButton` style `ChipToggle`: pill, dot in channel color, dim when off) and a `Freeze cursor` chip; then range as `<ListBox Style="{StaticResource Segmented}" ItemsSource="{Binding PlotRanges}" SelectedItem="{Binding PlotMinutes}">` with the existing `{0} min` item template; `Follow live` as `ToggleSwitch`; `Reset view` as icon `GhostButton`. Keep all `AutomationProperties.Name`s.
5. `CaptureView.Draw`: call `PlotTheme.Apply`, line width 2, same fill rule.

Accept: `03-output-on.png` — no top/right frame, Plex tick labels, soft fill under each trace, dot at the latest sample; `12-waveform` uses the same look.

### WP8 — Monitor and manual control (D6, D17)

Files: `Features/Monitor/ManualControlPanel.xaml`, `Controls/NumericUnitEditor.xaml`, `Features/Monitor/MonitorView.xaml`.

- Panel padding 16; title row = "Manual control" + a `ChipWarning` "Pending" shown when `IsDirty` (keep the `DraftStatus` text for validation messages).
- `NumericUnitEditor`: field column 136, height 40, mono 15; unit in `Muted` 13.
- Buttons row: `Apply changes` (primary, `IconCheck`) + `Discard` (ghost).
- Output block: full-width `Output On` using `OnButton` + `IconPower`; `OutputOnReason` moves **below** it as a caption (it is cramped beside the button today).
- Protections / Presets expanders: 1 px `Line` divider above each, content padding `0,8,0,12`; `Apply protections` default, `Clear fault` ghost; `Overcurrent protection` → `ToggleSwitch`.
- Monitor footer: `Start recording` default with `IconRecordings`; while recording, the `StatusBadge` shows a red dot (`Fault`) + label; `Poll (s)` unchanged.

Accept: `04-pending-draft.png` shows the Pending chip and an indigo Apply button; `Output On` is the widest, green-tinted control in the panel.

### WP9 — Sequences (D15)

Files: `Features/Sequences/SequenceView.xaml`, `Features/Sequences/SequencePreview.cs`, `Features/Sequences/SequenceViewModel.cs`.

- Toolbar card (padding 16): row 1 = Name (fills) · Repeats (72) · `Validate` (ghost) · `Run sequence` (primary, `IconPlay`) · `Stop run` (`DangerButton` when enabled, `IconStop`). Row 2 = file path · `Open` (`IconFolder`) · `Save` (`IconSave`). Labels above fields.
- Remove the Preview `Expander`: show the summary line (`Preview` text, plus the amber "Unsaved changes" as `ChipWarning`) and the `SequencePreview` strip (height 64) always visible above the grid.
- `SequencePreview`: also invalidate on `ProgressText` changes; draw thin `Line`-colored step boundaries; fill the active step's span (`vm.Rows[i].IsActive`) with `ActionSoft`; stroke width 2.
- Step grid: active row keeps the `Ready` left bar; row text Regular; step toolbar → `Add step` default with `IconPlus`, the rest ghost with icons, `Delete` ghost with `Fault` foreground.
- Run status card (replace the two bare text lines): `PanelFrame`, padding `16,10`; `ProgressText`, a `ProgressBar` bound to a new `[ObservableProperty] private double holdFraction;` set in `Tick()` — when `p.Phase == "Holding"` and the active row's `HoldSeconds` parses to `h > 0`: `1 - Math.Clamp((p.HoldDeadline - clock.Seconds) / h, 0, 1)`, else 0 — and `RunResult` as caption.

Accept: `08-active-sequence.png` shows the preview strip with the active step highlighted, a progress bar, and a solid red `Stop run`.

### WP10 — Recordings and Diagnostics (D14)

Files: `Features/Recordings/RecordingsView.xaml`, `Features/Diagnostics/DiagnosticsView.xaml`, `Features/Diagnostics/DiagnosticsRegistersView.xaml`, new `Controls/ContainsConverter.cs`.

- Recordings: "Save to" card with label above, `Refresh` ghost, `Open folder` default. Table card: `PanelFrame` with `Background=Well`, padding `1,1,1,6` (transparent headers from WP4 stop corners poking out). Outcome, Shutdown and Source cells become chips via `DataGridTemplateColumn`: Outcome → `ChipReady` if it starts with "Completed", `ChipNeutral` if "Cancelled", `ChipFault` otherwise; Shutdown → "Verified off" `ChipReady`, "Unverified" `ChipFault`, anything else `ChipNeutral`; Source → `ChipAccent` for Simulator, `ChipNeutral` otherwise. Text values unchanged.
- `ContainsConverter` (`IValueConverter`, `ConverterParameter` = substring, returns bool) for string-item styling.
- Diagnostics lists: both mono 12.5, item padding `10,3`, no horizontal scrollbar (`TextTrimming` + tooltip with full text). Protocol items containing `"  > "` (sent) are `Action`-colored, others `Ink`. Event items containing `"failure:"` are `Fault`-colored. Card titles 16 with an item-count caption on the right.
- Registers: `PanelFrame` padding 16; mask grid keeps its columns; all text boxes mono; `Apply masks` default, others ghost; the acknowledgement checkbox uses the `AckCallout` style from WP11.
- Export bar: label above, `Copy all` ghost, `Export` default.

Accept: `09-recordings.png` shows colored chips and hover-ready rows with no square corners; `10-diagnostics.png` shows sent commands in indigo.

### WP11 — Capture, Instrument Setup, Service forms (D13, D21)

Files: `Features/Capture/CaptureView.xaml(.cs)`, `Features/Capture/TransientView.xaml`, `Features/InstrumentSetup/InstrumentSetupView.xaml`, `Features/Service/ServiceView.xaml`, `Themes/Feedback.xaml`.

One form pattern for all three views (preserve every binding, command, `x:Name`, `Click` handler and caption):
- Page header: `PageTitle` + readback caption on the left, the page's read button (default, `IconRefresh`) on the right.
- Each card: `PanelFrame`, padding 16; `Title` (16) + readback as `Caption`; body is a `Grid` with `Grid.IsSharedSizeScope="True"` on the card and a label column `SharedSizeGroup="FormLabel"` so labels align; rows 8 px apart; actions in a bottom row, left-aligned, 8 px gaps. Replace the label/control `WrapPanel`s with grid rows.
- `AckCallout` (new `Border` style in `Feedback.xaml`): bg `WarningSoft`, 3 px left border `Warning`, radius 6, padding `12,8`; contains the existing acknowledgement `CheckBox` unchanged. Use for all "I have…" checkboxes and the remote-lockout confirmation.
- Risky actions (`Reset instrument`, `Recall isolated`, `Enter remote lockout`, `Enter Compatibility`, `Set passcode`): default button with `Fault` foreground and `FaultLine` border (keyed style `RiskButton`, tonal — not solid red, which is reserved for Output Off/Stop).
- Capture: left settings card keeps the 2-column field grid with labels above; action bar = start-mode combo · Wait · `Start capture` (primary) · `Fire trigger` · `Stop` (ghost). Replace the `StatisticsText` line with six stat tiles (label caption above a mono 15 value: DC, RMS, Min, Max, Pulse low, Pulse high) — add six named `TextBlock`s set in `Draw()`. Keep `StatisticsText` and its existing assignment but set `Visibility="Collapsed"`, and in `Draw()` copy the same string to `AutomationProperties.Name` on the tile row so screen readers still get the full statistics. `Export capture` default + path caption.
- Calibration prompt box: bg `Well`, border `Action`, radius 8.

Accept: in `13-` and `14-` PNGs labels share one left edge per card, acknowledgements are amber callouts, and `Read instrument`, `Read service settings`, `Run self-test` are still clicked by the harness.

### WP12 — Motion and empty states (D11, D16)

Files: new `Controls/Motion.cs`, `App.xaml.cs`, `Shell/MainWindow.xaml(.cs)`, `Controls/StatusBadge.xaml(.cs)`, `Features/Monitor/MonitorView.xaml`, `Themes/Feedback.xaml`.

1. Helper:
   ```csharp
   public static class Motion
   {
       public static bool Enabled { get; set; } = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
       public static void Reveal(FrameworkElement element, double offsetY = 8, int milliseconds = 160)
       {
           if (!Enabled) return;
           var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
           var shift = new TranslateTransform(0, offsetY); element.RenderTransform = shift;
           var time = TimeSpan.FromMilliseconds(milliseconds);
           element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, time) { EasingFunction = ease });
           shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0, time) { EasingFunction = ease });
       }
   }
   ```
2. `App.StartAsync`: `if (Argument(e.Args, "--capture-artifacts") is not null) Motion.Enabled = false;` **before** creating `MainWindow` (keeps harness screenshots deterministic).
3. Use it: `WorkspaceChanged` → `if (args.Source == WorkspaceTabs && WorkspaceTabs.SelectedContent is FrameworkElement view) Motion.Reveal(view);`; `ConnectionToggle.Checked` → reveal the `ConnectionPanel` (give it an `x:Name`); expanders via one class handler in `App.OnStartup`:
   ```csharp
   EventManager.RegisterClassHandler(typeof(Expander), Expander.ExpandedEvent, new RoutedEventHandler((sender, _) =>
   { if (sender is Expander x && x.Template?.FindName("ExpandSite", x) is FrameworkElement site) Motion.Reveal(site, 6, 140); }));
   ```
4. Live dot: in the connection pill, a second ellipse behind the dot animates `Opacity` .5 → 0 and scale 1 → 2.4 over 1.8 s, repeating. Start and stop it in `MainWindow.UpdateConnectionPanel` (which already runs on every `ConnectionStatus` change, plus once after `InitializeComponent`): run only when `ConnectionStatus == Ready && Motion.Enabled`, otherwise `BeginAnimation(property, null)` to clear it. This is the single looping animation in the app.
5. `EmptyState` style (`Feedback.xaml`): centered stack — 28 px icon in `Muted`, 15 px Medium title, caption, max width 320. Apply to: the trend area when `ConnectionStatus == Disconnected` (overlay on `Well` covering the plots: `IconPlug`, "Not connected", "Open Connection, choose the serial adapter, then Connect."); Recordings ("No recordings yet"); both Diagnostics lists.

Accept: `01-disconnected.png` shows the empty state instead of two −10…10 grids; on screen, switching workspaces fades/slides in ~160 ms; with Windows "Animation effects" off nothing animates.

### WP13 — Specification and evidence

Files: `plans/02-experience-and-visual-design.md`, `plans/08-decisions-risks-and-sources.md`, `README.md`.

Update the token table, typography, geometry and motion sections of plan 02 to the values above and record the decisions that changed (indigo accent; seven-segment readout with ghost segments and glow; left rail; 12 px panels; one looping live-dot animation; state-reactive Output Off). Regenerate `artifacts/ui-evidence/` with `tools\verify-ui.ps1`.

---

## Verification (end to end)

1. `tools\test.ps1` — all existing tests pass (no view-model behavior changed).
2. `tools\verify-ui.ps1` — exits 0; `verification.txt` ends with `Binding errors: 0`; the new font `Require` passes; all 14 PNGs regenerate.
3. Review the PNGs against each WP's acceptance bullets, especially `01`, `03`, `04`, `05-compact-1120x680`, `07-…-at-150`, `08`, `11-lost-unknown`.
4. Manual pass with `tools\dev.ps1 -Simulator`: hover/focus/keyboard-tab through every workspace; open every combo and tooltip; resize to 1000×620; check the dark title bar; toggle Windows high contrast and "Animation effects" off and relaunch.
5. `tools\publish.ps1` — confirm `Assets\Fonts\IBMPlexSans-Regular.ttf` is in the output and chart labels use Plex.

## Font note (D22)

The app is not sold, and the Technology seven-segment font stays. Nobody has read that font's license terms; the file itself only says "All rights reserved". That is not a concern for private use. It becomes one if the published package is handed to other people or the repository is made public, because either one redistributes the font file. At that point, read the font's terms or replace it with an openly licensed seven-segment font. `SegmentDisplay` (WP6) measures glyphs at runtime, so a replacement only needs the new file in the project and the `MeasurementFont` resource in `Themes/Palette.xaml` pointed at it. One candidate is DSEG7 (SIL Open Font License 1.1, github.com/keshikan/DSEG, latest stable release v0.46). Its decimal point has zero width, so check the readout spacing after any swap.
