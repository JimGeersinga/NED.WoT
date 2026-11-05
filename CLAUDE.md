# CLAUDE.md

Read [docs/PLAN.md](docs/PLAN.md) before changing anything. It is the plan of
record: decisions taken, the project layout, the Wargaming and Clan API
integration, the settings and the milestone list. The look of the app is in
[docs/DESIGN.md](docs/DESIGN.md). This file covers the rules for working in the
repo; the plan covers what to build.

If this file, the plan, the design rules and an issue disagree, stop and say so.
Don't pick one silently. The plan is fixed by updating it in the same PR, never
by drifting from it.

## Work is tracked on GitHub, upfront

Repo: `JimGeersinga/NED.WoT`. Every change belongs to an **issue** in a
**milestone**. No issue means no code.

- **Milestones and their issues are created before implementation starts**,
  not as work is discovered. New work found mid-milestone becomes a new issue
  (or is folded into the earliest open issue that depends on it). It never
  ships unrecorded.
- **Order follows `Depends on`.** An issue may start once every issue on its
  `Depends on:` line is merged. Among the issues that may start, the earliest
  milestone goes first, then the lowest number. To pull work forward or push it
  back, add it to (or add to its own) `Depends on:` line. A dependency on the
  Clan repo is written `JimGeersinga/NED.WoT.Clan#N` and counts as met once
  that issue is merged there.
- Milestones are titled `NN — Name` (e.g. `02 — Sign in with Wargaming`) and
  map 1:1 to the milestone table in `docs/PLAN.md` §5.
- Labels: `type:story` (user-visible, demoable) or `type:enabler`
  (infrastructure that a later issue needs; it must say which under
  **Enables**), plus `ui-only` where it applies (see "Who does what").

### Issues must be grounded in code

An issue is written so that an implementer with only the issue, this file and
the plan can do the work without guessing. "Grounded" means:

1. **Code it touches, by path and symbol.** Every existing file, type or member
   the work reads or changes is named with its repo-relative path. Paths must
   be checked to exist when the issue is written (or refreshed): `Glob`/`Grep`
   first, cite second. Example:
   `NED.WoT.BattleResults.Client/Services/SettingService.cs` → `Save`.
2. **Code it creates, by path and symbol.** A **Files** section lists new and
   changed files, with the public types/members each one introduces. This is
   the ceiling, not a suggestion: a type not listed needs a reason in the PR.
3. **Code it builds on from earlier issues** is cited as `#N → path → symbol`,
   so the dependency is checkable.
4. **External contracts are concrete.** A Wargaming call names the endpoint,
   the HTTP method, the parameters and the response fields used, e.g.
   `GET account/info/?account_id&fields=global_rating → data[account_id].global_rating`.
   Anything not yet verified against the live API is marked **(verify)**.
5. **Acceptance is checkable.** Stories use Gherkin with concrete values
   (`token expires in 48 h`, `2026-03-29 02:30`). Enablers use a `Done when`
   checklist. Every criterion maps to a command whose output proves it, or to a
   screenshot, or to a manual step written out in the PR.
6. **`Depends on: #N, …`** is the last line of the body.

Issue skeleton:

```markdown
## Outcome            (enabler)  /  **As a** … **I want** … **So that** …  (story)

## Context
- Plan: docs/PLAN.md §…
- Code (existing): path → Symbol — why it matters
- Builds on: #N → path → Symbol
- Wargaming: METHOD endpoint (params) → fields used  (verify)

## Files
- new: path — types/members
- changed: path — what changes

## Decisions taken
## Acceptance criteria   /   ## Done when
## Verify
dotnet build NED.WoT.BattleResults.Client/NED.WoT.BattleResults.Client.csproj -f net10.0-windows10.0.19041.0

## Enables              (enablers only)
Depends on: #N
```

## Who does what

- **Planning runs on Opus.** That covers milestones, issue writing and
  grounding, plan updates, reviewing what comes back, and decisions that need
  the user.
- **Implementation runs on Sonnet.** Each issue is implemented by a subagent
  launched with `model: "sonnet"` (`.claude/agents/issue-implementer.md`), and
  the issue body is its brief. The subagent implements exactly that issue, on
  that issue's branch, and reports back. It doesn't widen scope or edit other
  issues. A blocker or a plan conflict is reported back, not worked around.
- The Opus session checks each result against the issue's acceptance criteria
  and the diff before moving to the next issue.
- **Every implemented issue gets an independent review before its PR.** After
  `issue-implementer` reports DONE, run the `review-issue` skill
  (`.claude/skills/review-issue/SKILL.md`) for that issue (a fresh
  `issue-reviewer` agent on Opus), put the findings to the owner and wait for
  answers. Only approved changes are applied. Nothing is pushed or turned into a
  PR until that round is closed. This holds inside `/goal` loops too: a review
  waiting on the owner pauses the loop.
