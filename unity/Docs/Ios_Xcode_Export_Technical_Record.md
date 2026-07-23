# iOS Xcode Export Technical Record

**Profile:** `ShellFoundation`

**Implementation branch:** `codex/ios-player-build-foundation`

**iOS foundation before upstream integration:** `523c60d`

**Integrated upstream main:** `8237e43`

**Unity version:** `2022.3.62f3`

**Status date:** 2026-07-23

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

## Product identity and signing boundary

The exporter records but never changes the bundle identifier, version, minimum iOS version, Apple Team ID, provisioning, or automatic-signing setting. Preflight now enforces the accepted product identifier and deployment floor before Unity can replace the last successful Xcode output.

| Setting | Current value |
| --- | --- |
| Bundle identifier | `com.yulee94.anotherlife` |
| Bundle version | `1.0` |
| Minimum iOS version | `14.0` |
| Apple Team ID | Not configured |
| Unity automatic signing | Disabled |

`com.yulee94.anotherlife` is the accepted permanent reverse-domain identifier for this product lane. The Apple Team remains deliberately unset because this Mac currently has no installed Apple code-signing identity or discoverable Team ID. The project must use the real Team ID from the enrolled account; inventing or committing a placeholder would create misleading provisioning state. Unsigned validation remains available while automatic signing stays disabled.

## Automated and export evidence

| Validation | Result |
| --- | --- |
| Focused iOS/input EditMode tests | 17 passed, 0 failed |
| Complete EditMode suite on macOS | 532 total: 530 passed, 2 failed in cross-platform save-lock assertions |
| Complete PlayMode suite | 26 passed, 0 failed |
| iOS Unity export | Succeeded |
| Final Unity BuildReport | `Succeeded`, 0 errors, 0 warnings |
| Xcode version | `26.6` (`17F113`) |
| Device SDK | iOS `26.5` |
| Unsigned Xcode Release build | `BUILD SUCCEEDED` for generic physical iOS device |
| Built product identity | `com.yulee94.anotherlife`; minimum iOS `14.0` |
| Selected App Store icon | Mystical-medieval `AL` artwork; 1024x1024 RGB PNG, no alpha; generated iPhone/iPad icon catalog compiled without the former missing-icon warning |
| Built application | arm64 Mach-O, unsigned, 69 MiB on disk, 36 files |
| Reported output size | 814,049,823 bytes |
| Generated file count | 2,776 |
| On-disk Xcode directory | 783 MiB |
| Output ignore check | Passed (`unity/.gitignore` ignores `/Builds/`) |

Required output was present:

- `Unity-iPhone.xcodeproj/project.pbxproj`
- `Info.plist`
- `Classes/`
- `Libraries/`
- three serialized scene levels (`Data/level0`, `Data/level1`, and `Data/level2`)

Selected SHA-256 evidence:

```text
c5ceb169f4a62cb8774e1fd86ac50b3898dc843c6b82f1a62efbb524c38294f0  Unity-iPhone.xcodeproj/project.pbxproj
7e1038955b237214cbe58fb12b84b969a16b04422559a26f1742685cd3f2a0fd  Info.plist
526b93a59fcfa91792ebedea221ca733a26be2d3cfbc305fc6fa7b061d8413fc  Data/globalgamemanagers
6581b3a687224ba2f298e2a84a89e70f924ed5caf5e0507f417d471aaf5a1db1  IosDevelopmentExport.summary.json
99907aa038d5629f21419ad290b550f945d57de54622157474c579ed1711788e  AnotherLifeUnity.app/AnotherLifeUnity
db4fb6b262e537b76b57d855e1c46ce104520d3ef279b51f33717e085e5452ed  Docs/Branding/App_Icon_Mystic_Medieval_AL_Source_1254.png
f53a38f1843c354e3ba464482ae017af99e6d9ca13b64fa83ce5fe6653f08a44  Assets/AL/Art/App_Icon_Mystic_Medieval_AL.png
64e6a31244649ea9bf6c487cb6d2553d02d2a998266ae6c43676b5960cf66c12  AppIcon.appiconset/Icon-Store-1024.png
```

## Warning assessment

The non-incremental headless export reported three `-nographics` warnings: Unity could not refresh ambient/reflection probes for Boot, RealmSelection, and Kingdom without a graphics device. They did not indicate missing Xcode output. The final incremental export reused validated scene data and completed with zero warnings and zero errors.

