# PixelGameState / GameState

Started 2026-10-23 (Phase 1: the owner, plus the Bank and the Stats as the pilot; then the Auto Clicker and the Focus potion). The player's PROGRESS moves out of the MonoBehaviours into one plain object, one system at a time. The old `IPixelSaved` registry keeps working for the systems that have not moved yet.

## Why
- **Saving:** each system saved its own part (`IPixelSaved`) in load-order bands.
- **Restart:** it needed hand-registered resets (`PixelStatics.ResetOnRestart`).
- **Startup order:** "is the save / the system there yet" checks were everywhere.
- **No simulation:** the rules couldn't run without the scene.

With one state object, saving writes it, loading replaces it and a new game makes a fresh one.

## The pieces
- **`GameState.cs`:**
  - `GameState` is plain `[Serializable]` data with no `UnityEngine.Object` references. It holds one sub-state per moved system (`BankState`, `StatsState`).
  - `GameState.MovedSystems` lists the moved systems' class names.
  - `Repair()` fills in missing parts after a load.
- **`PixelGameState.cs` (the owner, in `PixelSystems.All`, Core):**
  - `PixelGameState.Current`: never null. It is made on first use, so early callers never wait.
  - `Replace(state)`: PixelSaveGame calls it FIRST in a load.
  - `RaiseLoaded()` / the `Loaded` event: called LAST in a load, after tiers, shop, the IPixelSaved parts, husks and tester machines. Views refresh here.
  - `NewGame()`: called right before the scene reloads for Restart (`PixelPauseMenu.Restart`) and New Game (`PixelTitleScreen.StartNewGame`). These are the only two scene loads.
  - It does NOT use `PixelStatics.ResetOnRestart`, because `sceneUnloaded` can arrive after the new scene has started.
  - The component shows `Current` in the Inspector while playing ("State"), so you can read values and type them in to test.
- **Saving:** `SaveData.state` holds `Current` whole, through JsonUtility. Old parts in the file for moved systems are simply ignored (no migration, per the save rule).

## Rules for a moved system
- **Read the state through `PixelGameState.Current.<part>` every time.** Never keep a reference to it: a load swaps the whole object. The usual shape is `private static BankState B => PixelGameState.Current.bank;`.
- **The component keeps only display, input, per-session numbers and Inspector tuning** (`[SerializeField]` + `[Tooltip]` as always). Anything that must survive save / load / restart goes in the sub-state.
- **No `IPixelSaved`, no `PixelSaveRegistry.Register`, no `PixelStatics.ResetOnRestart` in its script.** `tools/compile-check/rules.sh` checks this for every name in `GameState.MovedSystems`.
- **Key data by stable ids (`PixelType` as int, string keys), not by tier index or list position.** Tier indices shift when pixel types are added. Turn them into tier indices in the component (see `PixelBank.CountAt`).
- **Physics objects stay outside:** husks, placed devices and tester machines are copied out when saving and rebuilt when loading, as before.

## Moving a system: the steps
1. **Read its `IPixelSaved` code:** its `SaveState` class, `Export` / `Import`, and its `SaveOrder` band. Also read every `ResetOnRestart` / `ResetStatics` it registers.
2. **Split its fields** into progress (saved, or reset on Restart) and the rest (UI, input, timers of the current session, Inspector tuning, caches).
3. **Add a `[Serializable] public class <Name>State`** to `GameState.cs` with the progress fields, keyed by stable ids.
   - Add `public <Name>State <name> = new <Name>State();` to `GameState`.
   - Add a line for it in `GameState.Repair()`, and a `Repair()` on the sub-state if it has lists.
   - Unity-serializable types only: no `Dictionary`. Use a `List` of small entries plus a `[NonSerialized]` lookup, as `StatsState` does.
4. **In the component:**
   - add `private static <Name>State X => PixelGameState.Current.<name>;`;
   - point every read / write of the old fields at `X.…`;
   - delete the old fields, the `IPixelSaved` block, the Register / Unregister lines and its restart reset (if the static was progress);
   - keep the public getters as thin forwards, so callers don't change.
5. **If its old Import did more than copy data** (pick a selection, rebuild a view, count something "on load"), subscribe to `PixelGameState.Loaded` in Awake / Start (and unsubscribe in OnDestroy) and do it there.
   - **If it needs ANOTHER system's state during load** (its old SaveOrder band mattered for more than refreshing a view), stop and ask: that is a design question, not a workaround.
6. **Add its class name to `GameState.MovedSystems`.**
7. **Update everything that points at the old way:**
   - callers of the old `GetState` / `SetState`;
   - its script header and `docs/systems` file;
   - the "Moved so far" line in `docs/systems/PixelSaveGame.md`;
   - the to-do line (`Assets/Resources/Todo.txt`).
8. **Run `tools/compile-check/check.sh`, add a changelog line, and say what to test:** save -> quit -> load, Restart, New game.

## Moved so far
| System | Sub-state | Notes |
|---|---|---|
| `PixelBank` | `BankState` (stored husks by pixel type: count + total value) | `Loaded` picks a stocked selection. Dev Tools' reset calls `ClearStored`. Capacity and 'bought' come from the shop (not progress). |
| `PixelStats` | `StatsState` (typed numbers, potions drunk, generic counters) | `PixelStats.Count / Best / Fastest / Total` now work without the component. `Loaded` counts the session if the 3-second count hasn't run. |
| `PixelAutoClicker` | `AutoClickerState` (bought, switched off, upgraded interval / clicks per tick) | The Inspector `intervalSeconds` / `clicksPerTick` are BASE values, used while the state holds 0 (the pattern for "an upgrade's value vs the tuning default"). The shop's load step (`SetPackState`) doesn't touch it, so there are no order issues. |
| `PixelFocus` | `FocusState` (running potion: skill id, name, seconds left) | The potion list stays on the component. `HoldsOffHazards` reads the state, so it works without the component. `Loaded` redraws the status line. |

The remaining systems, simplest first, are on the to-do list.
