# Prompt for Claude Design: WoT Gevechtslog

Paste everything below the line into a new Claude Design canvas. Attach or link the two references first.

- Design system: https://claude.ai/artifact/MnaxNrvDVemYHNzsu5rBQD (WoT HQ)
- Reference canvas built from it: https://claude.ai/artifact/EGzACdgttfqT2bjKChG46e (WoT HQ site; look at its AppBar, Members, History, States, NoAccess, SettingsA/B and LogoFinal boards for patterns to follow)

---

Design **WoT Gevechtslog**, the Windows desktop app that reads World of Tanks `.wotreplay` files and lists the battles in a table. It is a sibling product of **WoT HQ**: same design system, same logo family, its own name and mark. Use the WoT HQ design system for every colour, type style, radius, spacing step and component. Install it on the canvas so the Theme menu works. Never invent tokens. Use stock design-system components plus its `wot-*` classes, and do not hand-roll look-alikes. Where this app needs a pattern the system lacks (listed under "New patterns"), build it only from existing tokens and say so in your reply.

## Product

- **Users:** members of the Dutch/Belgian clan NED, between battles or after a clan match. They want to check clan results fast and compare players' stats.
- **Window:** the app's client area only, about 1280 × 800, resizable down to 1024 wide. Desktop only, no phone layout. Do not draw an OS title bar.
- **Language:** Dutch UI, *je*-form, sentence case, verbs as button labels, WoT's own English names for maps and tanks. Dates `dd-MM-yyyy`, time `HH:mm`, tabular figures wherever numbers align.
- **Dark only.** There is no light theme and no theme toggle.
- **Mood:** armoured, tactical, calm. One hot accent. Edges instead of shadows. Metal texture and rivets only on the app bar and dialog title bars.

## Name and logo

