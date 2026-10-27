// ---- System notes ----
// PixelHuskSource.cs
//   HUSK SOURCE (2026-10-19, a Dev Hub > Tester feature 'Husk Source', for testing machine lines): a simple box with a coupling SOCKET on each side (Out).
//   It makes an ENDLESS supply of one chosen block type, but only while something takes them: every spawnSeconds each Out coupling with a belt starting
//   at it (with room) or a machine's In coupling right in front of it gets a NEW husk (SpawnStoredPixel + PixelCouplings.Emit), and a Container Hose
//   pulling from one of its couplings gets one whenever it pumps (IPixelHuskSource). Nothing connected = nothing made.
//   LEFT-CLICK the box to choose the type in the shared block picker (PixelSortingHopperWindow, IPixelTierPickTarget: every block type, no None); the type
//   spins over the box. Saved with the tester save (position, turn, type; its couplings go with it).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelHuskSource : MonoBehaviour, IPixelSocketDevice, IPixelHuskSource, IPixelTierPickTarget
{
    [Min(0.05f)]
    [Tooltip("Seconds between two husks out of each connected coupling.")]
    [SerializeField] private float spawnSeconds = 0.5f;

    private const float SideCubes = 2.2f, HeightCubes = 1.6f;

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 0.5f, nextSpawn;
    private int tier = -1;
    private PixelSocket[] sockets = new PixelSocket[0];
    private Transform display;
    private GameObject model;
    private readonly List<Collider> body = new List<Collider>();

    // ---------------- building ----------------

    public static GameObject CreateGhost(PixelClicker c) => PixelAimLaser.Attach(Build(c, true), Ports(Mathf.Max(0.05f, c != null ? c.OldPixelWorldSize : 0.5f)));

    /// <summary>Its four sockets (the real box and its placing ghost's aim beams), one on each side, Out.</summary>
    private static PixelSocket[] Ports(float u)
    {
        float port = SideCubes * u * 0.5f + 0.35f * u;
        return new[]
        {
            new PixelSocket("front", new Vector3(0f, 0f, -port), Vector3.back, PixelSocketFlow.Output),
            new PixelSocket("back", new Vector3(0f, 0f, port), Vector3.forward, PixelSocketFlow.Output),
            new PixelSocket("left", new Vector3(-port, 0f, 0f), Vector3.left, PixelSocketFlow.Output),
            new PixelSocket("right", new Vector3(port, 0f, 0f), Vector3.right, PixelSocketFlow.Output),
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
        GameObject root = new GameObject(ghostMode ? "Husk Source (Ghost)" : "Husk Source");
        PixelHuskSource s = root.AddComponent<PixelHuskSource>();
        s.clicker = c;
        s.ghost = ghostMode;
        s.unit = Mathf.Max(0.05f, c != null ? c.OldPixelWorldSize : 0.5f);
        float u = s.unit, w = SideCubes * u, h = HeightCubes * u;
        PixelFactoryKit k = new PixelFactoryKit(c, ghostMode);
        Color shell = new Color(0.3f, 0.26f, 0.42f, 1f), metal = new Color(0.7f, 0.72f, 0.76f, 1f), glow = new Color(0.8f, 0.5f, 1f, 1f);
        Transform tr = root.transform;
        k.Box("Plinth", tr, new Vector3(0f, 0.08f * u, 0f), new Vector3(w + 0.3f * u, 0.16f * u, w + 0.3f * u), new Color(0.12f, 0.12f, 0.14f, 1f), default, !ghostMode, 0.5f, 0.5f);
        k.Box("Body", tr, new Vector3(0f, h * 0.5f + 0.16f * u, 0f), new Vector3(w, h, w), shell, default, !ghostMode, 0.35f, 0.55f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Box("Corner", tr, new Vector3(sx * w * 0.5f, h * 0.5f + 0.16f * u, sz * w * 0.5f), new Vector3(0.16f * u, h + 0.06f * u, 0.16f * u), metal, default, false, 0.85f, 0.7f);
        k.Box("Glow Band", tr, new Vector3(0f, h * 0.75f + 0.16f * u, 0f), new Vector3(w + 0.04f * u, 0.08f * u, w + 0.04f * u), glow, default, false, 0f, 0.8f, glow);
        k.Box("Lid", tr, new Vector3(0f, h + 0.2f * u, 0f), new Vector3(w * 0.9f, 0.08f * u, w * 0.9f), metal, default, false, 0.8f, 0.6f);
        k.Cyl("Pedestal", tr, new Vector3(0f, h + 0.3f * u, 0f), 0.35f * u, 0.12f * u, glow, default, 0f, 0.8f);
        GameObject top = new GameObject("Display");
        top.transform.SetParent(tr, false);
        top.transform.localPosition = new Vector3(0f, h + 0.9f * u, 0f);
        s.display = top.transform;
        if (!ghostMode)
        {
            s.sockets = Ports(u);
            foreach (Collider col in root.GetComponentsInChildren<Collider>()) s.body.Add(col);
            PixelCouplings.Register(s);
        }
        return root;
    }

    // ---------------- sockets / hose ----------------
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.3f, 0.26f, 0.42f, 1f);
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk) => false;   // (it only gives)

    /// <summary>A hose pulls one: endless, of the chosen type.</summary>
    public bool TakeHuskOut(PixelSocket socket, out int t, out double amount)
    {
        t = tier; amount = 0d;
        if (ghost || clicker == null || !clicker.IsValidTierIndex(tier)) return false;
        amount = System.Math.Max(1d, clicker.Tiers[tier].amountPerClick);
        return true;
    }

    private void OnEnable() { if (!ghost && sockets.Length > 0) PixelCouplings.Register(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); }
    private void OnDestroy() { PixelSortingHopperWindow.Forget(this); }

    // ---------------- every frame ----------------

    private void Update()
    {
        if (ghost || clicker == null) return;
        if (display != null) display.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        if (PixelCalm.InMenu || Time.timeScale <= 0f) return;
        HandleClick();
        if (Time.time < nextSpawn) return;
        nextSpawn = Time.time + spawnSeconds;
        if (!clicker.IsValidTierIndex(tier)) return;
        foreach (PixelSocket s in sockets) Feed(s);
    }

    /// <summary>One new husk out of this socket's Out coupling - only when a belt there has room or a machine's In coupling is right in front.</summary>
    private void Feed(PixelSocket s)
    {
        PixelCoupling c = PixelCoupling.On(this, s);
        if (c == null || !c.IsOut) return;
        bool belt = PixelCouplings.BeltStartingAt(c.PortWorld) != null;
        if (belt ? !PixelCouplings.OutputHasRoom(this, s.id) : PixelCouplings.IntakeAt(c.PortWorld + c.FacingWorld * 0.5f * unit, unit, this) == null) return;
        Rigidbody rb = clicker.SpawnStoredHusk(tier, System.Math.Max(1d, clicker.Tiers[tier].amountPerClick), c.PortWorld + Vector3.up * 0.6f * unit, Vector3.zero);
        if (rb == null) return;
        OldPixelPopIn pop = rb.GetComponent<OldPixelPopIn>();
        if (pop != null) pop.Finish();
        rb.isKinematic = !belt;   // (HandIn wants a kinematic husk; PutOnBelt makes it loose again)
        if (!PixelCouplings.Emit(this, s.id, rb)) rb.isKinematic = false;
    }

    /// <summary>Left-click the box: the block picker opens.</summary>
    private void HandleClick()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null || PixelInput.PointerOverUI() || PixelFeatureTester.Placing || PixelCarryHandles.Hot) return;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        bool over = false;
        foreach (Collider col in body) if (col != null && col.Raycast(ray, out _, 500f)) { over = true; break; }
        if (!over) return;
        PixelPointerClaim.ClaimButton();
        if (PixelInput.LeftPressed()) PixelSortingHopperWindow.Open(this, 0);
    }

    // ---------------- the block picker (IPixelTierPickTarget) ----------------
    public PixelClicker Clicker => clicker;
    public int PickedTier(int slot) => tier;
    public void SetPickedTier(int slot, int t) => SetTier(t);
    public string PickTitle(int slot) => "Husk Source";
    public string PickHint(int slot) => "It makes endless husks of this type, out of every coupling something takes them from.";
    public bool PickAllowsNone => false;
    public bool PickAllTiers => true;
    public bool PickAlive => this != null;

    private void SetTier(int t)
    {
        tier = clicker != null && clicker.IsValidTierIndex(t) ? t : -1;
        if (model != null) Destroy(model);
        model = tier >= 0 && display != null ? clicker.CreateDisplayPixel(tier, display, unit * 0.7f) : null;
        if (model != null) model.transform.localPosition = Vector3.zero;
        PixelSortingHopperWindow.Changed(this);
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; public string type; }

    public static string ExportState(GameObject root)
    {
        PixelHuskSource s = root != null ? root.GetComponent<PixelHuskSource>() : null;
        if (s == null) return null;
        string type = s.clicker != null && s.clicker.IsValidTierIndex(s.tier) ? s.clicker.Tiers[s.tier].type.ToString() : "";
        return JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, type = type });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save save = JsonUtility.FromJson<Save>(json);
        if (save == null) return null;
        GameObject go = Place(c, save.position, save.yaw);
        if (!string.IsNullOrEmpty(save.type) && System.Enum.TryParse(save.type, out PixelClicker.PixelType type)) go.GetComponent<PixelHuskSource>().SetTier(c.IndexOf(type));
        return go;
    }
}
