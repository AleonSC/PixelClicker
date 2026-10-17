// ---- System notes ----
// PixelTinyRobot.cs
//   TINY ROBOT (a Dev Hub > Tester feature for now, PixelFeatureTester 'Tiny Robot'; meant to become an Electrician-unlocked consumable): pick a SPOT ON A CONVEYOR BELT (the ghost snaps to the belt under the mouse, PixelTinyRobot.TrySnap; a click that is
//   not on a belt does nothing) and a little hover robot appears over it. For robotSeconds (default 60) it keeps fetching the NEAREST loose old pixel to that spot (within reachCubes, default 18): it flies over, grabs it (the pixel is made kinematic and
//   hangs under the robot), flies back and puts it down on the belt at the spot (it waits a moment while another pixel is still in the way), where the belt's normal capture takes over and carries it off. Pixels that are held by the player, flying,
//   attached, already riding a belt / carried by a machine or standing on any belt are left alone; a pixel the player grabs while the robot has it is simply let go. When its time is up it drops what it carries and flies away. The spot follows the belt (it is
//   stored as a distance along the belt). Saved with the tester save (spot + seconds left).
// ---------------------------------------------------
using UnityEngine;

public class PixelTinyRobot : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How long the robot works before it flies away, in seconds. 0 = the coded default (60).")]
    [SerializeField] private float robotSeconds = 0f;

    [Min(0f)]
    [Tooltip("How far from the spot it looks for pixels, in cube widths. 0 = the coded default (18).")]
    [SerializeField] private float reachCubes = 0f;

    [Min(0f)]
    [Tooltip("Flying speed in cube widths per second. 0 = the coded default (7).")]
    [SerializeField] private float speedCubes = 0f;

    private float Seconds => robotSeconds > 0f ? robotSeconds : 60f;
    private float Reach => reachCubes > 0f ? reachCubes : 18f;
    private float Speed => speedCubes > 0f ? speedCubes : 7f;

    private enum State { Idle, ToPixel, ToSpot, Leaving }

    private PixelClicker clicker;
    private float unit = 1f;
    private bool ghost;
    private PixelConveyor belt;
    private float along;
    private Transform bot, beacon;
    private Transform[] props;
    private State state = State.Idle;
    private Rigidbody target;
    private float timeLeft, nextSearch, waitedForRoom, leaveT;
    private Vector3 botPos;

    // ---------------- placing ----------------

    private static bool FindSpot(PixelClicker c, Vector3 point, out PixelConveyor best, out float s) => PixelConveyor.NearestOn(c, point, out best, out s);

    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        if (!FindSpot(c, point, out PixelConveyor belt, out float s)) return false;
        Vector3 p = belt.PointAt(s);
        snappedPoint = new Vector3(p.x, p.y + belt.SurfaceHeight, p.z);
        Vector3 d = belt.Direction;
        snappedYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        return true;
    }

    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, "Tiny Robot (Ghost)"); }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw) { return PlaceWith(c, point, -1f); }

    private static GameObject PlaceWith(PixelClicker c, Vector3 point, float secondsLeft)
    {
        if (!FindSpot(c, point, out PixelConveyor belt, out float s)) { PixelHints.Announce("Click a spot on a conveyor belt for the tiny robot."); return null; }
        GameObject root = Build(c, false, "Tiny Robot");
        PixelTinyRobot r = root.GetComponent<PixelTinyRobot>();
        r.belt = belt; r.along = s;
        if (secondsLeft > 0f) r.timeLeft = secondsLeft;
        r.botPos = r.SpotPoint() + Vector3.up * (3f * r.unit);
        r.bot.position = r.botPos;
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, string name)
    {
        GameObject root = new GameObject(name);
        PixelTinyRobot r = root.AddComponent<PixelTinyRobot>();
        r.clicker = c; r.ghost = ghostMode; r.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        r.timeLeft = r.Seconds;
        r.BuildModel();
        return root;
    }

    private void BuildModel()
    {
        float u = unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color shell = new Color(0.72f, 0.76f, 0.84f, 1f), dark = new Color(0.2f, 0.23f, 0.3f, 1f), teal = new Color(0.2f, 0.9f, 0.85f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f);
        beacon = new GameObject("Beacon").transform;
        beacon.SetParent(transform, false);
        k.Cyl("Ring", beacon, new Vector3(0f, 0.02f * u, 0f), 0.5f * u, 0.04f * u, teal);
        k.Cyl("Dot", beacon, new Vector3(0f, 0.05f * u, 0f), 0.12f * u, 0.04f * u, Color.white);
        bot = new GameObject("Bot").transform;
        bot.SetParent(transform, false);
        // A small hover bot: boxy body, head with two teal eyes, an antenna, a grabber underneath and two spinning rotors.
        k.Box("Body", bot, Vector3.zero, new Vector3(0.34f * u, 0.26f * u, 0.34f * u), shell, default, false, 0.6f, 0.6f);
        k.Box("Belly", bot, new Vector3(0f, -0.16f * u, 0f), new Vector3(0.24f * u, 0.07f * u, 0.24f * u), dark, default, false, 0.5f, 0.4f);
        k.Box("Eye L", bot, new Vector3(-0.08f * u, 0.04f * u, 0.175f * u), new Vector3(0.07f * u, 0.07f * u, 0.02f * u), teal, default, false, 0f, 0.9f, teal);
        k.Box("Eye R", bot, new Vector3(0.08f * u, 0.04f * u, 0.175f * u), new Vector3(0.07f * u, 0.07f * u, 0.02f * u), teal, default, false, 0f, 0.9f, teal);
        k.Cyl("Antenna", bot, new Vector3(0f, 0.22f * u, 0f), 0.012f * u, 0.18f * u, dark);
        k.Ball("Bulb", bot, new Vector3(0f, 0.33f * u, 0f), 0.04f * u, accent);
        k.Box("Claw L", bot, new Vector3(-0.1f * u, -0.24f * u, 0f), new Vector3(0.04f * u, 0.12f * u, 0.14f * u), dark);
        k.Box("Claw R", bot, new Vector3(0.1f * u, -0.24f * u, 0f), new Vector3(0.04f * u, 0.12f * u, 0.14f * u), dark);
        props = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            k.Cyl("Arm", bot, new Vector3(sx * 0.24f * u, 0.07f * u, 0f), 0.012f * u, 0.12f * u, dark, new Vector3(0f, 0f, 90f));
            GameObject rotor = k.Cyl("Rotor", bot, new Vector3(sx * 0.3f * u, 0.13f * u, 0f), 0.14f * u, 0.015f * u, teal);
            props[i] = rotor.transform;
        }
    }

    // ---------------- saving ----------------

    [System.Serializable]
    private class Save { public Vector3 spot; public float left; }

    public static string ExportState(GameObject root)
    {
        PixelTinyRobot r = root != null ? root.GetComponent<PixelTinyRobot>() : null;
        return r == null || r.belt == null ? null : JsonUtility.ToJson(new Save { spot = r.SpotPoint(), left = r.timeLeft });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : PlaceWith(c, s.spot, Mathf.Max(5f, s.left));
    }

    // ---------------- behaviour ----------------

    private Vector3 SpotPoint()
    {
        Vector3 p = belt.PointAt(Mathf.Clamp(along, 0f, belt.Length));
        return new Vector3(p.x, p.y + belt.SurfaceHeight, p.z);
    }

    private void OnDestroy() { DropTarget(); }

    private void DropTarget()
    {
        if (target != null) { target.isKinematic = false; PixelPathCarrier.SetVelocity(target, Vector3.zero); }
        target = null;
    }

    /// <summary>A pixel the robot may fetch: loose on the ground, not touched by anything else.</summary>
    private bool Fetchable(Rigidbody rb)
    {
        if (rb == null || rb.isKinematic || clicker.IsFlyingPixel(rb) || PixelAttachment.IsAttached(rb)) return false;
        OldPixelInfo info = PixelHuskQuery.Get(rb).info;
        if (info == null || info.heldByPlayer) return false;
        OldPixelDespawn despawn = PixelHuskQuery.Get(rb).despawn;
        if (despawn != null && (despawn.IsDespawning || despawn.Held)) return false;
        Vector3 c = rb.worldCenterOfMass;
        foreach (PixelConveyor b in PixelConveyor.All) if (b != null && !b.IsGhost && b.Carries(c)) return false;   // already on a belt (or about to be captured)
        return true;
    }

    private Rigidbody FindTarget()
    {
        Vector3 spot = SpotPoint();
        float best = Reach * unit; best *= best;
        Rigidbody pick = null;
        foreach (Rigidbody rb in clicker.OldPixels)
        {
            if (!Fetchable(rb)) continue;
            float d = (rb.position - spot).sqrMagnitude;
            if (d < best) { best = d; pick = rb; }
        }
        return pick;
    }

    private void Update()
    {
        if (ghost || clicker == null) return;
        if (belt == null) { DropTarget(); Destroy(gameObject); return; }   // its belt was cleared
        float dt = Time.deltaTime;
        Vector3 spot = SpotPoint();
        transform.position = spot;
        if (beacon != null) beacon.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.time * 4f));
        if (props != null) foreach (Transform p in props) if (p != null) p.Rotate(0f, 900f * dt, 0f, Space.Self);

        if (state != State.Leaving)
        {
            timeLeft -= dt;
            if (timeLeft <= 0f) { state = State.Leaving; leaveT = 0f; if (target != null) DropAtSpot(); }
        }

        float hover = 2.2f * unit;
        Vector3 goal = spot + Vector3.up * hover;
        switch (state)
        {
            case State.Idle:
                goal = spot + Vector3.up * hover + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.08f * unit;
                if (Time.time >= nextSearch)
                {
                    nextSearch = Time.time + 0.3f;
                    target = FindTarget();
                    if (target != null) state = State.ToPixel;
                }
                break;
            case State.ToPixel:
                if (!Fetchable(target)) { target = null; state = State.Idle; break; }
                goal = target.position + Vector3.up * 0.75f * unit;
                if ((botPos - goal).sqrMagnitude < 0.04f * unit * unit)
                {
                    target.isKinematic = true;
                    PixelPathCarrier.SetVelocity(target, Vector3.zero);
                    state = State.ToSpot;
                    waitedForRoom = 0f;
                    PixelAudio.Play("click", 1.4f);
                }
                break;
            case State.ToSpot:
                if (target == null) { state = State.Idle; break; }
                OldPixelInfo info = PixelHuskQuery.Get(target).info;
                if (info != null && info.heldByPlayer) { DropTarget(); state = State.Idle; break; }   // the player grabbed it
                goal = spot + Vector3.up * (0.95f * unit + 0.75f * unit);
                if ((botPos - goal).sqrMagnitude < 0.04f * unit * unit)
                {
                    waitedForRoom += dt;
                    if (RoomAtSpot(spot) || waitedForRoom > 2.5f) { DropAtSpot(); state = State.Idle; nextSearch = 0f; }
                }
                break;
            case State.Leaving:
                leaveT += dt;
                goal = spot + Vector3.up * (hover + 6f * unit);
                bot.localScale = Vector3.one * Mathf.Clamp01(1.2f - leaveT);
                if (leaveT > 1.2f) { Destroy(gameObject); return; }
                break;
        }

        float speed = Speed * unit;
        botPos = Vector3.MoveTowards(botPos, goal, speed * dt * Mathf.Clamp(Vector3.Distance(botPos, goal) / (1.5f * unit) + 0.25f, 0.25f, 1f));
        bot.position = botPos;
        Vector3 toGoal = goal - botPos; toGoal.y = 0f;
        if (toGoal.sqrMagnitude > 0.0004f) bot.rotation = Quaternion.Slerp(bot.rotation, Quaternion.LookRotation(toGoal.normalized, Vector3.up), 6f * dt);
        bot.rotation = Quaternion.Euler(0f, bot.eulerAngles.y, 0f);

        if (target != null && target.isKinematic && (state == State.ToSpot || state == State.Leaving))
        {
            OldPixelDespawn d = PixelHuskQuery.Get(target).despawn;
            if (d != null) d.KeepAlive();
            target.position = botPos + Vector3.down * 0.75f * unit;
        }
    }

    private bool RoomAtSpot(Vector3 spot)
    {
        Vector3 drop = spot + Vector3.up * (clicker.OldPixelWorldSize * 0.7f);
        float r = 0.95f * unit;
        foreach (Rigidbody rb in clicker.OldPixels)
            if (rb != null && rb != target && (rb.position - drop).sqrMagnitude < r * r) return false;
        return true;
    }

    private void DropAtSpot()
    {
        if (target == null) return;
        Vector3 spot = SpotPoint();
        target.position = spot + Vector3.up * (clicker.OldPixelWorldSize * 0.7f);
        target.isKinematic = false;
        PixelPathCarrier.SetVelocity(target, Vector3.zero);
        target = null;
    }
}
