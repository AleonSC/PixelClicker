// ---- System notes ----
// PixelUISparks.cs
//   ONE place for the gold screen sparkles of the 'find' shows (Dragon Find, Egg Hatch; 2026-10-24: was a copied block in each): a pool of
//   four-pointed star images on the show's canvas that drift, slow down, fall a little, spin and fade. A plain helper object a show keeps in a field.
//   - Build(parent, count, colour): makes the pool under 'parent' (call it where the sparkles should sit in the draw order); the star sprite is
//     drawn once and shared.
//   - Emit(at, pixels, perSecond, dt): sheds sparkles around a thing 'pixels' wide at 'at' (canvas units), 'perSecond' of them.
//   - Burst(at, count, minSpeed, maxSpeed, sizeScale): a ring of sparkles flying out at once.
//   - Tick(dt): moves them; call every frame of the show. Drop(): forget the pool (the canvas that holds it is being destroyed).
//   (The skill level popup's fireworks are a different look - coloured discs - and keep their own pool.)
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

public class PixelUISparks
{
    private struct Spark
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos, vel;
        public float life, maxLife, size, spin;
        public bool alive;
    }

    private static Sprite sprite;   // (a texture: survives a restart, no reset needed)
    private Spark[] sparks;
    private Color color;
    private float emitCarry;

    /// <summary>Makes the pool of 'count' sparkle images under 'parent' (anchored bottom-left: positions are canvas units).</summary>
    public void Build(Transform parent, int count, Color sparkleColor)
    {
        color = sparkleColor;
        emitCarry = 0f;
        if (sprite == null) sprite = BuildSprite();
        sparks = new Spark[count];
        for (int i = 0; i < sparks.Length; i++)
        {
            GameObject go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = sprite;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            go.SetActive(false);
            sparks[i] = new Spark { rect = r, image = img };
        }
    }

    /// <summary>Forgets the pool (its images go with the canvas).</summary>
    public void Drop() { sparks = null; }

    /// <summary>Sheds sparkles around a thing 'pixels' wide at 'at', 'perSecond' of them a second.</summary>
    public void Emit(Vector2 at, float pixels, float perSecond, float dt)
    {
        emitCarry += perSecond * dt;
        while (emitCarry >= 1f)
        {
            emitCarry -= 1f;
            Vector2 dir = Random.insideUnitCircle;
            Spawn(at + dir * pixels * 0.5f, dir.normalized * Random.Range(30f, 150f) + Vector2.up * Random.Range(10f, 60f),
                  Random.Range(0.5f, 1.2f), Random.Range(14f, 34f));
        }
    }

    /// <summary>A burst of 'count' sparkles flying out from 'at'.</summary>
    public void Burst(Vector2 at, int count, float minSpeed, float maxSpeed, float sizeScale)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Spawn(at, dir * Random.Range(minSpeed, maxSpeed), Random.Range(0.6f, 1.4f), Random.Range(16f, 44f) * sizeScale);
        }
    }

    private void Spawn(Vector2 pos, Vector2 vel, float life, float size)
    {
        if (sparks == null) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i].alive) continue;
            sparks[i].alive = true;
            sparks[i].pos = pos; sparks[i].vel = vel;
            sparks[i].life = sparks[i].maxLife = life;
            sparks[i].size = size;
            sparks[i].spin = Random.Range(-180f, 180f);
            sparks[i].image.color = Color.Lerp(color, Color.white, Random.value * 0.6f);
            sparks[i].rect.gameObject.SetActive(true);
            return;
        }
    }

    /// <summary>Moves, shrinks and fades the live sparkles.</summary>
    public void Tick(float dt)
    {
        if (sparks == null) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (!sparks[i].alive) continue;
            sparks[i].life -= dt;
            if (sparks[i].life <= 0f) { sparks[i].alive = false; sparks[i].rect.gameObject.SetActive(false); continue; }
            sparks[i].vel *= 1f - Mathf.Min(1f, dt * 1.6f);
            sparks[i].vel += Vector2.down * 40f * dt;
            sparks[i].pos += sparks[i].vel * dt;
            float k = sparks[i].life / sparks[i].maxLife;
            float s = sparks[i].size * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 0.5f + 0.2f);
            sparks[i].rect.anchoredPosition = sparks[i].pos;
            sparks[i].rect.sizeDelta = new Vector2(s, s);
            sparks[i].rect.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * sparks[i].spin);
            Color col = sparks[i].image.color; col.a = Mathf.Clamp01(k * 1.6f); sparks[i].image.color = col;
        }
    }

    private static Sprite BuildSprite()
    {
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float glow = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                float cross = Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) * 7f) * Mathf.Clamp01(1f - r);   // four-pointed star
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(glow * 0.6f + cross)));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
