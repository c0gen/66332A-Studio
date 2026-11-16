# 10: UI refinement pass 2 (handoff spec)

## Context

[Plan 09](09-ui-polish-pass.md) is implemented and the app now reads as a designed product: icon rail, indigo accent, DSEG7 readout with ghost segments, themed charts, chips, empty states. This second pass reviews the **current** build (the 15 screenshots regenerated on November 15, 2026 at 23:30 and the current XAML) against the same goal: **clean, commercial, professional, very modern, a little flashy**.

What still falls short, in four groups:

1. **State legibility.** Output state and a lost connection are still carried by 12 px text.
2. **Chart noise.** Twenty time labels on one axis, heavy event lines, raw-second units.
3. **Leftover inconsistencies.** Duplicated app name, mixed title sizes, muddy tints, read-only fields that look editable, a layout jump in the manual panel.
4. **Flatness.** Every surface is one flat tone with a 1 px line. Only the digits have any light in them.

**Status (November 16, 2026).** Specification only; no work package has been implemented yet. None of the code below has been compiled. Where this document and [plan 02](02-experience-and-visual-design.md) or [plan 09](09-ui-polish-pass.md) disagree, this document wins until WP10 folds the changes back into plan 02.

Evidence reviewed: `artifacts/ui-evidence/01`–`15` PNGs, `docs/images/simulator-monitor.png`, every XAML file and control under `src/Agilent66332A.Desktop/`, the three `Verification/DesktopVerification*.cs` files, `plans/02` and `plans/09`. ScottPlot member names below were checked against the pinned 5.0.55 assembly.

## What changed since plan 09 (read before starting)

Work from the current code, not from plan 09's description:

- The header has **one** output control, `OutputControlButton` (style `OutputToggleButton`, `Themes/Actions.xaml`). Its `Content` is `Output On` while `ShellViewModel.ShowOutputOn` is true, otherwise `Output Off`.
- The manual panel's primary button is `{Binding ApplyLabel}`: `Apply & turn on` or `Apply changes`. The panel has no Output On button.
- Connection settings are a modeless window, `Shell/ConnectionWindow.xaml`, hosting `ConnectionPanel`, opened by `ConnectionButton`. It has its own `Output Off` button.
- High-contrast mapping and the dark title bar live in `Controls/WindowAppearance.cs`.
- The harness is three partial files: `DesktopVerification.cs`, `.Connection.cs`, `.Service.cs`.

## Ground rules for the implementing model (read before every WP)

1. **Presentation only.** Do not change command logic, `CanExecute` rules, or safety gating. Add view-model members only where a WP gives the exact code; all of them are read-only.
2. **The harness finds controls by exact text.** Keep these as plain `Content` strings on visible buttons: `Output On`, `Output Off`, `Apply & turn on`, `Start recording`, `Stop recording`, `Stop run`, `Start capture`, `Export capture`, `Read instrument`, `Read service settings`, `Run self-test`, `Recall isolated`, `Enter Compatibility`, `Start procedure`, `Connect`, `Disconnect`, `Refresh ports`. Keep these `Expander.Header` strings: `Review memory and startup actions`, `Review language and Compatibility actions`. Keep both Service hyperlink texts. Add icons only through `c:Icon.Geometry`.
3. **Keep these names and parts:** `OutputControlButton`, `ConnectionButton`, `ConnectionPulse`, `WorkspaceNav`, `WorkspaceTabs` (a `c:WorkspaceTabControl` with seven `TabItem`s in the current order), `MeasurementDisplay`, `VoltagePlot`, `CurrentPlot`, `PowerPlot`, `VoltageRow`, `CurrentRow`, `PowerRow`, `NoChannels`, `CursorText`, `WaveformPlot`, `StatisticsText`, `StatisticsTiles`, `EmptyWaveform`, the six `…Stat` text blocks, `Passcode`, `NewPasscode`, `StepScroller`, `VersionText`, `CloseButton`, and the Expander template parts `HeaderSite` and `ExpandSite`. `ServiceView.Content` must stay the `ScrollViewer`.
4. **The output control must be visible and unclipped** in every workspace at 1000 × 620 and at every capture. `ConnectionWindow` keeps its `Output Off` button.
5. Keep every `AutomationProperties.*`, `Label Target=`, `ToolTip` and `LiveSetting`. Binding errors must stay **0**.
6. **Color meaning is fixed.** `Voltage` cyan and `Current` amber are channel colors only. Red (`Fault`, `Danger`) is only for faults and Output Off/Stop. `Warning` amber means energized or pending. `Action` indigo means primary action, selection, focus and activity.
7. Add every new brush key to `WindowAppearance.ApplyHighContrastPalette`. Gradients, glows and washes are off in high contrast.
8. No new NuGet packages. No modal dialogs. No animated numbers. **Output, fault and connection visuals change through setters and data triggers, never storyboards.** Any storyboard uses literal values and is gated on `c:Motion.Enabled` like the existing templates.
9. After each WP run `tools\verify-ui.ps1` (must print `Binding errors: 0`) and `tools\test.ps1`, then open the regenerated PNGs and check the WP's acceptance bullets.

