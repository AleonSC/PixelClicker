using UnityEngine;

/// <summary>
/// An old pixel that a Sorter spat out. While it is in the stream it has NO collision (its collider is a trigger), so the pixels of a
/// stream don't knock into each other, the ring, the cube or the pipe and fly off in all directions: they follow their ballistic path
/// (gravity still works). When it comes down to the floor it lands there: it is put on the surface, nudged away from any pixels that
/// already lie on that spot, and only then becomes solid (it ignores collisions with whatever it still overlaps, so nothing gets
/// shoved apart violently). Landing also counts as the pixel's first impact for look behaviours that wait for one (Water splashes, Glass shatters).
/// </summary>
public class OldPixelStream : MonoBehaviour
{
    private PixelClicker clicker;
    private BoxCollider box;
    private Rigidbody body;
    private float half, age;
    private bool landed;

    public void Setup(PixelClicker owner, BoxCollider collider)
    {
        clicker = owner;
        box = collider;
        body = GetComponent<Rigidbody>();
        Vector3 s = transform.lossyScale;
        half = Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 0.5f;
        if (box != null) box.isTrigger = true; // no collisions while it is in the stream
    }

    private static Vector3 VelocityOf(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocityOf(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    private void FixedUpdate()
    {
        if (landed || body == null || clicker == null) return;
        age += Time.fixedDeltaTime;

        Vector3 v = VelocityOf(body);
        // Safety: a pixel that never finds the floor (or falls out of the world) lands where it is.
        if (age > 10f || body.position.y < -60f) { Land(body.position, Vector3.up); return; }
        if (v.y > 0.05f) return; // still rising

        float reach = half + Mathf.Max(0f, -v.y) * Time.fixedDeltaTime * 1.5f + 0.04f;
        RaycastHit[] hits = Physics.RaycastAll(body.position, Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit ground = default;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || h.collider == box || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) continue; // other old pixels are not the ground
            if (clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue; // nor is the cube
            if (h.normal.y < 0.4f) continue;                                       // nor walls
            if (h.distance < best) { best = h.distance; ground = h; found = true; }
        }
        if (found) Land(ground.point, ground.normal);
    }

    private void Land(Vector3 point, Vector3 normal)
    {
        landed = true;
        Vector3 pos = new Vector3(point.x, point.y + half + 0.005f, point.z);

        // Pixels already lying on this spot: step away from them a little and ignore the collision with whatever still overlaps.
        Collider[] near = Physics.OverlapBox(pos, Vector3.one * (half * 1.05f), transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        Vector3 away = Vector3.zero;
        foreach (Collider c in near)
        {
            if (c == box || c.transform.IsChildOf(transform) || c.GetComponentInParent<OldPixelInfo>() == null) continue;
            Vector3 d = pos - c.bounds.center;
            d.y = 0f;
            away += d.sqrMagnitude > 0.0001f ? d.normalized : Random.insideUnitSphere;
        }
        if (away.sqrMagnitude > 0.0001f)
        {
            away.y = 0f;
            away.Normalize();
            pos += away * (half * 1.2f);
        }
        body.position = pos;
        transform.position = pos;

        Collider[] overlapping = Physics.OverlapBox(pos, Vector3.one * (half * 1.02f), transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in overlapping)
            if (c != box && !c.transform.IsChildOf(transform) && c.GetComponentInParent<OldPixelInfo>() != null)
                Physics.IgnoreCollision(box, c, true);

        Vector3 v = VelocityOf(body);
        SetVelocityOf(body, new Vector3(v.x * 0.4f + away.x * 0.4f, 0f, v.z * 0.4f + away.z * 0.4f)); // a gentle slide, no bounce
        body.angularVelocity *= 0.3f;
        if (box != null) box.isTrigger = false; // solid now

        // The landing counts as the impact that look behaviours wait for.
        OldPixelSplash splash = GetComponent<OldPixelSplash>();
        if (splash != null) { splash.Land(point); return; }
        OldPixelShatter shatter = GetComponent<OldPixelShatter>();
        if (shatter != null && shatter.StreamLand(point, normal)) return;
        PixelAudio.PlayScaled("pixel_land", 0.5f);
        Destroy(this);
    }
}