- **`ui-only` issues skip that round.** An issue labelled `ui-only` changes only
  `.razor`, `.razor.css`, `wwwroot/`, `Resources/` (images, fonts) and `docs/`.
  It goes straight to a PR, and the owner reviews it with its screenshots. An
  issue that touches anything else (C# files, the csproj, `MauiProgram.cs`,
  `Platforms/`) keeps the full review round. An implementer who finds that a
  `ui-only` issue needs such a change stops and reports back. Prefer a small
  shared helper over copying logic between components; an issue that needs one
  is not `ui-only`.

## Branches, commits, PRs

- Branch `issue/<n>-<slug>` off an up-to-date **`master`** (this repo has no
  `main`). PR base is `master`.
- One issue → one PR. The title is `#<n> <issue title>` and the body starts
  with `Closes #<n>`, followed by how each acceptance criterion was checked.
- Before pushing: the build below with zero errors, and the screenshots the
  issue names attached to the PR (see "Screenshots"). CI
  (`.github/workflows/ci.yml`) must pass.
- **Every PR raises the app version.** Bump `Identity Version` in
  `NED.WoT.BattleResults.Client/Platforms/Windows/Package.appxmanifest`:
  `type:story` → minor (`2.2.1.0` → `2.3.0.0`), anything else → patch
  (`2.3.0.0` → `2.3.1.0`); leave the revision (4th number) alone, the packaging build may raise it
  (`AppxAutoIncrementPackageRevision`). Job `version` in
  `.github/workflows/ci.yml` fails the PR unless the version is
  higher than on `master`, so each release installs as an update.
- **A release is run by hand**: Actions → `Release` → "Run workflow" on
  `master` (`.github/workflows/release.yml`; the job is skipped on any other
  branch). Merging to `master` does not deploy.
- **The owner merges.** Never merge a PR yourself. The next issue starts only
  after the previous issue's PR is merged into `master`, so nothing is stacked.
- Review changes go on the same branch as new commits; don't force-push over
  a reviewed history unless asked.
- The `gh` CLI's active account may not be the repo owner. Run `gh` against
  this repo as `GH_TOKEN=$(gh auth token --user JimGeersinga) gh …`.

## Dependencies

Microsoft packages first. Then what the csproj already references (MudBlazor,
the MAUI and Blazor WebView packages). **No paid or commercially licensed
packages.** Before adding *any* package that isn't already in
`NED.WoT.BattleResults.Client/NED.WoT.BattleResults.Client.csproj`, ask the
user, with a case for why the BCL, MAUI and the existing packages fall short.
Version bumps don't need to ask.

## Wargaming rules that apply

- **EU only**: `https://api.worldoftanks.eu/wot/`, `auth/login` on the same host.
- **A call that carries a token is a POST form.** The token never goes in a
  URL.
- **The token lives only in `SecureStorage`** and is never logged, never put in
  an exception message and never written to `Preferences`.
- **Wargaming answers HTTP 200 on errors.** Check the envelope's `status`, not
  the HTTP code. Don't deserialize the error's `value`: it echoes the token.
- **Never call `auth/logout` on sign-out.** Signing out only forgets the token
  locally.
- Never trust the login redirect's query values without verifying the token
  (docs/PLAN.md §3).

## UI copy

- Dutch, *je*-form, sentence case, verbs as button labels.
- WoT's own English names for maps, tanks and clan roles.
- Dates `dd-MM-yyyy`, times `HH:mm`, tabular figures where numbers align.
- Never truncate a button label or column header. Long player names get an
  ellipsis with the full name in a tooltip.

## Screenshots instead of UI tests

There is no test project. Each screen and state an issue names is shown in the
PR as a screenshot, at **1280×800 and 1920×1080** (desktop only, no phone).

- Taken with Playwright connected over CDP to the Debug build's WebView2: start
  the app with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`,
  then `chromium.connectOverCDP('http://localhost:9222')`. **(verify)** that the
  MAUI WebView2 honours the variable; if it doesn't, #2 settles another way and
  updates this section.
- Attached to the PR, **never committed** (`.gitignore` holds `/*.png` and
  `.playwright-mcp/`).

## What we don't adopt from the Clan repo, and why

This app is one MAUI Blazor Hybrid project, and the owner chose process and docs
only. So these rules of `JimGeersinga/NED.WoT.Clan` do **not** apply here:

- The Recur build rules: analyzers, warnings as errors, central package
  management, the `var` and block-body rules, the `DateTime.Now` ban.
- The Core/Web/AppHost layering and the command/query handler pattern.
- Test projects, test fakes and the "one rule, one test" suite.

A rule the plan states still gets an acceptance criterion, checked by hand in the
PR. Non-trivial logic (a parser, a branch that decides money-like or access
outcomes, a token rule) still gets one runnable check; a `Debug.Assert`
self-check is enough.

## Commands

```bash
dotnet build NED.WoT.BattleResults.Client/NED.WoT.BattleResults.Client.csproj -f net10.0-windows10.0.19041.0
dotnet run --project NED.WoT.BattleResults.Client -f net10.0-windows10.0.19041.0     # Debug build, for screenshots
```

The release workflow publishes with
`dotnet publish NED.WoT.BattleResults.Client\NED.WoT.BattleResults.Client.csproj -c Release -f:net10.0-windows10.0.19041.0 …`
(`.github/workflows/release.yml`). CI (`ci.yml`) only builds.
