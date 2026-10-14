# Pixel Clicker – project notes

3D incremental clicker in Unity (C#, TextMeshPro). All code lives in `Assets/Scripts/`. Everything UI is built at runtime (no prefabs/scene UI required). Nothing has been compiled or run by Claude – the user tests in Unity.

## Core architecture

| Script / system | What it is (first sentence) | Details |
|---|---|---|
| `PixelClicker.cs` | Core. Click-me hint: while the Inventory UI's guide text ("Click N more...") shows and nothing has been collected yet (PixelClicker.ClickHint, set by PixelUI.UpdateGuide; | in the header comment of `PixelClicker.cs` |
| `PixelLooks.cs` | Per-pixel-type 3D look, separate from the tier data (the UI keeps the tier colour): | `docs/systems/PixelLooks.md` |
| `PixelCameraIntro.cs` | Camera intro (added by PixelClicker.Awake): | in the header comment of `PixelCameraIntro.cs` |
| `Editor/PixelShaderKeepAlive.cs` | Editor-only ([InitializeOnLoad], menu Pixel Clicker > Rebuild Shader Keep-Alive Materials): | in the header comment of `PixelShaderKeepAlive.cs` |
| `PixelShaders.cs` | PixelShaders.SpriteDefault(): the only place that looks up the Sprites/Default shader (unlit, alpha-blended, vertex colours - trails, glow shells, face marks, arrows, stars, hole trails). | in the header comment of `PixelShaders.cs` |
| `PixelFind.cs` | PixelFind.First<T>() / Count<T>(): the only place with the FindFirstObjectByType / FindObjectOfType version check. | in the header comment of `PixelFind.cs` |
| `PixelUIKit.cs` | Shared static helpers for code-built UI: EnsureEventSystem, CreateCanvas, CreateText, CreateButton, Stretch, CreateDropdown (arrow = a drawn triangle sprite), CreateInputField (white 3 px blinking car | `docs/systems/PixelUIKit.md` |
| `PixelMinigame.cs` | Abstract base for minigames (registry PixelMinigame.All/Find(id), Id, Running, Activate/Deactivate, optional goal tracker: | in the header comment of `PixelMinigame.cs` |
| `PixelBlackholeMinigame.cs` | Black hole minigame (Minigames tab, a pack with unlocksMinigame = "blackhole"): | in the header comment of `PixelBlackholeMinigame.cs` |
| `PixelMinigameLimits.cs` | Limits how many minigames have something on screen together (minAtOnce 2 / maxAtOnce 3, limitAtOnce, quietSecondsAfterPlay 30 (nothing spawns that soon after Play; | in the header comment of `PixelMinigameLimits.cs` |
| `PixelGhostMinigame.cs` | Ghost minigame (bought in the shop's Minigames tab via a pack with unlocksMinigame = "ghost"): | in the header comment of `PixelGhostMinigame.cs` |
| `PixelMeteorMinigame.cs` | Meteor minigame (pack unlocksMinigame = "meteor", Minigames tab): | in the header comment of `PixelMeteorMinigame.cs` |
| `PixelEvilTreeMinigame.cs` | Evil Tree minigame (id eviltree, pack unlocksMinigame = "eviltree", Minigames tab, needs the Woodcutter's Pack): a sapling sprouts, grows into a giant, flails so it is hard to click and has a health bar; felling it pays Magic Wood (the minigame's material / tracker) and Woodcutting XP only (PixelMinigame.SkillXpTarget). | in the header comment of `PixelEvilTreeMinigame.cs` |
| `PixelStardustMinigame.cs` | Stardust minigame (id stardust, passive: Running = the Meteor pixel is unlocked, no pack of its own; | in the header comment of `PixelStardustMinigame.cs` |
| `PixelMeteorSolar.cs` | Meteor and Solar rework. Meteor (PixelLook.meteorTexture + damageCracks + cometTail; | `docs/systems/PixelMeteorSolar.md` |
| `PixelRockMesh.cs` | Jagged rocky pixels: PixelLook.jagged (Obsidian, Meteor, Rock, the six ores; | in the header comment of `PixelRockMesh.cs` |
| `PixelVacuumFx.cs` | The Vacuum pixel's look (looksVersion 38 replaced the saved look; | in the header comment of `PixelVacuumFx.cs` |
| `PixelGelFx.cs` | The Ghost pixel's look (looksVersion 39 replaced the saved look): | in the header comment of `PixelGelFx.cs` |
| `PixelGuideVendor.cs` | Cubie, the suspicious cube guide / shopkeeper (added by PixelClicker.Awake; | in the header comment of `PixelGuideVendor.cs` |
| `PixelShopNudge.cs` (+ `PixelGuideVendor.ShopHold`) | Shop hold after Cubie's second visit (added by PixelClicker.Awake): | in the header comment of `PixelShopNudge.cs` |
| `PixelCubiePeek.cs` | Cubie peeks out from behind the Save / Load window (component added to the pause menu by PixelPauseMenu.BuildSaveLoadPanel, Setup(panelRect, isActive)): | in the header comment of `PixelCubiePeek.cs` |
| `PixelTitleWanderers.cs` | Vendors wandering the main menu (component added to the title screen in PixelTitleScreen.Build, layer under foreground, behind the bar / logo / buttons): | `docs/systems/PixelTitleWanderers.md` |
| Main menu camera / floor / welcome-back flow | The camera swing is smoother (PixelCameraIntro.Apply turns against an 'up' that blends from world up to the scene camera's own up (upRef), so a TOP-DOWN scene camera no longer makes the picture flip 1 | `docs/systems/MainMenuFlow.md` |
| `PixelCubiePeek.cs` (more) | The Cubie peek is now also on the Settings screen and the Key Bindings screen (PixelPauseMenu.AddCubiePeek(panel): | in the header comment of `PixelCubiePeek.cs` |
| `PixelFeedback.cs` | Give feedback (for testers): the pause menu's long green Give feedback button (right above Quit; | in the header comment of `PixelFeedback.cs` |
| `PixelVisitorMinigame.cs` / `PixelTinkererMinigame.cs` / `PixelWizardMinigame.cs` / `PixelEntrepreneurMinigame.cs` / `PixelFarmerMinigame.cs` | Visitor minigames (no shop packs any more - started by PixelGuideVendor; | `docs/systems/PixelVisitorMinigame.md` |
| `PixelProspectorVendor.cs` | The Prospector (id prospector, 'Pip the Prospector'; | in the header comment of `PixelProspectorVendor.cs` |
| `PixelLumberjackVendor.cs` | The Lumberjack (id lumberjack, 'Big Tim the Lumberjack'; | in the header comment of `PixelLumberjackVendor.cs` |
| `PixelEggMinigame.cs` / `PixelBeanMinigame.cs` | Two very rare cat visitors (ids egg, bean; | `docs/systems/PixelEggMinigame.md` |
| `PixelOvercharge.cs` / `PixelElectricLinks.cs` / `PixelBolts.cs` | Electric pixel abilities (both components added by PixelClicker.Awake). | `docs/systems/PixelOvercharge.md` |
| `PixelChargeBooster.cs` / `PixelLightningRod.cs` | Electric devices (DeviceKind.ChargeBooster / LightningRod, defaults auto-added like the fan, cost Electric pixels; | in the header comment of `PixelChargeBooster.cs` |
| `PixelElectricFx.cs` | Looks / effects of the Electrician pixels: | in the header comment of `PixelElectricFx.cs` |
| `PixelObsidianFx.cs` | Obsidian flair: natural black glass (dark neutral bevel rim, no emission / violet; | in the header comment of `PixelObsidianFx.cs` |
| `PixelDragonWish.cs` | Dragon Cubes + the wish. Seven rare-drop tiers (PixelType.DragonCube1..7, PixelTier.rareDrop, helpers PixelClicker.IsDragonCube/DragonCubeStars; | `docs/systems/PixelDragonWish.md` |
| `PixelGodFind.cs` / `PixelGodSynth.cs` | The three Egyptian God Cubes (PixelType.GodSlifer / GodRa / GodObelisk, rare drops about 1 in 7 million, only one of each can be held): own looks, find shows (storm + lightning serpent / sun + wings / fist slam) and code-made sounds. | `docs/systems/PixelGodCubes.md` |
| `PixelInfinity.cs` | Prime Cubes (6 rare one-of-each drops with the God Cubes' rules and own shows), the Wizard's Prime Gauntlet (DeviceKind.Gauntlet) and its window that forges the Prime Cubelette. | `docs/systems/PixelGodCubes.md` |
| `PixelCollectionLog.cs` | Collection Log book (book button at the top left of the Log window): a two-page spread, click a page to turn it; Dragon Cubes, God Cubes, Prime Cubes and the Cubelette show as '???' until found. | in the header comment of `PixelCollectionLog.cs` |
| `PixelCollectionGallery.cs` | Collection Log gallery (camera button on an entry page): a pitch black first person room (20x20, walkable 10x10, neon wireframe floor) with every collectible floating and lighting its surroundings; built on entering, destroyed on leaving, shown only through its own camera on a full-screen canvas. | in the header comment of `PixelCollectionGallery.cs` |
| `PixelSnakeMinigame.cs` | Snake minigame (pack unlocksMinigame = "snake", Minigames tab, needs the RGB pack; | in the header comment of `PixelSnakeMinigame.cs` |
| `PixelSortMinigame.cs` | Sorting Race minigame (id sort, pack unlocksMinigame = "sort", Minigames tab, needs the RGB pack; | in the header comment of `PixelSortMinigame.cs` |
| `PixelDashMinigame.cs` | Cube Dash minigame (id dash, pack unlocksMinigame = "dash", Minigames tab, needs the RGB pack, 50K each; | in the header comment of `PixelDashMinigame.cs` |
| `PixelTrafficMinigame.cs` / `PixelTrafficSynth.cs` | Red Light Green Light minigame (id traffic, pack unlocksMinigame = "traffic", Minigames tab, needs the RGB pack, 80K each; | in the header comment of `PixelTrafficMinigame.cs` |
| `PixelEvadeMinigame.cs` | Cube Evasion minigame (id evade, pack unlocksMinigame = "evade", Minigames tab, needs the RGB pack, 70K each; | in the header comment of `PixelEvadeMinigame.cs` |
| `PixelWeakspotMinigame.cs` | Weak Spots (id weakspot, the skills' minigame; | in the header comment of `PixelWeakspotMinigame.cs` |
| `PixelBlinkMinigame.cs` | Cube Blink minigame (id blink, pack unlocksMinigame = "blink", Minigames tab, needs the RGB pack, 60K each; | in the header comment of `PixelBlinkMinigame.cs` |
| `PixelBreakoutMinigame.cs` | Breakout minigame (id breakout, pack unlocksMinigame = "breakout", Minigames tab, needs the RGB pack; | in the header comment of `PixelBreakoutMinigame.cs` |
| `PixelBombMinigame.cs` | Bomb Defusal minigame (pack unlocksMinigame = "bomb", Minigames tab, needs the RGB pack): | in the header comment of `PixelBombMinigame.cs` |
| `PixelPadMinigame.cs` | Ultra Holes (the reworked Ultra Pad; id pad, pack unlocksMinigame = "pad", Minigames tab, needs the RGB pack; | `docs/systems/PixelPadMinigame.md` |
| `PixelCombo.cs` | Combo meter (shop upgrade "Combo Meter", UpgradeEffect.ComboMeter, 5 levels; | in the header comment of `PixelCombo.cs` |
| `PixelCrafting.cs` | Crafting (shop pack with unlocksCrafting, Upgrades tab): | `docs/systems/PixelCrafting.md` |
| `PixelPets.cs` | Pets (added by PixelClicker.Awake, singleton PixelPets.Instance): | `docs/systems/PixelPets.md` |
| `PixelPetsCustomize.cs` | Part of partial class PixelPets: names and outfits. | `docs/systems/PixelPetsCustomize.md` |
| `PixelPetsStudio.cs` | Part of partial class PixelPets: the Outfit Studio (button 'Design outfits' in the Customize Pet window, own canvas customizeSortingOrder + 20, PixelWindows 170). | in the header comment of `PixelPetsStudio.cs` |
| `PixelToggles.cs` | Toggles window: a button docked to the middle of the right edge (PixelHud.Dock with anchor (1, 0.5) = side docking, slides sideways) stays hidden until the Auto Clicker is bought (hideUntilAutoClicker | `docs/systems/PixelToggles.md` |
| `PixelMyWindow.cs` | My Window (the player's own window; component added by PixelClicker.Awake): | in the header comment of `PixelMyWindow.cs` |
| `PixelCustomKeys.cs` | Custom keybinds: bind any item (potion, device...) or tool to your own key; the window is the Keybinds tab of My Window; saved per save file. | in the header comment of `PixelCustomKeys.cs` |
| `PixelBlockTooltip.cs` | Block hover tooltip (added by PixelClicker.Awake; | in the header comment of `PixelBlockTooltip.cs` |
| `PixelPixelGuide.cs` / `PixelGuideTexts.cs` | Pixel Guide (added by PixelClicker.Awake): | in the header comment of `PixelPixelGuide.cs` |
| `PixelDatabase.cs` / `PixelDatabaseWindow.cs` | Game Database (opened by the Database tab of the pause menu's Game Guide screen, PixelPauseMenu.BuildGuideScreen -> PixelDatabaseWindow.Toggle(); | in the header comment of `PixelDatabase.cs` |
| `PixelGrab.cs` | Pixel Grabbing (unlocked from the start - Awake sets grabbingActive; | in the header comment of `PixelGrab.cs` |
| `PixelTimeStop.cs` | Time Stop (shop pack with unlocksTimeStop, Upgrades tab): | in the header comment of `PixelTimeStop.cs` |
| `PixelBank.cs` | Pixel Bank (shop pack with unlocksBank, Upgrades tab): | `docs/systems/PixelBank.md` |
| `PixelSkills.cs` / `PixelSkillsWindow.cs` | Skill system SHELL (added by PixelClicker.Awake, PixelSkills.Instance, disableSkills): | `docs/systems/PixelSkills.md` |
| `PixelTogglesTutorial.cs` | Toggles tutorial (first-time tips): after the Auto Clicker is bought Cubie narrates the Toggles tab / window, then hints at Skills and the Woodcutter's Pack; the skills tutorial waits for it. | in the header comment of `PixelTogglesTutorial.cs` |
| `PixelSkillsTutorial.cs` / `PixelTutorialDim.cs` | Skills tutorial (part of partial class PixelSkills, started from the window's Update by UpdateTutorialStart the first time a skill pack is available and the screen is calm: | in the header comment of `PixelSkillsTutorial.cs` |
| `PixelSkillsStandard.cs` | Part of partial class PixelSkills: Standard is a real skill. | in the header comment of `PixelSkillsStandard.cs` |
| Skill tree window (`PixelSkillsWindow.cs`, `PixelScrollFadeX.cs`) | The tree's nodes sit in a SIDEWAYS scroll view (nodeView + nodeScroll, wheel / drag; | in the header comment of `PixelSkillsWindow.cs` |
| `PixelSkillsMastery.cs` | Part of partial class PixelSkills: Mastery, one bar for the progress of ALL skills together, along the bottom of the Skills window (BuildMasteryBar, RefreshMastery, gold fill, 'Mastery 12.5% - x / y XP', tooltip). | in the header comment of `PixelSkillsMastery.cs` |
| `PixelSkillsMinigames.cs` | Part of partial class PixelSkills: the minigame XP multiplier. | in the header comment of `PixelSkillsMinigames.cs` |
| `PixelSkillsBonus.cs` | Part of partial class PixelSkills: the bonus XP bank. | in the header comment of `PixelSkillsBonus.cs` |
| `PixelSkillsTracker.cs` | Part of partial class PixelSkills: the pinned skill tracker. | in the header comment of `PixelSkillsTracker.cs` |
| `PixelSkillsPopups.cs` | Part of partial class PixelSkills: '+n <Skill> XP' popups while a skill trains (AddXp calls QueueXpPopup; | in the header comment of `PixelSkillsPopups.cs` |
| `PixelTube.cs` | Static PixelTube.Build(points, radius, sides, mesh): | in the header comment of `PixelTube.cs` |
| `PixelStatusBox.cs` | Shared top status box: potion timer, placing line and the takeover minigames' status lines sit in a window-style box that slides out from under the black bar (over its edge effect) and back; PixelStatusBox.Create / Show / IsShown. | in the header comment of `PixelStatusBox.cs` |
| `PixelStatusLines.cs` | Keyed text lines (Set / Clear) for a second status box hanging under the others: Black Hole counter, Bomb timer / result, Ghost / Meteor / Evil Tree messages. | in the header comment of `PixelStatusLines.cs` |
| `PixelHud.cs` | Screen frame: black bar along the top and bottom (barHeight) and the docked buttons. | in the header comment of `PixelHud.cs` |
| `PixelBarEdgeFx.cs` | Bar styles (Settings > Display 'Bar style', PixelBarStyle.Current): what glows along the black bars' inner edge - Neon glow, Pixel equalizer (audio-reactive pixel columns), Pixel wave, Marquee, Scanner, Thin line; one PixelBarEdgeFx per edge, ticked by PixelHud and PixelTitleScreen. | in the header comment of `PixelBarEdgeFx.cs` |
| `PixelAudio.cs` (UFO beam) | The main-menu UFO's abduction plays ufo_beam (code-made PixelSynth.UfoBeam: | in the header comment of `PixelAudio.cs` |
| `PixelAudio.cs` | Sound system. Alt-tab keeps the music track: | `docs/systems/PixelAudio.md` |
| `PixelKeys.cs` | Rebindable keys: PixelAction enum (TimeStop, Hose, RotateLeft, RotateRight, EventLog, TimeSlow, FirstPerson), defaults + the player's choices (PlayerPrefs PixelClicker.Key.*), PixelKeys.Pressed/Held(action) (both input systems; | in the header comment of `PixelKeys.cs` |
| `PixelMenuKeys.cs` | Blank-by-default keyboard shortcuts for the docked menu buttons: | in the header comment of `PixelMenuKeys.cs` |
| `PixelDisplaySettings.cs` | Player display / accessibility settings (PlayerPrefs PixelClicker.Setting.*, applied before the first scene): | in the header comment of `PixelDisplaySettings.cs` |
| `PixelDebug.cs` | PixelDebug.Info(msg, ctx): diagnostic logging that is compiled out of release builds (Editor/dev builds only). | in the header comment of `PixelDebug.cs` |
| `PixelInput.cs` | Static input helpers for both input systems (PointerPosition, LeftPressed, RightPressed, PointerOverUI); | in the header comment of `PixelInput.cs` |
| `PixelViewBounds.cs` | Keeps old pixels inside the camera's view with no scene objects: | in the header comment of `PixelViewBounds.cs` |
| `PixelFloorModes.cs` | What the floor shows while 'Display floor' is off (PixelFloor.OffMode: Invisible / Flat plane / Landing pulse) and the PixelFloorPulse ripples where old pixels land. | in the header comment of `PixelFloorModes.cs` |
| `PixelFloorFade.cs` | Floor edge fade (partial class PixelFloor, UpdateEdgeFade from LateUpdate; | in the header comment of `PixelFloorFade.cs` |
| `PixelFloor.cs` | Floor styles (added by PixelClicker.Awake): | `docs/systems/PixelFloor.md` |
| `PixelDayNight.cs` | Day / night cycle (added by PixelClicker.Awake; | in the header comment of `PixelDayNight.cs` |
| `PixelOrthoView.cs` | Flat view (added by PixelClicker.Awake; Settings > Display > 'Flat view' = PixelOrthoView.Enabled, off by default, synced per save): | `docs/systems/PixelOrthoView.md` |
| `PixelSkybox.cs` | Sky styles (added by PixelClicker.Awake, implements IPixelLookSource): | `docs/systems/PixelSkybox.md` |
| `PixelLookSource.cs` / `PixelNoise.cs` | IPixelLookSource = anything with looks to step through (Usable, Current, StyleName, PreviewTexture/Color/Rect, Step); | in the header comment of `PixelLookSource.cs` |
| `PixelViewWindow.cs` | The View window (opens only with its key, PixelAction.ViewWindow, default V; replaces the retired F view key and X zoom key): view tick boxes (First person / Orbital / Top down = the normal view; no separate Default view), an Orthographic tick box (not in first person), a zoom slider and the orbit controls (auto orbit, turn / tilt buttons). | in the header comment of `PixelViewWindow.cs` |
| `PixelOrbitCamera.cs` | Orbital camera and the BASE VIEW (the scene camera starts 6 units from the cube at 89 degrees = zoom 1.0 of the View window; added by PixelClicker.Awake; | in the header comment of `PixelOrbitCamera.cs` |
| `PixelZoom.cs` | Zoom of the normal view (the View window's slider): the camera slides along the line to the cube and a hidden HomeCamera keeps the view bounds on the normal view. | in the header comment of `PixelZoom.cs` |
| `PixelFirstPerson.cs` | First person mode (added by PixelClicker.Awake; | `docs/systems/PixelFirstPerson.md` |
| `PixelPlacedDevice.cs` | Abstract base of placed devices: countdown, timer text facing the camera, shrink-away (InitCommon, OnTick, IsDying). | in the header comment of `PixelPlacedDevice.cs` |
| `PixelPop.cs` | Open / close animation for windows and tip boxes (component on the Inventory box, the Log panel, the Got-it box, the Shop panel, the Crafting window, the Toggles window and every pause-menu screen: | in the header comment of `PixelPop.cs` |
| `PixelNotice.cs` | PixelNotice.Show(message, seconds, onClosed, compact, small, aboveCombo, nearLog) (nearLog = small box at the bottom left beside the event log, used by the event_log tip shown at the first tier unlock; | `docs/systems/PixelNotice.md` |
| `PixelHints.cs` | First-time tips: Inspector list of Hint (id, enabled, text; | `docs/systems/PixelHints.md` |
| `PixelTitleScreen.cs` | Start screen (added by PixelClicker.Awake): | `docs/systems/PixelTitleScreen.md` |
| `PixelTitleMusic.cs` | Main menu background music selector (previous / next arrows + track drop-down; PixelAudio.SelectMusicTrack): shown when the title opens, fades after ~10 s without activity, back on hover. | in the header comment of `PixelTitleMusic.cs` |
| `PixelEyedropper.cs` | Eyedropper (created on demand by PixelEyedropper.Begin(callback)): | in the header comment of `PixelEyedropper.cs` |
| `PixelHold.cs` | PixelHold.Update(caller, target, seconds, label, colour): | in the header comment of `PixelHold.cs` |
| `PixelFanDevice.cs` | (extends PixelPlacedDevice) Runtime behaviour of a placed fan (device kind = Fan): | in the header comment of `PixelFanDevice.cs` |
| `PixelSorterDevice.cs` | (extends PixelPlacedDevice) Placed Sorter (device kind = Sorter): | `docs/systems/PixelSorterDevice.md` |
| `PixelSprinklerDevice.cs` | (extends PixelPlacedDevice) Sprinkler (DeviceKind.Sprinkler 9, auto-added like the fan by EnsureDefaultPotions, Utilities card; | in the header comment of `PixelSprinklerDevice.cs` |
| `PixelMagnetSorterDevice.cs` | (extends PixelPlacedDevice) Magnetized Sorter (DeviceKind.MagnetSorter 11, Device.craftOnly = never sold: | in the header comment of `PixelMagnetSorterDevice.cs` |
| `PixelQuantumDevice.cs` | (extends PixelPlacedDevice) Quantum Organizer (DeviceKind.QuantumOrganizer 12, auto-added like the fan by EnsureDefaultPotions, Utilities card; | in the header comment of `PixelQuantumDevice.cs` |
| Attuned devices (`PixelConsumables.AppendAttunedDevices`, `PixelCrafting.FindAttuneRecipe`) | Crafting a Vacuum Device, Fan or Magnet with a plain (single-type) potion (ResultKind.Attuned, any box order, uses up one of each) makes an attuned copy that only affects old pixels of the potion's pixel type. | `docs/systems/PixelConsumables.md` |
| `PixelFunnel.cs` / `PixelFunnelDevice.cs` | Funnel consumable (DeviceKind.Funnel 16, one at a time, 600 Gray, Utilities card): a glass cone under the clicker cube catches falling old pixels and carries them down a see-through tube to a floor output the player clicks (belt latch like the Wood Chipper); | in the header comment of `PixelFunnel.cs` |
| `PixelStorageCrate.cs` | (extends PixelPlacedDevice) Storage Crate (DeviceKind.StorageCrate 13, auto-added like the fan, Utilities card; | in the header comment of `PixelStorageCrate.cs` |
| `PixelMagnetDevice.cs` | (extends PixelPlacedDevice) Magnet (DeviceKind.Magnet 10, auto-added like the fan by EnsureDefaultPotions, Utilities card; | in the header comment of `PixelMagnetDevice.cs` |
| `PixelVacuumDevice.cs` | (extends PixelPlacedDevice) Runtime behaviour of a placed vacuum device (lathe-turned body PixelConsumables.BuildVacuumBody / VacuumLatheMesh: | in the header comment of `PixelVacuumDevice.cs` |
| `PixelConsumables.cs` | Potions (one per pixel type, editable list: | `docs/systems/PixelConsumables.md` |
| `PixelUI.cs` | "Inventory" toggle box (top-left) (rows = swatch + plain light name + right-aligned amount (Currency tab: | `docs/systems/PixelUI.md` |
| Inventory currency groups (`PixelUI.Refresh`, `CurrencyGroup`) | The Currency tab lists the pixel rows in GROUPS under foldable headers (click a header, '- PURE' / '+ PURE'; | `docs/systems/PixelUI.md` |
| `PixelShop.cs` / `PixelShopUI.cs` / `PixelShopDefaults.cs` | One partial class PixelShop in three files: | `docs/systems/PixelShop.md` |
| `PixelOperation.cs` / `PixelShopOperation.cs` | Upgrades > Operation (the third sub-tab of the shop's Upgrades tab, operationSubTabText; | in the header comment of `PixelOperation.cs` |
| `PixelTalk.cs` | Talking blips for dialogue boxes (PixelNotice tip / tutorial boxes, Cubie's speech panel): a code-made squeak per second typed letter, a speaker mute button next to each back button (saved per profile). | in the header comment of `PixelTalk.cs` |
| `PixelDevTextEdit.cs` | Dev Hub custom text: right click any hub label to type your own text (saved per profile); the hub also has a search bar and favorite / minimizable widget cards. | `docs/systems/PixelDevTools.md` |
| `PixelSnap.cs` / `PixelRightTap.cs` | Shift-snapping (grid + angle steps) while placing consumables and tester machines; PixelRightTap = quick right click vs right hold (hold deletes tester objects). | in the header comments |
| `PixelSplitter.cs` | Splitter block (tester feature): snaps to a belt end and shares the belt's pixels round-robin out of its other three sides (belts snap to those outputs). | in the header comment of `PixelSplitter.cs` |
| `PixelElevator.cs` / `PixelPathCarrier.cs` | Conveyor Elevator (tester feature): a tower that lifts pixels from a belt end up to a higher belt (two-stage placing: magnetize to a belt end, click, mouse sets the height, click); PixelPathCarrier = shared 'carry pixels along a polyline' helper (used by the Funnel, the Elevator and the Splitter). Belts can now be raised / sloped with the scroll wheel while placing. | in the header comment of `PixelElevator.cs` |
| `PixelConveyor.cs` / `PixelKiln.cs` / `PixelFactoryKit.cs` | Tester machines: conveyor belt (two-click placing, carries old pixels, right click reverses) and kiln (burns what enters its mouth into Charcoal old pixels); PixelFactoryKit = shared primitive builder. | `docs/systems/PixelFactory.md` |
| `PixelFirePixel.cs` / `PixelProspector.cs` | Fire pixel + Prospector's Pack (PixelType.Fire 25, ores Copper..Nickel 26-31; | `docs/systems/PixelFirePixel.md` |
| `PixelNuclearPixel.cs` | Nuclear pixel (PixelType.Nuclear 33, IsStandalone (no potions / seeds / crafting / pets / achievements / Value upgrades), spawn switch in Toggles; | in the header comment of `PixelNuclearPixel.cs` |
| `PixelTreasure.cs` | Treasure pixel (PixelType.Treasure 32, IsStandalone, spawn switch in Toggles; | in the header comment of `PixelTreasure.cs` |
| `PixelDragonFind.cs` | Dragon Cube find show (component added by PixelClicker.Awake; | in the header comment of `PixelDragonFind.cs` |
| `PixelRobotWorker.cs` | UFO Helper (class and shop pack keep the old name 'Robot Worker' so saves keep working; | `docs/systems/PixelRobotWorker.md` |
| `PixelFirstPersonWall.cs` | First person out-of-bounds protection: the player is kept inside the circle old pixels use (PixelFloor.TryGetEdgeCircle), put back at the entry spot after falling out of the world, and a neon wireframe wall fades in when they get near the edge. | in the header comment of `PixelFirstPersonWall.cs` |
| `PixelFirstPersonGrid.cs` | First person neon floor: grid shown only near the player, the cube and ground-resting old pixels. | in the header comment of `PixelFirstPersonGrid.cs` |
| Farming pixels / Wood / Rock | Wood is part of the Woodcutting skill now (see the Woodcutting row; | `docs/systems/SkillBlocks.md` |
| Filler blocks / base block textures (`PixelSkills.TryGetFiller`, `PixelClicker.FillerInfo`, `PixelBlockTextures.cs`) | Each skill pack can have a 'useless' FILLER block (SkillPack.fillerType = a pixel type name; | in the header comment of `PixelBlockTextures.cs` |
| Seed shell rework | Seed block: a glossy ridged seed POD (PixelLook.blockSurface Seed + blob rounded mesh, PixelBlockTextures.SeedShell(level, max): | `docs/systems/SkillBlocks.md` |
| Woodcutting (`PixelSkills.WoodcuttingId`, `PixelClicker.IsWoodBlock`) | The first, simple skill (listed right under Standard / Offline): | `docs/systems/SkillBlocks.md` |
| Hazard blocks (`PixelType.Pest` 49 = Farming, `GasPocket` 50 = Mining; `PixelClicker.IsHazard`, `TriggerHazard`, `UpdateHazardExpiry`, `PixelSkills.TryGetHazard` / `ApplyHazardPenalty` / `PenaltyFactor`) | Negative blocks in the skill packs: a tough block (Pest 6 hits, Gas Pocket 5, pays nothing) that makes up a small share of the spawns (SkillPack.hazardBaseShare 0 = 3% at level 1, hazardPerLevelShare | `docs/systems/SkillBlocks.md` |
| `PixelPestSwarm.cs` (Pest rework) | The Pest block (PixelLook.blockSurface Pest, PixelBlockTextures.PestHide(level,max): | in the header comment of `PixelPestSwarm.cs` |
| Foraging pixels / Forager's Pack | Foraging (PixelSkills.ForagingId, second base gathering skill after Woodcutting; | `docs/systems/SkillBlocks.md` |
| Fishing / ore rework | Fishing (PixelSkills.FishingId, third base gathering skill; | `docs/systems/SkillBlocks.md` |
| `PixelFilters.cs` | Screen filters (Settings > Display: CRT TV, VHS, Wild West, Noir, Night Vision, Neon Dream; URP post-processing volume + a code-drawn overlay, strength slider). | in the header comment of `PixelFilters.cs` |
| `PixelTierPicture.cs` | Shared real-model pictures of pixel types (hidden studio + camera -> RenderTexture): PixelTierPicture.Get / Render. Used by the Next-up window; the guide / shop / log / operation studios still have their own copies. | in the header comment of `PixelTierPicture.cs` |
| `PixelNextUp.cs` | The 'Next up' window (shown with Block Hold): a vertical window with three spinning cubes - the next block on top, the current one in the middle, the previous one at the bottom - that slide down when the block changes. | in the header comment of `PixelNextUp.cs` |
| `PixelBlockRules.cs` | Shared foundation for SPECIAL blocks (rare replacements of rolled blocks, e.g. Great Oak) and HOLD blocks (bonus while held with Block Hold, e.g. Whetstone): two data lists of rules. | `docs/systems/PixelBlockRules.md` |
| `PixelBlockHold.cs` | Block Hold (available from the start): hold the right mouse button on the cube's block to pull it out of the rotation while a meter drains; letting go puts it back. Holding a plain block also puts a growing STAKE on it (pays more, or nothing, when it comes round again). | in the header comment of `PixelBlockHold.cs` |
| `PixelShuffleBonus.cs` | Shuffle bonus (test): tap right click on the skill's filler block (Block Hold's quick tap) to start a combo box with a decaying bar; the combo grows each time the block it hid behind is broken and the filler is shuffled again; breaking the filler cashes in bonus XP, an empty bar auto-breaks it for a smaller bonus. | in the header comment of `PixelShuffleBonus.cs` |
| `PixelMold.cs` | Foraging's Mold hazard (zone that spreads over fallen pixels per click, shake to clean) + glowing Berry / Mushroom / Herb / Truffle old pixels. | in the header comment of `PixelMold.cs` |
| `PixelFishingSpawns.cs` | Fishing's fixed spawn table (partial class PixelClicker): Water 80.5%, Driftwood 5%, Seaweed 7.5%, Jellyfish 3%, Shell 3%, Fish 1%, plus replacement rolls (Fish -> Whale 5%, Seaweed / Shell -> Bottle 2%, Shell -> Pearl) and Bait after a Water pixel. | in the header comment of `PixelFishingSpawns.cs` |
| `PixelBurstPop.cs` | Aims a clicked pixel's old copy at a random point on a ring round the cube when the ground is a long fall away (partial class PixelClicker: TryBurstVelocity), so pixels spread over the floor instead of dropping in one stack. | in the header comment of `PixelBurstPop.cs` |
| `PixelBaitBucket.cs` | Bait Bucket consumable (DeviceKind.BaitBucket, Supplies): right-click it in Fishing and a bucket stands at a fixed floor spot until you stop Fishing; it stores Fish / Water old pixels dropped in (stored fish raise Fish's spawn weight via PixelBait.FishMultiplier). | in the header comment of `PixelBaitBucket.cs` |
| `PixelTreasureMap.cs` / `PixelWhaleFight.cs` / `PixelOceanSynth.cs` | Fishing's SPECIAL blocks (a rarer tier beside the base blocks; | `docs/systems/PixelTreasureMap.md` |
| `PixelFarmFx.cs` | Farming pixel particles: PixelFarmFx.Attach (ambient loop per PixelLook.farmFx: | in the header comment of `PixelFarmFx.cs` |
| `PixelShopTutorial.cs` | Shop tutorial (added by PixelShop): the first time the Shop button is available (PixelShop.ShopAvailable) and the screen is calm (title screen over, PixelHints.IntroHoldsGuide false, no other tip, no takeover minigame, not paused; | in the header comment of `PixelShopTutorial.cs` |
| `PixelButtonPulse.cs` | Button pulse: every shop button (the shop's own CreateButton adds PixelButtonPulse) sends a clean neon outline out from under it when pressed - a thin glowing ring (a code-drawn 9-sliced rounded-recta | in the header comment of `PixelButtonPulse.cs` |
| `PixelShopPictures.cs` | Part of partial class PixelShop: rotating 3D pictures on shop rows/cards (AddRowPicture, AddCardPicture, AddPackPicture; | in the header comment of `PixelShopPictures.cs` |
| `PixelShopConsumables.cs` | Third-and-a-half part of partial class PixelShop: | in the header comment of `PixelShopConsumables.cs` |
| `PixelAutoClicker.cs` | Timer that calls clicker.AutoCollect() (paused while a takeover minigame runs or the cube is shrunk away: | in the header comment of `PixelAutoClicker.cs` |
| `PixelAchievements.cs` | Modular achievements: Inspector list of Achievement (id, title, description, Kind, pixelType, target). | in the header comment of `PixelAchievements.cs` |
| `PixelCubeIcon.cs` | UI MaskableGraphic drawing a tiny spinning shaded 3D cube (no camera/render texture) - achievement icons. | in the header comment of `PixelCubeIcon.cs` |
| `PixelLog.cs` | Bottom-left "Log" toggle with two tabs (the old Pixels / Clicks tab was removed; | `docs/systems/PixelLog.md` |
| `PixelSaveGame.cs` | Save/load to JSON in Application.persistentDataPath (tiers, shop packs by name, auto clicker, potions owned). | `docs/systems/PixelSaveGame.md` |
| `PixelAutosaves.cs` | Autosave history (the last 100 timer autosaves kept in <persistentDataPath>/Autosaves, oldest replaced first; opened from the small 'Autosaves' button in the Save / Load window) + a screenshot saved next to EVERY save and shown in the hover tooltips (PixelSaveShots). | in the header comment of `PixelAutosaves.cs` |
| `PixelOfflineProgress.cs` | Offline progress: PixelSaveGame stores savedAtTicks (UTC) on every save and, on the startup load only, calls Grant(lastSaved). | in the header comment of `PixelOfflineProgress.cs` |
| `PixelStats.cs` | Play stats. Typed fields (manual/auto clicks, play time, pixels spent, ghosts/meteors clicked, meteors/black holes spawned, fans/vacuum devices/sorters placed, singularities fed, potions used per name | in the header comment of `PixelStats.cs` |
| `PixelWindows.cs` | CloseAllExcept(owner) closes every open window with priority < 100 (Crafting calls it when it opens, and closes itself when AnyOpenExcept(this) turns true; | in the header comment of `PixelWindows.cs` |
| `PixelPauseMenu.cs` (Settings screen) | The Settings screen is a scrolling list with section headers (Gameplay, Interface, Saving, Display, Audio, Controls). | `docs/systems/PixelPauseMenu.md` |
| `PixelMenuFx.cs` | Fancy pause menu layer (Settings > Interface > Fancy pause menu, on by default; off = the classic menu): animated main-menu buttons (hover grow, shine sweep, accent stripe, icons, staggered entrance), drifting motes + vignette, a cycling panel glow, a title line. | in the header comment of `PixelMenuFx.cs` |
| `PixelPauseMenu.cs` | Pause menu (main menu = mainColumns-column button grid LayoutMainButtons, mainPanelWidth wide; | in the header comment of `PixelPauseMenu.cs` |
| `PixelDevTools` Changelog tab | The Changelog moved from the pause menu to the Dev Tools panel as a third tab (BuildChangelogView / RebuildChangelog, changelogResource 'Changelog', newest first); | `docs/systems/PixelDevTools.md` |
| `PixelPauseMenu` How To Play (tabs) | The pause menu has one Game Guide button (between Settings and Give feedback; | `docs/systems/PixelPauseMenu.md` |
| `PixelPauseMenu` Game Guide text tabs (the Game Guide has THREE tabs: Controls, How To Play, Database; the Database tab opens PixelDatabaseWindow) | The Controls and How To Play tabs are two panels that show Assets/Resources/HowToPlay.txt and Assets/Resources/Controls.txt (the user edits these files; | `docs/systems/PixelPauseMenu.md` |
| `PixelCrashLog.cs` | Local crash / error reports (added by PixelClicker.Awake). | in the header comment of `PixelCrashLog.cs` |
| `PixelDevShapes.cs` / `PixelDevMockup.cs` / `PixelDevMockWindow.cs` | Layout dev tools ('Mock up mode' tick box in the Dev Tools Tools tab = one draggable window with a Shapes tab and a UI pieces tab, minimizable to a thin header; | in the header comment of `PixelDevShapes.cs` |
| `PixelDevProbe.cs` | Dev snapshot tools for Claude (keys F10 mark this moment = screenshot + state report, F11 inspect under the mouse; Tools buttons for a UI layout dump), reports in <persistentDataPath>/Snapshots + clipboard; `PixelDevTools` opens with PixelAction.DevTools (F9) and its dock button is hidden unless the Dev Tools tick box is on. | in the header comment of `PixelDevProbe.cs` |
| `PixelBoundsSnapshot.cs` | Dev tool (Dev Tools > Tools > 'Snapshot bounds') that writes the camera / view / cube / floor / PixelViewBounds numbers and where the viewport edges land on the floor to <persistentDataPath>/Snapshots + clipboard + screenshot, for tuning the pixel bounds. | in the header comment of `PixelBoundsSnapshot.cs` |
| `PixelBalanceReport.cs` | Dev tool (Dev Tools > Tools > 'Balance report'): a text report for pasting into any AI (per-skill XP/click, minutes to level at manual / auto speed, pay per click, outliers, Inspector levers, CSV); it previews each pack through `PixelSkills.PreviewPackId` / `PreviewLevel` so it uses the game's own spawn shares. | in the header comment of `PixelBalanceReport.cs` |
| `PixelDevHub.cs` / `PixelWindowChrome.cs` | The Dev Hub: one window (drag / minimize / pin / dock to a side) with filing-cabinet tabs (Main = PixelDevTools widgets, To-Do, Changelog, Inspector, Shapes, UI Mock) that can be reordered or popped out into floating windows; PixelWidgetBoard = draggable widget cards. PixelWindowChrome is the reusable drag / minimize / pin / dock behaviour. | `docs/systems/PixelDevTools.md` |
| `PixelDevTools.cs` | Dev tools (Editor/dev builds only unless allowInFullBuild is ticked on the component; | `docs/systems/PixelDevTools.md` |

### Data model
- `PixelTier`: name, colour/`UIColor`, `PixelType` enum (see PixelClicker.cs; values are only ever appended so saves stay valid), `TierUnlockMode` (PreviousTierThreshold / ShopOnly), `spawnWeight`, `randomPayout` + `payoutMin/Max` (a random whole amount per harvest, e.g. Electric 1-15; `amountPerClick` = the average for estimates; migrated by `packDataVersion` 3), `clicksToCollect` (tough pixels: only the last click pays; `PixelClicker.PixelHit` event for the others), `startingAmount`, `translucent` / `materialOverride`, `glow` + `glowIntensity` (emission on a runtime material copy + point light), `vacuum` flag, `spawnSwitch` (Auto = Glass, Luminescent and the `IsSpecialType` types get a spawn toggle in the Toggles window, see `PixelTier.CanSwitchOff`; `spawnDisabled` saved; `CanSpawn` used by `PickSpawnTier`), `flyAway`/`flyDirection`/`flySpeed` (old pixel streaks away in a straight camera-relative line with a smooth tapered trail instead of falling - Meteor; `flyRandomDirection` + `flyAngleRange` 10-170 deg = a random direction on the top half of the screen), `count`, `totalCollected`, `unlocked`.
- Spawn: weighted random among **unlocked** tiers only (`PickSpawnTier`).
- Tough pixels raise `PixelHit` for hits 1..n-1 and `PixelFinalHit` (UI shows 'n/n', then the payout popup follows after `toughPayoutDelay`) on the breaking click. Click flow (`CollectInternal(bool automatic)`): `AddCurrency` → `PixelCollected` event → click effects → `Vacuum()` if vacuum tier → `SpawnFallingCopy` → roll next tier → `Materialize`.
- Events: `CurrencyChanged`, `CurrencyGained`, `PixelCollected(tier, amount, automatic)`, `PixelsVacuumed(vacuumTier, total, count)`, `VacuumBreakdown(double[] perTier)`.
- Shop reward tiers are added to `PixelClicker` at runtime via `EnsureTier` (locked until bought). `EnsureTier` inserts new shop tiers before the Dragon Cubes (rare drops stay at the END of the tier list). `EnsureTier` and Awake must only **raise** `Count`, never wipe typed starting values.
- `PixelShop.EnsureDefaultPacks()` (called from `Awake` and editor-only `OnValidate` via `delayCall`) walks the `BuiltInPacks` table and inserts any missing built-in pack into the serialized list so it is editable in the Inspector. One toggle: `addDefaultPacks`. Context menu: "Add Missing Default Packs". Older saved data is upgraded by `MigrateOldPackData()`. It also removes a leftover "Pong" pack (name or minigame id `pong`) from the saved list and renumbers the other packs' requirements.

## Changelog workflow (standing instruction from the user)
- The Dev Tools panel's **Changelog** tab shows `Assets/Resources/Changelog.txt` (one change per line, newest shown first).
- **After every change you make, append one line** to that file: `[YYYY-MM-DD HH:MM UTC] what was added/changed/fixed` (get the time with `date -u +"%Y-%m-%d %H:%M"`). Keep it one plain-language sentence; commit it with the change.
- The changelog is **never cleared** (the user backs it up themselves when they want). It is also the history of what was done before: before assuming something was never built or tried, search it (`grep -i <word> Assets/Resources/Changelog.txt`) and `git log --grep=<word>`.

## Active conventions
- **Every variable must be editable in Unity**: `[SerializeField] private` + `[Tooltip]` (+ `[Header]` grouping). Nested config classes are `[Serializable]`.
- Field initializers don't apply to already-serialized components → use Awake/OnValidate fix-ups for new defaults. No native-object constructors (`new RectOffset`) in field initializers.
- Version guards: `FindFirstObjectByType` vs `FindObjectOfType` (`UNITY_2023_1_OR_NEWER`); `PhysicsMaterial`/`linearDamping` vs `PhysicMaterial`/`drag` (`UNITY_6000_0_OR_NEWER`); `InputSystemUIInputModule` vs `StandaloneInputModule` (`ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER`). Input must work with both legacy and new Input System.
- UI is code-built: ScreenSpaceOverlay Canvas + CanvasScaler (1920×1080 ref, match 0.5) + GraphicRaycaster; auto-create EventSystem if missing; TextMeshProUGUI only. **One shared font** via `PixelClicker.UIFont`; every UI script applies it. Avoid obsolete TMP properties (`enableWordWrapping`).
- UI layout: line pitch ≥ 1.3× font size, header height computed from font; the Inventory and Log boxes have a centred title and a close "X" in a header above the tabs (clicking their button also toggles); titles capitalised ("Currency", "Log", "Shop").
- Clicks on UI must not hit the pixel (`EventSystem.IsPointerOverGameObject`); pixel hitbox is a full-size trigger so rapid clicks during materialize aren't lost; use `Physics.RaycastAll` with `QueryTriggerInteraction.Collide`.
- Scripts depend on each other (`FormatNumber`, `VacuumBreakdown`, `UIFont`, …). **Always copy the whole `Assets/Scripts` folder together** – most past compile errors (CS0122, CS1061, CS0246, CS0111) were stale/mismatched copies in the Unity project.
- **Compile check before committing:** run `tools/compile-check/check.sh` (compiles `Assets/Scripts` without Unity; details in `tools/compile-check/README.md`) and fix every error it reports. It does not replace testing in Unity (editor-only / Unity-6-only / UI-TMP-stub gaps, run-time bugs).
- Git: develop on the branch the session names (recently `claude/intelligent-cori-y2qt3d`); commit + push when work is done; no PRs unless asked.

- **Skill packs (Mining, Farming, Electrician, future ones) are chosen in the Skills window only - never add a switch for one to the Toggles window.**

## Big-picture check on every new feature (standing instruction from the user)
- Features like new pixel packs, skills, minigames or devices tend to be added one at a time with no shared system behind them, and the code turns into a mess nobody asked to clean up. **Before building a new feature, and again when reporting it, look at how it fits the game as a whole:**
  - Is there already a system for this kind of thing (a table, base class, helper, data-driven list)? Use it. If the new feature is the 3rd or later of its kind and each one is hand-wired, say so and propose the shared system instead of copying the pattern again.
  - Does it duplicate logic that lives elsewhere, add another scattered check (`Showing`, `IsPaused`, `TakeoverActive`...), or touch many files for one addition? Name it.
  - Is there an obvious performance cost (per-frame work, allocations, `Find` calls, unpooled spawns)? Avoid it or flag it.
  - Does it interact with saves, the Toggles / Skills windows, the shop, achievements or the database in a way that needs a migration or index update?
- In the reply, add a short **'Fits the game / could be better'** note: what the feature reuses, what it hand-wires, and the one or two cleanups worth doing. Keep it to a few lines.
- Small tweaks and bug fixes need no note. For a bigger refactor than the request, don't do it unasked: ask whether to build it now or add it to the to-do (see below).

## To-Do list (standing instruction from the user)
- `Assets/Resources/Todo.txt` is the project's idea / to-do list, shown in the Dev Tools panel's To-Do tab. **When the user says to add something to the to-do, add a line to that file** (`[ ] short description`) and commit it. **When an idea or feature comes up that is bigger than the current request or not asked for yet, ask whether to add it to the to-do instead of building it in the moment.** Mark items done by changing `[ ]` to `[x]` when they ship. Also add a changelog line for each to-do edit.

## How the notes are organised (keep CLAUDE.md small)
- This file holds only the working rules, the data model, and ONE index line per script / system.
- Per-system detail lives in the **header comment at the top of the script itself** (short notes) or in **`docs/systems/<Name>.md`** (long ones: shop, skills, floors, skies, pets, vendors...). Open the doc / script header before changing a system.
- When you change a system: update its script header comment or its `docs/systems` file, and only touch the index line here if what the system IS changed. Do not paste feature history into this file.
- Open ideas / balance notes: `docs/roadmap.md`. The to-do list stays in `Assets/Resources/Todo.txt`.
