# Interface map (UI IDs)

Every piece of the game's interface has a short ID the user can quote ("make W4.3 wider"). A letter for the group, a number for the piece, and `.n` for its n-th part (the Parts column, in order).
Generated from `tools/ui-map/ui_map.txt` by `python3 tools/ui-map/build.py` - edit the .txt, never this file. IDs never change: new pieces go at the end of their group, new parts at the end of their line. Line numbers drift as code changes; search the named builder method if a line is off.

## H - HUD

Always-on screen pieces: the bars, their buttons, meters

| ID | Piece | Kind | Where | Opened by | Code | Parts |
|---|---|---|---|---|---|---|
| H1 | Screen bars (top + bottom black bars, neon edge glow) | HUD bar | top edge + bottom edge, full width | always (Settings > Interface: Menu bar glow / Bar style / Custom bar colour) | `PixelHud.cs:360 (BuildBars)` | **H1.1** Top Bar<br>**H1.2** Bottom Bar<br>**H1.3** bar glow (eyedropper + colour-cycle)<br>**H1.4** docked-button slide system (Dock :394, DockInGroup :426<br>**H1.5** Menu buttons mode peeking/always/hidden) |
| H2 | Inventory button | button | top-left corner (docked on top bar) | always; key PixelAction.Inventory (unbound by default) | `PixelUI.cs:1131 (BuildAutomaticUI :1110)` | **H2.1** slides out near mouse<br>**H2.2** toggles Inventory window |
| H3 | Bank tab | button | top-left, beside Inventory (top bar) | shown once Pixel Bank bought | `PixelBank.cs:1181 (BuildUI)` | **H3.1** Bank Button (opens Pixel Bank window)<br>**H3.2** bank hint tip (BuildTip :1272) |
| H4 | Hose button (square) | button | top-left, right of Bank tab | Pixel Bank owned; key B (Hose) | `PixelBank.cs:1189 (BuildUI)` | **H4.1** equips the pixel hose (hose drawn :504 BuildHose, nozzle :592 BuildNozzle) |
| H5 | Shop button | button | top-right corner (top bar) | appears after Cubie's 2nd visit; key PixelAction.Shop | `PixelShopUI.cs:80 (BuildShopButton)` | **H5.1** gold pulse frame while shop hold is on (PixelShopNudge) |
| H6 | Crafting button | button | top-right, left of Shop (top bar) | Crafting upgrade bought; key PixelAction.Crafting | `PixelCrafting.cs:630 (BuildUI)` | **H6.1** toggles Crafting window |
| H7 | Log button | button | bottom-left corner (bottom bar) | always; key PixelAction.LogWindow | `PixelLog.cs:651 (BuildUI :640)` | **H7.1** toggles Pixel Log window |
| H8 | Skills tab | button | bottom-left, right of Log (bottom bar) | shown once a skill pack besides Standard exists; key PixelAction.Skills | `PixelSkillsWindow.cs:122 (BuildWindow :115)` | **H8.1** toggles Skills window |
| H9 | Toggles tab | button | bottom-right corner (bottom bar) | always; key PixelAction.Toggles | `PixelToggles.cs:580 (BuildUI :574)` | **H9.1** toggles Toggles window<br>**H9.2** first-use hint box (Hint Text / Hint Ok :727-741) |
| H10 | Pocket (My Window) square button | button | bottom-right, left of Toggles tab | always (beside Toggles) | `PixelToggles.cs:586 (BuildUI; icon BuildMyWindowIcon :877)` | **H10.1** toggles Pocket window |
| H11 | Rotation (Pixel Guide) button | button | bottom-right, left of Pocket button | always | `PixelPixelGuide.cs:148 (BuildUI :142)` | **H11.1** label "Rotation"<br>**H11.2** toggles Pixel Rotation window |
| H12 | Pause button (on-screen) | button | configurable corner (off by default) | showPauseButtonOnScreen | `PixelPauseMenu.cs:1087 (BuildUI :1077)` | **H12.1** opens pause menu |
| H13 | Active status box (potion timer / placing instructions) | HUD bar | top-centre, slides out under top bar | a potion is active or a device is being placed | `PixelUI.cs:2511 (BuildActiveHud; box PixelStatusBox.cs:41 Create)` | **H13.1** potion name + seconds left<br>**H13.2** placing message + cancel hint |
| H14 | Status lines box | HUD bar | top-centre, stacked under status boxes | PixelStatusLines.Set(key,...) by any system | `PixelStatusLines.cs:52 (Make)` | **H14.1** keyed text lines, timed or sticky |
| H15 | Tier guide / unlock message | HUD bar | top-centre under top bar | next pixel unlock / guidance text | `PixelUI.cs:2352 (BuildGuide)` | **H15.1** auto-sized text box |
| H16 | Event log | panel | left side, between the bars | always (Settings > Interface: Event log; Accessibility: Event log size); key Enter toggles | `PixelHints.cs:687 (BuildOverlay)` | **H16.1** scrolling list of event lines<br>**H16.2** click a line for its full tip |
| H17 | Combo meter | HUD bar | bottom-centre, in/above bottom bar | Combo upgrade bought, manual clicking | `PixelCombo.cs:541 (BuildMeter)` | **H17.1** combo count<br>**H17.2** tier multiplier<br>**H17.3** tier effects (BuildEffects :648) |
| H18 | Time Stop meter | HUD bar | top-centre under top bar | key T (Time Stop owned) while stopped / refilling | `PixelTimeStop.cs:451 (EnsureMeter)` | **H18.1** energy bar + fill<br>**H18.2** Time Stop haze overlay (BuildHaze :393) |
| H19 | XP dock popups ("+n Skill XP") | popup | bottom-right above bottom bar (draggable, per save) | skill XP gained | `PixelSkillsPopups.cs:155 (ShowPopup)` | **H19.1** XP dock stack<br>**H19.2** husk-crush score at world spot<br>**H19.3** crush-bonus text by mouse<br>**H19.4** hold-drag to move, right-click to reset |
| H20 | Skill tracker (pinned) | panel | floating, draggable (default near bottom-left) | pin button in Skills window header | `PixelSkillsTracker.cs:110 (EnsureTracker; pin :64 BuildPinButton)` | **H20.1** skill name + level<br>**H20.2** XP bar<br>**H20.3** lock (stick)<br>**H20.4** downsize (one-line)<br>**H20.5** X |
| H21 | Next up window | panel | left edge (Tool window, draggable/dockable) | Standard skill-tree node 'What's Next'; Settings > Interface 'Next-up window' | `PixelNextUp.cs:244 (Build)` | **H21.1** NEXT / NOW / BEFORE spinning cubes (+LATER/THEN with Look Further/Foresight)<br>**H21.2** minimize '-' |
| H22 | Daylight bar (Solar) | HUD bar | top-right | Solar skill in play | `PixelDaylight.cs:96 (Build)` | **H22.1** row of sun squares |
| H23 | Ghost backpack | HUD bar | bottom-right above bottom bar | Ghost minigame owned | `PixelGhostMinigame.cs:840 (EnsureBackpack)` | **H23.1** backpack icon + stash count |
| H24 | Ultra Holes HUD | HUD bar | screen overlay during round | Pad minigame round | `PixelPadMinigame.cs:405 (RoundRoutine :364)` | **H24.1** round status text |
| H25 | Minigame HUDs (Blink, Breakout, Dash, Evade, Snake, Sort, Traffic, Weak Spot) | HUD bar | bottom-right corner score/timer box | that takeover minigame running | `PixelSnakeMinigame.cs:738 (BuildUi; same pattern PixelBlinkMinigame.cs:364, PixelBreakoutMinigame.cs:761, PixelDashMinigame.cs:353, PixelEvadeMinigame.cs:353, PixelSortMinigame.cs:678, PixelTrafficMinigame.cs:505, PixelWeakspotMinigame.cs:729)` | **H25.1** score<br>**H25.2** time left<br>**H25.3** leave hint |
| H26 | Golden Snitch charge bar | HUD bar | screen-space near the snitch | Golden Snitch present | `PixelGoldenSnitch.cs:613 (BuildUI)` | **H26.1** charge bar + fill |
| H27 | First-person crosshair | overlay | centre | first-person view | `PixelFirstPerson.cs:944 (BuildHud)` | **H27.1** crosshair dot |
| H28 | First-person hotbar | HUD bar | bottom-centre | first-person view | `PixelFirstPersonHotbar.cs:191 (BuildBar)` | **H28.1** slots<br>**H28.2** axe (BuildAxe :164) |
| H29 | Aim cursor | overlay | follows mouse | aiming modes (laser etc.) | `PixelAimCursor.cs:91 (Build)` | **H29.1** custom cursor |
| H30 | Hold meter (ring/bar) | overlay | follows mouse (right of pointer) | any hold-to-act (remove device, pry, drag dock) | `PixelHold.cs:159 (Build)` | **H30.1** ring with %<br>**H30.2** bar variant |
| H31 | Block Hold meter | overlay | centre (over cube) | right-hold on the cube block (Block Hold owned) | `PixelBlockHold.cs:744 (BuildMeter)` | **H31.1** hold progress segments |
| H32 | Dragon wish button ("Summon the Dragon") | button | top-centre under top bar | holding all seven Dragon Cubes | `PixelDragonWish.cs:118 (EnsureButton)` | **H32.1** glowing button<br>**H32.2** opens wish scene |

