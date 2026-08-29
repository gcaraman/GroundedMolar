# MolarMap UI design guidelines

## Purpose

MolarMap should feel at home beside Grounded without pretending to be an in-game screen. These guidelines translate the game's settings UI into a maintainable Windows/WPF design system while preserving the tool's clarity, accessibility, and fail-closed data semantics.

The game's exported UI is a reference, not a component library. Do not copy extracted fonts or textures into a distributable build unless their redistribution rights are established.

## Evidence and confidence

The primary reference corpus is the supplied export at:

`C:\Users\Caram\Downloads\49ddcf9460ea124db305d541891f465a693d971f\Output\Exports\Maine\Content\UI`

Evidence reviewed on 2026-08-23:

- `Options/UI_Options.json`: settings shell, categories, scrollable content, contextual title and description, and reference dimensions.
- `Options/UI_LabeledToggle.json`, `UI_LabeledSlider.json`, and `UI_LabeledDropdown.json`: option-row layout, typography, control sizing, focus/hover treatment, and interaction states.
- `Options/UI_GenericDropdown.json` and `UI_OnOffToggle.json`: reusable control composition.
- `Buttons/UI_TabButton.json`: category navigation and checked/unchecked states.
- `GlobalColor/GlobalColorTheme_Default.json`: the default runtime color tokens.
- `Fonts/BaseFont.json` and `Fonts/TitleFont.json`: type roles and fallback behavior.
- `Images/T_UI_OptionNugget_*`, `T_UI_Dropdown*`, `T_UI_SliderThumb`, and `T_UI_Switch_*`: native texture dimensions and named state variants.
- The corresponding exported PNGs for those assets, plus `T_UI_BtnSolid`, `T_UI_BtnSquare`, `T_UI_ExpandoRotato`, `T_UI_MenuHeaderThing`, and `SCABAnims/T_UI_OminentSlide_3`: pixel-level silhouette, alpha-mask, outline, and state treatment.
- `src/GroundedMolar.App/MainWindow.xaml`: the current desktop UI being guided.

The JSON export proves widget structure and configured values, but it is not a runtime screenshot. Treat exact appearance after Unreal materials, animation curves, scaling, and platform-specific rendering as unverified. Rules below labeled **Grounded-derived** follow exported values; **Project adaptation** rules are deliberate MolarMap decisions.

### Texture findings

The narrowly selected settings textures confirm that most shared chrome is authored as monochrome white/gray artwork with transparency and then tinted or composited at runtime. The PNG pixels therefore prove shape and value relationships, not final screen color.

- Option nuggets are compact irregular tabs with a clipped lower-left and upper-right profile. Up/down are filled; hover becomes a bold outline with a dark or transparent center.
- Dropdowns are long fields with clipped top-left and lower-right corners, an angled right edge, and a triangular down indicator cut into that edge. Hover adds a light inset/outline while retaining the silhouette.
- The slider thumb is a simple forward-leaning parallelogram.
- On/off switches are wide, tapered capsules with explicit interior `I`/`O`-like marks. Off is dim and outlined; on is bright and filled/outlined. State does not depend on color alone.
- The menu header is a broad field crossed by parallel diagonal cuts. The shared hover strip is nearly empty except for fine edge marks, supporting a restrained animated highlight rather than a busy texture.
- Solid and square button assets are minimal masks with subtly clipped corners, designed to receive runtime color rather than carry their own palette.

Do not sample the white PNG values as application colors. Reproduce the geometry with WPF `Path`, `Geometry`, borders, and semantic brushes where useful.

## Design character

Grounded's settings language combines a playful, chunky silhouette with disciplined information architecture:

- Warm, high-contrast color rather than neutral desktop gray.
- Large, readable labels and generous row height.
- Condensed uppercase display text paired with a broader, friendlier body face.
- Controls that look like physical pieces: nuggets, tabs, switches, and sliders.
- Strong selection and focus feedback using shape, texture/layer, and color together.
- A stable list-and-detail layout: choices remain scannable while explanation occupies a separate region.

For MolarMap, preserve the warmth, weight, and clear states. Avoid ornamental density that competes with the map or makes authoritative status harder to read.

