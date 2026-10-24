// ---- System notes ----
// PixelConsumablesPlacing.cs
//   Part of PixelConsumables (partial class): placing devices (preview, Robot Worker, seeds, the floor point, turning; the device itself is built in PixelConsumablesSpawn.cs). The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using static PixelInput;

public partial class PixelConsumables
{
    // ------------------------------------------------------------------
    // Placing devices
    // ------------------------------------------------------------------

    private int placingIndex = -1;
    private Action<int, Vector3, float> robotPicked;   // set while the Robot Worker's owner is choosing a spot (nothing is used up)
    private int placeStartFrame;
    private GameObject preview;

    /// <summary>True while the player is choosing where to put a device.</summary>
    public bool IsPlacing => placingIndex >= 0;

    /// <summary>Name of the device being placed (empty when not placing).</summary>
    public string PlacingName => IsPlacing ? devices[placingIndex].displayName : "";

    /// <summary>The device's own placing message (empty = use the default one).</summary>
    public string PlacingMessage => !IsPlacing ? ""
        : devices[placingIndex].kind == DeviceKind.StorageCrate ? PixelKeys.Replace("Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)")
        : PixelKeys.Replace(devices[placingIndex].placingMessage);

    private float placingYaw, rawYaw;   // rawYaw = the turn before Shift snapping
    private bool chipperLatched;
    private Vector3 chipperLatchPoint;
    private float chipperLatchYaw;

    // --- Robot Worker support ---

    /// <summary>The placeable kinds (the ones that stand in the world): the Robot Worker can keep any of these going.</summary>
    public static bool IsPlaceableKind(DeviceKind kind) =>
        kind == DeviceKind.Vacuum || kind == DeviceKind.Fan ||
        kind == DeviceKind.ChargeBooster || kind == DeviceKind.LightningRod || kind == DeviceKind.Sprinkler || kind == DeviceKind.Magnet;

    /// <summary>How many of a device you hold (huge with infinite resources).</summary>
    public int DeviceOwned(int deviceIndex) => Inf ? 99999 : devices[deviceIndex].owned;

    /// <summary>Starts choosing a spot for the Robot Worker: a ghost follows the mouse like when placing, but nothing is used up. 'picked' gets (device, point, yaw).</summary>
    public bool BeginRobotPlacement(int deviceIndex, Action<int, Vector3, float> picked)
    {
        if (picked == null || deviceIndex < 0 || deviceIndex >= devices.Length || !IsPlaceableKind(devices[deviceIndex].kind)) return false;
        if (!BeginPlacement(deviceIndex, true)) return false;
        robotPicked = picked;
        return true;
    }

    /// <summary>A see-through ghost of a device at a spot (the Robot Worker's job marker). The caller owns it.</summary>
    public GameObject CreateGhost(int deviceIndex, Vector3 point, float yaw)
    {
        Device d = EffectiveDevice(devices[deviceIndex]);
        Camera cam = clicker.ViewCamera;
        GameObject ghost = d.kind == DeviceKind.Fan ? BuildFanObject(d, true, out _, out _) : BuildDeviceObject(d, true, out _, out _);
        ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return ghost;
    }

