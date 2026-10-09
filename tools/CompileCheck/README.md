# Compile check (no Unity needed)

Compiles `Assets/Scripts` with the .NET SDK against Unity's engine DLLs, so compile errors
(typos, missing members, wrong types, mismatched scripts) show up before the project is opened in Unity.
Unity ignores this folder (it is outside `Assets/`).

```
tools/CompileCheck/check.sh                 # both variants
tools/CompileCheck/check.sh Editor          # just one
```

Exit code 0 = no errors. Installs the .NET 8 SDK via apt if it is missing; cloud sessions get it from
`.claude/hooks/session-start.sh`.

## Variants (which `#if` branches get checked)

| Variant | Defines | Checks |
|---|---|---|
| `Editor` | `UNITY_EDITOR`, `ENABLE_LEGACY_INPUT_MANAGER` | Editor-only code (OnValidate, `UnityEditor` calls), legacy Input paths |
| `PlayerInputSystem` | `ENABLE_INPUT_SYSTEM` | Player build, new Input System paths |

## What it does NOT check

- **Unity 6 / 2023 branches** (`UNITY_6000_0_OR_NEWER`, `UNITY_2023_1_OR_NEWER`): the engine DLLs are
  Unity 2021.3 (`UnityEngine.Modules` 2021.3.33 from NuGet; `UnityEditor.dll` from `Unity3D.SDK` 2021.1),
  so the older `#else` branches are checked instead. Engine APIs added after 2021.3 will be reported as
  errors even though they exist in Unity 6 - put them behind a version `#if`.
- **uGUI, EventSystems, TextMeshPro, Input System** are not on NuGet, so `Stubs/` holds hand-written
  stand-ins (real signatures, empty bodies, modelled on Unity 6 / ugui 2.0 TMP). A member missing from a
  stub gives a false error: add it to the stub **only if it really exists in Unity**. A member wrongly
  added to a stub would hide a real error.
- Anything at runtime: behaviour, serialization, scene setup, Inspector values. It is a compiler, not a test run.
