// ---- System notes ----
// PixelCraftingCube.cs
//   CRAFTING CUBE (2026-10-20, Devices session): a crafting machine. Placed from the CRAFTING WINDOW's 'Crafting Cube' button (it closes the window and starts
//   placing through PixelFeatureTester.StartFeature("Crafting Cube"); also in Dev Hub > Tester). A carved block with glowing edges, ONE coupling socket on each
//   side (all In) and a spell BOOK floating above it. HOVERING the cube opens the book (its cover swings open, pages fan); moving away closes it.
//   RIGHT-CLICK (a quick tap; a right HOLD still removes it like every tester object) opens the recipe picker (PixelCraftingCubeWindow) - the crafting
//   window's recipes (PixelCrafting.MachineRecipes).
//   HOW IT CRAFTS: the recipe's PIXEL ingredients are paid in HUSKS of that type, by value (each husk adds its own amount, OldPixelInfo.amount) - fed in
//   through its couplings (belts, hoses) or dropped on its top. Husks of other types are refused (a belt throws them on). It keeps up to storeMultiple x what
//   one craft needs per type. As soon as every pixel ingredient is covered it crafts (PixelCrafting.MachineCraft: potion / device ingredients come out of the
//   inventory, the result goes INTO the inventory, it counts for the recipe book and crafting XP) at most once per craftSeconds. Prices are the crafting
//   window's (PricedRecipe). A ring over the book shows how close the next craft is; hovering shows what it makes and what it still needs.
//   Saved with the tester save (position, turn, recipe, the husk value stored).
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

public class PixelCraftingCube : MonoBehaviour, IPixelSocketDevice
{
    [Min(0.1f)]
    [Tooltip("Shortest time between two crafts, in seconds.")]
    [SerializeField] private float craftSeconds = 1.2f;
    [Min(1f)]
    [Tooltip("How much it keeps of each pixel ingredient, as a multiple of what one craft needs (more = husks of that type are refused).")]
    [SerializeField] private float storeMultiple = 2f;
    [Min(0.5f)]
    [Tooltip("How fast the book opens / closes when the mouse comes onto / leaves the cube (per second).")]
    [SerializeField] private float bookSpeed = 3.5f;
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of its top (husks dropped on it) and whether it can craft.")]
    [SerializeField] private float checkSeconds = 0.15f;

    private const float SideCubes = 2f;
    private static readonly Color Stone = new Color(0.36f, 0.3f, 0.42f, 1f), Trim = new Color(0.6f, 0.48f, 0.3f, 1f), Rune = new Color(0.75f, 0.5f, 1f, 1f),
                                  Leather = new Color(0.45f, 0.16f, 0.14f, 1f), Paper = new Color(0.96f, 0.93f, 0.82f, 1f), Gold = new Color(0.95f, 0.78f, 0.3f, 1f);

    private PixelClicker clicker;
    private PixelCrafting crafting;
    private bool ghost;
    private float unit = 0.5f, open, nextCheck, nextCraft, craftFlash;
    private PixelSocket[] sockets = new PixelSocket[0];
    private readonly List<Collider> body = new List<Collider>();
    private Transform book, coverPivot, pagePivot;
    private TextMeshPro label;
    private PixelWorldRing ring;
    private readonly List<Renderer> runes = new List<Renderer>();
    private PixelCrafting.Recipe recipe;   // the chosen recipe (as defined; priced each time)
    private readonly Dictionary<PixelClicker.PixelType, double> stored = new Dictionary<PixelClicker.PixelType, double>();
    private string missing = "";   // why it can't craft (shown on hover)
    private readonly PixelRightTap tap = new PixelRightTap();
    private static readonly List<PixelCrafting.Recipe> recipeScratch = new List<PixelCrafting.Recipe>();

    public PixelCrafting.Recipe Recipe => recipe;
    public PixelCrafting Crafting => crafting != null ? crafting : (crafting = PixelFind.Cached<PixelCrafting>());

    // ---------------- building ----------------

