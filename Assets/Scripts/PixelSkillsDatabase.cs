// ---- System notes ----
// PixelSkillsDatabase.cs
//   Part of PixelSkills: its Game Database entries (IPixelDatabaseSource, see PixelDatabase.cs).
//   - 'Skill' entries (SkillsOrder): description + perks + tree node lines; discovered = IsAvailable; pinnable; linked to its blocks, filler and hazard.
//   - 'Skill node' entries (SkillNodesOrder): one per tree node (cost, level needed, 'coming soon'), linked to its skill, the node it needs and the
//     block toggle it unlocks; discovered with its skill.
// ---------------------------------------------------
using System;
using System.Collections.Generic;

public partial class PixelSkills : IPixelDatabaseSource
{
    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        // ---- Skills ----
        steps.Add(PixelDatabase.SkillsOrder, b =>
        {
            foreach (SkillPack s in Packs)
            {
                if (s == null) continue;
                System.Text.StringBuilder sb = new System.Text.StringBuilder(PixelDatabase.Plain(s.description));
                if (s.perks != null) foreach (Perk perk in s.perks) sb.Append("\nLevel ").Append(perk.level).Append(": ").Append(PixelDatabase.Plain(perk.text));
                if (s.nodes != null) foreach (TreeNode n in s.nodes) sb.Append("\nSkill tree - ").Append(n.displayName).Append(": ").Append(PixelDatabase.Plain(n.description));
                PixelDatabase.Entry se = b.Add("Skill", s.displayName, sb.ToString());
                se.discovered = IsAvailable(s);
                se.pinType = "skill"; se.pinKey = s.id;
                b.Skills[s.id] = se;
                if (s.types != null) foreach (PixelClicker.PixelType t in s.types) b.Link(se, "Pixel block", b.Pixel(t), "Block of skill");
                if (Enum.TryParse(s.fillerType, out PixelClicker.PixelType filler)) b.Link(se, "Filler block", b.Pixel(filler), "Filler block of skill");
                if (Enum.TryParse(s.hazardType, out PixelClicker.PixelType hazard)) b.Link(se, "Hazard block", b.Pixel(hazard), "Hazard block of skill");
            }
        });

        // ---- Skill tree nodes (each its own entry, linked to its skill, the node it needs and the block it unlocks) ----
        steps.Add(PixelDatabase.SkillNodesOrder, b =>
        {
            foreach (SkillPack s in Packs)
            {
                if (s == null || s.nodes == null || !b.Skills.TryGetValue(s.id, out PixelDatabase.Entry owner)) continue;
                Dictionary<string, PixelDatabase.Entry> nodeEntries = new Dictionary<string, PixelDatabase.Entry>();
                foreach (TreeNode n in s.nodes)
                {
                    if (n == null || string.IsNullOrEmpty(n.displayName)) continue;
                    string text = PixelDatabase.Plain(n.description) + "\nCosts " + n.cost + " skill point" + (n.cost == 1 ? "" : "s") + " (a skill earns one per level above 1)." + (n.minLevel > 1 ? "\nNeeds " + s.displayName + " level " + n.minLevel + "." : "") + (n.implemented ? "" : "\nComing soon: the effect is not built yet.");
                    PixelDatabase.Entry ne = b.Add("Skill node", s.displayName + ": " + n.displayName, text);
                    ne.discovered = owner.discovered;
                    nodeEntries[n.id] = ne;
                    b.Link(ne, "Node of skill", owner, "Tree node");
                    if (Enum.TryParse(n.guestType, out PixelClicker.PixelType guest)) b.Link(ne, "Unlocks the block toggle for", b.Pixel(guest), "Toggle unlocked by tree node");
                }
                foreach (TreeNode n in s.nodes)
                    if (n != null && !string.IsNullOrEmpty(n.requires) && nodeEntries.TryGetValue(n.id, out PixelDatabase.Entry mine))
                        foreach (string req in n.requires.Split(','))
                            if (nodeEntries.TryGetValue(req.Trim(), out PixelDatabase.Entry needed)) b.Link(mine, "Needs node", needed, "Needed by node");
            }
        });
    }
}
