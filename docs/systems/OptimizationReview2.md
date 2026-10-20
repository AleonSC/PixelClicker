# Optimization review 2 (2026-10-17)

The first review (the old HANDOFF.md section 4, 2026-10-08; summarised in `OlderNotes.md`) covered ~22k lines. This one covers all ~133k lines (293 scripts). Six read-only passes each took about 22k lines and followed the callers to confirm how often the code runs. The top items were then checked again by hand. There is still no profiler data, so "cost" below is read from the code. Profile before doing the L items.

Effort: S < 1 h, M = a few hours, L = a day. Risk = chance of breaking behaviour.

**Done:** A1 (rolling save picture stays on the GPU; read back + JPG only when a save needs it), A2 + A3 (shared redraw limit `PixelTierPicture.Due`, 30/s, used by Render, shop pictures, shop Operation, Log icons; shop picture keys cached), A4 partly (the Log icons use the limit; its studio copy is not moved onto PixelTierPicture yet - its camera size differs). A5, A6, B3, B5 (per-click work: RefreshUI only formats tiers that have a label, the Skills window refresh is capped to 10/s on XP, the Log skips its '+N' while closed; spawn roll asks LeftOutOfSpawns once per tier per roll (`FillSpawnableNow`), `PixelSkills.Find` is a plain loop, pack type settings parsed once (`PixelSkills.ParseTypes`), `PixelClicker.TypeName` caches enum names). A7 (`+N` popup pool in PixelUI, `maxPopups`). A8 (`PixelClicker.PointerHits`: one NonAlloc pointer ray per frame for TryGetBlockAt / IsLiveCubeAt; device and pet hover checks test themselves first and cast at the cube only on a hit; devices cache their hit parts (`CountsForHit`); pets cache their collider and share one physics material; belt sorter NonAlloc). Section C leaks (ring mesh + colour block, Mold shells / zone disc, ShineMesh cache, Weak Spots / Meteor / Breakout / Evil Tree materials, Ghost backpack sprite, PixelHuskQuery prunes from Get every 256 new husks). B1 manager loop (`PixelHuskTicker`: ScaledGravity / OldPixelImpact / OldPixelPopIn / OldPixelDespawn tick from one loop). B1 pooling done (2026-10-18): `PixelHuskPool` - HuskReleasing signal that the ~25 holders of loose husks listen to, claimed husks never pooled, stripped on release, cleaned on reuse; switch `PixelClicker.huskPooling`. Save registry (old HANDOFF #23) done: `PixelSaveRegistry`. Cleanup batch (2026-10-18): A9 (cube icons + Ultra swatch on the 30/s limit, off-screen skipped), D1/D2/D4 (flying set, view-bounds look flag cached, gravity well, stream NonAlloc, Pad / PathCarrier / Nuclear / splash lookups, Solar scan only when unlocked), B4 (skill XP / node keys cached, LevelOf cached), section E (scene searches -> Cached, floor renderers / props, rain, day-night retry, UFO property block, autosave choice + no pretty JSON, settled HUD buttons / combo) and the per-frame strings (PixelKeys.Replace cache, status boxes / lines, guide / potion lines, device timers, tooltips). B2 done (2026-10-19): one PixelFx burst pool (PixelFx.Rent) for every per-hit effect and the click pop. F4 done: Log / Pixel Guide / shop pictures / Operation use PixelTierPicture / PixelModelPicture. Still open: D3 (machine scans), PixelHints rows, the other shared helpers in F. Untested in Unity.

## A. Biggest wins (normal play, always on)

| # | Where | Problem | Fix | Effort/Risk |
|---|---|---|---|---|
| A1 | `PixelAutosaves.cs:209` ShotRunner.Update (`CacheSeconds = 2.5`) | Every 2.5 s during play it does a full-screen `ScreenCapture.CaptureScreenshotAsTexture` (GPU stall + ~8 MB), then a Blit, a `ReadPixels` (a second stall) and a JPG encode on the main thread, just to keep a thumbnail ready for saves made while paused. This is a regular hitch. | Capture only when a save happens and when the pause menu is about to open. Alternatively, use a small RT with `AsyncGPUReadback`. | S-M / low |
| A2 | `PixelNextUp.cs:226` LateUpdate -> `PixelTierPicture.Render` | Once "What's Next" is owned, each chip does a full `Camera.Render` every frame, including duplicate tiers and fading chips. That is 3-5 extra render passes per frame. | Inside `PixelTierPicture.Render`: skip a tier that was already rendered this frame and cap renders to ~25 fps. This also fixes the BlockTooltip, CollectionLog, Database, SkillLevelPopup and LookStudio callers. | S / low |
| A3 | `PixelShopPictures.cs:161`, `PixelShopOperation.cs:310` | Every visible shop picture runs `cam.Render()` every frame while the shop is open (~10 passes). It also builds a `"tier:"+type` key string per slot per frame. | Same fps cap / round-robin, plus cached key strings. | S / low |
| A4 | `PixelLog.cs:423-494` SkinIcon / RenderSkin | Achievements/Goals tab: one `Camera.Render` per tier per frame. It is also a **private copy of PixelTierPicture**. | Switch to `PixelTierPicture` (it then gets the A2 cap). | S-M / low |
| A5 | `PixelClicker.cs:1975` RefreshUI (from every `AddCurrency`) | Runs `FormatNumber` for all ~145 tiers on every click, including every vacuumed husk. The labels it fills (`countLabel`, `summaryLabel`, `nextUnlockLabel`) are never assigned in code. | Return early when no label is set; format only the tiers that have a label. | S / none |
| A6 | `PixelSkillsWindow.cs:288` `Changed += RefreshWindow` | `AddXpFlat` raises `Changed` twice per block, so with the Skills window open every click (auto clicks too) fully re-lays out cards, perks and the tree. This bypasses the 0.4 s timer. | Have `Changed` only mark the window dirty (`refreshTimer = 0`). | S / none |
| A7 | `PixelUI.cs:970` PopupRoutine | Every "+N" popup is a new GameObject + TextMeshProUGUI + coroutine + Destroy, with no cap. Setting the outline per popup probably creates a font material instance every time (possible leak, one draw call each). | Pool of ~16 labels, oldest reused first, sharing one outline material. | M / low |
| A8 | `PixelClicker.TryGetBlockAt` (:1036) / `IsLiveCubeAt` (:1067); `PixelConsumables.UpdateArming` (:1446); `PixelPets.PetUnderPointer` (:727) | Several `Physics.RaycastAll` + sorts every frame from AimCursor, BlockTooltip, BlockHold, PowerClick, device arming and pets (pets run even with zero pets). Husk checks use `oldPixels.Contains` (O(n)). Device hit-tests call `GetComponentsInChildren<Renderer>` + `.name` per device per frame. | One cached pointer query per frame in PixelClicker (`RaycastNonAlloc`, static buffer, `PixelHuskQuery.Get` instead of Contains). Return early when there are no devices/pets. Cache device renderers when the device is built. | S-M / low |
| A9 | `PixelCubeIcon.cs:38-68` (HANDOFF #7, still open) | Every icon calls `SetVerticesDirty` every frame and allocates `UIVertex[4]`/`Vector3[4]`. With the Log open that is dozens of mesh rebuilds per frame. `PixelUltraSwatch` likewise forces the pinned Inventory canvas to rebuild every frame. | Static buffers, ~25 fps, skip culled icons; give animated bits their own nested Canvas. | S / low |

## B. Per-click churn (grows with auto-click speed)

| # | Where | Problem | Fix | Effort/Risk |
|---|---|---|---|---|
| B1 | `PixelClickerHusks.cs:578-810` (HANDOFF #12/#13) | Each husk is a new GameObject + 6-12 AddComponents, and each husk runs its own Update/FixedUpdate (Despawn, PopIn, ScaledGravity, Impact; plus Wobble/Spin/Shine/... looks). `OldPixelImpact.OnCollisionEnter` calls GetComponent on every contact. | Short term: `collision.rigidbody` + `PixelHuskQuery`; cache the per-type bounce sound id, trail curves and gradients. Long term: a husk pool + one manager loop. | S now, L pooling / medium |
| B2 | `PixelFarmFx.cs:101/116`, `PixelFirePixel.cs:166`, `PixelElectricFx.cs:72-146` | Every hit on gathering / fire / electric blocks builds new GameObjects + ParticleSystems (and a FractureFx Mesh), destroyed seconds later. Not covered by the earlier click-fx pool. | A small pool in `PixelFx`, re-armed with `PixelFx.Configure`. | M / low |
| B3 | `PixelSkills.LeftOutOfSpawns` (:881), `Find` (:639), `GuestOn` (:977); `TryGetFiller/Hazard` | Picking the next block calls these for every unlocked tier, three times, with a closure `Find(p => ...)`, `type.ToString()`, `Enum.TryParse` and `Split` on every roll. | Plain for-loops, a `HashSet<PixelType>` for guests, parse filler/hazard once per pack. | S-M / low |
| B4 | `PixelSkills.AddXpFlat` / `LevelOf`, `HasStandardNode` (:1109) | Builds `"skill."+id+".xp"` 3x and loops up to 99 levels twice per click. `HasStandardNode` builds a string on each call, every frame, from PowerClick, BlockHold, NextUp and Shuffle. | Store keys on SkillPack/TreeNode; a node-unlocked bool cache reset on unlock/load. | S / none |
| B5 | `PixelLog.OnPixelCollected` (:647) | `string.Format` + TMP text on every click, even with the Log closed. | Return early when the panel is closed. | S / none |

## C. Leaks (objects created and never destroyed)

All S / low-none: give each a shared material + `MaterialPropertyBlock`, or destroy it with its owner.
- `PixelFirePixel.cs:119` PixelFxRing: a new ring Mesh + material instance per spawn (~20 call sites: Fire, farm, treasure...). Cache one ring mesh.
- `PixelMold.cs:197-247`: a new Material per moldy husk / forage glow.
- `PixelLooks.cs:1162` ShineMesh: not cached, and `PixelLookShine.Awake` orphans the original after Instantiate.
- `PixelWeakspotMinigame.cs:497` (also used by Fishing: 2 per spot), `PixelMeteorMinigame.cs:373/638/683`, `PixelBreakoutMinigame.cs:434`, `PixelEvilTreeMinigame.cs:221`, `PixelGhostMinigame.BuildBackpackSprite`, `PixelPets.cs:613` PhysicsMaterial.
- `PixelHuskQuery.cs:39-55`: the cache only prunes from `Near()`, so dead husk entries pile up all session. Remove the entry in `OldPixelInfo.OnDestroy`, or prune every N inserts.

## D. Per-physics-step husk scans

| # | Where | Fix | Effort/Risk |
|---|---|---|---|
| D1 | `PixelClickerHusks.IsFlyingPixel` (:896): uncached `GetComponent<OldPixelFlight>` per husk per step (PixelViewBounds:139 always on, 24 caller files); `IgnoresViewBounds` -> `PixelLooks.Find` per husk per step | A flight flag on OldPixelInfo / the PixelHuskQuery entry; cache the looks result in ViewBounds' infoCache | S / low |
| D2 | `OldPixelGravityWell.FixedUpdate` (`PixelLooks.cs:1768`): GetComponent before the radius check, plus lambda RemoveWhere/Sort every step | Distance check first, `PixelHuskQuery.Get`, cached comparer | S / low |
| D3 | `PixelConveyor.cs:683-713` (husks x belts, riders^2), `PixelConnector.cs:289`, `PixelElectricLinks.cs:229/330` (allocates two Dictionaries every frame with zero links) | Return early when empty; reuse collections; one shared "loose husks this step" list in PixelHuskQuery; per-lane sorted riders | S for the early-outs, M for the list / medium |
| D4 | `PixelClickerSkillBlocks.cs:33` Solar scan every 0.15 s even when Solar is locked; `PixelStream.cs:58` RaycastAll per streamed husk per step; `PixelPadMinigame`, `PixelPathCarrier`, `PixelNuclearPixel`, `PixelSeed.WaterPixel` GetComponent per husk | Unlock check; NonAlloc; `PixelHuskQuery.Get` | S / none |

## E. Per-frame strings / layout / scene searches (small each, many of them)

- `PixelFind.First` in per-frame code -> `Cached`: `PixelFloorModes.cs:111` (every painted floor, always), `:177`, `PixelFloorLive.cs:896`, `PixelFloor.cs:889`, `PixelSkyboxRain.cs:34`, `PixelAchievements.cs:881`, `PixelSkillsTutorial.cs:80`, `PixelCrashLog.cs:359` (4 searches every 2 s), `PixelBlockTooltip.cs:270`, `PixelShop.cs:2267`.
- `PixelFloor.ApplyVisibility` (:939) runs `GetComponentsInChildren<Renderer>` every frame; `ApplyProps` (:885) runs at full cost every frame on the title screen.
- `PixelKeys.Replace` builds ~30 strings per call and runs every frame from the Time Stop meter (`PixelTimeStop.cs:438`) and the placing message (`PixelConsumables.cs:2229`). Cache inside `Replace`, cleared on `PixelKeys.Changed`.
- `PixelStatusBox.LateUpdate` calls `GetPreferredValues` every frame for every box (hidden ones too), and `PixelStatusLines.Host.Update` rebuilds rich text every frame.
- Text formatted every frame while it does not change: `PixelUI.UpdateGuide` (:2330), `UpdateActiveHud` (:2398), `PixelPlacedDevice.cs:293` timers, `PixelCollectionGallery.cs:432`, `PixelLog.UpdateAchievementTip` (:607), tooltips re-measuring (`PixelSkillsWindow.UpdateTip`, `PixelShopCurrency`, `PixelCrafting.UpdateTip`).
- `PixelRobotWorker.cs:984/441`: `new MaterialPropertyBlock` per light per frame.
- `PixelSaveGame.Update` (:530) reads `PlayerPrefs.GetInt` every frame; the autosave writes pretty-printed JSON (:735). Turn pretty-printing off.
- Settled animations still writing every frame: `PixelFancyButton` (`PixelMenuFx.cs:231`), `PixelDockedButton` (`PixelHud.cs:541`), `PixelCombo` effects at alpha 0.

## F. Shared helpers that would remove whole classes of the above

1. **Pointer / floor raycast helper** (HANDOFF #19): one per-frame cached `RaycastNonAlloc` query + sorted hits. Replaces about 10 copies.
2. **PixelHuskQuery as the only husk lookup**: add `flight` / `despawn` fields, prune on destroy, and a "loose husks this step" list for the machines.
3. **Shared tinted materials + one static MaterialPropertyBlock** (`SetColor(renderer, color)`), a cached outlined TMP font material, and a cached ring mesh: these fix most of section C.
4. **Throttle inside PixelTierPicture.Render**: fixes A2-A4 in one place.
5. **Per-enum string table** (bounce sound ids, stat keys `"skill."+id+".xp"`, `"charges.used."+type`).
6. **A "format only when the input changed" UI helper** next to `PixelUIKit.SetText` (TMP already skips identical strings, so the waste is in building them).

## Checked and fine

Floor animated redraw (throttled + buffer reused), PixelAudio voice pool, PixelBasicFx pool, block/look texture caches, Shop/Inventory/Log/Bank/Toggles/Stats refresh timers, PixelStats, PixelSystems (one-time searches), Recycler capture (10 Hz + PixelHuskQuery), dev tools (all return early when closed), minigame idle Updates, PixelPrefs.Save (only on settings changes).
