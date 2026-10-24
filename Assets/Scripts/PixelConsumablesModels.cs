// ---- System notes ----
// PixelConsumablesModels.cs
//   Part of PixelConsumables (partial class): the 3D models: shop display models (CreateDisplayModel, BuildItemModel) and the placed device objects (BuildDeviceObject, BuildFanObject). The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;

public partial class PixelConsumables
{
    // --- A 3D picture of each consumable for the Operation tab ---

    private bool displayOnly;   // building a model for the shop: no range markings, no timer

    /// <summary>Builds a model of a consumable at the origin (no timer, range disc or collider), or null if there is none. The caller owns it.</summary>
    public GameObject CreateDisplayModel(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        if (d == null || clicker == null) return null;
        displayOnly = true;
        GameObject root = null;
        try
        {
            Camera cam = clicker.ViewCamera;
            switch (kind)
            {
                case DeviceKind.Fan:
                    root = BuildFanObject(d, false, out _, out _);
                    break;
                case DeviceKind.Vacuum:
                case DeviceKind.Sprinkler:
                case DeviceKind.Magnet:
                case DeviceKind.QuantumOrganizer:
                case DeviceKind.StorageCrate:
                    root = BuildDeviceObject(d, false, out _, out _);
                    break;
                case DeviceKind.ChargeBooster:
                    root = BuildDeviceObject(d, false, out _, out _);
                    DecorateBoosterModel(root, d);
                    break;
                case DeviceKind.LightningRod:
                    root = BuildDeviceObject(d, false, out _, out _);
                    DecorateRodModel(root, d);
                    break;
                case DeviceKind.WoodChipper:
                    root = PixelWoodChipper.CreateDisplay(clicker);
                    break;
                case DeviceKind.Funnel:
                    root = PixelFunnel.CreateDisplay(clicker);
                    break;
                case DeviceKind.Recycler:
                    root = PixelRecycler.CreateDisplay(clicker);
                    break;
                case DeviceKind.SortingHopper:
                    root = PixelSortingHopper.CreateDisplay(clicker);
                    break;
                case DeviceKind.Sweeper:
                    root = PixelSweeper.CreateDisplay(clicker);
                    break;
                case DeviceKind.JunctionBox:
                    root = PixelJunctionBox.CreateDisplay(clicker);
                    break;
                case DeviceKind.PocketBiomassGenerator:
                case DeviceKind.BiomassGenerator:
                    root = PixelBiomassGenerator.CreateDisplay(clicker, d.bodyDiameter > 0f ? d.bodyDiameter : 1f);
                    break;
                case DeviceKind.PowerBank:
                    root = PixelPowerBank.CreateDisplay(clicker);
                    break;
                case DeviceKind.BeltSorter: root = PixelBeltSorter.CreateDisplay(clicker); break;
                case DeviceKind.ConveyorElevator: root = PixelElevator.CreateDisplay(clicker); break;
                case DeviceKind.BeltJump: root = PixelBeltJump.CreateDisplay(clicker); break;
                case DeviceKind.ConveyorCombiner: root = PixelConveyorCombiner.CreateDisplay(clicker); break;
                case DeviceKind.SolarPanel: root = PixelSolarPanel.CreateDisplay(clicker); break;
                default:
                    if (PixelInstantItems.Find(kind) != null) root = BuildItemModel(kind, d);   // instant-use items (PixelInstantItems) have small item models
                    break;
            }
        }
        finally { displayOnly = false; }
        return root;
    }

