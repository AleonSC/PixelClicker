// ---- System notes ----
// PixelDevEvents.cs
//   EVENT TRIGGERS (Dev Hub tab 'Events'): fire the things that normally happen once or at random, to test their texts and looks.
//   * Tips: pick any first-time tip (PixelHints.AllHints) and Show it again (PixelHints.ShowNow); 'Forget all seen tips' (PixelHints.ResetSeen) makes every
//     tip show again as it happens; 'Did you know?' shows the next general tip now (PixelDidYouKnow.DevShowNow).
//   * Tutorials: replay one tutorial - the whole tutorial (PixelHints.ReplayTutorial), Cubie's visits (PixelGuideVendor.Replay), the Toggles / Shop / Skills
//     tutorials (their done-stats cleared, they start at the next calm moment) or the Woodcutting guide (PixelSkills.StartWoodGuide).
//   * Popups: a skill level-up popup (PixelSkillLevelPopup.Show, the skill's next level) and an achievement popup (PixelAchievements.DevShowPopup) - nothing is
//     granted. Your own text: show it as a tip box (PixelNotice) or as an event-log line (PixelHints.Announce) to see how a new text reads.
//   Minigames / visitors and rare finds already have spawners on the Main tab (Minigames, Super Rares), so they are not repeated here.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevEvents : MonoBehaviour
{
    private const float RowH = 42f, DropW = 470f, BtnX = 488f, BtnW = 202f;

    private static PixelDevEvents instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Events").AddComponent<PixelDevEvents>();
    }

    // The tutorials that can be replayed: name, and what replaying does (returns the line for the status text).
    private struct Tutorial { public string name; public Func<string> play; }

    private RectTransform root;
    private TMP_Dropdown tipDropdown, tutorialDropdown, skillDropdown, achievementDropdown;
    private int[] tipMap, achievementMap, skillMap;
    private readonly List<string> tipIds = new List<string>();
    private readonly List<PixelSkills.SkillPack> skillPacks = new List<PixelSkills.SkillPack>();
    private Tutorial[] tutorials;
    private TMP_InputField ownText;
    private TMP_Text status;
    private bool built;

    private void Awake()
    {
        instance = this;
        root = PixelDevHub.TabRoot("events");
        PixelDevHub.SetTabCallback("events", on => { if (!on) return; if (!built) Build(); RefreshLists(); });
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Build()
    {
        built = true;
        tutorials = new[]
        {
            new Tutorial { name = "Whole tutorial (intro, Cubie, all)", play = () => { PixelHints.ReplayTutorial(); return "The new-game intro, Cubie's visits and every tutorial play again from the next calm moment."; } },
            new Tutorial { name = "Cubie's visits", play = () => { PixelGuideVendor.Replay(); return "Cubie's visits play again (this also re-arms the Toggles, Shop and Skills tutorials)."; } },
            new Tutorial { name = "Toggles tutorial", play = () => { PixelTogglesTutorial.Replay(); return PixelToggles.TabVisible ? "The Toggles tutorial plays at the next calm moment." : "Re-armed, but it only plays once the Toggles tab is showing (buy the Auto Clicker)."; } },
            new Tutorial { name = "Shop tutorial", play = () => { PixelStats.Clear(PixelShopTutorial.DoneKey); PixelShopTutorial.RequestStart(); return "The Shop tutorial plays at the next calm moment (needs the shop to be available)."; } },
            new Tutorial { name = "Skills tutorial", play = () => { PixelStats.Clear(PixelSkills.TutorialDoneKey); return "The Skills tutorial plays when a skill pack is available and the screen is calm."; } },
            new Tutorial { name = "Woodcutting guide", play = () =>
            {
                if (PixelSkills.Instance == null) return "There is no skill system in this scene.";
                PixelSkills.Instance.StartWoodGuide();
                return "Started the Woodcutting guide (nothing happens if another tutorial is running or the Skills window was never opened).";
            } },
        };

        float y = 8f;
        PixelDevUI.Heading(root, "Tips", 10f, y, 400f); y += 38f;
        tipDropdown = PixelDevUI.Dropdown(root, "Tip", 10f, y, DropW, RowH, PixelDevUI.FontSize - 3f, escapeMasks: true);
        PixelDevUI.Button(root, "Show tip", BtnX, y, BtnW, RowH, PixelDevUI.Green, ShowTip); y += RowH + 8f;
        PixelDevUI.Button(root, "Forget all seen tips", 10f, y, 330f, RowH, PixelDevUI.ButtonColor, () =>
        {
            PixelHints.ResetSeen();
            SetStatus("Every first-time tip will show again the next time it happens.");
        });
        PixelDevUI.Button(root, "Show a 'Did you know?' tip", 348f, y, 342f, RowH, PixelDevUI.ButtonColor, () =>
        {
            PixelDidYouKnow tips = PixelFind.Cached<PixelDidYouKnow>();
            SetStatus(tips != null && tips.DevShowNow() ? "Showing the next 'Did you know?' tip." : "No 'Did you know?' tips found.");
        });
        y += RowH + 18f;

        PixelDevUI.Heading(root, "Tutorials", 10f, y, 400f); y += 38f;
        tutorialDropdown = PixelDevUI.Dropdown(root, "Tutorial", 10f, y, DropW, RowH, PixelDevUI.FontSize - 3f, escapeMasks: true);
        List<string> tutorialNames = new List<string>();
        foreach (Tutorial t in tutorials) tutorialNames.Add(t.name);
        tutorialDropdown.ClearOptions();
        tutorialDropdown.AddOptions(tutorialNames);
        PixelDevUI.Button(root, "Play", BtnX, y, BtnW, RowH, PixelDevUI.Green, () =>
        {
            int i = tutorialDropdown.value;
            if (i >= 0 && i < tutorials.Length) SetStatus(tutorials[i].play());
        });
        y += RowH + 18f;

        PixelDevUI.Heading(root, "Popups (nothing is given)", 10f, y, 500f); y += 38f;
        skillDropdown = PixelDevUI.Dropdown(root, "Skill", 10f, y, DropW, RowH, PixelDevUI.FontSize - 3f, escapeMasks: true);
        PixelDevUI.Button(root, "Level-up popup", BtnX, y, BtnW, RowH, PixelDevUI.Green, ShowLevelUp); y += RowH + 8f;
        achievementDropdown = PixelDevUI.Dropdown(root, "Achievement", 10f, y, DropW, RowH, PixelDevUI.FontSize - 3f, escapeMasks: true);
        PixelDevUI.Button(root, "Achievement", BtnX, y, BtnW, RowH, PixelDevUI.Green, ShowAchievement); y += RowH + 18f;

        PixelDevUI.Heading(root, "Launch", 10f, y, 400f); y += 38f;
        PixelDevUI.Button(root, "Studio intro", 10f, y, 330f, RowH, PixelDevUI.Green, () =>
            SetStatus(PixelStudioIntro.Replay() ? "Playing the Galactic Arcade studio intro (any key skips)." : "The studio intro is already playing (or missing)."));
        PixelDevUI.Button(root, "Reload pixel texts", 348f, y, 342f, RowH, PixelDevUI.ButtonColor, () =>
        {
            PixelTextOverrides.Reload();
            PixelDialogue.File.Reload();
            PixelClicker c = PixelFind.Cached<PixelClicker>();
            if (c != null) c.ReapplyTextOverrides();
            SetStatus("Read PixelTexts.txt and Dialogue.txt again: pixel names / guide / husk texts update now; dialogue set at start (tips, Cubie, visitors) needs a new play.");
        });
        y += RowH + 18f;

        PixelDevUI.Heading(root, "Your own text", 10f, y, 400f); y += 38f;
        ownText = PixelDevUI.Input(root, "Own Text", 10f, y, 680f, RowH, "Type a text to see how it reads in game");
        y += RowH + 8f;
        PixelDevUI.Button(root, "Show as tip box", 10f, y, 330f, RowH, PixelDevUI.ButtonColor, () =>
        {
            if (string.IsNullOrWhiteSpace(ownText.text)) { SetStatus("Type a text first."); return; }
            PixelNotice.Show(ownText.text);
            SetStatus("Showing your text in a tip box.");
        });
        PixelDevUI.Button(root, "Add to the event log", 348f, y, 342f, RowH, PixelDevUI.ButtonColor, () =>
        {
            if (string.IsNullOrWhiteSpace(ownText.text)) { SetStatus("Type a text first."); return; }
            PixelHints.Announce(ownText.text, ownText.text);
            SetStatus("Added your text to the event log (bottom left).");
        });
        y += RowH + 14f;

        status = PixelDevUI.WrapText(root, "Status", 10f, y, 680f, 60f, 18f, PixelDevUI.DimText);
        y += 66f;
        PixelDevUI.WrapText(root, "Note", 10f, y, 680f, 60f, 17f, PixelDevUI.DimText).text =
            "Minigames, visitors (Cubie's friends, the Lumberjack, the cats...) and rare finds have their own spawners on the Main tab: Minigames and Super Rares.";
    }

    private void SetStatus(string text) { if (status != null) status.text = text; }

    /// <summary>Fills the tip / skill / achievement drop-downs (they can change while playing: new skill packs, Inspector edits).</summary>
    private void RefreshLists()
    {
        PixelHints hints = PixelFind.Cached<PixelHints>();
        tipIds.Clear();
        List<string> tipLabels = new List<string>();
        if (hints != null)
            foreach (PixelHints.Hint h in hints.AllHints)
            {
                if (h == null || string.IsNullOrEmpty(h.id)) continue;
                tipIds.Add(h.id);
                string words = string.IsNullOrEmpty(h.text) ? "" : h.text.Replace("\n", " ");
                if (words.Length > 38) words = words.Substring(0, 38) + "...";
                tipLabels.Add(h.id + (words.Length > 0 ? "  -  " + words : ""));
            }
        int keepTip = PixelUIKit.Original(tipMap, tipDropdown.value);
        tipMap = PixelUIKit.AddSortedOptions(tipDropdown, tipLabels);
        tipDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(tipMap, keepTip));

        skillPacks.Clear();
        List<string> skillNames = new List<string>();
        if (PixelSkills.Instance != null)
            foreach (PixelSkills.SkillPack p in PixelSkills.Instance.Packs)
            {
                if (p == null) continue;
                skillPacks.Add(p);
                skillNames.Add(string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName);
            }
        int keepSkill = PixelUIKit.Original(skillMap, skillDropdown.value);
        skillMap = PixelUIKit.AddSortedOptions(skillDropdown, skillNames);
        skillDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(skillMap, keepSkill));

        PixelAchievements ach = PixelFind.Cached<PixelAchievements>();
        List<string> achNames = new List<string>();
        if (ach != null) for (int i = 0; i < ach.Count; i++) achNames.Add(ach.GetTitle(i));
        int keepAch = PixelUIKit.Original(achievementMap, achievementDropdown.value);
        achievementMap = PixelUIKit.AddSortedOptions(achievementDropdown, achNames);
        achievementDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(achievementMap, keepAch));
    }

    private void ShowTip()
    {
        int i = PixelUIKit.Original(tipMap, tipDropdown.value);
        if (i < 0 || i >= tipIds.Count) { SetStatus("No tips found."); return; }
        PixelHints.ShowNow(tipIds[i]);
        SetStatus(PixelHints.TipsEnabled ? "Showing tip '" + tipIds[i] + "' (after any tip already waiting)." : "Tips are switched off in Settings, so nothing shows.");
    }

    private void ShowLevelUp()
    {
        int i = PixelUIKit.Original(skillMap, skillDropdown.value);
        if (PixelSkills.Instance == null || i < 0 || i >= skillPacks.Count) { SetStatus("No skills found."); return; }
        PixelSkills.SkillPack pack = skillPacks[i];
        int level = PixelSkills.Instance.LevelOf(pack) + 1;
        PixelSkillLevelPopup.Show(pack, level, 1);
        SetStatus("Showing the level " + level + " popup (it waits while a menu is open).");
    }

    private void ShowAchievement()
    {
        PixelAchievements ach = PixelFind.Cached<PixelAchievements>();
        int i = PixelUIKit.Original(achievementMap, achievementDropdown.value);
        if (ach == null || i < 0 || i >= ach.Count) { SetStatus("No achievements found."); return; }
        ach.DevShowPopup(i);
        SetStatus("Showing the popup of '" + ach.GetTitle(i) + "'.");
    }
}
