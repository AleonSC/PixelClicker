// ---- System notes ----
// PixelSystems.cs
//   ONE list of the game's system components (Awake used to add them one line at a time). Each entry = a component type + the group it
//   belongs to (UI, Core, Skills, Rares and Shows, Characters, Camera, World, Shop, Minigames), in the order they must start.
//   - The Shop and Minigames groups (the shop's features and every built-in minigame / visitor) are added by PixelShop.Awake itself
//     (AddMissing with ShopGroups: on the shop's object, before it sets up its packs) and in edit mode by its OnValidate; PixelClicker.Awake's
//     AddMissing covers them too if the shop has not woken yet.
//   - At run time PixelClicker.Awake calls AddMissing: a system that is NOT in the scene yet is added to the clicker object (as before),
//     and in the Editor a warning names them, so they can be put in the scene for editing.
//   - In the Editor, the menu 'Pixel Clicker > Add Missing Systems To Scene' (Editor/PixelSystemsMenu.cs) puts every missing system on a
//     tidy 'Pixel Systems' object with one child per group, so their Inspector values can be edited and SAVED in edit mode (a component
//     only added in Play mode loses every change when Play stops). 'Pixel Clicker > Move Systems Off The Clicker' moves the ones sitting on
//     the clicker object into those groups, values included.
//   A new system goes in this list (with its group); every system script must sit in a file of the same name and find PixelClicker with
//   PixelFind (not only GetComponent), so it works on any object.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PixelSystems
{
    /// <summary>One system: its component type and the group (child object of 'Pixel Systems') it goes in.</summary>
    public struct Entry
    {
        public Type type;
        public string group;
        public Entry(Type type, string group) { this.type = type; this.group = group; }
    }

    /// <summary>Name of the scene object that holds the system groups.</summary>
    public const string RootName = "Pixel Systems";

    /// <summary>Every system, in start-up order.</summary>
    public static readonly Entry[] All =
    {
        new Entry(typeof(PixelTitleScreen), "UI"),
        new Entry(typeof(PixelHints), "UI"),
        new Entry(typeof(PixelUITheme), "UI"),
        new Entry(typeof(PixelTipQueue), "UI"),
        new Entry(typeof(PixelDeviceCard), "UI"),
        new Entry(typeof(PixelConsumableGallery), "UI"),
        new Entry(typeof(PixelStudioIntro), "UI"),
        new Entry(typeof(PixelGameState), "Core"),
        new Entry(typeof(PixelMaterials), "Core"),
        new Entry(typeof(PixelCrashLog), "Core"),
        new Entry(typeof(PixelViewBounds), "Core"),
        new Entry(typeof(PixelMinigameLimits), "Core"),
        new Entry(typeof(PixelSessionRecorder), "Core"),
        new Entry(typeof(PixelTimeControl), "Core"),
        new Entry(typeof(PixelPowerClick), "Core"),
        new Entry(typeof(PixelPets), "Characters"),
        new Entry(typeof(PixelSkills), "Skills"),
        new Entry(typeof(PixelBlockTooltip), "UI"),
        new Entry(typeof(PixelAimCursor), "UI"),
        new Entry(typeof(PixelSkillLevelPopup), "UI"),
        new Entry(typeof(PixelShopNudge), "UI"),
        new Entry(typeof(PixelTreasureMap), "Skills"),
        new Entry(typeof(PixelWhaleFight), "Skills"),
        new Entry(typeof(PixelGodFind), "Rares and Shows"),
        new Entry(typeof(PixelInfinity), "Rares and Shows"),
        new Entry(typeof(PixelCustomKeys), "UI"),
        new Entry(typeof(PixelCollectionLog), "UI"),
        new Entry(typeof(PixelDragonFind), "Rares and Shows"),
        new Entry(typeof(PixelPixelGuide), "UI"),
        new Entry(typeof(PixelShuffleBonus), "Skills"),
        new Entry(typeof(PixelFocus), "Skills"),
        new Entry(typeof(PixelTreeMode), "Skills"),
        new Entry(typeof(PixelBaitBucket), "Skills"),
        new Entry(typeof(PixelViewWindow), "Camera"),
        new Entry(typeof(PixelZoom), "Camera"),
        new Entry(typeof(PixelOrbitCamera), "Camera"),
        new Entry(typeof(PixelClickerAura), "World"),
        new Entry(typeof(PixelDayNight), "World"),
        new Entry(typeof(PixelSocketPlates), "World"),
        new Entry(typeof(PixelSapField), "World"),
        new Entry(typeof(PixelMyWindow), "UI"),
        new Entry(typeof(PixelDaylight), "World"),
        new Entry(typeof(PixelNextUp), "UI"),
        new Entry(typeof(PixelDidYouKnow), "UI"),
        new Entry(typeof(PixelFilters), "World"),
        new Entry(typeof(PixelBlockHold), "Skills"),
        new Entry(typeof(PixelHuskCrush), "Skills"),
        new Entry(typeof(PixelGoals), "UI"),
        new Entry(typeof(PixelHazardTell), "Skills"),
        new Entry(typeof(PixelBlockRules), "Skills"),
        new Entry(typeof(PixelGreatOak), "Skills"),
        new Entry(typeof(PixelMold), "Skills"),
        new Entry(typeof(PixelGoldenSnitch), "Rares and Shows"),
        new Entry(typeof(PixelEggHatch), "Rares and Shows"),
        new Entry(typeof(PixelFirstPerson), "Camera"),
        new Entry(typeof(PixelGuideVendor), "Characters"),
        new Entry(typeof(PixelCubieBill), "Characters"),
        new Entry(typeof(PixelDragonWish), "Rares and Shows"),
        new Entry(typeof(PixelOvercharge), "Skills"),
        new Entry(typeof(PixelElectricLinks), "Skills"),
        new Entry(typeof(PixelCameraIntro), "Camera"),
        new Entry(typeof(PixelFloor), "World"),
        new Entry(typeof(PixelSkybox), "World"),
        new Entry(typeof(PixelOrthoView), "Camera"),
        // The shop's systems (PixelShop.Awake adds just these two groups, in this order, before it sets up its packs).
        new Entry(typeof(PixelAutoClicker), "Shop"),
        new Entry(typeof(PixelGhostMinigame), "Minigames"),
        new Entry(typeof(PixelMeteorMinigame), "Minigames"),
        new Entry(typeof(PixelEvilTreeMinigame), "Minigames"),
        new Entry(typeof(PixelBlackholeMinigame), "Minigames"),
        new Entry(typeof(PixelBombMinigame), "Minigames"),
        new Entry(typeof(PixelPadMinigame), "Minigames"),
        new Entry(typeof(PixelStardustMinigame), "Minigames"),
        new Entry(typeof(PixelSnakeMinigame), "Minigames"),
        new Entry(typeof(PixelSortMinigame), "Minigames"),
        new Entry(typeof(PixelBreakoutMinigame), "Minigames"),
        new Entry(typeof(PixelDashMinigame), "Minigames"),
        new Entry(typeof(PixelBlinkMinigame), "Minigames"),
        new Entry(typeof(PixelWeakspotMinigame), "Minigames"),
        new Entry(typeof(PixelEvadeMinigame), "Minigames"),
        new Entry(typeof(PixelTrafficMinigame), "Minigames"),
        new Entry(typeof(PixelTinkererMinigame), "Minigames"),
        new Entry(typeof(PixelWizardMinigame), "Minigames"),
        new Entry(typeof(PixelFarmerMinigame), "Minigames"),
        new Entry(typeof(PixelEntrepreneurMinigame), "Minigames"),
        new Entry(typeof(PixelProspectorVendor), "Minigames"),
        new Entry(typeof(PixelForagerVendor), "Minigames"),
        new Entry(typeof(PixelAnglerVendor), "Minigames"),
        new Entry(typeof(PixelLumberjackVendor), "Minigames"),
        new Entry(typeof(PixelEggMinigame), "Minigames"),
        new Entry(typeof(PixelBeanMinigame), "Minigames"),
        new Entry(typeof(PixelShopTutorial), "Shop"),
        new Entry(typeof(PixelGrab), "Shop"),
        new Entry(typeof(PixelTimeStop), "Shop"),
        new Entry(typeof(PixelBank), "Shop"),
        new Entry(typeof(PixelRobotWorker), "Shop"),
        new Entry(typeof(PixelCrafting), "Shop"),
        new Entry(typeof(PixelCombo), "Shop"),
        new Entry(typeof(PixelConsumables), "Shop"),
        new Entry(typeof(PixelMarket), "Shop"),
    };

    /// <summary>The groups PixelShop adds itself (in its Awake, before its packs are set up).</summary>
    public static readonly string[] ShopGroups = { "Shop", "Minigames" };

    /// <summary>Adds every system that is not in the scene yet to 'host' (the clicker object), or only those of the given groups
    /// (PixelShop: ShopGroups). Returns the names of the ones it added.</summary>
    public static List<string> AddMissing(GameObject host, params string[] onlyGroups)
    {
        List<string> added = new List<string>();
        if (host == null) return added;
        foreach (Entry e in All)
        {
            if (onlyGroups != null && onlyGroups.Length > 0 && Array.IndexOf(onlyGroups, e.group) < 0) continue;
            if (e.type == null || PixelFind.First(e.type) != null) continue;
            host.AddComponent(e.type);
            added.Add(e.type.Name);
        }
#if UNITY_EDITOR
        if (added.Count > 0)
            Debug.LogWarning("PixelSystems: " + added.Count + " system(s) were added for this Play only, so their Inspector values can't be saved: "
                             + string.Join(", ", added) + ". Use the menu Pixel Clicker > Add Missing Systems To Scene (outside Play mode) to put them in the scene.");
#endif
        return added;
    }
}
