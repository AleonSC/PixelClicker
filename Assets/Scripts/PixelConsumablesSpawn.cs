// ---- System notes ----
// PixelConsumablesSpawn.cs
//   Part of PixelConsumables (partial class): building a working device in the world (SpawnDevice: the model, its device component and settings, used by placing and by loading a save; StartsDisarmed). The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;

public partial class PixelConsumables
{
    /// <summary>Does a device of this kind start grey and idle until it is clicked once? (The permanent machines need no switching on.)</summary>
    public static bool StartsDisarmed(DeviceKind kind) =>
        kind != DeviceKind.StorageCrate && kind != DeviceKind.WoodChipper && kind != DeviceKind.Funnel && kind != DeviceKind.Recycler &&
        kind != DeviceKind.SortingHopper && kind != DeviceKind.Sweeper && kind != DeviceKind.JunctionBox && kind != DeviceKind.PowerBank && !IsGeneratorKind(kind);

    /// <summary>Builds a working device in the world (placing it, or restoring it from a save).</summary>
    private PixelPlacedDevice SpawnDevice(int index, Vector3 point, float yaw, float duration, float aim)
    {
        Device d = EffectiveDevice(devices[index]);
        Camera cam = clicker.ViewCamera;
        PixelPlacedDevice result;
        PixelPlacedDevice.DisarmedText = string.IsNullOrEmpty(disarmedText) ? "Click to start" : disarmedText;
        PixelPlacedDevice.ArmFadeSeconds = armFadeSeconds > 0f ? armFadeSeconds : 0.7f;

        if (d.kind == DeviceKind.Fan)
        {
            GameObject fan = BuildFanObject(d, false, out Transform blades, out TextMeshPro fanTimer);
            fan.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));