- Name: **WoT Gevechtslog** ("battle log"). Wordmark: `WoT` in `text-primary`, `Gevechtslog` in `primary`, Barlow Condensed 700, 22px, using `.wot-brand` and `.wot-wordmark`.
- Mark: the WoT HQ plate, bronze edge, gunsight and orange tank, with **three rising bars** on the hull in place of the HQ letters (hit, penetration and block ratios). Create it as `gevechtslog-mark.svg` from this exact source and upload it as an asset. Do not redraw it:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64" role="img" aria-label="WoT Gevechtslog"><title>WoT Gevechtslog</title><path d="M14 3H50L61 14V50L50 61H14L3 50V14Z" fill="#1c2127" stroke="#b0916a" stroke-width="2.5"/><circle cx="32" cy="32" r="19" fill="none" stroke="#a2abb5" stroke-width="3"/><rect x="30.5" y="7.5" width="3" height="9" fill="#a2abb5"/><rect x="30.5" y="47.5" width="3" height="9" fill="#a2abb5"/><rect x="7.5" y="30.5" width="9" height="3" fill="#a2abb5"/><rect x="47.5" y="30.5" width="9" height="3" fill="#a2abb5"/><g transform="translate(32 33) scale(0.78) translate(-32.8 -35)"><path d="M14 38L18.5 32H45.5L50 38L45.5 44H18.5Z" fill="#ff8800"/><path d="M24 32L26.5 26H37.5L40 32Z" fill="#ff8800"/><rect x="38.5" y="27.6" width="11.5" height="2.2" fill="#ff8800"/><rect x="49" y="26.8" width="2.6" height="3.8" fill="#ff8800"/><g transform="translate(32 38) scale(0.36) translate(-33 -33.5)" fill="#121417"><rect x="17" y="33" width="8" height="12"/><rect x="29" y="27" width="8" height="18"/><rect x="41" y="21" width="8" height="24"/></g></g></svg>
```

- Use it in the app bar (36px, 10px gap to the wordmark), the splash screen (160px), the window and taskbar icon (shown at 48/32/24/16px) and the favicon. Same rules as the HQ logo: half the mark's width in clear space, never recoloured, outlined or glowing, never on orange. Below 32px the bars fade and it reads as a tank in a sight, which is intended.
- There is no hero variant and no landing page.
- The clan's tag is a **clan chip** (`.wot-clan-chip`, tag only, no emblem image) in the app bar. Never put it in place of the mark.

## Deliverables

Make **one interactive prototype** (`is_interactive`) of the main window, then static boards for every state. Each board is 1280 × 800 unless noted. Use realistic Dutch sample data (see the end).

**Prototype `Main`** with working behaviour: expanding and collapsing battle rows, the match-type toggle, the search, the result chips filtering rows, the stats toggle, sorting by clicking column headers, copy buttons showing a snackbar, and every dialog below opening and closing.

**Static boards:**
1. Main, default: match type "Clan", 3 day groups, one battle expanded to its player details, stats off.
2. Main, stats mode on, with the player-focus filter set to one player.
3. Main, loading: skeleton rows, shimmer, live battle counter.
4. Empty states, three variants on one 1280 × 800 board: replay folder missing, no battles since the chosen date, filters match nothing.
5. Battle details close-up (board 1280 × 900): the player table with both teams, one collapsed.
6. Settings dialog, normal; and first-run variant (no Annuleren button, no close button, warning snackbar behind the scrim).
7. Clan match dialog (the auto-opened one).
8. Clan members dialog.
9. Unknown names dialog.
10. Update dialog, plus the app bar with the update button visible.
11. No-access page.
12. Splash screen.
13. Snackbars (all severities), a table row for a replay that failed to parse, and the global error bar.
14. Logo board: mark on the plate, app bar lockup, size ramp 48/32/24/16, splash, window icon.

## Window layout

Top to bottom, no side drawer (it is a single-page app):

1. **App bar** (`AppBar`, metal texture, rivets, 56px): mark + wordmark, clan chip with the configured tag (e.g. `[NED-1]`), spacer, then on the right: **Update beschikbaar** (outlined button with `Update` icon, only when an update exists), **Vernieuwen** (`Refresh` icon button), **Instellingen** (`Settings` icon button). Icon-only buttons have `aria-label` and a tooltip.
2. **Page header:** H3 "Gevechtsresultaten", caption "Gevechten sinds 26-10-2026" (the date from settings).
3. **Summary card** (`Card`, `wot-accent-edge`, the one accent card on the screen). It replaces the old sticky footer and the totals in each group.
   - Overline "Overzicht". Key number: **win percentage** in `wot-num-xl` (the single orange number), coloured by band only through its status chip beside it, not by changing the orange.
   - Record: four count chips with icon, word and count: Gewonnen, Gelijkspel, Verloren, Onbekend.
   - Total battles ("48 gevechten") and a quiet caption "Geladen in 2,4 s".
   - In stats mode it adds the four aggregate stats: Geschoten (with p/m), Geraakt %, Gepenetreerd %, Geblokt/ontvangen %.
   - It reflects the match-type and search filters. The result chips below do not change it.
4. **Filter toolbar** (one row that wraps, on `surface`, steel edge):
   - Search: outlined, dense autocomplete, placeholder "Zoek op spelernaam of clantag", `Search` adornment, clearable. Each option shows the value on the left and a caption "Speler" or "Clan" on the right.
   - **Match type**: `ToggleGroup`, exclusive: Alles | Normaal | Clan | Onbekend. Default Clan.
   - **Result filter**: four selectable chips, multi-select, all on by default: Gewonnen, Gelijkspel, Verloren, Onbekend, each with its count. Off chips look clearly off (text style, no tint).
   - Right-aligned: **Statistieken** switch with `AutoGraph` icon, shown only to users who may see battle stats. When on, a "Filter spelerstatistieken" autocomplete appears next to it.
   - **Clanleden** icon button (`Groups` outlined) with a neutral count chip.
   - **Onbekende namen** warning chip with count, shown only when unresolved map or tank names exist. It opens the dialog.
5. **Battle table** (`DataGrid`, dense, striped, hover, sticky header, fills the remaining height, virtualised). Grouped by day, with a sticky group header per day. Footer: none.

### Day group header

Label "Vandaag", "Gisteren" or "3 dagen geleden" (H6 style) plus a neutral count chip ("5 gevechten"), a win-percentage status chip (icon + word + number), a read-only caption "3 gewonnen · 1 gelijkspel · 1 verloren", and on the right an outlined `ContentCopy` icon button "Kopieer opstellingen van deze dag" (disabled when the group is empty). A battle starting before 02:00 belongs to the previous day. In stats mode the header also carries that day's aggregate stats, aligned under the stat columns. The header does **not** contain result toggles.

### Table columns

| Column | Content | Notes |
|---|---|---|
| (chevron) | `KeyboardArrowRight` / `KeyboardArrowDown` | The header cell holds a collapse-all button (`UnfoldLess`) with tooltip "Alles inklappen". Whole row is clickable. |
| # | Position within the day, oldest = 1 | Numeric, centred. The old trailing duplicate `#` column is removed. |
| Datum | `05-11-2026 20:14 – 20:31` | Sortable. |
| Duur | `14m 05s` | Right-aligned, tabular, sortable. |
| Map | Map name + base chip `I` or `II` | Sortable. |
| VS | `[NED-1]  7  –  4  [DUTCH]`, then own result chip + copy button | See below. |
| Geschoten, Geraakt, Gepenetreerd, Geblokt | Only in stats mode | See below. |

