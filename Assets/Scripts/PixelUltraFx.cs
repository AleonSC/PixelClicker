using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The feedback for gaining an Ultra pixel, played where the pixel was: a gold "+1 Ultra Red" text that rises and fades, a burst of
/// sparkles, and the sound <c>ultra_gain</c> (silent until a clip is assigned in the PixelAudio sound list). Everything is drawn
/// in code and cleans itself up after about a second and a half. Called by <see cref="PixelMinigame.UltraRoll"/>.
/// </summary>
public class PixelUltraFx : MonoBehaviour
{
    private const float Seconds = 1.5f;
    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);

    /// <summary>Plays the effect at a world position. 'text' / 'sound' can be switched off when the caller already has its own.</summary>
    public static void Play(PixelClicker clicker, int tier, Vector3 at, bool text = true, bool sound = true)
    {
        if (clicker == null) return;
        if (sound) PixelAudio.Play("ultra_gain");
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;

        GameObject go = new GameObject("Ultra Gain Fx");
        PixelUltraFx fx = go.AddComponent<PixelUltraFx>();
        fx.StartCoroutine(fx.Run(clicker, tier, at, cam, text));
    }

    private IEnumerator Run(PixelClicker clicker, int tier, Vector3 at, Camera cam, bool showText)
    {
        // Sparkles: small gold squares thrown outwards in the camera's plane.
        Shader shader = PixelShaders.SpriteDefault();
        const int count = 12;
        Transform[] sparks = new Transform[count];
        Vector3[] dirs = new Vector3[count];
        float[] speeds = new float[count];
        Material mat = null;
        if (shader != null)
        {
            mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.color = Gold;
        }
        float unit = Mathf.Max(0.05f, clicker.PixelBaseSize);
        for (int i = 0; i < count; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(spark.GetComponent<Collider>());
            spark.transform.SetParent(transform, false);
            spark.transform.position = at;
            spark.transform.rotation = cam.transform.rotation;
            spark.transform.localScale = Vector3.one * unit * Random.Range(0.12f, 0.22f);
            if (mat != null) spark.GetComponent<Renderer>().sharedMaterial = mat;
            spark.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            dirs[i] = cam.transform.right * Mathf.Cos(angle) + cam.transform.up * Mathf.Sin(angle);
            speeds[i] = unit * Random.Range(0.9f, 1.8f);
            sparks[i] = spark.transform;
        }

        // Rising text.
        TextMeshPro label = null;
        if (showText)
        {
            GameObject tgo = new GameObject("Ultra Gain Text");
            tgo.transform.SetParent(transform, false);
            label = tgo.AddComponent<TextMeshPro>();
            string name = clicker.IsValidTierIndex(tier) ? clicker.Tiers[tier].displayName : "";
            label.text = "+1 Ultra " + name;
            label.fontSize = 4f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Gold;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(40, 25, 0, 255);
            if (clicker.UIFont != null) label.font = clicker.UIFont;
            label.rectTransform.sizeDelta = new Vector2(12f, 3f);
        }

        float t = 0f;
        while (t < Seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Seconds);

            for (int i = 0; i < count; i++)
            {
                if (sparks[i] == null) continue;
                float burst = 1f - (1f - Mathf.Min(1f, k * 2.2f)) * (1f - Mathf.Min(1f, k * 2.2f)); // fast out, then slows
                sparks[i].position = at + dirs[i] * (speeds[i] * burst);
                sparks[i].rotation = cam.transform.rotation;
                sparks[i].localScale = Vector3.one * unit * (0.2f * (1f - k));
            }

            if (label != null)
            {
                Vector3 p = at + Vector3.up * (unit * (0.8f + 1.6f * k));
                label.transform.position = PixelUIKit.KeepOnScreen(cam, label, p);
                label.transform.rotation = cam.transform.rotation;
                label.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            }
            yield return null;
        }
        if (mat != null) Destroy(mat);
        Destroy(gameObject);
    }
}
