// ---- System notes ----
// PixelAimCursor.cs
//   The AIM CURSOR for edge-click aiming (PixelBurstPop): while the pointer is on the clickable cube (PixelClicker.TryGetBlockAt, isLive) the system
//   cursor is hidden and a code-drawn cursor follows the pointer on its own top canvas:
//   - near the middle of the cube (where husks scatter at random) a small spinning CUBE;
//   - towards an edge (PixelClicker.TryGetAim sharpness > 0) an ARROW pointing the way the husk will fly, turning smoothly as the pointer moves round
//     the edge; the cube fades into the arrow as the aim sharpens (the arrow grows with it), tinted by the current block's colour;
//   - each click gives it a little punch; it pops in when the pointer reaches the cube.
//   Anywhere else (off the cube, over UI, menus, first person, clicks blocked, edge aiming off) the normal cursor is back. Only shows the cursor it
//   hid (Cursor.visible) - first person keeps its own. disableAimCursor switches it off. A system component (PixelSystems, UI group).
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

public class PixelAimCursor : MonoBehaviour
{
    [Tooltip("Switch the aim cursor off (the normal cursor stays over the cube).")]
    [SerializeField] private bool disableAimCursor = false;
    [Tooltip("Size of the cursor pictures on screen, in pixels at 1080p.")]
    [SerializeField] private float cursorSize = 44f;
    [Tooltip("How quickly the arrow turns to follow the pointer round the edge (higher = snappier).")]
    [SerializeField] private float turnSpeed = 18f;
    [Range(0f, 1f)]
    [Tooltip("How much the arrow takes the current block's colour (0 = always white).")]
    [SerializeField] private float tintStrength = 0.65f;

    private PixelClicker clicker;
    private GameObject canvasObject;
    private RectTransform canvasRect, root, arrowRect, cubeRect;
    private Image arrowImage, cubeImage;
    private bool hidCursor, wasOn;
    private float angle, show, aimBlend, punch, spin;

    private void Awake() { clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }

    private void OnDisable() { Restore(); if (canvasObject != null) canvasObject.SetActive(false); }
    private void OnDestroy() { Restore(); if (canvasObject != null) Destroy(canvasObject); }

    private void Restore()
    {
        if (hidCursor) { Cursor.visible = true; hidCursor = false; }
    }

    private void LateUpdate()
    {
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        Vector2 pointer = PixelInput.PointerPosition();
        Vector2 dir = Vector2.right;
        float sharp = 0f;
        bool on = !disableAimCursor && clicker != null && Application.isFocused
                  && !PixelCalm.InMenu && !PixelFirstPerson.Active && !PixelInput.PointerOverUI()
                  && !PixelClicker.ExternalClickBlock && !PixelCameraIntro.ClicksLocked;
        if (on && (!clicker.TryGetBlockAt(pointer, out _, out bool isLive, out _) || !isLive)) on = false;
        if (on && !clicker.TryGetAim(pointer, out dir, out sharp)) on = false;
        if (!on)
        {
            if (wasOn) { wasOn = false; Restore(); }
            if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false);   // (the system cursor is back at once: the drawn one just goes)
            return;
        }
        if (canvasObject == null) Build();
        if (!canvasObject.activeSelf) canvasObject.SetActive(true);
        if (!wasOn) { wasOn = true; show = 0f; angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; }
        if (Cursor.visible) { Cursor.visible = false; hidCursor = true; }

        float dt = Time.unscaledDeltaTime;
        show = Mathf.MoveTowards(show, 1f, dt * 8f);
        aimBlend = Mathf.MoveTowards(aimBlend, sharp, dt * 6f);
        float want = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle = Mathf.LerpAngle(angle, want, 1f - Mathf.Exp(-turnSpeed * dt));
        if (PixelInput.LeftPressed()) punch = 1f;
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        spin += dt * 90f;

