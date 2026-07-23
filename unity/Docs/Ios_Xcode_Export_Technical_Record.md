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
| Complete EditMode suite on macOS | 531 total: 529 passed, 2 failed in cross-platform save-lock assertions |
| Complete PlayMode suite | 26 passed, 0 failed |
| iOS Unity export | Succeeded |
| Final Unity BuildReport after merging `8237e43` | `Succeeded`, 0 errors, 4 reviewed warnings |
| Xcode version | `26.6` (`17F113`) |
| Device SDK | iOS `26.5` |
| Unsigned Xcode Debug build | `BUILD SUCCEEDED` for generic physical iOS device |
| Selected App Store icon | Mystical-medieval `AL` artwork; 1024x1024 RGB PNG, no alpha; generated iPhone/iPad icon catalog compiled without the former missing-icon warning |
| Built application | arm64 Mach-O, unsigned, 100 MiB, 36 files |
| Reported output size | 814,044,258 bytes |
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
667c8f281aab2b6cb5d47d9ecd2fc33b1591fa2aa61516b7d640fd23d3676cdc  Unity-iPhone.xcodeproj/project.pbxproj
7e1038955b237214cbe58fb12b84b969a16b04422559a26f1742685cd3f2a0fd  Info.plist
d52819cf2c5d20686b2869816eeef38100172361afac1cf9e62f8983fdc60357  Data/globalgamemanagers
f5cad99c8b374a56b1bac59b15e0176fb6ad4ff40d56c85d48ce6c35b7355828  IosDevelopmentExport.summary.json
bb791297fc960bc9658dfcb2a13e3353dc5c2b17e474fa5ad5543af3efe156cc  AnotherLifeUnity.app/AnotherLifeUnity
db4fb6b262e537b76b57d855e1c46ce104520d3ef279b51f33717e085e5452ed  Docs/Branding/App_Icon_Mystic_Medieval_AL_Source_1254.png
f53a38f1843c354e3ba464482ae017af99e6d9ca13b64fa83ce5fe6653f08a44  Assets/AL/Art/App_Icon_Mystic_Medieval_AL.png
64e6a31244649ea9bf6c487cb6d2553d02d2a998266ae6c43676b5960cf66c12  AppIcon.appiconset/Icon-Store-1024.png
```

## Warning assessment

The initial non-incremental headless export reported three `-nographics` warnings: Unity could not refresh ambient/reflection probes for Boot, RealmSelection, and Kingdom without a graphics device. They did not indicate missing Xcode output. The final incremental export reused validated build data and completed with zero warnings and zero errors.

The post-merge export rebuilt scripts and therefore reported the same three reviewed `-nographics` warnings plus the handheld `OnMouse_` warning. It completed with zero errors, and the unsigned Xcode compile succeeded.

The remaining warning is actionable: Kingdom currently contains six `OnMouseDown`/`OnMouseEnter`/`OnMouseExit` handlers across `KingdomVisualizer` and `CityLayoutEngine`. Unity warns that these handlers can affect handheld performance. Replacing them with an intentional touch/pointer input path is a separate gameplay-input change and should be measured on hardware rather than folded into export tooling.

The selected 1024x1024 mystical-medieval `AL` icon is assigned to Unity's iOS application and App Store slots. Its visual direction is an enchanted Gothic portal with carved stone, engraved silver-and-gold lettering, deep indigo magic, and a large readable monogram. The user-supplied 1254x1254 original is retained under `Docs/Branding`; the Unity asset is a no-alpha 1024x1024 derivative. Unity generated the required iPhone, iPad, and marketing entries, and Xcode compiled the asset catalog without the former missing App Store icon warning.

The open Apple-stage follow-ups are:

1. Unity 2022.3.62f3 generated a call to the Game Controller `dpads` API, which Xcode marks as iOS 14 or newer, while the project currently targets iOS 12. The recommended compatibility floor is iOS 14 unless an older-device requirement justifies a Unity upgrade or a reviewed engine-side workaround.
2. Xcode reports that Unity's `GameAssembly` script phase runs every build because it declares no outputs. This affects build iteration time, not Player runtime behavior.

Xcode 26.6 also emitted atomic memory-order warnings from Unity's generated Baselib headers during the first compile. They did not prevent the arm64 framework from linking, but they are a Unity/Xcode compatibility diagnostic to revisit before treating this editor/toolchain pair as a release baseline.

## Critical merged-main finding: macOS save-lock semantics

The `8237e43` integration brought the current save candidate inventory and commit-certainty work into the iOS branch. On macOS, 529 of 531 EditMode tests pass. The two failures are both real platform differences:

1. An open bounded read handle does not prevent a path rename on this macOS/Mono runtime.
2. An existing writable shared handle does not make the primary save unreadable; the current loader can return `LoadedPrimary`.

The Windows-focused file-share assumptions therefore do not prove the same contention behavior on macOS or iOS. These assertions must not be skipped or weakened merely to make the suite green.

Recommended direction: keep the iOS packaging branch out of the active save implementation, and resolve this in the save-hardening lane with an explicit cross-platform lock/snapshot protocol and platform-matrix tests. Until that contract lands and is revalidated here, ordinary uncontended persistence is covered, but concurrent-access save integrity on Apple platforms is not release-proven.

## Environment gates and recommended direction

Unity iOS Build Support and Xcode 26.6 are installed, Xcode is the selected developer directory, and the iOS 26.5 device SDK can compile the generated project. The account-free Debug build succeeded with code signing explicitly disabled and produced an unsigned arm64 application at `unity/Builds/Validation/iOS/DerivedData/Build/Products/Debug-iphoneos/AnotherLifeUnity.app`.

No valid Apple code-signing identity is installed. The downloaded simulator component is also not currently registered with `simctl`. Therefore physical-device installation, simulator launch, signed archive creation, and App Store signing remain unproven.

Recommended sequence:

1. Resolve and revalidate the cross-platform save-lock contract in the save-hardening lane before claiming Apple release persistence.
2. Adopt iOS 14 as the minimum target, or explicitly justify and test the iOS 12 requirement against Unity's generated Game Controller code.
3. Decide the permanent bundle identifier and Apple Developer Team before enabling signing.
4. Keep physical-device builds as the primary compatibility/performance target; repair simulator runtime registration only if rapid UI-only iteration justifies maintaining both variants.
5. Run Boot through RealmSelection on a signed physical-device build, then create a development archive.
6. Profile and replace the Kingdom mouse-event path if hardware measurements show material input or frame-time cost.

## Runtime and compatibility impact

The committed build foundation remains Editor-only tooling plus EditMode tests and documentation. The selected-icon follow-up adds one 1024x1024 RGB application icon, its retained 1254x1254 original source, and the existing iOS Player Settings references; it adds no runtime component, package, native library, scene, or behavior. The generated Xcode project, DerivedData, and unsigned application are disposable and ignored. The unsigned Debug application occupies 100 MiB on disk; actual device compatibility and signed/install size remain unmeasured until the Apple-account and hardware gates are completed.
