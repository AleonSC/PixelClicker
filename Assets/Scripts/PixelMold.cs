// ---- System notes ----
// PixelMold.cs
//   Foraging's Mold block (PixelType.Mold 75, a hazard-style block that replaced the Toadstool in the Foraging pack; the Toadstool still exists but is switched off for now)
//   and the glowing Foraging finds. Component added by PixelClicker.Awake (PixelMold.Instance, Inspector fields for every number).
//   MOLD (Foraging hazard, 1 left click sets it off, 1 right click clears it): with wholeMap (on) setting it off molds EVERY husk on the field at once;
//   zoneHoldSeconds 0 = 30 and killSeconds 0 = 22 (longer than before). Old notes: every left click on the Mold block on the cube calls PixelMold.Spread (PixelClicker.CollectInternal / TriggerHazard) = the mold zone around the cube grows by
//   spreadWidths old-pixel widths (a faint green disc on the floor, up to maxWidths). Right click (DefuseHazard) / the block leaving only STOPS the spreading - the zone stays and then
//   fades by itself after zoneHoldSeconds + zoneFadeSeconds. Every infectInterval s the zone infects the old pixels lying inside it (PixelMoldy: a green shell that darkens; the pixel dies
//   after killSeconds - the timer waits while it is held). SHAKE a held moldy pixel (PixelMoldy.FixedUpdate adds up the change of velocity of the dragged body) to shake the mold off:
//   shakeNeeded cleans it and gives cleanImmuneSeconds (OldPixelInfo.moldImmuneUntil) so it is not re-infected at once, and gives cleanXp (3) Foraging XP (GiveCleanXp). The shell is a pale blue-green fuzz that turns whiter (moldRipeColor) as the pixel nears death - never dark, so moldy husks don't look like Truffles. Not saved: a loaded game has no zone / mold.
//   GLOW: PixelMold.TryAttachGlow (called from PixelClicker.SpawnFallingCopy) gives a fresh Berry / Mushroom / Herb / Truffle old pixel a glowGlowChance chance to be a glowing variant;
//   it only starts to glow (PixelForageGlow, shell fades in and pulses) once OldPixelDespawn.AgeFraction reaches glowStartFraction (0.5 = halfway through its lifespan).
//   The glow is a look only for now (no extra pay). Also not saved.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelMold : MonoBehaviour
{
    public static PixelMold Instance { get; private set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Mold zone")]
    [Tooltip("Setting the Mold off covers the WHOLE field at once (every husk lying anywhere gets moldy). Off = the old growing zone round the cube.")]
    [SerializeField] private bool wholeMap = true;
    [Min(0f)]
    [Tooltip("How far the mold spreads with each click on the Mold block, in old-pixel widths. 0 = the default (0.9).")]
    [SerializeField] private float spreadWidths = 0f;
    [Min(0f)]
    [Tooltip("Widest the mold zone gets, in old-pixel widths. 0 = the default (8).")]
    [SerializeField] private float maxWidths = 0f;
    [Min(0f)]
    [Tooltip("Seconds after the last spread before the zone starts to fade. 0 = the default (30).")]
    [SerializeField] private float zoneHoldSeconds = 0f;
    [Min(0f)]
    [Tooltip("Seconds the zone takes to fade away completely. 0 = the default (25).")]
    [SerializeField] private float zoneFadeSeconds = 0f;
    [Min(0f)]
    [Tooltip("How often (seconds) the zone infects old pixels lying inside it. 0 = the default (0.5).")]
    [SerializeField] private float infectInterval = 0f;
    [Tooltip("Colour of the zone on the floor and the mold shells on infected pixels.")]
    [SerializeField] private Color moldColor = new Color(0.62f, 0.95f, 0.8f, 1f);
    [Tooltip("Colour a mold shell grows towards as the pixel is about to die (a pale white fuzz - kept light so moldy husks never look like dark Truffles).")]
    [SerializeField] private Color moldRipeColor = new Color(0.9f, 0.98f, 0.93f, 1f);
    [Min(0f)]
    [Tooltip("Foraging XP for shaking the mold off one husk.")]
    [SerializeField] private float cleanXp = 3f;

    [Header("Infected pixels")]
    [Min(0f)]
    [Tooltip("Seconds an infected pixel survives before the mold kills it (the time stops while you hold it). 0 = the default (22).")]
    [SerializeField] private float killSeconds = 0f;
    [Min(0f)]
    [Tooltip("How much a held pixel must be shaken (added up change of speed, world units per second) to shake the mold off. 0 = the default (28).")]
    [SerializeField] private float shakeNeeded = 0f;
    [Min(0f)]
    [Tooltip("Seconds a freshly cleaned pixel can't be infected again. 0 = the default (8).")]
    [SerializeField] private float cleanImmuneSeconds = 0f;

    [Header("Glowing finds")]
    [Range(0f, 1f)]
    [Tooltip("Chance that a fresh Berry / Mushroom / Herb / Truffle old pixel is a glowing variant. 0 = the default (0.12).")]
    [SerializeField] private float glowChance = 0f;
    [Range(0f, 1f)]
    [Tooltip("The variant starts to glow once this fraction of its lifespan has passed. 0 = the default (0.5 = halfway).")]
    [SerializeField] private float glowStartFraction = 0f;
    [Tooltip("Switch the mold zone and its infection off (the Mold block is then harmless).")]
    [SerializeField] private bool disableMold = false;
    [Tooltip("Switch the glowing variants off.")]
    [SerializeField] private bool disableGlow = false;

    public float SpreadWidths => spreadWidths > 0f ? spreadWidths : 0.9f;
    public float MaxWidths => maxWidths > 0f ? maxWidths : 8f;
    public float KillSeconds => killSeconds > 0f ? killSeconds : 22f;
    public float ShakeNeeded => shakeNeeded > 0f ? shakeNeeded : 28f;
    public float CleanImmune => cleanImmuneSeconds > 0f ? cleanImmuneSeconds : 8f;
    public float GlowStart => glowStartFraction > 0f ? glowStartFraction : 0.5f;
    public Color MoldColor => moldColor;
    public Color MoldRipeColor => moldRipeColor;

    /// <summary>Shaking the mold off a husk trains Foraging (cleanXp, through the normal XP path with its popup).</summary>
    public void GiveCleanXp()
    {
        PixelSkills s = PixelSkills.Instance;
        PixelSkills.SkillPack pack = s != null ? s.Find(PixelSkills.ForagingId) : null;
        if (pack != null && cleanXp > 0f) s.AddXp(pack, cleanXp);
    }

    private PixelClicker clicker;
    private Vector3 center;
    private float radius, lastSpread = -999f, infectTimer;
    private Transform disc;
    private Material discMaterial;

    private void Awake() { Instance = this; clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }

    /// <summary>A click on the Mold block: the mold zone around 'pos' (the cube) grows by 'steps' steps.</summary>
    public static void Spread(Vector3 pos, float steps = 1f)
    {
        if (Instance == null || Instance.disableMold) return;
        Instance.DoSpread(pos, steps);
    }

    private void DoSpread(Vector3 pos, float steps)
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        float widthUnit = clicker.OldPixelWorldSize;
        if (radius <= 0.01f)
        {
            float floorY = pos.y - clicker.PixelBaseSize * 2f;
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            center = new Vector3(pos.x, floorY + 0.02f, pos.z);
        }
        radius = wholeMap ? 1000f : Mathf.Min(MaxWidths * widthUnit, radius + SpreadWidths * widthUnit * steps);   // (whole map: every husk anywhere is in reach)
        lastSpread = Time.time;
        EnsureDisc();
    }

    private void EnsureDisc()
    {
        if (disc != null) return;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Mold Zone";
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        discMaterial = new Material(PixelShaders.SpriteDefault());
        go.GetComponent<Renderer>().sharedMaterial = discMaterial;
        disc = go.transform;
    }

    private void Update()
    {
        if (disc == null && radius <= 0f) return;
        if (!(Time.timeScale > 0f) || PixelPauseMenu.IsPaused) return;

        // Fade away on its own once it has not been fed for a while.
        float hold = zoneHoldSeconds > 0f ? zoneHoldSeconds : 30f, fade = zoneFadeSeconds > 0f ? zoneFadeSeconds : 25f;
        float sinceSpread = Time.time - lastSpread;
        float fadeK = sinceSpread <= hold ? 0f : Mathf.Clamp01((sinceSpread - hold) / fade);
        if (fadeK >= 1f) { radius = 0f; if (disc != null) { Destroy(disc.gameObject); disc = null; } return; }

        if (disc != null)
        {
            float shown = Mathf.Min(radius, 40f) * (1f - fadeK * 0.5f);   // (the whole-map zone is drawn as a big green sheet, not a 1000-unit disc)
            disc.position = center;
            disc.localScale = new Vector3(shown * 2f, 0.004f, shown * 2f);
            Color c = moldColor;
            c.a = Mathf.Lerp(0.28f, 0.04f, fadeK) * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f));
            discMaterial.color = c;
        }

        if (disableMold || clicker == null) return;
        infectTimer -= Time.deltaTime;
        if (infectTimer > 0f) return;
        infectTimer = infectInterval > 0f ? infectInterval : 0.5f;
        float r2 = radius * (1f - fadeK * 0.5f);
        r2 *= r2;
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody b = list[i];
            if (b == null) continue;
            Vector3 d = b.position - center;
            d.y = 0f;
            if (d.sqrMagnitude > r2) continue;
            OldPixelInfo info = b.GetComponent<OldPixelInfo>();
            if (info == null || info.heldByPlayer || Time.time < info.moldImmuneUntil) continue;
            OldPixelDespawn dsp = b.GetComponent<OldPixelDespawn>();
            if (dsp != null && dsp.IsDespawning) continue;
            if (b.GetComponent<PixelMoldy>() == null) b.gameObject.AddComponent<PixelMoldy>().Setup(this);
        }
    }

    private void OnDestroy() { if (disc != null) Destroy(disc.gameObject); if (Instance == this) Instance = null; }

    // ---- Glowing finds ----

    /// <summary>Called for every fresh old pixel: Berry / Mushroom / Herb / Truffle sometimes become glowing variants.</summary>
    public static void TryAttachGlow(GameObject copy, PixelClicker.PixelType type)
    {
        if (Instance == null || Instance.disableGlow) return;
        if (type != PixelClicker.PixelType.Berry && type != PixelClicker.PixelType.Mushroom && type != PixelClicker.PixelType.Herb && type != PixelClicker.PixelType.Truffle) return;
        if (Random.value > (Instance.glowChance > 0f ? Instance.glowChance : 0.12f)) return;
        Color c = type == PixelClicker.PixelType.Berry ? new Color(1f, 0.35f, 0.6f)
                : type == PixelClicker.PixelType.Mushroom ? new Color(1f, 0.65f, 0.35f)
                : type == PixelClicker.PixelType.Herb ? new Color(0.5f, 1f, 0.55f)
                : new Color(1f, 0.9f, 0.45f);
        copy.AddComponent<PixelForageGlow>().Setup(c, Instance.GlowStart);
    }

    /// <summary>A translucent unlit shell slightly bigger than the pixel (a child cube), used by the glow and the mold.</summary>
    public static Renderer MakeShell(Transform parent, float scale, out Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Shell";
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        material = new Material(PixelShaders.SpriteDefault());
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }
}