        // Follow the pointer (overlay canvas: screen pixels -> canvas units).
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer, null, out Vector2 local)) root.anchoredPosition = local;
        float pop = EaseOutBack(show) * (1f + 0.25f * punch);
        root.localScale = Vector3.one * pop;

        Color tint = Color.white;
        if (clicker.IsValidTierIndex(clicker.CurrentClickTierIndex)) tint = Color.Lerp(Color.white, clicker.Tiers[clicker.CurrentClickTierIndex].UIColor, tintStrength);
        float a = Mathf.SmoothStep(0f, 1f, aimBlend);
        arrowImage.color = new Color(tint.r, tint.g, tint.b, a);
        cubeImage.color = new Color(tint.r, tint.g, tint.b, 1f - a);
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        arrowRect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, a);
        cubeRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(spin * Mathf.Deg2Rad) * 12f);
        cubeRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, a);
    }

    private void Build()
    {
        canvasObject = PixelUIKit.CreateCanvas("Aim Cursor", 32000, new Vector2(1920f, 1080f), false);
        canvasRect = canvasObject.GetComponent<RectTransform>();
        root = new GameObject("Cursor", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvasObject.transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        cubeImage = MakeImage("Cube", DrawCube(), new Vector2(0.5f, 0.5f), out cubeRect);
        arrowImage = MakeImage("Arrow", DrawArrow(), new Vector2(0.12f, 0.5f), out arrowRect);   // (pivot near the tail: the arrow points away from the pointer)
    }

    private Image MakeImage(string name, Texture2D tex, Vector2 pivot, out RectTransform rect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        Image img = go.GetComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        img.raycastTarget = false;
        rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = Vector2.one * cursorSize;
        rect.anchoredPosition = Vector2.zero;
        return img;
    }

    // ---- code-drawn pictures (white with a dark outline, tinted by the Image colour) ----

    private const int N = 64;

    private static Texture2D Finish(bool[,] mask)
    {
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool inside = mask[x, y], edge = false;
                if (!inside)
                    for (int dy = -3; dy <= 3 && !edge; dy++)
                        for (int dx = -3; dx <= 3 && !edge; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx >= 0 && sy >= 0 && sx < N && sy < N && mask[sx, sy] && dx * dx + dy * dy <= 9) edge = true;
                        }
                px[y * N + x] = inside ? new Color32(255, 255, 255, 255) : edge ? new Color32(20, 20, 28, 230) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>An arrow pointing right: a shaft from the left and a wide head at the right.</summary>
    private static Texture2D DrawArrow()
    {
        bool[,] m = new bool[N, N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cy = Mathf.Abs(y + 0.5f - N * 0.5f);
                bool shaft = x >= 8 && x < 38 && cy <= 5f;
                float head = (x + 0.5f - 36f);   // the head: a triangle from x 36 to 58
                bool tri = head >= 0f && x < 58 && cy <= 18f * (1f - head / 22f);
                m[x, y] = shaft || tri;
            }
        return Finish(m);
    }

    /// <summary>A small cube seen from a corner: three faces (the dark outline draws the edges between them).</summary>
    private static Texture2D DrawCube()
    {
        bool[,] m = new bool[N, N];
        Vector2 c = new Vector2(N * 0.5f, N * 0.5f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // An isometric cube: a hexagon (corners top, upper / lower right, bottom, lower / upper left) with a 'Y' of gaps for the three
                // inner edges - centre to the bottom corner, centre to the upper-left and upper-right corners. The outline fills the gaps dark.
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float hx = Mathf.Abs(p.x), hy = Mathf.Abs(p.y);
                bool hex = hx <= 20f && hy <= 22f - hx * 0.5f;
                bool innerEdge = (hx < 1.2f && p.y < 0f) || (p.y >= 0f && Mathf.Abs(p.y - 0.6f * hx) < 1.2f);
                m[x, y] = hex && !innerEdge;
            }
        return Finish(m);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }
}
