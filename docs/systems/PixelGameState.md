# PixelGameState / GameState

Started 2026-10-23 (Phase 1: the owner, plus the Bank and the Stats as the pilot; then the Auto Clicker, the Focus potion, Time Stop and Pixel Grabbing, the Bait Bucket and Cubie's bill, the Combo switch, the event log, the UFO Helper, the pets, the achievements, the potion / device inventory, the skill choices, the minigame switches / counters and the shop packs). The player's PROGRESS moves out of the MonoBehaviours into one plain object, one system at a time. The old `IPixelSaved` registry keeps working for the systems that have not moved yet.

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
  - `RaiseLoaded()` / the `Loaded` event: called once tiers, shop, husks and the IPixelSaved parts are back, but BEFORE the Dev Hub tester machines (a Solar Panel links to a placed device, belts dock on devices' couplings). Views refresh and world objects rebuild here.
  - `RaiseSaving()` / the `Saving` event: called right before a save is written. World objects that change all the time (placed devices, the running potion) copy themselves into their sub-state here instead of every frame.
  - `NewGame()`: called right before the scene reloads for Restart (`PixelPauseMenu.Restart`) and New Game (`PixelTitleScreen.StartNewGame`). These are the only two scene loads.
  - It does NOT use `PixelStatics.ResetOnRestart`, because `sceneUnloaded` can arrive after the new scene has started.
  - The component shows `Current` in the Inspector while playing ("State"), so you can read values and type them in to test.
- **Saving:** `SaveData.state` holds `Current` whole, through JsonUtility. Old parts in the file for moved systems are simply ignored (no migration, per the save rule).

## Rules for a moved system
- **Read the state through `PixelGameState.Current.<part>` every time.** Never keep a reference to it: a load swaps the whole object. The usual shape is `private static BankState B => PixelGameState.Current.bank;`.
- **The component keeps only display, input, per-session numbers and Inspector tuning** (`[SerializeField]` + `[Tooltip]` as always). Anything that must survive save / load / restart goes in the sub-state.
- **No `IPixelSaved`, no `PixelSaveRegistry.Register`, no `PixelStatics.ResetOnRestart` in its script.** `tools/compile-check/rules.sh` checks this for every name in `GameState.MovedSystems`. A restart reset that clears something that is not progress (the old scene's event listeners) is marked `// restart-reset-not-progress: <what>` on its line.
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
| `PixelTimeStop` | `SwitchState` (`timeStop`: switched off in Toggles) | The old Import's side effect (a switched-off Time Stop ends any stop / slow) runs in `Loaded`. 'Bought' (`timeStopActive`, `timeSlowOwned`) still comes from the shop packs; it moves with the shop. |
| `PixelGrab` | `SwitchState` (`grab`: switched off in Toggles) | `Loaded` lets go of a held husk if it is off and re-applies `clicker.GrabEnabled`. Grabbing itself is always unlocked. |
| `PixelBaitBucket` | `BaitBucketState` (placed, spot, stored Fish / Water husks by type with their payout) | The first WORLD object moved: the bucket writes its spot into the state each frame while it stands (it can be carried) and `Loaded` rebuilds it from the state. The pattern for a placed thing: the state holds what's needed to rebuild it, the object is only the view. |
| `PixelCubieBill` | `CubieBillState` (paid per colour by type, trust tier, revealed) | The old part loaded early on purpose (Recyclers rebuilt during the load ask `TagShown`); with `Replace` at the START of the load this now holds by itself. Its `ResetOnRestart` clears event listener lists (not progress), marked `// restart-reset-not-progress:` so the compile-check rule allows it. |
| `PixelCombo` | `SwitchState` (`combo`: switched off in Toggles) | `Loaded` drops the running combo if it is off and redraws the meter. |
| `PixelHints` (event log) | `EventLogState` (the log's lines: short text + the tip behind it) | Only the event log's lines moved: the 'seen' flags of first-time tips stay global PlayerPrefs (they are per machine, not per save). `Loaded` redraws the log from the top with no box. |
| `PixelRobotWorker` (UFO Helper) | `UfoState` (switched off; the job: device by display name + spot, or banking one pixel type) | `SetJob` / `SetBankJob` write the state, so the runtime job and the state never drift; `Loaded` rebuilds the job's ghost and re-applies the switch. |
| `PixelPets` | `PetsState` (owned pets in the order found: type, off, outfit, name) | Each live `Pet` keeps a reference to its state entry (`Pet.saved`; `type` / `off` / `outfit` / `customName` are properties on it), so every change is the state's at once - the pattern for a list of world objects. `Loaded` rebuilds the bodies. |
| `PixelAchievements` | `AchievementsState` (earned tiers + when, by achievement id) | The achievement list stays Inspector config; `earnedTiers` / `EarnedAtOf` / `StampTier` read and write the state by id (no stored reference). The old 'ResetOnNextStart' flag that Restart / New Game set is gone: a new game's fresh state has nothing earned. `Loaded` re-grants anything already reached quietly. |
| `PixelConsumables` (inventory) | `InventoryState` (held potions / devices by key, the ghost backpack bonus; the WORLD part: placed devices + the running potion) | `Potion.owned` / `bonusCap` and `Device.owned` are properties reading the state by key (potion "p:type:second:third", device "d:<display name>", the key cached on the item). Placed devices and the running potion change every frame, so they copy themselves in on `PixelGameState.Saving` and are rebuilt in `Loaded` - the pattern for world objects that change all the time. The last `IPixelSaved` part. |
| `PixelSkills` | `SkillsState` (the pack in play, the guest blocks switched on, the offline task) | These three were per-save SETTINGS (PlayerPrefs copied into each save by PixelSettingsSync); now plain progress, and taken off PixelSettingsSync's list. Skill XP, bought nodes, the bonus XP bank and Mastery were already `StatsState` counters, so they moved with the Stats. The hazard-penalty restart reset is marked not-progress. `Loaded` raises `Changed`. Side effect: the title screen no longer takes the newest save's skill pack with its other settings (it shows the plain rotation until Continue). |
| `PixelMinigame` (+ every subclass) | `MinigamesState` (per minigame id: switched off, goal counter, second number) | The base class has `UserDisabled`, `SavedCount` and `SavedExtra` over the state; a subclass keeps its counter as a PROPERTY over them (`private double chunks { get => SavedCount; set => SavedCount = value; }`), so its code is unchanged. `OnProgressLoaded` (virtual) fixes up after a load (goal already announced, bomb parts earned >= held); a switched-off one stops on `Loaded`. The Ultra Holes' counter is PixelClicker's Ultra-ever-earned: until PixelClicker moves, the Pad copies it in on `Saving` and back on load. PixelSaveGame no longer writes `SaveData.minigames`. |
| `PixelShop` | `ShopState` (per pack display name: bought, level) | `ShopPack.purchased` / `level` are properties over the state. Applying a pack's effects on load must stay at its FIXED point (right after the tiers, before the rest of the load: not-bought packs switch their features off, some upgrades re-apply), so PixelSaveGame still calls it there - `shop.ApplyLoadedPackStates()` - and it reads the state instead of the file. The rule for a core step whose ORDER matters: PixelSaveGame keeps calling it at its step; only where the data comes from changes. PixelSaveGame no longer writes `SaveData.packs`. |

The remaining systems, simplest first, are on the to-do list.