    private static float U(PixelClicker c) => Mathf.Max(0.05f, c != null ? c.OldPixelWorldSize : 0.5f);

    public static GameObject CreateGhost(PixelClicker c) => PixelAimLaser.Attach(Build(c, true), Ports(U(c)));

    /// <summary>Its four sockets (the real cube and its placing ghost's aim beams), one on each side, In.</summary>
    private static PixelSocket[] Ports(float u)
    {
        float port = SideCubes * u * 0.5f + 0.35f * u, py = 0.45f * u;
        return new[]
        {
            new PixelSocket("front", new Vector3(0f, py, -port), Vector3.back, PixelSocketFlow.Intake),
            new PixelSocket("back", new Vector3(0f, py, port), Vector3.forward, PixelSocketFlow.Intake),
            new PixelSocket("left", new Vector3(-port, py, 0f), Vector3.left, PixelSocketFlow.Intake),
            new PixelSocket("right", new Vector3(port, py, 0f), Vector3.right, PixelSocketFlow.Intake),
        };
    }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode)
    {
        GameObject root = new GameObject(ghostMode ? "Crafting Cube (Ghost)" : "Crafting Cube");
        PixelCraftingCube cube = root.AddComponent<PixelCraftingCube>();
        cube.clicker = c; cube.ghost = ghostMode; cube.unit = U(c);
        float u = cube.unit, w = SideCubes * u, h = SideCubes * u, e = 0.14f * u;
        PixelFactoryKit k = new PixelFactoryKit(c, ghostMode);
        Transform t = root.transform;
        bool col = !ghostMode;

        // The block: a carved stone cube with wooden trim on its 12 edges, a glowing rune line round its middle and a rune on each face.
        k.Box("Body", t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, w), Stone, default, col, 0.2f, 0.35f);
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                k.Box("Edge", t, new Vector3(a * w * 0.5f, h * 0.5f, b * w * 0.5f), new Vector3(e, h + e, e), Trim, default, false, 0.3f, 0.5f);
                k.Box("Edge", t, new Vector3(a * w * 0.5f, b < 0 ? 0f : h, 0f), new Vector3(e, e, w + e), Trim, default, false, 0.3f, 0.5f);
                k.Box("Edge", t, new Vector3(0f, b < 0 ? 0f : h, a * w * 0.5f), new Vector3(w + e, e, e), Trim, default, false, 0.3f, 0.5f);
            }
        cube.runes.Add(k.Box("Rune Band", t, new Vector3(0f, h * 0.62f, 0f), new Vector3(w + 0.02f * u, 0.06f * u, w + 0.02f * u), Rune, default, false, 0f, 0.8f, Rune).GetComponent<Renderer>());
        for (int f = 0; f < 4; f++)
        {
            Quaternion q = Quaternion.Euler(0f, f * 90f, 0f);
            Vector3 n = q * Vector3.forward;
            GameObject r = k.Box("Face Rune", t, new Vector3(0f, h * 0.35f, 0f) + n * (w * 0.5f + 0.01f * u), new Vector3(0.36f * u, 0.36f * u, 0.02f * u), Rune, new Vector3(0f, f * 90f, 45f), false, 0f, 0.8f, Rune);
            cube.runes.Add(r.GetComponent<Renderer>());
        }
        // A shallow lectern plate on top (husks dropped on it are taken).
        k.Box("Top Plate", t, new Vector3(0f, h + 0.05f * u, 0f), new Vector3(w * 0.8f, 0.1f * u, w * 0.8f), Trim, default, col, 0.3f, 0.55f);

        // The floating BOOK: a back cover with the left pages, a spine, and a front cover with the right pages that swings open round the spine.
        GameObject bookGo = new GameObject("Book");
        bookGo.transform.SetParent(t, false);
        bookGo.transform.localPosition = new Vector3(0f, h + 1.35f * u, 0f);
        Transform bk = bookGo.transform;
        float bw = 0.62f * u, bd = 0.85f * u, ct = 0.05f * u, pt = 0.12f * u;   // half width (one cover), depth, cover / pages thickness
        k.Box("Back Cover", bk, new Vector3(-bw * 0.5f, 0f, 0f), new Vector3(bw, ct, bd), Leather, default, false, 0.2f, 0.45f);
        k.Box("Left Pages", bk, new Vector3(-bw * 0.48f, ct * 0.5f + pt * 0.5f, 0f), new Vector3(bw * 0.92f, pt, bd * 0.92f), Paper, default, false, 0f, 0.3f);
        k.Cyl("Spine", bk, new Vector3(0f, ct, 0f), ct * 1.2f, bd, Leather, new Vector3(90f, 0f, 0f), 0.2f, 0.45f);
        GameObject cover = new GameObject("Cover");
        cover.transform.SetParent(bk, false);
        cover.transform.localPosition = new Vector3(0f, ct, 0f);
        k.Box("Front Cover", cover.transform, new Vector3(bw * 0.5f, 0f, 0f), new Vector3(bw, ct, bd), Leather, default, false, 0.2f, 0.45f);
        k.Box("Clasp", cover.transform, new Vector3(bw * 0.98f, 0f, 0f), new Vector3(0.06f * u, ct * 1.6f, 0.22f * u), Gold, default, false, 0.9f, 0.7f);
        k.Box("Cover Emblem", cover.transform, new Vector3(bw * 0.5f, -ct * 0.6f, 0f), new Vector3(0.18f * u, 0.01f * u, 0.18f * u), Gold, new Vector3(0f, 45f, 0f), false, 0.9f, 0.7f);
        GameObject pages = new GameObject("Right Pages");
        pages.transform.SetParent(cover.transform, false);
        k.Box("Pages", pages.transform, new Vector3(bw * 0.48f, ct * 0.5f + pt * 0.5f, 0f), new Vector3(bw * 0.92f, pt, bd * 0.92f), Paper, default, false, 0f, 0.3f);
        // A glowing glyph on each open page (seen once the book is open).
        k.Box("Glyph", bk, new Vector3(-bw * 0.48f, ct * 0.5f + pt + 0.005f * u, 0f), new Vector3(0.24f * u, 0.005f * u, 0.3f * u), Rune, default, false, 0f, 0.8f, Rune);
        k.Box("Glyph", pages.transform, new Vector3(bw * 0.48f, ct * 0.5f + pt + 0.005f * u, 0f), new Vector3(0.24f * u, 0.005f * u, 0.3f * u), Rune, default, false, 0f, 0.8f, Rune);
        cube.book = bk;
        cube.coverPivot = cover.transform;
        cube.pagePivot = pages.transform;
        cube.ApplyBook(0f);

        if (!ghostMode)
        {
            cube.sockets = Ports(u);
            foreach (Collider c2 in root.GetComponentsInChildren<Collider>()) cube.body.Add(c2);
            // The book can be pointed at too (hover / right click).
            BoxCollider bookHit = root.AddComponent<BoxCollider>();
            bookHit.center = new Vector3(0f, h + 1.35f * u, 0f);
            bookHit.size = new Vector3(1.6f * u, 0.8f * u, 1.1f * u);
            bookHit.isTrigger = true;
            cube.body.Add(bookHit);
            PixelCouplings.Register(cube);
            cube.BuildSigns();
        }
        return root;
    }

    private void BuildSigns()
    {
        GameObject lg = new GameObject("Recipe Label");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, SideCubes * unit + 2.3f * unit, 0f);
        label = lg.AddComponent<TextMeshPro>();
        label.fontSize = 2.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = true;
        label.rectTransform.sizeDelta = new Vector2(12f, 4f);
        if (clicker != null && clicker.UIFont != null) label.font = clicker.UIFont;
        label.gameObject.SetActive(false);
        ring = PixelWorldRing.Attach(transform, new Vector3(0f, SideCubes * unit + 1.95f * unit, 0f), 0.32f * unit, Rune);
        ring.Set(-1f, Rune);
    }

    /// <summary>The book's pose for 'k' (0 = shut, 1 = wide open): the front cover swings round the spine, the right pages follow.</summary>
    private void ApplyBook(float k)
    {
        if (coverPivot == null) return;
        float e = k * k * (3f - 2f * k);   // (eased)
        coverPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(178f, 8f, e));   // shut: lying on the left pages; open: almost flat to the right
        if (pagePivot != null) pagePivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -4f, e));
    }

    // ---------------- sockets ----------------

    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => Stone;

    /// <summary>A husk through one of its couplings: taken if the recipe needs its type and there is room for it.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk) => TryTake(husk);

    private void OnEnable() { if (!ghost && sockets.Length > 0) PixelCouplings.Register(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); }
    private void OnDestroy() { PixelCraftingCubeWindow.Forget(this); }

    // ---------------- the recipe ----------------

    /// <summary>Sets the recipe (the picker window). Husk value stored for types the new recipe doesn't use is kept (it counts again if switched back).</summary>
    public void SetRecipe(PixelCrafting.Recipe r)
    {
        recipe = r;
        missing = "";
        nextCraft = 0f;
        PixelAudio.Play("ui_click", 1.3f);
        PixelCraftingCubeWindow.Changed(this);
    }

    /// <summary>How much of this pixel type one craft needs (0 = not an ingredient). Both ingredients of the same type add up.</summary>
    private static double NeedOf(PixelCrafting.Recipe priced, PixelClicker.PixelType type)
    {
        double n = 0d;
        if (priced.a.kind == PixelCrafting.ItemKind.Pixel && priced.a.type == type) n += priced.a.Amount;
        if (priced.b.kind == PixelCrafting.ItemKind.Pixel && priced.b.type == type) n += priced.b.Amount;
        return n;
    }

    private double Stored(PixelClicker.PixelType type) => stored.TryGetValue(type, out double v) ? v : 0d;

    /// <summary>Take a husk if the recipe needs its type and the store has room (the husk flies into the cube).</summary>
    private bool TryTake(Rigidbody husk)
    {
        if (ghost || husk == null || recipe == null || clicker == null || Crafting == null) return false;
        OldPixelInfo info = PixelHuskQuery.Get(husk).info;
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex)) return false;
        PixelClicker.PixelType type = clicker.Tiers[info.tierIndex].type;
        double need = NeedOf(crafting.PricedRecipe(recipe), type);
        if (need <= 0d || Stored(type) >= need * Mathf.Max(1f, storeMultiple)) return false;
        double amount = System.Math.Max(0d, info.amount);
        if (!clicker.ReleaseOldPixel(husk, false)) return false;
        StartCoroutine(PixelHuskHolder.FlyIn(husk.gameObject, null, transform.TransformPoint(new Vector3(0f, SideCubes * unit + 0.2f * unit, 0f)), false, 0.25f));
        stored[type] = Stored(type) + amount;
        PixelAudio.Play("ui_click", 1.5f);
        PixelStats.Count("craftcube.husks");
        return true;
    }

    // ---------------- every frame ----------------

    private void Update()
    {
        if (ghost || clicker == null) return;
        bool hovered = !PixelCalm.InMenu && PointerOnThis();
        open = Mathf.MoveTowards(open, hovered || PixelCraftingCubeWindow.IsOpenFor(this) ? 1f : 0f, bookSpeed * Time.unscaledDeltaTime);
        ApplyBook(open);
        if (book != null)
        {
            float bob = Mathf.Sin(Time.time * 1.6f) * 0.08f * unit;
            book.localPosition = new Vector3(0f, SideCubes * unit + 1.35f * unit + bob, 0f);
            book.localRotation = Quaternion.Euler(0f, Time.time * 20f * (1f - open), 0f);   // (it turns slowly while shut, holds still while you read it)
        }
        craftFlash = Mathf.Max(0f, craftFlash - Time.deltaTime * 2.5f);
        ShowSigns(hovered);
        if (PixelCalm.InMenu) return;

        // Right TAP on the cube: the recipe picker (a right HOLD is the tester's 'remove').
        if (PixelInput.RightPressed() || PixelInput.RightHeld() || tap.Pending)
        {
            bool usable = !PixelClicker.ExternalClickBlock && !PixelInput.PointerOverUI() && !PixelFirstPerson.Active && !PixelFeatureTester.Placing;
            if (tap.Update(usable && hovered ? (object)this : null)) PixelCraftingCubeWindow.Open(this);
        }

        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkSeconds;
        TakeFromTop();
        TryCraft();
    }

    /// <summary>Husks lying on its top plate (dropped by hand, fallen on) are taken if the recipe wants them.</summary>
    private void TakeFromTop()
    {
        if (recipe == null) return;
        float half = SideCubes * unit * 0.5f, top = SideCubes * unit;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            if (i >= pixels.Count) continue;
            Rigidbody b = pixels[i];
            if (b == null || !PixelHuskHolder.Takeable(clicker, b)) continue;
            Vector3 p = transform.InverseTransformPoint(b.position);
            if (Mathf.Abs(p.x) > half || Mathf.Abs(p.z) > half || p.y < top - 0.1f * unit || p.y > top + 1.2f * unit) continue;
            TryTake(b);
        }
    }

    /// <summary>Every pixel ingredient covered: one craft (the rest of the ingredients from the inventory), the husk value it used is taken off.</summary>
    private void TryCraft()
    {
        if (recipe == null || Crafting == null || Time.time < nextCraft) return;
        PixelCrafting.Recipe priced = crafting.PricedRecipe(recipe);
        foreach (PixelCrafting.Ingredient ing in new[] { priced.a, priced.b })
        {
            if (ing.kind != PixelCrafting.ItemKind.Pixel) continue;
            double need = NeedOf(priced, ing.type);
            if (Stored(ing.type) < need) { missing = ""; return; }   // (still filling with husks: the ring shows it)
        }
        if (!crafting.MachineCraft(priced, recipe)) { missing = "Needs the other ingredient in your inventory, or room for the result"; return; }
        missing = "";
        foreach (PixelCrafting.Ingredient ing in new[] { priced.a, priced.b })
            if (ing.kind == PixelCrafting.ItemKind.Pixel) stored[ing.type] = System.Math.Max(0d, Stored(ing.type) - ing.Amount);
        nextCraft = Time.time + craftSeconds;
        craftFlash = 1f;
        PixelAudio.Play("purchase");   // (the runes and the ring flash; the result is in the inventory)
        PixelStats.Count("craftcube.crafts");
    }

    /// <summary>How close the next craft is (0..1): the least-filled pixel ingredient; -1 = no recipe.</summary>
    private float Progress(PixelCrafting.Recipe priced)
    {
        if (priced == null) return -1f;
        float f = 1f;
        foreach (PixelCrafting.Ingredient ing in new[] { priced.a, priced.b })
            if (ing.kind == PixelCrafting.ItemKind.Pixel) { double need = NeedOf(priced, ing.type); if (need > 0d) f = Mathf.Min(f, (float)(Stored(ing.type) / need)); }
        return Mathf.Clamp01(f);
    }

    /// <summary>The ring over the book (progress), the runes (brighter as it fills, flash on a craft) and, while hovered, what it makes and still needs.</summary>
    private void ShowSigns(bool hovered)
    {
        PixelCrafting.Recipe priced = recipe != null && Crafting != null ? crafting.PricedRecipe(recipe) : null;
        float progress = Progress(priced);
        if (ring != null) ring.Set(progress, Color.Lerp(Rune, Color.white, craftFlash));
        Color glow = recipe == null ? Rune * 0.35f : Color.Lerp(Rune * (0.5f + 0.5f * progress), Color.white, craftFlash);
        glow.a = 1f;
        SetRunes(glow);
        if (label == null) return;
        label.gameObject.SetActive(hovered);
        if (!hovered) return;
        Camera cam = clicker.ViewCamera;
        if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, cam.transform.up);
        if (priced == null) { label.text = "<b>Crafting Cube</b>\n<size=75%>Right-click to choose a recipe</size>"; return; }
        string text = "<b>" + crafting.MachineResultName(recipe) + "</b>";
        foreach (PixelCrafting.Ingredient ing in new[] { priced.a, priced.b })
        {
            if (ing.kind == PixelCrafting.ItemKind.Pixel)
                text += "\n<size=75%>" + crafting.IngredientName(ing) + " husks: " + PixelClicker.FormatNumber(System.Math.Floor(Stored(ing.type))) + " / " + PixelClicker.FormatNumber(ing.Amount) + "</size>";
            else
                text += "\n<size=75%>" + crafting.IngredientName(ing) + " x" + PixelClicker.FormatNumber(ing.Amount) + " (from your inventory)</size>";
        }
        if (!string.IsNullOrEmpty(missing)) text += "\n<size=70%><color=#FF8A7A>" + missing + "</color></size>";
        label.text = text;
    }

    private Color runesShown = new Color(-1f, 0f, 0f, 0f);

    private void SetRunes(Color c)
    {
        if (Mathf.Abs(c.r - runesShown.r) + Mathf.Abs(c.g - runesShown.g) + Mathf.Abs(c.b - runesShown.b) < 0.02f) return;
        runesShown = c;
        foreach (Renderer r in runes)
        {
            if (r == null) continue;
            Material m = r.material;
            m.color = c;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c); }
        }
    }

    /// <summary>Is the pointer on the cube or its book (nothing nearer in the way: the clicker cube, a husk)?</summary>
    private bool PointerOnThis()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null || PixelInput.PointerOverUI()) return false;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        float best = float.MaxValue;
        foreach (Collider c in body) if (c != null && c.Raycast(ray, out RaycastHit hit, 500f)) best = Mathf.Min(best, hit.distance);
        if (best == float.MaxValue) return false;
        foreach (RaycastHit h in Physics.RaycastAll(ray, best, ~0, QueryTriggerInteraction.Ignore))
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return false;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return false;
        }
        return true;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save
    {
        public Vector3 position;
        public float yaw;
        public string recipe = "";      // the recipe's label
        public int recipeIndex = -1;    // its place in the list (when two share a label)
        public List<string> types = new List<string>();
        public List<double> amounts = new List<double>();
    }

    public static string ExportState(GameObject root)
    {
        PixelCraftingCube cube = root != null ? root.GetComponent<PixelCraftingCube>() : null;
        if (cube == null) return null;
        Save s = new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y };
        if (cube.recipe != null && cube.Crafting != null)
        {
            cube.crafting.MachineRecipes(recipeScratch);
            s.recipe = cube.recipe.label;
            s.recipeIndex = recipeScratch.IndexOf(cube.recipe);
        }
        foreach (KeyValuePair<PixelClicker.PixelType, double> kv in cube.stored)
            if (kv.Value > 0d) { s.types.Add(kv.Key.ToString()); s.amounts.Add(kv.Value); }
        return JsonUtility.ToJson(s);
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        GameObject root = Place(c, s.position, s.yaw);
        PixelCraftingCube cube = root.GetComponent<PixelCraftingCube>();
        if (cube.Crafting != null && !string.IsNullOrEmpty(s.recipe))
        {
            cube.crafting.MachineRecipes(recipeScratch);
            PixelCrafting.Recipe r = s.recipeIndex >= 0 && s.recipeIndex < recipeScratch.Count && recipeScratch[s.recipeIndex].label == s.recipe ? recipeScratch[s.recipeIndex]
                                   : recipeScratch.Find(x => x.label == s.recipe);
            cube.recipe = r;
        }
        for (int i = 0; i < s.types.Count && i < s.amounts.Count; i++)
            if (System.Enum.TryParse(s.types[i], out PixelClicker.PixelType t)) cube.stored[t] = s.amounts[i];
        return root;
    }
}
