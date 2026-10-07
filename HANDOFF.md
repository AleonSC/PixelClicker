# Pixel Clicker - handoff for a new session

Written 2026-10-07 at the end of a long session. Read `CLAUDE.md` first (it is the authoritative script-by-script map and the working rules); this file adds the things CLAUDE.md does not say: how the work has been going, what is unverified, the lessons learned, and the **optimization review** (section 4) that was the agreed next step.

---

## 1. What the project is

3D incremental clicker in Unity (C#, TextMeshPro). Click a cube to collect pixels, buy upgrades/minigames/devices in a shop. ~22.7k lines in `Assets/Scripts/` (42 scripts), **all UI is built at runtime** (no prefabs, no scene UI). Content text files the user edits live in `Assets/Resources/` (`Changelog.txt`, `HowToPlay.txt`, `Controls.txt`).

Claude cannot compile or run Unity. The user tests in the editor and pastes errors/screenshots. Everything below that says "untested" means "written but the user has not confirmed it works".

## 2. How to work with this user (standing rules)

These are repeated in CLAUDE.md; the important ones:

- Branch: `claude/pixelclicker-initial-script-un1ao1`. Commit and push when work is done (`git push -u origin <branch>`). **No PRs unless asked.** Never force-push.
- Commit messages end with:
  `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01MLDA8YRETKw2wrxsN92QzB` (use the current session's lines if they differ).
- **After every game change append one line** to `Assets/Resources/Changelog.txt`: `[YYYY-MM-DD HH:MM UTC] plain-language sentence` (`date -u +"%Y-%m-%d %H:%M"`). "Clear the changelog" = empty the file (keep it) and commit. The user's pause menu shows this file to players.
- Update `Controls.txt` when a key/control changes, `HowToPlay.txt` when a major system is added, `CLAUDE.md` when a system changes.
- **Every variable must be editable in Unity**: `[SerializeField] private` + `[Tooltip]` (+ `[Header]`). Field initializers do not update already-serialized components, so to push a new default onto the user's scene **rename the field** (this has been done several times, e.g. `logWidth`, `guideTextSize`).
- Always tell the user to **copy the whole `Assets/Scripts` and `Assets/Resources` folders** (most past compile errors were stale partial copies). Say when a script was deleted so they delete it in Unity too.
- Code compatibility: both input systems (`#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER`), Unity 6 vs older (`PhysicsMaterial` vs `PhysicMaterial`, `linearDamping` vs `drag`), `PixelFind.First<T>()` instead of Unity's Find methods, `TextMeshProUGUI` only, **older TMP: do not use `textWrappingMode` / `enableWordWrapping`** (use `overflowMode`).
- Tone: the user likes short, direct answers, and a final summary of what changed + which folders to copy. They test fast and report screenshots.

### Pending reminders for the user (do these first)
1. **Clear the changelog**: they built a game for a friend; they asked to be reminded. Entries from "Added a start screen..." onward are post-build. Only clear when they say so.
2. The agreed plan was: optimization review -> this handoff. Both are now written; the next step is the user choosing which optimizations to do (section 4).

## 3. State of the project

### 3.1 Systems added in the last long session (all pushed, user reports "everything seems to work")
Time Stop energy meter + haze + looping hum + click stockpile; consumables inventory category dropdown; looping sounds in `PixelAudio` (`StartLoop/StopLoop`); `PixelHints` (first-time tips, the scrolling event log box with history saved in the save file, new-game intro, purchase announcements); `PixelNotice` onClosed callback; `PixelTitleScreen` (blurred Play screen); Log and Inventory are mutually exclusive; Combo Meter toggle; Toggles tab hidden until Auto Clicker bought; Bank hose button half-width centred; bomb-parts "earned vs held" split; usage achievements (Potion Taster, Gadgeteer); Restart resets tips and ends the crash-log session cleanly.

### 3.2 Things the user has NOT explicitly confirmed (verify when they next test)
- The latest rounds (intro tips order after the Play screen, Restart returning to the Play screen, `introDelay`) - the user said "everything seems to work" before the final Play-screen/Restart tweaks.
- Blur quality of the title screen (`PixelTitleScreen.BuildBlur` renders `Camera.main` to a texture; may look blocky; may fail on URP overlay cameras - fallback is a dark cover).
- Event log scrolling when the box is not full (fixed by computing the view height from the screen; verify at other aspect ratios).

### 3.3 Known gaps (also in CLAUDE.md)
Offline progress only covers the auto clicker; `PixelLog` has its own number formatter; PixelShop is split into partial classes but logic and UI are still one class.

### 3.4 Lessons learned this session (avoid repeating)
- **Patching by "first text match" put code in the wrong function twice** (the purchase tips landed in `DevSkipIntro` instead of `TryBuy`; the tips never fired for ~10 turns). When editing with scripts, assert the match is unique or anchor on the function name, and `grep -n` the result.
- `Time.timeScale = 0` is used by both the pause menu and Time Stop and the title screen. Anything that must keep running while frozen needs `Time.unscaledDeltaTime` (the cube's `AnimDelta`, Time Stop's meter, hints, the event log).
- Scene reload (Restart) does **not** reset statics. Check any `static` state when adding systems (see 4.4).
- The Unity `rect` of a freshly created UI element is not valid until the canvas updates; compute sizes from `Screen`/canvas scale instead (event log bug).

## 4. Optimization review (nothing below has been changed yet)

Method: read the Update/FixedUpdate loops of every script (39 per-frame methods), the old-pixel spawn path, the UI refresh paths, and grepped for allocations, Find calls, duplicated helpers and statics. No profiler data exists - the user has not reported performance problems. Effort: S < 1 h, M = a few hours, L = a day. Risk = chance of breaking behaviour.

### 4.1 Per-frame work that should be event- or timer-driven (best payoff, low risk)

| # | Where | Problem | Fix | Effort/Risk |
|---|---|---|---|---|
| 1 | `PixelShop.Update` (`PixelShop.cs:763`) | **Every frame, for every purchased pack, calls `ApplyPackEffects`** (IndexOf/UnlockTier per reward tier, `PixelMinigame.Find`, component activate calls) just to catch Inspector-ticked packs. | Run that loop only in the editor / once at start / when `OnValidate` or the Inspector changes (`#if UNITY_EDITOR`), or on a 1-second timer. Normal buying already applies effects in `TryBuy`. | S / low |
| 2 | `PixelShop.RefreshRows` (`PixelShopUI.cs:662`), called every frame while the shop is open | Rewrites name/description/cost strings (`string.Format`, `ResolveDescription`, TMP `.text`) for every visible row each frame -> GC + TMP rebuilds. | Refresh on events (`CurrencyChanged`, purchase, tab switch) or at most 4x/second; set TMP text only when the string changed. | S-M / low |
| 3 | `PixelUI.Update` -> `Refresh()` (`PixelUI.cs:539/972`) every frame while the Inventory is open | `string.Format` + TMP text for every tier line each frame; also resizes the box each frame. | Subscribe to `CurrencyChanged`/potion events and set a dirty flag; refresh when dirty or on a 0.25 s timer. | S / low |
| 4 | `PixelLog.Update` -> `Refresh()` (`PixelLog.cs:397`) every frame while open | Same as above (tab lists + achievements bars). | Same dirty-flag / timer. | S / low |
| 5 | `PixelCombo.Update` -> `Apply()` every frame (`PixelCombo.cs`) | `string.Format` for the label every frame while the meter is visible. | Only reformat when `combo` or `Multiplier` changes. | S / low |
| 6 | `PixelTimeStop.UpdateMeter` | Sets the status label text every frame while the meter shows. | Assign only on change. | S / none |
| 7 | `PixelCubeIcon.Update` (`SetVerticesDirty` every frame) | Every achievement icon in the open Log re-meshes each frame. | Cap to ~20 fps, skip when off-screen/closed. | S / low |
| 8 | `PixelHints.RebuildRows` (`PixelHints.cs:462`) | Destroys and recreates every event-log row (up to 100 TMP objects) whenever an event arrives or the box is shown. | Append the new row only, pool rows, and rebuild only when the history was replaced (load). | M / low |
| 9 | `PixelPauseMenu.Update` `RefreshStats` | Every frame while the Stats screen is open (paused). | 0.25 s timer. Minor. | S / none |

General rule for UI: a small helper `PixelUIKit.SetText(TMP_Text, string)` that skips identical strings would remove a whole class of waste.

### 4.2 Old-pixel pipeline (`PixelClicker.SpawnFallingCopy`, `PixelClicker.cs:1557`)

| # | Problem | Fix | Effort/Risk |
|---|---|---|---|
| 10 | **Leak:** a new `PhysicsMaterial`/`PhysicMaterial` is created for *every* old pixel (`PixelClicker.cs:1617/1626`) and never destroyed. | Create one in `Awake` (rebuild when the bounce/friction settings change) and share it. | S / low |
| 11 | Fly-away (meteor) copies create a new trail `Material` each (`CreateVisualMaterial`, ~line 1684) - same leak. | Cache one trail material per tier. | S / low |
| 12 | Per spawn: `new MaterialPropertyBlock`, `GetComponentsInChildren<Collider>()` of the live cube, 6-8 `AddComponent` calls, `new GameObject`. At auto-click speed or with the Time Stop stockpile (150) this is the main GC source. | Cache the live cube's colliders; reuse one property block; **pool old pixels** (disable/enable instead of Instantiate/Destroy). Pooling touches `ReleaseOldPixel`, `DespawnOldPixel`, bank spit/suck, black hole, pad, grab - do it last. | M-L / medium |
| 13 | Each old pixel runs its own `Update` (`OldPixelDespawn`, `OldPixelPopIn`) and `FixedUpdate` (`ScaledGravity`). | One manager component iterating `OldPixels` (single Update/FixedUpdate) and applying gravity/despawn. Cuts per-object overhead roughly 3x. | M / medium |
| 14 | `PixelViewBounds.FixedUpdate` (`PixelViewBounds.cs:83`) calls `GetComponent<OldPixelInfo>()` for every old pixel every physics step, and rebuilds 4 edge planes with 12 `ViewportPointToRay` calls each step. | Cache `OldPixelInfo` (store it in a parallel list or on first lookup) and rebuild planes only when the camera or the bar height changes. | S / low |
| 15 | `OldPixels` is a `List<Rigidbody>` with `Contains`/`RemoveAt(0)` (O(n)) used by clicks, bank, devices. | Fine at 30-150 items; only matters if the cap is raised a lot. Note only. | - |

### 4.3 Duplicated code (maintainability)

| # | Duplication | Suggestion |
|---|---|---|
| 16 | Dual input-system `#if` blocks re-implemented in 8 files (PixelClicker, PixelBank, PixelConsumables, PixelGhostMinigame, PixelHints, PixelPauseMenu, PixelTimeStop, PixelUI) although `PixelInput` exists. | Add `KeyPressed(KeyCode)`, `ScrollWheel()`, `EscapePressed()`, `EnterPressed()` to `PixelInput` and delete the copies. Easy to get wrong in only one input mode, so test both. |
| 17 | Number formatting: `PixelClicker.FormatNumber`, `PixelLog.FormatAmount`, `PixelPauseMenu.FormatCount`, `PixelUI.FormatAmount`. | One `PixelClicker.FormatNumber` (it respects `AbbreviateNumbers`); keep thin wrappers if the suffix rules truly differ (check first - they may). |
| 18 | `Camera.main` used in 13 files although `clicker.TargetCamera` exists (and `Camera.main` does a lookup). | Route through `PixelClicker.TargetCamera` (cached). |
| 19 | Per-minigame spawn/placement helpers (floor raycast `RaycastAll` in Blackhole, Pad, Consumables; screen-to-world rows in Ghost/Meteor/Bomb). | Extract a small `PixelWorld` helper (floor point from screen/ray, random point in view). |
| 20 | Each potion/device/achievement/hint list has its own "add missing defaults" routine (`EnsureDefault*`). | A generic `EnsureDefaults<T>(list, defaults, key)` helper. |
| 21 | Wiring: ~100 `PixelFind.First<T>()` calls in Awake/Start across scripts (13 in `PixelSaveGame`, 11 in `PixelShop`, 11 in `PixelPauseMenu`). | Optional: a `PixelGame` hub component that owns references. Only worth it if you start adding many more systems. |

### 4.4 Fragile spots / correctness risks

| # | Risk | Suggestion |
|---|---|---|
| 22 | **Static state survives Restart** (scene reload does not reload the domain): `OldPixelDespawn.Frozen`, `PixelClicker.ExternalClickBlock`, `PixelClicker.InfiniteResources`, `PixelHints.instance`, `PixelNotice.box`, `PixelPauseMenu.IsPaused/GameStopped`, `PixelTimeStop.IsStopped` (this one resets in OnDestroy). If a black hole or the hose is active at the moment of Restart, the flag can stay set. Also matters if the editor's "Enter Play Mode without domain reload" is ever turned on. | Reset statics in `[RuntimeInitializeOnLoadMethod]` and in the relevant `OnDestroy`. |
| 23 | **Save system is manual**: `PixelSaveGame` hand-maps every system into `SaveData` in two places (save + load). Every new system needs edits in both; easy to forget (the Combo toggle needed it). | An `IPixelSaveable` interface (`Save(SaveData)`/`Load(SaveData)` per component, registry like `PixelMinigame.All`). Medium effort; do it before adding more systems. Also add a save-version number and a `.bak` of the last good save. |
| 24 | Hint ids are free strings (`"minigame_" + id`, `"pixel_" + type`, `"device_" + kind`); a typo or rename silently does nothing (the earlier "wrong function" bug was invisible for the same reason). | `Debug.LogWarning` in the Editor when `Trigger` gets an unknown id; ideally an enum or constants class. Better: have `PixelShop` raise one `PackPurchased(pack)` event and let `PixelHints` decide, instead of `TryBuy` calling `PixelHints.Trigger` directly. |
| 25 | `PixelShop.TryBuy` has several hard-coded special cases (auto clicker hint, bank, crafting...) and the purchase-effect flags (`unlocksCrafting`, `unlocksGrabbing`, `unlocksTimeStop`, `unlocksBank`, `unlocksMinigame`) are one bool each. | A `PackEffect` list (enum + parameter) instead of N bools; each system registers a handler. Reduces the "add a feature = touch 6 places" cost. |
| 26 | Time scale ownership: pause menu, Time Stop and the title screen each save/restore `Time.timeScale`; the ordering relies on `PixelPauseMenu.GameStopped` checks. | One `PixelTime` owner with a stack of "freeze reasons" (pause, time stop, title) and `Scaled/Unscaled` helpers. Would also remove the `AnimDelta` special case. |
| 27 | Debug logs always on: `PixelHud` logs every docked button, `PixelUI` logs on build, `PixelShop` logs pack details (lines `PixelHud.cs:151`, `PixelUI.cs:964/1238`, `PixelShop.cs:621/754/988`, `PixelClicker.cs:1408`). | Gate behind a `verboseLogging` bool or `#if UNITY_EDITOR`. Console noise also hides real errors from the crash logger. |
| 28 | `PixelTitleScreen` blur uses `Camera.main.Render()` into a RenderTexture - assumes a normal (non-overlay URP) camera and the default render pipeline behaviour. | If it ever looks wrong, fall back to `ScreenCapture.CaptureScreenshotAsTexture` at end of frame with the overlay hidden. |
| 29 | Large files: `PixelClicker` 2034 lines (also holds 6 helper MonoBehaviours), `PixelUI` 1550, `PixelPauseMenu` 1414, `PixelConsumables` 1293, `PixelShop*` ~2900 over 4 files, `PixelBank` 1152, `PixelLog` 1039, `PixelCrafting` 1022. | Split in this order of value: move `OldPixel*` helpers out of `PixelClicker.cs`; split `PixelPauseMenu` (menu, stats, settings, guide screens); split `PixelUI` (currency, consumables, popups, guide). Pure moves, no behaviour change. |

### 4.5 Suggested order (cheap and safe first)
1. 4.1 items 1-6 and 10, 11, 14, 27 (all small, all low risk) - one commit, noticeable drop in GC and per-frame work.
2. 4.4 item 22 (static resets) and 24 (unknown-hint warning).
3. 4.3 items 16-18 (shared input/format/camera helpers).
4. 4.2 items 12-13 (pooling / manager loop) once measured with the Unity Profiler (add `Profiler` markers or just use the Deep Profile on a stockpile of 150).
5. 4.4 items 23, 25, 26 (save registry, pack effects, time owner) before adding the next big system.
6. 4.4 item 29 (file splits) as a calm refactor session.

Open question for the user: do they want performance work first (1-2) or new features first? The user has not asked for features beyond the CLAUDE.md "possible next steps" list (more packs/tiers, particle polish, sound clips, balancing, prestige).

## 5. Quick orientation for the first minutes of the new session
- `git log --oneline | head -30` shows the full history of this work.
- Boot order: `PixelClicker.Awake` adds `PixelTitleScreen`, `PixelHints`, `PixelCrashLog`, `PixelViewBounds`; the shop adds most feature components; `PixelSaveGame` loads one frame after start.
- Test checklist the user usually runs: Play button -> intro tips -> click cube -> shop purchases (check event log lines + tips) -> Time Stop (T) -> Restart (back to Play screen, tips again, no crash notice).