## Layout system

### Grounded-derived reference geometry

The settings shell is authored against 1920 × 1080. Its principal content includes:

- A 1140 × 740 settings region.
- A 640 × 740 contextual region for the selected setting's title, description, and occasional preview.
- Scrollable category content switched between Sound, Controller, Display, Keyboard, Accessibility, Game, and HDR groups.
- Reusable option rows with a minimum height of 80.
- A nominal 620-unit label area and 400-unit control area.
- Labels vertically centered; controls right-aligned with approximately 20 units of trailing space.

These are ratios, not mandatory WPF pixels. The useful settings-row ratio is approximately 61:39.

### Project adaptation

- Use an 8-DIP spacing base. Preferred values are 8, 16, 24, and 32 DIP.
- Give standard settings rows a minimum height of 64 DIP on desktop; use 72–80 DIP when descriptions or touch/gamepad use justify it.
- Use a two-column row above 760 DIP available width: label 3fr, control 2fr. Stack label over control below that breakpoint.
- Keep the map as the visual primary surface. Tool controls belong in one compact command/settings region, not scattered around the map.
- Group save source, view controls, and marker appearance as separate sections. Do not rely on proximity alone to explain the groups.
- Keep contextual help close enough to associate with a focused setting, either in a stable side panel or directly beneath the row on narrow windows.
- Preserve minimum window support and keyboard reachability at 200% Windows scaling. Never require a 1920 × 1080 display.

## Color system

### Grounded-derived default tokens

The default runtime theme exports these useful semantic colors:

| Role | Export token | Hex |
| --- | --- | --- |
| Deep base | `SCABosBase` | `#7B0D00` |
| Primary red | `SCABosAccentOne` | `#9F2600` |
| Bright red-orange | `SCABOSAccentTwo` | `#C32C00` |
| Ochre accent | `SCABOSAccentThree` | `#AF7B00` |
| Warning | `SCABOSWarningOne` | `#FA3D2B` |
| Basic text | `BasicText` | `#F7EFDD` |
| Header text | `HeaderText` | `#FBD688` |
| Subheader/selected | `SubheaderText`, `Selected` | `#DB9400` |
| Attention text | `Attention` | `#FFF0CB` |
| Dark overlay | `GlobalBG` | `#280500` at 80% opacity |
| Darkest surface | `GlobalBGDarkener` | `#280500` |
| Button accent | `ButtonBGAccent` | `#B64434` |
| Disabled text | `DisabledText` | `#E7D090` at 60% opacity |

Alpha-bearing export hex strings are represented above as color plus opacity to avoid ARGB/RGBA ambiguity.

### Project adaptation

Define semantic WPF resources rather than placing hex values in individual controls:

- `Surface.Window`: near-black warm aubergine or the exported dark overlay.
- `Surface.Panel`: a warmer, lighter layer distinguishable from the window without a heavy border.
- `Surface.PanelStrong`: used for the active save card and focused settings.
- `Text.Primary`: warm cream.
- `Text.Secondary`: muted warm cream with adequate contrast.
- `Text.Heading`: pale gold.
- `Accent.Primary`: red-orange for actions and active structure.
- `Accent.Selected`: ochre/gold for selection and focus.
- `Status.Success`, `Status.Warning`, `Status.Error`, and `Status.Unknown`: independent semantic tokens.

Color rules:

- Never use Grounded's orange as proof that save data is validated. Data confidence and collection state need explicit text and/or icons.
- `Unknown` and `Unsupported` must be visibly distinct from disabled controls and from ordinary empty states.
- Maintain at least WCAG 2.1 AA contrast for normal text (4.5:1) and large text/UI boundaries (3:1).
- Do not encode hover, checked, validation, or collection state by color alone.
- Keep the map palette and marker-state palette independent from application chrome.

## Typography

### Grounded-derived roles

- `BaseFont` uses Gill Sans Infant Std Extended as its primary Latin face, with explicit script fallbacks.
- `TitleFont` uses Gill Sans MT Pro Condensed.
- Settings row labels use the base role at exported size 28.
- Toggle value text uses the condensed title role at size 26 and transforms to uppercase.
- The settings context title uses the base role at size 26; its description uses size 28.

