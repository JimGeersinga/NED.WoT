# WoT Gevechtslog — plan of record

The desktop app that reads World of Tanks `.wotreplay` files and lists the
battles in a table. It is a sibling of WoT HQ (`JimGeersinga/NED.WoT.Clan`).
CLAUDE.md holds the rules for working in the repo; docs/DESIGN.md holds the look.
A path marked "created by #N" does not exist yet.

## 1. Decisions taken

| Topic | Decision |
|---|---|
| Name | WoT Gevechtslog, a sibling of WoT HQ. `ApplicationId` and the package identity stay the same, so installed copies keep updating. |
| Look | The WoT HQ design system, dark only. The canvas "WoT Gevechtslog" (https://claude.ai/artifact/Xi6h8kaLxdDjXZLHoH7Fro) is the look of record; the design system is https://claude.ai/artifact/MnaxNrvDVemYHNzsu5rBQD. |
| Sign-in | Wargaming OpenID in the system browser with a loopback redirect, under the app's own WG application (Mobile/Standalone). The token is kept in `SecureStorage` and prolonged at start when it expires within 72 h. |
| Rights | From WoT HQ (`POST /api/gevechtslog/session`, `JimGeersinga/NED.WoT.Clan#149`): the app sends the Wargaming token at every start (and daily while open); HQ verifies it and answers identity, clan, role, `canUse` and `canSeeAdvancedStats`. **HQ down means no access** (owner, 2026-11-09). |
| Identity | Sign in with Wargaming (§3.2), then HQ's answer (§3.3) gives nickname, clan tag, name, emblem, role and rights; none of it comes from Settings. |
| Replay folders | Several folders (title + path) and a carousel in the app bar. Only the selected folder is read, watched and held in memory; switching folders drops the old one's battles and reads the new one from disk. |
| Live battle | One temp-replay path setting, separate from the folders. |
| Ratings | PR, WTR (`account/wtr`) and clan rating in the Clan battle dialog, for everyone with access, from Wargaming's public API under the app's own application id. |
| Tests | No test project. Acceptance is checked by hand and with screenshots (CLAUDE.md, "Screenshots instead of UI tests"). |
| Build | The .NET recommended analyzers run (`AnalysisLevel` `latest-recommended`) and any warning fails the build (`TreatWarningsAsErrors`); decided by the owner on 2026-11-07. The other Recur build rules stay out (CLAUDE.md). The SDK is pinned to 11.0.x by `global.json`, the same band CI uses. |

## 2. Project layout (as it is)

One MAUI Blazor Hybrid project, `NED.WoT.BattleResults.Client/` (target
`net11.0-windows10.0.19041.0`, MSIX, UI in MudBlazor). `NED.WoT.BattleResults.Core/`
is a second project in `NED.WoT.slnx`.

| Folder | Holds |
|---|---|
| `Components/Layout` | `MainLayout.razor` (app bar and page frame), `AccountMenu`, `FolderCarousel`, `Splash`, `UpdateDialog` |
| `Components/Pages` | `Index.razor` (the battle table), `Login.razor`, `Unauthorized.razor` |
| `Components/Battles` | the battle table and what it shows: `BattleFilterBar`, `BattleRow`, `BattleGroupHeader`, `BattleTotals`, `BattleRecord`, `Details`, `RatioCell`, `EmptyState`, `UndefinedNamesDialog` |
| `Components/Clan` | `ClanMatchDialog`, `ClanMembersDialog` |
| `Components/Setup` | `SettingsDialog` |
| `Components/Shared` | `WotDialogTitle`, `ResultChip`, `NumberFormat`: used by more than one feature |
| `Services` | `BattleReportService`, `BattleReportMapper`, `SettingService`, `HqAuthenticationStateProvider`, `UpdateCheckService`, `MapNameResolver`, `TankNameResolver`, `ColorHelper` |
| `Models` | `Settings`, `AppSettings`, `BattleReport` and its event args |
| `Attributes` | `DirectoryExistsAttribute` |
| `Platforms/Windows` | `App.xaml(.cs)`, `Package.appxmanifest`, `app.manifest` |
| `wwwroot` | `index.html`, `css/app.css`, `css/fontawesome/`, `favicon.ico` |
| `Resources` | `AppIcon/`, `Raw/` |

New folders are created by the issue that needs them and named in its **Files**
section (e.g. `Theme/` and `wwwroot/fonts/` by #2).

## 3. Wargaming and the Clan API

### 3.1 The app's own Wargaming application
Registered at developers.wargaming.net as **Mobile/Standalone** (no IP lock; the
app calls Wargaming from the player's own machine). Its `application_id` is not
a secret and ships with the app (created by #10). It is a different application
from WoT HQ's.

### 3.2 Sign-in (created by #10)
1. The app opens `https://api.worldoftanks.eu/wot/auth/login/` in the system
   browser with `application_id`, a `redirect_uri` of `http://localhost:<port>/…`
   served by a loopback listener in the app, a random `state`, and
   `expires_at`. Wargaming's firewall blocks a redirect_uri with a literal IP (checked 2026-11-09), so the loopback listener uses `localhost`.
2. The listener accepts one request, checks `state`, reads `status`,
   `access_token`, `expires_at`, `account_id`, `nickname`, then shuts down.
3. **The query values are not trusted.** The token is verified by a POST to
   `account/info/` (form: `access_token`, `account_id`) before anything is
   stored; the nickname is taken from that answer. The call asks `fields=nickname,private`: `private` is required, and is only returned when the token belongs to that account **(verify)**. Checked 2026-11-09: a loopback
   `redirect_uri` with a port is accepted for application
   `953ad65cc3872f5a909dc1921ba5acf7`, and a bad token answers HTTP 200 with
   `INVALID_ACCESS_TOKEN` (whose `value` echoes the token).
4. The token and its expiry go to `SecureStorage`. At start the app calls
   `POST auth/prolongate/` when the token expires within 72 h and stores the new
   token at once (the old one is dead immediately). On `INVALID_ACCESS_TOKEN` the
   app asks the player to sign in again.
5. Sign-out forgets the token locally. `auth/logout` is never called.

### 3.3 Rights and identity (created by #75; the login screen by #11)
The app logs in itself (§3.2) and then sends the verified token to
`POST {HqBaseUrl}api/gevechtslog/session` (form: `account_id`, `access_token`,
HTTPS only). HQ verifies the token under its own `Gevechtslog:ApplicationId`
(the app's application id `953ad65cc3872f5a909dc1921ba5acf7`, a separate id from
HQ's reserves application) and answers `{ accountId, nickname, clanId, clanTag,
clanName, emblemUrl, role, canUse, canSeeAdvancedStats }`; 401 means the token is
invalid (sign in again), 503 that Wargaming is unreachable. The app trusts the
answer and never decides rights itself. Nothing is kept on the HQ side. HQ down,
or any other failure to get an answer, shows "Geen verbinding met WoT HQ".

### 3.4 Public reads (created by #18)
`Services/WargamingApiClient.cs`: GET, no token, under the app's own application id, for the Clan battle dialog.
Failures (Wargaming error, timeout, no network) are a failed result, never an exception (caller cancellation is the
one exception, rethrown). 10 s per method call, retries included; one retry after 1 s on `REQUEST_LIMIT_EXCEEDED`.
The error's `value` is never deserialized. Checked against the live EU API on 2026-11-12:

- `account/list/?search=<n1,n2,...>&type=exact&limit=100` gives `data[].account_id`, `.nickname`. A comma-separated
  exact search of 86 names (a whole clan) returned all 86; unknown names are simply absent. Matching is case-insensitive.
- `account/info/?account_id=<ids>&fields=global_rating` gives `data[id].global_rating` (the PR). An unknown id gives
  `data[id] = null`. Hidden stats are assumed to give the same `null`, which the client maps to `StatsHidden`, but this is
  **not verified**: none of the 86 NED-1 members hides their stats and no hidden account was found. Only a `null` entry
  counts as hidden; an object without `global_rating` fails the call.
- `clans/list/?search=<tag>&limit=100&fields=clan_id,tag,name,emblems.x64` is a prefix search: the client picks the entry
  whose `tag` equals the tag. NED-1 gives 500012147, "Noobs Elite Divisie", `emblems.x64.portal`. No match gives `data: []`.
  Short tags: `NED` (295 prefix matches), `ABC` (519) and `AA` (13348) each return 100 entries on page 1, with the exact tag
  first in all three. The default limit is already 100. For a very short, common tag the exact one could still fall off the
  first page; the client does not page on.
- `clanratings/clans/?clan_id=<id>&fields=efficiency` gives `data[id].efficiency.value` and `.rank` (NED-1: 7261, rank 643).
  An unrated or unknown clan gives `data[id] = null`. **(verify)** that `efficiency.value` can be null: not seen; the client
  gives a null rating, rank and emblem when `efficiency`, its `value` or `rank`, or `emblems.x64` is missing or null.
  Other keys are not used.
- WTR exists in the public API (found 2026-11-12; the 2026-11-09 check was wrong): `GET account/wtr/?account_id=<up to 100 ids>` → `data[id] = { rating, account_id }`, or `null` for an unknown id **or an account without a WTR** (live 2026-11-12: real accounts with battles, e.g. one with 3020 battles, also return `null`; ids 1 and 2 are `null`; no key was ever missing) (101 ids → `ACCOUNT_ID_LIST_LIMIT_EXCEEDED`). Shown next to PR, with a level icon: limits `[0, 400, 1000, 1400, 1900, 2700, 3200, 3900, 4800, 5400, 6200, 7300, 8000, 8800, 9900]` = Brons I–III, Zilver I–III, Goud I–III, Aas I–III, Legende I–III. **(verify)** what `account/info` returns for a player who hides their stats (not seen yet; expected `null`, which the app shows as hidden). WTR is optional: a failed `account/wtr` call, or a missing entry, only leaves WTR empty, and PR still shows; `account/list` and `account/info` failures fail the whole result.

## 4. Settings

Today every setting is a `Preferences.Default` key named after a property of
`Models/Settings.cs`, read and written by `Services/SettingService.cs`.

| Key (`Preferences`) | Holds | Fate |
|---|---|---|
| `ReplayFolders` | The replay folders in app-bar order, as JSON `[{"Title","Path"}]` (added by #15) | Stays |
| `WotReplayDirectory` | The single replay folder path before #15 | Read once to make the folder "Replays" when `ReplayFolders` is absent; never written again |
| `LoadBattlesSince` | The oldest battle date to load, as ticks | Removed by #71; every replay in the folder loads, the stored value is ignored |
| `ClanAbbreviation` | The clan tag used to find the player's team | Removed by #12: read from `IdentityService.Current.ClanTag` (the login) |
| `PlayerName` | The player name used to find the player's team | Removed by #12: read from `IdentityService.Current.Nickname` (the login) |
| `SingleBattleResultOpenedOnly` | Opening a battle collapses the others | Stays |
| `OnlyHighlistOwnMatches` | Highlight only the own team's result | Stays |
| `UpdateScreeenWhileLoading` | Rows stream in while loading | Stays |
| `AutoClanLookup` | Open the Clan battle dialog when a clan match starts | Stays |
| `IsDarkMode` | The light/dark choice | Removed by #2 (dark only) |

New keys are added by the issue that needs them and listed here in the same PR.
The Wargaming token is **not** a `Preferences` key; it is in `SecureStorage`
(§3.2). Names and values of folder and temp-replay settings are fixed by #15 and
#17.

## 5. Milestones

Titles match the GitHub milestones one to one.

| # | Milestone | Issues | What it delivers |
|---|---|---|---|
| 00 | Working agreements | #1, #21, #23, #25 | CLAUDE.md, this plan and docs/DESIGN.md; a manual release workflow and a version bump in every PR. |
| 01 | WoT Gevechtslog look and feel | #2–#9 | The WoT HQ design system on today's screens: theme, logo, app bar, battle table, stats mode, details, dialogs and every state. |
| 02 | Filter dropdowns | #13–#14 | Clan, Speler, Map and Resultaat dropdowns and the Clangevechten \| Alles switch. |
| 03 | Sign in with Wargaming | #10–#12, #75 | Wargaming login in the desktop app (#10, #11); access and identity from WoT HQ replace the Excel check (#75); the account menu and the removal of the clan and player Settings fields (#12). |
| 04 | Replay folders | #15–#16 | Several replay folders in Settings and a folder carousel in the app bar. |
| 05 | Live battle | #17 | A separate temp-replay path for the live clan lookup. |
| 06 | Clan battle statistics | #18–#19 | Clan rating and PR in the Clan battle dialog, from Wargaming's public API. |
| 07 | Speed and memory | #41–#45 | A reload frees the old battle list; replays are read by block and parsed into a typed model; analyzers run as errors. |
| 08 | Fewer MudBlazor overrides | #54–#60 | Hand-made buttons, icons, chips and menu rows become MudBlazor components; unused and leaking CSS goes; shadows move into the theme. |
| 09 | Teams | #65–#70 | Per-team statistics: lobbies grouped into teams, a team filter and results per map per team. |
| 10 | .NET 11 | #78 | Upgrade to .NET 11. |
| 11 | Component structure | #108–#114 | Components in feature folders; `Index`, the Clan battle dialog, the filter bar and `Details` split so each has one job. |

Order inside and across milestones follows each issue's `Depends on:` line
(CLAUDE.md), not the table.

The numbers stay, but the work order is 07 → 04 → 03 (owner, 2026-11-07), set
through the `Depends on:` lines of #15 and #10.

Milestone 08 goes first (owner, 2026-11-07), set through the `Depends on:` lines of #10, #17, #31, #32, #34 and #38.
