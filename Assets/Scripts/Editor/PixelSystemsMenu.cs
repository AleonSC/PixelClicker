// ---- System notes ----
// Editor/PixelSystemsMenu.cs
//   Editor-only menu for the game's system components (the list is PixelSystems.All):
//   - Pixel Clicker > Add Missing Systems To Scene: every system that is not in the open scene yet is added to a 'Pixel Systems' object,
//     on a child per group (UI, Core, Skills, Rares and Shows, Characters, Camera, World). Then they exist in EDIT mode, so their Inspector
//     values can be changed and saved with the scene (before, most were only added when Play started, and every change was lost when it
//     stopped). Systems already in the scene, wherever they are, are left alone.
//   - Pixel Clicker > Move Systems Off The Clicker: every system found ANYWHERE in the scene outside 'Pixel Systems' (the clicker, its children,
//     the camera...) is moved into its group with all its Inspector values (Copy Component + Paste Component As New, then the old one is removed);
//     if the group already had a default copy (from Add Missing Systems), that default is removed so the hand-set values win. Asks first, listing them.
//   Both work outside Play mode only, can be undone (Ctrl+Z) and mark the scene as changed - save the scene to keep the result.
// ---------------------------------------------------
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PixelSystemsMenu
{
    [MenuItem("Pixel Clicker/Add Missing Systems To Scene")]
    private static void AddMissingSystems()
    {
        if (!CanEdit()) return;
        int added = 0;
        foreach (PixelSystems.Entry e in PixelSystems.All)
        {
            if (e.type == null || PixelFind.First(e.type) != null) continue;   // already in the scene somewhere
            Undo.AddComponent(Group(e.group), e.type);
            added++;
        }
        Finish(added == 0
            ? "Every system is already in the scene."
            : added + " system(s) added under '" + PixelSystems.RootName + "'. Save the scene to keep them.");
    }

    [MenuItem("Pixel Clicker/Move Systems Off The Clicker")]
    private static void MoveSystemsOffClicker()
    {
        if (!CanEdit()) return;
        // Every system component ANYWHERE in the scene outside 'Pixel Systems' (on the clicker, a child of it, the camera...) - the ones set up by hand
        // (Copy Component in Play mode, Paste after it) - is moved into its group with all its values. If the group already holds an untouched copy
        // (added by 'Add Missing Systems To Scene'), that copy is the one removed: the hand-set one wins.
        GameObject root = GameObject.Find(PixelSystems.RootName);
        var moves = new System.Collections.Generic.List<Component>();
        var replaced = new System.Collections.Generic.List<Component>();
        System.Text.StringBuilder list = new System.Text.StringBuilder();
        foreach (PixelSystems.Entry e in PixelSystems.All)
        {
            if (e.type == null) continue;
            Component outside = null, inside = null;
            foreach (Object o in Resources.FindObjectsOfTypeAll(e.type))
            {
                Component c = o as Component;
                if (c == null || EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid()) continue;   // (prefabs / assets, not scene objects)
                bool underRoot = root != null && c.transform.IsChildOf(root.transform);
                if (underRoot) { if (inside == null) inside = c; }
                else if (outside == null) outside = c;
            }
            if (outside == null) continue;
            moves.Add(outside);
            if (inside != null) replaced.Add(inside);
            list.Append("\n- ").Append(e.type.Name).Append(" (on '").Append(outside.gameObject.name).Append("')").Append(inside != null ? " - replaces the default copy" : "");
        }
        if (moves.Count == 0)
        {
            Finish("Every system is already inside '" + PixelSystems.RootName + "' - nothing to move.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Pixel Systems", "Move these systems into '" + PixelSystems.RootName + "' with all their values?" + list, "Move", "Cancel")) return;
        foreach (Component c in replaced) if (c != null) Undo.DestroyObjectImmediate(c);
        int moved = 0;
        foreach (Component old in moves)
        {
            PixelSystems.Entry e = System.Array.Find(PixelSystems.All, x => x.type == old.GetType());
            GameObject group = Group(e.type != null ? e.group : "Core");
            // The same as the Inspector's 'Copy Component' and 'Paste Component As New': every value comes along.
            if (!UnityEditorInternal.ComponentUtility.CopyComponent(old)) continue;
            if (!UnityEditorInternal.ComponentUtility.PasteComponentAsNew(group)) continue;
            Undo.DestroyObjectImmediate(old);
            moved++;
        }
        Finish(moved + " system(s) moved into '" + PixelSystems.RootName + "' with their values. Save the scene to keep the change.");
    }

    /// <summary>Only outside Play mode: anything changed in Play mode is thrown away when it stops.</summary>
    private static bool CanEdit()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
        EditorUtility.DisplayDialog("Pixel Systems", "Stop Play mode first - changes made in Play mode are not saved.", "OK");
        return false;
    }

    /// <summary>The group's child object under 'Pixel Systems' (both made if missing).</summary>
    private static GameObject Group(string name)
    {
        GameObject root = GameObject.Find(PixelSystems.RootName);
        if (root == null)
        {
            root = new GameObject(PixelSystems.RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + PixelSystems.RootName);
        }
        Transform child = root.transform.Find(name);
        if (child == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Undo.SetTransformParent(go.transform, root.transform, "Group " + name);
            child = go.transform;
        }
        return child.gameObject;
    }

    private static void Finish(string message)
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Pixel Systems: " + message);
        EditorUtility.DisplayDialog("Pixel Systems", message, "OK");
    }
}