---

## Deficiencies (prioritized)

| ID | Sev | Deficiency (evidence) | Fixed in |
|---|---|---|---|
| R1 | High | **Output state is still small, and the action reads like a status.** The only state text is a 12 px `OUTPUT ON` chip. With output off, the header shows a green-tinted `Output On` button that looks like a status lamp (`02-simulator-off.png`). | WP3 |
| R2 | High | **A lost connection looks almost like idle.** `11-lost-unknown.png` differs from `01-disconnected.png` only by a red pill, a 12 px chip and footer text. | WP3 |
| R3 | High | **Time axis is overcrowded.** Twenty labels nearly touch (`02`, `03`, `11`). | WP4 |
| R4 | High | **The app name appears twice** (title bar and header) while the header gives no page context. Setup and Service have page titles, the other five workspaces have none. The simulator shows no instrument identity. | WP2 |
| R5 | High | **Apply and Discard jump 24 px** when a draft becomes pending (`03` versus `04`), so the buttons move under the pointer. | WP5 |
| R6 | Med | Manual control card is half empty on tall windows (`02`). | WP5 |
| R7 | Med | Channel edge lines in the display float inside the padding and look unfinished (`02`). | WP3 |
| R8 | Med | Flat depth: every card has the same flat border, and the primary button is a flat fill. | WP1 |
| R9 | Med | Muddy soft tints. Amber callouts and chips read grey-olive (`13`, `14`); tonal red reads purple-grey (`11`). | WP1 |
| R10 | Med | First-run empty state has awkward copy ("Click Disconnected…") and no action (`01`). | WP6 |
| R11 | Med | A recording or sequence run is invisible outside its own workspace. | WP2 |
| R12 | Med | Status bar shows a lone dot when the message is empty (`02`, `09`, `10`). | WP2 |
| R13 | Med | Recordings show UTC while charts and logs show local time; `Completed / Complete` is a raw composite; `Unavailable` looks like an error (`09`). | WP8 |
| R14 | Med | Diagnostics: the registers expander floats bare on the background; timestamps have the same weight as the message (`10`). | WP8 |
| R15 | Med | Capture: time axis in raw seconds (`0.0002`), statistic tiles without units, card titles at page-title size (`12`). | WP7 |
| R16 | Med | Read-only fields look editable (`15`), and so do locked sequence fields during a run (`08`). | WP1 |
| R17 | Med | Event markers are full-height dotted lines at 55 % and bury the trace (`11`). | WP4 |
| R18 | Med | Sequence preview draws disconnected flat segments; the idle run card shows an empty progress bar (`08`). | WP9 |
| R19 | Low | Chip toggles look nearly the same on and off (`02`: Voltage versus Power estimate). | WP1 |
| R20 | Low | A first launch shows a red `OUTPUT ?` as if something had failed (`01`). | WP3 |
| R21 | Low | The empty Port box has no prompt (`15`). | WP6 |
| R22 | Low | About is a text-only button in an icon rail. | WP2 |
| R23 | Low | Chart title rows carry no data. | WP4 |
| R24 | Low | Evidence gaps: Output trigger tab, registers panel and a wide layout are never captured. | WP10 |
| R25 | Info | Plan 02 still describes a permanent Output Off button and a full-width Output On in the manual panel. The app ships one header control. | WP10 |

---

## Token changes (contrast computed, WCAG)

| Key | Value | Notes |
|---|---|---|
| `WarningSoft` | `#2F3130` → `#382F1B` | `Warning` text 8.4:1, `Ink` 11.7:1, `Muted` 6.3:1 |
| `FaultSurface` | `#312A34` → `#3A222B` | `Fault` text 6.6:1 |
| `ReadySoft` | `#213236` → `#1B3530` | `Ready` text 7.5:1 |
| `+PanelEdge` | vertical gradient `#33425A` → `#253140` at 35 % → `#253140` | card borders, lit from above |
| `+ActionFill` | vertical gradient `#979EFF` → `#7F87F5` | primary button at rest; `ActionInk` 6.0–7.8:1 |

`Danger` stays a flat fill: white on a lighter red drops below 4.5:1.

---

## Work packages

Do WP1–WP3 in order. WP4–WP9 touch separate areas and can follow in any order. WP10 is last.

### WP1 — Tokens, depth and shared styles (R8, R9, R16, R19)

Files: `Themes/Palette.xaml`, `Themes/Actions.xaml`, `Themes/Inputs.xaml`, `Themes/Feedback.xaml`, `Controls/WindowAppearance.cs`.