/// <summary>A glowing Foraging find: lies there plain, then glows (shell fades in and pulses) from halfway into its lifespan.</summary>
public class PixelForageGlow : MonoBehaviour
{
    private Color color;
    private float startFraction;
    private OldPixelDespawn despawn;
    private Material mat;
    private Renderer shell;

    public void Setup(Color c, float start) { color = c; startFraction = start; despawn = GetComponent<OldPixelDespawn>(); }

    private void Update()
    {
        if (despawn == null) { despawn = GetComponent<OldPixelDespawn>(); if (despawn == null) return; }
        float f = despawn.AgeFraction;
        if (f < startFraction) return;
        if (shell == null) shell = PixelMold.MakeShell(transform, 1.14f, out mat);
        float k = Mathf.Clamp01((f - startFraction) / 0.1f);
        Color c = color;
        c.a = k * (0.38f + 0.14f * Mathf.Sin(Time.time * 4f));
        mat.color = c;
    }
}

/// <summary>An old pixel with mold on it: its shell darkens until the mold kills it. Shake it while holding it to get the mold off.</summary>
public class PixelMoldy : MonoBehaviour
{
    private PixelMold owner;
    private float timer, shake;
    private Rigidbody body;
    private OldPixelInfo info;
    private OldPixelDespawn despawn;
    private Material mat;
    private Vector3 lastVelocity;

