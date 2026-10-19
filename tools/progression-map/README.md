# Progression Map

A small offline app for planning Pixel Clicker's progression: tiers are columns (each gated by a milestone of Cubie's bill), features are
cards you drag between them.

## Open it (Windows)
Double-click **`Open Progression Map.bat`** (or open `index.html` in Edge or Chrome). No install and no internet needed.

## Use it
- **Drag** a card to another tier, or up / down to reorder it.
- **Click a status** (Idea / Agreed / Built) to step it to the next one.
- **Click a card** to edit its name, tier, category, owning session and notes, or to delete it. **+ Add feature** at the bottom of a tier.
- **Edit** on a tier header changes its name, milestone, theme, colour and what Cubie's shop sells at that tier.
- Each tier shows a progress bar (built / agreed / ideas) and warns when it **lacks** a Skill, Device or Transport.
- **Search** and the filters (status, category, session) hide the cards that don't match.

## Saving
- Every change saves automatically in your browser (it is still there next time you open the app in the same browser).
- **Save file** writes a `.json` copy (a backup, or something to share); **Open file** loads one.
- **Reset to plan** goes back to the starting data in `progression-data.js` (the design from the Design Discussion chat, 2026-10-19).

## For Claude
`progression-data.js` holds the starting data (`window.PROGRESSION_SEED`): tiers, the required categories, categories, sessions and
features. Update it when a feature ships or the plan changes, so **Reset to plan** brings in the latest state.