1. `Palette.xaml`: change the three soft tints from the token table and add:
   ```xml
   <LinearGradientBrush x:Key="PanelEdge" StartPoint="0,0" EndPoint="0,1">
     <GradientStop Color="#33425A" Offset="0" />
     <GradientStop Color="#253140" Offset="0.35" />
     <GradientStop Color="#253140" Offset="1" />
   </LinearGradientBrush>
   <LinearGradientBrush x:Key="ActionFill" StartPoint="0,0" EndPoint="0,1">
     <GradientStop Color="#979EFF" Offset="0" />
     <GradientStop Color="#7F87F5" Offset="1" />
   </LinearGradientBrush>
   ```
   In `PanelFrame` set `BorderBrush` to `{DynamicResource PanelEdge}`.
2. `Actions.xaml`, `PrimaryButton`: set `Background` and `BorderBrush` to `{DynamicResource ActionFill}`. Leave the hover and pressed triggers as they are.
3. `WindowAppearance.ApplyHighContrastPalette`: add `"PanelEdge"` to the `WindowTextBrush` list and `"ActionFill"` to the `HighlightBrush` list.
4. `Inputs.xaml`, default `TextBox` style: add
   ```xml
   <Style.Triggers>
     <Trigger Property="IsReadOnly" Value="True">
       <Setter Property="Background" Value="Transparent" />
       <Setter Property="BorderBrush" Value="{DynamicResource Line}" />
       <Setter Property="Foreground" Value="{DynamicResource Muted}" />
     </Trigger>
   </Style.Triggers>
   ```
5. `Feedback.xaml`, `ChipToggle` template: on `Chrome` set `Background="Transparent"`, `BorderThickness="1"`, `BorderBrush="{DynamicResource Line}"`, `Padding="8,3"`. In the `IsChecked` trigger set `Chrome.Background` to `{DynamicResource SurfaceRaised}` and add `<Setter TargetName="Chrome" Property="BorderBrush" Value="{Binding Background, RelativeSource={RelativeSource TemplatedParent}}" />`. Keep the dot and foreground setters.

Accept: callouts in `13` and `14` read amber; the Pending chip in `04` is clearly amber; cards have a slightly lighter top edge; the three read-only boxes in `15` are flat and muted; in `02` the Voltage and Current chips are outlined in their channel colors and Power estimate is a hollow outline.

### WP2 — Shell: header, rail, status bar (R4, R11, R12, R22)

Files: `Shell/MainWindow.xaml`, `Shell/ShellViewModel.cs`, `Themes/Icons.xaml`, `Themes/Workspace.xaml`, `Features/InstrumentSetup/InstrumentSetupView.xaml`, `Features/Service/ServiceView.xaml`, `Features/Monitor/MonitorView.xaml`.

1. `ShellViewModel.cs`: add the property and add `nameof(InstrumentLabel)` to the list in `Refresh()`. Leave `IdentityLabel` in place.
   ```csharp
   public string InstrumentLabel => Session.State == ConnectionState.Disconnected || Session.Snapshot is not { } snapshot
       ? "No instrument connected"
       : Session.SourceKind == "Simulator" ? $"{snapshot.Identity.Model} simulator"
       : $"{snapshot.Identity.Manufacturer} {snapshot.Identity.Model} / {snapshot.Identity.SerialNumber}";
   ```
2. `MainWindow.xaml` header, left block. Replace the two text blocks with the workspace name and the instrument:
   ```xml
   <TextBlock Text="{Binding SelectedItem.Header, ElementName=WorkspaceTabs, FallbackValue=Monitor}" Style="{StaticResource PageTitle}" />
   <TextBlock Text="{Binding InstrumentLabel}" Style="{StaticResource Caption}" TextWrapping="NoWrap"
              TextTrimming="CharacterEllipsis" ToolTip="{Binding ProfileLabel}" />
   ```
   Change the sixth `TabItem` header to `Instrument setup`. The app name stays in the title bar and About.
3. Remove the `PageTitle` text block from `InstrumentSetupView.xaml` ("Instrument setup") and `ServiceView.xaml` ("Instrument service"). Keep the caption lines and the read buttons.
4. Header activity chip, first child of the right-hand `StackPanel`, before `ConnectionButton`:
   ```xml
   <c:StatusBadge Text="{Binding RecordingLabel}" Foreground="{DynamicResource Action}" Background="{DynamicResource ActionSoft}"
                  Margin="0,0,12,0" VerticalAlignment="Center"
                  Visibility="{Binding IsRecordingActive, Converter={StaticResource BoolVisibility}}" />
   ```
   In `MonitorView.xaml` change the footer recording badge to the same `Action` / `ActionSoft` pair (rule 6: red is for faults and stop).
5. Status bar message. Remove the local `Text="{Binding Message}"` and give the text block this style, keeping its other attributes:
   ```xml
   <TextBlock.Style>
     <Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}">
       <Setter Property="Text" Value="{Binding Message}" />
       <Style.Triggers>
         <DataTrigger Binding="{Binding Message}" Value="">
           <Setter Property="Text" Value="{Binding ConnectionLabel}" />
           <Setter Property="Foreground" Value="{DynamicResource Muted}" />
         </DataTrigger>
       </Style.Triggers>
     </Style>
   </TextBlock.Style>
   ```