## W - Main windows

Windows opened from the bars or keys

| ID | Piece | Kind | Where | Opened by | Code | Parts |
|---|---|---|---|---|---|---|
| W1 | Inventory | window | top-left, under Inventory button (grows away from edge) | Inventory button / key | `PixelUI.cs:1146 (BuildAutomaticUI :1110)` | **W1.1** tab Currency (Pure / Exotic groups + Ultra row, :1231)<br>**W1.2** tab Consumables: category dropdown Potions/Utilities/Seeds/Supplies<br>**W1.3** Consumables search box<br>**W1.4** Consumables 3D view button (:1780)<br>**W1.5** potion rows (:2186)<br>**W1.6** tab Materials<br>**W1.7** compact/pin toggle in header (:1818)<br>**W1.8** compact minimise / stick / currency-potion switch buttons (:1865, :2010) |
| W2 | Consumables 3D gallery | window | centred | Inventory > Consumables 3D view button | `PixelConsumableGallery.cs:119 (Build; window :124)` | **W2.1** filter dropdown All/Devices/Potions/Seeds/Supplies<br>**W2.2** tile grid with 3D models<br>**W2.3** hover description box<br>**W2.4** left-click use<br>**W2.5** right-click solo window |
| W3 | Consumable solo window | window | centred above gallery | right-click a gallery tile | `PixelConsumableGallery.cs:274` | **W3.1** large turning model<br>**W3.2** description box<br>**W3.3** Use button |
| W4 | Shop | window | centred (currency panel on its left) | Shop button / key | `PixelShopUI.cs:106 (BuildPanel; window :112)` | **W4.1** Search box (header left)<br>**W4.2** Help '?' hold-to-replay tutorial<br>**W4.3** tab Pixels (chips Toggleables / Skill packs, PixelShopPixels.cs:18)<br>**W4.4** tab Consumables (chips Potions / Utilities / Seeds / Supplies, PixelShopConsumables.cs:23)<br>**W4.5** tab Upgrades: sub-tab Features (BuildSubTabs :206)<br>**W4.6** Upgrades sub-tab Value<br>**W4.7** Upgrades sub-tab Operation<br>**W4.8** tab Minigames (tracker rows)<br>**W4.9** tile grid + buy bar (PixelShopTilePage.cs:138/:155)<br>**W4.10** search results page (PixelShopSearch.cs:28) |
| W5 | Shop currency panel | panel | left of Shop window | opens with Shop | `PixelShopCurrency.cs:56 (BuildCurrencyPanel)` | **W5.1** "Currency" title<br>**W5.2** pixel amounts list |
| W6 | Shop Upgrades window | window | centred (over Shop) | Buy on a bought pack with upgrades / Operation row arrow / device panel Upgrades | `PixelShopUI.cs:539 (BuildUpgradesWindow)` | **W6.1** Back arrow (header left)<br>**W6.2** per-pack upgrade rows<br>**W6.3** X |
| W7 | Pixel Log | window | bottom-left, above Log button | Log button / key | `PixelLog.cs:662 (BuildUI :640)` | **W7.1** Collection Log book button (header left :672)<br>**W7.2** tab Achievements (list + summary bar :759)<br>**W7.3** tab Goals (:766)<br>**W7.4** Pixels list (legacy, moved to Stats) |
| W8 | Collection Log (book) | window | centred | book button in Pixel Log header | `PixelCollectionLog.cs:193 (Build; window :200)` | **W8.1** title page (found count)<br>**W8.2** one page per Dragon/God/Prime cube<br>**W8.3** page turn by clicking pages<br>**W8.4** camera button per page -> 3D gallery<br>**W8.5** X |
| W9 | Skills | window | left edge, vertically centred | Skills tab / key | `PixelSkillsWindow.cs:135 (BuildWindow :115)` | **W9.1** header pin (-> tracker)<br>**W9.2** Help '?'<br>**W9.3** header label + minigame bonus<br>**W9.4** XP bar<br>**W9.5** Pack List cards (name, level, ACTIVE badge, tree square :652)<br>**W9.6** detail pane: name, description, info<br>**W9.7** Perks accordion (:225)<br>**W9.8** Train button (:258)<br>**W9.9** Skill tree toggle + pixel-pack buttons (BuildTreeUi :284)<br>**W9.10** guide book button (PixelSkillsGuide.cs:50) |
| W10 | Skill tree | window | right of Skills window (stretched beside it) | Skill tree button / tree square on a card | `PixelSkillsWindow.cs:323 (BuildTreeWindow; window :327)` | **W10.1** Expand '<' (header left)<br>**W10.2** points label<br>**W10.3** sideways-scrolling node view with lines<br>**W10.4** node info<br>**W10.5** Unlock button |
| W11 | Skill guide book | popup | centred | book button in Skills detail (Woodcutting) | `PixelSkillsGuide.cs:115 (ShowGuideAsk; book :50)` | **W11.1** ask box with yes/no<br>**W11.2** guide pages |
| W12 | Toggles (window title "Toggleable") | window | right edge, vertically centred | Toggles tab / key | `PixelToggles.cs:604 (BuildUI :574)` | **W12.1** Group dropdown: Pixels, Upgrades, Minigames, Vendors, Pets<br>**W12.2** Toggle search<br>**W12.3** Toggle list of tick boxes |
| W13 | Pocket (My Window) | window | free floating, draggable + resizable | Pocket square button | `PixelMyWindow.cs:347 (BuildUI :331)` | **W13.1** header Info (dock info) + Edit buttons<br>**W13.2** tab Pinned (:378)<br>**W13.3** tab Event log (:379)<br>**W13.4** tab Keybinds (:380)<br>**W13.5** widget cards (drag header, '-' fold, hold-x remove :845)<br>**W13.6** blank '+' card with search picker<br>**W13.7** pinned hover card slot |
| W14 | Pixel Rotation (Pixel Guide) | window | right edge, middle (or beside open Shop) | Rotation button | `PixelPixelGuide.cs:159 (BuildUI :142)` | **W14.1** orange '!' in header<br>**W14.2** pixel list with turning 3D pictures<br>**W14.3** '!' tooltip (BuildTip :194) |
| W15 | Pixel Bank | window | left edge, vertically centred | Bank tab / key | `PixelBank.cs:1217 (BuildUI :1181)` | **W15.1** hose button in header (BuildWindowHoseButton :1334)<br>**W15.2** Capacity<br>**W15.3** Bank list of stored pixels<br>**W15.4** hint |
| W16 | Crafting | window | centred | Crafting button / key | `PixelCrafting.cs:647 (BuildUI :630)` | **W16.1** Recipe Book button (header left :962)<br>**W16.2** two ingredient slots<br>**W16.3** recipe result + cost<br>**W16.4** amount (- / + / Max)<br>**W16.5** Craft button<br>**W16.6** Crafting Cube button<br>**W16.7** Materials panel (BuildMaterialsPanel :803) |
| W17 | Recipe Book | panel | beside Crafting window (top-right anchored) | book button in Crafting header | `PixelCrafting.cs:989 (BuildBook)` | **W17.1** "Recipe Book" title<br>**W17.2** recipe list |
| W18 | Game Database | window | centred (above pause menu) | Pause > Game Guide > Database tab | `PixelDatabaseWindow.cs:111 (Build :103)` | **W18.1** search box<br>**W18.2** category tabs (All, Pixels, Packs, Skills, Items, Recipes, Minigames, Awards, Tips, Pets, Floors, Skies, Rules, Nodes, Settings, Show ???)<br>**W18.3** results list<br>**W18.4** entry detail with clickable references |
| W19 | View window | window | right edge, half way down (Tool window) | key V (Shift+V cycles views) | `PixelViewWindow.cs:445 (Build :439)` | **W19.1** view tick boxes First person / Orbital / Top down / Free cam<br>**W19.2** Orthographic (flat) view<br>**W19.3** Zoom slider<br>**W19.4** orbit controls (hold-repeat buttons)<br>**W19.5** Invert zoom scroll<br>**W19.6** Orbit field of view |
| W20 | Give feedback | window | centred | Pause > Give feedback; key F8 | `PixelFeedback.cs:177 (BuildWindow)` | **W20.1** text box<br>**W20.2** Take screenshot<br>**W20.3** pencil colours + Undo<br>**W20.4** Save |
| W21 | Prime Gauntlet | window | centred | right-click Gauntlet item in inventory | `PixelInfinity.cs:151 (Build :141)` | **W21.1** flat golden cube (six faces)<br>**W21.2** stone tray<br>**W21.3** Forge button<br>**W21.4** X |
| W22 | UFO Helper window | window | centred | click the UFO helper | `PixelRobotWorker.cs:1048 (BuildWindow :1038)` | **W22.1** job / consumable choices (Vacuum, Fan, Charge Booster, Lightning Rod, Sprinkler, bank a pixel)<br>**W22.2** X |
| W23 | Pet customize | window | centred over dim | pet menu Customize | `PixelPetsCustomize.cs:722 (BuildCustomizeWindow :711)` | **W23.1** name field<br>**W23.2** 3D preview<br>**W23.3** outfit grid (BuildOutfit :492)<br>**W23.4** buttons along bottom |
| W24 | Pet Outfit Studio | window | centred, large (1800x980) | from pet customize | `PixelPetsStudio.cs:415 (BuildStudioWindow :405)` | **W24.1** studio stage (:214)<br>**W24.2** custom outfit parts (:128) |