**VS cell:** both clan tags (`?` when a team has no dominant clan), and the number of tanks each side destroyed, tabular, the winner's number in weight 600. Tags are coloured by that team's result when "Alleen eigen gevechten uitlichten" is off; when it is on, only your own team's tag is highlighted and the other is neutral. Your own team's tag takes the clan-chip bronze edge. After the score comes your result as a status chip (icon + word) and an outlined `ContentCopy` icon button "Kopieer opstelling" (disabled when your team is unknown).

**Result chips** (new, built on the Status pattern): Gewonnen = `Color.Success` + `CheckCircle`; Verloren = `Color.Error` + `Cancel`; Gelijkspel = `Color.Warning` + `Remove`; Onbekend = skipped grey + `HelpOutline`. Variant Text (tinted).

**Stat cells** (stats mode): value on the left, ratio on the right, aligned in columns.
- Geschoten: count + `12 p/m` (shots per minute, plain `text-secondary`; the old `<`/`>` arrow is dropped).
- Geraakt: count + hit %. Red at or below 50, green at 70 or above, amber between.
- Gepenetreerd: count + pen %. Red at or below 55, green at 70 or above, amber between.
- Geblokt: `blocked / received` + block %. Green at 25 or above, amber below.
- Each ratio is its colour **plus** a 16px Filled icon (`arrow_drop_down` low, `remove` mid, `arrow_drop_up` high) so colour is never the only cue. A legend sits in the stats toggle's tooltip.

**States of a row:** default, hover (`table-hover`), expanded (`wot-row-active`: `primary-soft` ground and 2px orange inset bar), loading (skeleton), error. An error row is one `Alert` warning spanning all columns with the file name and message ("Kan bestand replay_20261105_2014.wotreplay niet lezen"). Optional setting "Maximaal één gevechtsresultaat open houden" makes opening one row collapse the rest, and the opened row scrolls into view.

### Battle details (expanded row)

A panel on `surface` with steel edge and 16px padding, one player table, grouped by team with a collapsible team header (`ExpansionPanel` style). Your own team starts open and the other starts collapsed. Team header: tag (`?` if unknown) in bold, coloured by result, plus that team's result chip.

Columns, in two header tiers: first row groups **Prestatie** (XP, Kills), **Schade** (Gedaan, Geblokt, Ontvangen), **Schoten** (Geschoten, Geraakt, Gepenetreerd); second row has the sortable labels.
`#` | Naam (`[CLAN] Naam`) | Tank | Speeltijd (`14m 05s`) | XP | Kills | Gedaan | Geblokt | Ontvangen | Geschoten | Geraakt | Gepenetreerd.
- The first five columns are sticky on horizontal scroll. The rest scroll.
- Default sort: XP descending within each team. Numbers right-aligned, tabular, with a thin space as thousands separator (`1 230`).
- **HP bar:** the name cell is filled left to right with `success` at about 25% alpha for the player's remaining HP percentage. A dead player gets a full-width `error` tint at about 15%, plus a 16px Filled `Dangerous` icon before the name, so it is not colour alone. A tooltip gives "Pantser 340 / 1 850 HP · Leefde 6m 12s".
- **Your own player:** the row uses `wot-row-active` (soft orange ground, 2px bar), not orange text, to keep within the one-accent rule, plus a Filled `Person` icon. Matching is by the player name from settings.
- **Team-killer:** name in `info` plus a Filled `Report` icon and tooltip "Teamkiller".
- In stats mode the Geblokt, Geschoten, Geraakt and Gepenetreerd cells add their ratio exactly as above. In normal mode Geblokt shows damage and blocked shots only.

## Dialogs

All dialogs: `Dialog` with the metal title bar and rivets, `surface-dialog`, `radius-lg`, scrim `overlay-dark`, Escape closes (except first-run settings), footer Cancel as text then Confirm as filled primary on the right. Title in H4 with a 20px Outlined icon.