6. Rail About. Add to `Icons.xaml`: `<Geometry x:Key="IconInfo">M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,7.5 V11 M8,5.1 V5.3</Geometry>`. Add a keyed `Button` style `RailButton` to `Workspace.xaml`: width 68, height 44, foreground `Muted`, `FocusVisualStyle` `FocusRing`, hand cursor, and a template that copies the `NavItem` cell without the indicator (transparent `Border` with radius 8, an 18 px `Viewbox` icon bound to `c:Icon.Geometry` above an 11 px `ContentPresenter`). On hover use `HoverOverlay` and `Ink`. Use it for About with `c:Icon.Geometry="{StaticResource IconInfo}"` and `Margin="4,0,4,8"`; keep `Content`, `ToolTip` and `Click`.

Accept: the header reads "Monitor" over "66332A simulator" in `02`, "Sequences" in `08`, "No instrument connected" in `01`; `13` and `14` have no second title; the status bar in `09` and `10` reads "Simulator ready" instead of a lone dot; a recording chip shows in the header of `08`; at 1000 × 620 the Service rail item and About are both fully visible.

### WP3 — Instrument display and output state (R1, R2, R7, R20)

Files: `Shell/ShellViewModel.cs`, `Controls/Readout.xaml`, `Controls/Readout.xaml.cs`, `Shell/InstrumentDisplay.xaml`, `Themes/Actions.xaml`.

1. `ShellViewModel.cs`: add these members. Add `nameof(VoltageFraction)` and `nameof(CurrentFraction)` to the list in `RefreshTelemetry()` and `nameof(OutputUnknownIsAlarm)` to the list in `Refresh()`.
   ```csharp
   public double VoltageFraction => Fraction(lastSample?.Voltage.Value, Session.Snapshot?.Settings.VoltageV);
   public double CurrentFraction => Fraction(lastSample?.Current.Value, Session.Snapshot?.Settings.CurrentLimitA);
   private double Fraction(double? measured, double? setting) =>
       Available && measured is { } value && setting is { } limit && limit > 0 ? Math.Clamp(value / limit, 0, 1) : 0;
   public bool OutputUnknownIsAlarm => OutputStatus == OutputState.Unknown &&
       (Session.State is ConnectionState.Ready or ConnectionState.Lost || Session.LastShutdown?.Outcome == ShutdownOutcome.Unverified);
   ```
2. **Level bars.** `Readout.xaml.cs`: add dependency properties `Level` (`double`, default `0.0`) and `ShowLevel` (`bool`, default `false`) with CLR wrappers, following the existing pattern. `Readout.xaml`: make the rows label, digits (`*`), bar, detail, and add in row 2 (move the detail text to row 3):
   ```xml
   <ProgressBar Grid.Row="2" Maximum="1" Height="3" Margin="0,2,0,5" IsHitTestVisible="False"
                Value="{Binding Level, ElementName=Root, Mode=OneWay}" Foreground="{Binding Accent, ElementName=Root}"
                AutomationProperties.Name="{Binding Label, ElementName=Root, StringFormat='{}{0} relative to its setting'}">
     <ProgressBar.Style>
       <Style TargetType="ProgressBar" BasedOn="{StaticResource {x:Type ProgressBar}}">
         <Setter Property="Visibility" Value="Hidden" />
         <Style.Triggers>
           <DataTrigger Binding="{Binding ShowLevel, ElementName=Root}" Value="True">
             <Setter Property="Visibility" Value="Visible" />
           </DataTrigger>
         </Style.Triggers>
       </Style>
     </ProgressBar.Style>
   </ProgressBar>
   ```
   In `InstrumentDisplay.xaml` set `Level="{Binding VoltageFraction}" ShowLevel="True"` on Voltage and `Level="{Binding CurrentFraction}" ShowLevel="True"` on Current. Power keeps the hidden bar so the rows align. The bar shows the reading against the setpoint or limit, so a current bar near full means the supply is near its limit.
3. **Backlight instead of edge lines.** Delete the `ChannelEdge` style and its three borders. Set each readout's `Margin` to `0`. Add this style and put `<Rectangle Style="{StaticResource Backlight}" Fill="{DynamicResource Voltage}" />` (then `Current`, then `Ink`) as the first child of each channel cell's `Grid`:
   ```xml
   <Style x:Key="Backlight" TargetType="Rectangle">
     <Setter Property="Margin" Value="-16,-8" />
     <Setter Property="IsHitTestVisible" Value="False" />
     <Setter Property="Visibility" Value="Collapsed" />
     <Setter Property="OpacityMask">
       <Setter.Value>
         <RadialGradientBrush Center="0.35,0.6" GradientOrigin="0.35,0.6" RadiusX="0.65" RadiusY="0.8">
           <GradientStop Color="#20000000" Offset="0" />
           <GradientStop Color="#00000000" Offset="1" />
         </RadialGradientBrush>
       </Setter.Value>
     </Setter>
     <Style.Triggers>
       <DataTrigger Binding="{Binding IsFresh}" Value="True">
         <Setter Property="Visibility" Value="Visible" />
       </DataTrigger>
       <DataTrigger Binding="{Binding Source={x:Static SystemParameters.HighContrast}}" Value="True">
         <Setter Property="Visibility" Value="Collapsed" />
       </DataTrigger>
     </Style.Triggers>
   </Style>
   ```
