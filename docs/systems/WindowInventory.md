# Window inventory (2026-10-18)

How every player-facing window was built before UI unification step 5 (the window builder, `PixelUIKit.CreateWindow`).
Use it to see what changes when a window moves onto the builder. Sizes are canvas units (1920x1080 reference).
"X at (-a,-b)" = the close button's offset from the top-right corner. SW = `PixelUIKit.StyleWindow`, Pop = `PixelPop`, Reg p = `PixelWindows` priority (Escape closes it).

| Window (script) | Title | Close X | Minimize | Drag / saved position | Edge dock | Other header buttons | Tabs | Pop / Reg | Background | Opened by | Header |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Inventory (PixelUI) | 48, centred, Caps | 70 at (-16,-8) | only pinned | only pinned (PixelPopoutDrag, Inventory.CompactX/Y) | no | pin (top left), lock when pinned | Currency / Consumables / Materials | Pop; p=10 | SW | HUD + key | 90 |
| Log (PixelLog) | 48, centred, Caps | 70 at (-20,-10) | no | no | no | Collection Log book (top left) | Achievements / Goals | Pop; p=20 | SW | HUD + key | 90 |
| Shop (PixelShopUI) | 56, centred, Caps | 80 at (-pad,-pad/2) | no | no | no | ? tutorial (left of X), search | Pixels / Upgrades / Consumables / Minigames | Pop; p=30 (+ Upgrades p=40) | SW + glow | HUD + key | 100 |
| Skills (PixelSkillsWindow) | 56, centred, Caps | 70 at (-14,-12) | no | no | no | pin, ? tutorial | - | Pop; p=14 | SW | HUD + key | ~90 |
| Skill Tree (PixelSkillsWindow) | 52, centred, Caps | 70 at (-14,-12) | no | no | no | < expand (top left) | - | Pop; p=15 | SW | Skills | - |
| Skill tracker (PixelSkillsTracker) | skill name 30 | 40 | yes (downsize) | whole panel (Skills.TrackerX/Y) | no | lock, < > | - | no Pop; not registered | SW | Skills pin | - |
| Toggles (PixelToggles) | 44, centred, Caps | 64 at (-14,-12) | no | no | no | - | group tabs / drop-down | Pop; p=15 | SW | HUD + key | 100 (Shop's) |
| Pocket (PixelMyWindow) | 30, LEFT, Caps | 44, centred in header | yes | own copy of drag + dock (MyWindow.*) | yes (own code) | Edit, Info, Undock, resize grip | Pinned / Event log / Keybinds | Pop; NOT registered | SW | Toggles dock button | 60 |
| Crafting (PixelCrafting) | 48, centred, Caps | 70 at (-20,-14) | no | no | no | recipe book (top left) | side panels | Pop; p=25 | SW | HUD + key | - |
| Bank (PixelBank) | 44, centred, Caps | 64 at (-14,-12) | no | no | no | hose toggle (top left) | - | NO Pop (SetActive); p=14 | SW | HUD + key | - |
| View (PixelViewWindow) | 32, centred, hard-coded caps | 46 RED at (-14,-13) | no | header strip, NOT saved | no | - | - | Pop; p=130 | SW | key V | 56 |
| Game Database (PixelDatabaseWindow) | 52, centred, Caps | 70 at (-14,-12) | no | no | no | search | category tabs | Pop; p=16 | SW | pause menu | - |
| Pixel Guide (PixelPixelGuide) | 44, centred, Caps | 64 at (-14,-12) | no | no | no | ! info (top left) | - | Pop; p=15 | SW | HUD | 100 (Shop's) |
| Collection Log (PixelCollectionLog) | page titles only | 56 RED at (-6,-6) | no | no | no | camera per page | page turning | Pop; p=70 | plain leather Image | Log book button | - |
| Outfit Studio (PixelPetsStudio) | 46, LEFT, hard-coded caps | none (Done) | no | no (modal) | no | - | - | no Pop; p=170 | SW | Customize | - |
| Pet Customize (PixelPetsCustomize) | 52, centred, no caps | none (Done) | no | no (modal) | no | - | - | no Pop; p=160 | SW | pet pencil | - |
| Visitor greeting / wares (PixelVisitorMinigame) | 54 / 50, centred, Caps | none (Accept / Reject / Done) | no | no | no | - | Buy / Sell | Pop; p=150 | SW | visitor event | - |
| Cubie speech (PixelGuideVendor) | 56, centred, Caps | none (Next) | no | no | no | - | - | Pop; p=150 | SW teal | tutorial / event | - |
| Next up (PixelNextUp) | 22, LEFT, hard-coded caps | none | yes (chrome) | PixelWindowChrome | yes (chrome) | undock | - | no Pop; not registered | SW | Settings toggle | 36 |
| Dev Hub (PixelDevHub) | 30, LEFT, hard-coded caps | 40 RED at (-8,-6) | yes (chrome) | PixelWindowChrome (+ scale) | yes (chrome) | undock, resize, search | 11 tool tabs | no Pop; p=200 | SW | F9 / pause menu | 52 |
| Offline progress (PixelOfflineProgress) | centred | none (Continue) | no | no (modal) | no | - | - | no Pop; p=200 | SW | on load | - |
| UFO helper (PixelRobotWorker) | 44, centred, Caps | 56 RED at (-14,-14) | no | no | no | - | - | p=55 | SW | world | - |
| Pet found popup (PixelPets) | 64, centred | none (OK) | no | no | no | - | - | p=150 | SW | event | - |

## Inconsistencies found
- Close X: 8 sizes (80 / 70 / 64 / 60 / 56 / 46 / 44 / 40), 7 offsets, grey in most but red in View, Collection Log, Dev Hub, UFO.
- Titles: 10 sizes (22-64), centred in most but left in Pocket, Next up, Dev Hub, Outfit Studio; four hard-code capitals, Pet Customize has none.
- Dragging: Dev Hub + Next up use PixelWindowChrome; Pocket has its own copy (drag + minimize + dock); View, tracker and pinned Inventory a bare PixelPopoutDrag. View's position is not saved.
- Minimize only in Pocket, Next up, Dev Hub, tracker, pinned Inventory; docking only in Pocket, Next up, Dev Hub.
- Bank (and Dev Hub, Studio, Customize, Offline) don't use PixelPop; Pocket, Next up and the tracker are not registered (Escape doesn't close them).
- PixelWindows priorities clash: Skills and Bank both 14; Toggles, Pixel Guide and Skill Tree all 15.
- Header heights set per window (90 / 100 / 60 / 56 / 52 / 36); Toggles and Pixel Guide borrow the Shop's.
- Top-left header slot used for different things (pin, book, hose, info, expand, search); a ? help button only in Shop (80) and Skills (70).
- Collection Log is the only main window without StyleWindow (on purpose: a leather book).

## The standard (chosen by the user 2026-10-18)
Built by `PixelUIKit.CreateWindow` (`PixelUIKitWindows.cs`); the numbers are constants at the top of that file.
- **Fixed windows** (the big HUD windows: Inventory, Log, Shop + Upgrades, Skills, Skill Tree, Toggles, Crafting, Bank, Game Database, Pixel Guide, Collection Log, UFO helper): open in place, NOT draggable, no minimize. Header 90, title 48 centred in capitals, grey X 70 at (-14, -12).
- **Tool windows** (View, Pocket, skill tracker, Next up, Dev Hub; the pinned Inventory keeps its own drag): draggable by the header with the position saved, minimize, dock to a screen edge (PixelWindowChrome). Header 56, title 30 centred in capitals, grey X 44 at (-8, -6), minimize just left of it.
- **Dialogs** (Outfit Studio, Pet Customize, visitor greeting / wares, Cubie's speech, offline summary, pet found): NO X - their Done / Continue / Next button is the choice (CreateWindow closeButton: false); fixed in the middle.
- Close X is always grey (no red ones).

## Progress (one window per commit)
- [x] View - Tool window: title 30 (was 32), grey X 44 (was red 46), NEW: minimize, dock to a screen edge, position remembered between sessions (it used to reset).
- [x] Inventory - X / pin buttons to the standard spots (2-6 px), Inspector header fields removed (it always has its header now). Pinned (compact) mode unchanged.
- [x] Log - X 70 moved 6 px right / 2 down, book button to the left slot (14,-12).
- [x] Collection Log - keeps the leather (styled: false); red 56 X -> grey 70; pages start below a 90 leather strip, so the default book is 52 taller (812).
- [x] Shop - title 56 -> 48, X 80 -> 70 grey, ? 80 -> 70 in the right slot, header 100 -> 90 (tabs / list / currency panel up 10). Upgrades window: title 48, X / Back 70, now grows in.
- [x] Toggles - title 44 -> 48, X 64 -> 70, header 100 -> 90 (content up 10).
- [x] Pixel Guide - title 44 -> 48, X / ! 64 -> 70, header 100 -> 90 (list up 10).
- [x] Skills - title 56 -> 48; pin / ? in the standard slots (? 2 px).
- [x] Skill Tree - title 52 -> 48; points + nodes 10 higher.
- [x] Crafting - X / book 6 px right-ish, title ~9 higher; Recipe Book panel X 60 -> standard 70.
- [x] Bank - title 44 -> 48, X / hose 64 -> 70, list 12 lower / shorter; NOW grows in / shrinks out like its neighbours.
- [x] Game Database - title 52 -> 48 (~4 higher), content unchanged.
- [x] UFO helper - red 56 X -> grey 70, title 44 -> 48, content 8 lower (no grow-in: rebuilt per page).
- [x] Outfit Studio, Pet Customize, Pet found - dialog titles 48 centred capitals (were 46 left / 52 / 64 no caps), keep their yellow colour; now grow in when shown (pet found: also the open sound).
- [x] Visitor greeting / aside / wares, Cubie's speech - titles 48 (were 54 / 50 / 56) keep their own colours; wares title no longer wraps, its list 6 lower.
- [x] Offline summary - "Welcome back!" moved into a standard header (48), the lines below ~30 higher; now grows in.
- [x] Next up - Tool window: title 30 centred (was 22 left), header 36 -> 56 (taller), width 200 -> 260, grey 44 minimize / undock, still no X, place kept.
- [x] Dev Hub - Tool window: title centred, red 40 X -> grey 44, search box moved to the left of the header (250 wide), no grow-in (it scales itself), place / scale kept.
- [x] Pocket - Tool window: its own drag / minimize / dock copy removed (chrome), title centred, X 44 in the corner, '-' text button instead of the bar icon, Info a square 44 button, place / minimized / docked state kept.
- [x] Prime Gauntlet - title 52 -> 48 (still gold, ~11 higher), X 60 -> standard 70 in the corner; keeps its gold-brown window.
- [x] Belt speed editor - a standard dialog: title 'BELT SPEED' 32 -> 48, the theme window colour (was its own blue-grey); its Close button stays (no X).
- Not on the builder (widgets, by choice): the skill tracker, the pinned Inventory.

## Known gaps (builder)
- The chrome's undock arrow uses the first right slot: a Tool window with an extra right header button must shift it while docked (Pocket does, LayoutHeaderButtons).
- Title colour is set after CreateWindow where a window keeps its own (pets yellow, visitors / Cubie their colour).
- Guarded: tools/compile-check/rules.sh fails when a script registers a window with PixelWindows without building it with CreateWindow (escape-only / window-built-in markers for the exceptions). The chrome raises StateChanged on minimize / dock.
