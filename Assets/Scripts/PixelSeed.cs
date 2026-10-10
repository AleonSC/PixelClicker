using UnityEngine;

/// <summary>
/// A sprout that came out of a cracked Seed shell. It tumbles down, "plants" itself upright where it lands, then grows a random old
/// pixel from nothing on its tip over a few seconds, holds it a moment (wobbling) and finally pops it off: the pixel becomes an ordinary
/// old pixel of that random type (with its normal payout) and the sprout shrinks away. Created by <see cref="PixelClicker"/>.
/// </summary>
public class SeedSprout : MonoBehaviour
{
    private enum Stage { Falling, Growing, Holding, Shrinking }

    /// <summary>How many sprouts exist right now (PixelClicker caps it).</summary>
    public static int Count { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Count = 0; }

    private PixelClicker clicker;
    private Rigidbody body;
    private Collider hit;
    private Transform visual;      // the sprout mesh
    private GameObject pixel;      // the growing pixel (a display model)
    private Stage stage = Stage.Falling;
    private float unit, growSeconds, holdSeconds, age, stageTime;
    private int tier = -1;
    private double amount;
    private float yaw;

    private void OnEnable() => Count++;
    private void OnDisable() => Count = Mathf.Max(0, Count - 1);

    public void Setup(PixelClicker owner, float pixelSize, float grow, float hold)
    {
        clicker = owner;
        unit = Mathf.Max(0.01f, pixelSize);
        growSeconds = Mathf.Max(0.5f, grow);
        holdSeconds = Mathf.Max(0f, hold);
        body = GetComponent<Rigidbody>();
        hit = GetComponent<Collider>();
        visual = PixelLooks.CreateSproutObject(transform, unit).transform;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (stage != Stage.Falling || collision.contactCount == 0) return;
        if (collision.GetContact(0).normal.y > 0.5f) Plant(); // landed on something flat
    }

    private void Update()
    {
        if (clicker == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;

        switch (stage)
        {
            case Stage.Falling:
                if (age > 6f || transform.position.y < -50f) Plant(); // never lands: plant it where it is
                break;

            case Stage.Growing:
            {
                stageTime += Time.deltaTime;
                float k = Mathf.Clamp01(stageTime / growSeconds);
                float ease = k * k * (3f - 2f * k);
                if (pixel != null)
                {
                    float size = unit * ease;
                    pixel.transform.localScale = Vector3.one * Mathf.Max(0.0001f, size);
                    pixel.transform.localPosition = new Vector3(0f, unit * PixelLooks.SproutStemTop + size * 0.5f, 0f);
                    pixel.transform.localRotation = Quaternion.Euler(0f, stageTime * 50f, 0f);
                }
                // The sprout sways a little while it works.
                transform.rotation = Quaternion.Euler(Mathf.Sin(stageTime * 3f) * 3f, yaw, Mathf.Cos(stageTime * 2.3f) * 3f);
                if (k >= 1f) { stage = Stage.Holding; stageTime = 0f; }
                break;
            }

            case Stage.Holding:
            {
                stageTime += Time.deltaTime;
                float shake = Mathf.Clamp01(stageTime / Mathf.Max(0.01f, holdSeconds)); // wobbles harder as it is about to pop
                if (pixel != null)
                {
                    pixel.transform.localRotation = Quaternion.Euler(Mathf.Sin(stageTime * 32f) * 8f * shake, stageTime * 50f, Mathf.Cos(stageTime * 29f) * 8f * shake);
                    pixel.transform.localPosition = new Vector3(0f, unit * PixelLooks.SproutStemTop + unit * 0.5f + shake * unit * 0.05f * Mathf.Abs(Mathf.Sin(stageTime * 14f)), 0f);
                }
                if (stageTime >= holdSeconds) PopOff();
                break;
            }

            case Stage.Shrinking:
            {
                stageTime += Time.deltaTime;
                float k = Mathf.Clamp01(stageTime / 0.5f);
                transform.localScale = Vector3.one * (1f - k);
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
    }

    /// <summary>Stands the sprout upright, switches off its physics and starts growing the pixel.</summary>
    private void Plant()
    {
        if (stage != Stage.Falling) return;
        if (body != null)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        if (hit != null) hit.enabled = false;
        yaw = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.position += Vector3.down * (unit * 0.03f); // pressed slightly into the ground
        PixelAudio.PlayScaled("pixel_land", 0.6f);

        tier = clicker.RandomSeedPixelTier();
        if (tier < 0) { stage = Stage.Shrinking; stageTime = 0f; return; }
        amount = clicker.RollOldPixelAmount(tier);
        pixel = clicker.CreateDisplayPixel(tier, transform, unit);
        if (pixel != null) pixel.transform.localScale = Vector3.one * 0.0001f;
        stage = Stage.Growing;
        stageTime = 0f;
    }

    /// <summary>The grown pixel pops off the sprout as an ordinary old pixel of its type.</summary>
    private void PopOff()
    {
        Vector3 at = pixel != null ? pixel.transform.position : transform.position + Vector3.up * unit;
        if (pixel != null) Destroy(pixel);
        pixel = null;
        Vector2 side = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.2f);
        clicker.SpawnStoredPixel(tier, amount, at, new Vector3(side.x, Random.Range(2.5f, 4f), side.y));
        PixelAudio.Play("click");
        PixelStats.Count("seed.grown");
        stage = Stage.Shrinking;
        stageTime = 0f;
    }
}