4. **Output state tile.** Replace the content of the fourth column with one large state tile above a row of the three small annunciators:
   ```xml
   <StackPanel Width="168" VerticalAlignment="Center" AutomationProperties.Name="{Binding OutputLabel}">
     <Border CornerRadius="8" Padding="12,7">
       <Border.Style>
         <Style TargetType="Border">
           <Setter Property="Background" Value="{DynamicResource SurfaceRaised}" />
           <Setter Property="TextElement.Foreground" Value="{DynamicResource Muted}" />
           <Style.Triggers>
             <DataTrigger Binding="{Binding OutputStatus}" Value="{x:Static model:OutputState.On}">
               <Setter Property="Background" Value="{DynamicResource WarningSoft}" />
               <Setter Property="TextElement.Foreground" Value="{DynamicResource Warning}" />
             </DataTrigger>
             <DataTrigger Binding="{Binding OutputUnknownIsAlarm}" Value="True">
               <Setter Property="Background" Value="{DynamicResource FaultSurface}" />
               <Setter Property="TextElement.Foreground" Value="{DynamicResource Fault}" />
             </DataTrigger>
           </Style.Triggers>
         </Style>
       </Border.Style>
       <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
         <Ellipse Width="8" Height="8" Margin="0,0,8,0"
                  Fill="{Binding (TextElement.Foreground), RelativeSource={RelativeSource AncestorType=Border}}" />
         <!-- the existing output TextBlock with its OUTPUT ? / OUTPUT OFF / OUTPUT ON triggers; add FontSize="14" FontWeight="Medium" -->
       </StackPanel>
     </Border>
     <UniformGrid Columns="3" Margin="0,8,0,0">
       <!-- the existing CV/CC, PROT and SIM borders, unchanged except: drop Grid.Row/Grid.Column, add Margin="0,0,4,0" to the first two -->
     </UniformGrid>
   </StackPanel>
   ```
   The text still says `OUTPUT ?` whenever the state is unknown. It is red when the app is or was connected and cannot verify the output, and neutral when no instrument has been connected.
5. **Energized and fault frame.** On the root `Border`, replace `Style="{StaticResource PanelFrame}"` with a local style based on `PanelFrame` that has two data triggers: `OutputStatus == On` sets `BorderBrush` to `{DynamicResource Warning}`; `ConnectionStatus == Lost` sets it to `{DynamicResource FaultLine}`. Keep the `Background` attribute.
6. **Lost-connection strip.** On the age-strip `Border`, remove the local `BorderBrush`, add `CornerRadius="0,0,11,11"`, and give it a style: default `BorderBrush` `Line`; when `ConnectionStatus == Lost`, `Background` `FaultSurface` and `BorderBrush` `FaultLine`. Before the `AgeLabel` text add a left-docked text block, collapsed unless `ConnectionStatus == Lost`: text `Connection lost. Output state unknown. Check the supply.`, 12 px SemiBold, `Fault`, right margin 12.
7. **Output On reads as an action.** In `OutputToggleButton`, change the three setters of the `ShowOutputOn` + enabled trigger to `Background` `SurfaceRaised`, `BorderBrush` `Ready`, `Foreground` `Ink`.

Accept: in `03` the display has an amber frame, a wide amber `OUTPUT ON` tile, a soft glow behind each live reading, a full voltage bar and a current bar at about 40 %. In `02` the tile is neutral `OUTPUT OFF` and the header button is a raised button with a green outline. In `01` the tile is neutral `OUTPUT ?` with no wash. In `11` the frame and strip are red and the strip states the lost connection. No strip corner pokes outside the rounded frame.

### WP4 — Trend charts (R3, R17, R23)

Files: `Controls/PlotTheme.cs`, `Controls/TrendPanel.xaml`, `Controls/TrendPanel.xaml.cs`, `Features/Monitor/MonitorView.xaml`.

1. **About six time labels.** Add to `PlotTheme`:
   ```csharp
   private static readonly int[] TickSteps = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 21600, 43200, 86400];

   public static void SetTimeTicks(Plot plot, double spanDays)
   {
       double seconds = Math.Max(1, spanDays * 86400);
       int step = TickSteps.FirstOrDefault(candidate => seconds / candidate <= 8, 86400);
       string format = step < 60 ? "HH:mm:ss" : "HH:mm";
       plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeFixedInterval(
           new ScottPlot.TickGenerators.TimeUnits.Second(), step, null, 1,
           start => start.Date.AddSeconds(Math.Floor(start.TimeOfDay.TotalSeconds / step) * step))
       { LabelFormatter = time => time.ToString(format) };
   }
   ```
   In `TrendPanel.StylePlot` delete the `DateTimeAutomatic` line. In `Draw`, after the two `SetLimitsX` branches and before `host.Refresh()`, add `var shown = plot.Axes.GetLimits(); PlotTheme.SetTimeTicks(plot, shown.Right - shown.Left);`. If the constructor call does not compile against 5.0.55, restore the `DateTimeAutomatic` line and skip this step. Do not upgrade ScottPlot.