## N - Popups & notices

Tips, popups, tooltips, tutorials, shows

| ID | Piece | Kind | Where | Opened by | Code | Parts |
|---|---|---|---|---|---|---|
| N1 | Tip / notice box ("Got it") | popup | top-centre under top bar (also centred / bottom / beside shop variants) | PixelNotice.Show (tips, tutorials) | `PixelNotice.cs:74 (Build; Show :33)` | **N1.1** message<br>**N1.2** Got it button<br>**N1.3** back button<br>**N1.4** Skip (BuildSkip :140) |
| N2 | First-time tips queue | popup | via notice box | PixelHints.Trigger(id) first time something happens | `PixelHints.cs:687 (shares BuildOverlay; tips table :258)` | **N2.1** queued one at a time (PixelTipQueue.cs gate) |
| N3 | Hint box (bottom) | tooltip | bottom-centre above bottom edge | systems needing an instruction line | `PixelHintBox.cs:23 (Create)` | **N3.1** single text label |
| N4 | Did you know? | popup | docked at right edge near top (draggable, dock left/right or float) | random when game is calm (Settings > Gameplay 'Did you know') | `PixelDidYouKnow.cs:453 (Build)` | **N4.1** heading<br>**N4.2** tip text<br>**N4.3** icon<br>**N4.4** dock marker<br>**N4.5** right-click resets |
| N5 | Achievement unlocked popup | popup | middle-right (hold to drag + pin, per save) | achievement tier earned | `PixelAchievements.cs:778 (BuildPopup)` | **N5.1** icon box with spinning cube<br>**N5.2** text<br>**N5.3** button<br>**N5.4** Settings 'Reset achievement popup position' |
| N6 | Skill level-up popup | popup | follows mouse (above pointer) | skill reaches a new level | `PixelSkillLevelPopup.cs:140 (Build; Show :60)` | **N6.1** badge with spinning icon<br>**N6.2** 'SKILL LEVEL n'<br>**N6.3** '+1 skill point' pill<br>**N6.4** fireworks |
| N7 | Gain popups (+n pixels) | popup | at cube / pointer, rising | pixel collected (Settings > Interface 'Popups', Accessibility 'Popup size') | `PixelUI.cs:1018 (ShowPopup; MakePopup :1054)` | **N7.1** floating coloured text |
| N8 | Ultra tooltip | tooltip | follows mouse | hover a currency row in Inventory | `PixelUI.cs:708 (BuildTooltip)` | **N8.1** Ultra count text / custom button tips |
| N9 | Log achievement / goal tip | tooltip | beside pointer | hover an achievement or goal row | `PixelLog.cs:548 (tip in UpdateTip)` | **N9.1** heading (CLICKING / COLLECTING / ACHIEVEMENT)<br>**N9.2** body<br>**N9.3** tier history |
| N10 | Skill tip | tooltip | beside pointer | hover skill cards / perks / tree nodes | `PixelSkillsWindow.cs:551 (BuildTip)` | **N10.1** text box |
| N11 | Block tooltip card | tooltip | follows mouse | hover the cube block or a husk (Settings > Interface 'Block tooltips' Full/Minimal/Off, Alt cycles) | `PixelBlockTooltip.cs:121 (Build)` | **N11.1** spinning cube<br>**N11.2** name<br>**N11.3** rarity tag<br>**N11.4** pays<br>**N11.5** progress bar<br>**N11.6** guide description |
| N12 | Power Click popup + flash | popup | centred | full-charge power click released | `PixelPowerClick.cs:223 (EnsureUI)` | **N12.1** white flash<br>**N12.2** 'POWER CLICK x25' text |
| N13 | Overcharge text | popup | centred | hand-click on an Electric pixel | `PixelOvercharge.cs:144 (SpawnText)` | **N13.1** 'OVERCHARGE!' text |
| N14 | Shuffle bonus box | popup | top-centre | tap right button on the skill's filler block | `PixelShuffleBonus.cs:159 (Build)` | **N14.1** title<br>**N14.2** bonus text |
| N15 | Pet found popup | popup | centred over dim | pet drop on a click | `PixelPets.cs:508 (ShowPopup :491)` | **N15.1** icon<br>**N15.2** message<br>**N15.3** OK |
| N16 | Pet menu | popup | near the clicked pet | right-click a pet | `PixelPetsMenu.cs:63 (BuildMenu)` | **N16.1** title<br>**N16.2** action buttons (customize etc.) |
| N17 | Offline progress summary | popup | top-centre over dim | game loads after time away | `PixelOfflineProgress.cs:318 (ShowSummary :276)` | **N17.1** title<br>**N17.2** scrolling gains list (BuildScrollList :355)<br>**N17.3** close |
| N18 | Save message / autosave note | popup | bottom-centre | save / load / autosave (Settings > Gameplay 'Autosave message') | `PixelSaveGame.cs:778 (BuildMessageUI)` | **N18.1** message text<br>**N18.2** autosave note |
| N19 | Crash notice | popup | centred | an error is logged | `PixelCrashLog.cs:416 (ShowNotice)` | **N19.1** notice text<br>**N19.2** buttons |
| N20 | Cubie (guide vendor) speech | window | bottom-right character + speech window beside him | new game visits / shop hold | `PixelGuideVendor.cs:868 (BuildUI :843)` | **N20.1** Cubie art<br>**N20.2** speech window with character name<br>**N20.3** next / advance (Escape = next page) |
| N21 | Visitor greeting / wares / aside (Tinkerer, Wizard, Farmer, Bean, Egg, Entrepreneur, Lumberjack, Prospector) | window | right side, beside visitor art | visitor minigame arrives | `PixelVisitorMinigame.cs:1122 (Panel; BuildUI :844)` | **N21.1** Greeting (:871) Accept / Reject<br>**N21.2** Wares list (:924) with buy rows + sell panel<br>**N21.3** Aside (:1041)<br>**N21.4** Rapport badge (PixelVisitorMinigameRapport.cs AddRapportBadge: a heart button in the greeting and wares box headers, coloured by his step<br>**N21.5** hover / click shows the rapport card - step + progress line, segmented bar, step names) |
| N22 | Shop tutorial | overlay | small Got it boxes right of Shop window | first time Shop available; Help '?' hold; Settings 'Replay tutorial' | `PixelShopTutorial.cs:114 (BeginRun; Replay :163)` | **N22.1** step currency list<br>**N22.2** step Pixels<br>**N22.3** step Upgrades > Features<br>**N22.4** step Upgrades > Value<br>**N22.5** step Consumables<br>**N22.6** step Minigames |
| N23 | Skills tutorial | overlay | boxes beside Skills window, Cubie bottom-right | first skill pack available | `PixelSkillsTutorial.cs:96 (StartTutorial)` | **N23.1** Got it steps pointing at Skills window parts |
| N24 | Toggles tutorial | overlay | beside Toggles window | first time Toggles due | `PixelTogglesTutorial.cs:60 (Replay)` | **N24.1** Got it steps |
| N25 | Tutorial dim | overlay | full screen | while a guided tutorial runs | `PixelTutorialDim.cs:46 (StartDim)` | **N25.1** dark fade<br>**N25.2** click blocker<br>**N25.3** pauses game |
| N26 | Highlight frame | overlay | around a target UI element | PixelHighlight.Set (e.g. Shop nudge) | `PixelHighlight.cs:74 (Build)` | **N26.1** pulsing gold frame |
| N27 | Treasure map | overlay | centre of screen | bottle (Fishing) breaks | `PixelTreasureMap.cs:229 (Show)` | **N27.1** scroll unroll<br>**N27.2** map with path and X<br>**N27.3** shrinks to dig spot |
| N28 | Dragon / God / Prime find show | overlay | full screen | finding a Dragon / God / Prime cube (Escape skips) | `PixelDragonFind.cs:354 (Build); PixelGodFind.cs:920 (Build)` | **N28.1** cinematic show<br>**N28.2** skip |
| N29 | Egg hatch show | overlay | full screen | pet egg hatches (Escape skips) | `PixelEggHatch.cs:450 (Build)` | **N29.1** hatch animation |
| N30 | Dragon wish scene | overlay | full screen | Summon the Dragon button | `PixelDragonWish.cs:296 (BuildScene)` | **N30.1** summoning scene<br>**N30.2** three wish choices |
| N31 | Eyedropper | overlay | full screen frozen screenshot + loupe at mouse | eyedropper buttons in Settings | `PixelEyedropper.cs:76 (BuildOverlay)` | **N31.1** screenshot<br>**N31.2** loupe<br>**N31.3** colour swatch<br>**N31.4** Escape cancels |
| N32 | Screen filters overlay | overlay | full screen | Settings > Display 'Filter' / strength | `PixelFilters.cs:485 (BuildOverlay)` | **N32.1** filter tint layer |

