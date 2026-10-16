# Pixel Clicker - tester guide (for Supper)

Hey! Thanks for playing again. The game has changed a lot since your last build, so here's what's new, what to try, and what I most
want to hear from you. Play however you like - the most useful thing is just playing normally and telling me how it felt.

## The big changes since you last played

- **Husks (the little cubes that fall off when you click) are material now, not money.** A click gives you that block's pixels; the
  husk that drops is a leftover. Husks give skill XP when you use them up, and they're what machines work with.
- **Cubie shows up after you've clicked 50 White pixels** and gives you a gift, including a **Recycler**: drop husks in the bin, press
  its button, and you get some pixels back for them. (Remember he gave you that. He will too.)
- **Skills come much earlier.** Your Standard skill levels from every block; at level 2 the Skills window opens and you can start
  working towards your first skill pack.
- **Woodcutting has trees now.** The cube rolls small filler blocks (twigs, leaves...) and every so often a whole tree arrives and plays
  out on the cube block by block, with a bar at the top. Chop the last block for "Timber!". Some trees are infested - you'll see a
  warning on the bar.
- **Block Hold:** hold the RIGHT mouse button on the cube to pull the current block out of the rotation. Holding one of the six basic
  colours puts a bet on it: it might pay a lot more later... or nothing.
- **More skills:** Foraging and Fishing join Woodcutting, Mining, Farming and Electrician. Each has its own blocks, a nasty hazard
  block, perks and a skill tree.
- Smaller things: "Did you know?" tips at the side of the screen, a search bar in the Stats screen, and something extremely rare and
  golden that you probably won't see. If you do, click it.

## What to try

1. **Start a new game** and play the first 20-30 minutes without me explaining anything. This is the part I most want fresh eyes on.
2. **Get to your first skill** (Woodcutting through the Lumberjack's Pack) and play it for a while. Do the trees feel good? Too often,
   too rare?
3. **Try a few different ways of playing:** clicking yourself, the auto clicker, machines (Recycler, Storage Crate...), potions.
   None of them should feel like the only right way.
4. **Do whatever you'd naturally do next.** If you get stuck or bored, that's useful too - just note when.

## How to send feedback

- Press **F8** (or the green **Give feedback** button in the pause menu, Esc). Type what you think, take screenshots, and draw on them.
  It all saves into one file - "Open feedback folder" shows where. Send me that folder when you're done.
- Or just message me. Short notes are fine ("got bored around the shop", "didn't get what husks were for").

## What I'd love to know

- **When did you first feel like you were making progress?** And was there a point where you stopped feeling it?
- **Did husks make sense?** Did you know what to do with them?
- **What confused you?** Any moment where you didn't know what to do next.
- **When did you get bored, if at all?**
- **What did you want to do that you couldn't?**
- **What was the most fun moment?**
- Anything that looked broken, glitched, or wrong.

## Don't worry about

- **Numbers and prices.** Balance is the next big job, so if something costs way too much or levels way too slowly, tell me - but
  don't feel you have to push through it.
- **Some unfinished extras** that are clearly placeholders. If you're not sure whether something's broken or just unfinished,
  mention it anyway.

Thanks again - watching you play last time helped more than anything else.

---

## Before sending this build (note for Antaleon - delete this section before sending)

- **Test the build yourself first:** the code split and the Pixel Systems change (Pixel Clicker > Add Missing Systems To Scene, then
  Move Systems Off The Clicker, save the scene) are untested, and so is the new filler timing.
- **Tree mode is behind a hidden test pack.** "Oak Trees (test)" isn't sold in the shop and the Dev Tools are off in normal builds.
  Either unlock it in the save you give Supper, or ask me to make tree mode part of the Lumberjack's Pack so it's on for everyone.
- **Tester features** (conveyor belts, splitter, elevator, vacuum cleaner, container hose...) live in the Dev Hub, so Supper won't see
  them unless you turn the Dev Tools on for his build.
- The F8 key is the default; if you rebound it, update the line above.
- **Session recorder:** tick **Record Every Play** on the PixelSessionRecorder component (Pixel Systems > Core) in Supper's build. His
  session is then written into the same Feedback folder he sends you (every 5 minutes and on quit) - paste that report to me and I'll
  set balance numbers from his real pace. In your own testing, use the Dev Hub's Session recorder widget (Start, then Stop & save).
- Run **Pixel Clicker > Add Missing Systems To Scene** once more so the new recorder is in the scene (the Console warns until you do).
