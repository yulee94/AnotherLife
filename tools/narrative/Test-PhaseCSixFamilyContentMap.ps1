param(
    [switch]$VerifyLegacyBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$mapPath = Join-Path $repoRoot "unity\Docs\Narrative\GameData\phase-c-six-family-content-map.json"
$packetPath = Join-Path $repoRoot "unity\Docs\Narrative\GameData\Phase_C_Six_Family_Source_Packet.md"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Phase C six-family content-map validation failed: $Message"
    }
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
        $propertyNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            Assert-True ($propertyNames.Add($property.Name)) "$JsonPath contains duplicate property '$($property.Name)'"
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

function Get-RequiredFamily {
    param(
        [object]$Map,
        [string]$Family
    )

    $matches = @($Map.families | Where-Object { $_.family -ceq $Family })
    Assert-True ($matches.Count -eq 1) "expected exactly one '$Family' family, found $($matches.Count)"
    return $matches[0]
}

function Get-RequiredEntry {
    param(
        [object]$Family,
        [string]$TechnicalAnchor
    )

    $matches = @($Family.entries | Where-Object { $_.technicalAnchor -ceq $TechnicalAnchor })
    Assert-True ($matches.Count -eq 1) "expected exactly one '$($Family.family)' entry '$TechnicalAnchor', found $($matches.Count)"
    return $matches[0]
}

function Get-RequiredContent {
    param(
        [object]$Entry,
        [string]$Field
    )

    $matches = @($Entry.content | Where-Object { $_.field -ceq $Field })
    Assert-True ($matches.Count -eq 1) "expected exactly one '$Field' content value for '$($Entry.technicalAnchor)', found $($matches.Count)"
    return $matches[0]
}

Assert-True (Test-Path -LiteralPath $mapPath -PathType Leaf) "content map is missing: $mapPath"
Assert-True (Test-Path -LiteralPath $packetPath -PathType Leaf) "packet is missing: $packetPath"

$mapBytes = [System.IO.File]::ReadAllBytes($mapPath)
Assert-True ($mapBytes.Length -gt 0) "content map is empty"
Assert-True (-not ($mapBytes.Length -ge 3 -and $mapBytes[0] -eq 0xEF -and $mapBytes[1] -eq 0xBB -and $mapBytes[2] -eq 0xBF)) "content map must be UTF-8 without a BOM"
Assert-True ($mapBytes[$mapBytes.Length - 1] -eq 0x0A) "content map must end with one LF byte"

$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$mapText = $strictUtf8.GetString($mapBytes)
$jsonOptions = [System.Text.Json.JsonDocumentOptions]::new()
$jsonOptions.AllowTrailingCommas = $false
$jsonOptions.CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
$jsonOptions.MaxDepth = 32
try {
    $jsonDocument = [System.Text.Json.JsonDocument]::Parse($mapText, $jsonOptions)
    try {
        Assert-NoDuplicateJsonProperties $jsonDocument.RootElement '$'
    }
    finally {
        $jsonDocument.Dispose()
    }
}
catch {
    throw "Phase C six-family content-map validation failed: strict JSON validation failed: $($_.Exception.Message)"
}

try {
    $map = $mapText | ConvertFrom-Json
}
catch {
    throw "Phase C six-family content-map validation failed: invalid JSON: $($_.Exception.Message)"
}

