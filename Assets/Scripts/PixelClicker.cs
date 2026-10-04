using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Pixel Clicker - core clicking loop.
///
/// Flow: player clicks the 3D pixel (cube) -> currency of the active tier is added ->
/// a copy of the pixel falls away -> the real pixel re-materializes (scale-in) with the
/// colour of the active tier.
///
/// Tiers (White -> Gray -> Black) live in a single list so adding new ones
/// (e.g. R, G, B) later is just a matter of adding an entry in the Inspector
/// and, optionally, a new value at the end of <see cref="PixelType"/>.
/// </summary>
public class PixelClicker : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Types
    // ------------------------------------------------------------------

    /// <summary>Currency identifiers. Append new values at the END to keep Inspector data valid.</summary>
    public enum PixelType
    {
        White = 0,
        Gray = 1,
        Black = 2,
        // Future: Red, Green, Blue ...
    }

    /// <summary>Everything that defines one currency/tier. Fully editable in the Inspector.</summary>
    [Serializable]
    public class PixelTier
    {
        [Tooltip("Which currency this tier represents.")]
        public PixelType type = PixelType.White;

        [Tooltip("Display name used by the UI.")]
        public string displayName = "White Pixels";

        [Tooltip("Colour applied to the pixel cube while this tier is active.")]
        public Color color = Color.white;

        [Tooltip("Currency gained per click on this tier.")]
        public double amountPerClick = 1;

        [Tooltip("Lifetime amount of the PREVIOUS tier needed to unlock this tier. Ignored if 'Unlocked At Start' is on.")]
        public double unlockThreshold = 10;

        [Tooltip("Tier is available from the very beginning.")]
        public bool unlockedAtStart = false;

        [Tooltip("Optional: one TMP label showing this tier's current count. Leave empty to skip.")]
        public TMP_Text countLabel;

        [Tooltip("Optional: sound played when clicking while this tier is active. Falls back to the default click sound.")]
        public AudioClip clickSound;

        // Runtime state (visible in the Inspector for debugging, editable at runtime).
        [Header("Runtime State")]
        [Tooltip("Current spendable amount.")]
        public double count;

        [Tooltip("Total ever collected (never decreases when spending). Used for unlock thresholds.")]
        public double totalCollected;

        [Tooltip("Has this tier been unlocked?")]
        public bool unlocked;
    }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Pixel Object")]
    [Tooltip("The cube the player clicks. Defaults to this GameObject if empty.")]
    [SerializeField] private Transform pixelTransform;

    [Tooltip("Renderer of the pixel. Auto-found on the pixel if empty.")]
    [SerializeField] private Renderer pixelRenderer;

    [Tooltip("Camera used for raycasting. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Layers the click raycast can hit.")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    [Tooltip("Max raycast distance.")]
    [SerializeField] private float maxRayDistance = 100f;

    [Tooltip("Name of the colour property on the material (URP Lit = _BaseColor, Built-in = _Color).")]
    [SerializeField] private string colorPropertyName = "_BaseColor";

    [Header("Tiers / Currencies")]
    [Tooltip("Ordered list of tiers. Index 0 is the first currency. Each tier unlocks from the previous one.")]
    [SerializeField] private PixelTier[] tiers =
    {
        new PixelTier { type = PixelType.White, displayName = "White Pixels", color = Color.white,
                        amountPerClick = 1, unlockedAtStart = true, unlockThreshold = 0 },
        new PixelTier { type = PixelType.Gray,  displayName = "Gray Pixels",  color = new Color(0.5f, 0.5f, 0.5f),
                        amountPerClick = 1, unlockThreshold = 10 },
        new PixelTier { type = PixelType.Black, displayName = "Black Pixels", color = Color.black,
                        amountPerClick = 1, unlockThreshold = 10 },
    };

    [Tooltip("If on, clicks always produce the highest unlocked tier. If off, use SetActiveTier() (e.g. from a button).")]
    [SerializeField] private bool autoUseHighestTier = true;

    [Tooltip("Index of the tier clicks currently produce (used when Auto Use Highest Tier is off).")]
    [SerializeField] private int activeTierIndex = 0;

    [Tooltip("Global multiplier applied to every click. Hook shops/upgrades into this later.")]
    [SerializeField] private double clickMultiplier = 1;

    [Header("UI (optional)")]
    [Tooltip("Label for a combined summary of all unlocked currencies.")]
    [SerializeField] private TMP_Text summaryLabel;

    [Tooltip("Text format for the summary line. {0} = name, {1} = amount.")]
    [SerializeField] private string summaryLineFormat = "{0}: {1}";

    [Tooltip("Label that shows hints like 'Collect 10 White Pixels to unlock Gray'.")]
    [SerializeField] private TMP_Text nextUnlockLabel;

    [Tooltip("Text for the next-unlock hint. {0} = required amount, {1} = current tier name, {2} = next tier name.")]
    [SerializeField] private string nextUnlockFormat = "Collect {0} {1} to unlock {2}";

    [Tooltip("Text shown when everything is unlocked.")]
    [SerializeField] private string allUnlockedText = "All tiers unlocked!";

    [Header("Respawn Animation")]
    [Tooltip("Spawn a falling copy of the pixel on every click.")]
    [SerializeField] private bool spawnFallingCopy = true;

    [Header("Old Pixel Physics")]
    [Tooltip("Seconds before an old pixel is destroyed. 0 = never.")]
    [SerializeField] private float fallingCopyLifetime = 6f;

    [Tooltip("Max old pixels kept in the scene. The oldest is destroyed first. 0 = no cap.")]
    [SerializeField] private int maxFallingCopies = 30;

    [Tooltip("Old pixels below this world Y are destroyed (catches pixels that fall off the world).")]
    [SerializeField] private float fallingCopyKillHeight = -50f;

    [Tooltip("Random launch speed range (min, max) in units/second.")]
    [SerializeField] private Vector2 popSpeedRange = new Vector2(1.5f, 4f);

    [Tooltip("Vertical part of the launch direction (min, max). -1 = straight down, 0 = sideways, 1 = straight up. Negative values pop it toward the ground.")]
    [SerializeField] private Vector2 popVerticalRange = new Vector2(-0.6f, 0.1f);

    [Tooltip("Random spin range (min, max) in radians/second, applied around a random axis.")]
    [SerializeField] private Vector2 popSpinRange = new Vector2(1f, 6f);

    [Tooltip("Mass of an old pixel. Affects how it pushes other rigidbodies.")]
    [SerializeField] private float fallingCopyMass = 1f;

    [Tooltip("Air resistance (linear). 0 = none.")]
    [SerializeField] private float fallingCopyDrag = 0f;

    [Tooltip("Air resistance on spin.")]
    [SerializeField] private float fallingCopyAngularDrag = 0.05f;

    [Tooltip("Gravity multiplier. 0 = floats, 1 = normal gravity (uses the project's gravity setting).")]
    [SerializeField] private float gravityScale = 1f;

    [Range(0f, 1f)]
    [Tooltip("Bounciness of old pixels (0 = no bounce, 1 = perfect bounce).")]
    [SerializeField] private float bounciness = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Friction of old pixels.")]
    [SerializeField] private float friction = 0.5f;

    [Tooltip("Continuous collision stops fast pixels tunneling through thin floors. Costs slightly more.")]
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

    [Tooltip("Layer old pixels are placed on (controls what they collide with in Project Settings > Physics). -1 = leave on Default.")]
    [SerializeField] private int fallingCopyLayer = -1;

    [Tooltip("Can old pixels block the click raycast? Off = clicks pass through them to the live pixel.")]
    [SerializeField] private bool oldPixelsBlockClicks = false;

    [Tooltip("Should old pixels collide with the live floating pixel?")]
    [SerializeField] private bool collideWithLivePixel = false;

    [Tooltip("Seconds the new pixel takes to materialize.")]
    [SerializeField] private float materializeDuration = 0.25f;

    [Tooltip("Scale curve over the materialize duration (0..1 time, 0..1 scale).")]
    [SerializeField] private AnimationCurve materializeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Ignore clicks while the pixel is still materializing.")]
    [SerializeField] private bool blockClicksWhileSpawning = false;

    [Header("Suspended In Midair")]
    [Tooltip("Keep the pixel floating: disables gravity on any Rigidbody on the pixel and makes it kinematic.")]
    [SerializeField] private bool suspendInMidair = true;

    [Tooltip("Gently bob up and down while floating.")]
    [SerializeField] private bool hoverBob = true;

    [Tooltip("How far (world units) the pixel bobs up and down.")]
    [SerializeField] private float hoverAmplitude = 0.1f;

    [Tooltip("Bob cycles per second.")]
    [SerializeField] private float hoverSpeed = 0.5f;

    [Tooltip("Slowly spin the pixel (degrees per second per axis). Set to 0 for no spin.")]
    [SerializeField] private Vector3 idleSpin = new Vector3(0f, 20f, 0f);

    [Header("Pulsing")]
    [Tooltip("Enable the pulsing scale effect.")]
    [SerializeField] private bool pulseEnabled = true;

    [Tooltip("How much the size changes. 0.05 = +/-5% of the base scale.")]
    [SerializeField] private float pulseAmount = 0.05f;

    [Tooltip("Pulses per second.")]
    [SerializeField] private float pulseSpeed = 1f;

    [Tooltip("Shape of one pulse (time 0..1 across a cycle, value -1..1). Default is a smooth sine-like wave.")]
    [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f * Mathf.PI * 0.5f),
        new Keyframe(0.25f, 1f, 0f, 0f),
        new Keyframe(0.75f, -1f, 0f, 0f),
        new Keyframe(1f, 0f, 2f * Mathf.PI * 0.5f, 0f));

    [Tooltip("Also pulse the pixel's brightness along with its size.")]
    [SerializeField] private bool pulseBrightness = false;

    [Tooltip("Brightness change at the pulse peak (0.1 = +/-10%). Only used if Pulse Brightness is on.")]
    [SerializeField] private float brightnessAmount = 0.1f;

    [Header("Effects")]
    [Tooltip("Particle system played at the pixel on click. Its start colour is set to the tier colour.")]
    [SerializeField] private ParticleSystem clickParticles;

    [Tooltip("Audio source used for sound effects. Added automatically if missing.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Default click sound.")]
    [SerializeField] private AudioClip defaultClickSound;

    [Tooltip("Sound played when a new tier unlocks.")]
    [SerializeField] private AudioClip unlockSound;

    [Range(0f, 1f)]
    [Tooltip("Volume of all sounds.")]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Random pitch variation (+/-) per click for variety.")]
    [SerializeField] private float pitchVariation = 0.05f;

    [Header("Events")]
    [Tooltip("Fired after every successful click.")]
    public UnityEvent onPixelClicked;

    [Tooltip("Fired when a tier unlocks. Passes the tier index.")]
    public UnityEvent<int> onTierUnlocked;

    [Tooltip("Fired whenever any currency amount changes (hook UI/save systems here).")]
    public UnityEvent onCurrencyChanged;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private Vector3 baseScale = Vector3.one;
    private Coroutine materializeRoutine;
    private bool isSpawning;
    private MaterialPropertyBlock propertyBlock;
    private int colorPropertyId;
    private Vector3 basePosition;
    private float materializeFactor = 1f;
    private Color currentColor = Color.white;

    // Old pixels currently in the scene (used for the cap and kill height).
    private readonly System.Collections.Generic.List<Rigidbody> oldPixels = new System.Collections.Generic.List<Rigidbody>();

    /// <summary>C# event version of onCurrencyChanged, handy for other scripts.</summary>
    public event Action CurrencyChanged;

    public PixelTier[] Tiers => tiers;
    public double ClickMultiplier { get => clickMultiplier; set => clickMultiplier = value; }

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (pixelTransform == null) pixelTransform = transform;
        if (pixelRenderer == null) pixelRenderer = pixelTransform.GetComponentInChildren<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (audioSource == null && (defaultClickSound != null || unlockSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        baseScale = pixelTransform.localScale;
        basePosition = pixelTransform.position;

        if (suspendInMidair)
        {
            Rigidbody body = pixelTransform.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = true;
            }
        }
        propertyBlock = new MaterialPropertyBlock();
        colorPropertyId = Shader.PropertyToID(colorPropertyName);

        // Apply start-unlocked flags.
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].unlockedAtStart) tiers[i].unlocked = true;
        }
    }

    private void Start()
    {
        ApplyPixelColor(GetClickTier().color);
        RefreshUI();
    }

    private void Update()
    {
        AnimatePixel();

        CleanOldPixels();

        if (!WasClickedThisFrame() || targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, clickableLayers);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Old pixels are skipped unless they're allowed to block clicks.
            if (!oldPixelsBlockClicks && hit.rigidbody != null && oldPixels.Contains(hit.rigidbody)) continue;

            if (hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform))
                Collect();
            return; // first non-ignored hit decides
        }
    }

    // ------------------------------------------------------------------
    // Old pixel housekeeping
    // ------------------------------------------------------------------

    /// <summary>Drops destroyed/out-of-bounds pixels from the list and enforces the cap.</summary>
    private void CleanOldPixels()
    {
        for (int i = oldPixels.Count - 1; i >= 0; i--)
        {
            Rigidbody b = oldPixels[i];
            if (b == null) { oldPixels.RemoveAt(i); continue; }
            if (b.position.y < fallingCopyKillHeight)
            {
                Destroy(b.gameObject);
                oldPixels.RemoveAt(i);
            }
        }
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private bool WasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }

    // ------------------------------------------------------------------
    // Core loop
    // ------------------------------------------------------------------

    /// <summary>Performs one click. Public so buttons or automation can call it too.</summary>
    public void Collect()
    {
        if (blockClicksWhileSpawning && isSpawning) return;

        int tierIndex = GetClickTierIndex();
        PixelTier tier = tiers[tierIndex];

        AddCurrency(tierIndex, tier.amountPerClick * clickMultiplier);

        PlayClickEffects(tier);
        if (spawnFallingCopy) SpawnFallingCopy();
        Materialize(GetClickTier().color); // re-read: the click may have unlocked a higher tier

        onPixelClicked?.Invoke();
    }

    /// <summary>Adds to a tier's amount (use negative via <see cref="TrySpend"/> instead) and checks unlocks.</summary>
    public void AddCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return;

        tiers[tierIndex].count += amount;
        tiers[tierIndex].totalCollected += amount;

        CheckUnlocks();
        NotifyChanged();
    }

    /// <summary>Spends currency if the player can afford it. Use this from shops later.</summary>
    public bool TrySpend(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount < 0 || tiers[tierIndex].count < amount) return false;
        tiers[tierIndex].count -= amount;
        NotifyChanged();
        return true;
    }

    /// <summary>Convenience overload using the enum.</summary>
    public bool TrySpend(PixelType type, double amount) => TrySpend(IndexOf(type), amount);

    /// <summary>Current spendable amount of a currency.</summary>
    public double GetCount(PixelType type)
    {
        int i = IndexOf(type);
        return i >= 0 ? tiers[i].count : 0;
    }

    /// <summary>Manually select which tier clicks produce (only used when Auto Use Highest Tier is off).</summary>
    public void SetActiveTier(int index)
    {
        if (!IsValidTier(index) || !tiers[index].unlocked) return;
        activeTierIndex = index;
        ApplyPixelColor(tiers[index].color);
        RefreshUI();
    }

    /// <summary>Unlock tier N once tier N-1's lifetime total reaches N's threshold.</summary>
    private void CheckUnlocks()
    {
        for (int i = 1; i < tiers.Length; i++)
        {
            if (tiers[i].unlocked) continue;
            if (tiers[i - 1].totalCollected >= tiers[i].unlockThreshold)
            {
                tiers[i].unlocked = true;
                PlaySound(unlockSound);
                onTierUnlocked?.Invoke(i);
            }
        }
    }

    // ------------------------------------------------------------------
    // Tier helpers
    // ------------------------------------------------------------------

    private bool IsValidTier(int i) => tiers != null && i >= 0 && i < tiers.Length;

    private int IndexOf(PixelType type)
    {
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].type == type) return i;
        return -1;
    }

    private int GetHighestUnlockedIndex()
    {
        for (int i = tiers.Length - 1; i >= 0; i--)
            if (tiers[i].unlocked) return i;
        return 0;
    }

    private int GetClickTierIndex()
    {
        if (autoUseHighestTier) return GetHighestUnlockedIndex();
        return IsValidTier(activeTierIndex) && tiers[activeTierIndex].unlocked
            ? activeTierIndex
            : GetHighestUnlockedIndex();
    }

    private PixelTier GetClickTier() => tiers[GetClickTierIndex()];

    // ------------------------------------------------------------------
    // Visuals
    // ------------------------------------------------------------------

    /// <summary>Applies float, spin and pulse every frame. Scale = base * materialize * pulse.</summary>
    private void AnimatePixel()
    {
        float time = Time.time;

        if (suspendInMidair && hoverBob)
        {
            pixelTransform.position = basePosition +
                Vector3.up * (Mathf.Sin(time * hoverSpeed * Mathf.PI * 2f) * hoverAmplitude);
        }

        if (idleSpin != Vector3.zero)
            pixelTransform.Rotate(idleSpin * Time.deltaTime, Space.Self);

        float pulse = 0f;
        if (pulseEnabled)
        {
            float cycle = Mathf.Repeat(time * pulseSpeed, 1f);
            pulse = pulseCurve.Evaluate(cycle); // -1..1
        }

        pixelTransform.localScale = baseScale * (materializeFactor * (1f + pulse * pulseAmount));

        if (pulseEnabled && pulseBrightness)
            ApplyPixelColor(currentColor * (1f + pulse * brightnessAmount), false);
    }

    private void ApplyPixelColor(Color color, bool remember = true)
    {
        if (remember) currentColor = color;
        if (pixelRenderer == null) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyId, color);
        // Also set the built-in name so either pipeline works if the property name is left default.
        propertyBlock.SetColor("_Color", color);
        pixelRenderer.SetPropertyBlock(propertyBlock);
    }

    private void Materialize(Color newColor)
    {
        if (materializeRoutine != null) StopCoroutine(materializeRoutine);
        materializeRoutine = StartCoroutine(MaterializeRoutine(newColor));
    }

    private IEnumerator MaterializeRoutine(Color newColor)
    {
        isSpawning = true;
        ApplyPixelColor(newColor);
        materializeFactor = 0f;

        float t = 0f;
        while (t < materializeDuration)
        {
            t += Time.deltaTime;
            float k = materializeDuration > 0f ? Mathf.Clamp01(t / materializeDuration) : 1f;
            materializeFactor = materializeCurve.Evaluate(k);
            yield return null;
        }

        materializeFactor = 1f;
        isSpawning = false;
        materializeRoutine = null;
    }

    /// <summary>Clones the visible pixel, adds real physics, and pops it out in a random direction.</summary>
    private void SpawnFallingCopy()
    {
        // Skip if the pixel is mid-materialize and basically invisible.
        if (pixelTransform.localScale.sqrMagnitude < 0.0001f) return;

        GameObject copy = new GameObject("OldPixel");
        copy.transform.SetPositionAndRotation(pixelTransform.position, pixelTransform.rotation);
        copy.transform.localScale = pixelTransform.lossyScale;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) copy.layer = fallingCopyLayer;

        // Copy only the visuals (mesh + material) so we don't duplicate this script.
        MeshFilter srcFilter = pixelRenderer != null ? pixelRenderer.GetComponent<MeshFilter>() : null;
        if (srcFilter != null && pixelRenderer != null)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;
            MeshRenderer mr = copy.AddComponent<MeshRenderer>();
            mr.sharedMaterial = pixelRenderer.sharedMaterial;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            pixelRenderer.GetPropertyBlock(block);
            mr.SetPropertyBlock(block);
        }

        // Collider with a physics material so bounce/friction are tweakable.
        BoxCollider box = copy.AddComponent<BoxCollider>();
