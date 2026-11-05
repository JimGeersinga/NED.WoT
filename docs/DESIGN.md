# WoT Gevechtslog design rules

The look of record is the canvas "WoT Gevechtslog" (https://claude.ai/artifact/Xi6h8kaLxdDjXZLHoH7Fro), built on the WoT HQ design system
(https://claude.ai/artifact/MnaxNrvDVemYHNzsu5rBQD). UI issues cite this file instead of inventing values. If the code and this file disagree, fix one in the same PR.
The code that carries it is created by #2 (`Theme/WotTheme.cs`, `wwwroot/css/`, `wwwroot/fonts/`) and the issues after it; until then the paths below do not exist.
All paths are under `NED.WoT.BattleResults.Client/`.

## Principles
- Dark only, no theme toggle. Primary is `#ff8800`.
- Orange (`Primary`) only on the primary action and on selected or active things: the selected toggle item, the expanded-row bar, the own-player row bar, the key number and the focus ring. At most about five orange things per screen. One filled primary button per view.
- Status is never shown by colour alone: every status chip carries an icon and a word.
- Material icons only (`Icons.Material.*`): Filled for state and identity, Outlined for navigation and neutral actions. Plus the game's own art where the canvas uses it (tank and level icons).
- No emoji, no other icon set.
- Fonts are **bundled** as woff2 under `wwwroot/fonts/` (Barlow Condensed for headings, buttons and overlines; Inter for text), so the desktop app looks right offline. This is a deliberate departure from WoT HQ's "fonts from Google Fonts".

## Palette (`PaletteDark`)
| Token | Value | Token | Value |
|---|---|---|---|
| Primary / contrast | `#ff8800` / `#121417` | Secondary / contrast | `#6796ea` / `#0b1222` |
| Tertiary / contrast | `#b0916a` / `#121417` | Info | `#3fbfe6` |
| Success | `#4cc77a` | Warning | `#f5c84c` |
| Error | `#ff6470` | Info/Success/Warning/Error contrast | `#121417` |
| Dark / contrast | `#262c33` / `#e6e9ec` | Background / BackgroundGray | `#0e1114` / `#0a0c0f` |
| Surface | `#161a1f` | Appbar background / text | `#14181d` / `#e6e9ec` |
| Text primary / secondary / disabled | `#e6e9ec` / `#a2abb5` / `#5d6670` | Action default / disabled / disabled bg | `#a2abb5` / `#4f5761` / `#262c33` |
| Divider / light | `#2a3139` / `#20262c` | Lines default / inputs | `#343c45` / `#6b7682` |
| Table lines | `#232930` | Table striped / hover | `rgba(255,255,255,.025)` / `rgba(255,255,255,.05)` |
| Overlay dark | `rgba(6,8,10,.75)` | Skeleton | `rgba(255,255,255,.07)` |

CSS-only tokens (`--wot-*` in `wwwroot/css/tokens.css`, the only CSS file with raw colour values): `surface-dialog #1c2127`, `surface-input #222830`, `skipped #8a939c`,
`primary-soft rgba(255,136,0,.14)`, `steel-highlight rgba(255,255,255,.09)`, `bronze-edge #6e5a42`, `chip-bg rgba(176,145,106,.06)`,
`appbar-grain`/`-top`/`-mid`/`-bottom` (app bar gradient), `dialog-bar-top #252b33` (top of the dialog title bar), `rivet-hi`/`-mid`/`-shadow`, `shadow-menu`, `shadow-dialog`, `error-boundary`, and `boot-bg` / `boot-primary` / `boot-text` / `boot-text-secondary` / `boot-lines` / `boot-track` / `boot-glow` / `boot-grid` (the splash in `index.html` and `Splash.razor`, drawn before the theme sets `--mud-palette-*`).

## Type scale
Heading family `"Barlow Condensed", Rajdhani, "Arial Narrow", sans-serif`; text family `Inter, system-ui, sans-serif`.

| Style | Family | Size | Weight | Line height | Letter spacing | Transform |
|---|---|---|---|---|---|---|
| Default, Body1 | Inter | .9375rem | 400 | 1.55 | 0 | none |
| H1 | Barlow Condensed | 3rem | 700 | 1.05 | 0 | none |
| H2 | Barlow Condensed | 2.375rem | 700 | 1.1 | 0 | none |
| H3 | Barlow Condensed | 1.875rem | 600 | 1.15 | 0 | none |
| H4 | Barlow Condensed | 1.5rem | 600 | 1.2 | .005em | none |
| H5 | Barlow Condensed | 1.25rem | 600 | 1.25 | .01em | none |
| H6 | Barlow Condensed | 1.0625rem | 600 | 1.3 | .04em | uppercase |
| Subtitle1 / Subtitle2 | Inter | 1rem / .875rem | 500 / 600 | 1.5 / 1.45 | 0 / .005em | none |
| Body2 | Inter | .8125rem | 400 | 1.5 | .005em | none |
| Button | Barlow Condensed | .9375rem | 600 | 1.2 | .05em | uppercase |
| Caption | Inter | .75rem | 400 | 1.4 | .01em | none |
| Overline | Barlow Condensed | .75rem | 600 | 1.6 | .12em | uppercase |

Numbers in tables, chips and pickers use tabular figures; `.wot-num` does the same elsewhere.

## Shape and space
- Radius 4px; chips 2px; dialogs 6px. No pill shapes except count chips.
- Layout: no drawer and no navigation rail. An app bar of 56px over the page. Window client area about 1280×800, down to 1024 wide. Desktop only, no phone layout.
- Elevation is a 1px hairline (`0 0 0 1px rgba(255,255,255,.06)`) at every level. A real shadow (`0 24px 56px rgba(0,0,0,.6)`) only on dialogs and popovers.
- Dialogs and popovers sit on `surface-dialog`; filled inputs on `surface-input`.
- Dialog title bar (`.wot-dialog-bar`): the app bar's steel (grain over `dialog-bar-top` to `surface-dialog`, top highlight), min height 60, padding `12px 16px 12px 24px`, rivets top-left and bottom-left, the title in `Typo.h5` sentence case (`.wot-dialog-title` sets only the display font), a close X on the right on every dialog except first-run settings. The actions sit under a 1px `divider` line, padding `12px 16px 16px`.
- Focus ring: a 2px primary outline set 2px off the element (no box-shadow, which showed as a dark ring on MudBlazor inputs).

## Where styles live
Every style has one home, chosen by what it styles. Work through the list in this order and stop at the first fit.

| # | Home | What goes there |
|---|---|---|
| 1 | `Theme/WotTheme.cs` | Everything MudTheme can hold: palette, typography, layout properties, shadows. |
| 2 | `wwwroot/css/tokens.css` | Values MudTheme has no slot for: `:root` `--wot-*` custom properties. The only file with raw colour values. |
| 3 | `wwwroot/css/mud.css` | Every rule whose selector names a `.mud-*` class, grouped by MudBlazor component. A component that needs a MudBlazor change gets a `wot-*` modifier class whose rule is here (`.mud-chip.wot-clan-chip`). |
| 4 | `wwwroot/css/wot.css` | Shared `wot-*` pieces used in more than one place or meant to be, with no `.mud-*` in the selector. |
| 5 | `wwwroot/css/gl.css` | The Gevechtslog patterns (below) that no other WoT HQ app uses. |
| 6 | `Component.razor.css` | One component's own elements. |
| (legacy) | `wwwroot/css/app.css` | Temporary: only the rules the screens not yet laid out again (the dialogs, Details) still need (`.Win`/`.Lose`/`.Draw`, the text utilities) and the splash. #3–#9 empty it; its `!important` rules are allowed until then. |

- **Cascade layer.** `wwwroot/css/layers.css` (linked first, because it only imports) declares `@layer mud` and imports `MudBlazor.min.css` into it. Our CSS is unlayered, so it wins over MudBlazor whatever the selector's specificity: no doubled or quadrupled classes.
  `<head>` order: `css/layers.css`, `tokens.css`, `mud.css`, `wot.css`, `gl.css`, `app.css` (legacy, above), then the scoped bundle.
- **`!important`** only in `mud.css`, only where MudBlazor writes an inline style, with a comment naming it. The focus ring in `wot.css` sits in `@layer mud` on purpose: it is a default that MudBlazor's controls override with their own outline.
- **No `.mud-*` in a `.razor.css`**, `::deep` included. MudBlazor's own element does not get the component's scope attribute either, so a modifier class on it is styled in `mud.css`.
- **Colours** in `mud.css`, `wot.css`, `gl.css` and `.razor.css` files are `var(--wot-*)` or `var(--mud-palette-*)`, never raw values.
- **`mud.css` is the upgrade checklist.** It starts with the MudBlazor version it was checked against (9.12.0 today); after a MudBlazor upgrade, walk the file group by group.

## `wot-*` classes (shared with WoT HQ)
Values come from the design canvas's `wot.css`; `mud.css`, `wot.css` and the components' `.razor.css` carry the same numbers on MudBlazor's DOM.
- `.wot-num`, `.wot-dialog-title`, `.wot-dialog-bar` (above).
- `.wot-card-hd` (a `MudCardHeader`): min height 56, padding `14px 16px 12px`. `.wot-divided` (a `MudList`): a 1px `--mud-palette-table-lines` line above every item but the first.
- `.wot-tabs` (`MudTabs`): no band, a 1px `--mud-palette-divider` line under the bar, tabs sized to their text (padding `0 20px`, height 48, icon + label gap 8), the active tab Primary with a 2px underline.
- `.wot-view-toggle` (`MudToggleGroup`): items 36px high, padding `0 14px`, gap 6, unselected text-secondary, selected `--wot-primary-soft` with Primary text.
- `.wot-filter-chip` (`MudChip`): 32px, `1px solid --mud-palette-lines-inputs`, transparent; selected `--wot-primary-soft` with Primary border and text.
- `.wot-grid` (`MudDataGrid`): head row on `--wot-surface-dialog`, text-secondary 600 13px labels, padding `8px 16px`, bottom border `--mud-palette-lines-default`; a grey cell is `CellClass="mud-text-secondary"`.
- `.wot-appbar`: 56px, padding `0 18px 0 16px`, gap 16. Background `repeating-linear-gradient(90deg, rgba(255,255,255,.014) 0 1px, transparent 1px 3px)` over
  `linear-gradient(180deg, #1b2026 0%, #14181d 55%, #111418 100%)`, `border-bottom: 1px solid #343c45`, `inset 0 1px 0 rgba(255,255,255,.09)`.
- `.wot-rivet` and its corner classes `.wot-rivet-tl`/`-tr`/`-bl`/`-br`: 6px, bronze `radial-gradient(circle at 35% 35%, #dcc7a6 0, #b0916a 45%, #6e5a42 100%)`, `0 1px 0 rgba(0,0,0,.65)`, 5px from each corner.
- `.wot-brand` (gap 10; mark 36px, `.wot-brand-mark`), `.wot-wordmark` (Barlow Condensed 700 22px, `.01em`; `WoT` in text-primary, `Gevechtslog` in Primary), `.wot-vsep` (1×28px `#343c45`).
- `.wot-clan-chip`: height 40, padding `0 12px 0 4px` (the 4px is for the emblem; **with the tag only, as here, `0 12px`**), gap 10, `1px solid #6e5a42`, radius 4, `rgba(176,145,106,.06)`. In this app the chip shows the **tag only, no emblem image**. `.wot-clan-tag` 600 17px Barlow Condensed `.03em`.
- `.wot-ubtn` (user button): height 44, gap 10, padding `0 6px 0 4px`, radius 4; `.wot-avatar` 32×32 square (radius 4), `1px solid #6e5a42`, `#262c33`, text Tertiary, 700 14px Barlow Condensed; name 600 14px Inter, role 12px Tertiary, `ExpandMore` chevron `#a2abb5`.
  Menu (`.wot-menu`): 260px wide, pinned 52px from the top and 16px from the right, dialog surface, `1px solid #343c45`, radius 6, padding `6px 0`, `0 16px 40px rgba(0,0,0,.55)`;
  items (`.wot-mitem`, MudMenuItem) 40px min height, padding `0 16px`, 15px, icon `#a2abb5`; the danger item (`.wot-menu-danger`) in the error colour.
- Focus ring (above).

**Not used in this app** (WoT HQ only): reserve colours (`ReserveStyle`), run status (`RunStatusStyle`), the calendar, the navigation drawer and rail, the landing page's HUD hero and the EN | NL switch (`.wot-lang`).

## Gevechtslog patterns (`wwwroot/css/gl.css`, from the canvas's `project/gl.css` and its "New patterns" note)
- **Result chip** (variant Text, tinted): Gewonnen `Success` + `CheckCircle`; Verloren `Error` + `Cancel`; Gelijkspel `Warning` + `Remove`; Onbekend skipped grey (`--wot-skipped`) + `HelpOutline`.
- **Ratio cell with a level icon** (stats mode): the value on the left, the ratio on the right, in columns. Each ratio is its colour **plus** a 16px Filled icon (`arrow_drop_down` low, `remove` mid, `arrow_drop_up` high).

  | Ratio | Red (low) | Green (high) | Between |
  |---|---|---|---|
  | Hit (Geraakt) | ≤ 50 | ≥ 70 | amber |
  | Penetration (Gepenetreerd) | ≤ 55 | ≥ 70 | amber |
  | Block (Geblokt) | — | ≥ 25 | amber below |

- **Day group header**: a sticky header per day with the label (H6), a neutral count chip, a win-percentage status chip and a copy button; in stats mode it carries that day's aggregate stats under the stat columns. **Fixed totals footer** for the whole table.
- **Battle table rows** (`.gr` grid rows, one column template for the header, the day headers, the rows and the footer): hover `table-hover`; alternate rows `table-striped`; the expanded row `.wot-row-active` (`primary-soft` ground and a 2px Primary inset bar); a row the file watcher adds gets `.gl-new`, a 2-second fade from a quiet steel tint (text-primary at 16%, no orange). Your own team is always the left tag, with the clan-chip bronze edge.
- **HP-bar name cell**: the cell filled left to right with `success` at about 25% alpha for the remaining HP; a dead player gets a full-width `error` tint at about 15% plus a 16px Filled `Dangerous` icon, so it is not colour alone.
- **Two-tier sticky-column player table**: two header tiers (groups Prestatie, Schade, Schoten over the sortable labels); the first five columns are sticky on horizontal scroll. The own row uses the row bar (`primary-soft` ground, 2px Primary inset bar) and a Filled `Person` icon, never orange text. A team-killer's name is `info` with a Filled `Report` icon.
- **Filter dropdowns**, **folder carousel**, **reorderable folder list**, **role chip and account menu**, **steel scrollbar**, **snackbar** (outlined dense `Alert` on `surface-dialog`, bottom left, auto-dismiss, lead word · sentence), **progress bar** (4px, hard 2px radius, Primary fill on a `dark` track), **empty state** (48px Outlined icon in text-secondary, H5, one sentence, one action) and the **WTR level icon**. Each is specified by the issue that builds it, taken from the canvas.

## Logo usage
- The app mark is `gevechtslog-mark.svg` (the WoT HQ plate with three rising bars on the tank's hull in place of "HQ", in a gunsight; created by #3). It sits in the app bar at 36px with a 10px gap to the wordmark, and as the window, taskbar and favicon icon. Minimum size 16px; below 32px the bars fade and it reads as a tank in a sight, which is intended.
- The splash screen uses `gevechtslog-hero.svg` (the open reticle without the plate) at 220px, on the HUD ground of the "Splashscherm" board. There is no landing page.
- Clear space around the mark is half the mark's width.
- Never recolour, outline, crop, glow or animate the logo. Never put it on orange or on photos.
- A clan's own Wargaming emblem is never the app logo. The clan chip shows the tag only.

## Do and don't
- Do take status colours, icons and words from the result chip and pair every status colour with its icon and label.
- Do give every icon-only button an `aria-label` and a tooltip, and a visible focus ring on every control. Text meets 4.5:1 on its ground.
- Don't use orange for decoration.
- Don't add a second palette, a light theme or a shadow on cards.
