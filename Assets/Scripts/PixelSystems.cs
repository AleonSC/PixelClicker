// ---- System notes ----
// PixelSystems.cs
//   ONE list of the game's system components (Awake used to add them one line at a time). Each entry = a component type + the group it
//   belongs to (UI, Core, Skills, Rares and Shows, Characters, Camera, World), in the order they must start.
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
    };

    /// <summary>Adds every system that is not in the scene yet to 'host' (the clicker object). Returns the names of the ones it added.</summary>
    public static List<string> AddMissing(GameObject host)
    {
        List<string> added = new List<string>();
        if (host == null) return added;
        foreach (Entry e in All)
        {
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
