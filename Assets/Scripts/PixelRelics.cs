// ---- System notes ----
// PixelRelics.cs
//   The RELICS: the collectibles you WORK for (the rare ones - Dragon, God, Prime Cubes - you find). They are the 'Relics' family in
//   PixelRareSets (never rolled, one of each, a Collection Log section and gallery row like the other families; a page you haven't earned yet
//   says how to get it). A vendor hands one over when your rapport with him reaches its step (PixelRapport Vendor.reward / rewardStep,
//   given by PixelRapport.GrantReward). This component holds what each one DOES while you hold it (Inspector):
//   - Golden Axe (Big Tim, Friend): Woodcutting XP x (1 + goldenAxeXpBonus) - PixelSkills.AddXp reads XpFactor.
//   - Market Charm (the Wizard, Partner): a spell cast from any vendor's Sell view (PixelVisitorMinigameSell.cs): every vendor's market
//     recovers to full at once (PixelMarket.ClearSaturation), then it recharges for marketCharmCooldownMinutes of PLAY time (saved through
//     the stat 'relic.charmReady', like calling a vendor).
//   A NEW relic = its PixelType (appended) + a member in PixelRareSets.Relics + a block card (PixelBlockCatalog) + a vendor's reward in
//   PixelRapport + its effect here.
// ---------------------------------------------------
using UnityEngine;

/// <summary>What the Relics (collectibles you earn from vendors) do while you hold them.</summary>
public class PixelRelics : MonoBehaviour
{
    [Header("Golden Axe (Big Tim, Friend)")]
    [Range(0f, 1f)]
    [Tooltip("Extra Woodcutting XP while you hold the Golden Axe (0.1 = +10%).")]
    [SerializeField] private float goldenAxeXpBonus = 0.1f;

    [Header("Market Charm (the Wizard, Partner)")]
    [Min(1f)]
    [Tooltip("Play minutes the Market Charm needs to recharge after it is cast.")]
    [SerializeField] private float marketCharmCooldownMinutes = 60f;

    /// <summary>The scene's relics component (null if there is none: the built-in values are used).</summary>
    public static PixelRelics Instance => PixelFind.Cached<PixelRelics>();

    /// <summary>Do you hold this relic?</summary>
    public static bool Holds(PixelClicker.PixelType type)
    {
        PixelClicker c = PixelFind.Cached<PixelClicker>();
        int i = c != null ? c.IndexOf(type) : -1;
        return i >= 0 && c.Tiers[i].count >= 1d;
    }

    /// <summary>x on a skill's XP from the relics you hold (the Golden Axe: Woodcutting).</summary>
    public static float XpFactor(string skillId)
    {
        if (skillId != PixelSkills.WoodcuttingId || !Holds(PixelClicker.PixelType.GoldenAxe)) return 1f;
        PixelRelics r = Instance;
        return 1f + (r != null ? r.goldenAxeXpBonus : 0.1f);
    }

    // ---- The Market Charm ----

    private const string CharmReadyKey = "relic.charmReady";   // play seconds when it can be cast again (only ever rises, so PixelStats.Best keeps it)

    private static double PlaySeconds { get { PixelStats s = PixelFind.Cached<PixelStats>(); return s != null ? s.PlaySeconds : Time.time; } }

    /// <summary>Seconds of play until the Market Charm can be cast again (0 = now).</summary>
    public static double CharmReadyIn => System.Math.Max(0d, PixelStats.Total(CharmReadyKey) - PlaySeconds);

    public static bool HoldsCharm => Holds(PixelClicker.PixelType.MarketCharm);

    /// <summary>Casts the Market Charm: every vendor pays full price again, then it recharges. False if you don't hold it or it is recharging.</summary>
    public static bool CastCharm()
    {
        if (!HoldsCharm || CharmReadyIn > 0d) return false;
        PixelRelics r = Instance;
        PixelStats.Best(CharmReadyKey, PlaySeconds + (r != null ? r.marketCharmCooldownMinutes : 60f) * 60d);
        PixelMarket market = PixelMarket.Instance;
        if (market != null) market.ClearSaturation();
        PixelStats.Count("relic.charmCast");
        PixelAudio.Play("achievement");
        PixelHints.Announce(PixelDialogue.File.Text("Messages", "market charm", "The Market Charm glows: every vendor pays full price again."));
        return true;
    }
}