Assert-ExactProperties $map @("schemaVersion", "packetId", "sourceBaseCommit", "expectedContentReferenceCount", "authority", "provenance", "families") '$'
Assert-ExactProperties $map.authority @("primaryMode", "codexFidelityStatus", "userFinalCreativeAcceptance", "runtimeAuthority") '$.authority'
Assert-ExactProperties $map.provenance @("hashBytes", "generatedArtifactsMustRecordSourceCommit", "generatedArtifactsMustRecordSourceBlobSha256") '$.provenance'
Assert-True ($map.schemaVersion -is [long]) "schemaVersion must be an integer"
Assert-True ($map.packetId -is [string]) "packetId must be a string"
Assert-True ($map.sourceBaseCommit -is [string]) "sourceBaseCommit must be a string"
Assert-True ($map.expectedContentReferenceCount -is [long]) "expectedContentReferenceCount must be an integer"
Assert-True ($map.authority.primaryMode -is [string]) "authority.primaryMode must be a string"
Assert-True ($map.authority.codexFidelityStatus -is [string]) "authority.codexFidelityStatus must be a string"
Assert-True ($map.authority.userFinalCreativeAcceptance -is [string]) "authority.userFinalCreativeAcceptance must be a string"
Assert-True ($map.authority.runtimeAuthority -is [string]) "authority.runtimeAuthority must be a string"
Assert-True ($map.provenance.hashBytes -is [string]) "provenance.hashBytes must be a string"
Assert-True ($map.provenance.generatedArtifactsMustRecordSourceCommit -is [bool]) "generatedArtifactsMustRecordSourceCommit must be a Boolean"
Assert-True ($map.provenance.generatedArtifactsMustRecordSourceBlobSha256 -is [bool]) "generatedArtifactsMustRecordSourceBlobSha256 must be a Boolean"
Assert-True ($map.families -is [System.Array]) "families must be an array"
Assert-True ($map.schemaVersion -eq 1) "schemaVersion must be 1"
Assert-True ($map.packetId -ceq "game-data-phase-c-six-family-source-2026-07-23-v001") "unexpected packetId"
Assert-True ($map.sourceBaseCommit -ceq "38de51138cc8b92c8469c7e9b5c37e84dead7ff1") "sourceBaseCommit must match the reviewed upstream main commit"
Assert-True ($map.expectedContentReferenceCount -eq 35) "expectedContentReferenceCount must be 35"
Assert-True ($map.authority.primaryMode -ceq "codex_narrative_content") "primary mode must remain Codex narrative/content"
Assert-True ($map.authority.codexFidelityStatus -ceq "content_reference_handoff_ready") "unexpected Codex fidelity status"
Assert-True ($map.authority.userFinalCreativeAcceptance -ceq "pending") "user final creative acceptance must remain pending"
Assert-True ($map.authority.runtimeAuthority -ceq "unchanged") "runtime authority must remain unchanged"
Assert-True ($map.provenance.hashBytes -ceq "committed_git_blob_content_bytes") "hash-byte provenance rule is missing"
Assert-True ($map.provenance.generatedArtifactsMustRecordSourceCommit -eq $true) "generated artifacts must retain the source commit"
Assert-True ($map.provenance.generatedArtifactsMustRecordSourceBlobSha256 -eq $true) "generated artifacts must retain the source blob SHA-256"

$expectedFamilies = @("realms", "buildings", "research", "troops", "champions", "skills")
$expectedEntryCounts = @{
    realms = 4
    buildings = 17
    research = 8
    troops = 4
    champions = 0
    skills = 4
}
$expectedContentCounts = @{
    realms = 8
    buildings = 15
    research = 8
    troops = 0
    champions = 0
    skills = 4
}
$expectedUnavailableCounts = @{
    realms = 0
    buildings = 2
    research = 0
    troops = 4
    champions = 0
    skills = 0
}

$families = @($map.families)
Assert-True ($families.Count -eq $expectedFamilies.Count) "expected six families, found $($families.Count)"

$contentKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$allowedDispositions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
[void]$allowedDispositions.Add("verbatim_preserved")
[void]$allowedDispositions.Add("not_authored_unavailable")
$recordlessUnavailableFamilyCount = 0

