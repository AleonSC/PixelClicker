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
        public string name, description;
        public Func<PixelClicker, GameObject> ghost;
        public Func<PixelClicker, Vector3, float, GameObject> place;
        // Two-point features (a conveyor belt): click a start, click an end, repeat (the end starts the next piece).
        public bool line;
        public bool singleLine;       // a line feature that is ONE piece (the Conveyor Connector): after the second click it starts over instead of chaining
        public Func<PixelClicker, GameObject> preview;
        public Func<PixelClicker, Vector3, Vector3, GameObject> placeLine;
        public Action<GameObject, Vector3, Vector3> updatePreview;
        public SnapFunc snap;
        // Tall features (the Conveyor Elevator): after the base is clicked the mouse sets the HEIGHT, a second click confirms.
        public bool heightStage;
        public bool groundPlane;      // placed on the flat ground level, not on whatever the mouse ray hits (belts, towers, the Splitter)
        public Func<PixelClicker, Vector3, float, float, GameObject> placeTall;
        public readonly List<GameObject> placed = new List<GameObject>();
        public TMP_Text placeLabel, clearLabel;
    }

    private static PixelFeatureTester instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

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

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();

        Register("Wood Chipper",
                 "A detailed trailer-mounted wood chipper. Wood pixels dropped into its hopper (or lying close above it) are shredded for Woodcutting XP. Anything else damages it, and enough of that breaks it for good - Clear it and place a new one. Drag pixels in with Pixel Grabbing. While placing, an arrow shows where its chips shoot out; if a conveyor belt's opening is in the arrow's area the chipper latches onto it and its Wood Chips drop straight onto the belt.",
                 c => PixelWoodChipper.CreateGhost(c), (c, p, yw) => PixelWoodChipper.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelWoodChipper.TrySnapToBelt;
        features[features.Count - 1].export = PixelWoodChipper.ExportState;
        features[features.Count - 1].import = PixelWoodChipper.ImportState;

        RegisterLine("Conveyor Belt",
                     "Click a point, then another: a belt is built between them (the second point starts the next piece, so short pieces make a curve; right click ends the chain, right click again stops). Clicks near a belt's end (or an elevator's top) snap to it, at its height. SCROLL the mouse wheel to raise or lower the point you are placing: the belt's supports grow and the belt slopes, so ramps and bridges are possible. Right click a placed belt to reverse it. Anything standing on a belt is carried along - drop things on it with Pixel Grabbing, or put it under a machine's outlet.",
                     c => PixelConveyor.CreateMarker(c), c => PixelConveyor.CreatePreview(c), (g, a, b) => PixelConveyor.UpdatePreview(g, a, b),
                     (c, a, b) => PixelConveyor.Place(c, a, b), PixelConveyor.Snap);
        features[features.Count - 1].export = PixelConveyor.ExportState;
        features[features.Count - 1].import = PixelConveyor.ImportState;
        RegisterLine("Conveyor Connector",
                     "A boxed channel for gaps in turns or for joining a belt to a machine. Click one end, then the other: it magnetizes to belt ends, elevator tops, splitter outputs, the Kiln's mouth, the Storage Crate and other connectors, and a box forms between the two (scroll raises / lowers the point). Any old pixel inside the box - handed in by a belt or dropped / spat into it - is forced along it and put onto the belt that starts at the far end or into the machine there.",
                     c => PixelConveyor.CreateMarker(c), c => PixelConnector.CreatePreview(c), (g, a, b) => PixelConnector.UpdatePreview(g, a, b),
                     (c, a, b) => PixelConnector.Place(c, a, b), PixelConnector.Snap);
        features[features.Count - 1].singleLine = true;
        features[features.Count - 1].export = PixelConnector.ExportState;
        features[features.Count - 1].import = PixelConnector.ImportState;
        RegisterLine("Container Hose",
                     "A see-through hose from one container to another. Click the SOURCE - a Storage Crate or a Quantum Organizer - then the TARGET: a Recycler, another Storage Crate, a Quantum Organizer or a Trash Can (both clicks magnetize to the nearest container's top). It pumps the source's stored pixels through the hose one by one and drops them into the target, only as many as the target has room for. The ends follow the containers when you carry them.",
                     c => PixelConveyor.CreateMarker(c), c => PixelContainerHose.CreatePreview(c), (g, a, b) => PixelContainerHose.UpdatePreview(g, a, b),
                     (c, a, b) => PixelContainerHose.Place(c, a, b), PixelContainerHose.Snap);
        features[features.Count - 1].singleLine = true;
        features[features.Count - 1].export = PixelContainerHose.ExportState;
        features[features.Count - 1].import = PixelContainerHose.ImportState;
        Register("Hose Adapter",
                 "A long open trough that snaps onto the end of a working Funnel's spout (bring the ghost near the spout) and carries what comes out of the hose. Start a conveyor belt at one of its two sides (the belt's first point snaps to the teal ports) or at its far end: the adapter hands the pixels to the belt that starts there, so the hose-to-belt connection is always clean.",
                 c => PixelConnector.CreateAdapterGhost(c), (c, p, yw) => PixelConnector.PlaceAdapter(c, p, yw));
        features[features.Count - 1].placeSnap = PixelConnector.TrySnapAdapter;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelConnector.ExportState;
        features[features.Count - 1].import = PixelConnector.ImportState;
        Register("Vacuum Cleaner",
                 "A big canister vacuum on wheels (to become a consumable). Left-click its body to drive it: W A S D or the arrow keys move it (diagonals too), relative to the camera. Husks that come near its wide front intake are sucked in and automatically spat back out of its hose - the mouse aims the hose, and they land where it points. Click the body again or press Escape to stop driving.",
                 c => PixelVacuumCleaner.CreateGhost(c), (c, p, yw) => PixelVacuumCleaner.Place(c, p, yw));
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelVacuumCleaner.ExportState;
        features[features.Count - 1].import = PixelVacuumCleaner.ImportState;
        Register("Trash Can",
                 "A plain cylinder that deletes things. Put it on the floor: every loose old pixel within a small radius (the teal ring) - thrown out of a Funnel hose, a machine or the end of a conveyor belt - is pulled to its mouth and vanishes, no credit. Belts and connectors that end at it feed it directly.",
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
        Register("Tiny Robot",
                 "A little hover robot (to become an Electrician consumable). Pick a SPOT ON A CONVEYOR BELT (the ghost snaps to the belt under the mouse; a click off the belts does nothing). For a minute it keeps fetching the nearest loose old pixel to that spot, flies it over and puts it down on the belt, where the belt carries it away. Pixels you grab are left alone.",
                 c => PixelTinyRobot.CreateGhost(c), (c, p, yw) => PixelTinyRobot.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelTinyRobot.TrySnap;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelTinyRobot.ExportState;
        features[features.Count - 1].import = PixelTinyRobot.ImportState;
        Register("Conveyor Elevator",
                 "A tower that lifts pixels from the end of a belt to a higher belt. Bring the ghost near the end of a belt and it magnetizes to it (turned the belt's way). Click to fix the base, then move the mouse up or down to set the height and click again to confirm (right click goes back a step). Place a new belt starting at the top: its first point snaps to the tower's top. Pixels from the belt are lifted and come out of the top.",
                 c => PixelElevator.CreateGhost(c), (c, p, yw) => PixelElevator.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelElevator.TrySnap;
        features[features.Count - 1].heightStage = true;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].placeTall = PixelElevator.PlaceTall;
        features[features.Count - 1].export = PixelElevator.ExportState;
        features[features.Count - 1].import = PixelElevator.ImportState;
        Register("Splitter",
                 "A block that snaps to the end of a conveyor belt (bring the ghost near the end) and shares what the belt brings out of its other three sides, one pixel at a time in turn. Start new belts from the three outputs (their first point snaps there); a side without a belt is skipped, and with no belts at all the pixels are flung out of all three sides.",
                 c => PixelSplitter.CreateGhost(c), (c, p, yw) => PixelSplitter.Place(c, p, yw));
        features[features.Count - 1].placeSnap = PixelSplitter.TrySnap;
        features[features.Count - 1].groundPlane = true;
        features[features.Count - 1].export = PixelSplitter.ExportState;
        features[features.Count - 1].import = PixelSplitter.ImportState;
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
        PixelWindows.Register(this, 210, () => placing != null || selecting, StopPlacing);   // Escape stops placing first
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

            Button place = PixelUIKit.CreateButton(font, card.transform, "Place", "Place", new Vector2(200f, 36f), new Color(0.2f, 0.6f, 0.3f, 1f), TextColor, 22f);
            PlaceTL(place.GetComponent<RectTransform>(), 16f, 108f, 200f, 36f);
            feature.placeLabel = place.GetComponentInChildren<TMP_Text>();
            place.onClick.AddListener(() => { if (placing == feature) StopPlacing(); else StartPlacing(feature); });
            Button clear = PixelUIKit.CreateButton(font, card.transform, "Clear", "Clear", new Vector2(200f, 36f), new Color(0.55f, 0.22f, 0.2f, 1f), TextColor, 22f);
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

    /// <summary>Starts placing a feature by name from the game itself (the Conveyor Belt item). Returns false when there is no such feature.</summary>
    public static bool StartFeature(string featureName)
    {
        Boot();
        if (instance == null) return false;
        Feature f = instance.features.Find(x => x.name == featureName);
        if (f == null) return false;
        instance.StartPlacing(f);
        instance.gameplayPlacing = true;
        return true;
    }

    private void StopPlacing()
    {
        gameplayPlacing = false;
        if (ghost != null) Destroy(ghost);
        ghost = null;
        if (previewGhost != null) Destroy(previewGhost);
        previewGhost = null;
        hasStart = false;
        stageActive = false;
        lineElev = 0f;
        placing = null;
        EndSelecting();
        if (blocking) { PixelClicker.ExternalClickBlock = false; blocking = false; }
        Refresh();
    }

    private void Clear(Feature f)
    {
        foreach (GameObject g in f.placed) if (g != null) Destroy(g);
        f.placed.Clear();
        Refresh();
    }

    /// <summary>The mouse's point on the flat ground level (belts and towers are measured from it, not from whatever the ray happens to hit).</summary>
    private bool TryGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (Feature f in features)
            foreach (GameObject go in f.placed)
            {
                if (go == null) continue;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.bounds.IntersectRay(ray, out float d) && d < bestDist) { bestDist = d; best = go; }
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

        Button sel = PixelUIKit.CreateButton(font, card.transform, "Select", "Select belts", new Vector2(200f, 36f), new Color(0.2f, 0.45f, 0.65f, 1f), TextColor, 22f);
        PlaceTL(sel.GetComponent<RectTransform>(), 16f, 128f, 200f, 36f);
        selectLabel = sel.GetComponentInChildren<TMP_Text>();
        sel.onClick.AddListener(() => { if (selecting) StopPlacing(); else StartSelecting(); });
        Button all = PixelUIKit.CreateButton(font, card.transform, "All", "Select all", new Vector2(200f, 36f), new Color(0.3f, 0.36f, 0.5f, 1f), TextColor, 22f);
        PlaceTL(all.GetComponent<RectTransform>(), 226f, 128f, 200f, 36f);
        all.onClick.AddListener(() => { foreach (PixelConveyor b in PixelConveyor.All) if (b != null && !b.IsGhost) AddToSelection(b); RefreshSelection(); });
        Button none = PixelUIKit.CreateButton(font, card.transform, "None", "Deselect", new Vector2(200f, 36f), new Color(0.3f, 0.36f, 0.5f, 1f), TextColor, 22f);
        PlaceTL(none.GetComponent<RectTransform>(), 436f, 128f, 200f, 36f);
        none.onClick.AddListener(ClearSelection);

        speedField = PixelUIKit.CreateInputField(font, card.transform, "Speed", new Vector2(120f, 36f), new Color(0.05f, 0.06f, 0.09f, 1f), Color.white, 22f, "1.6");
        PlaceTL(speedField.GetComponent<RectTransform>(), 16f, 172f, 120f, 36f);
        speedField.contentType = TMP_InputField.ContentType.DecimalNumber;
        Button set = PixelUIKit.CreateButton(font, card.transform, "Set", "Set speed", new Vector2(150f, 36f), new Color(0.2f, 0.6f, 0.3f, 1f), TextColor, 22f);
        PlaceTL(set.GetComponent<RectTransform>(), 146f, 172f, 150f, 36f);
        set.onClick.AddListener(ApplySpeedToSelection);
        Button rev = PixelUIKit.CreateButton(font, card.transform, "Reverse", "Reverse", new Vector2(130f, 36f), new Color(0.55f, 0.4f, 0.15f, 1f), TextColor, 22f);
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
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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
    public class TesterSave { public string feature; public string json; }

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
                if (!string.IsNullOrEmpty(json)) list.Add(new TesterSave { feature = f.name, json = json });
            }
        }
        return list.ToArray();
    }

    /// <summary>Loading a save: the placed test objects are replaced by the saved ones.</summary>
    public static void ImportAll(TesterSave[] saved)
    {
        kept = saved;
        if (instance == null && saved != null && saved.Length > 0) Boot();
        if (instance == null) return;
        instance.StopPlacing();
        foreach (Feature f in instance.features) { foreach (GameObject g in f.placed) if (g != null) Destroy(g); f.placed.Clear(); }
        if (saved != null && instance.clicker != null)
            foreach (TesterSave t in saved)
            {
                if (t == null || string.IsNullOrEmpty(t.json)) continue;
                Feature f = instance.features.Find(x => x.name == t.feature);
                if (f == null || f.import == null) continue;
                GameObject go = null;
                try { go = f.import(instance.clicker, t.json); } catch (Exception e) { Debug.LogWarning("PixelFeatureTester: could not restore a " + t.feature + " - " + e.Message); }
                if (go != null) f.placed.Add(go);
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
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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
        if (hasStart) p = PixelConveyor.ClampEnd(lineStart, p);
        string text = "Scroll: raise / lower the belt supports. Height " + (lineElev / unit).ToString("0.0") + " cubes" + (snapped ? "  (snapped to an end)" : "");
        if (statusLabel != null && text != lastElevText) { statusLabel.text = text; lastElevText = text; }
        if (!hasStart)
        {
            if (ghost != null) { ghost.SetActive(hasPoint); if (hasPoint) ghost.transform.position = p; }
        }
        else
        {
            if (previewGhost != null && hasPoint) placing.updatePreview(previewGhost, lineStart, p);
            else if (previewGhost != null) previewGhost.SetActive(false);
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
            else
            {
                GameObject go = placing.placeLine(clicker, lineStart, p);
                if (go != null) { placing.placed.Add(go); lineStart = p; }
                if (placing.singleLine)
                {
                    hasStart = false;
                    if (previewGhost != null) Destroy(previewGhost);
                    previewGhost = null;
                }
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

    // ---------------- two-stage tall placing (the Conveyor Elevator) ----------------

    private bool stageActive;
    private Vector3 stagePoint;
    private float stageYaw;

    private void UpdateHeightStage(bool hasPoint, Vector3 point, float useYaw)
    {
        if (!stageActive)
        {
            if (PixelKeys.Held(PixelAction.RotateLeft)) yaw -= 120f * Time.unscaledDeltaTime;
            if (PixelKeys.Held(PixelAction.RotateRight)) yaw += 120f * Time.unscaledDeltaTime;
            if (Time.frameCount == placeStartFrame) return;
            if (hasPoint && PixelInput.LeftPressed()) { stageActive = true; stagePoint = point; stageYaw = useYaw; Refresh(); }
            else if (PixelInput.RightPressed()) StopPlacing();
            return;
        }
        // Stage 2: the base stays; the mouse sets the height (its ray cut with a vertical plane through the base, facing the camera).
        if (ghost != null)
        {
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(stagePoint, Quaternion.Euler(0f, stageYaw, 0f));
        }
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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

    private void Update()
    {
        if (linkPanel != null) { UpdateLink(); return; }
        if (selecting) { UpdateSelect(); return; }
        if (placing == null) { UpdateDelete(); return; }
        if (!PixelDevHub.IsOpen) { StopPlacing(); return; }
        PixelClicker.ExternalClickBlock = true;
        blocking = true;

        bool overUi = PixelInput.PointerOverUI();
        Vector3 point = Vector3.zero;
        bool hasPoint = !overUi && ((placing.line || placing.heightStage || placing.groundPlane) ? TryGroundPoint(out point) : TryFloorPoint(out point));
        if (hasPoint && PixelSnap.Active) point = PixelSnap.Point(point, clicker.OldPixelWorldSize);   // Shift: snap to the grid (a latch onto a belt end still wins)
        float useYaw = PixelSnap.Active ? PixelSnap.Yaw(yaw) : yaw;   // Shift: snap the turn to angle steps
        if (hasPoint && placing.placeSnap != null && placing.placeSnap(clicker, point, useYaw, ghost, out Vector3 snapP, out float snapYaw)) { point = snapP; useYaw = snapYaw; }
        if (ghost != null && !(placing.line && hasStart) && !(placing.heightStage && stageActive))
        {
            ghost.SetActive(hasPoint);
            if (hasPoint) ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, useYaw, 0f));
        }
        if (placing.line) { UpdateLine(hasPoint, point); return; }
        if (placing.heightStage) { UpdateHeightStage(hasPoint, point, useYaw); return; }
        if (PixelKeys.Held(PixelAction.RotateLeft)) yaw -= 120f * Time.unscaledDeltaTime;
        if (PixelKeys.Held(PixelAction.RotateRight)) yaw += 120f * Time.unscaledDeltaTime;

        if (Time.frameCount == placeStartFrame) return;
        if (hasPoint && PixelInput.LeftPressed())
        {
            GameObject go = placing.place(clicker, point, useYaw);
            if (go != null) placing.placed.Add(go);
            if (go != null && placing.linkAfterPlace) { linkPanel = go; linkOnly = false; placeStartFrame = Time.frameCount; go.GetComponent<PixelSolarPanel>()?.ShowRange(true); }
            Refresh();
        }
        else if (PixelInput.RightPressed()) StopPlacing();
    }
}
