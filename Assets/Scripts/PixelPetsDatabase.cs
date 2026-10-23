// ---- System notes ----
// PixelPetsDatabase.cs
//   Part of PixelPets: its Game Database entries (IPixelDatabaseSource, see PixelDatabase.cs) - one 'Pet' entry per pixel type that can have a pet
//   (find chance, spawn weight factor and husk slow-down read live; a line for the pets with a special trick); discovered = owned; linked to its pixel.
// ---------------------------------------------------

public partial class PixelPets : IPixelDatabaseSource
{
    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        steps.Add(PixelDatabase.PetsOrder, b =>
        {
            if (b.Clicker == null) return;
            foreach (PixelClicker.PixelTier t in b.Clicker.Tiers)
            {
                if (t == null || !CanHavePet(t.type)) continue;
                string pixelName = PixelDatabase.PixelName(t);
                string special = "";
                switch (t.type.ToString())
                {
                    case "Seed": special = "\nThis pet plants itself in the ground and sows seeds."; break;
                    case "Ghost": special = "\nThis pet only hovers."; break;
                    case "Glass": special = "\nThis pet shatters now and then and pulls itself back together."; break;
                    case "Vacuum": special = "\nThis pet sucks up nearby husks and counts them."; break;
                }
                PixelDatabase.Entry pet = b.Add("Pet", pixelName + " Pet",
                    "A companion of the " + pixelName + " pixel. Every click has a small chance (" + (FindChancePerClick * 100f).ToString("0.###") + "%) of finding it; automatic clicks count less, a Pet Treat helps. " +
                    "While it is out, " + pixelName + " comes up " + SpawnWeightFactor.ToString("0.#") + "x as often and its husks last " + DespawnSlowDown.ToString("0.#") + "x longer." + special);
                pet.discovered = IsOwned(t.type);
                b.Link(pet, "Pet of pixel", b.Pixel(t.type), "Has a pet");
            }
        });
    }
}