2. **Quieter event markers.** In `Draw`, set `marker.LineWidth = 1` and alpha `0.7` for `failure`, `0.28` for the rest.
3. **Live dot halo.** Before the existing last-sample marker, unless `SystemParameters.HighContrast`, add `plot.Add.Marker(x[^1], y[^1], MarkerShape.FilledCircle, 16, channel.WithAlpha(0.22));`.
4. **Statistics in the title rows.** In `TrendPanel.xaml` turn each chart title `StackPanel` into a `DockPanel` (`Margin="12,0,12,0"`) and dock right a text block named `VoltageStats`, `CurrentStats` or `PowerStats` (style `Caption`, `MonoFont`, `TextWrapping="NoWrap"`, automation name such as "Voltage statistics for the visible range"). Add two parameters to `Draw`, `TextBlock stats` and `string format`, and pass `VoltageStats, "F4"`, `CurrentStats, "F5"` and `PowerStats, "F4"` from the three calls in `Render`. After step 1's `shown` line set `stats.Text = Summary(x, y, shown.Left, shown.Right, format);`:
   ```csharp
   private static string Summary(double[] x, double[] y, double left, double right, string format)
   {
       double min = double.PositiveInfinity, max = double.NegativeInfinity, sum = 0;
       int count = 0;
       for (int index = 0; index < y.Length; index++)
       {
           if (!double.IsFinite(y[index]) || x[index] < left || x[index] > right) continue;
           min = Math.Min(min, y[index]); max = Math.Max(max, y[index]); sum += y[index]; count++;
       }
       return count == 0 ? "" : $"min {min.ToString(format)}   avg {(sum / count).ToString(format)}   max {max.ToString(format)}";
   }
   ```
5. `MonitorView.xaml`: give the `Freeze cursor` chip `Margin="12,0,0,0"` so it reads as a separate control from the channel chips.

Accept: `02` and `03` show five to eight evenly spaced time labels on round times; in `11` the traces stay readable through the event markers; each chart title row ends with min, avg and max for the visible range; the last sample has a soft halo.

### WP5 — Manual control (R5, R6)

Files: `Features/Monitor/ManualControlPanel.xaml`, `Features/Monitor/ManualControlPanel.xaml.cs`.

1. **No jump.** On the `DraftStatus` text block, base the style on `Caption` instead of `OptionalCaption` and add `MinHeight="16"`. The line now always holds its place.
2. **Use the height.** Name the Protections expander `ProtectionsSection`. Replace the constructor:
   ```csharp
   private bool userChose, adjusting;

   public ManualControlPanel()
   {
       InitializeComponent();
       ProtectionsSection.Expanded += (_, _) => userChose |= !adjusting;
       ProtectionsSection.Collapsed += (_, _) => userChose |= !adjusting;
       SizeChanged += (_, _) =>
       {
           if (userChose) return;
           adjusting = true;
           ProtectionsSection.IsExpanded = ActualHeight >= 560;
           adjusting = false;
       };
   }
   ```
   Protections opens by itself when the panel is tall enough to show it without scrolling, and the user's own choice wins afterwards.

Accept: Apply and Discard sit at the same height in `03` and `04`; `02` (1440 × 900) shows Protections open with no scrollbar; `05` (1120 × 680) shows it closed.

### WP6 — First run and connection window (R10, R21)

Files: new `Controls/ShellCommands.cs`, `Shell/MainWindow.xaml`, `Shell/MainWindow.xaml.cs`, `Features/Monitor/MonitorView.xaml`, `Shell/ConnectionPanel.xaml`.

1. Command:
   ```csharp
   namespace Agilent66332A.Desktop.Controls;

   public static class ShellCommands
   {
       public static readonly System.Windows.Input.RoutedUICommand OpenConnection =
           new("Connection settings", nameof(OpenConnection), typeof(ShellCommands));
   }
   ```
   `MainWindow.xaml`: add `<Window.CommandBindings><CommandBinding Command="{x:Static c:ShellCommands.OpenConnection}" Executed="OpenConnectionFromCommand" /></Window.CommandBindings>`. `MainWindow.xaml.cs`: add `private void OpenConnectionFromCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs args) => OpenConnectionSettings(sender, args);`.
2. `MonitorView.xaml` empty state: wrap the existing `HeaderedContentControl` in a centered `StackPanel`, change its `Content` to `Choose the serial port in Connection settings, then connect.`, and add below it `<Button Content="Connection settings" c:Icon.Geometry="{StaticResource IconPlug}" Style="{StaticResource PrimaryButton}" Command="{x:Static c:ShellCommands.OpenConnection}" HorizontalAlignment="Center" Margin="0,12,0,0" />`.
3. `ConnectionPanel.xaml`: put the `SerialPort` combo box in a `Grid` with a `MutedText` text block over it (`Text="Select a serial port"`, `Margin="11,0,0,0"`, `IsHitTestVisible="False"`), collapsed by default and visible through a data trigger when `{Binding SelectedPort}` is `{x:Null}`.