    /// <summary>Puts a working device in the world for the Robot Worker (uses one up). Null if you have none.</summary>
    public PixelPlacedDevice RobotPlace(int deviceIndex, Vector3 point, float yaw)
    {
        if (deviceIndex < 0 || deviceIndex >= devices.Length) return null;
        Device d = devices[deviceIndex];
        if (!Inf && d.owned <= 0) return null;
        Device effective = EffectiveDevice(d);
        PixelPlacedDevice placed = SpawnDevice(deviceIndex, point, yaw, UsesKind(d.kind) ? effective.uses : DeviceSeconds(effective), 0f);
        if (placed == null) return null;
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);
        PixelAudio.Play("device_place");
        // (it stays switched on: keeping a device going without the player is the UFO's job. The placed events fire like for any device.)
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(deviceIndex);
        return placed;
    }

    /// <summary>Starts placing a device: a cylinder follows the mouse until you click the floor. Returns false if you own none.</summary>
    public bool BeginPlacement(int deviceIndex) => BeginPlacement(deviceIndex, false);

    private bool BeginPlacement(int deviceIndex, bool forRobot)
    {
        if (IsPlacing || deviceIndex < 0 || deviceIndex >= devices.Length) return false;
        if (!forRobot && !Inf && devices[deviceIndex].owned <= 0) return false;

        placingIndex = deviceIndex;
        placeStartFrame = Time.frameCount;
        Camera cam = clicker.ViewCamera;
        placingYaw = cam != null ? cam.transform.eulerAngles.y : 0f; // a fan starts out blowing away from the camera
        if (devices[deviceIndex].kind == DeviceKind.Funnel) placingYaw = 0f;   // (the funnel's turn is only a nudge on top of "away from the cube")
        rawYaw = placingYaw;
        if (devices[deviceIndex].kind == DeviceKind.Seed)
        {
            preview = new GameObject("Seed Preview");
            PixelLooks.CreateSproutObject(preview.transform, clicker.SeedSproutWorldSize);
            preview.SetActive(false);
        }
        else
        {
            preview = devices[deviceIndex].kind == DeviceKind.Fan
                ? BuildFanObject(EffectiveDevice(devices[deviceIndex]), true, out _, out _)
                : BuildDeviceObject(EffectiveDevice(devices[deviceIndex]), true, out _, out _);
            preview.SetActive(false);
        }
        clicker.SetClicksBlocked(true); // so the placing click doesn't also hit the cube
        return true;
    }

    /// <summary>Stops placing without using up the device.</summary>
    public void CancelPlacement()
    {
        if (IsPlacing) EndPlacement();
    }

    private void EndPlacement()
    {
        robotPicked = null;
        placeEndFrame = Time.frameCount; // the click that ended placing must not start a removal hold
        placingIndex = -1;
        if (preview != null) Destroy(preview);
        preview = null;
        StartCoroutine(UnblockClicksNextFrame());
    }

    private System.Collections.IEnumerator UnblockClicksNextFrame()
    {
        yield return null; // the click that ended placing has been fully handled by then
        if (clicker != null) clicker.SetClicksBlocked(false);
    }

    private void UpdatePlacement()
    {
        if (Time.timeScale <= 0f) return;
        if (Time.frameCount == placeStartFrame) return; // ignore the right-click that started this

        PixelSnap.Configure(shiftSnapGridCubes, shiftSnapAngleDegrees);
        bool hasPoint = TryGetPlacePoint(out Vector3 point);
        if (hasPoint && PixelSnap.Active) point = PixelSnap.Point(point, clicker.OldPixelWorldSize);   // snap to the grid (hold Shift for free placement)
        preview.SetActive(hasPoint);
        if (hasPoint) preview.transform.position = point;

        // A fan can be turned with the mouse wheel or Q / E.
        if (devices[placingIndex].kind == DeviceKind.Fan || devices[placingIndex].kind == DeviceKind.StorageCrate || devices[placingIndex].kind == DeviceKind.WoodChipper || devices[placingIndex].kind == DeviceKind.Funnel || devices[placingIndex].kind == DeviceKind.Recycler || devices[placingIndex].kind == DeviceKind.SortingHopper || devices[placingIndex].kind == DeviceKind.JunctionBox || IsGeneratorKind(devices[placingIndex].kind))
        {
            rawYaw += TurnInput();
            placingYaw = PixelSnap.Active ? PixelSnap.Yaw(rawYaw) : rawYaw;   // snap the turn to angle steps unless Shift is held (the raw turn keeps counting so Q / E stay smooth)
            preview.transform.rotation = Quaternion.Euler(0f, placingYaw, 0f);
        }
        chipperLatched = false;

        if (hasPoint && devices[placingIndex].kind == DeviceKind.Funnel)
        {
            // The funnel's output goes where you point (kept within reach, the spout turned away from the cube plus your turn); a belt opening in front of it latches on.
            PixelFunnel funnel = preview.GetComponent<PixelFunnel>();
            if (funnel != null)
            {
                funnel.Adjust(point, placingYaw, out Vector3 funnelPoint, out float funnelYaw);
                chipperLatched = true; chipperLatchPoint = funnelPoint; chipperLatchYaw = funnelYaw;
                preview.transform.SetPositionAndRotation(funnelPoint, Quaternion.Euler(0f, funnelYaw, 0f));
            }
        }

        if (devices[placingIndex].kind == DeviceKind.SortingHopper)
            preview.transform.localScale = Vector3.one * PixelSortingHopper.UnlatchedSize;   // (it stands where you point - it never snaps onto a Recycler / Junction Box)

        if (devices[placingIndex].kind == DeviceKind.Seed)
        {
            if (LeftPressed() && hasPoint && !PointerOverUI()) PlantSeed(point);
            else if (RightPressed()) CancelPlacement();
            return;
        }

        if (LeftPressed() && hasPoint && !PointerOverUI()) PlaceDevice(point);
        else if (RightPressed()) CancelPlacement();
    }

    /// <summary>Plants the seed being held at 'point'; keeps planting while more seeds of that kind are left.</summary>
    private void PlantSeed(Vector3 point)
    {
        Device d = devices[placingIndex];
        if (d.beans)
        {
            // Magic Beans: a beanstalk with a Dragon Cube (and a handful of other cubes) instead of a sprout.
            if (PixelBeanstalk.Current != null) { PixelHints.Announce("A beanstalk is already growing - pick its cubes first"); return; }
            int dragonTier = clicker.PickMissingDragonCube();
            if (dragonTier < 0 && Inf) dragonTier = clicker.PickAnyDragonCube();
            if (dragonTier < 0)
            {
                PixelHints.Announce("You already hold all seven Dragon Cubes - the beans have nothing to grow");
                EndPlacement();
                return;
            }
            PixelBeanstalk.Plant(clicker, point, dragonTier, false);
            if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);
            PixelStats.Count("beans.planted");
            EndPlacement();
            return;
        }
        int tier = clicker.IndexOf(d.seedType);
        if (PixelClicker.IsDragonCube(d.seedType))
        {
            tier = clicker.PickMissingDragonCube(); // a Dragon Cube you don't hold
            if (tier < 0 && Inf) tier = clicker.PickAnyDragonCube();   // infinite resources (testing): any of the seven, even one you hold
            if (tier < 0)
            {
                PixelHints.Announce("You already hold all seven Dragon Cubes - the Dragon Seed has nothing left to grow");
                EndPlacement();
                return;
            }
        }
        if ((!Inf && d.owned <= 0) || tier < 0) { EndPlacement(); return; }
        if (!clicker.SeedSpotFree(point))
        {
            PixelHints.Announce("There is already a seed there - plant it a little further away");
            return;
        }
        if (!clicker.PlantSeedSproutAt(point, tier))
        {
            PixelHints.Announce("Too many sprouts already - wait for some to finish growing");
            return;
        }
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);
        PixelStats.Count("seeds.planted");
        if (!Inf && d.owned <= 0) EndPlacement();
    }

    private void PlaceDevice(Vector3 point)
    {
        int index = placingIndex;
        Device d = devices[index];
        if (chipperLatched && d.kind == DeviceKind.Funnel) { point = chipperLatchPoint; placingYaw = chipperLatchYaw; }
        if (robotPicked != null)
        {
            Action<int, Vector3, float> picked = robotPicked;
            float yaw = placingYaw;
            EndPlacement();
            picked(index, point, yaw);
            return;
        }
        bool movingFunnel = d.kind == DeviceKind.Funnel && PixelFunnel.Exists;   // placing the funnel again just moves it
        if (!Inf && !movingFunnel) d.owned = Mathf.Max(0, d.owned - 1);

        Device effective = EffectiveDevice(d);
        PixelPlacedDevice placed = SpawnDevice(index, point, placingYaw, UsesKind(d.kind) ? effective.uses : DeviceSeconds(effective), 0f);
        if (placed != null && StartsDisarmed(d.kind)) placed.Disarm();   // starts grey and idle until you left-click it

        EndPlacement();
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(index);
    }

    /// <summary>The floor point under the mouse: the first suitable collider hit, else the fallback plane.</summary>
    private bool TryGetPlacePoint(out Vector3 point) => TryGetFloorPoint(PointerPosition(), out point);

    /// <summary>The spot on the floor under a screen position (not on the cube or an old pixel). Also used by the Robot Worker.</summary>
    public bool TryGetFloorPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = clicker.ViewCamera;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, placementLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue; // not on top of old pixels
            if (clicker.PixelTransform != null && hit.transform.IsChildOf(clicker.PixelTransform)) continue; // not on the cube
            if (hit.normal.y < minSurfaceNormalY) continue;                          // walls / steep slopes
            point = hit.point;
            return true;
        }

        Plane floor = new Plane(Vector3.up, new Vector3(0f, fallbackFloorY, 0f));
        if (floor.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    /// <summary>Degrees to turn this frame from the mouse wheel and the Q / E keys.</summary>
    public float TurnInput()
    {
        float degrees = 0f;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current != null && !PixelInput.ControlHeld()) degrees += Mathf.Sign(Mouse.current.scroll.ReadValue().y) * (Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f ? 1f : 0f) * rotateStepDegrees;
#else
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && !PixelInput.ControlHeld()) degrees += Mathf.Sign(wheel) * rotateStepDegrees;   // (Ctrl + wheel zooms the camera)
#endif
        if (PixelKeys.Held(PixelAction.RotateRight)) degrees += rotateSpeedDegrees * Time.deltaTime; // rebindable (default E)
        if (PixelKeys.Held(PixelAction.RotateLeft)) degrees -= rotateSpeedDegrees * Time.deltaTime;  // (default Q)
        return degrees;
    }
}
