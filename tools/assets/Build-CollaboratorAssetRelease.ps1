[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$MeshyRoot,
    [Parameter(Mandatory)] [string]$VisualizationRoot,
    [Parameter(Mandatory)] [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sevenZip = (Get-Command 7z -ErrorAction Stop).Source
$repository = 'yulee94/AnotherLife'
$releaseBaseUrl = 'https://github.com/yulee94/AnotherLife/releases/download'
$allowedModelExtensions = @('.blend', '.fbx', '.glb')
$allowedImageExtensions = @('.jpeg', '.jpg', '.png', '.webp')
$coveredSourcePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$globalHashOwners = @{}
$packageSummaries = [System.Collections.Generic.List[object]]::new()
$directAssetSummaries = [System.Collections.Generic.List[object]]::new()
$existingAssetSummaries = [System.Collections.Generic.List[object]]::new()

$categoryTags = [ordered]@{
    'terrestrial-2d' = 'collab-assets-terrestrial-2d-2026-08-17-v001'
    'kingdom-2d-25d' = 'collab-assets-kingdom-2d-25d-2026-08-17-v001'
    'terrestrial-3d' = 'collab-assets-terrestrial-3d-2026-08-17-v001'
    'realm-architecture-3d' = 'collab-assets-realm-architecture-3d-2026-08-17-v001'
    'worldkit-3d' = 'collab-assets-worldkit-3d-2026-08-17-v001'
    'champion-3d' = 'collab-assets-champion-3d-2026-08-17-v001'
    'realm-dragons-3d' = 'collab-assets-realm-dragons-3d-2026-08-17-v001'
    'first-user-planning' = 'collab-assets-first-user-planning-2026-08-17-v001'
    'equipment-3d' = 'collab-assets-equipment-3d-2026-08-17-v001'
}

function Get-NormalizedRelativePath {
    param(
        [Parameter(Mandatory)] [string]$Root,
        [Parameter(Mandatory)] [string]$Path
    )

    return $Path.Substring($Root.TrimEnd('\').Length + 1).Replace('\', '/')
}

function Get-Sha256Lower {
    param([Parameter(Mandatory)] [string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory)] $Value,
        [Parameter(Mandatory)] [string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($Path, $json + "`n", [System.Text.UTF8Encoding]::new($false))
}

function New-AssetPackage {
    param(
        [Parameter(Mandatory)] [string]$Category,
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [object[]]$Roots,
        [Parameter(Mandatory)] [string[]]$Extensions,
        [string]$IncludePattern = '.*',
        [string]$ExcludePattern = '(?!)'
    )

    $categoryRoot = Join-Path $OutputRoot $Category
    $packageRoot = Join-Path $categoryRoot $Name
    $filesRoot = Join-Path $packageRoot 'files'
    New-Item -ItemType Directory -Path $filesRoot -Force | Out-Null

    $entries = [System.Collections.Generic.List[object]]::new()
    $deduplicated = [System.Collections.Generic.List[object]]::new()
    $normalizedExtensions = $Extensions | ForEach-Object { $_.ToLowerInvariant() }

    foreach ($rootSpec in $Roots) {
        $sourceRoot = [string]$rootSpec.Path
        $prefix = [string]$rootSpec.Prefix
        if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
            throw "Missing package source root: $sourceRoot"
        }

        $sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -File -Recurse -Force | Sort-Object FullName
        foreach ($sourceFile in $sourceFiles) {
            $extension = $sourceFile.Extension.ToLowerInvariant()
            if ($extension -notin $normalizedExtensions) {
                continue
            }

            $sourceRelativePath = Get-NormalizedRelativePath -Root $sourceRoot -Path $sourceFile.FullName
            $logicalPath = if ([string]::IsNullOrWhiteSpace($prefix)) {
                $sourceRelativePath
            }
            else {
                "$($prefix.TrimEnd('/'))/$sourceRelativePath"
            }

            if ($logicalPath -notmatch $IncludePattern -or $logicalPath -match $ExcludePattern) {
                continue
            }

            $resolvedSourcePath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
            $coveredSourcePaths.Add($resolvedSourcePath) | Out-Null
            $sha256 = Get-Sha256Lower -Path $resolvedSourcePath
            if ($globalHashOwners.ContainsKey($sha256)) {
                $owner = $globalHashOwners[$sha256]
                $deduplicated.Add([ordered]@{
                    sourcePath = $logicalPath
                    bytes = [long]$sourceFile.Length
                    sha256 = $sha256
                    ownerCategory = $owner.category
                    ownerArtifact = $owner.artifact
                    ownerPath = $owner.path
                })
                continue
            }

            $targetPath = Join-Path $filesRoot $logicalPath.Replace('/', '\')
            $targetDirectory = Split-Path -Parent $targetPath
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
            Copy-Item -LiteralPath $resolvedSourcePath -Destination $targetPath

            $packagedPath = "files/$logicalPath"
            $globalHashOwners[$sha256] = [ordered]@{
                category = $Category
                artifact = "$Name.zip"
                path = $packagedPath
            }
            $entries.Add([ordered]@{
                sourcePath = $logicalPath
                packagedPath = $packagedPath
                bytes = [long]$sourceFile.Length
                sha256 = $sha256
            })
        }
    }

    if ($entries.Count -eq 0 -and $deduplicated.Count -eq 0) {
        throw "Package $Name selected no files."
    }

    $manifestPath = Join-Path $packageRoot 'manifest.json'
    $manifest = [ordered]@{
        schemaVersion = 1
        documentKind = 'anotherlife.collaborator-asset-package.v1'
        category = $Category
        package = $Name
        status = 'NON_PRODUCTION_COLLABORATOR_SOURCE'
        repositoryVisibility = 'PUBLIC'
        entries = $entries
        deduplicatedReferences = $deduplicated
        exclusions = @(
            'Credentials and environment files are never included.',
            'Raw provider request payloads, execution locks, logs, and incomplete downloads are excluded.',
            'A review package does not grant runtime, production, rights, release, or final visual approval.'
        )
    }
    Write-JsonFile -Value $manifest -Path $manifestPath

    $archivePath = Join-Path $categoryRoot "$Name.zip"
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }
    & $sevenZip a -tzip -mx=1 $archivePath (Join-Path $packageRoot '*') | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "7-Zip failed for package $Name with exit code $LASTEXITCODE."
    }

    $archiveFile = Get-Item -LiteralPath $archivePath
    $archiveSha = Get-Sha256Lower -Path $archivePath
    $tag = $categoryTags[$Category]
    $packageSummaries.Add([ordered]@{
        category = $Category
        releaseTag = $tag
        name = $archiveFile.Name
        bytes = [long]$archiveFile.Length
        sha256 = $archiveSha
        downloadUrl = "$releaseBaseUrl/$tag/$($archiveFile.Name)"
        packagedFileCount = $entries.Count
        deduplicatedReferenceCount = $deduplicated.Count
    })

    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

function Add-DirectAsset {
    param(
        [Parameter(Mandatory)] [string]$Category,
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Disposition
    )

    $sourceFile = Get-Item -LiteralPath $Path
    $resolvedSourcePath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
    $coveredSourcePaths.Add($resolvedSourcePath) | Out-Null
    $sha256 = Get-Sha256Lower -Path $resolvedSourcePath
    if ($globalHashOwners.ContainsKey($sha256)) {
        throw "Direct asset duplicates an existing publication owner: $resolvedSourcePath"
    }

    $categoryRoot = Join-Path $OutputRoot $Category
    New-Item -ItemType Directory -Path $categoryRoot -Force | Out-Null
    $targetPath = Join-Path $categoryRoot $sourceFile.Name
    Copy-Item -LiteralPath $resolvedSourcePath -Destination $targetPath
    $tag = $categoryTags[$Category]
    $globalHashOwners[$sha256] = [ordered]@{
        category = $Category
        artifact = $sourceFile.Name
        path = $sourceFile.Name
    }
    $directAssetSummaries.Add([ordered]@{
        category = $Category
        releaseTag = $tag
        name = $sourceFile.Name
        bytes = [long]$sourceFile.Length
        sha256 = $sha256
        disposition = $Disposition
        downloadUrl = "$releaseBaseUrl/$tag/$($sourceFile.Name)"
    })
}

function Add-ExistingReleaseAsset {
    param(
        [Parameter(Mandatory)] [string]$Category,
        [Parameter(Mandatory)] [string]$LocalPath,
        [Parameter(Mandatory)] [string]$ExpectedSha256,
        [Parameter(Mandatory)] [string]$Url,
        [Parameter(Mandatory)] [string]$Disposition
    )

    $sourceFile = Get-Item -LiteralPath $LocalPath
    $resolvedSourcePath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
    $coveredSourcePaths.Add($resolvedSourcePath) | Out-Null
    $actualSha = Get-Sha256Lower -Path $resolvedSourcePath
    if ($actualSha -ne $ExpectedSha256.ToLowerInvariant()) {
        throw "Existing release hash mismatch for $resolvedSourcePath. Expected $ExpectedSha256, got $actualSha."
    }
    if ($globalHashOwners.ContainsKey($actualSha)) {
        throw "Existing release asset duplicates an existing publication owner: $resolvedSourcePath"
    }

    $globalHashOwners[$actualSha] = [ordered]@{
        category = $Category
        artifact = $sourceFile.Name
        path = $Url
    }
    $existingAssetSummaries.Add([ordered]@{
        category = $Category
        name = $sourceFile.Name
        bytes = [long]$sourceFile.Length
        sha256 = $actualSha
        disposition = $Disposition
        existingUrl = $Url
        existingReleaseTag = 'meshy-review-assets-2026-08-12-v001'
    })
}

if (Test-Path -LiteralPath $OutputRoot) {
    throw "Output root already exists; use a new path or remove the exact staging directory deliberately: $OutputRoot"
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null

$v003Root = Join-Path $MeshyRoot 'TERRESTRIAL_IMAGE_REVIEW_QUEUE_20260814_v003'
New-AssetPackage -Category 'terrestrial-2d' -Name 'terrestrial-current-concepts-v003' -Roots @(
    @{ Path = $v003Root; Prefix = 'v003' }
) -Extensions @('.json', '.md', '.png') -IncludePattern '^v003/(a2_3d_fidelity_dispositions|auto_go_dispositions|historical_draft_reassessment|images/(2d_absent_completions|fantasy_enhanced_variants)|source_contracts/(?!world_settlement_planning)|gallery/contact_sheets/correction_lane_status_contact_sheet_v001\.png)' -ExcludePattern '(provider_payloads|provider_execution|\.lock\.json$)'

New-AssetPackage -Category 'terrestrial-2d' -Name 'terrestrial-corrections-and-rejections-v003' -Roots @(
    @{ Path = $v003Root; Prefix = 'v003' }
) -Extensions @('.png') -IncludePattern '^v003/images/(provider_inputs|rejected_generation_attempts|rejected_model_corrections)/' -ExcludePattern '(?!)'

New-AssetPackage -Category 'terrestrial-2d' -Name 'terrestrial-historical-review-queues-v001-v002' -Roots @(
    @{ Path = (Join-Path $MeshyRoot 'TERRESTRIAL_IMAGE_REVIEW_QUEUE_20260814_v001'); Prefix = 'v001' },
    @{ Path = (Join-Path $MeshyRoot 'TERRESTRIAL_IMAGE_REVIEW_QUEUE_20260814_v002'); Prefix = 'v002' },
    @{ Path = (Join-Path $MeshyRoot 'terrestrial_review_queue_tools'); Prefix = 'tools' },
    @{ Path = (Join-Path $MeshyRoot 'source_inputs'); Prefix = 'source_inputs' },
    @{ Path = (Join-Path $MeshyRoot '20260812_014823_veilspine-widow-umbral-elite-s_019ff1b9'); Prefix = 'veilspine-source-only-history' }
) -Extensions @('.css', '.html', '.js', '.json', '.md', '.png') -ExcludePattern '(provider_payloads|provider_execution|\.lock\.json$)'

New-AssetPackage -Category 'kingdom-2d-25d' -Name 'kingdom-settlement-and-role-planning-v003' -Roots @(
    @{ Path = $v003Root; Prefix = 'v003' }
) -Extensions @('.json', '.png') -IncludePattern '^v003/(gallery/contact_sheets/world_settlement[^/]*|images/world_settlement_planning|source_contracts/world_settlement_planning)/?' -ExcludePattern '(?!)'

$terrestrialSourceDirectories = [ordered]@{
    'hollowbark-stalker-source' = '20260812_012123_hollowbark-stalker-eldergrove_019ff1a0'
    'rimehorn-breaker-source' = '20260812_013056_rimehorn-breaker-stonehold-eli_019ff1a9'
    'reliquary-basilisk-source' = '20260812_014011_reliquary-basilisk-crownlands_019ff1b2'
    'cindermaw-salamander-source' = '20260812_015128_cindermaw-salamander-umbral-el_019ff1bc'
}
foreach ($entry in $terrestrialSourceDirectories.GetEnumerator()) {
    New-AssetPackage -Category 'terrestrial-3d' -Name $entry.Key -Roots @(
        @{ Path = (Join-Path $MeshyRoot $entry.Value); Prefix = $entry.Key }
    ) -Extensions ($allowedModelExtensions + $allowedImageExtensions) -ExcludePattern '(provider_payloads|provider_execution|\.incomplete$)'
}

$terrestrialEvidenceDirectories = @(
    '20260812_140926_meridian-tempest-roc_019ff460',
    '20260812_140928_ashvein-triarch_019ff460',
    '20260812_140929_mirrorfin-lurker_019ff460',
    '20260812_140931_mere-root-leviathan_019ff460',
    '20260812_140932_oreblind-delver_019ff460',
    '20260812_140935_crownstep-lion_019ff460',
    '20260812_140936_sunmane-thornstag_019ff460',
    '20260812_140937_veilspine-widow_019ff460',
    '20260812_140938_slaghide-gorer_019ff460',
    '20260812_140939_galeclaw-courser_019ff460',
    '20260812_140940_gravewing-siphon_019ff460',
    '20260814_170523_broadcrest-aurochs-chalklight_019fff4d'
)
$terrestrialEvidenceRoots = foreach ($directory in $terrestrialEvidenceDirectories) {
    @{ Path = (Join-Path $MeshyRoot $directory); Prefix = $directory }
}
New-AssetPackage -Category 'terrestrial-3d' -Name 'terrestrial-review-model-evidence' -Roots $terrestrialEvidenceRoots -Extensions $allowedImageExtensions -ExcludePattern '(?!)'

$championDirectories = [ordered]@{
    'crownlands-champion-variant-01' = '20260811_173910_crownlands-champion-variant-01_019feff9'
    'crownlands-champion-tpose-variants' = '20260811_191653_crownlands-champion-tpose-vari_019ff053'
}
foreach ($entry in $championDirectories.GetEnumerator()) {
    New-AssetPackage -Category 'champion-3d' -Name $entry.Key -Roots @(
        @{ Path = (Join-Path $MeshyRoot $entry.Value); Prefix = $entry.Key }
    ) -Extensions ($allowedModelExtensions + $allowedImageExtensions) -ExcludePattern '(provider_payloads|provider_execution|\.incomplete$)'
}

$dragonDirectories = [ordered]@{
    'crownlands-aurelius-dragon' = '20260812_002720_aurelius-crownlands-dragon_019ff16f'
    'stonehold-ferrum-dragon' = '20260812_004643_ferrum-stonehold-dragon-source_019ff181'
    'eldergrove-virens-dragon' = '20260812_010102_virens-eldergrove-dragon-sourc_019ff18e'
    'umbral-nox-source-images' = '20260812_010641_nox-umbral-void-dragon-source_019ff193'
    'umbral-nox-corrected-dragon' = '20260812_011702_nox-umbral-void-dragon-correct_019ff19c'
    'wish-vaeloryn-dragon' = '20260812_010849_vaeloryn-celestial-wish-dragon_019ff195'
    'four-realm-dragon-review-scene' = 'four_realm_dragons_non_film_review_v001'
}
foreach ($entry in $dragonDirectories.GetEnumerator()) {
    New-AssetPackage -Category 'realm-dragons-3d' -Name $entry.Key -Roots @(
        @{ Path = (Join-Path $MeshyRoot $entry.Value); Prefix = $entry.Key }
    ) -Extensions ($allowedModelExtensions + $allowedImageExtensions) -ExcludePattern '(provider_payloads|provider_execution|\.incomplete$)'
}

New-AssetPackage -Category 'worldkit-3d' -Name 'common-worldkit-prototypes-and-textures' -Roots @(
    @{ Path = (Join-Path $MeshyRoot 'common_worldkit_20260813_v001'); Prefix = 'common-worldkit' }
) -Extensions ($allowedModelExtensions + $allowedImageExtensions) -ExcludePattern '(provider_payloads|provider_execution|\.incomplete$)'

$firstUserRoots = @(
    @{ Path = (Join-Path $VisualizationRoot 'first-user-experience-replacement-v003'); Prefix = 'first-user-experience-replacement-v003' },
    @{ Path = (Join-Path $VisualizationRoot 'first-user-experience-system-v002'); Prefix = 'first-user-experience-system-v002' },
    @{ Path = (Join-Path $VisualizationRoot 'loading-to-realm-motion-study-v002'); Prefix = 'loading-to-realm-motion-study-v002' },
    @{ Path = (Join-Path $VisualizationRoot 'motion-storyboard-v001'); Prefix = 'motion-storyboard-v001' },
    @{ Path = (Join-Path $VisualizationRoot 'prototype_v001'); Prefix = 'prototype_v001' }
)
New-AssetPackage -Category 'first-user-planning' -Name 'first-user-ux-motion-and-wireframe-planning' -Roots $firstUserRoots -Extensions @('.cjs', '.css', '.html', '.js', '.json', '.md', '.mp4', '.png', '.sha256', '.yaml', '.yml') -ExcludePattern '(^|/)(node_modules|browser|vendor)/'

$existingReleaseBase = 'https://github.com/yulee94/AnotherLife/releases/download/meshy-review-assets-2026-08-12-v001'
$existingReleaseAssets = @(
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140928_ashvein-triarch_019ff460'; File = 'ashvein_triarch_review_master.glb'; Sha = 'e165b8e9728c041f9dfdb03857483c7995d0cc071b38ce56fe6b948575a46bcc'; Disposition = 'REJECTED_REPLACEMENT_REQUIRED' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140935_crownstep-lion_019ff460'; File = 'crownstep_lion_review_master.glb'; Sha = '2d70d9060666d0b7c45930e0e0fd79f369e5ca13b0ab76eeb9698383e488c7b9'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140939_galeclaw-courser_019ff460'; File = 'galeclaw_courser_review_master.glb'; Sha = '76d7cd81eaba72a95609555d96a94f0c97ee6e55e1686340752e119e547e7b05'; Disposition = 'CHANGES_REQUIRED' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140940_gravewing-siphon_019ff460'; File = 'gravewing_siphon_review_master.glb'; Sha = 'acd9872c0b376921a15803662ac41ae71c800e4cb91ef053ad40b301196c3aa6'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140926_meridian-tempest-roc_019ff460'; File = 'meridian_tempest_roc_review_master.glb'; Sha = 'd8bbd40ab8144bcefd851a5be88154bdc29448ffc63ecdd9cf043e3468c8f72d'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140932_oreblind-delver_019ff460'; File = 'oreblind_delver_review_master.glb'; Sha = 'd4c616f4006ea31e0f4ba69002458cbc55741ada12f5069ef144ea0aa36995b8'; Disposition = 'REJECTED_REPLACEMENT_REQUIRED' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140938_slaghide-gorer_019ff460'; File = 'slaghide_gorer_review_master.glb'; Sha = 'bf684e7755521522ab396537a655a31b5a1ecb07cc6b543137ee1c06bfb6438e'; Disposition = 'CHANGES_REQUIRED' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140936_sunmane-thornstag_019ff460'; File = 'sunmane_thornstag_review_master.glb'; Sha = 'e558685e6967099041cc3e212e11fcc108f38693c328d75b75b9e04396f18d13'; Disposition = 'REJECTED_REPLACEMENT_REQUIRED' },
    @{ Category = 'terrestrial-3d'; Directory = '20260812_140937_veilspine-widow_019ff460'; File = 'veilspine_widow_review_master.glb'; Sha = '156627839b14139784be0be0130811c191924f044855588172f061e7ec91f54d'; Disposition = 'REJECTED_REPLACEMENT_REQUIRED' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140941_offhand-dagger_019ff460'; File = 'offhand_dagger_review_master.glb'; Sha = 'a8b0a333b1895107e747a0ef873447aa89b9f613880ebe732fd2d091a5ae9cf0'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140941_offhand-orb_019ff460'; File = 'offhand_orb_review_master.glb'; Sha = '6f320d95f74e97227b144bf2b8ae026906e601cf5866ce81d73da40dddbd734f'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140941_offhand-shield_019ff460'; File = 'offhand_shield_review_master.glb'; Sha = '0b721351ae43ea736f5dcbf07e89eb50b47569338b018dc14efa337abc8cfbb1'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140942_offhand-tome_019ff460'; File = 'offhand_tome_review_master.glb'; Sha = '932731f17c538e869b0d9ce315c58a63f721d094848eada2d6a9a127df043457'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140930_weapon-axe_019ff460'; File = 'weapon_axe_review_master.glb'; Sha = 'e63d6ce229cf73311cbb9235a5e787964b31ed8a4829dc51afe750907279cd82'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140940_weapon-bow_019ff460'; File = 'weapon_bow_review_master.glb'; Sha = '46e97465c4795a14d5cbd95c5f4f4c3e620ab6f7c79584b836fad349aab84362'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140941_weapon-hammer_019ff460'; File = 'weapon_hammer_review_master.glb'; Sha = '3559ddcff520961a0ea869792c4fbdfe055b495195127b140d844760c3d852a5'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140933_weapon-staff_019ff460'; File = 'weapon_staff_review_master.glb'; Sha = 'f1ef6f8441a7f7360c2eb89d6bf2b2d1b9e9eeb196de63f25670f7cd0389bffb'; Disposition = 'PROVISIONAL_REVIEW_ASSET' },
    @{ Category = 'equipment-3d'; Directory = '20260812_140924_weapon-sword_019ff460'; File = 'weapon_sword_review_master.glb'; Sha = 'd225301d04b08eede6086034f445d6b42117d5315c6fde5c263b775d888f189a'; Disposition = 'PROVISIONAL_REVIEW_ASSET' }
)
foreach ($asset in $existingReleaseAssets) {
    Add-ExistingReleaseAsset -Category $asset.Category -LocalPath (Join-Path (Join-Path $MeshyRoot $asset.Directory) $asset.File) -ExpectedSha256 $asset.Sha -Url "$existingReleaseBase/$($asset.File)" -Disposition $asset.Disposition
}

$newTerrestrialModels = @(
    @{ Directory = '20260812_140929_mirrorfin-lurker_019ff460'; File = 'mirrorfin_lurker_review_master.glb'; Disposition = 'INCOMPLETE_REVIEW_ASSET' },
    @{ Directory = '20260812_140931_mere-root-leviathan_019ff460'; File = 'mere_root_leviathan_review_master.glb'; Disposition = 'REJECTED_ANATOMY_TOPOLOGY' },
    @{ Directory = '20260814_170523_broadcrest-aurochs-chalklight_019fff4d'; File = 'broadcrest_aurochs_chalklight_review_master.glb'; Disposition = 'PROVISIONAL_PASS_REVIEW_PENDING' }
)
foreach ($asset in $newTerrestrialModels) {
    Add-DirectAsset -Category 'terrestrial-3d' -Path (Join-Path (Join-Path $MeshyRoot $asset.Directory) $asset.File) -Disposition $asset.Disposition
}

$gateModels = @(
    @{ Directory = '20260812_140923_gate-eldergrove_019ff460'; File = 'gate_eldergrove_review_master.glb' },
    @{ Directory = '20260812_140923_gate-stonehold_019ff460'; File = 'gate_stonehold_review_master.glb' },
    @{ Directory = '20260812_140924_gate-crownlands_019ff460'; File = 'gate_crownlands_review_master.glb' },
    @{ Directory = '20260812_140924_gate-umbral_019ff460'; File = 'gate_umbral_review_master.glb' }
)
foreach ($asset in $gateModels) {
    Add-DirectAsset -Category 'realm-architecture-3d' -Path (Join-Path (Join-Path $MeshyRoot $asset.Directory) $asset.File) -Disposition 'RETAINED_REVIEW_PENDING'
}

$gateEvidenceRoots = foreach ($asset in $gateModels) {
    @{ Path = (Join-Path $MeshyRoot $asset.Directory); Prefix = $asset.Directory }
}
New-AssetPackage -Category 'realm-architecture-3d' -Name 'realm-gate-review-evidence' -Roots $gateEvidenceRoots -Extensions $allowedImageExtensions -ExcludePattern '(?!)'

Add-DirectAsset -Category 'realm-dragons-3d' -Path (Join-Path (Join-Path $MeshyRoot '20260812_140927_wish-dragon_019ff460') 'wish_dragon_review_master.glb') -Disposition 'REJECTED_OR_REPLACEMENT_REVIEW_HISTORY'

$allMeshyAssetFiles = Get-ChildItem -LiteralPath $MeshyRoot -File -Recurse -Force | Where-Object {
    $_.Extension.ToLowerInvariant() -in ($allowedModelExtensions + $allowedImageExtensions)
}
$uncoveredMeshyAssets = foreach ($file in $allMeshyAssetFiles) {
    $resolved = [System.IO.Path]::GetFullPath($file.FullName)
    if (-not $coveredSourcePaths.Contains($resolved)) {
        [ordered]@{
            path = Get-NormalizedRelativePath -Root $MeshyRoot -Path $resolved
            bytes = [long]$file.Length
            extension = $file.Extension.ToLowerInvariant()
            sha256 = Get-Sha256Lower -Path $resolved
        }
    }
}

$uncoveredModelFiles = @($uncoveredMeshyAssets | Where-Object { $_.extension -in $allowedModelExtensions })
if ($uncoveredModelFiles.Count -ne 0) {
    $uncoveredModelPath = Join-Path $OutputRoot 'UNEXPECTED_UNCOVERED_MODELS.json'
    Write-JsonFile -Value $uncoveredModelFiles -Path $uncoveredModelPath
    throw "Model coverage failed: $($uncoveredModelFiles.Count) model files are not categorized. See $uncoveredModelPath"
}
$uncoveredImageFiles = @($uncoveredMeshyAssets | Where-Object { $_.extension -in $allowedImageExtensions })
if ($uncoveredImageFiles.Count -ne 0) {
    $uncoveredImagePath = Join-Path $OutputRoot 'UNEXPECTED_UNCOVERED_IMAGES.json'
    Write-JsonFile -Value $uncoveredImageFiles -Path $uncoveredImagePath
    throw "Image coverage failed: $($uncoveredImageFiles.Count) image files are not categorized. See $uncoveredImagePath"
}

foreach ($category in $categoryTags.Keys) {
    $categoryRoot = Join-Path $OutputRoot $category
    New-Item -ItemType Directory -Path $categoryRoot -Force | Out-Null
    $categoryManifestName = "$category.manifest.json"
    $categoryManifestPath = Join-Path $categoryRoot $categoryManifestName
    $categoryManifest = [ordered]@{
        schemaVersion = 1
        documentKind = 'anotherlife.collaborator-asset-release.v1'
        category = $category
        releaseTag = $categoryTags[$category]
        status = 'NON_PRODUCTION_COLLABORATOR_SOURCE'
        repository = $repository
        repositoryVisibility = 'PUBLIC'
        packageArtifacts = @($packageSummaries | Where-Object category -eq $category)
        directArtifacts = @($directAssetSummaries | Where-Object category -eq $category)
        existingArtifacts = @($existingAssetSummaries | Where-Object category -eq $category)
        limitations = @(
            'Review, rejected, provisional, and planning assets retain their individual dispositions.',
            'No package grants production, runtime, release, rights, balance, or final visual approval.',
            'No credentials, environment files, raw provider payloads, execution locks, logs, or incomplete downloads are included.'
        )
    }
    Write-JsonFile -Value $categoryManifest -Path $categoryManifestPath
}

$coverageAudit = [ordered]@{
    schemaVersion = 1
    documentKind = 'anotherlife.collaborator-asset-coverage-audit.v1'
    meshyRoot = $MeshyRoot
    totalMeshyModelFiles = @($allMeshyAssetFiles | Where-Object { $_.Extension.ToLowerInvariant() -in $allowedModelExtensions }).Count
    totalMeshyImageFiles = @($allMeshyAssetFiles | Where-Object { $_.Extension.ToLowerInvariant() -in $allowedImageExtensions }).Count
    coveredSourcePathCount = $coveredSourcePaths.Count
    uniquePublishedHashCount = $globalHashOwners.Count
    uncoveredModelFileCount = $uncoveredModelFiles.Count
    uncoveredImageFiles = $uncoveredImageFiles
    packages = $packageSummaries
    directAssets = $directAssetSummaries
    existingAssets = $existingAssetSummaries
}
Write-JsonFile -Value $coverageAudit -Path (Join-Path $OutputRoot 'collaborator_asset_coverage_audit_v001.json')

Write-Output "Built categorized collaborator asset staging at $OutputRoot"
Write-Output "Packages: $($packageSummaries.Count)"
Write-Output "Direct assets: $($directAssetSummaries.Count)"
Write-Output "Existing referenced assets: $($existingAssetSummaries.Count)"
Write-Output "Covered model files: $($coverageAudit.totalMeshyModelFiles)"
Write-Output "Uncovered image files retained in audit: $(@($coverageAudit.uncoveredImageFiles).Count)"
