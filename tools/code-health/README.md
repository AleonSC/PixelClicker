# Code health

`CodeHealth.html` is a health and optimization report for every script in `Assets/Scripts`: an overall score (0-100, weighted by lines
of code), four category scores (Performance, Size and structure, Project rules, Docs), a "fix first" list, a treemap of every script by
area coloured by score, and a sortable / filterable table. Click a script for its findings with line numbers and why each one matters.

Rebuild it after changes (each rebuild whose score changed adds a point to `history.json`, the trend chart):

    python3 tools/code-health/build.py          # --no-history = leave the trend alone

- The checks and their points are the `CHECKS` table at the top of `build.py`; the page layout is `template.html`.
- Checks added 2026-10-24, so the report sees the kind of cleanup it used to miss:
  - **duplicate** (Size): the same 8+ lines of real code (declarations, braces and blank lines skipped; text inside quotes compared too) in two places - a helper waiting to be made. `DUP_WINDOW` sets the length.
  - **restart-reset** (Rules): a `PixelStatics.ResetOnRestart` line without `// restart-reset-not-progress: <what>` (progress belongs in the GameState).
  - **one-place** (Rules): a version-split API outside the one script that hides it (`ONE_PLACE`: physics materials -> PixelPhysicsMaterials, scene searches -> PixelFind, object ids -> PixelObjectId). Add a line there when a new shared helper takes over such a job.
  - Adding checks lowers the score at first (they find what was never counted); the trend chart shows the step.
- It reads the code as text (no Unity), so a finding is a place to look, not proof. "Every frame" = inside Update / LateUpdate /
  FixedUpdate / OnGUI only; one-time setup guarded by `if (x == null)` is not counted; cached shared assets in statics are fine.
- Areas and descriptions come from the code map (`tools/code-map/build.py`, `docs/Index.md`).
- Published page: https://claude.ai/artifact/SvWtYz7Y1c3Ja3BsNTNTHr (republish `CodeHealth.html` to that link after a rebuild). `tools/rebuild-code-pages.sh` rebuilds this and the code map together.
