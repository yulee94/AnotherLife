# Shader Cache Corruption Runbook

**Status:** Active runbook (not a spec). Use this when UI renders as nothing and the console
spams shader-include errors. A fresh engineer should be able to recognize and recover from this
issue by following this document alone.

**One-line summary:** Opening the project with a Unity Editor version other than the pinned
`2022.3.62f3` (in practice `6000.5.3f1`, installed side-by-side) corrupts `Library/ShaderCache`
and breaks every UI shader. Delete the three generated cache directories and reimport with the
correct version.

---

## 1. Symptom

- Every UI element (`Text`, `Image`, `Button`) renders as nothing, so the **realm-select** and
  **kingdom** screens look empty/broken.
- The Unity console spams errors shaped like this:

  ```
  Shader error in 'UI/Default': Couldn't open include file 'HLSLSupport.cginc'
    at Assets/DefaultResourcesExtra/UI/UI-Default.shader(49)
  Shader error in 'Hidden/BlitCopy': Couldn't open include file 'HLSLSupport.cginc'
  Shader error in 'Hidden/Internal-GUIRoundedRect': Couldn't open include file 'HLSLSupport.cginc'
  ```

- A key tell that this is cache corruption rather than a missing file: `HLSLSupport.cginc` **does**
  exist at `Editor/Data/CGIncludes`. The shader compiler's include resolution is broken, not the
  include file.

## 2. Root cause

The project was opened with Unity `6000.5.3f1` instead of the required `2022.3.62f3`. The
version-mismatched editor wrote a stale, incompatible `Library/ShaderCache` (in the observed case,
`Library/ShaderCache/builtin` was dated 2026-07-15). On a later open with `2022.3.62f3`, that stale
cache poisoned shader include resolution, so built-in UI shaders failed to compile.

`Library/` is a generated directory (gitignored via `/[Ll]ibrary/` in `unity/.gitignore`), so it is
never version-controlled and always safe to delete.

## 3. Recovery (fix)

Close Unity completely, delete the three generated cache directories, then reopen with the correct
version. Unity reimports automatically on the next open.

PowerShell (from anywhere):

```powershell
$unity = "C:\Users\MY\Documents\AnotherLife\unity"
Remove-Item -Recurse -Force "$unity\Library\ShaderCache" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$unity\Library\Bee"        -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$unity\Library\BurstCache" -ErrorAction SilentlyContinue
```

Then in Unity Hub, open `C:\Users\MY\Documents\AnotherLife\unity` with **2022.3.62f3** and let it
reimport.

Heavier fallback (slower full reimport) if the targeted delete does not clear the errors:

```powershell
Remove-Item -Recurse -Force "C:\Users\MY\Documents\AnotherLife\unity\Library"
```

> Do **not** run any of the above while the Unity Editor has the project open; the editor holds and
> rewrites these caches on exit.

## 4. Verification

1. Reopen the project in `2022.3.62f3` and wait for the import to finish.
2. Confirm the console no longer shows `Shader error in 'UI/Default'` /
   `Couldn't open include file 'HLSLSupport.cginc'`.
3. Open the realm-select and kingdom screens and confirm `Text`/`Image`/`Button` elements render.

Recovery is complete only when both (2) and (3) pass.

## 5. Prevention

1. **Only ever open this project with `2022.3.62f3`.** `unity/ProjectSettings/ProjectVersion.txt`
   is already pinned to that version; this only triggers Unity Hub's soft mismatch warning, so do
   not click through it.
2. **In-repo guard:** `unity/Assets/AL/Scripts/Editor/UnityVersionGuard.cs`
   (namespace `AL.EditorTools`) runs at domain reload and fails fast on any editor version other
   than `2022.3.62f3` — it logs a multi-line error and calls `EditorApplication.Exit(1)` so the
   wrong editor cannot finish importing into a version-mismatched cache. It shows a blocking modal
   in interactive mode and is skipped in batch mode (exit still occurs).
   - Escape hatch: set the `AL_ALLOW_ANY_UNITY_VERSION` environment variable to any non-empty value
     other than `0`/`false` for a sanctioned one-off open. The bypass is still logged as a warning.
   - Honest limit: the guard runs at the earliest script hook Unity exposes (during domain reload),
     after the first asset import has already begun — it stops the wrong editor from proceeding,
     but cannot prevent the very first import of a wrong-version open.
3. **Remove or rename the side-by-side Unity 6 install (`6000.5.3f1`)** so Hub cannot open this
   project with it at all.

## 6. Related artifacts

- `unity/ProjectSettings/ProjectVersion.txt` — pinned editor version.
- `unity/Assets/AL/Scripts/Editor/UnityVersionGuard.cs` — fail-fast version guard.
- `unity/Docs/UnityHub_GitHub_Handoff.md` — how to open this project in Unity Hub.
