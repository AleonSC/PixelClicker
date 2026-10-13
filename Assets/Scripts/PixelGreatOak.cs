// ---- System notes ----
// PixelGreatOak.cs
//   The GREAT OAK (PixelType.GreatOak 77, PixelBlockRules special rule): a huge Woodcutting block that takes a random 50-100 clicks. While Woodcutting is the skill in play every rolled
//   wood block (Wood, Bark, Sap, Leaf, Heartwood, Twig) has a 5% chance of becoming one (edit it in PixelBlockRules; no spawn weight of its own).
//   While you hit it, BIRD NESTS fall out of the tree (this component, added by PixelClicker.Awake, listens to PixelClicker.PixelHit): each hit has a nestChance (0 = 40%) to drop one,
//   a nest (a woven ring with eggs) lands on the ground near the cube and stays for nestSeconds (0 = 9, it blinks away in the last 2 s). Click it (the pointer, or the crosshair in
//   first person - with any item selected) before it vanishes for nestXp (0 = 30) Woodcutting XP, a chirp and a puff of feathers. At most maxNests (0 = 10) lie about at once; each nest follows a short scripted arc out of a random SIDE of the cube (no physics and no collider at all, so it cannot bounce away), lands close to the cube (up to scatterCubes, 0 = 3.2 cube widths), makes one small hop and stays.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelGreatOak : MonoBehaviour
{
    [Header("Great Oak")]
    [Range(0f, 1f)]
    [Tooltip("Chance that a hit on the Great Oak shakes a bird nest loose. 0 = the coded default (0.4).")]
    [SerializeField] private float nestChance = 0f;

    [Min(0f)]
    [Tooltip("Seconds a fallen nest stays before it disappears. 0 = the coded default (9).")]
    [SerializeField] private float nestSeconds = 0f;

    [Min(0f)]
    [Tooltip("Woodcutting XP for catching a nest in time. 0 = the coded default (30).")]
    [SerializeField] private float nestXp = 0f;

    [Min(0f)]
    [Tooltip("How far (in cube widths) a nest can land from the cube. 0 = the coded default (3.2).")]
    [SerializeField] private float scatterCubes = 0f;

    [Min(0)]
    [Tooltip("Most nests lying about at once. 0 = the coded default (10).")]
    [SerializeField] private int maxNests = 0;

    public static PixelGreatOak Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    private class Nest
    {
        public GameObject go;
        public float born;
        public Vector3 from, to;      // the side of the cube it leaves from / the spot on the ground it lands on
        public float arcHeight, spinY;
        public Renderer[] renderers;
    }

    private PixelClicker clicker;
    private readonly List<Nest> nests = new List<Nest>();
    private Material nestMat, eggMat;

    private float Seconds => nestSeconds > 0f ? nestSeconds : 9f;
    private float Chance => nestChance > 0f ? nestChance : 0.4f;
    private int Max => maxNests > 0 ? maxNests : 10;

    private void Awake() { Instance = this; }

    private void OnEnable()
    {
        clicker = GetComponent<PixelClicker>();
        if (clicker != null) clicker.PixelHit += OnHit;
    }

    private void OnDisable()
    {
        if (clicker != null) clicker.PixelHit -= OnHit;
        foreach (Nest n in nests) if (n.go != null) Destroy(n.go);
        nests.Clear();
        if (Instance == this) Instance = null;
    }

    private void OnHit(int tierIndex, int hits, int needed, bool automatic)
    {
        if (clicker == null || !clicker.IsValidTierIndex(tierIndex) || clicker.Tiers[tierIndex].type != PixelClicker.PixelType.GreatOak) return;
        if (Random.value > Chance || nests.Count >= Max) return;
        SpawnNest();
    }

    private void SpawnNest()
    {
        float size = clicker.PixelBaseSize;
        Vector3 origin = clicker.LiveWorldPosition + Vector3.up * size * 0.3f;
        if (nestMat == null)
        {
            nestMat = Mat(new Color(0.42f, 0.29f, 0.16f));
            eggMat = Mat(new Color(0.65f, 0.85f, 0.88f));
        }
        GameObject go = new GameObject("Bird Nest");
        float s = size * 0.55f;
        go.transform.position = origin;
        Piece(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(s, s * 0.12f, s), nestMat);          // the woven base
        Piece(go.transform, PrimitiveType.Cylinder, new Vector3(0f, s * 0.12f, 0f), new Vector3(s * 0.92f, s * 0.06f, s * 0.92f), nestMat);
        for (int i = 0; i < 6; i++)                                                                                              // twigs sticking out of the rim
        {
            float a = i * 60f;
            GameObject twig = Piece(go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(s * 0.05f, s * 0.05f, s * 0.5f), nestMat);
            twig.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, s * 0.12f, s * 0.42f);
            twig.transform.localRotation = Quaternion.Euler(-15f, a, 0f);
        }
        for (int i = 0; i < 3; i++)                                                                                              // three eggs
        {
            GameObject egg = Piece(go.transform, PrimitiveType.Sphere, new Vector3(Mathf.Cos(i * 2.1f) * s * 0.18f, s * 0.2f, Mathf.Sin(i * 2.1f) * s * 0.18f), new Vector3(s * 0.2f, s * 0.26f, s * 0.2f), eggMat);
            egg.name = "Egg";
        }
        // No physics at all: the nest follows a short scripted arc out of the SIDE of the cube, lands close to it, makes one small hop and stays put. Nothing can knock it far away,
        // and it has no collider to hit anything on the way (it is clicked by aiming at it, not by a collider).
        float far = size * (scatterCubes > 0f ? scatterCubes : 3.2f);
        float dist = Mathf.Lerp(Mathf.Min(1.3f * size, far), far, Random.value);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 flat = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 from = origin + flat * size * 0.55f;
        Vector3 landXZ = clicker.LiveWorldPosition + flat * dist;
        float groundY = GroundY(landXZ, size, from.y);
        Vector3 to = new Vector3(landXZ.x, groundY + s * 0.07f, landXZ.z);
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
        nests.Add(new Nest { go = go, born = Time.time, from = from, to = to, arcHeight = size * Random.Range(0.7f, 1.1f), spinY = Random.Range(-540f, 540f), renderers = go.GetComponentsInChildren<Renderer>() });
        PixelAudio.Play("nest_fall");
    }

    /// <summary>The height of the ground under a point (the floor, ignoring old pixels and the cube).</summary>
    private float GroundY(Vector3 point, float size, float fallbackY)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * size * 4f, Vector3.down, size * 12f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.normal.y < 0.7f) continue;
            return h.point.y;
        }
        if (PixelFloor.Instance != null && PixelFloor.Instance.FloorRenderer != null) return PixelFloor.Instance.FloorRenderer.bounds.max.y;
        return fallbackY - size * 0.3f;
    }

    private const float FlightSeconds = 0.5f, HopSeconds = 0.22f;

    /// <summary>The scripted flight: an arc out of the cube, then one small hop, then it rests.</summary>
    private void Fly(Nest n)
    {
        float t = Time.time - n.born;
        if (t < FlightSeconds)
        {
            float u = t / FlightSeconds;
            Vector3 p = Vector3.Lerp(n.from, n.to, u);
            p.y += n.arcHeight * 4f * u * (1f - u);
            n.go.transform.position = p;
            n.go.transform.Rotate(0f, n.spinY * Time.deltaTime, 0f, Space.World);
        }
        else if (t < FlightSeconds + HopSeconds)
        {
            float v = (t - FlightSeconds) / HopSeconds;
            n.go.transform.position = n.to + Vector3.up * n.arcHeight * 0.22f * 4f * v * (1f - v);
        }
        else if (n.go.transform.position != n.to) n.go.transform.position = n.to;
    }

    private static GameObject Piece(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Collider c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    private static Material Mat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    private void Update()
    {
        if (nests.Count == 0) return;
        for (int i = nests.Count - 1; i >= 0; i--)
        {
            Nest n = nests[i];
            if (n.go == null) { nests.RemoveAt(i); continue; }
            Fly(n);
            float age = Time.time - n.born;
            if (age >= Seconds) { Vanish(n); nests.RemoveAt(i); continue; }
            bool blink = age > Seconds - 2f && Mathf.Sin(Time.time * 18f) < 0f;   // hurry: it flickers in the last 2 seconds
            foreach (Renderer r in n.renderers) if (r != null) r.enabled = !blink;
        }

        if (!PixelInput.LeftPressed() || PixelPauseMenu.IsPaused) return;
        Ray ray;
        if (PixelFirstPerson.TryGetViewRay(out Ray fpRay)) ray = fpRay;
        else
        {
            if (PixelInput.PointerOverUI()) return;
            Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            if (cam == null) return;
            ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        }
        TryCatch(ray);
    }

    private void TryCatch(Ray ray)
    {
        float reach = PixelFirstPerson.Active ? clicker.OldPixelWorldSize * 14f : 1000f;
        float radius = clicker.PixelBaseSize * 0.45f;
        Nest best = null;
        float bestDist = float.MaxValue;
        foreach (Nest n in nests)
        {
            if (n.go == null) continue;
            Vector3 to = n.go.transform.position - ray.origin;
            float along = Vector3.Dot(to, ray.direction);
            if (along < 0f || along > reach) continue;
            float off = (to - ray.direction * along).magnitude;
            if (off > radius) continue;
            if (along < bestDist) { bestDist = along; best = n; }
        }
        if (best == null) return;
        Catch(best);
    }

    private void Catch(Nest n)
    {
        nests.Remove(n);
        Vector3 pos = n.go.transform.position;
        PixelSkills skills = PixelSkills.Instance;
        if (skills != null)
        {
            PixelSkills.SkillPack pack = skills.Find(PixelSkills.WoodcuttingId);
            if (pack != null) skills.AddXp(pack, nestXp > 0f ? nestXp : 30f);
        }
        PixelAudio.Play("nest_catch");
        PixelStats.Count("nests.caught");
        PixelFarmFx.Break(PixelClicker.PixelType.Leaf, pos, clicker.PixelBaseSize * 0.7f, new Color(0.9f, 0.85f, 0.7f));   // a puff of leaves / feathers
        Destroy(n.go);
    }

    private void Vanish(Nest n)
    {
        PixelFarmFx.Break(PixelClicker.PixelType.Twig, n.go.transform.position, clicker.PixelBaseSize * 0.4f, new Color(0.42f, 0.29f, 0.16f));
        Destroy(n.go);
    }
}