The important pattern is the role contrast, not the exact proprietary fonts or Unreal sizes.

### Project adaptation

- Use a redistributable or system-available humanist sans for body text, such as Segoe UI.
- Use a condensed, bold, redistributable face for short headings only if it is intentionally packaged and licensed; otherwise use `Segoe UI Semibold` with modest character spacing.
- Window/page title: 24–28 DIP, semibold, uppercase permitted.
- Section title: 18–20 DIP, semibold.
- Control label: 14–16 DIP, regular or medium; sentence case preferred for desktop readability.
- Value/button text: 14–16 DIP, semibold; uppercase only for short labels.
- Supporting/status text: 12–14 DIP; do not reduce opacity below legibility to create hierarchy.
- Avoid all-caps paragraphs, filenames, paths, GUIDs, and error messages.
- Let user-visible text wrap or trim with a tooltip; never clip translated strings into fixed ornamental shapes.

## Controls and interaction states

### Option rows

**Grounded-derived:** toggles, sliders, and dropdowns share the same large labeled-row composition. A hidden hover/focus bar sits behind the row and becomes visible through animation. The row, not just the small control, is the navigable unit.

**Project adaptation:** create one reusable `SettingRow` style/control with label, optional description/help, value host, validation/status slot, and row-level focus visual. Reuse it for monitoring, marker opacity, and future preferences.

### Buttons

- Primary actions should have a filled warm accent surface, a cream label, and a clearly different hover/focus outline.
- Secondary actions may use a quieter panel surface but must retain a visible boundary.
- Destructive actions require a separate danger treatment and explicit wording.
- Icon-only buttons require accessible names and tooltips. Use text for uncommon actions such as “Choose saves folder”.
- Keep at least 32 × 32 DIP pointer targets; prefer 40 × 40 DIP for frequent controls.
- Do not use ellipses unless the action opens a picker/dialog or requires more input.
- A small clipped corner or angled trailing edge is the preferred Grounded-derived silhouette. Use it consistently; do not mix several unrelated corner treatments.

### Toggle

**Grounded-derived:** `T_UI_Switch_Off` and `T_UI_Switch_On` are separate 136 × 48 assets; the widget is configured as a toggle button. The visual includes explicit on/off state rather than a bare checkmark.

**Project adaptation:** use an on/off switch or checkbox with a persistent text label. Display state text when ambiguity is possible. Keep the full row clickable, but preserve standard keyboard behavior and automation properties.

If a custom switch is introduced, use a tapered outline and explicit `ON`/`OFF` text or equivalent state glyph. Do not copy the exported bitmap or depend on a cream/gray color change alone.

### Slider

**Grounded-derived:** the slider uses a distinct 48 × 36 thumb; the labeled slider pairs a 620-wide label area with a 400-wide value/control area and shows a condensed value readout.

**Project adaptation:** always show the current value and unit (`45%`, not `0.45`). Support arrow keys, Home/End, and a sensible step. For unapproached-marker opacity, expose 0–100% in the UI while retaining the existing normalized value internally.

A slanted parallelogram thumb is an appropriate lightweight reference to the game. Its focus indicator and hit target must remain larger than the visible shape.

### Dropdown

**Grounded-derived:** dropdown backgrounds have separate 128 × 48 up and hover assets. The labeled dropdown uses the common 80-high option row.

**Project adaptation:** use dropdowns only for mutually exclusive values with more than two choices. Keep the selected value visible, and ensure the popup is not narrower than its field.

Use one angled trailing edge and a high-contrast downward triangle if custom dropdown chrome is added. The arrow must remain a semantic indicator, not decoration.

### Focus, hover, press, selected, and disabled

Every interactive control must define all five states:

- Hover: a modest surface/outline change.
- Keyboard focus: a persistent 2-DIP high-contrast outline, independent of hover.
- Pressed: a stronger fill or slight inset change; do not move layout.
- Selected/checked: gold/ochre plus a shape, indicator, or state text.
- Disabled: reduced emphasis while retaining readable text; explain why when the reason is not obvious.

Use 100–180 ms transitions for hover and focus if animation is added. Honor Windows reduced-motion/accessibility preferences and never delay functionality for animation.

