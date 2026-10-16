// ---- System notes ----
// Editor/PixelSystemsMenu.cs
//   Editor-only menu for the game's system components (the list is PixelSystems.All):
//   - Pixel Clicker > Add Missing Systems To Scene: every system that is not in the open scene yet is added to a 'Pixel Systems' object,
//     on a child per group (UI, Core, Skills, Rares and Shows, Characters, Camera, World). Then they exist in EDIT mode, so their Inspector
//     values can be changed and saved with the scene (before, most were only added when Play started, and every change was lost when it
//     stopped). Systems already in the scene, wherever they are, are left alone.
//   - Pixel Clicker > Move Systems Off The Clicker: systems sitting on the PixelClicker object are moved into those groups with all their
//     Inspector values (Copy Component + Paste Component As New, then the old one is removed).
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
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            EditorUtility.DisplayDialog("Pixel Systems", "There is no PixelClicker in the open scene.", "OK");
            return;
        }
        int moved = 0;
        foreach (PixelSystems.Entry e in PixelSystems.All)
        {
            if (e.type == null) continue;
            Component old = clicker.GetComponent(e.type);
            if (old == null) continue;
            GameObject group = Group(e.group);
            // The same as the Inspector's 'Copy Component' and 'Paste Component As New': every value comes along.
            if (!UnityEditorInternal.ComponentUtility.CopyComponent(old)) continue;
            if (!UnityEditorInternal.ComponentUtility.PasteComponentAsNew(group)) continue;
            Undo.DestroyObjectImmediate(old);
            moved++;
        }
        Finish(moved == 0
            ? "No systems were sitting on the clicker object."
            : moved + " system(s) moved off the clicker into '" + PixelSystems.RootName + "'. Save the scene to keep the change.");
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
