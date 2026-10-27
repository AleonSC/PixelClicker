// ---- System notes ----
// PixelConsumablesSaving.cs
//   Part of PixelConsumables (partial class): saving and restoring the placed devices (PlacedState, GetPlacedDevices, RestorePlacedDevice) and the running potion (RestoreActive). The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using UnityEngine;

public partial class PixelConsumables
{
    // ------------------------------------------------------------------
    // Saving: the running potion and the devices standing in the world
    // ------------------------------------------------------------------

    /// <summary>Devices that last a number of uses (clicks, strikes) instead of a time.</summary>
    private static bool UsesKind(DeviceKind kind) => kind == DeviceKind.ChargeBooster || kind == DeviceKind.LightningRod;

    /// <summary>A placed device as the save system stores it.</summary>
    [Serializable]
    public class PlacedState
    {
        public string device;
        public Vector3 position;
        public float yaw;
        public float remaining;
        public float aim;   // the Charge Booster's charge
        public bool disarmed;   // placed but never switched on yet
        public string extra;    // device-specific text (an organizer's or crate's contents, a machine's settings)
        public string couplings;   // the couplings fitted on the device's sockets (PixelCouplings.Export)
    }

    /// <summary>The devices currently standing in the world (those not already shrinking away).</summary>
    public System.Collections.Generic.List<PlacedState> GetPlacedDevices()
    {
        System.Collections.Generic.List<PlacedState> list = new System.Collections.Generic.List<PlacedState>();
        foreach (PixelPlacedDevice placed in PixelPlacedDevice.All)
        {
            if (placed == null || placed.IsEnding || placed.DeviceIndex < 0 || placed.DeviceIndex >= devices.Length) continue;
            PlacedState state = new PlacedState
            {
                device = devices[placed.DeviceIndex].displayName,
                position = placed.transform.position,
                yaw = placed.transform.eulerAngles.y,
                remaining = placed.Remaining,
                disarmed = !placed.Armed,
            };
            state.couplings = PixelCouplings.Export(placed.gameObject);
            if (placed is PixelChargeBooster booster) state.aim = booster.Charge;
            if (placed is PixelQuantumDevice organizer) state.extra = organizer.ExportState();
            if (placed is PixelStorageCrate storageCrate) state.extra = storageCrate.ExportState();
            PixelRecycler recycler = placed.GetComponent<PixelRecycler>();
            if (recycler != null) state.extra = recycler.ExportState();   // the husks waiting in a Recycler
            PixelSortingHopper hopper = placed.GetComponent<PixelSortingHopper>();
            if (hopper != null) state.extra = hopper.ExportState();       // the Sorting Hopper's chosen type
            PixelSweeper sweeper = placed.GetComponent<PixelSweeper>();
            if (sweeper != null) state.extra = sweeper.ExportState();     // the Sweeper's wind left
            PixelBiomassGenerator generator = placed.GetComponent<PixelBiomassGenerator>();
            if (generator != null) state.extra = generator.ExportState();   // a Biomass Generator's charge left
            PixelPowerBank bankState = placed.GetComponent<PixelPowerBank>();
            if (bankState != null) state.extra = bankState.ExportState();   // a Power Bank's charge
            PixelJunctionBox junction = placed.GetComponent<PixelJunctionBox>();
            if (junction != null) state.extra = junction.ExportState();   // the Junction Box's filters and buffer
            list.Add(state);
        }
        return list;
    }

    /// <summary>Removes every placed device at once (before a save is loaded).</summary>
    public void ClearPlacedDevices()
    {
        foreach (PixelPlacedDevice placed in new System.Collections.Generic.List<PixelPlacedDevice>(PixelPlacedDevice.All))
            if (placed != null) { placed.TakeOutForClear(); Destroy(placed.gameObject); }   // (out of the All list now: Destroy waits for the end of the frame)
    }

    /// <summary>Puts a saved device back in the world with the time it had left.</summary>
    public void RestorePlacedDevice(PlacedState state)
    {
        if (state == null || state.remaining <= 0f) return;
        int index = Array.FindIndex(devices, x => x != null && x.displayName == state.device);
        if (index < 0) return;
        PixelPlacedDevice restored = SpawnDevice(index, state.position, state.yaw, state.remaining, state.aim);
        if (restored != null && state.disarmed && !(restored is PixelStorageCrate) && !(restored is PixelPermanentDevice) && !(restored is PixelRecyclerDevice)) restored.Disarm();
        if (restored is PixelQuantumDevice restoredOrganizer) restoredOrganizer.ImportState(state.extra);
        PixelRecycler restoredRecycler = restored != null ? restored.GetComponent<PixelRecycler>() : null;
        if (restoredRecycler != null) restoredRecycler.ImportState(state.extra);
        PixelSortingHopper restoredHopper = restored != null ? restored.GetComponent<PixelSortingHopper>() : null;
        if (restoredHopper != null) restoredHopper.ImportState(state.extra);
        PixelSweeper restoredSweeper = restored != null ? restored.GetComponent<PixelSweeper>() : null;
        if (restoredSweeper != null) restoredSweeper.ImportState(state.extra);
        PixelBiomassGenerator restoredGenerator = restored != null ? restored.GetComponent<PixelBiomassGenerator>() : null;
        if (restoredGenerator != null) restoredGenerator.ImportState(state.extra);
        PixelPowerBank restoredBank = restored != null ? restored.GetComponent<PixelPowerBank>() : null;
        if (restoredBank != null) restoredBank.ImportState(state.extra);
        PixelJunctionBox restoredJunction = restored != null ? restored.GetComponent<PixelJunctionBox>() : null;
        if (restoredJunction != null) restoredJunction.ImportState(state.extra);
        if (restored is PixelStorageCrate restoredCrate) restoredCrate.ImportState(state.extra);
        if (restored != null) PixelCouplings.Import(restored.gameObject, state.couplings);   // its couplings go back on its sockets
    }

    /// <summary>Starts a potion again with the time it had left (no sound, nothing used up). Used when loading a save.</summary>
    public void RestoreActive(int index, float secondsLeft)
    {
        if (index < 0 || index >= potions.Length || secondsLeft <= 0f) return;
        Potion potion = potions[index];
        activeIndex = index;
        remaining = secondsLeft;
        if (potion.craftOnly) clicker.SetForcedSpawnTiers(potion.type, potion.secondType, potion.hasThird ? potion.thirdType : (PixelClicker.PixelType?)null);
        else clicker.SetForcedSpawnTier(potion.type);
    }
}