#if UNITY_6000_0_OR_NEWER
        box.sharedMaterial = new PhysicsMaterial("OldPixel")
        {
            bounciness = bounciness,
            dynamicFriction = friction,
            staticFriction = friction,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            frictionCombine = PhysicsMaterialCombine.Average
        };
#else
        box.sharedMaterial = new PhysicMaterial("OldPixel")
        {
            bounciness = bounciness,
            dynamicFriction = friction,
            staticFriction = friction,
            bounceCombine = PhysicMaterialCombine.Maximum,
            frictionCombine = PhysicMaterialCombine.Average
        };
#endif

        // Keep the old pixel from colliding with / bumping the live one if requested.
        if (!collideWithLivePixel)
        {
            foreach (Collider live in pixelTransform.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(box, live);
        }

        Rigidbody rb = copy.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.0001f, fallingCopyMass);
        rb.useGravity = false; // gravity applied by ScaledGravity so gravityScale works
        rb.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = fallingCopyDrag;
        rb.angularDamping = fallingCopyAngularDrag;
#else
        rb.drag = fallingCopyDrag;
        rb.angularDrag = fallingCopyAngularDrag;
#endif
        if (gravityScale > 0f)
            copy.AddComponent<ScaledGravity>().scale = gravityScale;

        // Random direction: random heading on the ground plane, random vertical component.
        Vector2 flat = UnityEngine.Random.insideUnitCircle;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
        flat.Normalize();
        float vertical = UnityEngine.Random.Range(popVerticalRange.x, popVerticalRange.y);
        Vector3 direction = new Vector3(flat.x, vertical, flat.y).normalized;
        float speed = UnityEngine.Random.Range(popSpeedRange.x, popSpeedRange.y);

        rb.AddForce(direction * speed, ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                     ForceMode.VelocityChange);

        if (fallingCopyLifetime > 0f) Destroy(copy, fallingCopyLifetime);

        oldPixels.Add(rb);
        while (maxFallingCopies > 0 && oldPixels.Count > maxFallingCopies)
        {
            Rigidbody oldest = oldPixels[0];
            oldPixels.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
    }

    private void PlayClickEffects(PixelTier tier)
    {
        if (clickParticles != null)
        {
            ParticleSystem.MainModule main = clickParticles.main;
            main.startColor = tier.color;
            clickParticles.transform.position = pixelTransform.position;
            clickParticles.Play();
        }

        PlaySound(tier.clickSound != null ? tier.clickSound : defaultClickSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, soundVolume);
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void NotifyChanged()
    {
        RefreshUI();
        onCurrencyChanged?.Invoke();
        CurrencyChanged?.Invoke();
    }

    /// <summary>Refreshes every assigned label. Safe to call any time.</summary>
    public void RefreshUI()
    {
        if (tiers == null) return;

        System.Text.StringBuilder sb = summaryLabel != null ? new System.Text.StringBuilder() : null;

        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            string amount = FormatNumber(t.count);

            if (t.countLabel != null)
            {
                t.countLabel.gameObject.SetActive(t.unlocked);
                t.countLabel.text = string.Format(summaryLineFormat, t.displayName, amount);
            }

            if (sb != null && t.unlocked)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.AppendFormat(summaryLineFormat, t.displayName, amount);
            }
        }

        if (summaryLabel != null) summaryLabel.text = sb.ToString();

        if (nextUnlockLabel != null)
        {
            int next = -1;
            for (int i = 1; i < tiers.Length; i++)
            {
                if (!tiers[i].unlocked) { next = i; break; }
            }

            nextUnlockLabel.text = next < 0
                ? allUnlockedText
                : string.Format(nextUnlockFormat, FormatNumber(tiers[next].unlockThreshold),
                                tiers[next - 1].displayName, tiers[next].displayName);
        }
    }

    /// <summary>Compact number formatting (1.2K, 3.4M ...). Replace with your own for big-number support.</summary>
    private static string FormatNumber(double value)
    {
        if (value < 1000) return Math.Floor(value).ToString("0");
        string[] suffix = { "", "K", "M", "B", "T" };
        int s = 0;
        while (value >= 1000 && s < suffix.Length - 1) { value /= 1000; s++; }
        return value.ToString("0.##") + suffix[s];
    }

    // ------------------------------------------------------------------
    // Editor helpers
    // ------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tiers != null && tiers.Length > 0)
            activeTierIndex = Mathf.Clamp(activeTierIndex, 0, tiers.Length - 1);
        materializeDuration = Mathf.Max(0f, materializeDuration);
        fallingCopyLifetime = Mathf.Max(0f, fallingCopyLifetime);
        maxFallingCopies = Mathf.Max(0, maxFallingCopies);
    }
#endif
}

/// <summary>
/// Tiny helper added at runtime to old pixels so their gravity can be scaled
/// (Unity's Rigidbody only has on/off gravity).
/// </summary>
public class ScaledGravity : MonoBehaviour
{
    [HideInInspector] public float scale = 1f;
    private Rigidbody body;

    private void Awake() => body = GetComponent<Rigidbody>();

    private void FixedUpdate()
    {
        if (body != null && !body.isKinematic)
            body.AddForce(Physics.gravity * scale, ForceMode.Acceleration);
    }
}
