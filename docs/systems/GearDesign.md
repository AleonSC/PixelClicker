# Gear, first person unlock and the alien vendor (design, not built yet)

Status: **ideas agreed in chat (2026-10-15/16), nothing built.** The to-do list has one line per step (`Assets/Resources/Todo.txt`, "Gear system" onwards).
This doc collects what was agreed and, more importantly, the **blank spots**: the decisions nobody has made yet. Each blank spot has a
suggested default (marked **Suggestion**) so building can start without waiting, but the user decides.

## The four pieces and their order

1. **Gear system:** one gear slot (you're a cube), free swapping, later more faces.
2. **First person as a mid-game unlock:** the Cube Visor, the first gear piece.
3. **The alien vendor:** only reachable in first person; sells relics and face sockets.
4. **Legendary gear:** the Prime Gauntlet's powers, God Cube relics, the Dragon Radar, the Seeker's Charm.

Each step needs the one before it. Building out of order means hand-wiring things that should come from the shared gear system.

## Goals

- **Gear changes HOW you play, not just your numbers.** Each piece is a new action or a new rule, not "+10% pay". The game already has
  many % layers (potions, Focus, pets, perks, combo, stakes, upgrades, Operation).
- **Choices matter.** One slot means wearing one thing means not wearing another.
- **The super rares finally pay off.** Today the God Cubes and Prime Cubes mostly sit in the Collection Log, and the Cubelette is a trophy.
- **First person becomes a place with things to do,** and reaching it is a "wow" moment instead of a key you press on day one.

---

## 1. Gear system

### Agreed
- **One slot, on the top face, worn like a hat.** Cubie's line: *"Where would you even put a second one? You don't have arms."*
- **Free, instant swapping,** with a quick-swap key, so gear is picked for the moment.
- **Later, more faces:** "face sockets" unlock the other faces one at a time, up to six; the bottom face comes last ("you're sitting on it").
  The alien sells sockets as its priciest item.
- **Data-driven:** one entry per gear item (name, slot, where it comes from, its effect); effects plug in through shared hooks the way
  `PixelBlockRules` works, never checks scattered through `PixelClicker`.
- **Lives in Pocket** (`PixelMyWindow`) as a new Gear tab. Saved per save file; a Database category; Stats rows.

### Blank spots
- **Is gear a new kind of item, or a consumables Device?** The Prime Gauntlet is already a Device (`DeviceKind.Gauntlet`), and devices have
  owned counts, the inventory, prices and the shop.
  **Suggestion:** gear items are Devices with a new "equippable" flag, so the shop, inventory, crafting and saving all work already;
  the equipped item is one saved field.
- **Can you own duplicates?** **Suggestion:** no, gear is one of each (like the God Cubes).
- **What does a second face DO when gear is worn there - the same as the top face?** Or does each face have a theme (top = active power,
  sides = passive, bottom = ???)? **Suggestion:** all faces equal at first; themes only if it feels flat.
- **How many face sockets, at what price, and where else do they come from** (only the alien, or also skill levels / Mastery)?
- **Swap cooldown?** Free swapping could let a player cycle gear every second to stack effects.
  **Suggestion:** no cooldown, but an item's power cooldown keeps running while it is off, so cycling gains nothing.
- **Does gear show on the cube?** A tiny hat / glove / visor on the top face would be charming, but every item then needs a model.
  **Suggestion:** yes, small and simple, built from primitives like the vendors are.
- **Does the auto clicker trigger gear powers,** or only your own clicks? **Suggestion:** only your own clicks.

---

## 2. First person as a mid-game unlock

### Agreed
- Today first person is available from the start (its key only checks for menus / takeovers: `PixelFirstPerson.CanEnter`).
- It becomes a **gift: the Cube Visor**, the first gear piece, given at a mid-game milestone.
- **First use plays a camera dive** down into the floor.
- **Hidden until then:** the View window's First person tick box and the first person key.
- Dev Hub: the Unlock widget can give it for testing.

### Blank spots
- **Which milestone?** Candidates: buying a second skill pack, Standard level 5, a Mastery %, Cubie's third visit, owning N devices.
  **Suggestion:** a Cubie visit triggered by Standard level 5, so it fits his existing visit system and the Standard skill.
- **Who gives it?** Cubie (the guide) or the alien (but the alien needs first person to be reached at all).
  **Suggestion:** Cubie gives the Visor; the alien's ship is what you discover with it.
- **Does the Visor have to be WORN to use first person?** If yes, first person costs your one gear slot, which clashes with the alien's
  relics (also first-person gear). **Suggestion:** no, once received, first person is unlocked for good and the Visor is a free bonus item
  (or it doesn't take the slot). This needs a decision before building.
- **The Collection Log gallery** is walked in first person. Should it stay usable before the unlock? **Suggestion:** yes, the gallery is its
  own room; it doesn't spoil the main floor reveal.
- **What does the dive look like,** and is there a line from Cubie after it?

---

## 3. The alien vendor

### Agreed
- A **3D alien** whose UFO lands at the **edge of the floor**, a spot never seen from the normal top view. You walk over in first person.
- Ties the game's UFO (the UFO Helper, the main menu UFO) into the world.
- Built on the shared vendor base **`PixelVisitorMinigame`** (wares, prices, dialogue), adding only the 3D body and the walk-up trigger -
  not a hand-wired fourth vendor.
- Sells **relics** (first person gear) and **face sockets**.

### Blank spots
- **Is the ship always there once unlocked, or does it visit** on a timer like Cubie and the other vendors?
  **Suggestion:** it visits (lands, stays a few minutes, leaves), so walking out is a small event, and the first landing is scripted to
  happen right after the Visor.
- **What does it charge?** Ordinary pixels, a new currency, husks, or something only first person gives (phase pixels - see relics)?
  **Suggestion:** phase pixels, so first person feeds its own shop.
- **Name and personality.** Every vendor has one (Cubie, Pip the Prospector, Big Tim the Lumberjack). Also: does it talk in text, or in
  bleeps with subtitles?
- **What if the player never goes into first person?** Does anything nudge them (a Cubie line, a beam of light visible from the top view)?
  **Suggestion:** a faint light beam at the floor edge that you can just see from the normal view.
- **How does "walk up and talk" work?** Look at it and press a key, click it, or just walk close? **Suggestion:** walk close, a prompt
  appears in the bottom hint box (`PixelHintBox`), press the interact key.
- **Does it ever appear on the main menu** with the other wandering vendors?

---

## 4. Alien relics (first person gear)

### Agreed (the ideas)
| Relic | What it does |
|---|---|
| Grav Boots | Low gravity and high jumps; reach husks stacked up high |
| Tractor Glove | Pull husks to you from across the floor, then throw them (e.g. into the Recycler) |
| Xeno Lens | Shows hidden **phase pixels** that only exist in first person and pay well |
| Blink Core | A short teleport dash |
| Probe Drone | A little bot that follows you and clicks the cube while you are away from it |

- Relics are **first-person-only on purpose,** so first person needs enough to do there.

### Blank spots
- **Phase pixels need their own design:** what they look like, where they appear, how often, what they pay, whether they are a currency.
  They are the backbone of first person's reason to exist, so they deserve their own short doc before the relics are built.
- **Does first person have enough to do with these five?** Today you can walk, click the cube and grab husks. The relics add movement and
  husk handling; is that enough to make it a real way to play, or does it need a goal (e.g. the machine repair idea already on the to-do)?
- **Is the Probe Drone too strong?** It is basically an auto clicker that works while you're away. **Suggestion:** it clicks slower than the
  Auto Clicker and only while you're in first person.
- **Tractor Glove vs Pixel Grabbing:** grabbing already exists. What makes the glove different enough? (Range? Several at once? Throwing?)
- **Do relics do anything at all outside first person?** Agreed: no, on purpose. Confirm, since it means top-view players never use them.

---

## 5. Legendary gear (the super rares)

### Agreed (the ideas)
- **The Prime Gauntlet becomes gear** instead of being used up by forging. Each Prime Cube set in it unlocks a power with a cooldown:

| Stone | Power |
|---|---|
| Space | Pull every floor husk into the Recycler or crate at once |
| Time | Rewind the last 10 s of clicks, or a free long Time Stop |
| Power | One punch breaks any tough block at 50x pay |
| Reality | Turn the current block into any block you pick |
| Mind | See the next 5 blocks and choose which one comes up |
| Soul | Pets much stronger for a minute |

  All six = **the Snap:** half the husks on screen vanish and pay out; long cooldown.
- **The Cubelette** becomes what the Gauntlet turns into after so many Snaps.
- **Dragon Radar** charm: a hint when a rare is about to roll. The wish stays as it is.
- **God Cube relics:** Slifer = every 20th click chains lightning to nearby husks; Ra = a sun phase, a golden hour for a minute;
  Obelisk = tough blocks take double hits.
- **Seeker's Charm** from the Golden Snitch: later snitches are easier to catch and pay more.

### Blank spots
- **Super rares are 1 in 70,000 to 1 in 7 million.** Most players will never own legendary gear. Is that fine (bragging rights), or should
  there be a slow guaranteed path too (e.g. the Wizard sells a lesser copy)?
- **How do you GET a God Cube relic?** Is the God Cube itself the relic (wear the cube), or does finding it unlock a crafted relic?
  **Suggestion:** the find unlocks a relic you can equip; the cube stays in the Collection Log.
- **"Rewind the last 10 s"** needs the game to record recent clicks and undo them (currency, husks, the cube's block). That is a big piece
  of work on its own. **Suggestion:** keep only the "free long Time Stop" half for the first version.
- **Mind and Reality could break the skills' designed rhythms** (Woodcutting's tree shapes, Fishing's spawn table). Do they work during
  trees / special blocks, or only in the normal rotation?
- **How many Snaps make the Cubelette, and what does the Cubelette do** once it exists (still a trophy, or the strongest gear piece)?
- **Gauntlet with only some stones:** usable from 1 stone, or only once all six are set? **Suggestion:** each stone's power works as soon as
  it is set; the Snap needs all six.
- **The Dragon Radar's hint:** a sound, a glow on the cube, a line in the status box? And how early before the rare roll?

---

## Fits the game / could be better

- **Reuses:** the Devices list (owning, prices, saving), Pocket for the tab, the vendor base for the alien, `PixelBlockRules`-style hooks
  for effects, the Gauntlet window for setting stones, the first person system and its neon floor, `PixelHintBox` for prompts.
- **Needs first:** the **rare set table** cleanup already on the to-do (one entry per super-rare family), so legendary gear reads its data
  from one place instead of enum ranges.
- **New pieces:** the gear entry list + effect hooks, the equipped-item save field, phase pixels, the alien's 3D body and landing.
- **Watch for:** each gear effect becoming another scattered check. Effects should ask the gear system ("is the Tractor Glove worn?")
  through one call, the way `PixelCalm` answers "is the game in a menu?".