    /// <summary>
    /// Turns a working machine built by its own Build into a picture-only model: every behaviour on it (the machine itself, its couplings) is
    /// removed at once, so it never registers as a real device or touches husks. Used by the conveyor pieces' CreateDisplay.
    /// </summary>
    public static GameObject DisplayOnly(GameObject root)
    {
        if (root == null) return null;
        foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null) DestroyImmediate(b);
        return root;
    }

    /// <summary>The shop picture's Charge Booster meter (in the world PixelChargeBooster builds the live one in the same spot): dark glass, 70% full.</summary>
    private void DecorateBoosterModel(GameObject root, Device d)
    {
        PixelFactoryKit k = new PixelFactoryKit(clicker, false);
        float x = d.bodyDiameter * 0.75f, h = d.bodyHeight * 1.1f, fill = h * 0.7f;
        k.Box("Meter Back", root.transform, new Vector3(x, h * 0.5f, 0f), new Vector3(0.14f, h, 0.14f), new Color(0.05f, 0.05f, 0.08f, 1f), default, false, 0.3f, 0.9f);
        k.Box("Meter Fill", root.transform, new Vector3(x, fill * 0.5f, 0f), new Vector3(0.1f, fill, 0.15f), d.color, default, false, 0f, 0.6f, d.color);
    }

    /// <summary>The shop picture's Lightning Rod tip (in the world PixelLightningRod adds the glowing ball itself).</summary>
    private void DecorateRodModel(GameObject root, Device d)
    {
        PixelFactoryKit k = new PixelFactoryKit(clicker, false);
        Color tip = new Color(1f, 1f, 0.8f, 1f);
        k.Prim(PrimitiveType.Sphere, "Tip", root.transform, new Vector3(0f, d.bodyHeight, 0f), Vector3.one * Mathf.Max(0.2f, d.bodyDiameter * 1.8f), tip, default, 0f, 0.8f, false, tip);
    }

    /// <summary>Simple models for the consumables that are never placed: a fuel canister, a ghost-bait orb and a pet-treat bone.</summary>
    private GameObject BuildItemModel(DeviceKind kind, Device d)
    {
        GameObject root = new GameObject(d.displayName + " (Model)");
        Color main = d.color;
        Color dark = new Color(main.r * 0.4f, main.g * 0.4f, main.b * 0.4f, 1f);
        Color bright = Color.Lerp(main, Color.white, 0.55f);
        if (kind == DeviceKind.ComboFuel) BuildComboFuelItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.PesticideSpray) BuildPesticideSprayItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.FocusPotion) BuildFocusPotionItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.GhostBait) BuildGhostBaitItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.Coupling) BuildCouplingItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.ContainerHose) BuildContainerHoseItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.ConveyorBelt) BuildConveyorBeltItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.Gauntlet) BuildGauntletItemModel(root, main, dark, bright);
        else if (kind == DeviceKind.BaitBucket) BuildBaitBucketItemModel(root, main, dark, bright);
        else BuildDumbbellItemModel(root, main, dark, bright);
        return root;
    }

    // BuildItemModel: the ComboFuel model.
    private void BuildComboFuelItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        GameObject can = MakePrimitive(PrimitiveType.Cylinder, "Canister", root.transform, main, false);
        can.transform.localScale = new Vector3(0.5f, 0.45f, 0.5f);
        can.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        GameObject band = MakePrimitive(PrimitiveType.Cylinder, "Band", root.transform, bright, false);
        band.transform.localScale = new Vector3(0.52f, 0.06f, 0.52f);
        band.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        GameObject cap = MakePrimitive(PrimitiveType.Cylinder, "Cap", root.transform, dark, false);
        cap.transform.localScale = new Vector3(0.28f, 0.07f, 0.28f);
        cap.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        GameObject flame = MakePrimitive(PrimitiveType.Sphere, "Flame", root.transform, bright, false);
        flame.transform.localScale = new Vector3(0.22f, 0.34f, 0.22f);
        flame.transform.localPosition = new Vector3(0f, 1.2f, 0f);
    }

    // BuildItemModel: the PesticideSpray model.
    private void BuildPesticideSprayItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        // A spray can: a tall can, a dark cap and a little nozzle.
        GameObject can = MakePrimitive(PrimitiveType.Cylinder, "Can", root.transform, new Color(main.r, main.g, main.b, 1f), false);
        can.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        can.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        GameObject label = MakePrimitive(PrimitiveType.Cylinder, "Label", root.transform, bright, false);
        label.transform.localScale = new Vector3(0.44f, 0.14f, 0.44f);
        label.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        GameObject cap = MakePrimitive(PrimitiveType.Cylinder, "Cap", root.transform, dark, false);
        cap.transform.localScale = new Vector3(0.3f, 0.08f, 0.3f);
        cap.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        GameObject nozzle = MakePrimitive(PrimitiveType.Cube, "Nozzle", root.transform, dark, false);
        nozzle.transform.localScale = new Vector3(0.08f, 0.08f, 0.2f);
        nozzle.transform.localPosition = new Vector3(0f, 0.98f, -0.12f);
    }

    // BuildItemModel: the FocusPotion model.
    private void BuildFocusPotionItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        // A round bottle with a long neck, a cork and a bright band.
        GameObject body = MakePrimitive(PrimitiveType.Sphere, "Bottle", root.transform, new Color(main.r, main.g, main.b, 1f), false);
        body.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        GameObject neck = MakePrimitive(PrimitiveType.Cylinder, "Neck", root.transform, bright, false);
        neck.transform.localScale = new Vector3(0.22f, 0.2f, 0.22f);
        neck.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        GameObject cork = MakePrimitive(PrimitiveType.Cylinder, "Cork", root.transform, new Color(0.55f, 0.38f, 0.22f, 1f), false);
        cork.transform.localScale = new Vector3(0.18f, 0.08f, 0.18f);
        cork.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        GameObject band = MakePrimitive(PrimitiveType.Cylinder, "Band", root.transform, dark, false);
        band.transform.localScale = new Vector3(0.72f, 0.04f, 0.72f);
        band.transform.localPosition = new Vector3(0f, 0.4f, 0f);
    }

    // BuildItemModel: the GhostBait model.
    private void BuildGhostBaitItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        GameObject orb = MakePrimitive(PrimitiveType.Sphere, "Orb", root.transform, new Color(main.r, main.g, main.b, 1f), false);
        orb.transform.localScale = Vector3.one * 0.8f;
        orb.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject eye = MakePrimitive(PrimitiveType.Sphere, "Eye", root.transform, new Color(0.05f, 0.05f, 0.08f, 1f), false);
            eye.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);
            eye.transform.localPosition = new Vector3(i * 0.17f, 0.58f, -0.34f);
        }
        GameObject tail = MakePrimitive(PrimitiveType.Cylinder, "Tail", root.transform, bright, false);
        tail.transform.localScale = new Vector3(0.4f, 0.12f, 0.4f);
        tail.transform.localPosition = new Vector3(0f, 0.12f, 0f);
    }

    // BuildItemModel: the Coupling model.
    private void BuildCouplingItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        // A flange (a short wide disc) with a collar and a green arrow on top.
        GameObject flange = MakePrimitive(PrimitiveType.Cylinder, "Flange", root.transform, main, false);
        flange.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);
        flange.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        flange.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        GameObject collar = MakePrimitive(PrimitiveType.Cylinder, "Collar", root.transform, new Color(0.25f, 0.27f, 0.3f, 1f), false);
        collar.transform.localScale = new Vector3(0.62f, 0.18f, 0.62f);
        collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        collar.transform.localPosition = new Vector3(0f, 0.5f, 0.15f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            GameObject arm = MakePrimitive(PrimitiveType.Cube, "Arrow", root.transform, new Color(0.35f, 1f, 0.45f, 1f), false);
            arm.transform.localScale = new Vector3(0.1f, 0.08f, 0.4f);
            arm.transform.localRotation = Quaternion.Euler(0f, sx * 40f, 0f);
            arm.transform.localPosition = new Vector3(sx * 0.13f, 1.02f, 0.1f);
        }
    }

    // BuildItemModel: the ContainerHose model.
    private void BuildContainerHoseItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        // A coil of see-through hose: a few short tube pieces round a ring.
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f * Mathf.Deg2Rad;
            GameObject piece = MakePrimitive(PrimitiveType.Cylinder, "Hose", root.transform, main, false);
            piece.transform.localScale = new Vector3(0.18f, 0.2f, 0.18f);
            piece.transform.localRotation = Quaternion.Euler(0f, -i * 36f, 90f);
            piece.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.5f, 0.25f + i * 0.02f, Mathf.Sin(a) * 0.5f);
        }
    }

    // BuildItemModel: the ConveyorBelt model.
    private void BuildConveyorBeltItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        // A short belt: a dark slab with a bright stripe and a roller at each end.
        GameObject slab = MakePrimitive(PrimitiveType.Cube, "Belt", root.transform, new Color(0.14f, 0.14f, 0.17f, 1f), false);
        slab.transform.localScale = new Vector3(1.4f, 0.14f, 0.55f);
        slab.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        GameObject stripe = MakePrimitive(PrimitiveType.Cube, "Stripe", root.transform, main, false);
        stripe.transform.localScale = new Vector3(1.2f, 0.02f, 0.12f);
        stripe.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            GameObject roller = MakePrimitive(PrimitiveType.Cylinder, "Roller", root.transform, bright, false);
            roller.transform.localScale = new Vector3(0.2f, 0.28f, 0.2f);
            roller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            roller.transform.localPosition = new Vector3(sx * 0.7f, 0.2f, 0f);
        }
    }

    // BuildItemModel: the Gauntlet model.
    private void BuildGauntletItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        // A golden cube with a gem socket on each of its faces (the six stone colours).
        GameObject cube = MakePrimitive(PrimitiveType.Cube, "Gold Cube", root.transform, main, false);
        cube.transform.localScale = Vector3.one * 0.8f;
        cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        for (int i = 0; i < 6; i++)
        {
            GameObject gem = MakePrimitive(PrimitiveType.Cube, "Socket", root.transform, PixelInfinity.StoneColor(i), false);
            gem.transform.localScale = Vector3.one * 0.26f;
            gem.transform.localPosition = new Vector3(0f, 0.5f, 0f) + dirs[i] * 0.42f;
            gem.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
        }
    }

    // BuildItemModel: the BaitBucket model.
    private void BuildBaitBucketItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        GameObject body = MakePrimitive(PrimitiveType.Cylinder, "Bucket", root.transform, main, false);
        body.transform.localScale = new Vector3(0.7f, 0.3f, 0.7f);
        body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        GameObject rim = MakePrimitive(PrimitiveType.Cylinder, "Rim", root.transform, bright, false);
        rim.transform.localScale = new Vector3(0.76f, 0.025f, 0.76f);
        rim.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        GameObject water = MakePrimitive(PrimitiveType.Cylinder, "Water", root.transform, new Color(0.3f, 0.62f, 1f, 1f), false);
        water.transform.localScale = new Vector3(0.64f, 0.02f, 0.64f);
        water.transform.localPosition = new Vector3(0f, 0.56f, 0f);
        GameObject handle = MakePrimitive(PrimitiveType.Cube, "Handle", root.transform, dark, false);
        handle.transform.localScale = new Vector3(0.74f, 0.03f, 0.03f);
        handle.transform.localPosition = new Vector3(0f, 0.9f, 0f);
    }

    // BuildItemModel: any other item (a dumbbell: a shaft with a knob at each corner).
    private void BuildDumbbellItemModel(GameObject root, Color main, Color dark, Color bright)
    {
        GameObject shaft = MakePrimitive(PrimitiveType.Cylinder, "Shaft", root.transform, main, false);
        shaft.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
        shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        shaft.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                GameObject knob = MakePrimitive(PrimitiveType.Sphere, "Knob", root.transform, bright, false);
                knob.transform.localScale = Vector3.one * 0.3f;
                knob.transform.localPosition = new Vector3(sx * 0.52f, 0.4f, sz * 0.12f);
            }
    }

    /// <summary>BuildDeviceObject step: the devices that build their own object (funnel, recycler, generators, junction box...); false for the rest.</summary>
    private bool TryBuildSelfBuiltDevice(Device d, bool isPreview, out GameObject built)
    {
        if (d.kind == DeviceKind.Funnel)
        {
            built = isPreview ? PixelFunnel.CreateGhost(clicker) : displayOnly ? PixelFunnel.CreateDisplay(clicker) : PixelFunnel.Place(clicker, Vector3.zero, 0f);
            return true;
        }
        if (d.kind == DeviceKind.WoodChipper)
        {
            built = isPreview ? PixelWoodChipper.CreateGhost(clicker) : displayOnly ? PixelWoodChipper.CreateDisplay(clicker) : PixelWoodChipper.Place(clicker, Vector3.zero, 0f);
            return true;
        }
        if (d.kind == DeviceKind.Recycler)
        {
            if (isPreview)
            {
                GameObject ghost = PixelRecycler.CreateGhost(clicker);
                ghost.transform.localScale = Vector3.one * Mathf.Max(0.5f, d.radius > 0f ? d.radius : 1f);   // the placing ghost shows the real size (Operation: Bin Size), like Place does
                built = ghost;
                return true;
            }
            built = displayOnly ? PixelRecycler.CreateDisplay(clicker) : PixelRecycler.Place(clicker, Vector3.zero, 0f);
            return true;
        }
        if (IsGeneratorKind(d.kind))
        {
            float size = d.bodyDiameter > 0f ? d.bodyDiameter : 1f;
            built = isPreview ? PixelBiomassGenerator.CreateGhost(clicker, size) : PixelBiomassGenerator.CreateDisplay(clicker, size);
            return true;
        }
        if (d.kind == DeviceKind.PowerBank)
        {
            built = isPreview ? PixelPowerBank.CreateGhost(clicker) : PixelPowerBank.CreateDisplay(clicker);
            return true;
        }
        if (d.kind == DeviceKind.JunctionBox)
        {
            built = isPreview ? PixelJunctionBox.CreateGhost(clicker) : displayOnly ? PixelJunctionBox.CreateDisplay(clicker) : PixelJunctionBox.Place(clicker, Vector3.zero, 0f);
            return true;
        }
        if (d.kind == DeviceKind.Sweeper)
        {
            built = isPreview ? PixelSweeper.CreateGhost(clicker) : displayOnly ? PixelSweeper.CreateDisplay(clicker) : PixelSweeper.Place(clicker, Vector3.zero, 0f);
            return true;
        }
        if (d.kind == DeviceKind.SortingHopper)
        {
            built = isPreview ? PixelSortingHopper.CreateGhost(clicker) : displayOnly ? PixelSortingHopper.CreateDisplay(clicker) : PixelSortingHopper.Place(clicker, Vector3.zero, 0f);
            return true;
        }
        built = null;
        return false;
    }

    /// <summary>
    /// Builds the cylinder (and range disc, and for a real device the timer text) with its origin on the floor.
    /// </summary>
    private GameObject BuildDeviceObject(Device d, bool isPreview, out Transform suckPoint, out TextMeshPro timer)
    {
        if (TryBuildSelfBuiltDevice(d, isPreview, out GameObject selfBuilt))
        {
            suckPoint = null; timer = null;
            return selfBuilt;
        }

        GameObject root = new GameObject(isPreview ? d.displayName + " (Preview)" : d.displayName);

        if (d.kind == DeviceKind.Vacuum)
        {
            PixelVacuumDevice.BuildModel(clicker, root, d, isPreview, previewOpacity);
        }
        else if (d.kind == DeviceKind.Sprinkler)
        {
            PixelSprinklerDevice.BuildModel(clicker, root, d, isPreview, previewOpacity);
        }
        else if (d.kind == DeviceKind.Magnet)
        {
            PixelMagnetDevice.BuildModel(clicker, root, d, isPreview, previewOpacity);
        }
        else if (d.kind == DeviceKind.QuantumOrganizer)
        {
            PixelQuantumDevice.BuildModel(clicker, root, d, isPreview, previewOpacity);
            if (isPreview) PixelAimLaser.Attach(root, PixelQuantumDevice.Ports(d));
        }
        else if (d.kind == DeviceKind.StorageCrate)
        {
            PixelStorageCrate.BuildModel(clicker, root, d, isPreview, previewOpacity);
            if (isPreview) PixelAimLaser.Attach(root, PixelStorageCrate.Ports(d));   // (red aim beams out of its coupling holes while placing)
        }
        else if (d.kind == DeviceKind.LightningRod)
        {
            PixelLightningRod.BuildModel(clicker, root, d, isPreview, previewOpacity);
        }
        else if (d.kind == DeviceKind.ChargeBooster)
        {
            PixelChargeBooster.BuildModel(clicker, root, d, isPreview, previewOpacity);
        }
        else
        {
            // Cylinder body (Unity's cylinder is 2 units tall, hence the halved Y scale).
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>()); // never blocks clicks, placement rays or old pixels
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(d.bodyDiameter, d.bodyHeight * 0.5f, d.bodyDiameter);
            body.transform.localPosition = new Vector3(0f, d.bodyHeight * 0.5f, 0f);

            Color bodyColor = d.color;
            if (isPreview) bodyColor.a = previewOpacity;
            Material bodyMat = clicker.CreateVisualMaterial(bodyColor, isPreview);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            else body.GetComponent<Renderer>().material.color = bodyColor;
        }

        // Flat disc showing the reach.
        if (showRange && !displayOnly && d.kind != DeviceKind.QuantumOrganizer && d.kind != DeviceKind.StorageCrate)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = PixelPlacedDevice.RangeMark;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(d.radius * 2f, 0.01f, d.radius * 2f);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            Color discColor = d.color;
            discColor.a = rangeOpacity;
            Renderer dr = disc.GetComponent<Renderer>();
            Material discMat = clicker.CreateVisualMaterial(discColor, true);
            if (discMat != null) dr.sharedMaterial = discMat;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dr.receiveShadows = false;
        }

        // Where pixels are pulled to: the top of the cylinder.
        GameObject top = new GameObject("Suction Point");
        top.transform.SetParent(root.transform, false);
        top.transform.localPosition = new Vector3(0f, d.bodyHeight, 0f);
        suckPoint = top.transform;

        timer = !isPreview && !displayOnly ? MakeTimerText(root, d, d.bodyHeight + timerHeightAbove) : null;

        return root;
    }

    /// <summary>
    /// Builds the fan: a post, a round housing, spinning blades, a floor wedge showing the blown area and (for a real
    /// fan) the timer. The fan blows along the object's forward (+Z) direction.
    /// </summary>
    /// <summary>
    /// The Fan's look (2026-10-19): a pedestal fan. A weighted round base with a control pod (three buttons) at the front, a two-part post (dark tube,
    /// chrome upper tube, a collar with a knob), a pivot block and a motor housing with vent rings behind the head, four pitched, round-tipped blades on a
    /// chrome spinner ("Blades", spun round its forward axis by PixelFanDevice) and a chrome guard: front and back rings, side bars, radial spokes and a
    /// glowing badge in front. It blows along +Z.
    /// </summary>
    private GameObject BuildFanObject(Device d, bool isPreview, out Transform blades, out TextMeshPro timer)
    {
        GameObject root = new GameObject(isPreview ? d.displayName + " (Preview)" : d.displayName);
        PixelFanDevice.BuildModel(clicker, root, d, isPreview, previewOpacity, out blades);   // (the model itself: PixelFanDevice.BuildModel)

        // The area that gets blown: a flat wedge on the floor.
        if (showRange && !displayOnly)
        {
            GameObject wedge = new GameObject(PixelPlacedDevice.BlowAreaMark, typeof(MeshFilter), typeof(MeshRenderer));
            wedge.transform.SetParent(root.transform, false);
            wedge.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            wedge.GetComponent<MeshFilter>().sharedMesh = PixelFanDevice.WedgeMesh(d.radius, d.coneAngle);

            Color wedgeColor = d.color;
            wedgeColor.a = rangeOpacity;
            MeshRenderer mr = wedge.GetComponent<MeshRenderer>();
            Material mat = clicker.CreateVisualMaterial(wedgeColor, true);
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        timer = !isPreview && !displayOnly ? MakeTimerText(root, d, d.bodyHeight + d.bodyDiameter * 0.5f + timerHeightAbove) : null;

        return root;
    }

    /// <summary>The countdown text above a real device (BuildDeviceObject, BuildFanObject), 'height' above its origin.</summary>
    private TextMeshPro MakeTimerText(GameObject root, Device d, float height)
    {
        GameObject tg = new GameObject("Timer");
        tg.transform.SetParent(root.transform, false);
        tg.transform.localPosition = new Vector3(0f, height, 0f);
        TextMeshPro timer = tg.AddComponent<TextMeshPro>();
        timer.text = string.Format(timerFormat, Mathf.CeilToInt(DeviceSeconds(d)));
        timer.fontSize = timerFontSize;
        timer.fontStyle = FontStyles.Bold;
        timer.alignment = TextAlignmentOptions.Center;
        timer.color = timerColor;
        if (clicker.UIFont != null) timer.font = clicker.UIFont;
        return timer;
    }

    /// <summary>A primitive with no collider and the game's material tinted with 'color' (PixelFactoryKit.PlainPart).</summary>
    private GameObject MakePrimitive(PrimitiveType type, string objectName, Transform parent, Color color, bool transparent)
        => PixelFactoryKit.PlainPart(clicker, type, objectName, parent, color, transparent);
}
