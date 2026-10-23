# Compile check (no Unity needed)

`tools/compile-check/check.sh` compiles `Assets/Scripts` with a normal C# compiler and prints only errors (exit code 1 if any). Run it before committing.

- Real Unity assemblies: NuGet `UnityEngine.Modules` 2021.3.33, downloaded on first run into `.cache/` (git-ignored). The .NET 8 SDK is installed with apt if missing.
- `Stubs.cs`: hand-written signatures for UnityEngine.UI, EventSystems and TextMeshPro (not on NuGet). If the check reports a missing UI / TMP member that Unity really has, add it to the stubs.
- Catches: wrong argument types / counts, unknown names or members in OUR code, duplicate names, bad overrides, missing usings, misuse of the Unity core API.
- Project rules (`rules.sh`, run by check.sh): text-search rules the compiler can't check, reported like errors. Today: a `Button` is only added inside `PixelUIKit` (`CreateButton` / `MakeClickable`); a script registering with `PixelWindows` builds its window with `PixelUIKit.CreateWindow` (mode-only registrations say `// escape-only: <reason>`, a window built in another file of the class `// window-built-in: <file>`). A system listed in `GameState.MovedSystems` must not implement `IPixelSaved`, register with `PixelSaveRegistry` or use `PixelStatics.ResetOnRestart` (its progress lives in the GameState). Add a new 'one place for this' rule there as one more block.
- Does NOT catch: code inside `#if UNITY_EDITOR` or `UNITY_6000_0_OR_NEWER` / Input System branches, TMP / UI calls the stubs leave out or get wrong, anything that only fails at run time (null references, missing shaders, scene setup).