The six Kingdom `OnMouseDown`/`OnMouseEnter`/`OnMouseExit` handlers were replaced by `IPointerClickHandler`, `IPointerEnterHandler`, and `IPointerExitHandler` contracts. An empty `IDragHandler` contract lets Unity cancel selection after the EventSystem drag threshold while the existing camera controller retains board-pan ownership. The runtime Kingdom camera now owns a `PhysicsRaycaster`, allowing the existing EventSystem to route both touch input on iOS and pointer input in the editor. The former handheld `OnMouse_` warning is absent from the final export. Physical-device validation is still required for gesture feel and selection ergonomics.

The selected 1024x1024 mystical-medieval `AL` icon is assigned to Unity's iOS application and App Store slots. Its visual direction is an enchanted Gothic portal with carved stone, engraved silver-and-gold lettering, deep indigo magic, and a large readable monogram. The user-supplied 1254x1254 original is retained under `Docs/Branding`; the Unity asset is a no-alpha 1024x1024 derivative. Unity generated the required iPhone, iPad, and marketing entries, and Xcode compiled the asset catalog without the former missing App Store icon warning.

The iOS 14 deployment floor resolves the former `dpads` availability mismatch caused by targeting iOS 12. The remaining Apple-stage follow-ups are:

1. Xcode reports that Unity's `GameAssembly` script phase runs every build because it declares no outputs. This affects build iteration time, not Player runtime behavior.
2. Unity 2022.3-generated Apple glue code triggers SDK deprecation warnings under Xcode 26.6, and its Baselib headers emit atomic memory-order warnings. They do not prevent the arm64 app from compiling, but they are Unity/Xcode compatibility diagnostics to revisit before treating this editor/toolchain pair as a release baseline.

## Critical merged-main finding: macOS save-lock semantics

The `8237e43` integration brought the current save candidate inventory and commit-certainty work into the iOS branch. On macOS, 529 of 531 EditMode tests pass. The two failures are both real platform differences:

1. An open bounded read handle does not prevent a path rename on this macOS/Mono runtime.
2. An existing writable shared handle does not make the primary save unreadable; the current loader can return `LoadedPrimary`.

The Windows-focused file-share assumptions therefore do not prove the same contention behavior on macOS or iOS. These assertions must not be skipped or weakened merely to make the suite green.

Recommended direction: keep the iOS packaging branch out of the active save implementation, and resolve this in the save-hardening lane with an explicit cross-platform lock/snapshot protocol and platform-matrix tests. Until that contract lands and is revalidated here, ordinary uncontended persistence is covered, but concurrent-access save integrity on Apple platforms is not release-proven.

## Environment gates and recommended direction

Unity iOS Build Support and Xcode 26.6 are installed, Xcode is the selected developer directory, and the iOS 26.5 device SDK can compile the generated project. The account-free Release build succeeded with code signing explicitly disabled and produced an unsigned arm64 application at `unity/Builds/Validation/iOS/DerivedData/Build/Products/Release-iphoneos/AnotherLifeUnity.app`.

No valid Apple code-signing identity is installed. The downloaded simulator component is also not currently registered with `simctl`. Therefore physical-device installation, simulator launch, signed archive creation, and App Store signing remain unproven.

Recommended sequence:

1. Resolve and revalidate the cross-platform save-lock contract in the save-hardening lane before claiming Apple release persistence.
2. Enroll or connect the Apple Developer account, then set the real Apple Team ID without changing `com.yulee94.anotherlife`.
3. Keep physical-device builds as the primary compatibility/performance target; repair simulator runtime registration only if rapid UI-only iteration justifies maintaining both variants.
4. Run Boot through RealmSelection and exercise Kingdom touch selection on a signed physical-device build.
5. Create and validate a development archive.

## Runtime and compatibility impact

The identity/deployment changes are confined to iOS Player Settings and iOS preflight validation. The selected-icon follow-up adds one 1024x1024 RGB application icon, its retained 1254x1254 original source, and the existing iOS Player Settings references. The Kingdom input change replaces legacy Unity mouse callbacks with existing EventSystem pointer interfaces and adds a `PhysicsRaycaster`; it introduces no package, native library, save mutation, narrative change, or scene-asset edit. The generated Xcode project, DerivedData, and unsigned application are disposable and ignored. The unsigned Release application occupies 69 MiB on disk; actual device compatibility and signed/install size remain unmeasured until the Apple-account and hardware gates are completed.