    public void Setup(PixelMold o)
    {
        owner = o;
        body = GetComponent<Rigidbody>();
        info = GetComponent<OldPixelInfo>();
        despawn = GetComponent<OldPixelDespawn>();
        PixelMold.MakeShell(transform, 1.07f, out mat);
    }

    private void FixedUpdate()
    {
        if (owner == null || body == null) return;
        if (info != null && info.heldByPlayer)
        {
            // Shaking = the held body's speed changes a lot, back and forth.
#if UNITY_6000_0_OR_NEWER
            Vector3 v = body.linearVelocity;
#else
            Vector3 v = body.velocity;
#endif
            shake += (v - lastVelocity).magnitude;
            lastVelocity = v;
            if (shake >= owner.ShakeNeeded) Clean();
        }
        else { shake = Mathf.Max(0f, shake - 20f * Time.fixedDeltaTime); lastVelocity = Vector3.zero; }
    }

    private void Update()
    {
        if (owner == null) { Destroy(this); return; }
        bool held = info != null && info.heldByPlayer;
        if (!held && Time.timeScale > 0f && !PixelOldGuard()) timer += Time.deltaTime;
        float k = Mathf.Clamp01(timer / owner.KillSeconds);
        if (mat != null)
        {
            Color c = Color.Lerp(owner.MoldColor, owner.MoldRipeColor, k);   // (pale fuzz, never dark: a moldy husk must not look like a Truffle)
            c.a = Mathf.Lerp(0.35f, 0.8f, k) + (held ? 0.1f * Mathf.Sin(Time.time * 30f) : 0f);
            mat.color = c;
        }
        if (k >= 1f && (despawn == null || !despawn.IsDespawning))
        {
            if (despawn != null) despawn.Begin(); else Destroy(gameObject);
        }
    }

    private static bool PixelOldGuard() => OldPixelDespawn.Frozen || OldPixelDespawn.HoldAll || PixelPauseMenu.IsPaused;

    private void Clean()
    {
        if (info != null) info.moldImmuneUntil = Time.time + owner.CleanImmune;
        PixelFarmFx.Break(PixelClicker.PixelType.Mold, transform.position, transform.lossyScale.x, owner.MoldColor);
        PixelAudio.Play("click");
        owner.GiveCleanXp();   // cleaning a moldy husk trains Foraging
        Transform shellT = transform.Find("Shell");
        if (shellT != null) Destroy(shellT.gameObject);
        Destroy(this);
    }
}
