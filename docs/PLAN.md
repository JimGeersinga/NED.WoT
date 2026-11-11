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
| Identity | After the access check the player must sign in with Wargaming. Nickname, clan tag, name and emblem and role come from HQ's answer (#75), not from Settings. Until #75, #11 looks the clan up itself with `clans/accountinfo`. |
| Replay folders | Several folders (title + path) and a carousel in the app bar. Only the selected folder is read, watched and held in memory; switching folders drops the old one's battles and reads the new one from disk. |
| Live battle | One temp-replay path setting, separate from the folders. |
| Ratings | PR and clan rating (no WTR: the public API has none) in the Clan battle dialog, for everyone with access, from Wargaming's public API under the app's own application id. |
| Tests | No test project. Acceptance is checked by hand and with screenshots (CLAUDE.md, "Screenshots instead of UI tests"). |
| Build | The .NET recommended analyzers run (`AnalysisLevel` `latest-recommended`) and any warning fails the build (`TreatWarningsAsErrors`); decided by the owner on 2026-11-07. The other Recur build rules stay out (CLAUDE.md). The SDK is pinned to 10.0.x by `global.json`, the same band CI uses. |

## 2. Project layout (as it is)

One MAUI Blazor Hybrid project, `NED.WoT.BattleResults.Client/` (target
`net10.0-windows10.0.19041.0`, MSIX, UI in MudBlazor). `NED.WoT.BattleResults.Core/`
is a second project in `NED.WoT.slnx`.

| Folder | Holds |
|---|---|
| `Components/Layout` | `MainLayout.razor` (app bar and page frame) |
| `Components/Pages` | `Index.razor` (the battle table), `Unauthorized.razor` |
| `Components/Shared` | `Details.razor` and the dialogs: `SettingsDialog`, `ClanMatchDialog`, `ClanMembersDialog`, `UndefinedNamesDialog`, `UpdateDialog` |
| `Services` | `BattleReportService`, `BattleReportMapper`, `SettingService`, `CustomAuthenticationStateProvider`, `UpdateCheckService`, `MapNameResolver`, `TankNameResolver`, `ColorHelper` |
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
`POST {ClanApiBaseUrl}api/gevechtslog/session` (form: `account_id`, `access_token`,
HTTPS only). HQ verifies the token under its own `Gevechtslog:ApplicationId`
(the app's application id `953ad65cc3872f5a909dc1921ba5acf7`, a separate id from
HQ's reserves application) and answers `{ accountId, nickname, clanId, clanTag,
clanName, emblemUrl, role, canUse, canSeeAdvancedStats }`; 401 means the token is
invalid (sign in again), 503 that Wargaming is unreachable. The app trusts the
answer and never decides rights itself. Nothing is kept on the HQ side. HQ down,
or any other failure to get an answer, shows "Geen verbinding met WoT HQ".

### 3.4 Public reads (created by #18)
Under the app's own application id, no token: `account/list` (name → account id),
`account/info` (`global_rating`), `clans/list` and `clanratings/clans` (`efficiency.value`, `.rank`); the public API has no WTR (checked 2026-11-09),
for the Clan battle dialog. Each call names its endpoint, parameters and fields
in its issue, marked **(verify)** until checked against the live API.

## 4. Settings

Today every setting is a `Preferences.Default` key named after a property of
`Models/Settings.cs`, read and written by `Services/SettingService.cs`.

| Key (`Preferences`) | Holds | Fate |
|---|---|---|
| `ReplayFolders` | The replay folders in app-bar order, as JSON `[{"Title","Path"}]` (added by #15) | Stays |
| `WotReplayDirectory` | The single replay folder path before #15 | Read once to make the folder "Replays" when `ReplayFolders` is absent; never written again |
| `LoadBattlesSince` | The oldest battle date to load, as ticks | Removed by #71; every replay in the folder loads, the stored value is ignored |
| `ClanAbbreviation` | The clan tag used to find the player's team (default `NED-1`) | Removed from Settings by #12; comes from the login |
| `PlayerName` | The player name used to find the player's team | Removed from Settings by #12; comes from the login |
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

Order inside and across milestones follows each issue's `Depends on:` line
(CLAUDE.md), not the table.

The numbers stay, but the work order is 07 → 04 → 03 (owner, 2026-11-07), set
through the `Depends on:` lines of #15 and #10.

Milestone 08 goes first (owner, 2026-11-07), set through the `Depends on:` lines of #10, #17, #31, #32, #34 and #38.
