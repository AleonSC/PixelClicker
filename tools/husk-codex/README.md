# Husk Codex (tool)

An interactive page of every pixel type: what a click on it does, what its husk does, how the husk falls (bounce, grip, gravity and its special behaviour) and which husks, sprouts and machines it touches. Three views: Catalogue (search / filter, a panel per block), Interaction map, Fall lab (husks dropped with their own physics numbers).

- Rebuild: `python3 tools/husk-codex/build.py` -> `tools/husk-codex/husk_codex.html`.
- Published page: https://claude.ai/artifact/PUToJo3t165YuTkT4cM9XH (republish `tools/husk-codex/husk_codex.html` to that URL after a rebuild).

Where the data comes from:
- Texts: `Assets/Resources/PixelTexts.txt` (falls back to the cards in `PixelBlockCatalog.cs`).
- Pay, clicks, spawn weight, colour: the `Block(...)` / `Ore(...)` lines and `new PixelTier { ... }` blocks in the scripts (a few rare drops and the Prospector ores are copied by hand in `build.py`).
- Husk physics and behaviour: `PixelLooks.CreateDefaults` (`customPhysics` bounce / friction / gravity, `flutter`, `splash`, `shatter`, `floatAway`...); without custom physics a husk uses the shared defaults (bounce 0.08, friction 0.65).
- Interactions: **hand-written** in `interactions.txt` (`from | to | kind | text`, things like 'Growing sprouts' are declared with `@id = name`). When a block, husk or machine rule changes, fix its line there. The build stops with the line number if a name or kind is unknown.
- The page itself: `page_template.html` (the data goes in at `__DATA__`).
