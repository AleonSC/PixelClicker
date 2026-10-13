// ---- System notes ----
// PixelFeatureTester.cs
//   The 'Tester' tab of the Dev Hub: a place to spawn and try things that are not part of the game yet (a consumable, a device, a machine...). Each testable thing is a Feature registered in
//   Awake (Register(name, description, ghostBuilder, placeBuilder)): the tab lists them as cards with a 'Place' button. Place = a see-through GHOST follows the mouse over the floor
//   (PixelConsumables.TryGetFloorPoint); left click puts a real one down (and keeps placing), Q / E turn it, right click or Escape stops (PixelWindows entry); 'Clear' removes the ones placed.
//   While placing, clicks on the cube are blocked (PixelClicker.ExternalClickBlock). Placed test objects are NOT saved. To add something to test: write a builder (see PixelWoodChipper:
//   CreateGhost / Place) and add one Register line here.
//   Two-point features (RegisterLine, a conveyor belt) use a marker ghost, then a preview piece between the first point and the mouse; every click builds a piece and the end starts the next.
//   Features now: Wood Chipper (PixelWoodChipper.cs), Conveyor Belt (PixelConveyor.cs, PixelFactoryKit.cs), Kiln (PixelKiln.cs; makes Charcoal old pixels).
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

    private sealed class Feature
    {
        public PlaceSnap placeSnap;
        public string name, description;
        public Func<PixelClicker, GameObject> ghost;
        public Func<PixelClicker, Vector3, float, GameObject> place;
        // Two-point features (a conveyor belt): click a start, click an end, repeat (the end starts the next piece).
        public bool line;
        public Func<PixelClicker, GameObject> preview;
        public Func<PixelClicker, Vector3, Vector3, GameObject> placeLine;
        public Action<GameObject, Vector3, Vector3> updatePreview;
        public Func<PixelClicker, Vector3, Vector3> snap;
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
                              Action<GameObject, Vector3, Vector3> previewUpdate, Func<PixelClicker, Vector3, Vector3, GameObject> placeBuilder, Func<PixelClicker, Vector3, Vector3> snapPoint)
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

        RegisterLine("Conveyor Belt",
                     "Click a point, then another: a belt is built between them (the second point starts the next piece, so short pieces make a curve; right click ends the chain, right click again stops). Clicks near a belt's end snap to it. Right click a placed belt to reverse it. Anything standing on a belt is carried along - drop things on it with Pixel Grabbing, or put it under a machine's outlet.",
                     c => PixelConveyor.CreateMarker(c), c => PixelConveyor.CreatePreview(c), (g, a, b) => PixelConveyor.UpdatePreview(g, a, b),
                     (c, a, b) => PixelConveyor.Place(c, a, b), (c, p) => PixelConveyor.Snap(c, p));
        Register("Kiln",
                 "A brick kiln with a glowing mouth on its front. Anything that goes into the mouth from the side burns (lead a conveyor belt into it, or throw things in): each burnt pixel makes a Charcoal old pixel that comes out of the chute at the back. Charcoal despawns like any old pixel, for now.",
                 c => PixelKiln.CreateGhost(c), (c, p, yw) => PixelKiln.Place(c, p, yw));

        Build(PixelDevHub.TabRoot("tester"));
        PixelWindows.Register(this, 210, () => placing != null, StopPlacing);   // Escape stops placing first
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        StopPlacing();
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
            statusLabel.text = placing == null ? "Pick 'Place' on a card to try it."
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

    private void StopPlacing()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        if (previewGhost != null) Destroy(previewGhost);
        previewGhost = null;
        hasStart = false;
        placing = null;
        if (blocking) { PixelClicker.ExternalClickBlock = false; blocking = false; }
        Refresh();
    }

    private void Clear(Feature f)
    {
        foreach (GameObject g in f.placed) if (g != null) Destroy(g);
        f.placed.Clear();
        Refresh();
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

    /// <summary>Two-point placing: marker at the mouse until the first click, then the belt stretches to the mouse; every further click builds that piece and starts the next one.</summary>
    private void UpdateLine(bool hasPoint, Vector3 raw)
    {
        Vector3 p = hasPoint ? placing.snap(clicker, raw) : raw;
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

    private void Update()
    {
        if (placing == null) return;
        if (!PixelDevHub.IsOpen) { StopPlacing(); return; }
        PixelClicker.ExternalClickBlock = true;
        blocking = true;

        bool overUi = PixelInput.PointerOverUI();
        bool hasPoint = !overUi && TryFloorPoint(out Vector3 point0);
        Vector3 point = Vector3.zero;
        if (hasPoint) TryFloorPoint(out point);
        float useYaw = yaw;
        if (hasPoint && placing.placeSnap != null && placing.placeSnap(clicker, point, yaw, ghost, out Vector3 snapP, out float snapYaw)) { point = snapP; useYaw = snapYaw; }
        if (ghost != null && !(placing.line && hasStart))
        {
            ghost.SetActive(hasPoint);
            if (hasPoint) ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, useYaw, 0f));
        }
        if (placing.line) { UpdateLine(hasPoint, point); return; }
        if (PixelKeys.Held(PixelAction.RotateLeft)) yaw -= 120f * Time.unscaledDeltaTime;
        if (PixelKeys.Held(PixelAction.RotateRight)) yaw += 120f * Time.unscaledDeltaTime;

        if (Time.frameCount == placeStartFrame) return;
        if (hasPoint && PixelInput.LeftPressed())
        {
            GameObject go = placing.place(clicker, point, useYaw);
            if (go != null) placing.placed.Add(go);
            Refresh();
        }
        else if (PixelInput.RightPressed()) StopPlacing();
    }
}