for ($familyIndex = 0; $familyIndex -lt $expectedFamilies.Count; $familyIndex++) {
    $family = $families[$familyIndex]
    $expectedFamily = $expectedFamilies[$familyIndex]
    Assert-ExactProperties $family @("family", "sourceStatus", "sourceEvidence", "entries") "$.families[$familyIndex]"
    Assert-True ($family.family -is [string]) "$.families[$familyIndex].family must be a string"
    Assert-True ($family.sourceStatus -is [string]) "$.families[$familyIndex].sourceStatus must be a string"
    Assert-True ($family.sourceEvidence -is [System.Array]) "$.families[$familyIndex].sourceEvidence must be an array"
    Assert-True ($family.entries -is [System.Array]) "$.families[$familyIndex].entries must be an array"
    Assert-True ($family.family -ceq $expectedFamily) "family order must be '$($expectedFamilies -join ', ')'; position $familyIndex was '$($family.family)'"
    Assert-True (-not [string]::IsNullOrWhiteSpace($family.sourceStatus)) "family '$expectedFamily' requires a sourceStatus"

    $evidencePaths = @($family.sourceEvidence)
    Assert-True ($evidencePaths.Count -gt 0) "family '$expectedFamily' requires source evidence"
    $evidenceSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($evidencePath in $evidencePaths) {
        Assert-True ($evidencePath -is [string]) "family '$expectedFamily' evidence paths must be strings"
        Assert-True (-not [string]::IsNullOrWhiteSpace($evidencePath)) "family '$expectedFamily' contains a blank evidence path"
        Assert-True ($evidenceSet.Add([string]$evidencePath)) "family '$expectedFamily' repeats evidence path '$evidencePath'"
        Assert-True (Test-Path -LiteralPath (Join-Path $repoRoot $evidencePath) -PathType Leaf) "family '$expectedFamily' evidence path does not exist: $evidencePath"
    }

    $entries = @($family.entries)
    Assert-True ($entries.Count -eq $expectedEntryCounts[$expectedFamily]) "family '$expectedFamily' expected $($expectedEntryCounts[$expectedFamily]) entries, found $($entries.Count)"
    if ($entries.Count -eq 0 -and $family.sourceStatus.StartsWith("not_authored_unavailable", [System.StringComparison]::Ordinal)) {
        $recordlessUnavailableFamilyCount++
    }
    $anchorSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $familyContentCount = 0
    $familyUnavailableCount = 0

    foreach ($entry in $entries) {
        Assert-ExactProperties $entry @("technicalAnchor", "disposition", "content") "$.families[$familyIndex].entries[$($anchorSet.Count)]"
        Assert-True ($entry.technicalAnchor -is [string]) "family '$expectedFamily' technicalAnchor must be a string"
        Assert-True ($entry.disposition -is [string]) "entry '$($entry.technicalAnchor)' disposition must be a string"
        Assert-True ($entry.content -is [System.Array]) "entry '$($entry.technicalAnchor)' content must be an array"
        Assert-True (-not [string]::IsNullOrWhiteSpace($entry.technicalAnchor)) "family '$expectedFamily' contains a blank technicalAnchor"
        Assert-True ($anchorSet.Add([string]$entry.technicalAnchor)) "family '$expectedFamily' repeats technicalAnchor '$($entry.technicalAnchor)'"
        Assert-True ($allowedDispositions.Contains([string]$entry.disposition)) "entry '$($entry.technicalAnchor)' has unsupported disposition '$($entry.disposition)'"

        $content = @($entry.content)
        if ($entry.disposition -ceq "verbatim_preserved") {
            Assert-True ($content.Count -gt 0) "verbatim-preserved entry '$($entry.technicalAnchor)' must contain source text"
        }
        else {
            $familyUnavailableCount++
            Assert-True ($content.Count -eq 0) "unavailable entry '$($entry.technicalAnchor)' must not contain source text"
        }

        $fieldSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($contentValue in $content) {
            Assert-ExactProperties $contentValue @("field", "key", "sourceText") "$.families[$familyIndex].entries[$($anchorSet.Count - 1)].content[$($fieldSet.Count)]"
            Assert-True ($contentValue.field -is [string]) "content field for '$($entry.technicalAnchor)' must be a string"
            Assert-True ($contentValue.key -is [string]) "content key for '$($entry.technicalAnchor)' must be a string"
            Assert-True ($contentValue.sourceText -is [string]) "sourceText for '$($entry.technicalAnchor)' must be a string"
            $familyContentCount++
            Assert-True ($contentValue.field -cin @("name", "description")) "entry '$($entry.technicalAnchor)' has unsupported content field '$($contentValue.field)'"
            Assert-True ($fieldSet.Add([string]$contentValue.field)) "entry '$($entry.technicalAnchor)' repeats field '$($contentValue.field)'"
            Assert-True ($contentValue.key -cmatch "^(realm|building|research|skill)\.[a-z][a-z0-9_]*\.(name|description)$") "invalid content key '$($contentValue.key)'"
            Assert-True ($contentKeys.Add([string]$contentValue.key)) "duplicate content key '$($contentValue.key)'"
            Assert-True (-not [string]::IsNullOrWhiteSpace($contentValue.sourceText)) "content key '$($contentValue.key)' has blank sourceText"
        }
    }

    Assert-True ($familyContentCount -eq $expectedContentCounts[$expectedFamily]) "family '$expectedFamily' expected $($expectedContentCounts[$expectedFamily]) content values, found $familyContentCount"
    Assert-True ($familyUnavailableCount -eq $expectedUnavailableCounts[$expectedFamily]) "family '$expectedFamily' expected $($expectedUnavailableCounts[$expectedFamily]) unavailable entries, found $familyUnavailableCount"
}

Assert-True ($contentKeys.Count -eq $map.expectedContentReferenceCount) "expected $($map.expectedContentReferenceCount) unique content references, found $($contentKeys.Count)"
Assert-True ($recordlessUnavailableFamilyCount -eq 1) "expected one recordless unavailable family, found $recordlessUnavailableFamilyCount"

