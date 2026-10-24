# Code map

`CodeMap.html` is an interactive map of every script in `Assets/Scripts`: one circle per system (a partial class over several files is one
circle), grouped by area, sized by lines of code, with a line wherever one script's code uses a type from another. Click a circle for its
description, files, what it uses and what uses it; search by name; show / hide areas (Shift-click = only that area).

Open `CodeMap.html` in a browser (it loads d3 from the web). Rebuild it after adding or renaming scripts:

    python3 tools/code-map/build.py

- The page layout is `template.html`; `build.py` fills in the data.
- Areas come from the `GROUPS` table at the top of `build.py` (first matching rule wins) - add a rule when a new script lands in the wrong area.
- Descriptions come from `docs/Index.md`, else the script's header comment.
- Published page: https://claude.ai/artifact/9KCRYHzzmGrNWNFTuqqz7w (republish `CodeMap.html` to that link after a rebuild). `tools/rebuild-code-pages.sh` rebuilds this and the Code Health report together.
