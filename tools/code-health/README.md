# Code health

`CodeHealth.html` is a health and optimization report for every script in `Assets/Scripts`: an overall score (0-100, weighted by lines
of code), four category scores (Performance, Size and structure, Project rules, Docs), a "fix first" list, a treemap of every script by
area coloured by score, and a sortable / filterable table. Click a script for its findings with line numbers and why each one matters.

Rebuild it after changes (each rebuild whose score changed adds a point to `history.json`, the trend chart):

    python3 tools/code-health/build.py          # --no-history = leave the trend alone

- The checks and their points are the `CHECKS` table at the top of `build.py`; the page layout is `template.html`.
- It reads the code as text (no Unity), so a finding is a place to look, not proof. "Every frame" = inside Update / LateUpdate /
  FixedUpdate / OnGUI only; one-time setup guarded by `if (x == null)` is not counted; cached shared assets in statics are fine.
- Areas and descriptions come from the code map (`tools/code-map/build.py`, `docs/Index.md`).