## M - Menus

Title screen, pause menu, settings, save / load

| ID | Piece | Kind | Where | Opened by | Code | Parts |
|---|---|---|---|---|---|---|
| M1 | Studio intro | overlay | full screen | game launch (Settings 'Studio intro') | `PixelStudioIntro.cs (BuildCanvas; Build)` | **M1.1** space scene (Earth, galaxy, GAME OVER)<br>**M1.2** arcade scene (cabinet, alien, presents) |
| M2 | Title screen | overlay | full screen; logo top-centre, bar across middle | game start / Quit > Main menu | `PixelTitleScreen.cs:512 (Build)` | **M2.1** Logo (BuildLogo :804, style favourite stars)<br>**M2.2** middle bar with Play / Continue / New Game (:581)<br>**M2.3** Load, Settings, Quit icon buttons (:582-584) with hover tip<br>**M2.4** Floor + Sky pickers bottom-right (BuildLookPicker :645)<br>**M2.5** bar glow (:370)<br>**M2.6** blur / live background |
| M3 | Music player | panel | bottom-centre (title + pause menu) | title screen / pause menu open; fades when idle | `PixelTitleMusic.cs:40 (Create)` | **M3.1** track list<br>**M3.2** arrows<br>**M3.3** mute<br>**M3.4** shuffle |
| M4 | Pause menu (main) | window | centred over full-screen dim | Escape (when no window open) | `PixelPauseMenu.cs:1077 (BuildUI)` | **M4.1** title plate "Paused" + menu search box<br>**M4.2** Resume<br>**M4.3** Save/Load<br>**M4.4** Stats<br>**M4.5** Settings<br>**M4.6** Game Guide<br>**M4.7** Give feedback<br>**M4.8** Quit<br>**M4.9** fancy backdrop |
| M5 | Save / Load screen | window | centred | Pause > Save/Load; Title > Load | `PixelPauseMenu.cs:1669 (BuildSaveLoadPanel)` | **M5.1** slot list (Save / Load per slot, Rename)<br>**M5.2** Autosaves button<br>**M5.3** overwrite confirm (BuildOverwriteOverlay :1760)<br>**M5.4** result box (BuildResultOverlay :1845)<br>**M5.5** rename box (BuildRenameOverlay :1886)<br>**M5.6** Cubie peek (PixelCubiePeek) |
| M6 | Autosaves window | window | centred over dim (above pause menu) | Save/Load > Autosaves | `PixelAutosaves.cs:335 (Build; class :288)` | **M6.1** autosave list with screenshots<br>**M6.2** load |
| M7 | Stats screen | window | centred | Pause > Stats | `PixelPauseMenu.cs:1599 (BuildStatsPanel)` | **M7.1** Stats search<br>**M7.2** foldable stat sections (AddStatSectionHeader :3173)<br>**M7.3** Restart button |
| M8 | Restart confirm | window | centred | Stats > Restart | `PixelPauseMenu.cs:2136 (BuildRestartPanel)` | **M8.1** hold-to-restart button<br>**M8.2** back |
| M9 | Settings screen | window | centred | Pause > Settings; Title > Settings | `PixelPauseMenu.cs:2475 (BuildSettingsPanel)` | **M9.1** Settings search<br>**M9.2** foldable sections<br>**M9.3** footer button Replay tutorial<br>**M9.4** footer button Change keybinds |
| M10 | Settings > Gameplay | panel | inside Settings list | click heading | `PixelPauseMenu.cs:2514` | **M10.1** Abbreviate numbers<br>**M10.2** Hide purchased shop items<br>**M10.3** Run in background<br>**M10.4** Pause stops game<br>**M10.5** Did you know<br>**M10.6** Day/night + length<br>**M10.7** Autosave message<br>**M10.8** Autosave interval<br>**M10.9** Floor style (+ display floor tick)<br>**M10.10** Floor off mode<br>**M10.11** Sky style |
| M11 | Settings > Interface | panel | inside Settings list | click heading | `PixelPauseMenu.cs:2543` | **M11.1** Tips<br>**M11.2** Event log<br>**M11.3** Popups<br>**M11.4** Block tooltips<br>**M11.5** Clicker aura (+eyedropper) + aura colour<br>**M11.6** Next-up window<br>**M11.7** Drop guide<br>**M11.8** Fancy menu<br>**M11.9** Custom button colour (+eyedropper)<br>**M11.10** Camera intro<br>**M11.11** Studio intro<br>**M11.12** Menu buttons mode<br>**M11.13** Bar glow (+eyedropper, cycle)<br>**M11.14** Bar style<br>**M11.15** Custom UI colour (+eyedropper)<br>**M11.16** Custom bar colour (+eyedropper) |
| M12 | Settings > Accessibility | panel | inside Settings list | click heading | `PixelPauseMenu.cs:2574` | **M12.1** Rotation<br>**M12.2** Pulsing<br>**M12.3** Colour-blind marks<br>**M12.4** Popup size<br>**M12.5** Event log size<br>**M12.6** UI scale<br>**M12.7** Reset achievement popup position<br>**M12.8** Reset tips location |
| M13 | Settings > Display | panel | inside Settings list | click heading | `PixelPauseMenu.cs:2589` | **M13.1** Filter<br>**M13.2** Filter strength<br>**M13.3** Orbit field of view<br>**M13.4** VSync<br>**M13.5** Fullscreen<br>**M13.6** Resolution |
| M14 | Settings > Audio | panel | inside Settings list | click heading | `PixelPauseMenu.cs:2609` | **M14.1** Mute<br>**M14.2** Mute music<br>**M14.3** UI clicks<br>**M14.4** Master volume<br>**M14.5** Effects volume<br>**M14.6** Music volume<br>**M14.7** Custom playlist |
| M15 | Key bindings screen | window | centred | Settings > Change keybinds | `PixelPauseMenu.cs:2952 (BuildKeysPanel)` | **M15.1** one row per action with key capture (Time stop, Pixel Bank hose, Rotate, Event log, Time slow, Inventory, Shop, Log, Crafting, Toggles, Bank, Skills, Feedback, View, Dev tools...)<br>**M15.2** back |
| M16 | Custom playlist screen | window | centred | Settings > Audio > Custom playlist | `PixelPauseMenu.cs:2853 (BuildPlaylistPanel)` | **M16.1** track tick boxes<br>**M16.2** back |
| M17 | Game Guide screen | window | centred | Pause > Game Guide | `PixelPauseMenu.cs:2189 (BuildGuideScreen)` | **M17.1** tab Controls<br>**M17.2** tab How To Play<br>**M17.3** tab Database (opens Game Database window)<br>**M17.4** scrolling text |
| M18 | Quit to... box | window | centred | Pause > Quit | `PixelPauseMenu.cs:2119 (BuildQuitPanel)` | **M18.1** Desktop<br>**M18.2** Main menu (saves first)<br>**M18.3** back |
| M19 | Custom key bindings view (Pocket Keybinds tab) | panel | inside Pocket window | Pocket > Keybinds tab | `PixelCustomKeys.cs:148 (BuildView; prompt :231)` | **M19.1** binding rows with key + x<br>**M19.2** search<br>**M19.3** key-capture prompt with confirm |

