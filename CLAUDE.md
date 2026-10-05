# Pixel Clicker – project notes

3D incremental clicker in Unity (C#, TextMeshPro). All code lives in `Assets/Scripts/`. Everything UI is built at runtime (no prefabs/scene UI required). Nothing has been compiled or run by Claude – the user tests in Unity.

## Core architecture

| Script | Role |
|---|---|
| `PixelClicker.cs` | Core. Owns the `PixelTier[]` list, currency counts/totals, click handling (raycast), spawn/materialize/pulse/hover/spin animation, old-pixel "falling copy" physics, vacuum suction, shared UI font (`UIFont`), `FormatNumber` (public static). Also defines helper MonoBehaviours `ScaledGravity` and `OldPixelInfo` at the bottom of the file. |
| `PixelConsumables.cs` | Potions (one per pixel type, editable list: name, type, costs, duration, owned). Drinking one calls `PixelClicker.SetForcedSpawnTier` for its duration. PixelShop sells them (Consumables tab); PixelShop auto-adds this component if missing. |
| `PixelUI.cs` | "Inventory" toggle box (top-left) with Currency / Consumables tabs (right-click a potion to drink), active-potion timer at top of screen; Currency tab: per-tier counts, cursor `+N` popups (manual) / cube popups (auto), vacuum `+X` total popup, per-entry vacuum `+X` indicators. Builds in `Start()` so shop-added tiers are included. |
| `PixelShop.cs` | Shop button (appears once `requiredTier` = Black is unlocked) + panel. `ShopPack` list: RGB Pack, Auto Clicker, Glass Pixel, Vacuum Pixel (Pixels tab), Faster Clicking, Multi-Click. Three tabs (Pixels / Upgrades / Consumables; per-pack `tab`, `Automatic` infers it) over a fixed-size scrolling list (ScrollRect + scrollbar). Leveled packs that require another pack (the auto clicker upgrades) are not in a tab: the required pack gets an arrow button opening a second "Upgrades Window". Handles buying, requirements, affordability colouring, levels/upgrades, Inspector-ticked `purchased`. `[RequireComponent(typeof(PixelAutoClicker))]`. |
| `PixelAutoClicker.cs` | Timer that calls `clicker.AutoCollect()`; `Interval` / `ClicksPerTick` properties; `Activate()` from shop; `startRunning` for testing. |
| `PixelLog.cs` | Bottom-left "Log" toggle: unlocked tiers with lifetime totals, overall total, vacuum `+X` deltas. Has its own local `FormatAmount`. |
| `PixelPauseMenu.cs` | Pause menu (Esc / on-screen Pause button): sets `Time.timeScale = 0`, Resume / Restart / Quit, `PixelPauseMenu.IsPaused`. PixelClicker ignores clicks while `Time.timeScale <= 0`. |
| `PixelDevTools.cs` | Bottom-right dev button, adds `amountToAdd` (10) to each unlocked tier through `AddCurrency`. Self-removes in non-dev builds. |

### Data model
- `PixelTier`: name, colour/`UIColor`, `PixelType` enum (White, Gray, Black, Red, Green, Blue, Glass, Vacuum), `TierUnlockMode` (PreviousTierThreshold / ShopOnly), `spawnWeight`, `startingAmount`, `translucent` / `materialOverride`, `vacuum` flag, `count`, `totalCollected`, `unlocked`.
- Spawn: weighted random among **unlocked** tiers only (`PickSpawnTier`).
- Click flow (`CollectInternal(bool automatic)`): `AddCurrency` → `PixelCollected` event → click effects → `Vacuum()` if vacuum tier → `SpawnFallingCopy` → roll next tier → `Materialize`.
- Events: `CurrencyChanged`, `CurrencyGained`, `PixelCollected(tier, amount, automatic)`, `PixelsVacuumed(vacuumTier, total, count)`, `VacuumBreakdown(double[] perTier)`.
- Shop reward tiers are added to `PixelClicker` at runtime via `EnsureTier` (locked until bought). `EnsureTier` and Awake must only **raise** `Count`, never wipe typed starting values.
- `PixelShop.EnsureDefaultPacks()` (called from `Awake` and editor-only `OnValidate` via `delayCall`) inserts missing default packs into the serialized list so they are editable in the Inspector. Toggles: `addDefault…Pack`.

## Active conventions
- **Every variable must be editable in Unity**: `[SerializeField] private` + `[Tooltip]` (+ `[Header]` grouping). Nested config classes are `[Serializable]`.
- Field initializers don't apply to already-serialized components → use Awake/OnValidate fix-ups for new defaults. No native-object constructors (`new RectOffset`) in field initializers.
- Version guards: `FindFirstObjectByType` vs `FindObjectOfType` (`UNITY_2023_1_OR_NEWER`); `PhysicsMaterial`/`linearDamping` vs `PhysicMaterial`/`drag` (`UNITY_6000_0_OR_NEWER`); `InputSystemUIInputModule` vs `StandaloneInputModule` (`ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER`). Input must work with both legacy and new Input System.
- UI is code-built: ScreenSpaceOverlay Canvas + CanvasScaler (1920×1080 ref, match 0.5) + GraphicRaycaster; auto-create EventSystem if missing; TextMeshProUGUI only. **One shared font** via `PixelClicker.UIFont`; every UI script applies it. Avoid obsolete TMP properties (`enableWordWrapping`).
- UI layout: line pitch ≥ 1.3× font size, header height computed from font; toggle boxes have no close "X" (clicking the button toggles); titles capitalised ("Currency", "Log", "Shop").
- Clicks on UI must not hit the pixel (`EventSystem.IsPointerOverGameObject`); pixel hitbox is a full-size trigger so rapid clicks during materialize aren't lost; use `Physics.RaycastAll` with `QueryTriggerInteraction.Collide`.
- Scripts depend on each other (`FormatNumber`, `VacuumBreakdown`, `UIFont`, …). **Always copy the whole `Assets/Scripts` folder together** – most past compile errors (CS0122, CS1061, CS0246, CS0111) were stale/mismatched copies in the Unity project.
- Git: develop on `claude/pixelclicker-initial-script-un1ao1`; commit + push when work is done; no PRs unless asked.

## Roadmap / open notes
- **Unverified in Unity**: latest `EnsureDefaultPacks`/`OnValidate` change (Glass & Vacuum packs appearing in the Packs list). If it misbehaves, fall back to the context-menu "Add Default … Pack To List" entries.
- Possible next steps (not requested yet – confirm before building): save/load of progress, more shop packs/tiers beyond Vacuum, sound/particle polish, balancing pass on spawn weights and costs, replacing runtime-built UI with prefabs if a designer-friendly layout is wanted.
- Known gaps: no persistence; `PixelLog` keeps a duplicate number formatter (`FormatAmount`) separate from `PixelClicker.FormatNumber` – could be unified.
