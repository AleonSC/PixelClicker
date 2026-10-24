// ---- System notes ----
// PixelBalanceReport.cs
//   Dev tool (Dev Tools > Tools > 'Balance report'): writes <persistentDataPath>/Snapshots/balance_<time>.txt and copies it to the clipboard - one plain
//   text / markdown report meant to be pasted into ANY AI. It starts with a READ ME block (what the game is, the balance goal, what every column
//   means, what to answer), then the assumptions, a cross-skill comparison at each reference level (XP per click, minutes to reach each level at the
//   manual and the auto clicking speed, pay per click, flagged outliers), a per-skill block table (share, clicks, pay, role) and the Inspector
//   levers with their current values, and a CSV copy of the comparison.
//   How the numbers are made: no formulas are copied. For every pack the report sets PixelSkills.PreviewPackId / PreviewLevel (read by
//   PixelSkills.ActiveId and the filler / hazard share; PixelTier.CanSpawn counts the pack's own pixels as unlocked via PixelSkills.PreviewForces),
//   so PixelClicker.SpawnShare gives the same shares the real spawn roll uses (filler fade, hazard share, Fishing's fixed table, special blocks,
//   pets), then clears the preview again (try / finally). XP is per collected block (1, x autoXpFactor for auto clicks), as PixelSkills.OnCollected.
//   NOT modelled: Value / pack upgrades and multipliers (base payouts only), minigame XP multiplier, bonus bank, Block Hold, skill-tree effects,
//   the Fishing Weak Spots round, hazard defusing, guests, special blocks that are not unlocked in the save, Standard's rotation beyond what the
//   save has unlocked. The reference speeds / levels are fields on PixelDevTools (Balance report section).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class PixelBalanceReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private class Row { public string name, role; public float share, clicks; public double pay; }

    private class Stats
    {
        public List<Row> rows = new List<Row>();
        public float avgClicks, xpPerBlock, hazardShare, fillerShare;
        public double payPerBlock, payPerClick, xpPerClick;
    }

    private class PackReport
    {
        public PixelSkills.SkillPack pack;
        public Dictionary<int, Stats> at = new Dictionary<int, Stats>();
        public Dictionary<int, double> minutesManual = new Dictionary<int, double>(), minutesAuto = new Dictionary<int, double>();
    }

    /// <summary>Builds and saves the report; returns the file path (null if it could not be written).</summary>
    public static string Capture(float manualCps, float autoCps, int[] levels)
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        PixelSkills skills = PixelSkills.Instance;
        if (clicker == null || skills == null) return null;
        if (levels == null || levels.Length == 0) levels = new[] { 1, 10, 20, 30 };
        Array.Sort(levels);
        manualCps = Math.Max(0.1f, manualCps);
        autoCps = Math.Max(0.1f, autoCps);
        int maxLevel = Math.Min(99, levels[levels.Length - 1]);

        var reports = new List<PackReport>();
        foreach (PixelSkills.SkillPack p in skills.Packs)
        {
            if (p == null || p.id == PixelSkills.OfflineId) continue;
            if (p.id != PixelSkills.StandardId && (p.types == null || p.types.Length == 0)) continue;
            reports.Add(Build(clicker, skills, p, manualCps, autoCps, levels, maxLevel));
        }

        string text = Compose(skills, reports, manualCps, autoCps, levels);
        return PixelDevProbe.Save("balance", text, false);
    }

    private static PackReport Build(PixelClicker clicker, PixelSkills skills, PixelSkills.SkillPack p, float manualCps, float autoCps, int[] levels, int maxLevel)
    {
        var r = new PackReport { pack = p };
        double minManual = 0d, minAuto = 0d;
        double growth = Math.Max(1.05d, p.xpGrowth), need = skills.BaseXpOf(p);   // (with the global xpCurveScale)
        float autoFactor = skills.AutoXpFactor;
        try
        {
            PixelSkills.PreviewPackId = p.id;
            for (int level = 1; level <= maxLevel; level++)
            {
                PixelSkills.PreviewLevel = level;
                Stats s = Measure(clicker, p);
                if (Array.IndexOf(levels, level) >= 0) r.at[level] = s;
                // time to climb from this level to the next at this level's mix
                double xpPerMinManual = s.xpPerClick * manualCps * 60d, xpPerMinAuto = s.xpPerClick * autoCps * 60d * autoFactor;
                minManual += xpPerMinManual > 0d ? need / xpPerMinManual : double.PositiveInfinity;
                minAuto += xpPerMinAuto > 0d ? need / xpPerMinAuto : double.PositiveInfinity;
                need *= growth;
                int reached = level + 1;
                if (Array.IndexOf(levels, reached) >= 0) { r.minutesManual[reached] = minManual; r.minutesAuto[reached] = minAuto; }
            }
        }
        finally { PixelSkills.PreviewPackId = null; PixelSkills.PreviewLevel = 0; }
        return r;
    }

    /// <summary>One snapshot of the rotation under the current preview: every pixel type with a share, its clicks and pay.</summary>
    private static Stats Measure(PixelClicker clicker, PixelSkills.SkillPack p)
    {
        var s = new Stats();
        var tiers = clicker.Tiers;
        for (int i = 0; i < tiers.Length; i++)
        {
            float share = clicker.SpawnShare(i);
            if (share <= 0f) continue;
            var t = tiers[i];
            string role = "block";
            string name = t.type.ToString();
            if (!string.IsNullOrEmpty(p.fillerType) && name == p.fillerType) role = "filler";
            else if (!string.IsNullOrEmpty(p.hazardType) && name == p.hazardType) role = "hazard";
            else if (t.rareDrop) role = "rare drop";
            else if (p.types != null && Array.IndexOf(p.types, t.type) < 0 && p.id != PixelSkills.StandardId) role = "special/guest";
            float clicks = t.randomClicks ? (t.randomClicksMin + t.randomClicksMax) * 0.5f : Math.Max(1, t.clicksToCollect);
            double pay = role == "hazard" ? 0d : t.amountPerClick;
            s.rows.Add(new Row { name = name, role = role, share = share, clicks = clicks, pay = pay });
        }
        float total = 0f;
        foreach (Row row in s.rows) total += row.share;
        if (total <= 0f) return s;
        foreach (Row row in s.rows)
        {
            row.share /= total;   // (the shares of special blocks can leave a few % unaccounted; normalise so the table sums to 100%)
            s.avgClicks += row.share * row.clicks;
            s.payPerBlock += row.share * row.pay;
            if (row.role == "hazard") s.hazardShare += row.share; else s.xpPerBlock += row.share;
            if (row.role == "filler") s.fillerShare += row.share;
        }
        s.payPerClick = s.avgClicks > 0f ? s.payPerBlock / s.avgClicks : 0d;
        s.xpPerClick = s.avgClicks > 0f ? s.xpPerBlock / s.avgClicks : 0d;
        return s;
    }

    private static string F(double v, string fmt = "0.##") => double.IsInfinity(v) || double.IsNaN(v) ? "never" : v.ToString(fmt, Inv);
    private static string Pct(double v) => (v * 100d).ToString(v >= 0.1d ? "0" : "0.0", Inv) + "%";

    private static string Compose(PixelSkills skills, List<PackReport> reports, float manualCps, float autoCps, int[] levels)
    {
        var sb = new StringBuilder(16384);
        AppendIntro(sb, skills, manualCps, autoCps);
        AppendComparison(sb, skills, reports, manualCps, autoCps, levels);
        AppendOutliers(sb, reports, levels);
        AppendPerSkill(sb, reports, levels);
        AppendLevers(sb, skills, reports);
        AppendCsv(sb, skills, reports, manualCps, autoCps, levels);
        return sb.ToString();
    }

    // Compose step: the title, the read-me for the AI and the assumptions.
    private static void AppendIntro(StringBuilder sb, PixelSkills skills, float manualCps, float autoCps)
    {
        sb.AppendLine("# Pixel Clicker - skill balance report  (" + DateTime.UtcNow.ToString("u") + ", build " + UnityEngine.Application.version + ")");
        sb.AppendLine();
        sb.AppendLine("## READ ME FIRST (instructions for the AI reading this)");
        sb.AppendLine("Pixel Clicker is a 3D incremental clicker (Unity). The player clicks a cube; each click hits the block on it, a block breaks after N clicks and pays currency + 1 XP");
        sb.AppendLine("for the skill in play. Each SKILL (Standard, Mining, Woodcutting, Foraging, Fishing, Farming, Electrician...) swaps the cube's blocks for its own set; the player trains one at a time.");
        sb.AppendLine("A skill has a FILLER block (common, near-useless, fades as you level), a HAZARD block (pays nothing, punishes XP if set off, share grows with level) and good / rare blocks.");
        sb.AppendLine("Auto clicking earns the same pay but only a fraction of the XP of a manual click.");
        sb.AppendLine();
        sb.AppendLine("DESIGN GOAL (from the developer): a chill sandbox where players express their playstyle and personality. No method should be clearly better than the others, but perfect");
        sb.AppendLine("balance is NOT expected - big outliers matter, small differences are fine. Engagement and variety matter more than exactness.");
        sb.AppendLine();
        sb.AppendLine("WHAT TO DO: 1) Read 'Cross-skill comparison' and 'Outliers'. 2) Say which skills are much faster / slower to level, and which pay far more / less per click, and whether the gap");
        sb.AppendLine("is likely intentional (rare blocks, tough ores) or a problem. 3) For each real problem propose a SPECIFIC change using the 'Levers' section (name the field, the current value");
        sb.AppendLine("and a new value) and say what the new numbers would be. 4) Point out anything in the assumptions that makes this report misleading. Keep the answer short and numbered.");
        sb.AppendLine();
        sb.AppendLine("COLUMNS: share = chance a new block on the cube is this type (the game's own spawn roll). clicks = clicks to break one (average if random). pay = currency per broken block.");
        sb.AppendLine("XP/click = XP per click (a broken block gives 1 XP, so tough blocks and hazards slow XP). minutes-to-level = cumulative minutes from level 1 using each level's own mix.");
        sb.AppendLine("Pay is in the skill's OWN currencies (wood, ore, fish...), which are NOT directly comparable between skills; XP and level times ARE comparable.");
        sb.AppendLine();
        sb.AppendLine("## Assumptions");
        sb.AppendLine("- Manual clicking speed: " + F(manualCps) + " clicks/s (the game's hard cap is about 14/s). Auto clicking: " + F(autoCps) + " clicks/s, XP x" + F(skills.AutoXpFactor) + " per auto click.");
        sb.AppendLine("- Every block of the skill is assumed unlocked; pets, guests and bait are as in the current save. Base payouts only.");
        sb.AppendLine("- NOT modelled: shop Value upgrades, pack upgrades and multipliers, minigame XP multiplier, bonus XP bank, Block Hold, skill-tree effects, hazard defusing and XP penalties,");
        sb.AppendLine("  Fishing's Weak Spots round, special blocks (e.g. Great Oak) not unlocked in the save, and Standard's blocks beyond what the save has unlocked.");
        sb.AppendLine("- Hazards: their clicks are counted (you hit them) but they pay nothing and give no XP.");
        sb.AppendLine();
    }

    // Compose step: the cross-skill comparison table at each reference level.
    private static void AppendComparison(StringBuilder sb, PixelSkills skills, List<PackReport> reports, float manualCps, float autoCps, int[] levels)
    {
        sb.AppendLine("## Cross-skill comparison");
        foreach (int lv in levels)
        {
            sb.AppendLine();
            sb.AppendLine("### At skill level " + lv);
            sb.AppendLine("| Skill | avg clicks/block | XP/click | XP/min manual | XP/min auto | pay/click | filler | hazard | min to reach level " + lv + " (manual / auto) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (PackReport r in reports)
            {
                if (!r.at.TryGetValue(lv, out Stats s) || s.rows.Count == 0) { sb.AppendLine("| " + r.pack.displayName + " | (no rotation) |||||||"); continue; }
                string to = lv > 1 && r.minutesManual.ContainsKey(lv) ? F(r.minutesManual[lv], "0.#") + " / " + F(r.minutesAuto[lv], "0.#") : "-";
                sb.AppendLine("| " + r.pack.displayName + " | " + F(s.avgClicks) + " | " + F(s.xpPerClick, "0.###") + " | " + F(s.xpPerClick * manualCps * 60d, "0.#") + " | "
                              + F(s.xpPerClick * autoCps * 60d * skills.AutoXpFactor, "0.#") + " | " + F(s.payPerClick, "0.###") + " | " + Pct(s.fillerShare) + " | " + Pct(s.hazardShare) + " | " + to + " |");
            }
        }
    }

    // Compose step: skills more than 50% above or below the median at the second reference level.
    private static void AppendOutliers(StringBuilder sb, List<PackReport> reports, int[] levels)
    {
        int refLevel = levels[Math.Min(1, levels.Length - 1)];   // the level the outlier check judges (the second reference level)
        sb.AppendLine();
        sb.AppendLine("## Outliers (judged at level " + refLevel + "; flagged when more than 50% above or below the median)");
        var xp = new List<double>(); var tl = new List<double>();
        foreach (PackReport r in reports)
        {
            if (r.at.TryGetValue(refLevel, out Stats s) && s.xpPerClick > 0d) xp.Add(s.xpPerClick);
            if (refLevel > 1 && r.minutesManual.TryGetValue(refLevel, out double m) && !double.IsInfinity(m)) tl.Add(m);
        }
        double medXp = Median(xp), medT = Median(tl);
        int flagged = 0;
        foreach (PackReport r in reports)
        {
            if (!r.at.TryGetValue(refLevel, out Stats s) || s.rows.Count == 0) continue;
            if (medXp > 0d && (s.xpPerClick > medXp * 1.5d || s.xpPerClick < medXp * 0.5d)) { sb.AppendLine("- " + r.pack.displayName + ": XP/click " + F(s.xpPerClick, "0.###") + " vs median " + F(medXp, "0.###") + " (" + F(s.xpPerClick / medXp, "0.#") + "x)"); flagged++; }
            if (medT > 0d && r.minutesManual.TryGetValue(refLevel, out double m) && (m > medT * 1.5d || m < medT * 0.5d)) { sb.AppendLine("- " + r.pack.displayName + ": " + F(m, "0.#") + " min to level " + refLevel + " vs median " + F(medT, "0.#") + " (" + F(m / medT, "0.#") + "x)"); flagged++; }
        }
        if (flagged == 0) sb.AppendLine("- none");
    }

    // Compose step: each skill's block table at each reference level.
    private static void AppendPerSkill(StringBuilder sb, List<PackReport> reports, int[] levels)
    {
        foreach (PackReport r in reports)
        {
            PixelSkills.SkillPack p = r.pack;
            sb.AppendLine();
            sb.AppendLine("## " + p.displayName + "  (id " + p.id + ")");
            if (!string.IsNullOrEmpty(p.description)) sb.AppendLine(p.description.Replace("\n", " "));
            foreach (int lv in levels)
            {
                if (!r.at.TryGetValue(lv, out Stats s) || s.rows.Count == 0) continue;
                sb.AppendLine();
                sb.AppendLine("Level " + lv + ": avg " + F(s.avgClicks) + " clicks/block, " + F(s.payPerBlock, "0.##") + " pay/block, pay/click " + F(s.payPerClick, "0.###") + ", XP/click " + F(s.xpPerClick, "0.###"));
                sb.AppendLine("| block | role | share | clicks | pay |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (Row row in s.rows) sb.AppendLine("| " + row.name + " | " + row.role + " | " + Pct(row.share) + " | " + F(row.clicks) + " | " + F(row.pay) + " |");
            }
        }
    }

    // Compose step: the Inspector fields that tune each skill.
    private static void AppendLevers(StringBuilder sb, PixelSkills skills, List<PackReport> reports)
    {
        sb.AppendLine();
        sb.AppendLine("## Levers (Inspector fields; 0 = the coded default shown)");
        sb.AppendLine("| Skill | baseXp (XP for level 2) | xpGrowth (x per level) | levelCap | filler (start / end share, fade level) | hazard (base / per level / max) | perks built | tree nodes built (points) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (PackReport r in reports)
        {
            PixelSkills.SkillPack p = r.pack;
            int perksOn = 0, perks = p.perks != null ? p.perks.Length : 0, nodesOn = 0, nodes = p.nodes != null ? p.nodes.Length : 0, pts = 0;
            for (int i = 0; i < perks; i++) if (p.perks[i] != null && p.perks[i].implemented) perksOn++;
            for (int i = 0; i < nodes; i++) if (p.nodes[i] != null) { pts += p.nodes[i].cost; if (p.nodes[i].implemented) nodesOn++; }
            string filler = string.IsNullOrEmpty(p.fillerType) ? "-" : p.fillerType + " (" + F(p.fillerStartShare > 0f ? p.fillerStartShare : 0.6f) + " / " + F(p.fillerEndShare > 0f ? p.fillerEndShare : 0.1f) + ", lvl " + (p.fillerFadeLevel > 1 ? p.fillerFadeLevel : 20) + ")";
            string hazard = string.IsNullOrEmpty(p.hazardType) ? "-" : p.hazardType + " (" + F(p.hazardBaseShare > 0f ? p.hazardBaseShare : 0.03f) + " / " + F(p.hazardPerLevelShare > 0f ? p.hazardPerLevelShare : 0.002f) + " / " + F(p.hazardMaxShare > 0f ? p.hazardMaxShare : 0.08f) + ")";
            sb.AppendLine("| " + p.displayName + " | " + F(skills.BaseXpOf(p)) + " | " + F(p.xpGrowth) + " | " + skills.LevelCapOf(p) + " | " + filler + " | " + hazard + " | " + perksOn + "/" + perks + " | " + nodesOn + "/" + nodes + " (" + pts + ") |");
        }
        sb.AppendLine("Global: autoXpFactor " + F(skills.AutoXpFactor) + " (PixelSkills). Per-block numbers (amountPerClick, clicksToCollect, spawnWeight) are on the PixelClicker tier list.");
    }

    // Compose step: the same numbers as CSV.
    private static void AppendCsv(StringBuilder sb, PixelSkills skills, List<PackReport> reports, float manualCps, float autoCps, int[] levels)
    {
        sb.AppendLine();
        sb.AppendLine("## CSV (skill,level,avg_clicks_per_block,xp_per_click,xp_per_min_manual,xp_per_min_auto,pay_per_click,filler_share,hazard_share,minutes_to_level_manual,minutes_to_level_auto)");
        foreach (PackReport r in reports)
            foreach (int lv in levels)
            {
                if (!r.at.TryGetValue(lv, out Stats s) || s.rows.Count == 0) continue;
                string a = r.minutesManual.TryGetValue(lv, out double mm) ? F(mm, "0.##") : "", b = r.minutesAuto.TryGetValue(lv, out double ma) ? F(ma, "0.##") : "";
                sb.AppendLine(r.pack.id + "," + lv + "," + F(s.avgClicks, "0.###") + "," + F(s.xpPerClick, "0.####") + "," + F(s.xpPerClick * manualCps * 60d, "0.##") + "," + F(s.xpPerClick * autoCps * 60d * skills.AutoXpFactor, "0.##")
                              + "," + F(s.payPerClick, "0.####") + "," + F(s.fillerShare, "0.###") + "," + F(s.hazardShare, "0.###") + "," + a + "," + b);
            }
    }

    private static double Median(List<double> v)
    {
        if (v.Count == 0) return 0d;
        v.Sort();
        int n = v.Count;
        return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) * 0.5d;
    }
}
