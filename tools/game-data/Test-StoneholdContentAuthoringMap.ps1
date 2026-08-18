param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Validates the Stonehold content-authoring slice (troops, champions, research, skills)
# against the canonical six-family content map and the non-fabrication rule.
#
# Checks:
#   - strict UTF-8 JSON (no BOM, single LF terminator, no duplicate properties)
#   - exact top-level and family shape
#   - 8 research + 4 skill name-only records with verbatim name_ref/display-name
#     cross-checked against the live content map
#   - canonical ID sets and lowercase snake_case conformance
#   - placeholder-flag dispositions (blocked_required) for all blocked mechanics
#   - zero-record troops/champions explicit not_authored_unavailable absence
#   - pinned content-map provenance (source commit + git-blob SHA-256)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$slicePath = Join-Path $repoRoot "unity\Docs\Narrative\GameData\stonehold-content-authoring-map.json"
$contentMapPath = Join-Path $repoRoot "unity\Docs\Narrative\GameData\phase-c-six-family-content-map.json"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Stonehold content-authoring-map validation failed: $Message"
    }
}

function Assert-String {
    param(
        [object]$Value,
        [string]$JsonPath
    )

    Assert-True ($Value -is [string]) "$JsonPath must be a string"
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
        throw "Stonehold content-authoring-map validation failed: $Label strict JSON validation failed: $($_.Exception.Message)"
    }

    try {
        return $text | ConvertFrom-Json
    }
    catch {
        throw "Stonehold content-authoring-map validation failed: $Label JSON conversion failed: $($_.Exception.Message)"
    }
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

# --- Load and shape-validate the slice and the content map ----------------------

$slice = Read-StrictJson $slicePath "Stonehold content-authoring map"
$contentMap = Read-StrictJson $contentMapPath "canonical content map"

Assert-ExactProperties $slice @(
    "schemaVersion",
    "sliceId",
    "realmId",
    "scope",
    "authority",
    "sources",
    "provenance",
    "nonFabricationRule",
    "loaderWiring",
    "families"
) '$'

Assert-ExactProperties $slice.authority @(
    "primaryMode",
    "userFinalCreativeAcceptance",
    "userBalanceAcceptance",
    "runtimeAuthority",
    "productionEligible"
) '$.authority'

Assert-ExactProperties $slice.sources @(
    "contentMap",
    "technicalHandoff",
    "skillConvergence",
    "championConvergence",
    "audit"
) '$.sources'

Assert-ExactProperties $slice.sources.contentMap @(
    "path",
    "sourceCommit",
    "gitBlobSha256"
) '$.sources.contentMap'

Assert-ExactProperties $slice.provenance @(
    "generatedArtifactsMustRecordSourceCommit",
    "generatedArtifactsMustRecordSourceBlobSha256"
) '$.provenance'

Assert-ExactProperties $slice.loaderWiring @("status", "reason") '$.loaderWiring'

Assert-True ($slice.schemaVersion -eq 1) "schemaVersion must be 1"
Assert-String $slice.sliceId '$.sliceId'
Assert-String $slice.realmId '$.realmId'
Assert-True ($slice.realmId -ceq "stonehold") "realmId must be 'stonehold'"
Assert-String $slice.authority.primaryMode '$.authority.primaryMode'
Assert-True ($slice.authority.userFinalCreativeAcceptance -ceq "pending") "userFinalCreativeAcceptance must remain pending"
Assert-True ($slice.authority.userBalanceAcceptance -ceq "pending") "userBalanceAcceptance must remain pending"
Assert-True ($slice.authority.runtimeAuthority -ceq "unchanged") "runtimeAuthority must remain unchanged"
Assert-True ($slice.authority.productionEligible -eq $false) "productionEligible must remain false"
Assert-True ($slice.provenance.generatedArtifactsMustRecordSourceCommit -eq $true) "provenance must require source commit"
Assert-True ($slice.provenance.generatedArtifactsMustRecordSourceBlobSha256 -eq $true) "provenance must require source blob SHA-256"
Assert-True ($slice.loaderWiring.status -ceq "blocked") "loader wiring must remain blocked"

Assert-Array $slice.families '$.families'
Assert-True (@($slice.families).Count -eq 4) "families must contain exactly four entries (research, skills, troops, champions)"

$expectedFamilyOrder = @("research", "skills", "troops", "champions")
for ($index = 0; $index -lt $expectedFamilyOrder.Count; $index++) {
    $family = $slice.families[$index]
    Assert-ExactProperties $family @(
        "family",
        "authoringDisposition",
        "records",
        "blockedFields",
        "absence"
    ) "$.families[$index]"
    Assert-True ($family.family -ceq $expectedFamilyOrder[$index]) "family order drifted at index $index"
    Assert-Array $family.records "$.families[$index].records"
    Assert-Array $family.blockedFields "$.families[$index].blockedFields"
}