## O - World UI

Cards, panels and labels on objects in the world

| ID | Piece | Kind | Where | Opened by | Code | Parts |
|---|---|---|---|---|---|---|
| O1 | Device hover card | tooltip | follows mouse (beside pointer) | hover a placed device 0.5 s | `PixelDeviceCard.cs:439 (BuildCard)` | **O1.1** name<br>**O1.2** time / uses bar<br>**O1.3** key stats<br>**O1.4** contents |
| O2 | Device panel | window | near the device (Tool window) | right-click tap a placed device / belt | `PixelDeviceCard.cs:357 (BuildPanel; window :360)` | **O2.1** sheet lines<br>**O2.2** steppers<br>**O2.3** Remove (Sure?)<br>**O2.4** Upgrades (shop Operation)<br>**O2.5** device-specific actions (e.g. Select filter) |
| O3 | Crafting Cube recipe picker | window | centred (above device panel) | right-click tap a Crafting Cube | `PixelCraftingCubeWindow.cs:81 (Build; Open :41)` | **O3.1** "Choose a recipe" scrolling list with result picture, name, ingredients |
| O4 | Hopper filter picker | window | centred (above device panel) | device panel 'Select filter' (Sorting Hopper / Husk Source) | `PixelSortingHopperWindow.cs:101 (Build; Open :58)` | **O4.1** "Select filter" grid of husk types + None |
| O5 | Belt speed editor | window | centred | conveyor belt panel action | `PixelConveyor.cs:1373 (BuildEditor; window :1380)` | **O5.1** "Belt speed" controls |
| O6 | UFO speech bubble | overlay | above the UFO (projected) | UFO Helper owned | `PixelRobotWorker.cs:864 (BuildBubble)` | **O6.1** alien face bubble |
| O7 | UFO cockpit HUD | overlay | full screen | piloting the UFO (Escape leaves) | `PixelRobotCockpit.cs:175 (BuildCockpitUi)` | **O7.1** cabin frame / controls |
| O8 | Pet name tag | overlay | above the pet (projected) | hover / named pet | `PixelPetsCustomize.cs:567 (EnsureTag)` | **O8.1** name label |
| O9 | Energy bar (world) | HUD bar | above its object in world | devices / objects with energy | `PixelEnergyBar.cs:22 (Attach)` | **O9.1** bar + fill |
| O10 | World ring | overlay | around object in world | progress / reach indicators | `PixelWorldRing.cs (Attach)` | **O10.1** ring |
| O11 | Hover marker arrow | overlay | above hovered husk / object in world | hover an interactable | `PixelHoverMarker.cs:27 (Attach)` | **O11.1** arrow + optional text |
| O12 | Hazard warning sign | overlay | above hazard block in world | hazard about to trigger | `PixelHazardTell.cs:110 (BuildSignSprite)` | **O12.1** red-orange triangle with '!' |
| O13 | Collection gallery (3D room) | overlay | full screen first-person room | Collection Log page camera button (Escape leaves) | `PixelCollectionGallery.cs:315 (BuildCanvas)` | **O13.1** cubes floating<br>**O13.2** leave hint |

