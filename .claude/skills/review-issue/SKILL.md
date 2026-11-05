---
name: review-issue
description: Run a fresh adversarial Opus review of an implemented issue branch, then walk the owner through each finding and apply only the approved changes. Use after issue-implementer reports DONE and before the PR is opened/handed to the owner. Not for ui-only issues. Args: issue number.
---

# Review an implemented issue

Args: issue number `N`. The current checkout must be on the issue branch
(`git branch --show-current` → `issue/N-*`); if not, switch to it first.

1. **Review (fresh context).** Launch `Agent` with
   `subagent_type: "issue-reviewer"` (not a fork, so it doesn't inherit the
   implementation reasoning). Prompt, nothing more:
   ```
   Review issue #N on branch <branch>.
   Implementer report:
   <paste the implementer's DONE report verbatim>
   ```
   Don't paste the diff, issue body or plan; the reviewer fetches what it needs.

2. **Sanity-check the findings** before showing them. Drop or downgrade any
   finding whose evidence doesn't hold up when you look (open the cited
   `file:line`). Don't add findings of your own here; note them separately.

3. **Ask the owner.** Show the review compactly, then use `AskUserQuestion`,
   grouped up to 4 findings per call, `blocker`/`should`/`plan-conflict` first.
   Per finding the options are: **Fix as proposed**, **Fix differently**
   (owner types how), **Won't fix** (owner gives the reason), and, for
   `question`, the A/B choices. Ask about nits in one multi-select question.
   Never apply anything the owner didn't approve.

4. **Apply.** If anything was approved, `SendMessage` the original
   implementer (it keeps its context, which costs less than a new agent).
   If it's gone, launch a new `issue-implementer`. Send:
   ```
   Apply these approved review changes to #N on <branch>, as new commits:
   F1: <change as approved>
   …
   Don't touch anything else. Report in the usual shape.
   ```
   If the owner resolved a `plan-conflict` by changing the plan, the plan
   edit goes in the same PR (CLAUDE.md rule); say so in the message.

5. **Re-check.** Run the build yourself and diff the fix commits against
   the approved list. Re-run the reviewer only if a fix touched more than the
   approved scope or a `blocker` was fixed in a non-trivial way.

6. **Record.** In the PR body, under the acceptance list, add
   `Review: F1 fixed, F2 won't fix (<reason>), …` so the owner sees the
   outcome at merge time.