1. **Instellingen** (medium, no backdrop close, no close button). Three sections under Overline headings:
   - *Replays*: "Pad naar WoT-replays" (outlined text field, `FolderOpen` adornment, required, must exist: "Replaypad is verplicht" / "Replaypad bestaat niet"), "Laad gevechten sinds" (date picker, `dd-MM-yyyy`, Monday first, required).
   - *Identiteit*: "Clanafkorting", "Spelernaam" (helper: "Hiermee herkent de app jouw team").
   - *Gedrag*, four `Switch` rows with a label and a one-line helper each: Maximaal één gevechtsresultaat open houden; Alleen eigen gevechten uitlichten; Scherm bijwerken tijdens het laden; Clan automatisch opzoeken bij de start van een gevecht.
   - Footer: left-aligned text button "Ga naar app-instellingen" (`OpenInNew`), then Annuleren (text; absent on first run), then **Opslaan** (filled primary). Changing the replay folder reloads all battles. On save, show a success snackbar "Instellingen opgeslagen".
2. **Clan gevecht** (large, opens by itself when a clan match starts, closes by itself when its replay file appears). Title: `[NED-1]  vs  [DUTCH]` (H4 tags, H5 "vs"). Summary row of three count chips (Gewonnen, Gelijkspel, Verloren) for earlier meetings. Two panels side by side, as `Tabs` ("Tegenstanders", "Eerdere gevechten") below 1100px:
   - Tegenstanders: `#` | Gevechten | Speler | Tanks (each tank with its share, e.g. `E 100  60%`). Sorted by battles descending.
   - Eerdere gevechten: Datum | Duur | Map + base | Winnaar (tag, coloured by your result).
   Footer: **Sluiten**.
3. **Clanleden (n)** (small, full width): table Speler | Gevechten, sortable, name ascending by default, fixed header, scrolls at 600px. Footer: Sluiten.
4. **Onbekende namen**: two read-only lists under Overline headings "Onbekende mapnamen" and "Onbekende tanknamen", divided by a `divider`. Footer: Sluiten. Short helper at the top: "Deze namen staan nog niet in de naamlijst van de app."
5. **Update** (small, no backdrop close): `Update` icon, "Er is een nieuwe versie van de applicatie beschikbaar.", Negeren (text) and **Herstarten** (filled primary).

## Other screens

- **Geen toegang:** centred card on `background-gray` well: mark, H3 "Geen toegang", body "Vraag toegang aan op de [NED] Discord-server in het #wot_chat-kanaal." with the two links in `secondary`, filled primary **Opnieuw proberen** (the only orange on that screen).
- **Splash:** `background`, mark 160px, wordmark, a 4px indeterminate progress bar with a hard 2px radius, orange fill on a `dark` track (the one orange moving thing). Caption "Gevechten laden…".
- **Empty states** (each: Outlined icon at 48px in `text-secondary`, H5, one body sentence, one action): replay folder missing ("Replaymap niet gevonden", action Instellingen openen); no battles ("Geen gevechten sinds 26-10-2026", action "Datum aanpassen"); filters match nothing ("Geen gevechten gevonden", action "Filters wissen").
- **Snackbars** (`Alert` outlined dense on `surface-dialog` with the dialog edge, bottom left, auto-dismiss, lead word + sentence): Info "Match gestart · NED-1 tegen DUTCH"; Success "Toegevoegd · replay_20261105_2014.wotreplay", "Gekopieerd · opstelling staat op het klembord", "Opgeslagen · instellingen zijn bewaard"; Warning "Verwijderd · replay_20261104_2231.wotreplay is niet meer aanwezig", "Replaymap · de map is niet ingevuld of bestaat niet"; Error "Bestandsbewaking · er ging iets mis, zie details".
- **Global error bar** (bottom, full width, `Alert` filled error with dark label): "Er is een onverwachte fout opgetreden." with a text button **Herladen** and a `Close` icon button.

## Behaviour and data rules the design must support

- Battles load in parallel from a folder; with "Scherm bijwerken tijdens het laden" on, rows stream in and the counter ticks. A file watcher adds and removes rows live, so show a row appearing and a quiet highlight for 2 seconds.
- Result of a battle = your team's result. Your team is the one whose clan tag equals the configured tag **or** that contains the configured player name. If neither matches, the result is Onbekend.
- Team tag = the most common clan tag among its players, when at least `max(half the team, 4)` share it, otherwise `?`. "Normaal" means both tags are `?`; "Clan" means at least one is not.
- Search matches an exact player name or clan tag in either team.
- Copying a battle gives the map + base, the result and your players by XP; copying a day puts those battles side by side, tab separated. Every copy shows the "Gekopieerd" snackbar.
- Nothing is hard-truncated except long player names (ellipsis, full name in a tooltip). Never truncate a button label or column header.