## D - Dev Hub

F9 dev tools: tabs and Main-tab widgets

| ID | Piece | Kind | Where | Opened by | Code | Parts |
|---|---|---|---|---|---|---|
| D1 | Dev Hub window | window | centred (Tool window: drag, minimize, dock left/right, scale grip) | key F9 / pause menu Dev Tools (dev builds) | `PixelDevHub.cs:213 (Awake; window :225)` | **D1.1** Search the hub box (BuildSearch :58)<br>**D1.2** filing-cabinet tab strip (BuildTab :348): drag to reorder, drag out to float, Dock button |
| D2 | Main tab | tab | Dev Hub content | Dev Hub tab "Main" | `PixelDevTools.cs:723 (BuildPanel)` | **D2.1** Collapse all / Expand all<br>**D2.2** draggable widget board (right-hold = favourite) |
| D3 | Main > Resource Tools | panel | Main tab widget card | always | `PixelDevTools.cs:1066 (BuildResourceToolsCard)` | **D3.1** Infinite resources<br>**D3.2** God mode (on-screen God pixel display, BuildGodDisplay :1636)<br>**D3.3** Husk despawns<br>**D3.4** Husk pile (Fill floor)<br>**D3.5** Clear key<br>**D3.6** Husk limit<br>**D3.7** Spawned pixel (dropdown, Spawn, Despawn)<br>**D3.8** Middle click (swap dropdown)<br>**D3.9** Add pixels (dropdown, amount, Add, Give Pet)<br>**D3.10** Focus potions (dropdown, Drink, Cancel)<br>**D3.11** Click count |
| D4 | Main > Dev reports | panel | Main tab widget card | widget | `PixelDevTools.cs:826` | **D4.1** Report Folder<br>**D4.2** Bounds snapshot<br>**D4.3** Mark Moment (F10)<br>**D4.4** UI layout dump<br>**D4.5** Balance Report<br>**D4.6** Session Recorder |
| D5 | Main > Reset counts | panel | Main tab widget card | widget | `PixelDevTools.cs:837` | **D5.1** Reset Counts button |
| D6 | Main > Double floor | panel | Main tab widget card | widget | `PixelDevTools.cs:884` | **D6.1** DoubleFloor toggle (no WidgetTitle case: shows its id) |
| D7 | Main > Time control | panel | Main tab widget card | widget | `PixelDevTools.cs:937` | **D7.1** Rewind<br>**D7.2** Pause<br>**D7.3** Play<br>**D7.4** Slow<br>**D7.5** Fast<br>**D7.6** Time status line<br>**D7.7** on-screen label top-centre (PixelTimeControl.cs RefreshLabel) |
| D8 | Main > Minigames | panel | Main tab widget card | widget | `PixelDevTools.cs:1201` | **D8.1** Minigame dropdown<br>**D8.2** Spawn<br>**D8.3** Despawn |
| D9 | Main > Unlock | panel | Main tab widget card | widget | `PixelDevTools.cs:1238` | **D9.1** Shop Unlock dropdown<br>**D9.2** Unlock<br>**D9.3** Relock |
| D10 | Main > Max XP | panel | Main tab widget card | widget | `PixelDevTools.cs:1253` | **D10.1** Skill XP dropdown<br>**D10.2** Max XP<br>**D10.3** Reset XP |
| D11 | Main > Clear world | panel | Main tab widget card | widget | `PixelDevTools.cs:1284` | **D11.1** Clear World button (confirm) |
| D12 | Main > Super Rares | panel | Main tab widget card | widget | `PixelDevTools.cs:1306` | **D12.1** Super Rares dropdown<br>**D12.2** Spawn Rare<br>**D12.3** Give Gauntlet<br>**D12.4** Farmer seeds |
| D13 | Main > Dev items | panel | Main tab widget card | widget | `PixelDevTools.cs:1317` | **D13.1** Dev Items dropdown<br>**D13.2** Give |
| D14 | Docs tab | tab | Dev Hub content | Dev Hub tab "Docs" | `PixelDevDocs.cs:74 (Awake; root :80)` | **D14.1** sub-tab To-Do<br>**D14.2** sub-tab Changelog<br>**D14.3** sub-tab Notes (BuildNotes :212)<br>**D14.4** sub-tab Ideas (BuildIdeas :146) |
| D15 | Tester tab | tab | Dev Hub content | Dev Hub tab "Tester" | `PixelFeatureTester.cs:268 (Build)` | **D15.1** feature cards to place / select<br>**D15.2** selection overlay (ShowRect :675) |
| D16 | Inspector tab | tab | Dev Hub content | Dev Hub tab "Inspector"; F11 inspect hover | `PixelDevInspector.cs:263 (Build)` | **D16.1** World sub-tab<br>**D16.2** Selected sub-tab |
| D17 | Shapes tab | tab | Dev Hub content (500 wide) | Dev Hub tab "Shapes" | `PixelDevShapes.cs:534 (BuildPanel)` | **D17.1** shape placer (cube, sphere, cylinder, capsule, ramp, wall, disc)<br>**D17.2** numbered list<br>**D17.3** copy layout |
| D18 | UI Mock tab | tab | Dev Hub content (500 wide) | Dev Hub tab "UI Mock" | `PixelDevMockup.cs:522 (BuildPanel; pieces canvas Awake :109)` | **D18.1** windows / boxes / buttons / text / images pieces<br>**D18.2** layering<br>**D18.3** numbers<br>**D18.4** piece list |
| D19 | Looks tab | tab | Dev Hub content | Dev Hub tab "Looks" | `PixelLookStudio.cs:156 (Build; rows :306)` | **D19.1** type dropdown<br>**D19.2** Copy look from<br>**D19.3** sections Colour, Surface, Glow, Edges, Shape and motion, Face marks, Flair, Husk behaviour |
| D20 | Setups tab | tab | Dev Hub content | Dev Hub tab "Setups" | `PixelDevSetups.cs:56 (Build)` | **D20.1** saved test setups list |
| D21 | Events tab | tab | Dev Hub content | Dev Hub tab "Events" | `PixelDevEvents.cs:55 (Build)` | **D21.1** tip / skill / achievement dropdowns to fire events |
| D22 | Sounds tab | tab | Dev Hub content | Dev Hub tab "Sounds" | `PixelDevSounds.cs:53 (Build; rows :73)` | **D22.1** one row per sound with play |
| D23 | Console tab | tab | Dev Hub content | Dev Hub tab "Console" | `PixelDevConsole.cs:83 (Build)` | **D23.1** log lines<br>**D23.2** copy |
| D24 | Models tab | tab | Dev Hub content | Dev Hub tab "Models" | `PixelModelEditor.cs:111 (Build)` | **D24.1** model search<br>**D24.2** position / rotation / scale sliders<br>**D24.3** Hue Sat Bri sliders<br>**D24.4** Save |
| D25 | Dev tooltip | tooltip | follows mouse | hover a Dev Hub control | `PixelDevTools.cs:1805 (BuildTipBox)` | **D25.1** text box |
| D26 | Dev error badge | button | top-right below top bar | an error is logged (dev builds) | `PixelDevConsole.cs:207 (BuildBadge)` | **D26.1** error count<br>**D26.2** opens Console tab |
| D27 | Dev text edit box | popup | at the edited label | dev text-edit on a label (Escape closes) | `PixelDevTextEdit.cs:177 (OpenEditor)` | **D27.1** input box |
| D28 | Session REC button | button | configurable corner (recButtonAnchor) | session recording (dev) | `PixelSessionRecorder.cs:214 (UpdateRecButton)` | **D28.1** REC toggle |
| D29 | Title "Record session" button | button | title screen top-left | title screen in dev builds | `PixelTitleScreen.cs:697 (BuildRecordButton)` | **D29.1** RECORD SESSION toggle |