$packetText = Get-Content -LiteralPath $packetPath -Raw
Assert-True ($packetText.Contains($map.packetId)) "packet does not cite its packetId"
Assert-True ($packetText.Contains("phase-c-six-family-content-map.json")) "packet does not cite the authoritative content map"
foreach ($contentKey in $contentKeys) {
    $occurrenceCount = ([regex]::Matches($packetText, [regex]::Escape("``$contentKey``"))).Count
    Assert-True ($occurrenceCount -eq 1) "packet must cite content key '$contentKey' exactly once, found $occurrenceCount"
}

if ($VerifyLegacyBaseline) {
    $localDataPath = Join-Path $repoRoot "unity\Assets\AL\Scripts\Services\Local\LocalGameDataService.cs"
    $enumPath = Join-Path $repoRoot "unity\Assets\AL\Scripts\Core\Enums\Enums.cs"
    $skillPath = Join-Path $repoRoot "unity\Assets\AL\StreamingAssets\GameData\al_skill_weather_catalog.json"
    $skillCasterPath = Join-Path $repoRoot "unity\Assets\AL\Scripts\ChampionMode\Skills\SkillCaster.cs"
    $localDataSource = Get-Content -LiteralPath $localDataPath -Raw

    $realmFamily = Get-RequiredFamily $map "realms"
    $realmMatches = [regex]::Matches($localDataSource, 'CreateFallbackRealm\(RealmId\.([A-Za-z]+), "([^"]+)", "((?:\\.|[^"])*)", "([^"]+)"\);')
    Assert-True ($realmMatches.Count -eq 4) "legacy baseline must contain four realm rows"
    foreach ($realmMatch in $realmMatches) {
        $anchor = "RealmId.$($realmMatch.Groups[1].Value)"
        $entry = Get-RequiredEntry $realmFamily $anchor
        $name = Get-RequiredContent $entry "name"
        $description = Get-RequiredContent $entry "description"
        $expectedDescription = [regex]::Unescape($realmMatch.Groups[3].Value + "\n\n" + $realmMatch.Groups[4].Value)
        Assert-True ($name.sourceText -ceq $realmMatch.Groups[2].Value) "realm name drift for '$anchor'"
        Assert-True ($description.sourceText -ceq $expectedDescription) "realm description drift for '$anchor'"
    }

    $buildingFamily = Get-RequiredFamily $map "buildings"
    $buildingLine = ([regex]::Match($localDataSource, 'string\[\] bIds = \{ ([^;]+) \};')).Groups[1].Value
    $buildingIds = @([regex]::Matches($buildingLine, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    Assert-True ($buildingIds.Count -eq 15) "legacy baseline must contain fifteen building rows"
    Assert-True ($localDataSource.Contains('def.DisplayName = bId.Replace("Mill", " Mill").Replace("Hall", " Hall").Replace("Mine", " Mine");')) "legacy building DisplayName transformation changed"
    foreach ($buildingId in $buildingIds) {
        $entry = Get-RequiredEntry $buildingFamily $buildingId
        $name = Get-RequiredContent $entry "name"
        $expectedName = $buildingId.Replace("Mill", " Mill").Replace("Hall", " Hall").Replace("Mine", " Mine")
        Assert-True ($name.sourceText -ceq $expectedName) "building name drift for '$buildingId'"
    }
    foreach ($unavailableBuilding in @("ManaShrine", "Mine")) {
        $entry = Get-RequiredEntry $buildingFamily $unavailableBuilding
        Assert-True ($entry.disposition -ceq "not_authored_unavailable") "'$unavailableBuilding' must remain unavailable"
        Assert-True (@($entry.content).Count -eq 0) "'$unavailableBuilding' must not gain inferred content"
    }

    $researchFamily = Get-RequiredFamily $map "research"
    $researchLine = ([regex]::Match($localDataSource, 'string\[\] techs = \{ ([^;]+) \};')).Groups[1].Value
    $researchNames = @([regex]::Matches($researchLine, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    Assert-True ($researchNames.Count -eq 8) "legacy baseline must contain eight research rows"
    foreach ($researchName in $researchNames) {
        $entry = Get-RequiredEntry $researchFamily $researchName
        $name = Get-RequiredContent $entry "name"
        Assert-True ($name.sourceText -ceq $researchName) "research name drift for '$researchName'"
    }

    $troopFamily = Get-RequiredFamily $map "troops"
    $enumSource = Get-Content -LiteralPath $enumPath -Raw
    $troopEnumBlock = ([regex]::Match($enumSource, 'public enum TroopType\s*\{(?<values>.*?)\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)).Groups['values'].Value
    $troopEnumNames = @([regex]::Matches($troopEnumBlock, '(?m)^\s*([A-Za-z][A-Za-z0-9_]*)\s*,?\s*$') | ForEach-Object { $_.Groups[1].Value })
    $expectedTroopEnumNames = @("Infantry", "Cavalry", "Ranged", "Siege")
    Assert-True ($troopEnumNames.Count -eq $expectedTroopEnumNames.Count) "TroopType baseline must contain exactly four values"
    for ($troopIndex = 0; $troopIndex -lt $expectedTroopEnumNames.Count; $troopIndex++) {
        Assert-True ($troopEnumNames[$troopIndex] -ceq $expectedTroopEnumNames[$troopIndex]) "TroopType order/value drift at index $troopIndex"
        $entry = Get-RequiredEntry $troopFamily "TroopType.$($troopEnumNames[$troopIndex])"
        Assert-True ($entry.disposition -ceq "not_authored_unavailable") "troop '$($troopEnumNames[$troopIndex])' must remain unavailable"
        Assert-True (@($entry.content).Count -eq 0) "troop '$($troopEnumNames[$troopIndex])' must not gain inferred content"
    }

    $championFamily = Get-RequiredFamily $map "champions"
    Assert-True (@($championFamily.entries).Count -eq 0) "champion source must remain recordless"
    Assert-True ($localDataSource.Contains('public TroopDefinition GetTroop(string id) => null;')) "legacy GetTroop lookup no longer confirms record absence"
    Assert-True ($localDataSource.Contains('public ChampionDefinition GetChampion(string id) => null;')) "legacy GetChampion lookup no longer confirms record absence"
    $committedAssetCandidates = @(& git -C $repoRoot ls-files -- "unity/Assets/*.asset")
    Assert-True ($LASTEXITCODE -eq 0) "git ls-files failed while checking committed Unity assets"
    Assert-True ($committedAssetCandidates.Count -eq 0) "legacy baseline unexpectedly contains committed ScriptableObject asset candidates"

    $skillFamily = Get-RequiredFamily $map "skills"
    $skillCatalog = Get-Content -LiteralPath $skillPath -Raw | ConvertFrom-Json
    $skillRows = @($skillCatalog.skillLoadouts)
    Assert-True ($skillRows.Count -eq 4) "legacy baseline must contain four skill rows"
    foreach ($skillRow in $skillRows) {
        $entry = Get-RequiredEntry $skillFamily $skillRow.id
        $name = Get-RequiredContent $entry "name"
        Assert-True ($name.sourceText -ceq $skillRow.displayName) "skill name drift for '$($skillRow.id)'"
    }

    $skillCasterSource = Get-Content -LiteralPath $skillCasterPath -Raw
    $skillNameBlock = ([regex]::Match($skillCasterSource, 'private readonly string\[\] _skillNames\s*=\s*\{(?<values>.*?)\};', [System.Text.RegularExpressions.RegexOptions]::Singleline)).Groups['values'].Value
    $skillIdBlock = ([regex]::Match($skillCasterSource, 'private readonly string\[\] _skillIds\s*=\s*\{(?<values>.*?)\};', [System.Text.RegularExpressions.RegexOptions]::Singleline)).Groups['values'].Value
    $skillCasterNames = @([regex]::Matches($skillNameBlock, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    $skillCasterIds = @([regex]::Matches($skillIdBlock, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    Assert-True ($skillCasterNames.Count -eq 4) "SkillCaster baseline must contain four skill names"
    Assert-True ($skillCasterIds.Count -eq 4) "SkillCaster baseline must contain four skill IDs"
    for ($skillIndex = 0; $skillIndex -lt 4; $skillIndex++) {
        $entry = Get-RequiredEntry $skillFamily $skillCasterIds[$skillIndex]
        $name = Get-RequiredContent $entry "name"
        Assert-True ($name.sourceText -ceq $skillCasterNames[$skillIndex]) "SkillCaster name drift for '$($skillCasterIds[$skillIndex])'"
    }

    Write-Output "PASS: legacy baseline matches 4 realms, 15 buildings, 8 research labels, 4 troop enum anchors, and 4 skills; troop/champion assets and lookup records remain absent"
}

Write-Output "PASS: six families, 37 observed technical anchors, 35 unique content references, 6 explicit unavailable anchors, and 1 unavailable recordless family validated"
