using UnityEngine;

/// <summary>
/// A grabbing-hand mouse cursor (open hand = "you can grab this", closed fist = "holding it"), drawn in code. Anything that wants it calls
/// <see cref="Request"/> every frame it applies; when nobody asks the normal cursor comes back. Used by the Storage Crate's handles.
/// </summary>
public class PixelHandCursor : MonoBehaviour
{
    public enum Shape { None = 0, Open = 1, Closed = 2 }

    private static PixelHandCursor instance;
    private static Shape requested;
    private static int requestFrame = -1;
    private static Texture2D openTex, closedTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; requested = Shape.None; requestFrame = -1; openTex = closedTex = null; }

    private Shape applied = Shape.None;

    /// <summary>Asks for the hand cursor this frame.</summary>
    public static void Request(Shape shape)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("Hand Cursor");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PixelHandCursor>();
        }
        requested = shape;
        requestFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        Shape want = requestFrame >= Time.frameCount - 1 && !PixelFirstPerson.Active ? requested : Shape.None;
        if (want == applied) return;
        applied = want;
        if (want == Shape.None) { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); return; }
        Texture2D tex = want == Shape.Open ? (openTex != null ? openTex : (openTex = Draw(false))) : (closedTex != null ? closedTex : (closedTex = Draw(true)));
        Cursor.SetCursor(tex, new Vector2(16f, 16f), CursorMode.Auto);
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
    }

    // ------------------------------------------------------------------
    // The pictures: white shapes with a dark outline (32 x 32).
    // ------------------------------------------------------------------

    private static Texture2D Draw(bool fist)
    {
        const int n = 32;
        bool[,] mask = new bool[n, n];   // [x, y from the top]

        void Capsule(float x0, float y0, float x1, float y1, float r)
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f), a = new Vector2(x0, y0), b = new Vector2(x1, y1);
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    if ((p - (a + ab * t)).magnitude <= r) mask[x, y] = true;
                }
        }
        void Block(float x0, float y0, float x1, float y1)
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (x + 0.5f >= x0 && x + 0.5f <= x1 && y + 0.5f >= y0 && y + 0.5f <= y1) mask[x, y] = true;
        }

        if (!fist)
        {
            // Open hand: four fingers, a palm and a thumb.
            Capsule(10f, 8f, 10f, 15f, 2.2f);
            Capsule(14f, 6f, 14f, 15f, 2.2f);
            Capsule(18f, 7f, 18f, 15f, 2.2f);
            Capsule(22f, 10f, 22f, 16f, 2.1f);
            Block(8f, 14f, 24f, 24f);
            Capsule(9f, 24f, 22f, 24f, 3.2f);
            Capsule(8.5f, 20f, 4.5f, 14.5f, 2.3f);
        }
        else
        {
            // Closed fist: a block with four knuckle bumps and the thumb across the front.
            Block(8f, 13f, 24f, 23f);
            Capsule(9.5f, 13f, 9.5f, 14f, 2.6f);
            Capsule(13.5f, 12.2f, 13.5f, 13f, 2.6f);
            Capsule(18f, 12.2f, 18f, 13f, 2.6f);
            Capsule(22f, 13f, 22f, 14f, 2.6f);
            Capsule(9f, 24f, 22f, 24f, 3.1f);
            Capsule(9.5f, 19.5f, 17f, 19.5f, 2.2f);
        }

        Color[] pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color c = new Color(0f, 0f, 0f, 0f);
                if (mask[x, y]) c = new Color(1f, 1f, 1f, 1f);
                else
                {
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < n && ny < n && mask[nx, ny]) { edge = true; break; }
                        }
                    if (edge) c = new Color(0.08f, 0.08f, 0.1f, 1f);
                }
                // Light seams between the fingers / the thumb, for a bit of detail.
                pixels[(n - 1 - y) * n + x] = c;
            }
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
