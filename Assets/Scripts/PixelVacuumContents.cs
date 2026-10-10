using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pixels a Vacuum pixel has sucked up, shown as very small versions floating about inside its (see-through) old copy.
/// Added by <see cref="PixelClicker"/> to the old Vacuum pixel that drops right after a vacuum click; the tiny pixels follow the cube's own
/// swelling / shrinking because they are its children and are removed with it.
/// </summary>
public class PixelVacuumContents : MonoBehaviour
{
    private struct Bit
    {
        public Transform t;
        public Vector3 home, spin;
        public float phase, speed;
    }

    private readonly List<Bit> bits = new List<Bit>();

    /// <summary>Fills the cube with a tiny display pixel for each tier index in 'tiers' (size = fraction of the cube's edge).</summary>
    public void Fill(PixelClicker clicker, IList<int> tiers, float size)
    {
        float range = Mathf.Max(0.02f, 0.5f - size * 0.75f - 0.02f); // keep them inside the walls
        for (int i = 0; i < tiers.Count; i++)
        {
            GameObject model = clicker.CreateDisplayPixel(tiers[i], transform, size);
            if (model == null) continue;
            Vector3 home = new Vector3(Random.Range(-range, range), Random.Range(-range, range), Random.Range(-range, range));
            model.transform.localPosition = home;
            model.transform.localRotation = Random.rotation;
            bits.Add(new Bit
            {
                t = model.transform, home = home, phase = Random.value * 6.28f, speed = Random.Range(0.8f, 1.8f),
                spin = Random.onUnitSphere * Random.Range(30f, 120f),
            });
        }
    }

    private void Update()
    {
        float time = Time.time;
        for (int i = 0; i < bits.Count; i++)
        {
            Bit b = bits[i];
            if (b.t == null) continue;
            float s = time * b.speed + b.phase;
            b.t.localPosition = b.home + new Vector3(Mathf.Sin(s), Mathf.Sin(s * 1.3f + 1f), Mathf.Cos(s * 0.9f)) * 0.04f; // gentle drifting
            b.t.Rotate(b.spin * Time.deltaTime, Space.Self);
        }
    }
}
