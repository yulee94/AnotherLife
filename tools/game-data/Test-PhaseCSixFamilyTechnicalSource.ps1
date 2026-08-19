param(
    [switch]$RequireProductionEligible,
    [string]$CandidatePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$resolvedTechnicalSourcePath = if ([string]::IsNullOrWhiteSpace($CandidatePath)) {
    Join-Path $repoRoot "unity\Docs\GameDataCatalog\PhaseC\phase-c-six-family-technical-source.json"
}
else {
    (Resolve-Path -LiteralPath $CandidatePath).Path
}
$forbiddenProductionPaths = @(
    "unity\Assets\Resources\GameData\catalog-set.json",
    "unity\Docs\GameDataCatalog\PhaseC\Generated\catalog-set.json"
)
foreach ($root in @("unity\Assets\StreamingAssets\GameData", "unity\Assets\Resources\GameData")) {
    foreach ($family in @("realms", "buildings", "research", "troops", "champions", "skills")) {
        $forbiddenProductionPaths += "$root\Catalogs\$family.v1.json"
    }
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Phase C six-family technical-source validation failed: $Message"
    }
}

function Assert-Integer {
    param(
        [object]$Value,
        [string]$JsonPath
    )

    Assert-True (($Value -is [long]) -or ($Value -is [int])) "$JsonPath must be an integer"
}

function Assert-String {
    param(
        [object]$Value,
        [string]$JsonPath
    )

    Assert-True ($Value -is [string]) "$JsonPath must be a string"
}

function Assert-Number {
    param(
        [object]$Value,
        [string]$JsonPath
    )

    $isNumber =
        ($Value -is [byte]) -or
        ($Value -is [sbyte]) -or
        ($Value -is [short]) -or
        ($Value -is [ushort]) -or
        ($Value -is [int]) -or
        ($Value -is [uint]) -or
        ($Value -is [long]) -or
        ($Value -is [ulong]) -or
        ($Value -is [single]) -or
        ($Value -is [double]) -or
        ($Value -is [decimal])
    Assert-True $isNumber "$JsonPath must be a JSON number"
    $number = [double]$Value
    Assert-True (-not [double]::IsNaN($number) -and -not [double]::IsInfinity($number)) "$JsonPath must be finite"
}

function Assert-Array {
    param(
        [object]$Value,
        [string]$JsonPath
    )

    Assert-True ($Value -is [System.Array]) "$JsonPath must be an array"
}

function Assert-ExactProperties {
    param(
        [object]$Value,
        [string[]]$ExpectedProperties,
        [string]$JsonPath
    )

    Assert-True ($null -ne $Value) "$JsonPath must be an object"
    $actualProperties = @($Value.PSObject.Properties | ForEach-Object { $_.Name })
    Assert-True ($actualProperties.Count -eq $ExpectedProperties.Count) "$JsonPath expected properties [$($ExpectedProperties -join ', ')], found [$($actualProperties -join ', ')]"
    foreach ($actualProperty in $actualProperties) {
        Assert-True ($ExpectedProperties -ccontains $actualProperty) "$JsonPath contains unknown property '$actualProperty'"
    }
    foreach ($expectedProperty in $ExpectedProperties) {
        Assert-True ($actualProperties -ccontains $expectedProperty) "$JsonPath is missing property '$expectedProperty'"
    }
}

function Assert-NoDuplicateJsonProperties {
    param(
        [System.Text.Json.JsonElement]$Element,
        [string]$JsonPath
    )

    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            Assert-True ($names.Add($property.Name)) "$JsonPath contains duplicate property '$($property.Name)'"
            Assert-NoDuplicateJsonProperties $property.Value "$JsonPath.$($property.Name)"
        }
        return
    }

    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        $index = 0
        foreach ($item in $Element.EnumerateArray()) {
            Assert-NoDuplicateJsonProperties $item "$JsonPath[$index]"
            $index++
        }
    }
}