            PixelFanDevice fanDevice = fan.AddComponent<PixelFanDevice>();
            fanDevice.Init(clicker, cam, blades, fanTimer, timerFormat, duration, d.radius, d.coneAngle,
                           d.coneHeight, d.blowAcceleration, d.liftAcceleration, d.bladeSpinDegrees, shrinkSeconds);
            result = fanDevice;
        }
        else if (UsesKind(d.kind))
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.position = point;
            int uses = Mathf.Max(1, Mathf.RoundToInt(duration));
            if (d.kind == DeviceKind.ChargeBooster)
            {
                PixelChargeBooster booster = root.AddComponent<PixelChargeBooster>();
                booster.Init(clicker, cam, timer, timerFormat, uses, shrinkSeconds, d, d.bodyHeight);
                booster.SetCharge(aim); // a saved booster keeps its charge (stored in the 'aim' slot)
                result = booster;
            }
            else
            {
                PixelLightningRod rod = root.AddComponent<PixelLightningRod>();
                rod.Init(clicker, cam, timer, timerFormat, uses, shrinkSeconds, d, d.bodyHeight, 0f);
                result = rod;
            }
        }
        else if (d.kind == DeviceKind.WoodChipper)
        {
            GameObject root = PixelWoodChipper.Place(clicker, point, yaw);
            PixelPermanentDevice chipperDevice = root.AddComponent<PixelPermanentDevice>();
            chipperDevice.Init(clicker, cam, shrinkSeconds);
            result = chipperDevice;
        }
        else if (d.kind == DeviceKind.Recycler)
        {
            GameObject root = PixelRecycler.Place(clicker, point, yaw, d.radius);   // (d.radius = the bin's size factor, Operation: Bin Size)
            PixelRecyclerDevice recyclerDevice = root.AddComponent<PixelRecyclerDevice>();   // (the countdown; the behaviour is in PixelRecycler)
            recyclerDevice.Init(clicker, cam, duration, shrinkSeconds);
            result = recyclerDevice;
        }
        else if (d.kind == DeviceKind.Sweeper)
        {
            GameObject root = PixelSweeper.Place(clicker, point, yaw);
            PixelPermanentDevice sweeperDevice = root.AddComponent<PixelPermanentDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelSweeper)
            sweeperDevice.Init(clicker, cam, shrinkSeconds);
            result = sweeperDevice;
        }
        else if (IsGeneratorKind(d.kind))
        {
            float size = d.bodyDiameter > 0f ? d.bodyDiameter : 1f;
            GameObject root = PixelBiomassGenerator.Place(clicker, point, yaw, size);
            PixelPermanentDevice generatorDevice = root.AddComponent<PixelPermanentDevice>();   // (the thin permanent-device stub; it removes itself when it runs dry)
            generatorDevice.Init(clicker, cam, shrinkSeconds);
            PixelBiomassGenerator gen = root.AddComponent<PixelBiomassGenerator>();
            Device fresh = d.kind == DeviceKind.BiomassGenerator ? CreateDefaultBiomassGenerator() : CreateDefaultPocketBiomassGenerator();
            gen.Init(clicker, d.radius, d.generatorMaxCharge > 0f ? d.generatorMaxCharge : fresh.generatorMaxCharge, d.generatorChargePerHusk > 0f ? d.generatorChargePerHusk : fresh.generatorChargePerHusk, size);
            result = generatorDevice;
        }
        else if (d.kind == DeviceKind.PowerBank)
        {
            GameObject root = PixelPowerBank.Place(clicker, point, yaw);
            PixelPermanentDevice bankDevice = root.AddComponent<PixelPermanentDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelPowerBank)
            bankDevice.Init(clicker, cam, shrinkSeconds);
            PixelPowerBank bank = root.AddComponent<PixelPowerBank>();
            bank.Init(clicker, d.radius, d.generatorMaxCharge > 0f ? d.generatorMaxCharge : CreateDefaultPowerBank().generatorMaxCharge);
            result = bankDevice;
        }
        else if (d.kind == DeviceKind.JunctionBox)
        {
            GameObject root = PixelJunctionBox.Place(clicker, point, yaw);
            PixelPermanentDevice junctionDevice = root.AddComponent<PixelPermanentDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelJunctionBox)
            junctionDevice.Init(clicker, cam, shrinkSeconds);
            result = junctionDevice;
        }
        else if (d.kind == DeviceKind.SortingHopper)
        {
            GameObject root = PixelSortingHopper.Place(clicker, point, yaw);
            PixelPermanentDevice hopperDevice = root.AddComponent<PixelPermanentDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelSortingHopper)
            hopperDevice.Init(clicker, cam, shrinkSeconds);
            result = hopperDevice;
        }
        else if (d.kind == DeviceKind.Funnel)
        {
            GameObject root = PixelFunnel.Place(clicker, point, yaw);
            PixelFunnelDevice funnelDevice = root.AddComponent<PixelFunnelDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelFunnel)
            funnelDevice.Init(clicker, cam, shrinkSeconds);
            result = funnelDevice;
        }
        else if (d.kind == DeviceKind.StorageCrate)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro crateTimer);
            if (crateTimer != null) crateTimer.gameObject.SetActive(false);
            root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            PixelStorageCrate crate = root.AddComponent<PixelStorageCrate>();
            crate.Init(clicker, cam, d, shrinkSeconds);
            result = crate;
        }
        else if (d.kind == DeviceKind.QuantumOrganizer)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.position = point;
            PixelQuantumDevice organizer = root.AddComponent<PixelQuantumDevice>();
            organizer.Init(clicker, cam, d, timer, timerFormat, duration, shrinkSeconds);
            result = organizer;
        }
        else if (d.kind == DeviceKind.Magnet)
        {
            GameObject root = BuildDeviceObject(d, false, out Transform topPoint, out TextMeshPro timer);
            root.transform.position = point;
            PixelMagnetDevice magnet = root.AddComponent<PixelMagnetDevice>();
            magnet.Init(clicker, cam, topPoint, timer, timerFormat, duration, d.radius, d.pullAcceleration, d.absorbDistance, shrinkSeconds, d.bodyHeight);
            result = magnet;
        }
        else if (d.kind == DeviceKind.Sprinkler)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.position = point;
            PixelSprinklerDevice sprinkler = root.AddComponent<PixelSprinklerDevice>();
            sprinkler.Init(clicker, cam, timer, timerFormat, duration, shrinkSeconds, d.radius, d.sprayInterval);
            result = sprinkler;
        }
        else
        {
            GameObject root = BuildDeviceObject(d, false, out Transform suckPoint, out TextMeshPro timer);
            root.transform.position = point;

            PixelVacuumDevice device = root.AddComponent<PixelVacuumDevice>();
            device.Init(clicker, cam, suckPoint, timer, timerFormat, duration, d.radius, d.pullAcceleration,
                        d.absorbDistance, shrinkSeconds);
            result = device;
        }

        result.DeviceIndex = index;
        result.AttuneTier = d.attuned ? clicker.IndexOf(d.attuneType) : -1;
        return result;
    }
}