Accept: `01` shows the new sentence and an indigo "Connection settings" button that opens the same single connection window as the header pill; `15` shows "Select a serial port" in the empty Port box.

### WP7 — Capture (R15)

Files: `Features/Capture/CaptureView.xaml`, `Features/Capture/CaptureView.xaml.cs`, `Features/Capture/TransientView.xaml`.

1. Change the `Digitizer` and `Triggered output levels` titles from `PageTitle` to `Title`.
2. `CaptureView.Draw`: scale the time axis and add units to the tiles. Leave the `StatisticsText` string as it is.
   ```csharp
   double span = seconds.Length > 1 ? Math.Abs(seconds[^1] - seconds[0]) : 0;
   (double scale, string timeUnit) = span < 0.001 ? (1e6, "µs") : span < 1 ? (1e3, "ms") : (1d, "s");
   double[] time = seconds.Select(value => value * scale).ToArray();
   ```
   Plot `time` instead of `seconds`. Set the bottom label to `$"Time ({timeUnit})"` or `$"Time from trigger ({timeUnit})"`. Append the unit to each tile, for example `DcStat.Text = $"{s.Dc:G6} {unit}";`.

Accept: `12` shows a time axis running from 0 to about 2 labeled "Time (ms)", every tile ends in V or A, and "Digitizer" matches the size of other card titles.

### WP8 — Recordings and diagnostics (R13, R14)

Files: new `Controls/PartConverter.cs`, `Features/Recordings/RecordingTimeConverter.cs`, `Features/Recordings/RecordingsView.xaml`, `Features/Diagnostics/DiagnosticsView.xaml`.

1. Converter:
   ```csharp
   namespace Agilent66332A.Desktop.Controls;

   public sealed class PartConverter : System.Windows.Data.IValueConverter
   {
       public string Separator { get; set; } = " / ";

       public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
       {
           string[] parts = (value as string ?? "").Split(Separator, 2);
           int index = int.Parse((string)parameter, System.Globalization.CultureInfo.InvariantCulture);
           return index < parts.Length ? parts[index] : "";
       }

       public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
           throw new NotSupportedException();
   }
   ```
2. **Local time.** `RecordingTimeConverter`: format `start.ToLocalTime()` instead of `start.UtcDateTime`. Rename the column header to `Started`. The tooltip keeps the stored UTC value.
3. **Outcome.** Add `<c:PartConverter x:Key="Part" />`. In the Outcome cell use a horizontal `StackPanel`: the existing `OutcomeChip` with its text bound to `{Binding Outcome, Converter={StaticResource Part}, ConverterParameter=0}`, then a second chip based on `ChipWarning` (`Margin="6,0,0,0"`) with text `{Binding Outcome, Converter={StaticResource Part}, ConverterParameter=1, StringFormat='Recording: {0}'}`. Collapse the second chip with two data triggers when part 1 equals `Complete` or the empty string.
4. **Shutdown.** In `ShutdownResult` add a trigger: value `Unavailable` sets `Text` to `—`. In `ShutdownChip` add a trigger: value `Unavailable` sets `Background` to `Transparent`.
5. **Registers card.** In `DiagnosticsView.xaml` wrap the registers `Expander` in `<Border Grid.ColumnSpan="3" Style="{StaticResource PanelFrame}" Padding="8,4" Margin="0,0,0,12">`, move `Grid.ColumnSpan` off the expander, remove its bottom margin, and remove the inner `PanelFrame` border around `DiagnosticsRegistersView`.
6. **Log lines.** Add `<c:PartConverter x:Key="LogPart" Separator="&#x20;&#x20;" />` (two spaces). In both list item templates use a `DockPanel`: a left-docked text block bound to part 0 in `Muted` with `Margin="0,0,12,0"`, then the existing text block with its style and triggers, its `Text` bound to part 1. The triggers keep testing the whole line.

Accept: `09` shows local start times, a single "Completed" or "Cancelled" chip per row, and a dash for manual recordings; `10` shows the registers expander as a card and muted timestamps beside bright messages; Copy all and Export output are unchanged.

### WP9 — Sequences (R18)

Files: `Features/Sequences/SequencePreview.cs`, `Features/Sequences/SequenceView.xaml`.

