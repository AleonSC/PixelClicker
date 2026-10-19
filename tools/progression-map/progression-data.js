// Starting data for the Progression Map (tools/progression-map/index.html).
// The app loads this when it has nothing saved yet, or when you press "Reset to plan".
// It mirrors the design agreed in the Design Discussion chat (2026-10-19):
// Cubie's bill in the six pure colours, milestones that unlock tiers, prestige at 100%.
// Status: idea = suggested, agreed = decided but not built, built = in the game.
window.PROGRESSION_SEED = {
  version: 1,
  tiers: [
    { id: "t0", name: "Tier 0 · Pure", theme: "Learn the clicker", when: "First hour, before the bill",
      unlock: "Start of the game", shop: "Starter stock: Auto Clicker, basic upgrades, early devices", color: "#7d8696" },
    { id: "t1", name: "Tier 1 · Gathering", theme: "Skills", when: "Early mid game",
      unlock: "10% of Cubie's bill paid", shop: "\"A regular, then.\" Skill packs, factory parts, the better potions", color: "#3f8a4f" },
    { id: "t2", name: "Tier 2 · Making", theme: "Products", when: "Mid game",
      unlock: "30% of Cubie's bill paid", shop: "\"Back room's open.\" Bigger upgrades, generators, rarer consumables", color: "#c47a1f" },
    { id: "t3", name: "Tier 3 · Operation", theme: "Automation", when: "Late game",
      unlock: "60% of Cubie's bill paid", shop: "\"Don't tell the others.\" Premium stock, cosmetics, rare-hunting gear", color: "#6a4fb3" },
    { id: "t4", name: "Settle up · Prestige", theme: "Pay it all", when: "End of a run",
      unlock: "100% of Cubie's bill paid", shop: "A new, bigger bill", color: "#b5462e", checkCoverage: false }
  ],
  // Every tier should have at least one feature in each of these categories; the map warns when one is missing
  // (a tier with checkCoverage: false, like Prestige, is skipped).
  required: ["Skill", "Device", "Transport"],
  categories: ["Skill", "Device", "Transport", "Crafting", "Power", "Shop", "Cubie", "Goal", "Character", "Collectible", "Cosmetic", "System"],
  sessions: ["Design Discussion", "Skills", "Shops", "Devices", "Husks and Physics", "UI and Interfaces", "Cosmetic Elements", "Sound", "Code Cleanup", "Gameplay Logs", "Overview"],
  features: [
    // ---------- Tier 0 · Pure ----------
    { id: "f1", tier: "t0", name: "Pure colours (White to Blue)", category: "System", status: "built", session: "Skills", notes: "Gray and Black unlock through early goals (Crush 10 husks, Recycle 20)." },
    { id: "f2", tier: "t0", name: "Husk Crush", category: "Skill", status: "built", session: "Skills", notes: "Right-click a husk for XP. Crush bonuses (air, last-gasp, streak) capped at a click's XP." },
    { id: "f3", tier: "t0", name: "Chain Crush / Charge Crush", category: "Skill", status: "built", session: "Skills", notes: "Standard tree nodes: click = touching same-type husks, hold = a growing ring." },
    { id: "f4", tier: "t0", name: "Standard abilities", category: "Skill", status: "built", session: "Skills", notes: "Block Hold, Power Click, Aimed Clicks, What's Next, Shuffle." },
    { id: "f5", tier: "t0", name: "Mystery skill bar", category: "Goal", status: "built", session: "Skills", notes: "'???' bar from the first click, revealed as Standard at level 2." },
    { id: "f6", tier: "t0", name: "Goals are the unlocks", category: "Goal", status: "built", session: "Skills", notes: "Click 50 -> Recycler, Crush 10 -> Gray, Recycle 20 -> Black, Earn 100 Black -> RGB + shop." },
    { id: "f7", tier: "t0", name: "Recycler", category: "Device", status: "built", session: "Devices", notes: "Cubie's gift. Currency only, same-colour streak, suction ring." },
    { id: "f8", tier: "t0", name: "Sorting Hopper", category: "Device", status: "built", session: "Devices", notes: "Latches onto the Recycler; one filtered colour, overflow out of the bottom." },
    { id: "f9", tier: "t0", name: "Storage Crate", category: "Device", status: "built", session: "Devices", notes: "" },
    { id: "f10", tier: "t0", name: "Sweeper", category: "Device", status: "built", session: "Devices", notes: "Wind-up helper that rescues fading husks into the nearest container." },
    { id: "f11", tier: "t0", name: "Workbench (crafting with husks)", category: "Crafting", status: "agreed", session: "Devices", notes: "One placeable bench, keeps its stock when moved; currency buys access, husks build things." },
    { id: "f12", tier: "t0", name: "Recipe Book button", category: "Crafting", status: "agreed", session: "Devices", notes: "Browse recipes, 'Set on Workbench', 'Place Workbench' when none is placed." },
    { id: "f13", tier: "t0", name: "Build mode + free cam", category: "System", status: "agreed", session: "Devices", notes: "Opens on the first placement (Cubie's Recycler); build bar of owned placeables." },
    { id: "f14", tier: "t0", name: "Wooden chute", category: "Transport", status: "agreed", session: "Devices", notes: "Rough tier 1 transport: downhill only, husks spill." },
    { id: "f15", tier: "t0", name: "Pocket Biomass Generator", category: "Power", status: "built", session: "Devices", notes: "Small, fed by hand or falling husks; keeps nearby timers running." },
    { id: "f16", tier: "t0", name: "Receipt and bill reveal", category: "Cubie", status: "agreed", session: "Design Discussion", notes: "Receipt at minute 1 ('Amount owed: ???'), reminders, the bill at ~60 min." },

    // ---------- Tier 1 · Gathering ----------
    { id: "f20", tier: "t1", name: "Gathering skills", category: "Skill", status: "built", session: "Skills", notes: "Woodcutting, Foraging, Fishing; Mining and Farming to finish." },
    { id: "f21", tier: "t1", name: "Skill vendors", category: "Character", status: "built", session: "Shops", notes: "Big Tim, the Prospector, the Farmer: visit while their skill trains." },
    { id: "f22", tier: "t1", name: "Conveyor belt", category: "Transport", status: "built", session: "Devices", notes: "Tester piece today; Mk I-III upgrades idea." },
    { id: "f23", tier: "t1", name: "Sockets + couplings", category: "System", status: "built", session: "Devices", notes: "The one way belts and hoses connect." },
    { id: "f24", tier: "t1", name: "Junction Box", category: "Device", status: "built", session: "Devices", notes: "Eight couplings, direction from what's attached, filtered outputs." },
    { id: "f25", tier: "t1", name: "Coupling settings", category: "System", status: "agreed", session: "Devices", notes: "Filter, overflow priority, rate limit, colour tag, In / Out / Auto." },
    { id: "f26", tier: "t1", name: "Wood Chipper", category: "Device", status: "built", session: "Devices", notes: "Woodcutting's husk sink." },
    { id: "f27", tier: "t1", name: "Smelter", category: "Device", status: "idea", session: "Devices", notes: "Ore husks -> ingots. Mining has no husk use yet." },
    { id: "f28", tier: "t1", name: "Composter", category: "Device", status: "idea", session: "Devices", notes: "Wood, forage and leaf husks -> Fertilizer for Farming." },
    { id: "f29", tier: "t1", name: "Robot arm", category: "Transport", status: "agreed", session: "Devices", notes: "Moves husks between two side-by-side machines." },

    // ---------- Tier 2 · Making ----------
    { id: "f40", tier: "t2", name: "Making skills", category: "Skill", status: "idea", session: "Skills", notes: "Smithing, Firemaking, Electrician (tier 2 skill plan)." },
    { id: "f41", tier: "t2", name: "Tube", category: "Transport", status: "built", session: "Devices", notes: "Goes up and down, never spills." },
    { id: "f42", tier: "t2", name: "Height levels + auto supports", category: "System", status: "idea", session: "Devices", notes: "3-4 fixed levels, stilts under raised machines, shadow + level tint." },
    { id: "f43", tier: "t2", name: "Products (new item kind)", category: "Crafting", status: "agreed", session: "Devices", notes: "Machines make products: a body, no currency. Wood Chips and Charcoal move over." },
    { id: "f44", tier: "t2", name: "Skill generators + power lines", category: "Power", status: "agreed", session: "Devices", notes: "One shared base, a fuel table; a skill tree node unlocks each." },
    { id: "f45", tier: "t2", name: "Condenser", category: "Device", status: "idea", session: "Devices", notes: "10 husks -> 1 dense husk worth 10; helps the floor limit." },
    { id: "f46", tier: "t2", name: "Vendor orders + Delivery Pad", category: "Goal", status: "agreed", session: "Shops", notes: "Timed delivery orders for big rewards; a reason to build a factory." },
    { id: "f47", tier: "t2", name: "Launcher + catcher", category: "Transport", status: "agreed", session: "Devices", notes: "The 'fun' transport: flings husks in an arc." },
    { id: "f48", tier: "t2", name: "Loom", category: "Device", status: "idea", session: "Devices", notes: "Leaf / moss / seaweed husks -> cloth for pet outfits." },

    // ---------- Tier 3 · Operation ----------
    { id: "f60", tier: "t3", name: "Automation skill", category: "Skill", status: "idea", session: "Skills", notes: "XP from husks your factory processes; perks make machines better." },
    { id: "f61", tier: "t3", name: "Signals (sensors, gates)", category: "System", status: "idea", session: "Devices", notes: "A full crate starts a Recycler, a gate opens on a colour." },
    { id: "f62", tier: "t3", name: "Pneumatic tube", category: "Transport", status: "idea", session: "Devices", notes: "Very fast, batches, needs power." },
    { id: "f63", tier: "t3", name: "Teleport pads", category: "Transport", status: "idea", session: "Devices", notes: "Wireless pairs, expensive." },
    { id: "f64", tier: "t3", name: "Stronger offline machines", category: "System", status: "idea", session: "Design Discussion", notes: "Machines keep working while you're away (Q10)." },
    { id: "f65", tier: "t3", name: "Counter device", category: "Device", status: "idea", session: "Devices", notes: "Shows husks per minute through a line." },

    // ---------- Settle up · Prestige ----------
    { id: "f80", tier: "t4", name: "Cubie's bill + Bill window", category: "Cubie", status: "agreed", session: "Design Discussion", notes: "One bill in six colours, pay any time; interest only talked about." },
    { id: "f81", tier: "t4", name: "Prestige: Cubie's Goodwill", category: "Cubie", status: "idea", session: "Design Discussion", notes: "A reset with a lasting bonus; the next bill is bigger." },
    { id: "f82", tier: "t4", name: "Collection Log", category: "Collectible", status: "built", session: "UI and Interfaces", notes: "Long-tail goal: every rare found." },
    { id: "f83", tier: "t4", name: "Skill capes / max-level cosmetics", category: "Cosmetic", status: "idea", session: "Cosmetic Elements", notes: "Q1: cosmetics locked behind long goals." }
  ]
};
