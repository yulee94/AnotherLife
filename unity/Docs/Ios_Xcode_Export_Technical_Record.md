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
| Xcode version | `26.6` (`17F113`) |
| Device SDK | iOS `26.5` |
| Unsigned Xcode Debug build | `BUILD SUCCEEDED` for generic physical iOS device |
| Built application | arm64 Mach-O, unsigned, 97 MiB, 36 files |
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
bb791297fc960bc9658dfcb2a13e3353dc5c2b17e474fa5ad5543af3efe156cc  AnotherLifeUnity.app/AnotherLifeUnity
```

## Warning assessment

The initial non-incremental headless export reported three `-nographics` warnings: Unity could not refresh ambient/reflection probes for Boot, RealmSelection, and Kingdom without a graphics device. They did not indicate missing Xcode output. The final incremental export reused validated build data and completed with zero warnings and zero errors.

The remaining warning is actionable: Kingdom currently contains six `OnMouseDown`/`OnMouseEnter`/`OnMouseExit` handlers across `KingdomVisualizer` and `CityLayoutEngine`. Unity warns that these handlers can affect handheld performance. Replacing them with an intentional touch/pointer input path is a separate gameplay-input change and should be measured on hardware rather than folded into export tooling.

The first full Xcode compile also exposed three Apple-stage follow-ups:

1. The generated app has no required 1024x1024 App Store icon. This is a release blocker that requires approved product artwork; the build foundation does not invent branding.
2. Unity 2022.3.62f3 generated a call to the Game Controller `dpads` API, which Xcode marks as iOS 14 or newer, while the project currently targets iOS 12. The recommended compatibility floor is iOS 14 unless an older-device requirement justifies a Unity upgrade or a reviewed engine-side workaround.
3. Xcode reports that Unity's `GameAssembly` script phase runs every build because it declares no outputs. This affects build iteration time, not Player runtime behavior.

Xcode 26.6 also emitted atomic memory-order warnings from Unity's generated Baselib headers during the first compile. They did not prevent the arm64 framework from linking, but they are a Unity/Xcode compatibility diagnostic to revisit before treating this editor/toolchain pair as a release baseline.

## Environment gates and recommended direction

Unity iOS Build Support and Xcode 26.6 are installed, Xcode is the selected developer directory, and the iOS 26.5 device SDK can compile the generated project. The account-free Debug build succeeded with code signing explicitly disabled and produced an unsigned arm64 application at `unity/Builds/Validation/iOS/DerivedData/Build/Products/Debug-iphoneos/AnotherLifeUnity.app`.

No valid Apple code-signing identity is installed. The downloaded simulator component is also not currently registered with `simctl`. Therefore physical-device installation, simulator launch, signed archive creation, and App Store signing remain unproven.

Recommended sequence:

1. Adopt iOS 14 as the minimum target, or explicitly justify and test the iOS 12 requirement against Unity's generated Game Controller code.
2. Provide approved 1024x1024 App Store icon artwork.
3. Keep physical-device builds as the primary compatibility/performance target; repair simulator runtime registration only if rapid UI-only iteration justifies maintaining both variants.
4. Decide the permanent bundle identifier and Apple Developer Team before enabling signing.
5. Run Boot through RealmSelection on a signed physical-device build, then create a development archive.
6. Profile and replace the Kingdom mouse-event path if hardware measurements show material input or frame-time cost.

## Runtime and compatibility impact

The committed implementation is Editor-only build tooling plus EditMode tests and documentation. It adds no runtime component, package, native library, scene, or asset to the Player. The generated Xcode project, DerivedData, and unsigned application are disposable and ignored. The unsigned Debug application occupies 97 MiB on disk; actual device compatibility and signed/install size remain unmeasured until the Apple-account and hardware gates are completed.