# --- Provenance: verify the pinned content-map blob SHA-256 --------------------

$git = Get-Command git -ErrorAction Stop
$blobBytes = Get-GitBlobBytes $git.Source $slice.sources.contentMap.sourceCommit $slice.sources.contentMap.path
$blobSha = Get-Sha256 $blobBytes
Assert-True ($blobSha -ceq $slice.sources.contentMap.gitBlobSha256) "content-map git-blob SHA-256 expected '$($slice.sources.contentMap.gitBlobSha256)', found '$blobSha'"
Assert-True ($slice.sources.contentMap.sourceCommit -ceq "963c4bc6e6db8ae2b87d363ceb229519e97f13b0") "content-map source commit drifted"

# --- Cross-check records against the live content map --------------------------

# Build a content map lookup: family -> content-key -> verbatim source text (name only).
$contentByNameKey = @{}
foreach ($cmFamily in @($contentMap.families)) {
    foreach ($entry in @($cmFamily.entries)) {
        foreach ($contentValue in @($entry.content)) {
            if ($contentValue.field -ceq "name") {
                $contentByNameKey[[string]$contentValue.key] = [string]$contentValue.sourceText
            }
        }
    }
}

$expectedResearchRecords = @(
    @("steel_forging", "research.steel_forging.name", "Steel Forging"),
    @("plate_armor", "research.plate_armor.name", "Plate Armor"),
    @("masonry", "research.advanced_masonry.name", "Advanced Masonry"),
    @("irrigation", "research.irrigation.name", "Irrigation"),
    @("ballistics", "research.ballistics.name", "Ballistics"),
    @("logistics", "research.logistics.name", "Logistics"),
    @("trade_routes", "research.trade_routes.name", "Trade Routes"),
    @("arcane_study", "research.arcane_study.name", "Arcane Study")
)
$expectedSkillRecords = @(
    @("realm_strike", "skill.realm_strike.name", "Realm Strike"),
    @("renewing_guard", "skill.renewing_guard.name", "Renewing Guard"),
    @("warzone_burst", "skill.warzone_burst.name", "Warzone Burst"),
    @("warmaster_breaker", "skill.warmaster_breaker.name", "Warmaster Breaker")
)

$idPattern = "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$"

function Assert-NameOnlyRecords {
    param(
        [object]$Family,
        [object[]]$ExpectedRecords,
        [string]$JsonPath
    )

    $records = @($Family.records)
    Assert-True ($records.Count -eq $ExpectedRecords.Count) "$JsonPath expected $($ExpectedRecords.Count) records, found $($records.Count)"
    Assert-True ($Family.authoringDisposition -ceq "name_only_records_mechanics_blocked") "$JsonPath authoringDisposition must be name_only_records_mechanics_blocked"
    Assert-True ($null -eq $Family.absence) "$JsonPath absence must be null for an authored family"

    $seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    for ($index = 0; $index -lt $ExpectedRecords.Count; $index++) {
        $record = $records[$index]
        $expected = $ExpectedRecords[$index]
        Assert-ExactProperties $record @("id", "nameRef", "displayName", "sourceAnchor") "$JsonPath.records[$index]"
        Assert-String $record.id "$JsonPath.records[$index].id"
        Assert-String $record.nameRef "$JsonPath.records[$index].nameRef"
        Assert-String $record.displayName "$JsonPath.records[$index].displayName"
        Assert-String $record.sourceAnchor "$JsonPath.records[$index].sourceAnchor"
        Assert-True ($record.id -cmatch $idPattern) "record id '$($record.id)' is not a canonical lowercase snake_case ID"
        Assert-True ($seenIds.Add([string]$record.id)) "duplicate record id '$($record.id)'"
        Assert-True ($record.id -ceq $expected[0]) "record id drifted at index ${index}: expected '$($expected[0])', found '$($record.id)'"
        Assert-True ($record.nameRef -ceq $expected[1]) "record nameRef drifted at index $index"
        Assert-True ($record.displayName -ceq $expected[2]) "record displayName drifted at index $index"
        Assert-True ($contentByNameKey.ContainsKey([string]$record.nameRef)) "nameRef '$($record.nameRef)' has no verbatim entry in the content map"
        Assert-True ($contentByNameKey[[string]$record.nameRef] -ceq $record.displayName) "displayName '$($record.displayName)' does not match the verbatim content-map text '$($contentByNameKey[[string]$record.nameRef])'"
    }
}

$researchFamily = Get-Family $slice "research"
$skillFamily = Get-Family $slice "skills"
$troopFamily = Get-Family $slice "troops"
$championFamily = Get-Family $slice "champions"