## Information and status language

MolarMap's truth model takes priority over theme fidelity:

- Say `Validated`, `Unknown`, or `Unsupported` explicitly where confidence affects output.
- Say `Uncollected`, `Collected`, `Approached`, or `Unapproached` only when resolved from authoritative records.
- Empty state: explain the next action, for example “Choose a World.csav or saves folder to load a map.”
- Unsupported state: keep the map clear and explain that the save format was not recognized. Never present this as “no molars found.”
- Loading/refresh: retain the last validated view only if it is clearly labeled stale; otherwise clear it as the current implementation does.
- Errors should state what failed, what remains safe, and the next recovery action. Do not expose raw exception text as the primary message.
- Filenames, paths, counts, and zoom are metadata; visually subordinate them to the current save identity and validation result.

## Recommended MolarMap composition

For the current single-window app:

1. A compact title/status bar with app identity and the current analysis confidence.
2. A “Current save” card with the 11:6 screenshot aperture, save name, location, timestamp, and authoritative count summary.
3. A command group containing “Choose save”, “Choose saves folder”, and “Refresh”. Make Refresh the default action only after a source exists.
4. A “View” group containing zoom out, zoom in, Fit, and the current zoom value.
5. A “Preferences” group using shared setting rows for automatic folder monitoring and unapproached-marker opacity.
6. The map as the remaining flexible area, with a clear empty/unsupported overlay when no validated image may be rendered.

At narrow widths, place the save card above commands and stack setting labels over their controls. At wide widths, a compact right-side control column is acceptable. Do not shrink the map solely to imitate the game's settings split.

## WPF implementation rules

- Move palette, type, spacing, button, focus, slider, checkbox/toggle, card, and setting-row resources into merged dictionaries under `src/GroundedMolar.App/Styles/` before the next substantial UI expansion.
- Reference resources with `DynamicResource` where future theme switching or high-contrast support is expected.
- Use semantic keys, not game asset names, in application XAML.
- Prefer vector geometry and WPF styling for chrome. Keep bitmap assets for content that genuinely requires authored raster art.
- Recreate only the small vocabulary of clipped corners, angled trailing edges, tapered switches, and diagonal header accents needed by the app. Do not build a texture atlas or import the game's monochrome masks.
- Set `AutomationProperties.Name` for icon-only and custom controls.
- Verify keyboard tab order, visible focus, screen-reader names, 200% scaling, minimum window size, long paths, long translated labels, and high-contrast mode.
- Keep all UI styling out of save decoding, parsing, state resolution, projection, and rendering code.

## Review checklist

Before merging UI work, verify:

- Does it preserve the map and authoritative status as the primary information?
- Does it reuse semantic resources and shared control styles?
- Are hover, focus, pressed, selected, disabled, loading, empty, error, `Unknown`, and `Unsupported` states defined where applicable?
- Is every state understandable without color alone?
- Is every interactive target keyboard reachable with a visible focus indicator?
- Do text and control boundaries meet contrast targets?
- Does it work at the project's minimum window size and 200% display scaling?
- Do long paths and localized labels wrap or trim safely?
- Are extracted game fonts/textures absent unless redistribution is explicitly cleared?
- Has visual polish remained separate from authoritative save analysis and fail-closed rendering?

## Current UI gap assessment

The current `MainWindow.xaml` already uses a warm dark background, cream text, orange-red borders, a current-save card, and a prominent map. Its main gaps against this guide are:

- Colors and control metrics are embedded directly in the window instead of semantic resources.
- Native WPF buttons, checkbox, and slider do not share a Grounded-inspired state system.
- Command, view, and preference controls rely mostly on proximity rather than labeled sections.
- Several secondary text colors use generic `Gray`, weakening palette cohesion and potentially contrast.
- The opacity slider exposes no unit in its static label and should present a percentage consistently.
- Keyboard focus, disabled, loading, unsupported, and error visuals are not centrally specified.
- The control area uses wrapping panels without a deliberate narrow-window setting-row layout.

Address these through shared resources and composition first. Decorative texture work should come only after interaction states, accessibility, and responsive behavior are verified.