function Read-StrictJson {
    param(
        [string]$Path,
        [string]$Label
    )

    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "$Label is missing: $Path"
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    Assert-True ($bytes.Length -gt 0) "$Label is empty"
    Assert-True (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) "$Label must be UTF-8 without a BOM"
    Assert-True ($bytes[$bytes.Length - 1] -eq 0x0A) "$Label must end with one LF byte"

    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $text = $utf8.GetString($bytes)
    $options = [System.Text.Json.JsonDocumentOptions]::new()
    $options.AllowTrailingCommas = $false
    $options.CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
    $options.MaxDepth = 32
    try {
        $document = [System.Text.Json.JsonDocument]::Parse($text, $options)
        try {
            Assert-NoDuplicateJsonProperties $document.RootElement '$'
        }
        finally {
            $document.Dispose()
        }
    }
    catch {
        throw "Phase C six-family technical-source validation failed: $Label strict JSON validation failed: $($_.Exception.Message)"
    }

    try {
        return $text | ConvertFrom-Json
    }
    catch {
        throw "Phase C six-family technical-source validation failed: $Label JSON conversion failed: $($_.Exception.Message)"
    }
}

function Assert-ExactArray {
    param(
        [object]$Actual,
        [object[]]$Expected,
        [string]$JsonPath
    )

    Assert-Array $Actual $JsonPath
    $actualItems = @($Actual)
    $expectedItems = @($Expected)
    Assert-True ($actualItems.Count -eq $expectedItems.Count) "$JsonPath expected $($expectedItems.Count) items, found $($actualItems.Count)"
    for ($index = 0; $index -lt $expectedItems.Count; $index++) {
        if ($expectedItems[$index] -is [string]) {
            Assert-String $actualItems[$index] "$JsonPath[$index]"
        }
        Assert-True ($actualItems[$index] -ceq $expectedItems[$index]) "$JsonPath[$index] expected '$($expectedItems[$index])', found '$($actualItems[$index])'"
    }
}

function Assert-ExactAlias {
    param(
        [object]$Aliases,
        [string]$LegacyId,
        [string]$CanonicalId,
        [string]$JsonPath
    )

    Assert-Array $Aliases $JsonPath
    $rows = @($Aliases)
    Assert-True ($rows.Count -eq 1) "$JsonPath must contain exactly one explicit alias"
    $alias = $rows[0]
    Assert-ExactProperties $alias @(
        "legacyId",
        "canonicalId",
        "introducedVersion",
        "retirementVersion",
        "migrationIssue"
    ) "$JsonPath[0]"
    Assert-String $alias.legacyId "$JsonPath[0].legacyId"
    Assert-String $alias.canonicalId "$JsonPath[0].canonicalId"
    Assert-String $alias.migrationIssue "$JsonPath[0].migrationIssue"
    Assert-True ($alias.legacyId -ceq $LegacyId) "$JsonPath[0].legacyId expected '$LegacyId'"
    Assert-True ($alias.canonicalId -ceq $CanonicalId) "$JsonPath[0].canonicalId expected '$CanonicalId'"
    Assert-Integer $alias.introducedVersion "$JsonPath[0].introducedVersion"
    Assert-True ($alias.introducedVersion -eq 1) "$JsonPath[0].introducedVersion must be 1"
    Assert-True ($null -eq $alias.retirementVersion) "$JsonPath[0].retirementVersion must remain null"
    Assert-True ($alias.migrationIssue -ceq "#165") "$JsonPath[0].migrationIssue must be #165"
}

function Get-Family {
    param(
        [object]$Map,
        [string]$Family
    )

    $matches = @($Map.families | Where-Object { $_.family -ceq $Family })
    Assert-True ($matches.Count -eq 1) "expected exactly one '$Family' family, found $($matches.Count)"
    return $matches[0]
}

function Get-SourceEntry {
    param(
        [object]$SourceMap,
        [string]$Family,
        [string]$TechnicalAnchor
    )

    $sourceFamily = Get-Family $SourceMap $Family
    $matches = @($sourceFamily.entries | Where-Object { $_.technicalAnchor -ceq $TechnicalAnchor })
    Assert-True ($matches.Count -eq 1) "source map expected one '$Family' entry '$TechnicalAnchor', found $($matches.Count)"
    return $matches[0]
}