Assert-NameOnlyRecords $researchFamily $expectedResearchRecords '$.families[0]'
Assert-NameOnlyRecords $skillFamily $expectedSkillRecords '$.families[1]'

$expectedResearchBlocked = @("max_level", "cost_profile_id", "duration_profile_id", "effect_ids", "prerequisite_research_ids")
Assert-True (@($researchFamily.blockedFields).Count -eq $expectedResearchBlocked.Count) "research blockedFields count drifted"
for ($i = 0; $i -lt $expectedResearchBlocked.Count; $i++) {
    Assert-True ([string]$researchFamily.blockedFields[$i] -ceq $expectedResearchBlocked[$i]) "research blockedFields[$i] drifted"
}

$expectedSkillBlocked = @("behavior_profile_id", "presentation_profile_id", "target_type", "cooldown_seconds", "power", "mana_cost", "cast_time_seconds", "range_meters", "vfx_asset_ref", "audio_asset_ref")
Assert-True (@($skillFamily.blockedFields).Count -eq $expectedSkillBlocked.Count) "skills blockedFields count drifted"
for ($i = 0; $i -lt $expectedSkillBlocked.Count; $i++) {
    Assert-True ([string]$skillFamily.blockedFields[$i] -ceq $expectedSkillBlocked[$i]) "skills blockedFields[$i] drifted"
}

# --- Troops and champions: explicit zero-record absence ------------------------

Assert-True (@($troopFamily.records).Count -eq 0) "troops must remain recordless"
Assert-True ($troopFamily.authoringDisposition -ceq "not_authored_unavailable") "troops disposition must be not_authored_unavailable"
Assert-True ($null -ne $troopFamily.absence) "troops requires an explicit absence marker"
Assert-True ($troopFamily.absence.disposition -ceq "not_authored_unavailable") "troops absence disposition drifted"
Assert-ExactProperties $troopFamily.absence @("disposition", "reason", "anchors", "visualPrecursor") '$.families[2].absence'
$troopAnchors = @($troopFamily.absence.anchors)
Assert-True ($troopAnchors.Count -eq 4) "troops absence must cite four TroopType anchors"
$expectedTroopAnchors = @("TroopType.Infantry", "TroopType.Cavalry", "TroopType.Ranged", "TroopType.Siege")
for ($i = 0; $i -lt 4; $i++) {
    Assert-True ([string]$troopAnchors[$i] -ceq $expectedTroopAnchors[$i]) "troop anchor drifted at index $i"
}
Assert-True ($null -eq $troopFamily.absence.visualPrecursor) "troops must not cite a visual precursor"

Assert-True (@($championFamily.records).Count -eq 0) "champions must remain recordless"
Assert-True ($championFamily.authoringDisposition -ceq "not_authored_unavailable") "champions disposition must be not_authored_unavailable"
Assert-True ($null -ne $championFamily.absence) "champions requires an explicit absence marker"
Assert-True ($championFamily.absence.disposition -ceq "not_authored_unavailable") "champions absence disposition drifted"
Assert-ExactProperties $championFamily.absence @("disposition", "reason", "anchors", "visualPrecursor") '$.families[3].absence'
Assert-True (@($championFamily.absence.anchors).Count -eq 0) "champions must not cite troop anchors"
Assert-True ($null -ne $championFamily.absence.visualPrecursor) "champions absence must cite the visual precursor"
Assert-True ($championFamily.absence.visualPrecursor -cmatch "Stonehold Vanguard") "champions absence must name the Stonehold Vanguard precursor"

# --- Non-fabrication guard: content-map dispositions ---------------------------

$cmTroopFamily = Get-Family $contentMap "troops"
Assert-True (@($cmTroopFamily.entries).Count -eq 4) "content map troops must have four unavailable anchors"
foreach ($entry in @($cmTroopFamily.entries)) {
    Assert-True ($entry.disposition -ceq "not_authored_unavailable") "content-map troop '$($entry.technicalAnchor)' must remain unavailable"
    Assert-True (@($entry.content).Count -eq 0) "content-map troop '$($entry.technicalAnchor)' must not gain content"
}

$cmChampionFamily = Get-Family $contentMap "champions"
Assert-True (@($cmChampionFamily.entries).Count -eq 0) "content-map champions must remain recordless"

$cmResearchFamily = Get-Family $contentMap "research"
Assert-True (@($cmResearchFamily.entries).Count -eq 8) "content-map research must have eight entries"
$cmSkillFamily = Get-Family $contentMap "skills"
Assert-True (@($cmSkillFamily.entries).Count -eq 4) "content-map skills must have four entries"

Write-Output "PASS: Stonehold slice authored 8 research + 4 skill name-only records (verbatim, traceable), 0 troop/champion records with explicit not_authored_unavailable absence, all mechanics blocked_required, provenance verified"
