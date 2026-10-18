// ---- System notes ----
// PixelTitleCubeShow.cs
//   Part of partial class PixelTitleScreen: while the main menu shows, the cube behind it changes to a random pixel type on every beat of cubeShowBpm (90 bpm = every 0.67 s) -
//   every type in the game (locked ones too; retired ores left out) - through PixelClicker.ShowLookPreview (look only: the game does not change). When
//   Play is pressed (after a Continue has loaded the save) the menu cube POPS OFF like a husk - a snapshot of its mesh / materials thrown out sideways
//   and up that spins and shrinks away within huskPopSeconds (0.6 s), on unscaled time, no physics, never a real husk - and the player's own block
//   materializes in its place (EndLookPreview); then the usual camera intro plays. cubeShow off = the menu shows the real block as before.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PixelTitleScreen
{
    [Header("Menu cube show")]
    [Tooltip("While the main menu shows, the cube changes to a random pixel type (any in the game) every few seconds; on Play it pops off like a husk and your own block appears.")]
    [SerializeField] private bool cubeShow = true;
    [Tooltip("Pace of the menu cube's changes in beats per minute: one change per beat (90 = every 0.67 s).")]
    [SerializeField] private float cubeShowBpm = 90f;
    [Tooltip("Seconds the menu cube's husk takes to fly off and vanish when Play is pressed.")]
    [SerializeField] private float huskPopSeconds = 0.6f;

    private float cubeShowTimer;
    private int cubeShowTier = -1;
    private readonly List<int> cubeShowPool = new List<int>();

    /// <summary>Called from Update while the screen shows: the next random type on every beat (cubeShowBpm).</summary>
    private void UpdateCubeShow()
    {
        if (!cubeShow || playing) return;
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null) return;
        cubeShowTimer -= Time.unscaledDeltaTime;
        if (cubeShowTimer > 0f) return;
        // Add the beat instead of setting it, so the changes keep a steady tempo; after a long hitch it starts a fresh beat.
        float beat = 60f / Mathf.Clamp(cubeShowBpm, 10f, 300f);
        cubeShowTimer += beat;
        if (cubeShowTimer <= 0f) cubeShowTimer = beat;
        if (cubeShowPool.Count == 0)
            for (int i = 0; i < clicker.Tiers.Length; i++)
                if (clicker.Tiers[i] != null && !PixelClicker.IsRetiredOre(clicker.Tiers[i].type)) cubeShowPool.Add(i);
        if (cubeShowPool.Count == 0) return;
        int pick = cubeShowPool[Random.Range(0, cubeShowPool.Count)];
        if (pick == cubeShowTier && cubeShowPool.Count > 1) pick = cubeShowPool[(cubeShowPool.IndexOf(pick) + 1) % cubeShowPool.Count];
        cubeShowTier = pick;
        clicker.ShowLookPreview(pick);
    }

    /// <summary>Play was pressed: the menu cube pops off as a husk and the player's block comes back.</summary>
    private void EndCubeShow()
    {
        cubeShowTimer = 0f;
        cubeShowTier = -1;
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null || !clicker.LookPreviewActive) return;
        PopHusk(clicker);
        clicker.EndLookPreview();
    }

    private void PopHusk(PixelClicker clicker)
    {
        Renderer r = clicker.MainRenderer;
        MeshFilter mf = r != null ? r.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return;
        GameObject husk = new GameObject("Menu Cube Husk");
        husk.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
        husk.transform.localScale = r.transform.lossyScale;
        // Copies of the mesh and materials: the cube's own ones change the moment the player's block materializes.
        Mesh mesh = Instantiate(mf.sharedMesh);
        husk.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = husk.AddComponent<MeshRenderer>();
        Material[] mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) if (mats[i] != null) mats[i] = new Material(mats[i]);
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        StartCoroutine(HuskFlight(husk, clicker.PixelBaseSize));
    }

    private IEnumerator HuskFlight(GameObject husk, float size)
    {
        Camera cam = Camera.main;
        Vector3 side = cam != null ? cam.transform.right : Vector3.right;
        Vector3 vel = (side * (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.6f, 2.4f) + Vector3.up * Random.Range(2.2f, 3f)) * size;
        Vector3 spin = Random.onUnitSphere * Random.Range(360f, 720f);
        Vector3 scale0 = husk.transform.localScale;
        float t = 0f, dur = Mathf.Max(0.1f, huskPopSeconds);
        while (t < dur && husk != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            vel += Vector3.down * 9.81f * size * dt;
            husk.transform.position += vel * dt;
            husk.transform.Rotate(spin * dt, Space.World);
            float k = Mathf.Clamp01(t / dur);
            float shrink = k < 0.25f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.25f) / 0.75f);   // a short moment at full size, then it shrinks away
            husk.transform.localScale = scale0 * (0.75f * Mathf.Max(0f, shrink));                    // (husk sized from the start)
            yield return null;
        }
        if (husk != null)
        {
            MeshRenderer mr = husk.GetComponent<MeshRenderer>();
            if (mr != null) foreach (Material m in mr.sharedMaterials) if (m != null) Destroy(m);
            MeshFilter mf = husk.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            Destroy(husk);
        }
    }
}
