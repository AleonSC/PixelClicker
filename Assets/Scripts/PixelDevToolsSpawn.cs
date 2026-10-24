// ---- System notes ----
// PixelDevToolsSpawn.cs
//   Part of PixelDevTools (partial class): the spawn selector and the pinned spawn / cheat widget cards (spawned pixel, minigames, middle click, focus, bill, shop unlocks, XP, husk pile, clear world, super rares, dev items) and their drop-downs. The system notes are in PixelDevTools.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelDevTools
{
    // ------------------------------------------------------------------
    // On-screen spawn selector
    // ------------------------------------------------------------------

    private Button resetButton;
    private TMP_Text resetLabel;
    private TMP_Text timeStatus;   // the Time control widget's status line
    private float resetConfirmUntil;

    private void ResetCountsClicked()
    {
        if (clicker == null) return;
        if (Time.unscaledTime > resetConfirmUntil)
        {
            resetConfirmUntil = Time.unscaledTime + 4f; // first click: ask again
            if (resetLabel != null) resetLabel.text = resetConfirmText;
            return;
        }

        resetConfirmUntil = 0f;
        clicker.ResetAllCounts();
        PixelBank bank = PixelFind.First<PixelBank>();
        if (bank != null) bank.ClearStored(); // the stored pixels too
        if (resetLabel != null) resetLabel.text = resetCountsText;
    }

    // ------------------------------------------------------------------
    // Spawn widgets (they replaced the on-screen row at the top of the screen): 'Spawned pixel', 'Minigames' and 'Middle click'.
    // They sit on the pinned widget board at the top of the Main tab (a drop-down's list would be clipped inside the scrolling board).
    // ------------------------------------------------------------------

    private TMP_Dropdown selectorDropdown, selectorMinigameDropdown;
    private readonly List<PixelClicker.PixelType[]> selectorChoices = new List<PixelClicker.PixelType[]>();
    private Image spawnPixelButtonImage;

    /// <summary>Builds the three spawn widgets into the pinned board (each is one row of controls on a card; 'y' is the row position the controls are placed at).</summary>
    private void BuildSpawnWidgets(RectTransform root, PixelCardBoard board, float inner, float y)
    {
        float bw = 140f, gap = 10f, dropW = inner - bw * 2f - gap * 2f;

        // ---- Spawned pixel: which pixel the cube spawns. Spawn = make only this one spawn (and swap the current block now), Despawn = back to normal. ----
        int start = root.childCount;
        selectorDropdown = PixelUIKit.CreateDropdown(font, root, "Spawned Pixel Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(selectorDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        selectorDropdown.onValueChanged.AddListener(v => SetInt("SelPixel", v));
        Button spawn = PixelUIKit.CreateButton(font, root, "Spawn Pixel Button", spawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(spawn.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        spawn.onClick.AddListener(SpawnPixelClicked);
        spawnPixelButtonImage = spawn.GetComponent<Image>();
        Button despawn = PixelUIKit.CreateButton(font, root, "Despawn Pixel Button", despawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(despawn.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        despawn.onClick.AddListener(DespawnPixelClicked);
        AddTip(spawn.gameObject, "Make only the chosen pixel (or the Monochrome / RGB pack) spawn on the cube, and swap the pixel on the cube to it now.");
        AddTip(despawn.gameObject, "Go back to normal spawning.");
        Widget(root, board, "spawnpixel", start, y);

        // ---- Minigames: pick one, Spawn it or Despawn it (Despawn clears whatever minigame is on screen when the chosen one isn't). ----
        start = root.childCount;
        selectorMinigameDropdown = PixelUIKit.CreateDropdown(font, root, "Minigame Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(selectorMinigameDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        selectorMinigameDropdown.onValueChanged.AddListener(v => SetInt("SelMinigame", v));
        Button mgSpawn = PixelUIKit.CreateButton(font, root, "Spawn Minigame Button", spawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(mgSpawn.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        mgSpawn.onClick.AddListener(SpawnSelected);
        Button mgDespawn = PixelUIKit.CreateButton(font, root, "Despawn Minigame Button", despawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(mgDespawn.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        mgDespawn.onClick.AddListener(DespawnSelected);
        Widget(root, board, "spawnminigame", start, y);

        // ---- Middle click: swap the pixel on the cube for the chosen one (first entry = off). ----
        start = root.childCount;
        swapDropdown = PixelUIKit.CreateDropdown(font, root, "Swap Dropdown", new Vector2(inner, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(swapDropdown.GetComponent<RectTransform>(), 40f, y, inner);
        swapDropdown.onValueChanged.AddListener(v => SetInt("SelSwap", v));
        Widget(root, board, "middleclick", start, y);

        // ---- Focus potions: pick one, Drink it (60 s, replaces the active one) or Cancel the active one (PixelFocus). ----
        start = root.childCount;
        focusDropdown = PixelUIKit.CreateDropdown(font, root, "Focus Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(focusDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        focusDropdown.onValueChanged.AddListener(v => SetInt("SelFocus", v));
        Button drink = PixelUIKit.CreateButton(font, root, "Drink Focus Button", drinkFocusText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(drink.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        drink.onClick.AddListener(DrinkFocus);
        Button cancelFocus = PixelUIKit.CreateButton(font, root, "Cancel Focus Button", cancelFocusText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(cancelFocus.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        cancelFocus.onClick.AddListener(CancelFocus);
        AddTip(drink.gameObject, "Drink the chosen focus potion: the skill's hazard block stops spawning while it lasts (replaces the one that is active).");
        AddTip(cancelFocus.gameObject, "Stop the active focus potion early.");
        Widget(root, board, "focus", start, y);

        BuildCheatWidgets(root, board, inner, y, bw, gap, dropW);   // the bill, shop unlock, XP, husk pile, clear world, super rares and dev items cards
    }

    // BuildSpawnWidgets, step 2: the cards after the spawn ones (same pinned board, same row sizes: bw = button width, dropW = drop-down width).
    private void BuildCheatWidgets(RectTransform root, PixelCardBoard board, float inner, float y, float bw, float gap, float dropW)
    {
        // ---- Cubie's bill (PixelCubieBill): reveal it now, open the next trust tier for free, or reset it. ----
        int start = root.childCount;
        float billW = (inner - 2f * gap) / 3f;
        Button billReveal = PixelUIKit.CreateButton(font, root, "Bill Reveal Button", billRevealText, new Vector2(billW, rowHeight), buttonColor, textColor, fontSize);
        Place(billReveal.GetComponent<RectTransform>(), 40f, y, billW);
        billReveal.onClick.AddListener(() => { if (PixelCubieBill.Instance != null) PixelCubieBill.Instance.DevReveal(); });
        Button billTier = PixelUIKit.CreateButton(font, root, "Bill Tier Button", billNextTierText, new Vector2(billW, rowHeight), buttonColor, textColor, fontSize);
        Place(billTier.GetComponent<RectTransform>(), 40f + billW + gap, y, billW);
        billTier.onClick.AddListener(() => { if (PixelCubieBill.Instance != null) PixelCubieBill.Instance.DevNextTier(); });
        Button billReset = PixelUIKit.CreateButton(font, root, "Bill Reset Button", billResetText, new Vector2(billW, rowHeight), buttonColor, textColor, fontSize);
        Place(billReset.GetComponent<RectTransform>(), 40f + 2f * (billW + gap), y, billW);
        billReset.onClick.AddListener(() => { if (PixelCubieBill.Instance != null) PixelCubieBill.Instance.DevReset(); });
        AddTip(billReveal.gameObject, "Reveal Cubie's bill now: he comes with his invoice at the next calm moment (quietly with Cubie / tips off). Once revealed: opens the Bill window.");
        AddTip(billTier.gameObject, "Pay every pure colour up to the next milestone for free: the next trust tier opens (Cubie talks, more shop stock).");
        AddTip(billReset.gameObject, "Reset the bill: nothing paid, tier 0, not revealed (new Recyclers wear the tag again).");
        Widget(root, board, "cubiebill", start, y);

        // ---- Shop unlocks: pick a shop pack (or All), Unlock it for free or Relock it. Shop packs only; 'Unlock everything' also unlocks pixels and maxes skills. ----
        start = root.childCount;
        shopUnlockDropdown = PixelUIKit.CreateDropdown(font, root, "Shop Unlock Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(shopUnlockDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        shopUnlockDropdown.onValueChanged.AddListener(v => SetInt("SelShopPack", v));
        Button shopUnlock = PixelUIKit.CreateButton(font, root, "Shop Unlock Button", shopUnlockText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(shopUnlock.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        shopUnlock.onClick.AddListener(() => ShopUnlockPicked(true));
        Button shopRelock = PixelUIKit.CreateButton(font, root, "Shop Relock Button", shopRelockText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(shopRelock.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        shopRelock.onClick.AddListener(() => ShopUnlockPicked(false));
        AddTip(shopUnlock.gameObject, "Unlock the picked shop pack for free (an upgrade goes to its top level). 'All shop items' unlocks every pack and every pixel (their switches are left off). Skills are not touched: use Max XP.");
        AddTip(shopRelock.gameObject, "Take the picked shop pack back: its pixels are locked again and its feature switched off (an upgrade goes to level 0). 'All' relocks every shop pack.");
        Widget(root, board, "shopunlock", start, y);

        // ---- Max XP: pick a skill (or All skills), give it the XP up to its level cap, or take its XP away. ----
        start = root.childCount;
        skillXpDropdown = PixelUIKit.CreateDropdown(font, root, "Skill XP Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(skillXpDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        skillXpDropdown.onValueChanged.AddListener(v => SetInt("SelSkillXp", v));
        Button maxXp = PixelUIKit.CreateButton(font, root, "Max XP Button", maxXpText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(maxXp.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        maxXp.onClick.AddListener(() => SkillXpPicked(true));
        Button resetXp = PixelUIKit.CreateButton(font, root, "Reset XP Button", resetXpText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(resetXp.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        resetXp.onClick.AddListener(() => SkillXpPicked(false));
        AddTip(maxXp.gameObject, "Give the picked skill (or every skill, Standard included) the XP up to its level cap. No popups.");
        AddTip(resetXp.gameObject, "Take all XP away from the picked skill (or every skill): back to level 1.");
        Widget(root, board, "maxxp", start, y);
        FillSkillXpDropdown();

        // ---- Husk pile: fill the floor up to the husk limit, to profile a full floor (Dev Inspector's World tab shows bodies / awake). ----
        start = root.childCount;
        Button fillFloor = PixelUIKit.CreateButton(font, root, "Fill Floor Button", fillFloorText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(fillFloor.GetComponent<RectTransform>(), 40f, y, bw);
        fillFloor.onClick.AddListener(() => StartCoroutine(FillFloorWithHusks()));
        AddTip(fillFloor.gameObject, "Drop random husks round the cube until the floor limit is reached. Profile with Window > Analysis > Profiler; the Dev Inspector's World tab shows how many bodies are awake.");
        Widget(root, board, "huskpile", start, y);

        // ---- Clear world: every placed device (machines, timed devices, generators...) and every tester object (belts, couplings, hoses...) removed at once. ----
        start = root.childCount;
        Button clearWorld = PixelUIKit.CreateButton(font, root, "Clear World Button", clearWorldText, new Vector2(bw * 2f + gap, rowHeight), ButtonRole.Danger, fontSize);
        Place(clearWorld.GetComponent<RectTransform>(), 40f, y, bw * 2f + gap);
        TMP_Text clearWorldLabel = clearWorld.GetComponentInChildren<TMP_Text>();
        float clearArmedUntil = -1f;
        clearWorld.onClick.AddListener(() =>
        {
            if (Time.unscaledTime > clearArmedUntil)
            {
                clearArmedUntil = Time.unscaledTime + 3f;   // (a second click within 3 s confirms)
                if (clearWorldLabel != null) clearWorldLabel.text = clearWorldConfirmText;
                StartCoroutine(ResetLabelLater(clearWorldLabel, clearWorldText, 3f));
                return;
            }
            clearArmedUntil = -1f;
            if (clearWorldLabel != null) clearWorldLabel.text = clearWorldText;
            ClearWorld();
        });
        AddTip(clearWorld.gameObject, "Remove EVERY placed device (Recycler, generators, Junction Box, timed devices...) and every tester object (belts, couplings, hoses, kiln, jumps...) at once. Click twice to confirm. Husks and seeds stay.");
        Widget(root, board, "clearworld", start, y);

        // ---- Super Rares: pick a Dragon / God / Prime Cube, the Cubelette or the Golden Snitch and Spawn it on the clicker cube; Gauntlet gives a Prime Gauntlet. ----
        start = root.childCount;
        rareDropdown = PixelUIKit.CreateDropdown(font, root, "Super Rares Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(rareDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        rareDropdown.onValueChanged.AddListener(v => SetInt("SelRare", v));
        Button spawnRare = PixelUIKit.CreateButton(font, root, "Spawn Rare Button", spawnRareText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(spawnRare.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        spawnRare.onClick.AddListener(SpawnPickedRare);
        Button gauntlet = PixelUIKit.CreateButton(font, root, "Give Gauntlet Button", giveGauntletText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(gauntlet.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        gauntlet.onClick.AddListener(GiveGauntlet);
        AddTip(spawnRare.gameObject, "Replace the block on the clicker cube with the picked super rare (click it to collect it like a real find; the Golden Snitch waits for your click and flies).");
        AddTip(gauntlet.gameObject, "Put a Prime Gauntlet in your inventory (right-click it there to set the six Prime Cubes and forge the Cubelette).");
        // Second row: the Farmer always brings a Dragon Seed.
        farmerSeedsToggle = BuildToggleRow(root, farmerSeedsToggleText, y + rowHeight + WidgetRowGap, inner, farmerAlwaysSeeds, on =>
        {
            farmerAlwaysSeeds = on;
            PixelFarmerMinigame.DevAlwaysDragonSeeds = on;
            SetBool("FarmerSeeds", on);
        });
        Widget(root, board, "superrares", start, y, 2);
        FillRareDropdown();

        // ---- Dev items: the devices taken out of the shop (Device.devOnly) - pick one and Give puts one in your inventory. ----
        start = root.childCount;
        devItemDropdown = PixelUIKit.CreateDropdown(font, root, "Dev Items Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(devItemDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        Button giveItem = PixelUIKit.CreateButton(font, root, "Give Dev Item Button", giveDevItemText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(giveItem.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        giveItem.onClick.AddListener(GiveDevItem);
        AddTip(giveItem.gameObject, "Put one of the picked item in your inventory. These devices are not on sale anywhere for now (Quantum Organizer, Funnel, Sprinkler, Vacuum Device, Bait Bucket, Conveyor Belt Kit).");
        Widget(root, board, "devitems", start, y);
        FillDevItemDropdown();
    }

    private const float WidgetRowGap = 10f;   // space between the rows of a multi-row widget card
    private TMP_Dropdown rareDropdown;
    private readonly List<PixelClicker.PixelType> rareTypes = new List<PixelClicker.PixelType>();   // dropdown option -> pixel type (in the game's own order)

    /// <summary>Fills the 'Super Rares' drop-down with every rare collectible tier: Dragon Cubes, God Cubes, Prime Cubes, the Cubelette, the Golden Snitch.</summary>
    private void FillRareDropdown()
    {
        if (rareDropdown == null || clicker == null) return;
        int keep = rareDropdown.options.Count > 0 ? rareDropdown.value : GetInt("SelRare", 0);
        rareTypes.Clear();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
        {
            if (t == null || !(t.rareDrop || PixelClicker.IsRareCube(t.type) || t.type == PixelClicker.PixelType.InfinityCubelette)) continue;
            if (rareTypes.Contains(t.type)) continue;
            rareTypes.Add(t.type);
            options.Add(new TMP_Dropdown.OptionData(t.displayName + (t.count >= 1d ? "  (held)" : "")));
        }
        rareDropdown.ClearOptions();
        rareDropdown.AddOptions(options);
        rareDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, options.Count - 1)));
        rareDropdown.RefreshShownValue();
    }

    /// <summary>Puts the picked super rare on the clicker cube in place of the current block.</summary>
    private void SpawnPickedRare()
    {
        if (clicker == null || rareDropdown == null) return;
        int v = rareDropdown.value;
        if (v < 0 || v >= rareTypes.Count) return;
        if (PixelGoldenSnitch.Active && rareTypes[v] == PixelClicker.PixelType.GoldenSnitch) return;   // (one snitch at a time)
        clicker.DevSwapCurrentPixel(rareTypes[v]);
        FillRareDropdown();
    }

    private TMP_Dropdown devItemDropdown;
    private readonly List<int> devItems = new List<int>();   // item index for each drop-down entry

    /// <summary>Fills the 'Dev items' drop-down with every Dev Tools-only device (PixelConsumables.ItemDevOnly).</summary>
    private void FillDevItemDropdown()
    {
        if (devItemDropdown == null) return;
        PixelConsumables consumables = PixelFind.Cached<PixelConsumables>();
        devItems.Clear();
        List<string> options = new List<string>();
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount; i++)
                if (consumables.ItemDevOnly(i)) { devItems.Add(i); options.Add(consumables.ItemName(i)); }
        devItemDropdown.ClearOptions();
        devItemDropdown.AddOptions(options);
        devItemDropdown.RefreshShownValue();
    }

    /// <summary>Gives one of the picked Dev Tools-only device.</summary>
    private void GiveDevItem()
    {
        PixelConsumables consumables = PixelFind.Cached<PixelConsumables>();
        if (consumables == null || devItemDropdown == null) return;
        if (devItems.Count == 0) FillDevItemDropdown();
        int v = devItemDropdown.value;
        if (v >= 0 && v < devItems.Count) consumables.AddItem(devItems[v], 1);
    }

    /// <summary>Gives one Prime Gauntlet (DeviceKind.Gauntlet) to the inventory.</summary>
    private void GiveGauntlet()
    {
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null) return;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDevice(i) && consumables.DeviceKindOf(i) == PixelConsumables.DeviceKind.Gauntlet) { consumables.AddItem(i, 1); return; }
    }

    private TMP_Dropdown skillXpDropdown;
    private int[] skillXpMap;   // option -> entry (0 = All skills, n = pack n - 1)

    /// <summary>Fills the 'Max XP' drop-down: 'All skills', then every skill with its level (alphabetical).</summary>
    private void FillSkillXpDropdown()
    {
        if (skillXpDropdown == null) return;
        PixelSkills s = PixelSkills.Instance;
        int keep = skillXpDropdown.options.Count > 0 ? skillXpDropdown.value : GetInt("SelSkillXp", 0);
        List<string> names = new List<string> { allSkillsText }, keys = new List<string> { "" };
        if (s != null && s.Packs != null)
            foreach (PixelSkills.SkillPack p in s.Packs)
            {
                names.Add(p != null ? p.displayName + "  (lv " + s.LevelOf(p) + ")" : "-");
                keys.Add(p != null ? p.displayName : "~");
            }
        skillXpMap = PixelUIKit.AddSortedOptions(skillXpDropdown, names, 1, keys);
        skillXpDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        skillXpDropdown.RefreshShownValue();
    }

    private void SkillXpPicked(bool max)
    {
        PixelSkills s = PixelSkills.Instance;
        if (s == null || skillXpDropdown == null) return;
        int v = PixelUIKit.Original(skillXpMap, skillXpDropdown.value);
        if (v == 0) { foreach (PixelSkills.SkillPack p in s.Packs) s.DevSetSkillXp(p, max); }
        else if (v - 1 < s.Packs.Count) s.DevSetSkillXp(s.Packs[v - 1], max);
        FillSkillXpDropdown();
    }

    private TMP_Dropdown shopUnlockDropdown;
    private int[] shopUnlockMap;   // option -> entry (0 = All, n = pack n - 1)

    /// <summary>Unlock (true) or relock (false) the shop pack picked in the 'Shop unlocks' widget; entry 0 = every pack.</summary>
    private void ShopUnlockPicked(bool unlock)
    {
        if (shop == null || shopUnlockDropdown == null) return;
        int v = PixelUIKit.Original(shopUnlockMap, shopUnlockDropdown.value);
        if (v == 0) { if (unlock) shop.DevUnlockAll(); else shop.DevRelockAllPacks(); }   // ('All shop items' = every pack AND every pixel, switches left off)
        else if (unlock) shop.DevUnlockPack(v - 1);
        else shop.DevRelockPack(v - 1);
        FillShopUnlockDropdown(false);
    }

    /// <summary>Fills the 'Shop unlocks' drop-down: 'All', then every shop pack with its state (keeps the pick).</summary>
    private void FillShopUnlockDropdown(bool first)
    {
        if (shopUnlockDropdown == null) return;
        int keep = first ? GetInt("SelShopPack", 0) : shopUnlockDropdown.value;
        List<string> names = new List<string> { shopAllText }, keys = new List<string> { "" };
        if (shop != null && shop.AllPacks != null)
            for (int i = 0; i < shop.AllPacks.Count; i++)
            {
                PixelShop.ShopPack p = shop.AllPacks[i];
                if (p == null) { names.Add("-"); keys.Add("~"); continue; }
                string state = shop.IsPurchased(i) ? "  (unlocked)" : shop.GetPackLevel(i) > 0 ? "  (level " + shop.GetPackLevel(i) + ")" : "";
                names.Add(p.displayName + (p.hideInShop ? "  [not in shop]" : "") + state);
                keys.Add(p.displayName);
            }
        shopUnlockMap = PixelUIKit.AddSortedOptions(shopUnlockDropdown, names, 1, keys);   // (alphabetical; 'All' stays first)
        shopUnlockDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        shopUnlockDropdown.RefreshShownValue();
    }

    private TMP_Dropdown focusDropdown;
    private int[] focusMap;   // option -> focus potion index

    private void DrinkFocus()
    {
        PixelFocus focus = PixelFocus.Instance;
        if (focus != null && focusDropdown != null) focus.Drink(PixelUIKit.Original(focusMap, focusDropdown.value));
    }

    private void CancelFocus()
    {
        if (PixelFocus.Instance != null) PixelFocus.Instance.Cancel();
    }

    /// <summary>Fills the focus potion drop-down from PixelFocus's list (keeps the pick).</summary>
    private void FillFocusDropdown(bool first)
    {
        if (focusDropdown == null) return;
        PixelFocus focus = PixelFocus.Instance;
        int keep = first ? GetInt("SelFocus", 0) : focusDropdown.value;
        List<string> focusNames = new List<string>();
        if (focus != null) foreach (PixelFocus.FocusPotion p in focus.Potions) focusNames.Add(p != null ? p.displayName : "-");
        focusMap = PixelUIKit.AddSortedOptions(focusDropdown, focusNames);   // (alphabetical)
        focusDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, focusNames.Count - 1)));
        focusDropdown.RefreshShownValue();
    }

    private void SpawnPixelClicked()
    {
        if (clicker == null || selectorDropdown == null) return;
        spawnSelectorEnabled = true;
        SetBool("Selector", true);
        int v = selectorDropdown.value;
        ApplySelector(v);
        if (v >= 0 && v < selectorChoices.Count && selectorChoices[v] != null && selectorChoices[v].Length == 1) clicker.DevSwapCurrentPixel(selectorChoices[v][0]);   // one exact pixel: the cube shows it right away
        RefreshSpawnedPixelButton();
    }

    private void DespawnPixelClicked()
    {
        if (clicker == null) return;
        spawnSelectorEnabled = false;
        SetBool("Selector", false);
        clicker.SetDevSpawnTiers(null);
        RefreshSpawnedPixelButton();
    }

    /// <summary>The Spawn button of the pixel widget is green while a spawn override is active.</summary>
    private void RefreshSpawnedPixelButton()
    {
        if (spawnPixelButtonImage != null) spawnPixelButtonImage.color = spawnSelectorEnabled ? new Color(0.25f, 0.6f, 0.3f, 1f) : buttonColor;
    }

    /// <summary>Pixel options: normal, Monochrome pack, RGB pack, then every pixel type (locked ones marked). Plus the minigame list and the middle-click list.</summary>
    private void FillSelector()
    {
        if (selectorDropdown == null || selectorMinigameDropdown == null || clicker == null) return;
        bool first = selectorChoices.Count == 0;
        int keep = first ? GetInt("SelPixel", 0) : selectorDropdown.value;
        selectorChoices.Clear();
        List<string> names = new List<string>();

        names.Add(spawnNormalText); selectorChoices.Add(null);
        names.Add(spawnMonochromeText);
        selectorChoices.Add(new[] { PixelClicker.PixelType.White, PixelClicker.PixelType.Gray, PixelClicker.PixelType.Black });
        names.Add(spawnRgbText);
        selectorChoices.Add(new[] { PixelClicker.PixelType.Red, PixelClicker.PixelType.Green, PixelClicker.PixelType.Blue });

        List<string> keys = new List<string> { "", "", "" };
        foreach (PixelClicker.PixelTier tier in clicker.Tiers)
        {
            names.Add(tier.displayName + (tier.unlocked ? "" : " (locked)"));
            keys.Add(tier.displayName);
            selectorChoices.Add(new[] { tier.type });
        }

        // Alphabetical (normal / Monochrome / RGB stay first); the choices are put in the same order as the options.
        int[] selMap = PixelUIKit.AddSortedOptions(selectorDropdown, names, 3, keys);
        List<PixelClicker.PixelType[]> selSorted = new List<PixelClicker.PixelType[]>();
        foreach (int i in selMap) selSorted.Add(selectorChoices[i]);
        selectorChoices.Clear();
        selectorChoices.AddRange(selSorted);
        selectorDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, names.Count - 1));
        selectorDropdown.RefreshShownValue();

        // Middle-click swap: off, then every pixel type.
        if (swapDropdown != null)
        {
            int keepSwap = first ? GetInt("SelSwap", 0) : swapDropdown.value;
            swapChoices.Clear();
            List<string> swapNames = new List<string> { swapPrefix + "off" }, swapKeys = new List<string> { "" };
            foreach (PixelClicker.PixelTier tier in clicker.Tiers)
            {
                swapNames.Add(swapPrefix + tier.displayName + (tier.unlocked ? "" : " (locked)"));
                swapKeys.Add(tier.displayName);
                swapChoices.Add(tier.type);
            }
            int[] swapMap = PixelUIKit.AddSortedOptions(swapDropdown, swapNames, 1, swapKeys);   // (alphabetical; 'off' stays first)
            List<PixelClicker.PixelType> swapSorted = new List<PixelClicker.PixelType>();
            for (int i = 1; i < swapMap.Length; i++) swapSorted.Add(swapChoices[swapMap[i] - 1]);
            swapChoices.Clear();
            swapChoices.AddRange(swapSorted);
            swapDropdown.SetValueWithoutNotify(Mathf.Clamp(keepSwap, 0, Mathf.Max(0, swapNames.Count - 1)));
            swapDropdown.RefreshShownValue();
        }

        // Minigames
        int keepGame = first ? GetInt("SelMinigame", 0) : selectorMinigameDropdown.value;
        dropdownMinigames.Clear();
        names.Clear();
        foreach (PixelMinigame game in PixelMinigame.All)
        {
            if (game == null) continue;
            dropdownMinigames.Add(game);
            names.Add(game.DisplayName);
        }
        int[] gameMap = PixelUIKit.AddSortedOptions(selectorMinigameDropdown, names);   // (alphabetical)
        List<PixelMinigame> gamesSorted = new List<PixelMinigame>();
        foreach (int i in gameMap) gamesSorted.Add(dropdownMinigames[i]);
        dropdownMinigames.Clear();
        dropdownMinigames.AddRange(gamesSorted);
        selectorMinigameDropdown.SetValueWithoutNotify(Mathf.Clamp(keepGame, 0, Mathf.Max(0, names.Count - 1)));
        selectorMinigameDropdown.RefreshShownValue();

        FillFocusDropdown(first);
        FillShopUnlockDropdown(first);
        FillSkillXpDropdown();
        FillRareDropdown();
    }

    private void ApplySelector(int index)
    {
        if (clicker == null || index < 0 || index >= selectorChoices.Count) return;
        clicker.SetDevSpawnTiers(selectorChoices[index]);
    }

    private void SpawnSelected()
    {
        if (selectorMinigameDropdown == null || selectorMinigameDropdown.value < 0 || selectorMinigameDropdown.value >= dropdownMinigames.Count) return;
        PixelMinigame game = dropdownMinigames[selectorMinigameDropdown.value];
        if (game != null) game.SpawnNow();
    }

    private void DespawnSelected()
    {
        if (selectorMinigameDropdown == null || selectorMinigameDropdown.value < 0 || selectorMinigameDropdown.value >= dropdownMinigames.Count) return;
        PixelMinigame game = dropdownMinigames[selectorMinigameDropdown.value];
        if (game != null && game.Busy) { game.DespawnNow(); return; }
        // The selected one isn't on screen: clear every minigame that is.
        foreach (PixelMinigame other in dropdownMinigames) if (other != null && other.Busy) other.DespawnNow();
    }
}