1. **Stepped preview.** In `OnRender`, set `LineJoin = PenLineJoin.Round` on both pens. Keep drawing the active band and the boundary lines inside the loop, but collect the two traces into `StreamGeometry` figures and draw them after the loop so each trace is one connected stepped line:
   ```csharp
   var voltage = new StreamGeometry(); var current = new StreamGeometry();
   using (var v = voltage.Open())
   using (var c = current.Open())
   {
       double elapsed = 0;
       for (int index = 0; index < d.Steps.Length; index++)
       {
           var step = d.Steps[index];
           double start = left + elapsed / duration * (right - left);
           elapsed += step.HoldSeconds;
           double end = left + elapsed / duration * (right - left);
           double voltageY = 26 - step.VoltageV / maxV * 20, currentY = 56 - step.CurrentLimitA / maxI * 20;
           if (vm.Rows[index].IsActive) dc.DrawRectangle(active, null, new Rect(start, 0, end - start, ActualHeight));
           dc.DrawLine(boundary, new(start, 0), new(start, ActualHeight));
           if (index == 0) { v.BeginFigure(new(start, voltageY), false, false); c.BeginFigure(new(start, currentY), false, false); }
           else { v.LineTo(new(start, voltageY), true, true); c.LineTo(new(start, currentY), true, true); }
           v.LineTo(new(end, voltageY), true, true); c.LineTo(new(end, currentY), true, true);
       }
   }
   dc.DrawGeometry(null, voltagePen, voltage);
   dc.DrawGeometry(null, currentPen, current);
   ```
2. **Idle run card.** In `SequenceView.xaml` bind the progress bar's `Visibility` to `{Binding RunCommand.IsRunning, Converter={StaticResource BoolVisibility}}`.

Accept: `08` shows two continuous stepped traces with the active step highlighted and a progress bar; with no run active the card shows "No active run" and no empty bar.

### WP10 — Evidence and specification (R24, R25)

Files: `Verification/DesktopVerification.cs`, `plans/02-experience-and-visual-design.md`, `plans/08-decisions-risks-and-sources.md`, `plans/10-ui-refinement-pass.md`.

1. Add three captures. Do not change any existing check.
   - After the `06-laptop-1366x768` capture: `window.Width = 1920; window.Height = 1080; await CaptureAsync(window, path, "16-wide-1920x1080");`
   - After the `10-diagnostics` capture:
     ```csharp
     var registers = VisualDescendants<Expander>(window).Single(section => Equals(section.Header, "Instrument registers and errors"));
     registers.IsExpanded = true;
     await CaptureAsync(window, path, "17-diagnostics-registers");
     registers.IsExpanded = false;
     ```
   - After the `12-waveform-1120x680` capture:
     ```csharp
     var captureTabs = VisualDescendants<TabControl>(window).Single(tabs => !ReferenceEquals(tabs, window.FindName("WorkspaceTabs")));
     captureTabs.SelectedIndex = 1;
     await CaptureAsync(window, path, "18-output-trigger-1120x680");
     captureTabs.SelectedIndex = 0;
     ```
2. Plan 02: update the token table (three soft tints, `PanelEdge`, `ActionFill`). Rewrite the shell paragraph to match what ships: the header shows the workspace name and the instrument, a recording chip, the connection button, and one output control that offers Output On only when the session allows it and Output Off otherwise. Remove the sentence about a full-width Output On in the manual panel. Describe the output state tile, the energized and fault frames, the level bars and the backlight in the measurement display section, and widen "Glow belongs to…" to include the display backlight. State that the level bars show the reading against its setting and are not decorative gauges.
3. Plan 08: record the decisions (state tile and frame color, neutral `OUTPUT ?` before any connection, local time in Recordings, indigo recording indicator, fixed-interval time ticks).
4. Set the status line of plan 10 to implemented, and update any screenshot count in `plans/` and `docs/` to 18.

Accept: `tools\verify-ui.ps1` writes 18 PNGs and `Binding errors: 0`.

---

## Verification (end to end)

1. `tools\test.ps1`: all existing tests pass.
2. `tools\verify-ui.ps1`: exits 0, `verification.txt` ends with `Binding errors: 0`, 18 PNGs regenerate.
3. Review the PNGs against each WP's acceptance bullets, especially `01`, `02`, `03`, `04`, `05-compact-1120x680`, `08`, `11-lost-unknown`, `12`, and the three new captures.
4. Manual pass with `tools\dev.ps1 -Simulator`: resize to 1000 × 620 and check the rail, header and output control in all seven workspaces; turn the output on and off and watch the tile, frame and header button change with no animation; zoom and pan a chart and confirm the time labels stay sparse; open Connection settings from both the header pill and the empty-state button; start a recording and switch workspaces to see the header chip; relaunch with Windows high contrast on and confirm no wash, gradient or halo appears.

## Not in this pass

- **Custom-drawn title bar.** Merging the header into the title bar is the most modern-looking option and would save about 40 px of height. It would also put the output control inside the window caption, where a missed hit-test attribute makes it unclickable by mouse while the automation-based harness still passes. It is left out for that reason. If it is wanted later, specify it together with a harness check for caption hit-testing.
- **A simulator entry point in the UI.** About says "Try the simulator without a connected instrument", but only the `--simulator` switch starts it. Adding a button is a behavior change and needs an owner decision. The alternative is rewording About to name the switch.
