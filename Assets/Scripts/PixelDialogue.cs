// ---- System notes ----
// PixelDialogue.cs
//   The user's own file for every 'Got it' box and every character's lines: Assets/Resources/Dialogue.txt (read through the shared PixelTextFile).
//   Sections (the [bracketed] key is how the game finds the text - don't change it):
//     [Tip: <id>]               first-time tips (PixelHints): text, overlay line (the short event-log line)
//     [Intro tips]              the new-game intro boxes: log, inventory, earn, right click
//     [Tutorial: Shop / Toggles / Skills]   the guided tutorials' boxes: step 1, step 2... (only the text; the order and what each step shows stay in code)
//     [Guide: Woodcutting]      the Woodcutting guide: question, later, step 1...
//     [Perk popup: <skill> level <n>]   the popup when a skill level gives a perk
//     [Cubie]                   name, next button, last button, recycler gift, visit 1 page 1..., visit 2 page 1... (pages can be added or removed: '-')
//     [Visitor: <id>]           each visitor / vendor (tinkerer, wizard, entrepreneur, farmer, prospector, lumberjack, egg, bean): name, greeting
//                               ({0} = the discount %), wares title, buttons, arrival log line, peek bubble, missed log, and their extra lines
//                               (farewell, gift farewell, dragon aside, gauntlet aside)
//     [Minigame: Black Hole]    its first-hole message ({0} = a number)
//     [Device placing]          the hints while placing a belt sorter / combiner / control lever / coupling on a belt
//     [Messages]                one-off event-log / notice lines said by code with no Inspector (item used up, market charm, view failed, Ultra gain)
//   'File wins': each owner copies the file's text over its Inspector / built-in text where it sets up its defaults (Awake), so the display
//   code is unchanged. Empty / missing = the game's own text. The file was first built from the code's built-in texts by tools/dialogue/build.py
//   (python3 tools/dialogue/build.py - it OVERWRITES the file, so only for a fresh start); in the editor the game also adds any missing text on play (PixelTextFile.Flush)
//   and new texts are appended later. Dev Hub > Events > 'Reload pixel texts' also reloads this file (texts set in Awake take a new play).
//   NOT in it: Did-you-know lines (Assets/Resources/DidYouKnow.txt), Big Tim's live offer sentences (built from numbers). Most event-log lines
//   are Inspector text fields on their own script; only the ones from code with no Inspector are here ([Messages]).
// ---------------------------------------------------
public static class PixelDialogue
{
    public static readonly PixelTextFile File = new PixelTextFile("Dialogue",
        "# DIALOGUE - every 'Got it' box and every character's lines. Edit them here; the game reads this file when it starts.\n" +
        "#\n" +
        "# The word in [brackets] tells the game where a text goes - do not change it. Each line is 'field: text'.\n" +
        "# Leave a text empty to keep the game's own one. Everything stays on ONE line: write \\n for a new line.\n" +
        "# Rich text works: <b>bold</b>, <i>italic</i>, <color=#FFD27F>colour</color>.\n" +
        "# Keep the {words in braces} where you want the game to fill something in: {0} (a number / name), {gift}, {recycler}, {key:EventLog}...\n" +
        "# Lists (Cubie's pages, tutorial steps): change the text of each numbered line. For Cubie you can add a page (the next number) or\n" +
        "#   remove one (write just -). Tutorial steps keep their number of boxes (each one also opens a tab / points at something).\n" +
        "# New texts the game gets later are added to the end of this file by themselves (in the editor). Lines starting with # are notes.\n");

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { File.Reload(); }

    /// <summary>Copies the file's text over each item's text in a list (tutorial steps), numbered 'step 1', 'step 2'...; the list keeps its length.</summary>
    public static void Steps<T>(string section, System.Collections.Generic.IList<T> list, System.Func<T, string> get, System.Action<T, string> set)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) set(list[i], File.Text(section, "step " + (i + 1), get(list[i])));
    }
}
