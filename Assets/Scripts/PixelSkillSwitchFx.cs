// ---- System notes ----
// PixelSkillSwitchFx.cs
//   The spin-and-flash of the 'skill switch' test block (PixelClicker.ShowSkillSwitchBlock, called by PixelSkills.SetActive when another skill is chosen): added to the cube's pixel for
//   about a second - an extra fast spin (spinDegreesPerSecond, easing out) and a white point-light flash (flashIntensity, fading) so the swap reads as an event. Unscaled time; it
//   removes itself (and its light) when done, and adding it again restarts it. The scale pop comes from the normal materialize.
// ---------------------------------------------------
using UnityEngine;

public class PixelSkillSwitchFx : MonoBehaviour
{
    private const float Seconds = 0.9f, SpinDegreesPerSecond = 1080f, FlashIntensity = 7f;

    private float age;
    private Light flash;
    private float size = 1f;

    /// <summary>Starts (or restarts) the effect on this pixel. 'size' = the pixel's base size, so the flash reaches about as far as the block is wide.</summary>
    public static void Play(Transform pixel, float size)
    {
        if (pixel == null) return;
        PixelSkillSwitchFx fx = pixel.GetComponent<PixelSkillSwitchFx>();
        if (fx == null) fx = pixel.gameObject.AddComponent<PixelSkillSwitchFx>();
        fx.size = Mathf.Max(0.1f, size);
        fx.age = 0f;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(age / Seconds);
        if (k >= 1f) { Finish(); return; }

        float ease = (1f - k) * (1f - k);
        transform.Rotate(0f, SpinDegreesPerSecond * ease * Time.unscaledDeltaTime, 0f, Space.World);

        if (flash == null)
        {
            GameObject go = new GameObject("Skill Switch Flash");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            flash = go.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = Color.white;
            flash.shadows = LightShadows.None;
            flash.range = Mathf.Max(3f, size * 5f);
        }
        // A quick bright pulse that dies away, with a second smaller blink part-way so it reads as a flash, not a glow.
        float pulse = Mathf.Clamp01(1f - k * 1.4f) + 0.5f * Mathf.Max(0f, Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k);
        flash.intensity = FlashIntensity * pulse;
    }

    private void Finish()
    {
        if (flash != null) Destroy(flash.gameObject);
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (flash != null) Destroy(flash.gameObject);
    }
}
