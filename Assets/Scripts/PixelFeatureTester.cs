// ---- System notes ----
// PixelFeatureTester.cs
//   The 'Tester' tab of the Dev Hub: a place to spawn and try things that are not part of the game yet (a consumable, a device, a machine...). Each testable thing is a Feature registered in
//   Awake (Register(name, description, ghostBuilder, placeBuilder)): the tab lists them as cards with a 'Place' button. Place = a see-through GHOST follows the mouse over the floor
//   (PixelConsumables.TryGetFloorPoint); left click puts a real one down (and keeps placing), Q / E turn it, right click or Escape stops (PixelWindows entry); 'Clear' removes the ones placed.
//   While placing, clicks on the cube are blocked (PixelClicker.ExternalClickBlock). Placed test objects ARE saved (see SAVING below). To add something to test: write a builder (see PixelWoodChipper:
//   CreateGhost / Place) and add one Register line here.
//   Two-point features (RegisterLine, a conveyor belt) use a marker ghost, then a preview piece between the first point and the mouse; every click builds a piece and the end starts the next.
//   DELETE: hold the right button on any placed tester object (not while placing) to remove it (PixelHold ring; a quick right click still flips a belt / re-links a panel: PixelRightTap). BELT SELECTION card:
//   'Select belts' mode toggles belts by click or box-drag (teal glow, PixelConveyor.SetSelected) and sets speed / reverses them together. Placing snaps to a grid by default; SHIFT = free placement. (It used to be: SHIFT while placing snaps to a grid and angle steps (PixelSnap).
//   SAVING: every placed object is saved with the save file (PixelFeatureTester.ExportAll / ImportAll, TesterSave = feature name + JSON from the feature's ExportState / ImportState; hooked into
//   PixelSaveGame after the placed devices are restored). LINKING (Solar Panel): after placing, linkPanel mode - click a device inside the ring; a right click on a placed panel starts it again.
//   The Conveyor Belt is also a real game item (PixelConsumables DeviceKind.ConveyorBelt, on sale at Electrician level 5): its right click calls PixelFeatureTester.StartFeature("Conveyor Belt"), which places without the Dev Hub.
//   Features now: Vacuum Cleaner (PixelVacuumCleaner.cs: drive with W A S D / arrows, the front intake sucks husks up and the hose spits them out where the mouse aims), Container Hose (PixelContainerHose.cs: a single line from a crate / organizer to a Recycler / crate / organizer / trash can), Wood Chipper (PixelWoodChipper.cs), Conveyor Belt (PixelConveyor.cs, PixelFactoryKit.cs), Kiln (PixelKiln.cs; makes Charcoal old pixels), Solar Panel (PixelSolarPanel.cs).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelFeatureTester : MonoBehaviour
{
    private const float W = PixelDevHub.ContentW, H = PixelDevHub.ContentH, CardH = 150f;

    /// <summary>A feature may snap its placement (the Wood Chipper latches onto a belt opening): returns true when it did, with the snapped pose.</summary>
    private delegate bool PlaceSnap(PixelClicker c, Vector3 point, float yaw, GameObject ghost, out Vector3 snappedPoint, out float snappedYaw);

    /// <summary>A line feature's snap: the point (with its height) of a belt end / elevator top the mouse is near; 'snapped' says whether it latched.</summary>
    private delegate Vector3 SnapFunc(PixelClicker c, Vector3 point, out bool snapped);

    private sealed class Feature
    {
        public PlaceSnap placeSnap;
        // Saving with the save file: export a placed object to JSON, import it back (null = this feature is not saved).
        public Func<GameObject, string> export;
        public Func<PixelClicker, string, GameObject> import;
        public bool linkAfterPlace;   // after placing, click a device to link it (the Solar Panel)
        public Func<IEnumerable<GameObject>> live;   // the placed list comes from the world (couplings: saved with their device, so they are not this feature's own)
        public string name, description;
        public Func<PixelClicker, GameObject> ghost;
        public Func<PixelClicker, Vector3, float, GameObject> place;
        // Two-point features (a conveyor belt): click a start, click an end, repeat (the end starts the next piece).
        public bool line;
        // Routed features (the Container Hose): click a start, any number of bend points, and an end that snaps (a coupling); one piece through them all.
        public bool route;
        public Func<PixelClicker, IReadOnlyList<Vector3>, GameObject> placeRoute;
        public Action<GameObject, IReadOnlyList<Vector3>> updateRoute;
        public Func<PixelClicker, GameObject> preview;
        public Func<PixelClicker, Vector3, Vector3, GameObject> placeLine;
        public Action<GameObject, Vector3, Vector3> updatePreview;
        public SnapFunc snap;
        public Func<Vector3, Vector3, string> lineCheck;   // two-point features: why a piece from a to b can't be placed (null = it can) - the conveyor's turn limit
        // Tall features (the Conveyor Elevator): after the base is clicked the mouse sets the HEIGHT, a second click confirms.
        public bool heightStage;
        // Scroll stage (the Belt Jump): stage 2 sets a value with the SCROLL WHEEL instead of the mouse height (stageValue / setStageValue, in stageStep steps
        // between stageMin and stageMax); requireSnap = stage 1 can only be clicked while the ghost is snapped (placeSnap returned true).
        public bool scrollStage, requireSnap;
        public Func<GameObject, float> stageValue, stageStep, stageMin, stageMax;
        public Action<GameObject, float> setStageValue;
        public string stageLabel = "gap";
        public bool groundPlane;      // placed on the flat ground level, not on whatever the mouse ray hits (belts, towers, the Junction Box)
        public Func<PixelClicker, Vector3, float, float, GameObject> placeTall;
        public readonly List<GameObject> placed = new List<GameObject>();
        public TMP_Text placeLabel, clearLabel;
    }

    private static PixelFeatureTester instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null; kept = null;
        PixelStatics.ResetOnRestart(OnSceneUnloaded);
    }

    // (Restart reloads the scene: the old game's loaded test objects must not be written into the new game's saves)
    private static void OnSceneUnloaded() { kept = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Feature Tester").AddComponent<PixelFeatureTester>();
    }

    private static readonly Color BoxColor = new Color(0.14f, 0.17f, 0.26f, 1f), TextColor = new Color(0.92f, 0.94f, 1f, 1f);

    private readonly List<Feature> features = new List<Feature>();
    private TMP_FontAsset font;
    private PixelClicker clicker;
    private PixelConsumables consumables;
    private TMP_Text statusLabel;
    private Feature placing;

    /// <summary>A tester feature is being placed (its clicks belong to the placing).</summary>
    public static bool Placing => instance != null && (instance.placing != null || instance.selecting);
    private GameObject ghost;
    private float yaw;
    private int placeStartFrame;
    private bool blocking;
    private bool hasStart;            // a line feature: the first point is chosen
    private Vector3 lineStart;
    private GameObject previewGhost;  // a line feature: the belt that stretches from the first point to the mouse

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>Adds something to test (call from Awake).</summary>
    private void Register(string name, string description, Func<PixelClicker, GameObject> ghostBuilder, Func<PixelClicker, Vector3, float, GameObject> placeBuilder)
    {
        features.Add(new Feature { name = name, description = description, ghost = ghostBuilder, place = placeBuilder });
    }

    /// <summary>Adds a two-point thing to test (a belt between two clicked points). 'markerBuilder' = the ghost at the mouse while choosing the first point.</summary>
    private void RegisterLine(string name, string description, Func<PixelClicker, GameObject> markerBuilder, Func<PixelClicker, GameObject> previewBuilder,
                              Action<GameObject, Vector3, Vector3> previewUpdate, Func<PixelClicker, Vector3, Vector3, GameObject> placeBuilder, SnapFunc snapPoint)
    {
        features.Add(new Feature { name = name, description = description, ghost = markerBuilder, line = true, preview = previewBuilder, updatePreview = previewUpdate, placeLine = placeBuilder, snap = snapPoint });
    }

    /// <summary>Adds a routed thing to test (a hose through clicked points, ending on a snap point).</summary>
    private void RegisterRoute(string name, string description, Func<PixelClicker, GameObject> markerBuilder, Func<PixelClicker, GameObject> previewBuilder,
                               Action<GameObject, IReadOnlyList<Vector3>> previewUpdate, Func<PixelClicker, IReadOnlyList<Vector3>, GameObject> placeBuilder, SnapFunc snapPoint)
    {
        features.Add(new Feature { name = name, description = description, ghost = markerBuilder, line = true, route = true, preview = previewBuilder, updateRoute = previewUpdate, placeRoute = placeBuilder, snap = snapPoint });
    }

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();

        Register("Coupling",
                 "The one piece that joins belts and hoses to a device. Bring it near a device: the free SOCKETS show teal rings and the coupling snaps onto the nearest one (Storage Crate in / out, Kiln mouth, Trash Can side, Recycler back hatch, Sorting Hopper back, Quantum Organizer side, Wood Chipper chute, Funnel output). Its arrow shows the way husks go: green into the device (In), orange out of it (Out); on an organizer click its teal dot to switch. Then end a belt at an In coupling or start one at an Out coupling (belts and hoses snap to it). It moves with the device and is saved with it. Hold the right button on it to take it off.",
                 c => PixelCoupling.CreateGhost(c), (c, p, yw) => PixelCoupling.PlaceAt(c, p, yw));
        features[features.Count - 1].placeSnap = PixelCoupling.TrySnapGhost;
        features[features.Count - 1].live = PixelCoupling.Removable;

        Register("Wood Chipper",
                 "A detailed trailer-mounted wood chipper. Wood pixels dropped into its hopper (or lying close above it) are shredded for Woodcutting XP. Anything else damages it, and enough of that breaks it for good - Clear it and place a new one. Drag pixels in with Pixel Grabbing. While placing, an arrow shows where its chips shoot out. Fit a Coupling on its chute (in front of the outlet) and start a belt there: its Wood Chips then drop straight onto the belt.",
                 c => PixelWoodChipper.CreateGhost(c), (c, p, yw) => PixelWoodChipper.Place(c, p, yw));
        features[features.Count - 1].export = PixelWoodChipper.ExportState;
        features[features.Count - 1].import = PixelWoodChipper.ImportState;

        RegisterLine("Conveyor Belt",
                     "Click a point, then another: a belt is built between them (the second point starts the next piece, so short pieces make a curve; right click ends the chain, right click again stops). Clicks near a belt's end (or an elevator's top) snap to it, at its height. SCROLL the mouse wheel to raise or lower the point you are placing: the belt's supports grow and the belt slopes, so ramps and bridges are possible. Right click a placed belt to reverse it. Anything standing on a belt is carried along - drop things on it with Pixel Grabbing, or put it under a machine's outlet.",
                     c => PixelConveyor.CreateMarker(c), c => PixelConveyor.CreatePreview(c), (g, a, b) => PixelConveyor.UpdatePreview(g, a, b),
                     (c, a, b) => PixelConveyor.Place(c, a, b), PixelConveyor.Snap);
        features[features.Count - 1].lineCheck = PixelConveyor.TurnProblem;
        features[features.Count - 1].export = PixelConveyor.ExportState;
        features[features.Count - 1].import = PixelConveyor.ImportState;
        RegisterRoute("Container Hose",
                     "A see-through hose between two COUPLINGS that you route yourself. Click an OUT coupling on a Storage Crate or Quantum Organizer (the crate's right-hand 'out' socket), then click as many points as you like to lead the hose round things (scroll raises / lowers the point), and finish by clicking an IN coupling on the target - a Recycler, another crate, an organizer, a Trash Can, the Kiln... (coupling clicks magnetize). Right click while routing takes the last point back. It pumps the source's stored husks through the hose one by one into the target, only as many as the target has room for. The ends follow the devices when you carry them. A quick right click on a placed hose turns its flow round (when both ends can work the other way).",
                     c => PixelConveyor.CreateMarker(c), c => PixelContainerHose.CreatePreview(c), PixelContainerHose.UpdatePreview, PixelContainerHose.Place, PixelContainerHose.Snap);
        features[features.Count - 1].export = PixelContainerHose.ExportState;
        features[features.Count - 1].import = PixelContainerHose.ImportState;
        Register("Vacuum Cleaner",
                 "A big canister vacuum on wheels (to become a consumable). Left-click its body to drive it: W A S D or the arrow keys move it (diagonals too), relative to the camera. Husks that come near its wide front intake are sucked in and automatically spat back out of its hose - the mouse aims the hose, and they land where it points. Click the body again or press Escape to stop driving.",
                 c => PixelVacuumCleaner.CreateGhost(c), (c, p, yw) => PixelVacuumCleaner.Place(c, p, yw));
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelVacuumCleaner.ExportState;
        features[features.Count - 1].import = PixelVacuumCleaner.ImportState;
        Register("Trash Can",
                 "A plain cylinder that deletes things. Put it on the floor: every loose old pixel within a small radius (the teal ring) - thrown out of a Funnel hose, a machine or the end of a conveyor belt - is pulled to its mouth and vanishes, no credit. Belts and hoses that end at a coupling on it feed it directly.",
                 c => PixelTrashCan.CreateGhost(c), (c, p, yw) => PixelTrashCan.Place(c, p, yw));
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelTrashCan.ExportState;
        features[features.Count - 1].import = PixelTrashCan.ImportState;
        Register("Belt Sorter",
                 "A low gantry you place anywhere on a conveyor belt (the ghost snaps to the belt under the mouse; the chute is on the side the mouse is). It kicks every pixel of one type off the side - onto the floor, or onto a belt that starts at the chute's mouth. Drop a block on its tray (drag one over it and let go) to choose what it filters; left click the shown block to take it out again (an empty tray lets everything pass). Right click the sorter to swap sides.",
                 c => PixelBeltSorter.CreateGhost(c), (c, p, yw) => PixelBeltSorter.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelBeltSorter.TrySnap;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelBeltSorter.ExportState;
        features[features.Count - 1].import = PixelBeltSorter.ImportState;
        Register("Conveyor Elevator",
                 "A tower that lifts pixels from the end of a belt to a higher belt. Bring the ghost near the end of a belt and it magnetizes to it (turned the belt's way). Click to fix the base, then move the mouse up or down to set the height and click again to confirm (right click goes back a step). Place a new belt starting at the top: its first point snaps to the tower's top. Pixels from the belt are lifted and come out of the top.",
                 c => PixelElevator.CreateGhost(c), (c, p, yw) => PixelElevator.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelElevator.TrySnap;
        features[features.Count - 1].heightStage = true;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].placeTall = PixelElevator.PlaceTall;
        features[features.Count - 1].export = PixelElevator.ExportState;
        features[features.Count - 1].import = PixelElevator.ImportState;
        Register("Junction Box",
                 "A box with eight coupling sockets, two on each side, and an opening on top that gently draws in husks above it (a real device: it is saved with the placed devices; also sold in the shop). Fit couplings and connect belts or hoses: a belt ending at a coupling / a hose pushing into it makes it IN, a belt starting at it / a hose pulling from it makes it OUT (its arrow shows which; no arrow = nothing attached). Husks that go in are deleted and NEW ones come out: to the OUT coupling whose filter is their type, else to the unfiltered outputs in turn, else wait in a small buffer (full = it refuses). Set a coupling's filter by dropping a husk on it, or click / scroll it. A Sorting Hopper latches onto its back.",
                 c => PixelJunctionBox.CreateGhost(c), (c, p, yw) => { PixelConsumables pc = PixelFind.Cached<PixelConsumables>(); return pc != null ? pc.PlaceDeviceNow(PixelConsumables.DeviceKind.JunctionBox, p, yw) : null; });
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].live = PixelJunctionBox.Placed;   // (saved and removed as a placed device, not by the tester)
        Register("Belt Jump",
                 "A pair of ramps for a conveyor line. Bring it to the END of a belt: it snaps on there (it can only go on a belt end). Click, then SCROLL to widen or narrow the gap - the landing ramp always sits straight across. Click to confirm. Husks reaching the belt's end are launched over the gap in the same arc every time and slide off the landing ramp onto a belt that starts there (belts snap to its end).",
                 c => PixelBeltJump.CreateGhost(c), (c, p, yw) => PixelBeltJump.PlaceGap(c, p, yw, PixelBeltJump.DefaultGapCubes * Mathf.Max(0.05f, c.OldPixelWorldSize)));
        features[features.Count - 1].placeSnap = PixelBeltJump.TrySnap;
        features[features.Count - 1].heightStage = true;
        features[features.Count - 1].scrollStage = true;
        features[features.Count - 1].requireSnap = true;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].placeTall = PixelBeltJump.PlaceGap;
        features[features.Count - 1].stageValue = PixelBeltJump.GapOf;
        features[features.Count - 1].setStageValue = PixelBeltJump.SetGap;
        features[features.Count - 1].stageStep = PixelBeltJump.StepWorld;
        features[features.Count - 1].stageMin = PixelBeltJump.MinWorld;
        features[features.Count - 1].stageMax = PixelBeltJump.MaxWorld;
        features[features.Count - 1].export = PixelBeltJump.ExportState;
        features[features.Count - 1].import = PixelBeltJump.ImportState;
        Register("Conveyor Combiner",
                 "A box that sits on a conveyor belt (bring it over a belt: it snaps on, turned the belt's way). Husks on that belt pass straight through. Each side has an inlet: lay a belt that ENDS at an inlet (belts snap there, square to the main belt) and its husks are merged onto the main belt at the box whenever there is a gap - two feeds become one.",
                 c => PixelConveyorCombiner.CreateGhost(c), (c, p, yw) => PixelConveyorCombiner.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelConveyorCombiner.TrySnap;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelConveyorCombiner.ExportState;
        features[features.Count - 1].import = PixelConveyorCombiner.ImportState;
        Register("Control Lever",
                 "A floor lever that stands BESIDE a conveyor belt, on either side, anywhere along it (bring it next to a belt: it snaps there on the side the mouse is on). LEFT-CLICK it to flip it: OFF (red lamp) stops every belt joined to that belt's line - end to end, either way round, corners included; ON (green) lets them run again.",
                 c => PixelControlLever.CreateGhost(c), (c, p, yw) => PixelControlLever.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelControlLever.TrySnap;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelControlLever.ExportState;
        features[features.Count - 1].import = PixelControlLever.ImportState;
        Register("Crafting Cube",
                 "A crafting machine (also placed from the Crafting window's 'Crafting Cube' button). Hover it and its book opens; RIGHT-CLICK it to choose a recipe. Feed it husks of the recipe's pixel types through its four couplings or drop them on top (by value); other ingredients come from your inventory and what it makes goes into your inventory.",
                 c => PixelCraftingCube.CreateGhost(c), (c, p, yw) => PixelCraftingCube.Place(c, p, yw));
        features[features.Count - 1].export = PixelCraftingCube.ExportState;
        features[features.Count - 1].import = PixelCraftingCube.ImportState;
        Register("Husk Source",
                 "A test box with a coupling socket on each side. LEFT-CLICK it to choose a block type (any type): it makes endless husks of that type out of every Out coupling that something takes them from - a belt starting at the coupling (while it has room), a machine's In coupling right in front of it, or a Container Hose pulling from it. Fit couplings on its sides first. Nothing connected = nothing made.",
                 c => PixelHuskSource.CreateGhost(c), (c, p, yw) => PixelHuskSource.Place(c, p, yw));
        features[features.Count - 1].export = PixelHuskSource.ExportState;
        features[features.Count - 1].import = PixelHuskSource.ImportState;
        Register("Kiln",
                 "A brick kiln with a glowing mouth on its front. Anything that goes into the mouth from the side burns (lead a conveyor belt into it, or throw things in): each burnt pixel makes a Charcoal old pixel that comes out of the chute at the back. Charcoal despawns like any old pixel, for now.",
                 c => PixelKiln.CreateGhost(c), (c, p, yw) => PixelKiln.Place(c, p, yw));
        features[features.Count - 1].export = PixelKiln.ExportState;
        features[features.Count - 1].import = PixelKiln.ImportState;
        Register("Solar Panel",
                 "A flat solar panel. It is powered while Solar pixels lie on it, and then it stops the countdown of the ONE device it is linked to (a blue line joins them). Solar pixels on it are slowly used up, unless they sit in a complete group of 5 - those last and power it for good. After placing it, click a device inside the blue ring to link it; right click a placed panel to link it again.",
                 c => PixelSolarPanel.CreateGhost(c), (c, p, yw) => PixelSolarPanel.Place(c, p, yw));
        features[features.Count - 1].export = PixelSolarPanel.ExportState;
        features[features.Count - 1].import = PixelSolarPanel.ImportState;
        features[features.Count - 1].linkAfterPlace = true;

        if (PixelDevTools.Available) Build(PixelDevHub.TabRoot("tester"));   // (in a normal build there is no tab: the Conveyor Belt item starts placing through StartFeature)
        PixelWindows.Register(this, 210, () => placing != null || selecting, StopPlacing);   // Escape stops placing first   // escape-only: stops placing / selecting
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        StopPlacing();
        if (overlayRoot != null) Destroy(overlayRoot);
        if (instance == this) instance = null;
    }

    private void Build(RectTransform root)
    {
        TMP_Text title = PixelUIKit.CreateText(font, root, "Title", "FEATURE TESTER", 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, TextColor);
        PlaceTL(title.rectTransform, 20f, 10f, W - 40f, 44f);
        title.raycastTarget = false;
        TMP_Text sub = PixelUIKit.CreateText(font, root, "Subtitle", "Things that are not in the game yet. Place one, try it, then tell Claude what to change.", 20f, TextAlignmentOptions.TopLeft, FontStyles.Italic, new Color(0.7f, 0.78f, 0.9f, 1f));
        PlaceTL(sub.rectTransform, 20f, 56f, W - 40f, 52f);
        sub.raycastTarget = false;

        ScrollRect scroll = PixelUIKit.CreateScrollView(root, "Features", new Color(0.5f, 0.5f, 0.6f, 1f), 12f, 40f, out RectTransform content, out GameObject bar);
        float top = 112f, bottom = 44f;
        PlaceTL(scroll.GetComponent<RectTransform>(), 10f, top, W - 20f, H - top - bottom);
        float y = 0f;
        foreach (Feature f in features)
        {
            Feature feature = f;
            GameObject card = new GameObject("Card " + f.name, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(content, false);
            PlaceTL(card.GetComponent<RectTransform>(), 0f, y, W - 40f, CardH);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            PixelDevHub.RegisterSearch("Tester: " + f.name, "tester", () => PixelUIKit.ScrollTo(scroll, cardRect), f.description);
            card.GetComponent<Image>().color = BoxColor;
            card.GetComponent<Image>().raycastTarget = false;
            PixelUIKit.StyleBox(card.GetComponent<Image>());

            TMP_Text name = PixelUIKit.CreateText(font, card.transform, "Name", f.name.ToUpperInvariant(), 28f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, TextColor);
            PlaceTL(name.rectTransform, 16f, 8f, W - 72f, 36f);
            name.raycastTarget = false;
            TMP_Text desc = PixelUIKit.CreateText(font, card.transform, "Description", f.description, 19f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.8f, 0.85f, 0.95f, 1f));
            PlaceTL(desc.rectTransform, 16f, 46f, W - 72f, 62f);
            desc.raycastTarget = false;

            Button place = PixelUIKit.CreateButton(font, card.transform, "Place", "Place", new Vector2(200f, 36f), ButtonRole.Primary, 22f);
            PlaceTL(place.GetComponent<RectTransform>(), 16f, 108f, 200f, 36f);
            feature.placeLabel = place.GetComponentInChildren<TMP_Text>();
            place.onClick.AddListener(() => { if (placing == feature) StopPlacing(); else StartPlacing(feature); });
            Button clear = PixelUIKit.CreateButton(font, card.transform, "Clear", "Clear", new Vector2(200f, 36f), ButtonRole.Danger, 22f);
            PlaceTL(clear.GetComponent<RectTransform>(), 226f, 108f, 200f, 36f);
            feature.clearLabel = clear.GetComponentInChildren<TMP_Text>();
            clear.onClick.AddListener(() => Clear(feature));
            y += CardH + 10f;
        }
        y = BuildSelectionCard(content, y);
        PixelUIKit.UpdateScrollView(scroll, bar, y, H - top - bottom);

        statusLabel = PixelUIKit.CreateText(font, root, "Status", "", 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, new Color(0.75f, 0.85f, 1f, 1f));
        PlaceTL(statusLabel.rectTransform, 20f, H - 40f, W - 40f, 34f);
        statusLabel.raycastTarget = false;
        statusLabel.enableAutoSizing = true; statusLabel.fontSizeMax = 20f; statusLabel.fontSizeMin = 11f;
        Refresh();
    }

    private void Refresh()
    {
        foreach (Feature f in features)
        {
            SyncLive(f);
            f.placed.RemoveAll(g => g == null);
            if (f.placeLabel != null) f.placeLabel.text = placing == f ? "Stop placing" : "Place";
            if (f.clearLabel != null) f.clearLabel.text = "Clear (" + f.placed.Count + ")";
        }
        if (statusLabel != null)
            statusLabel.text = linkPanel != null ? "Click a device inside the blue ring to link the solar panel to it (right click skips)."
                : placing == null ? "Pick 'Place' on a card to try it."
                : placing.line ? "Placing " + placing.name + (hasStart ? ": left click the end point (it starts the next piece), right click ends the chain." : ": left click the first point.") + " Right click stops / Esc stops."
                : "Placing " + placing.name + ": left click puts one down, Q / E turn it, right click or Esc stops.";
    }

    private void StartPlacing(Feature f)
    {
        StopPlacing();
        if (clicker == null) return;
        placing = f;
        yaw = 0f;
        placeStartFrame = Time.frameCount;
        ghost = f.ghost(clicker);
        if (ghost != null) ghost.SetActive(false);
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        Refresh();
    }

    private bool gameplayPlacing;   // placing started by the game (StartFeature), not from the Dev Hub card: it does not need the hub open

    private Func<bool> spendOne;   // placing a bought item (a Coupling, a Container Hose): uses one up per piece placed; returns false when none are left

    /// <summary>
    /// Starts placing a feature by name from the game itself (the Conveyor Belt Kit, Coupling and Container Hose items). 'spendOne' (optional) is called after
    /// every piece placed: it uses one item up and says whether another can be placed (false stops placing). Returns false when there is no such feature.
    /// </summary>
    public static bool StartFeature(string featureName, Func<bool> spendOne = null)
    {
        Boot();
        if (instance == null) return false;
        Feature f = instance.features.Find(x => x.name == featureName);
        if (f == null) return false;
        instance.StartPlacing(f);
        instance.gameplayPlacing = true;
        instance.spendOne = spendOne;
        return true;
    }

    /// <summary>A piece was placed: a bought item is used up; when none are left, placing stops. True = placing stopped.</summary>
    private bool AfterPlaced()
    {
        if (spendOne == null || spendOne()) return false;
        PixelHints.Announce("That was the last one - buy more in the shop");
        StopPlacing();
        return true;
    }

    private void StopPlacing()
    {
        gameplayPlacing = false;
        spendOne = null;
        if (ghost != null) Destroy(ghost);
        ghost = null;
        if (previewGhost != null) Destroy(previewGhost);
        previewGhost = null;
        hasStart = false;
        routePoints.Clear();
        stageActive = false;
        lineElev = 0f;
        placing = null;
        EndSelecting();
        if (blocking) { PixelClicker.ExternalClickBlock = false; blocking = false; }
        Refresh();
    }

    /// <summary>A feature whose objects live in the world (couplings) takes its list from there.</summary>
    private static void SyncLive(Feature f)
    {
        if (f.live == null) return;
        f.placed.Clear();
        foreach (GameObject g in f.live()) if (g != null) f.placed.Add(g);
    }

    private void Clear(Feature f)
    {
        SyncLive(f);
        foreach (GameObject g in f.placed) if (g != null) Destroy(g);
        f.placed.Clear();
        Refresh();
    }

    /// <summary>The mouse's point on the flat ground level (belts and towers are measured from it, not from whatever the ray happens to hit).</summary>
    private bool TryGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = PixelClicker.ViewCameraOf(clicker);
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, PixelConveyor.FloorY(clicker), 0f));
        if (!ground.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    private bool TryFloorPoint(out Vector3 point)
    {
        Vector2 pointer = PixelInput.PointerPosition();
        if (consumables != null) return consumables.TryGetFloorPoint(pointer, out point);
        point = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(pointer);
        Plane floor = new Plane(Vector3.up, Vector3.zero);
        if (!floor.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    // ---------------- deleting: hold the right button on a placed tester object ----------------

    private const float DeleteHoldSeconds = 0.8f;
    private GameObject redTarget;   // the tester object that is turning red while it is held

    private void UpdateDelete()
    {
        GameObject target = null;
        if (clicker != null && !PixelFirstPerson.Active && !PixelPauseMenu.IsPaused && (PixelInput.RightPressed() || PixelInput.RightHeld()) && !PixelInput.PointerOverUI())
            target = ObjectUnderPointer();
        bool deleted = PixelHold.Update(this, target, DeleteHoldSeconds, "", new Color(0.9f, 0.3f, 0.3f, 1f), true, "remove_buildup");
        if (redTarget != null && redTarget != target) PixelRemoveTint.Set(redTarget, 0f);   // (the hold moved off it: back to normal)
        redTarget = target;
        if (target != null) PixelRemoveTint.Set(target, deleted ? 1f : PixelHold.ProgressOf(target));   // it turns red as the ring fills
        if (!deleted || target == null) return;
        redTarget = null;
        foreach (Feature f in features) f.placed.Remove(target);
        Destroy(target);   // (a belt's riders are let go by the belt system; a chipper just vanishes)
        PixelAudio.Play("device_remove");
        Refresh();
    }

    /// <summary>The nearest placed tester object under the mouse (by its visible parts); null if the cube or an old pixel is in front of it.</summary>
    private GameObject ObjectUnderPointer()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (Feature f in features)
        {
            SyncLive(f);
            foreach (GameObject go in f.placed)
            {
                if (go == null) continue;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    PixelCoupling part = r.GetComponentInParent<PixelCoupling>();
                    if (part != null && !part.Fitted && part.gameObject != go) continue;   // (a coupling on a machine is its own object: holding it removes the coupling, not the machine)
                    if (r.enabled && r.bounds.IntersectRay(ray, out float d) && d < bestDist) { bestDist = d; best = go; }
                }
            }
        }
        if (best == null) return null;
        foreach (RaycastHit h in Physics.RaycastAll(ray, bestDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return null;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return null;
        }
        return best;
    }

    // ---------------- selecting belts: edit their speed together ----------------

    private bool selecting;
    private bool pressing, rectShown;
    private Vector2 dragStart;
    private readonly HashSet<PixelConveyor> selection = new HashSet<PixelConveyor>();
    private TMP_Text selectLabel, countLabel;
    private TMP_InputField speedField;
    private GameObject overlayRoot;
    private RectTransform rectView;

    private float BuildSelectionCard(RectTransform content, float y)
    {
        const float CardH2 = 214f;
        GameObject card = new GameObject("Card Belt Selection", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(content, false);
        PlaceTL(card.GetComponent<RectTransform>(), 0f, y, W - 40f, CardH2);
        card.GetComponent<Image>().color = BoxColor;
        card.GetComponent<Image>().raycastTarget = false;
        PixelUIKit.StyleBox(card.GetComponent<Image>());

        TMP_Text name = PixelUIKit.CreateText(font, card.transform, "Name", "BELT SELECTION", 28f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, TextColor);
        PlaceTL(name.rectTransform, 16f, 8f, W - 72f, 36f);
        name.raycastTarget = false;
        TMP_Text desc = PixelUIKit.CreateText(font, card.transform, "Description", "Change several belts at once. Press Select belts, then click belts to toggle them or drag a box round a group (they glow teal). Type a speed (cube widths per second) and press Set speed; Reverse turns them all round. Hold the right button on any placed tester object to delete it.", 19f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.8f, 0.85f, 0.95f, 1f));
        PlaceTL(desc.rectTransform, 16f, 46f, W - 72f, 80f);
        desc.raycastTarget = false;

        Button sel = PixelUIKit.CreateButton(font, card.transform, "Select", "Select belts", new Vector2(200f, 36f), ButtonRole.Primary, 22f);
        PlaceTL(sel.GetComponent<RectTransform>(), 16f, 128f, 200f, 36f);
        selectLabel = sel.GetComponentInChildren<TMP_Text>();
        sel.onClick.AddListener(() => { if (selecting) StopPlacing(); else StartSelecting(); });
        Button all = PixelUIKit.CreateButton(font, card.transform, "All", "Select all", new Vector2(200f, 36f), ButtonRole.Neutral, 22f);
        PlaceTL(all.GetComponent<RectTransform>(), 226f, 128f, 200f, 36f);
        all.onClick.AddListener(() => { foreach (PixelConveyor b in PixelConveyor.All) if (b != null && !b.IsGhost) AddToSelection(b); RefreshSelection(); });
        Button none = PixelUIKit.CreateButton(font, card.transform, "None", "Deselect", new Vector2(200f, 36f), ButtonRole.Neutral, 22f);
        PlaceTL(none.GetComponent<RectTransform>(), 436f, 128f, 200f, 36f);
        none.onClick.AddListener(ClearSelection);

        speedField = PixelUIKit.CreateInputField(font, card.transform, "Speed", new Vector2(120f, 36f), new Color(0.05f, 0.06f, 0.09f, 1f), Color.white, 22f, "1.6");
        PlaceTL(speedField.GetComponent<RectTransform>(), 16f, 172f, 120f, 36f);
        speedField.contentType = TMP_InputField.ContentType.DecimalNumber;
        Button set = PixelUIKit.CreateButton(font, card.transform, "Set", "Set speed", new Vector2(150f, 36f), ButtonRole.Success, 22f);
        PlaceTL(set.GetComponent<RectTransform>(), 146f, 172f, 150f, 36f);
        set.onClick.AddListener(ApplySpeedToSelection);
        Button rev = PixelUIKit.CreateButton(font, card.transform, "Reverse", "Reverse", new Vector2(130f, 36f), ButtonRole.Neutral, 22f);
        PlaceTL(rev.GetComponent<RectTransform>(), 306f, 172f, 130f, 36f);
        rev.onClick.AddListener(() => { foreach (PixelConveyor b in selection) if (b != null) b.Flip(); });
        countLabel = PixelUIKit.CreateText(font, card.transform, "Count", "0 selected", 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, new Color(0.75f, 0.85f, 1f, 1f));
        PlaceTL(countLabel.rectTransform, 446f, 172f, W - 500f, 36f);
        countLabel.raycastTarget = false;
        return y + CardH2 + 10f;
    }

    private void StartSelecting()
    {
        StopPlacing();
        selecting = true;
        pressing = false; rectShown = false;
        placeStartFrame = Time.frameCount;
        if (selectLabel != null) selectLabel.text = "Stop selecting";
        if (statusLabel != null) statusLabel.text = "Click belts to toggle them, or drag a box round several. Right click or Esc stops.";
    }

    private void EndSelecting()
    {
        if (!selecting) return;
        selecting = false; pressing = false;
        HideRect();
        if (selectLabel != null) selectLabel.text = "Select belts";
    }

    private void AddToSelection(PixelConveyor belt) { if (selection.Add(belt)) belt.SetSelected(true); }

    private void ClearSelection()
    {
        foreach (PixelConveyor b in selection) if (b != null) b.SetSelected(false);
        selection.Clear();
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        selection.RemoveWhere(b => b == null);
        if (countLabel != null) countLabel.text = selection.Count + " selected";
        if (speedField != null && selection.Count > 0 && !speedField.isFocused)
        {
            foreach (PixelConveyor b in selection) { speedField.text = b.SpeedCubes.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); break; }
        }
    }

    private void ApplySpeedToSelection()
    {
        if (speedField == null || !float.TryParse(speedField.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return;
        foreach (PixelConveyor b in selection) if (b != null) b.SetSpeedCubes(v);
        PixelAudio.Play("ui_click");
    }

    private void UpdateSelect()
    {
        PixelClicker.ExternalClickBlock = true;
        blocking = true;
        if (!PixelDevHub.IsOpen && !gameplayPlacing) { StopPlacing(); return; }
        Vector2 m = PixelInput.PointerPosition();
        if (Time.frameCount != placeStartFrame)
        {
            if (!pressing && PixelInput.LeftPressed() && !PixelInput.PointerOverUI()) { pressing = true; dragStart = m; }
            if (pressing)
            {
                if (PixelInput.LeftHeld())
                {
                    if ((m - dragStart).sqrMagnitude > 64f) ShowRect(dragStart, m);
                }
                else
                {
                    pressing = false;
                    if (rectShown) { SelectInRect(dragStart, m); HideRect(); }
                    else ToggleUnderPointer();
                    RefreshSelection();
                }
            }
            else if (PixelInput.RightPressed()) { StopPlacing(); return; }
        }
    }

    private void ToggleUnderPointer()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null) return;
        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(PixelInput.PointerPosition()), 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return;
            PixelConveyor belt = h.collider.GetComponentInParent<PixelConveyor>();
            if (belt == null) continue;
            if (selection.Contains(belt)) { selection.Remove(belt); belt.SetSelected(false); }
            else AddToSelection(belt);
            return;
        }
    }

    private void SelectInRect(Vector2 a, Vector2 b)
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null) return;
        Rect r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            foreach (Vector3 w in new[] { belt.transform.position, belt.EndA, belt.EndB })
            {
                Vector3 sp = cam.WorldToScreenPoint(w);
                if (sp.z > 0f && r.Contains(new Vector2(sp.x, sp.y))) { AddToSelection(belt); break; }
            }
        }
    }

    private void ShowRect(Vector2 a, Vector2 b)
    {
        if (overlayRoot == null)
        {
            overlayRoot = PixelUIKit.CreateCanvas("Tester Selection Overlay", 400, new Vector2(1920f, 1080f), false, false);   // (pixel units: no scaling)
            GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(overlayRoot.transform, false);
            box.GetComponent<Image>().color = new Color(0.25f, 0.9f, 1f, 0.22f);
            box.GetComponent<Image>().raycastTarget = false;
            rectView = box.GetComponent<RectTransform>();
            rectView.anchorMin = rectView.anchorMax = rectView.pivot = Vector2.zero;
        }
        overlayRoot.SetActive(true);
        rectShown = true;
        rectView.anchoredPosition = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        rectView.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void HideRect()
    {
        rectShown = false;
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    // ---------------- saving with the save file ----------------

    [Serializable]
    public class TesterSave { public string feature; public string json; public string couplings; }

    private static TesterSave[] kept;   // what was loaded while the tester was not running (kept as it was, so a save made then does not lose it)

    /// <summary>Every placed test object (chipper, belts and their riders, kiln, solar panels) as JSON for the save file.</summary>
    public static TesterSave[] ExportAll()
    {
        if (instance == null) return kept;
        List<TesterSave> list = new List<TesterSave>();
        foreach (Feature f in instance.features)
        {
            if (f.export == null) continue;
            foreach (GameObject go in f.placed)
            {
                if (go == null) continue;
                string json = f.export(go);
                if (!string.IsNullOrEmpty(json)) list.Add(new TesterSave { feature = f.name, json = json, couplings = PixelCouplings.Export(go) });   // (its couplings go with it)
            }
        }
        return list.ToArray();
    }

    /// <summary>Removes EVERY object the tester placed (belts, couplings, hoses, kiln, jumps...): the Dev Hub's 'Clear world' widget. Returns how many went.</summary>
    public static int ClearAllPlaced()
    {
        if (instance == null) return 0;
        instance.StopPlacing();
        int n = 0;
        foreach (Feature f in instance.features)
        {
            SyncLive(f);
            foreach (GameObject g in f.placed) if (g != null) { Destroy(g); n++; }
            f.placed.Clear();
        }
        instance.Refresh();
        return n;
    }

    /// <summary>Loading a save: the placed test objects are replaced by the saved ones.</summary>
    public static void ImportAll(TesterSave[] saved)
    {
        kept = saved;
        if (instance == null && saved != null && saved.Length > 0) Boot();
        if (instance == null) return;
        instance.StopPlacing();
        foreach (Feature f in instance.features) { if (f.live == null) foreach (GameObject g in f.placed) if (g != null) Destroy(g); f.placed.Clear(); }   // (couplings go with their machines)
        if (saved != null && instance.clicker != null)
            foreach (TesterSave t in saved)
            {
                if (t == null || string.IsNullOrEmpty(t.json)) continue;
                Feature f = instance.features.Find(x => x.name == t.feature);
                if (f == null || f.import == null) { Debug.LogWarning("PixelFeatureTester: a saved '" + t.feature + "' was not restored - no tester feature by that name can load it."); continue; }
                GameObject go = null;
                try { go = f.import(instance.clicker, t.json); } catch (Exception e) { Debug.LogWarning("PixelFeatureTester: could not restore a " + t.feature + " - " + e.Message + "\n" + e.StackTrace); }
                if (go != null) { f.placed.Add(go); PixelCouplings.Import(go, t.couplings); }
                else Debug.LogWarning("PixelFeatureTester: a saved " + t.feature + " came back empty and was dropped (" + t.json + ").");
            }
        instance.Refresh();
    }

    // ---------------- linking (the Solar Panel) ----------------

    private GameObject linkPanel;

    /// <summary>Starts linking a placed panel to a device: click a device inside the ring (called by the panel on a right click).</summary>
    public static void BeginLink(GameObject panel)
    {
        if (instance == null) return;
        instance.StopPlacing();
        instance.linkPanel = panel;
        instance.placeStartFrame = Time.frameCount;
        panel.GetComponent<PixelSolarPanel>()?.ShowRange(true);
        instance.linkOnly = true;
        PixelClicker.ExternalClickBlock = true; instance.blocking = true;
        instance.Refresh();
    }

    private bool linkOnly;   // linking a placed panel without placing anything

    private void EndLink(bool keepPlacing)
    {
        if (linkPanel != null) linkPanel.GetComponent<PixelSolarPanel>()?.ShowRange(false);
        linkPanel = null;
        if (linkOnly) { linkOnly = false; if (blocking) { PixelClicker.ExternalClickBlock = false; blocking = false; } }
        Refresh();
    }

    private void UpdateLink()
    {
        PixelClicker.ExternalClickBlock = true;
        blocking = true;
        if (linkPanel == null) { EndLink(true); return; }
        if (ghost != null) ghost.SetActive(false);
        if (Time.frameCount == placeStartFrame) return;
        Camera cam = clicker.ViewCamera;
        if (cam == null) return;
        if (PixelInput.LeftPressed() && !PixelInput.PointerOverUI())
        {
            PixelPlacedDevice device = PixelSolarPanel.DeviceUnderRay(linkPanel, cam.ScreenPointToRay(PixelInput.PointerPosition()));
            if (device != null)
            {
                linkPanel.GetComponent<PixelSolarPanel>().LinkTo(device);
                EndLink(true);
            }
        }
        else if (PixelInput.RightPressed()) EndLink(true);
    }

    private float lineElev;          // how high above the ground the belt point being placed is (scroll wheel)
    private string lastElevText;

    /// <summary>Two-point placing: marker at the mouse until the first click, then the belt stretches to the mouse; every further click builds that piece and starts the next one. The scroll wheel raises / lowers the point.</summary>
    private void UpdateLine(bool hasPoint, Vector3 raw)
    {
        float unit = clicker.OldPixelWorldSize;
        float wheel = PixelInput.ScrollY();
        if (Mathf.Abs(wheel) > 0.01f && !PixelInput.ControlHeld()) lineElev = Mathf.Clamp(lineElev + Mathf.Sign(wheel) * 0.5f * unit, 0f, 30f * unit);
        float groundY = raw.y;
        Vector3 elevated = new Vector3(raw.x, groundY + lineElev, raw.z);
        bool snapped = false;
        Vector3 p = hasPoint ? placing.snap(clicker, elevated, out snapped) : elevated;
        if (placing.route) { UpdateRoute(hasPoint, p, snapped, groundY, unit); return; }
        if (hasStart) p = PixelConveyor.ClampEnd(lineStart, p);
        string problem = hasStart && hasPoint && placing.lineCheck != null ? placing.lineCheck(lineStart, p) : null;
        string text = problem != null ? problem + " - pick a gentler angle" : "Scroll: raise / lower the belt supports. Height " + (lineElev / unit).ToString("0.0") + " cubes" + (snapped ? "  (snapped to an end)" : "");
        if (statusLabel != null && text != lastElevText) { statusLabel.text = text; lastElevText = text; }
        if (!hasStart)
        {
            if (ghost != null) { ghost.SetActive(hasPoint); if (hasPoint) ghost.transform.position = p; }
        }
        else
        {
            if (previewGhost != null && hasPoint && problem == null) { previewGhost.SetActive(true); placing.updatePreview(previewGhost, lineStart, p); }
            else if (previewGhost != null) previewGhost.SetActive(false);   // (too sharp a turn: no preview, it can't be placed)
        }
        if (Time.frameCount == placeStartFrame) return;
        if (hasPoint && PixelInput.LeftPressed())
        {
            if (snapped) lineElev = Mathf.Max(0f, p.y - groundY);   // (carry on at that end's height)
            if (!hasStart)
            {
                hasStart = true;
                lineStart = p;
                if (ghost != null) ghost.SetActive(false);
                if (previewGhost == null) previewGhost = placing.preview(clicker);
                Refresh();
            }
            else if (problem != null) { PixelHints.Announce(problem); PixelAudio.Play("ui_click"); }
            else
            {
                GameObject go = placing.placeLine(clicker, lineStart, p);
                if (go != null) { placing.placed.Add(go); lineStart = p; }
                Refresh();
            }
        }
        else if (PixelInput.RightPressed())
        {
            if (hasStart)
            {
                hasStart = false;                                   // ends the chain, the first point can be chosen again
                if (previewGhost != null) Destroy(previewGhost);
                previewGhost = null;
                Refresh();
            }
            else StopPlacing();
        }
    }

    // ---------------- routed placing (the Container Hose) ----------------

    private readonly List<Vector3> routePoints = new List<Vector3>();
    private readonly List<Vector3> routeScratch = new List<Vector3>();

    /// <summary>
    /// A routed piece: the first click starts it, every click after that adds a bend point, and a click that SNAPS (onto a coupling) finishes it - one
    /// piece through all the points. Right click takes the last point back (with none left it stops placing). Scroll raises / lowers the point.
    /// </summary>
    private void UpdateRoute(bool hasPoint, Vector3 p, bool snapped, float groundY, float unit)
    {
        string text = (routePoints.Count == 0 ? "Click the start (an Out coupling)." : "Click bend points, then an In coupling to finish. Right click: take the last point back.")
                      + "  Scroll: height " + (lineElev / unit).ToString("0.0") + " cubes" + (snapped ? "  (on a coupling)" : "");
        if (statusLabel != null && text != lastElevText) { statusLabel.text = text; lastElevText = text; }
        if (routePoints.Count == 0)
        {
            if (ghost != null) { ghost.SetActive(hasPoint); if (hasPoint) ghost.transform.position = p; }
        }
        else if (previewGhost != null)
        {
            routeScratch.Clear();
            routeScratch.AddRange(routePoints);
            if (hasPoint) routeScratch.Add(p);
            placing.updateRoute(previewGhost, routeScratch);
        }
        if (Time.frameCount == placeStartFrame) return;
        if (hasPoint && PixelInput.LeftPressed())
        {
            if (snapped) lineElev = Mathf.Max(0f, p.y - groundY);
            routePoints.Add(p);
            if (routePoints.Count == 1)
            {
                hasStart = true;
                if (ghost != null) ghost.SetActive(false);
                if (previewGhost == null) previewGhost = placing.preview(clicker);
            }
            else if (snapped)
            {
                GameObject go = placing.placeRoute(clicker, routePoints);
                if (go != null) placing.placed.Add(go);
                EndRoute();
                if (go != null && AfterPlaced()) return;
            }
            Refresh();
        }
        else if (PixelInput.RightPressed())
        {
            if (routePoints.Count > 0) { routePoints.RemoveAt(routePoints.Count - 1); if (routePoints.Count == 0) EndRoute(); Refresh(); }
            else StopPlacing();
        }
    }

    private void EndRoute()
    {
        routePoints.Clear();
        hasStart = false;
        if (previewGhost != null) Destroy(previewGhost);
        previewGhost = null;
    }

    // ---------------- two-stage tall placing (the Conveyor Elevator) ----------------

    private bool stageActive;
    private Vector3 stagePoint;
    private float stageYaw;

    private void UpdateHeightStage(bool hasPoint, Vector3 point, float useYaw, bool snapped)
    {
        if (!stageActive)
        {
            if (PixelKeys.Held(PixelAction.RotateLeft)) yaw -= 120f * Time.unscaledDeltaTime;
            if (PixelKeys.Held(PixelAction.RotateRight)) yaw += 120f * Time.unscaledDeltaTime;
            if (placing.requireSnap && ghost != null) ghost.SetActive(hasPoint && snapped);   // (only shown where it can go)
            if (placing.requireSnap && statusLabel != null)
            {
                string t = snapped ? "Click to put it on this belt's end." : "Bring it to the END of a conveyor belt - it snaps on there.";
                if (t != lastElevText) { statusLabel.text = t; lastElevText = t; }
            }
            if (Time.frameCount == placeStartFrame) return;
            if (hasPoint && PixelInput.LeftPressed() && (!placing.requireSnap || snapped)) { stageActive = true; stagePoint = point; stageYaw = useYaw; Refresh(); }
            else if (PixelInput.RightPressed()) StopPlacing();
            return;
        }
        if (placing.scrollStage) { UpdateScrollStage(); return; }
        // Stage 2: the base stays; the mouse sets the height (its ray cut with a vertical plane through the base, facing the camera).
        if (ghost != null)
        {
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(stagePoint, Quaternion.Euler(0f, stageYaw, 0f));
        }
        Camera cam = clicker.ViewCamera;
        if (cam != null && ghost != null && !PixelInput.PointerOverUI())
        {
            Vector3 toCam = cam.transform.position - stagePoint; toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) toCam = -cam.transform.forward;
            Plane plane = new Plane(toCam.normalized, stagePoint);
            Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
            if (plane.Raycast(ray, out float enter))
            {
                float step = PixelElevator.StepWorld(ghost);
                float h = Mathf.Round((ray.GetPoint(enter).y - stagePoint.y) / step) * step;
                h = Mathf.Clamp(h, PixelElevator.MinWorld(ghost), PixelElevator.MaxWorld(ghost));
                PixelElevator.SetHeight(ghost, h);
                string text = "Move the mouse up / down to set the height (" + (h / clicker.OldPixelWorldSize).ToString("0.0") + " cubes), click to confirm, right click to go back.";
                if (statusLabel != null && text != lastElevText) { statusLabel.text = text; lastElevText = text; }
            }
        }
        if (Time.frameCount == placeStartFrame) return;
        if (PixelInput.LeftPressed() && !PixelInput.PointerOverUI() && ghost != null)
        {
            GameObject go = placing.placeTall(clicker, stagePoint, stageYaw, ghost.GetComponent<PixelElevator>().Height);
            if (go != null) placing.placed.Add(go);
            stageActive = false;
            Refresh();
        }
        else if (PixelInput.RightPressed()) { stageActive = false; Refresh(); }
    }

    /// <summary>Stage 2 of a scroll-stage feature (the Belt Jump): it stays where it snapped; the scroll wheel sets its value (the gap); click confirms.</summary>
    private void UpdateScrollStage()
    {
        if (ghost != null)
        {
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(stagePoint, Quaternion.Euler(0f, stageYaw, 0f));
            float wheel = PixelInput.ScrollY();
            if (Mathf.Abs(wheel) > 0.01f && placing.setStageValue != null && placing.stageValue != null)
            {
                float step = placing.stageStep != null ? placing.stageStep(ghost) : 0.5f;
                float v = placing.stageValue(ghost) + Mathf.Sign(wheel) * step;
                if (placing.stageMin != null) v = Mathf.Max(v, placing.stageMin(ghost));
                if (placing.stageMax != null) v = Mathf.Min(v, placing.stageMax(ghost));
                placing.setStageValue(ghost, v);
            }
            float shown = placing.stageValue != null ? placing.stageValue(ghost) : 0f;
            string text = "Scroll to set the " + placing.stageLabel + " (" + (shown / clicker.OldPixelWorldSize).ToString("0.0") + " cubes), click to confirm, right click to go back.";
            if (statusLabel != null && text != lastElevText) { statusLabel.text = text; lastElevText = text; }
        }
        if (Time.frameCount == placeStartFrame) return;
        if (PixelInput.LeftPressed() && !PixelInput.PointerOverUI() && ghost != null)
        {
            GameObject go = placing.placeTall(clicker, stagePoint, stageYaw, placing.stageValue != null ? placing.stageValue(ghost) : 0f);
            if (go != null) placing.placed.Add(go);
            stageActive = false;
            if (go != null && AfterPlaced()) return;   // (a Belt Jump bought in the shop: one is used up per jump)
            Refresh();
        }
        else if (PixelInput.RightPressed()) { stageActive = false; Refresh(); }
    }

    private void Update()
    {
        if (linkPanel != null) { UpdateLink(); return; }
        if (selecting) { UpdateSelect(); return; }
        if (placing == null) { UpdateDelete(); return; }
        if (!PixelDevHub.IsOpen && !gameplayPlacing) { StopPlacing(); return; }   // (placing started from a shop item - Coupling, Container Hose, Conveyor Belt Kit - does not need the hub open)
        PixelClicker.ExternalClickBlock = true;
        blocking = true;

        bool overUi = PixelInput.PointerOverUI();
        Vector3 point = Vector3.zero;
        bool hasPoint = !overUi && ((placing.line || placing.heightStage || placing.groundPlane) ? TryGroundPoint(out point) : TryFloorPoint(out point));
        if (hasPoint && PixelSnap.Active) point = PixelSnap.Point(point, clicker.OldPixelWorldSize);   // Shift: snap to the grid (a latch onto a belt end still wins)
        float useYaw = PixelSnap.Active ? PixelSnap.Yaw(yaw) : yaw;   // Shift: snap the turn to angle steps
        bool snappedNow = false;
        if (hasPoint && placing.placeSnap != null && placing.placeSnap(clicker, point, useYaw, ghost, out Vector3 snapP, out float snapYaw)) { point = snapP; useYaw = snapYaw; snappedNow = true; }
        if (ghost != null && !(placing.line && hasStart) && !(placing.heightStage && stageActive))
        {
            ghost.SetActive(hasPoint);
            if (hasPoint) ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, useYaw, 0f));
        }
        if (placing.line) { UpdateLine(hasPoint, point); return; }
        if (placing.heightStage) { UpdateHeightStage(hasPoint, point, useYaw, snappedNow); return; }
        if (PixelKeys.Held(PixelAction.RotateLeft)) yaw -= 120f * Time.unscaledDeltaTime;
        if (PixelKeys.Held(PixelAction.RotateRight)) yaw += 120f * Time.unscaledDeltaTime;

        if (Time.frameCount == placeStartFrame) return;
        if (hasPoint && PixelInput.LeftPressed())
        {
            GameObject go = placing.place(clicker, point, useYaw);
            if (go != null) placing.placed.Add(go);
            if (go != null && AfterPlaced()) return;
            if (go != null && placing.linkAfterPlace) { linkPanel = go; linkOnly = false; placeStartFrame = Time.frameCount; go.GetComponent<PixelSolarPanel>()?.ShowRange(true); }
            Refresh();
        }
        else if (PixelInput.RightPressed()) StopPlacing();
    }
}
