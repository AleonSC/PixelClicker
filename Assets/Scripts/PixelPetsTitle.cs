using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Part of <see cref="PixelPets"/>: what the main menu needs to show the current save's pets (<see cref="PixelTitleWanderers"/>): the list of owned pets
/// (type + outfit) and a model of one - the pet's real-look pixel with its outfit on - built under a parent in a hidden studio.
/// </summary>
public partial class PixelPets
{
    public struct TitlePet
    {
        public PixelClicker.PixelType type;
        public int outfit;
    }

    /// <summary>Every pet of the current save (switched on or off), in the order they were found.</summary>
    public List<TitlePet> TitlePets()
    {
        List<TitlePet> list = new List<TitlePet>();
        foreach (Pet p in pets) list.Add(new TitlePet { type = p.type, outfit = p.outfit });
        return list;
    }

    /// <summary>A model of the pet (1 unit wide, centred on 'parent') with its outfit. Null when its pixel type does not exist.</summary>
    public GameObject BuildTitleModel(TitlePet pet, Transform parent)
    {
        if (clicker == null) return null;
        int tier = clicker.IndexOf(pet.type);
        if (tier < 0) return null;
        GameObject model = clicker.CreateDisplayPixel(tier, parent, 1f);
        if (model == null) return null;
        model.transform.localPosition = Vector3.zero;
        BuildOutfit(parent, pet.outfit, 1f);
        return model;
    }
}
