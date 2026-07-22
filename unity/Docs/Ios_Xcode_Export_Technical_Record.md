# iOS Xcode Export Technical Record

**Profile:** `ShellFoundation`

**Implementation branch:** `codex/ios-player-build-foundation`

**Branch base:** `2d81669`

**Upstream main at implementation:** `3a22b33ecdd768441ae55f87c41632cce2979aff`

**Unity version:** `2022.3.62f3`

**Status date:** 2026-07-22

## Direction and project boundary

The iOS lane uses one shared Unity source project and a dedicated Git worktree/branch. It does not copy the game into a second source tree that can drift. Unity generates a separate, disposable Xcode project for the Apple build stage:

```text
/Users/robert/Developer/AnotherLife-iOS
└── unity/Builds/Validation/iOS/Xcode
```

The worktree is the isolated iOS development lane. The generated Xcode project is ignored build output and is never committed. Changes intended for every platform remain in shared Unity source; Apple-specific export policy stays in Editor-only tooling.

## Export contract

The batch entry is:

```text
AL.EditorTools.ProductionIosBuilder.ExportIosDevelopmentXcodeProject
```

It exports a Unity Development Xcode project for `BuildTarget.iOS` using exactly these descriptor-owned scenes:

1. `Assets/AL/Scenes/Boot.unity`
2. `Assets/AL/Scenes/RealmSelection.unity`
3. `Assets/AL/Scenes/Kingdom.unity`

`Assets/Test.unity` and `Assets/AL/Scenes/ChampionArena.unity` remain excluded. The exporter validates the committed Build Settings, scene structure, exact Unity version, iOS module availability, compilation state, and ignored output boundary before it invokes Unity's build pipeline.

Preflight failures preserve the last successful Xcode project. Machine-readable JSON and a complete text BuildReport are written beside the Xcode directory under `unity/Builds/Validation/iOS/`, so a failed validation cannot overwrite source or erase prior successful evidence.

## Signing-neutral boundary

The exporter records but never changes the bundle identifier, version, minimum iOS version, Apple Team ID, provisioning, or automatic-signing setting. The current inherited values are:

| Setting | Current value |
| --- | --- |
| Bundle identifier | `com.DefaultCompany.AnotherLifeUnity` |
| Bundle version | `1.0` |
| Minimum iOS version | `12.0` |
| Apple Team ID | Not configured |
| Unity automatic signing | Disabled |

The current bundle identifier is a placeholder, not an accepted product identity. Selecting the permanent reverse-domain identifier and Apple Developer Team is an explicit product/account decision because changing them affects App Store identity, provisioning, entitlements, upgrades, and device installation.

## Automated and export evidence

| Validation | Result |
| --- | --- |
| Focused iOS EditMode tests | 6 passed, 0 failed |
| Complete EditMode suite | 311 passed, 0 failed |
| Complete PlayMode suite | 26 passed, 0 failed |
| iOS Unity export | Succeeded |
| Final Unity BuildReport | `Succeeded`, 0 errors, 0 warnings |
| Initial non-incremental diagnostic export | `Succeeded`, 0 errors, 4 reviewed warnings |
| Reported output size | 809,741,503 bytes |
| Generated file count | 2,773 |
| On-disk Xcode directory | 778 MiB |
| Output ignore check | Passed (`unity/.gitignore` ignores `/Builds/`) |

Required output was present:

- `Unity-iPhone.xcodeproj/project.pbxproj`
- `Info.plist`
- `Classes/`
- `Libraries/`
- three serialized scene levels (`Data/level0`, `Data/level1`, and `Data/level2`)

Selected SHA-256 evidence:

```text
eea61d4ec74436d7a004a22d49866271736d4483a184ed7d310b86ee8327823d  Unity-iPhone.xcodeproj/project.pbxproj
7e1038955b237214cbe58fb12b84b969a16b04422559a26f1742685cd3f2a0fd  Info.plist
f6646bd1e5daaab89b3e59c32482bb5c0f1caf37abeca7ee34d3343cbdc03b52  Data/globalgamemanagers
66ca38a8c671c0deb9ad7f77821d1c4a8ecf679bb837e11aa9c8732edf1b13aa  IosDevelopmentExport.summary.json
```

## Warning assessment

The initial non-incremental headless export reported three `-nographics` warnings: Unity could not refresh ambient/reflection probes for Boot, RealmSelection, and Kingdom without a graphics device. They did not indicate missing Xcode output. The final incremental export reused validated build data and completed with zero warnings and zero errors.

The remaining warning is actionable: Kingdom currently contains six `OnMouseDown`/`OnMouseEnter`/`OnMouseExit` handlers across `KingdomVisualizer` and `CityLayoutEngine`. Unity warns that these handlers can affect handheld performance. Replacing them with an intentional touch/pointer input path is a separate gameplay-input change and should be measured on hardware rather than folded into export tooling.

## Environment gates and recommended direction

Unity iOS Build Support is installed, so the Xcode project export is proven. This Mac currently has only Apple Command Line Tools selected, no full Xcode installation, and no valid code-signing identity. Therefore Xcode compilation, simulator/device launch, archive creation, and App Store signing are not yet proven.

Recommended sequence:

1. Install a full Xcode release supported by this Mac and select its developer directory.
2. Keep physical-device builds as the primary compatibility/performance target; add a simulator export only if rapid UI-only iteration justifies maintaining both variants.
3. Decide the permanent bundle identifier and Apple Developer Team before enabling signing.
4. Review whether iOS `12.0` is still the intended minimum deployment target before adding platform APIs or release dependencies.
5. Compile the generated project, run Boot through RealmSelection on a real device, then create a development archive.
6. Profile and replace the Kingdom mouse-event path if hardware measurements show material input or frame-time cost.

## Runtime and compatibility impact

The committed implementation is Editor-only build tooling plus EditMode tests and documentation. It adds no runtime component, package, native library, scene, or asset to the Player. The generated Xcode project is disposable and ignored. Runtime size and performance are therefore unchanged by this commit; actual device compatibility and signed application size remain unmeasured until the Xcode and Apple-account gates are completed.