function Get-GitBlobBytes {
    param(
        [string]$GitExecutable,
        [string]$Commit,
        [string]$RelativePath
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $GitExecutable
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    [void]$startInfo.ArgumentList.Add("cat-file")
    [void]$startInfo.ArgumentList.Add("blob")
    [void]$startInfo.ArgumentList.Add("$Commit`:$RelativePath")

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        Assert-True ($process.Start()) "could not start Git to read the committed source blob"
        $stream = [System.IO.MemoryStream]::new()
        try {
            $process.StandardOutput.BaseStream.CopyTo($stream)
            $errorText = $process.StandardError.ReadToEnd()
            $process.WaitForExit()
            Assert-True ($process.ExitCode -eq 0) "Git could not read '$Commit`:$RelativePath': $errorText"
            return $stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-Sha256 {
    param([byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

$map = Read-StrictJson $resolvedTechnicalSourcePath "technical source"
Assert-ExactProperties $map @(
    "schemaVersion",
    "candidateId",
    "productionEligible",
    "upstream",
    "approval",
    "evidencePolicy",
    "productionFamilyOrder",
    "families",
    "blockingIds",
    "generationGate"
) '$'
Assert-ExactProperties $map.upstream @(
    "sourcePacketId",
    "sourcePacketPath",
    "contentMapPath",
    "sourceCommit",
    "contentMapGitBlobSha256"
) '$.upstream'
Assert-ExactProperties $map.approval @(
    "userFinalCreativeAcceptance",
    "userBalanceAcceptance",
    "runtimeAuthority"
) '$.approval'
Assert-ExactProperties $map.evidencePolicy @(
    "observedValues",
    "balanceAuthority"
) '$.evidencePolicy'
Assert-ExactProperties $map.generationGate @(
    "status",
    "requireProductionEligibleResult",
    "outputPaths"
) '$.generationGate'

Assert-Integer $map.schemaVersion '$.schemaVersion'
Assert-True ($map.schemaVersion -eq 1) "schemaVersion must be 1"
Assert-True ($map.candidateId -is [string]) "candidateId must be a string"
Assert-True ($map.candidateId -ceq "game-data-phase-c-six-family-technical-source-2026-07-23-v001") "unexpected candidateId"
Assert-True ($map.productionEligible -is [bool]) "productionEligible must be a Boolean"
Assert-True ($map.productionEligible -eq $false) "Phase C2 must remain production-ineligible"
Assert-String $map.upstream.sourcePacketId '$.upstream.sourcePacketId'
Assert-String $map.upstream.sourcePacketPath '$.upstream.sourcePacketPath'
Assert-String $map.upstream.contentMapPath '$.upstream.contentMapPath'
Assert-String $map.upstream.sourceCommit '$.upstream.sourceCommit'
Assert-String $map.upstream.contentMapGitBlobSha256 '$.upstream.contentMapGitBlobSha256'
Assert-True ($map.upstream.sourcePacketId -ceq "game-data-phase-c-six-family-source-2026-07-23-v001") "unexpected upstream source packet ID"
Assert-True ($map.upstream.sourcePacketPath -ceq "unity/Docs/Narrative/GameData/Phase_C_Six_Family_Source_Packet.md") "unexpected source packet path"
Assert-True ($map.upstream.contentMapPath -ceq "unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json") "unexpected content-map path"
Assert-True ($map.upstream.sourceCommit -ceq "963c4bc6e6db8ae2b87d363ceb229519e97f13b0") "unexpected merged source commit"
Assert-True ($map.upstream.contentMapGitBlobSha256 -ceq "8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353") "unexpected content-map Git-blob SHA-256"
Assert-String $map.approval.userFinalCreativeAcceptance '$.approval.userFinalCreativeAcceptance'
Assert-String $map.approval.userBalanceAcceptance '$.approval.userBalanceAcceptance'
Assert-String $map.approval.runtimeAuthority '$.approval.runtimeAuthority'
Assert-True ($map.approval.userFinalCreativeAcceptance -ceq "pending") "user final creative acceptance must remain pending"
Assert-True ($map.approval.userBalanceAcceptance -ceq "pending") "user balance acceptance must remain pending"
Assert-True ($map.approval.runtimeAuthority -ceq "unchanged") "runtime authority must remain unchanged"
Assert-String $map.evidencePolicy.observedValues '$.evidencePolicy.observedValues'
Assert-String $map.evidencePolicy.balanceAuthority '$.evidencePolicy.balanceAuthority'
Assert-True ($map.evidencePolicy.observedValues -ceq "migration_evidence_only") "observed values must remain migration evidence only"
Assert-True ($map.evidencePolicy.balanceAuthority -ceq "not_approved") "observed values must not claim balance authority"
Assert-String $map.generationGate.status '$.generationGate.status'
Assert-String $map.generationGate.requireProductionEligibleResult '$.generationGate.requireProductionEligibleResult'
Assert-True ($map.generationGate.status -ceq "blocked") "generation gate must remain blocked"
Assert-True ($map.generationGate.requireProductionEligibleResult -ceq "refused_without_writes") "generation refusal behavior drifted"
Assert-Array $map.generationGate.outputPaths '$.generationGate.outputPaths'
Assert-True (@($map.generationGate.outputPaths).Count -eq 0) "blocked generation must declare zero output paths"

$expectedFamilies = @("realms", "buildings", "research", "troops", "champions", "skills")
Assert-ExactArray $map.productionFamilyOrder $expectedFamilies '$.productionFamilyOrder'
Assert-Array $map.families '$.families'
Assert-True (@($map.families).Count -eq 6) "families must contain exactly six rows"

$sourceMapPath = Join-Path $repoRoot $map.upstream.contentMapPath
$sourcePacketPath = Join-Path $repoRoot $map.upstream.sourcePacketPath
Assert-True (Test-Path -LiteralPath $sourcePacketPath -PathType Leaf) "upstream source packet is missing"
$sourceMap = Read-StrictJson $sourceMapPath "upstream content map"
Assert-True ($sourceMap.packetId -ceq $map.upstream.sourcePacketId) "upstream packet ID does not match the content map"
Assert-ExactArray @($sourceMap.families | ForEach-Object { $_.family }) $expectedFamilies '$.upstream.contentMap.families'

$git = Get-Command git -ErrorAction Stop
$blobBytes = Get-GitBlobBytes $git.Source $map.upstream.sourceCommit $map.upstream.contentMapPath
$blobSha = Get-Sha256 $blobBytes
Assert-True ($blobSha -ceq $map.upstream.contentMapGitBlobSha256) "committed content-map SHA-256 expected '$($map.upstream.contentMapGitBlobSha256)', found '$blobSha'"
& $git.Source -C $repoRoot diff --quiet --no-ext-diff $map.upstream.sourceCommit -- $map.upstream.contentMapPath
Assert-True ($LASTEXITCODE -eq 0) "working content map differs from the committed upstream source"

$expectedRealmRows = @(
    @("stonehold", "RealmId.Stonehold", 1, "realm.stonehold.name", "realm.stonehold.description", "ResourceType.DeepOre"),
    @("eldergrove", "RealmId.Eldergrove", 2, "realm.eldergrove.name", "realm.eldergrove.description", "ResourceType.WorldSap"),
    @("crownlands", "RealmId.Crownlands", 3, "realm.crownlands.name", "realm.crownlands.description", "ResourceType.RoyalSigil"),
    @("umbral", "RealmId.Umbral", 4, "realm.umbral.name", "realm.umbral.description", "ResourceType.DarkCrystal")
)
$expectedBuildingRows = @(
    @("town_hall", "TownHall", "building.town_hall.name"),
    @("farm", "Farm", "building.farm.name"),
    @("lumber_mill", "LumberMill", "building.lumber_mill.name"),
    @("quarry", "Quarry", "building.quarry.name"),
    @("gold_mine", "GoldMine", "building.gold_mine.name"),
    @("barracks", "Barracks", "building.barracks.name"),
    @("academy", "Academy", "building.academy.name"),
    @("market", "Market", "building.market.name"),
    @("storehouse", "Storehouse", "building.storehouse.name"),
    @("forge", "Forge", "building.forge.name"),
    @("stable", "Stable", "building.stable.name"),
    @("workshop", "Workshop", "building.workshop.name"),
    @("embassy", "Embassy", "building.embassy.name"),
    @("wall", "Wall", "building.wall.name"),
    @("watchtower", "Watchtower", "building.watchtower.name")
)
$expectedResearchRows = @(
    @("steel_forging", "Steel Forging", "research.steel_forging.name", "steel_forging"),
    @("plate_armor", "Plate Armor", "research.plate_armor.name", "plate_armor"),
    @("masonry", "Advanced Masonry", "research.advanced_masonry.name", "masonry"),
    @("irrigation", "Irrigation", "research.irrigation.name", "irrigation"),
    @("ballistics", "Ballistics", "research.ballistics.name", $null),
    @("logistics", "Logistics", "research.logistics.name", $null),
    @("trade_routes", "Trade Routes", "research.trade_routes.name", $null),
    @("arcane_study", "Arcane Study", "research.arcane_study.name", "arcane_study")
)
$expectedSkillRows = @(
    @("realm_strike", 0, "melee_damage", 4, 20, 0.05, 2.6, 150, 0.72, "realm_slash"),
    @("renewing_guard", 1, "self_heal_guard", 8, 30, 0.35, 0, 180, 0, "renewing_guard"),
    @("warzone_burst", 2, "area_damage", 10, 45, 0.45, 4.2, 115, 0.72, "warzone_shockwave"),
    @("warmaster_breaker", 3, "elite_break_damage", 14, 60, 0.65, 3.4, 260, 0.72, "warmaster_breaker")
)

$expectedFamilyBlockers = [ordered]@{
    realms = @("realms.rare_resource_catalog", "realms.capability_profiles", "realms.asset_refs")
    buildings = @("buildings.max_level_review", "buildings.production_profiles", "buildings.cost_profiles", "buildings.duration_profiles", "buildings.asset_refs")
    research = @("research.max_levels", "research.cost_profiles", "research.duration_profiles", "research.effects", "research.prerequisites")
    troops = @("troops.records", "troops.localization", "troops.base_stats", "troops.training_profiles", "troops.asset_refs")
    champions = @("champions.records", "champions.localization", "champions.realm_class_assignments", "champions.asset_refs", "champions.base_skill_refs", "champions.stat_profiles")
    skills = @("skills.slot_policy", "skills.behavior_profiles", "skills.presentation_profiles", "skills.target_authority", "skills.audio_asset_refs", "skills.vfx_asset_refs", "skills.balance_acceptance")
}
$expectedUnavailable = [ordered]@{
    realms = @()
    buildings = @("ManaShrine", "Mine")
    research = @()
    troops = @("TroopType.Infantry", "TroopType.Cavalry", "TroopType.Ranged", "TroopType.Siege")
    champions = @()
    skills = @()
}
$expectedMappingCounts = [ordered]@{
    realms = 4
    buildings = 15
    research = 8
    troops = 0
    champions = 0
    skills = 4
}

$canonicalIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$contentRefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
for ($familyIndex = 0; $familyIndex -lt $expectedFamilies.Count; $familyIndex++) {
    $familyName = $expectedFamilies[$familyIndex]
    $family = $map.families[$familyIndex]
    Assert-ExactProperties $family @(
        "family",
        "requiredForProduction",
        "artifactDisposition",
        "mappings",
        "unavailableAnchors",
        "blockingIds"
    ) "$.families[$familyIndex]"
    Assert-String $family.family "$.families[$familyIndex].family"
    Assert-String $family.artifactDisposition "$.families[$familyIndex].artifactDisposition"
    Assert-True ($family.family -ceq $familyName) "family order drifted at index $familyIndex"
    Assert-True ($family.requiredForProduction -is [bool]) "family '$familyName' requiredForProduction must be Boolean"
    Assert-True ($family.requiredForProduction -eq $true) "family '$familyName' must remain production-required"
    Assert-True ($family.artifactDisposition -ceq "blocked_required") "family '$familyName' must remain blocked-required"
    Assert-Array $family.mappings "$.families[$familyIndex].mappings"
    Assert-True (@($family.mappings).Count -eq $expectedMappingCounts[$familyName]) "family '$familyName' mapping count drifted"
    Assert-ExactArray $family.unavailableAnchors @($expectedUnavailable[$familyName]) "$.families[$familyIndex].unavailableAnchors"
    Assert-ExactArray $family.blockingIds @($expectedFamilyBlockers[$familyName]) "$.families[$familyIndex].blockingIds"

    $sourceFamily = Get-Family $sourceMap $familyName
    foreach ($unavailableAnchor in @($family.unavailableAnchors)) {
        $sourceEntry = Get-SourceEntry $sourceMap $familyName $unavailableAnchor
        Assert-True ($sourceEntry.disposition -ceq "not_authored_unavailable") "unavailable anchor '$unavailableAnchor' is not unavailable in Phase C1"
        Assert-True (@($sourceEntry.content).Count -eq 0) "unavailable anchor '$unavailableAnchor' must not have content"
    }
    if ($familyName -ceq "champions") {
        Assert-True (@($sourceFamily.entries).Count -eq 0) "champions must remain recordless"
    }
}

$realmMappings = @(Get-Family $map "realms").mappings
for ($index = 0; $index -lt $expectedRealmRows.Count; $index++) {
    $mapping = $realmMappings[$index]
    $expected = $expectedRealmRows[$index]
    Assert-ExactProperties $mapping @("canonicalId", "technicalAnchor", "legacyEnumValue", "contentRefs", "aliases", "observed") "$.families[0].mappings[$index]"
    Assert-ExactProperties $mapping.observed @("rareResourceAnchor") "$.families[0].mappings[$index].observed"
    Assert-String $mapping.canonicalId "$.families[0].mappings[$index].canonicalId"
    Assert-String $mapping.technicalAnchor "$.families[0].mappings[$index].technicalAnchor"
    Assert-String $mapping.observed.rareResourceAnchor "$.families[0].mappings[$index].observed.rareResourceAnchor"
    Assert-True ($mapping.canonicalId -ceq $expected[0]) "realm canonical ID drifted at index $index"
    Assert-True ($mapping.technicalAnchor -ceq $expected[1]) "realm technical anchor drifted at index $index"
    Assert-Integer $mapping.legacyEnumValue "$.families[0].mappings[$index].legacyEnumValue"
    Assert-True ($mapping.legacyEnumValue -eq $expected[2]) "realm enum value drifted at index $index"
    Assert-ExactArray $mapping.contentRefs @($expected[3], $expected[4]) "$.families[0].mappings[$index].contentRefs"
    Assert-Array $mapping.aliases "$.families[0].mappings[$index].aliases"
    Assert-True (@($mapping.aliases).Count -eq 0) "realm mappings must not invent string aliases"
    Assert-True ($mapping.observed.rareResourceAnchor -ceq $expected[5]) "realm rare-resource evidence drifted at index $index"
    $sourceEntry = Get-SourceEntry $sourceMap "realms" $mapping.technicalAnchor
    Assert-ExactArray @($sourceEntry.content | ForEach-Object { $_.key }) @($mapping.contentRefs) "realm source refs '$($mapping.canonicalId)'"
    Assert-True ($canonicalIds.Add("realms/$($mapping.canonicalId)")) "duplicate realm canonical ID '$($mapping.canonicalId)'"
    foreach ($contentRef in $mapping.contentRefs) { Assert-True ($contentRefs.Add($contentRef)) "duplicate content ref '$contentRef'" }
}

$buildingMappings = @(Get-Family $map "buildings").mappings
for ($index = 0; $index -lt $expectedBuildingRows.Count; $index++) {
    $mapping = $buildingMappings[$index]
    $expected = $expectedBuildingRows[$index]
    Assert-ExactProperties $mapping @("canonicalId", "technicalAnchor", "contentRefs", "aliases", "observed") "$.families[1].mappings[$index]"
    Assert-ExactProperties $mapping.observed @("maxLevel") "$.families[1].mappings[$index].observed"
    Assert-String $mapping.canonicalId "$.families[1].mappings[$index].canonicalId"
    Assert-String $mapping.technicalAnchor "$.families[1].mappings[$index].technicalAnchor"
    Assert-True ($mapping.canonicalId -ceq $expected[0]) "building canonical ID drifted at index $index"
    Assert-True ($mapping.technicalAnchor -ceq $expected[1]) "building technical anchor drifted at index $index"
    Assert-ExactArray $mapping.contentRefs @($expected[2]) "$.families[1].mappings[$index].contentRefs"
    Assert-ExactAlias $mapping.aliases $expected[1] $expected[0] "$.families[1].mappings[$index].aliases"
    Assert-Integer $mapping.observed.maxLevel "$.families[1].mappings[$index].observed.maxLevel"
    Assert-True ($mapping.observed.maxLevel -eq 10) "building '$($mapping.canonicalId)' max level must remain 10"
    $sourceEntry = Get-SourceEntry $sourceMap "buildings" $mapping.technicalAnchor
    Assert-ExactArray @($sourceEntry.content | ForEach-Object { $_.key }) @($mapping.contentRefs) "building source refs '$($mapping.canonicalId)'"
    Assert-True ($canonicalIds.Add("buildings/$($mapping.canonicalId)")) "duplicate building canonical ID '$($mapping.canonicalId)'"
    foreach ($contentRef in $mapping.contentRefs) { Assert-True ($contentRefs.Add($contentRef)) "duplicate content ref '$contentRef'" }
}

$researchMappings = @(Get-Family $map "research").mappings
for ($index = 0; $index -lt $expectedResearchRows.Count; $index++) {
    $mapping = $researchMappings[$index]
    $expected = $expectedResearchRows[$index]
    Assert-ExactProperties $mapping @("canonicalId", "technicalAnchor", "contentRefs", "aliases", "observed") "$.families[2].mappings[$index]"
    Assert-ExactProperties $mapping.observed @("androidLegacyId") "$.families[2].mappings[$index].observed"
    Assert-String $mapping.canonicalId "$.families[2].mappings[$index].canonicalId"
    Assert-String $mapping.technicalAnchor "$.families[2].mappings[$index].technicalAnchor"
    Assert-True ($mapping.canonicalId -ceq $expected[0]) "research canonical ID drifted at index $index"
    Assert-True ($mapping.technicalAnchor -ceq $expected[1]) "research technical anchor drifted at index $index"
    Assert-ExactArray $mapping.contentRefs @($expected[2]) "$.families[2].mappings[$index].contentRefs"
    Assert-ExactAlias $mapping.aliases $expected[1] $expected[0] "$.families[2].mappings[$index].aliases"
    if ($null -eq $expected[3]) {
        Assert-True ($null -eq $mapping.observed.androidLegacyId) "research '$($mapping.canonicalId)' must not invent an Android legacy ID"
    }
    else {
        Assert-String $mapping.observed.androidLegacyId "$.families[2].mappings[$index].observed.androidLegacyId"
        Assert-True ($mapping.observed.androidLegacyId -ceq $expected[3]) "research Android legacy ID drifted at index $index"
    }
    $sourceEntry = Get-SourceEntry $sourceMap "research" $mapping.technicalAnchor
    Assert-ExactArray @($sourceEntry.content | ForEach-Object { $_.key }) @($mapping.contentRefs) "research source refs '$($mapping.canonicalId)'"
    Assert-True ($canonicalIds.Add("research/$($mapping.canonicalId)")) "duplicate research canonical ID '$($mapping.canonicalId)'"
    foreach ($contentRef in $mapping.contentRefs) { Assert-True ($contentRefs.Add($contentRef)) "duplicate content ref '$contentRef'" }
}
Assert-True (-not ($researchMappings.canonicalId -ccontains "advanced_masonry")) "Advanced Masonry must remain content/alias source, not a new canonical ID"

$skillMappings = @(Get-Family $map "skills").mappings
for ($index = 0; $index -lt $expectedSkillRows.Count; $index++) {
    $mapping = $skillMappings[$index]
    $expected = $expectedSkillRows[$index]
    Assert-ExactProperties $mapping @("canonicalId", "technicalAnchor", "contentRefs", "aliases", "observed") "$.families[5].mappings[$index]"
    Assert-ExactProperties $mapping.observed @(
        "legacySlot",
        "role",
        "cooldownSeconds",
        "manaCost",
        "castTimeSeconds",
        "rangeMeters",
        "power",
        "botDamageMultiplier",
        "vfxKey"
    ) "$.families[5].mappings[$index].observed"
    Assert-String $mapping.canonicalId "$.families[5].mappings[$index].canonicalId"
    Assert-String $mapping.technicalAnchor "$.families[5].mappings[$index].technicalAnchor"
    Assert-String $mapping.observed.role "$.families[5].mappings[$index].observed.role"
    Assert-String $mapping.observed.vfxKey "$.families[5].mappings[$index].observed.vfxKey"
    Assert-True ($mapping.canonicalId -ceq $expected[0]) "skill canonical ID drifted at index $index"
    Assert-True ($mapping.technicalAnchor -ceq $expected[0]) "skill technical anchor drifted at index $index"
    Assert-ExactArray $mapping.contentRefs @("skill.$($expected[0]).name") "$.families[5].mappings[$index].contentRefs"
    Assert-Array $mapping.aliases "$.families[5].mappings[$index].aliases"
    Assert-True (@($mapping.aliases).Count -eq 0) "skill mappings must not invent aliases"
    Assert-Integer $mapping.observed.legacySlot "$.families[5].mappings[$index].observed.legacySlot"
    Assert-Number $mapping.observed.cooldownSeconds "$.families[5].mappings[$index].observed.cooldownSeconds"
    Assert-Number $mapping.observed.manaCost "$.families[5].mappings[$index].observed.manaCost"
    Assert-Number $mapping.observed.castTimeSeconds "$.families[5].mappings[$index].observed.castTimeSeconds"
    Assert-Number $mapping.observed.rangeMeters "$.families[5].mappings[$index].observed.rangeMeters"
    Assert-Number $mapping.observed.power "$.families[5].mappings[$index].observed.power"
    Assert-Number $mapping.observed.botDamageMultiplier "$.families[5].mappings[$index].observed.botDamageMultiplier"
    Assert-True ($mapping.observed.legacySlot -eq $expected[1]) "skill slot drifted at index $index"
    Assert-True ($mapping.observed.role -ceq $expected[2]) "skill role evidence drifted at index $index"
    Assert-True ([double]$mapping.observed.cooldownSeconds -eq [double]$expected[3]) "skill cooldown evidence drifted at index $index"
    Assert-True ([double]$mapping.observed.manaCost -eq [double]$expected[4]) "skill mana evidence drifted at index $index"
    Assert-True ([double]$mapping.observed.castTimeSeconds -eq [double]$expected[5]) "skill cast-time evidence drifted at index $index"
    Assert-True ([double]$mapping.observed.rangeMeters -eq [double]$expected[6]) "skill range evidence drifted at index $index"
    Assert-True ([double]$mapping.observed.power -eq [double]$expected[7]) "skill power evidence drifted at index $index"
    Assert-True ([double]$mapping.observed.botDamageMultiplier -eq [double]$expected[8]) "skill bot multiplier evidence drifted at index $index"
    Assert-True ($mapping.observed.vfxKey -ceq $expected[9]) "skill VFX evidence drifted at index $index"
    $sourceEntry = Get-SourceEntry $sourceMap "skills" $mapping.technicalAnchor
    Assert-ExactArray @($sourceEntry.content | ForEach-Object { $_.key }) @($mapping.contentRefs) "skill source refs '$($mapping.canonicalId)'"
    Assert-True ($canonicalIds.Add("skills/$($mapping.canonicalId)")) "duplicate skill canonical ID '$($mapping.canonicalId)'"
    foreach ($contentRef in $mapping.contentRefs) { Assert-True ($contentRefs.Add($contentRef)) "duplicate content ref '$contentRef'" }
}

Assert-True ($canonicalIds.Count -eq 31) "expected 31 mapped canonical IDs, found $($canonicalIds.Count)"
Assert-True ($contentRefs.Count -eq 35) "expected 35 unique content refs, found $($contentRefs.Count)"
Assert-True (@((Get-Family $map "troops").mappings).Count -eq 0) "troops must not contain production mappings"
Assert-True (@((Get-Family $map "champions").mappings).Count -eq 0) "champions must remain recordless"

$expectedBlockingIds = @("approval.user_creative_balance")
foreach ($familyName in $expectedFamilies) {
    $expectedBlockingIds += @($expectedFamilyBlockers[$familyName])
}
Assert-ExactArray $map.blockingIds $expectedBlockingIds '$.blockingIds'
Assert-True (@($map.blockingIds).Count -eq 32) "expected 32 explicit blockers"
foreach ($blockingId in $map.blockingIds) {
    Assert-True ($blockingId -is [string]) "blocking IDs must be strings"
    Assert-True (-not [string]::IsNullOrWhiteSpace($blockingId)) "blocking IDs must not be blank"
}

foreach ($relativePath in $forbiddenProductionPaths) {
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath) -PathType Leaf)) "Phase C2 must not create production artifact '$relativePath'"
}

if ($RequireProductionEligible) {
    throw "Phase C six-family production generation refused without writes: candidate '$($map.candidateId)' is blocked by $(@($map.blockingIds).Count) explicit blockers and pending user approval."
}

Write-Output "PASS: six production-required schemas, 31 exact technical mappings, 35 content refs, 6 unavailable anchors, and 32 blockers validated"
Write-Output "PASS: Phase C technical source remains production-ineligible; authorized six-family runtime catalog-set lives at unity/Assets/StreamingAssets/GameData/"