## Rules from the design system to keep

- One filled primary button per view. One accent card per screen. At most about five orange things per screen: key number, selected toggle item, expanded-row bar, own-player row bar, focus ring.
- Status is always icon + word + colour. Never colour alone, never red against green by hue alone.
- Text 4.5:1 on its ground, focus ring on every control, `aria-label` on every icon-only button, real `<button>` and `<input>` elements.
- 4px radius, 2px for chips, 6px for dialogs. No pills except count chips. Material icons only (Filled for state and identity, Outlined for neutral actions). No emoji, no other icon set.
- Do not use the reserve-type colours, the HUD hero, the calendar blocks or the drawer; this app is not the reserves module.
- Do not add features beyond those described. Rationale goes in your reply, not on the boards.

## New patterns to build from existing tokens (list them in your reply)

Result chip; ratio cell with level icon; day group header; HP-bar name cell; two-tier sticky-column player table; summary card with key number; filter toolbar with selectable result chips; snackbar; progress bar; empty state.

## Sample data

**Settings:** clan `NED-1`, player `Tankgeneraal`, folder `C:\Games\World_of_Tanks_EU\replays`, since `26-10-2026`. Today is `05-11-2026`.

**Battles** (own team first; result is yours; kills are `own–enemy`):

| Day | Start | Duur | Map | Base | Teams | Kills | Result |
|---|---|---|---|---|---|---|---|
| Vandaag | 21:02 | 11m 48s | Prokhorovka | I | NED-1 vs RHINE | 15–9 | Gewonnen |
| Vandaag | 20:31 | 14m 05s | Himmelsdorf | II | DUTCH vs NED-1 | 6–15 | Gewonnen |
| Vandaag | 20:05 | 08m 52s | Lakeville | I | NED-1 vs ZULU | 4–15 | Verloren |
| Vandaag | 19:40 | 15m 00s | Malinovka | II | ? vs NED-1 | 11–11 | Gelijkspel |
| Vandaag | 19:12 | 10m 30s | Mines | I | NED-1 vs ? | 15–12 | Gewonnen |
| Gisteren | 22:18 | 12m 40s | Ruinberg | I | NED-1 vs RHINE | 15–14 | Gewonnen |
| Gisteren | 21:44 | 09m 17s | Fjords | II | ZULU vs NED-1 | 15–5 | Verloren |
| Gisteren | 21:02 | 13m 22s | Steppes | I | NED-1 vs DUTCH | 15–7 | Gewonnen |
| Gisteren | 01:15 | 07m 03s | Abbey | II | ? vs ? | 15–3 | Onbekend (counts as Gisteren's previous day: "2 dagen geleden") |
| 3 dagen geleden | 20:55 | 14m 31s | Prokhorovka | II | RHINE vs NED-1 | 8–15 | Gewonnen |

**Expanded battle:** the first row above, 15 players per team, fictional names (e.g. Tankgeneraal, BrabantseBeer, DeVeenmeester, KoningKanon, IJzerenHein, Stalen_Tulp), clan tag in front of every name. Tanks from: E 100, Object 140, T-62A, Leopard 1, Bat.-Châtillon 25 t, Centurion Action X, Strv 103B, Grille 15, Kranvagn, Progetto M40 mod. 65, Foch 155, Chieftain/T95. Sort by XP descending. 5 to 7 players dead per team, the rest with a mix of HP bars. XP 300–1 900, damage dealt 0–4 200, Geschoten 4–35. Make the ratios span red, amber and green so every state shows. One team-killer on the losing side.

**Clan members dialog:** 24 names, Gevechten 3–41. **Clan match:** NED-1 vs DUTCH, 3 earlier meetings (2 gewonnen, 1 verloren), 14 opponents, 2–3 tanks each.

**Unknown names:** maps `Navy Base`, `Test Range`; tanks `Skoda T 56 EVO`, `WZ-111 Alpine Tiger`.

**Access:** `Geen toegang` page links to the [NED] Discord server and the #wot_chat channel.

## Reply format

After the canvas is built: the link, one line on what you made, a short list of the new patterns, and any assumption you made. Do not render, screenshot or verify unless I ask.
