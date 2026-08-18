param([string]$PacketPath = "$PSScriptRoot/../../unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json")
$ErrorActionPreference = 'Stop'
function Assert($condition, $message) { if (-not $condition) { throw $message } }
function Test-Packet($packet) {
    Assert ($packet.schemaVersion -eq 1) 'unsupported schema'
    Assert ($packet.packetVersion -ceq 'omen1-a1-2026-08-13-v004') 'packet version drift'
    Assert (($packet.approval.decisions | Sort-Object -Unique).Count -eq 16) 'D1-D16 approval missing'
    Assert ($packet.placement.offerAction -ceq 'SELECT_VALERIUS') 'offer action drift'
    Assert ($packet.placement.autoAccept -eq $false) 'auto-accept must remain disabled'
    Assert ($packet.placement.completionUnlockId -ceq 'CH1_REALM_INTRO') 'completion unlock drift'
    Assert ($packet.placement.completionDestination -ceq 'CH1_REALM_INTRO') 'completion destination drift'
    $groups = @($packet.states.id) + @($packet.objectives.id) + @($packet.dialogue.id) + @($packet.consequences.id)
    Assert (($groups | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) 'blank ID'
    Assert (($groups | Sort-Object -Unique).Count -eq $groups.Count) 'duplicate ID'
    $states = @{}; $packet.states | ForEach-Object { $states[$_.id] = $true }
    $objectives = @{}; $packet.objectives | ForEach-Object { $objectives[$_.id] = $true }
    $dialogue = @{}; $packet.dialogue | ForEach-Object { $dialogue[$_.id] = $true }
    foreach ($transition in $packet.transitions) {
        Assert ($states.ContainsKey($transition.from) -and $states.ContainsKey($transition.to)) 'invalid state target'
        if ($transition.objective) { Assert ($objectives.ContainsKey($transition.objective)) 'missing objective' }
        if ($transition.dialogue) { Assert ($dialogue.ContainsKey($transition.dialogue)) 'missing transition dialogue' }
    }
    foreach ($objective in $packet.objectives) {
        $required = @('id', 'textKey', 'activatesIn', 'completesOn')
        $actual = @($objective.PSObject.Properties.Name)
        Assert ($actual.Count -eq $required.Count) 'objective property count mismatch'
        foreach ($property in $required) {
            Assert ($actual -ccontains $property) "missing objective property: $property"
        }
        foreach ($property in $actual) {
            Assert ($required -ccontains $property) "unexpected objective property: $property"
        }
        Assert ($packet.localization.PSObject.Properties.Name -ccontains $objective.textKey) 'missing objective localization'
    }
    foreach ($node in $packet.dialogue) {
        Assert ($packet.localization.PSObject.Properties.Name -contains $node.textKey) 'missing dialogue localization'
        foreach ($choice in $node.choices) {
            Assert ($packet.localization.PSObject.Properties.Name -contains $choice.key) 'missing choice localization'
            if ($choice.target -and $choice.target -ne 'end') { Assert ($dialogue.ContainsKey($choice.target)) 'missing dialogue target' }
        }
    }
    $expectedCapabilities = @(
        'LOCATION_SKY_CASTLE_MARKER',
        'ACTION_DEPLOY_CHAMPION',
        'HOOK_SKY_CASTLE_ARENA',
        'EVENT_SKY_CASTLE_ARENA_SUCCESS',
        'EVENT_SKY_CASTLE_ARENA_FAILURE',
        'EVENT_SKY_CASTLE_ARENA_CANCELLED',
        'EVENT_SKY_CASTLE_ARENA_UNAVAILABLE',
        'ARTIFACT_CELESTIAL_TEAR',
        'CH1_REALM_INTRO'
    )
    Assert (@($packet.externalCapabilities).Count -eq $expectedCapabilities.Count) 'external capability count drift'
    for ($index = 0; $index -lt $expectedCapabilities.Count; $index++) {
        Assert ($packet.externalCapabilities[$index].id -ceq $expectedCapabilities[$index]) 'external capability identity/order drift'
    }
    Assert (($packet.externalCapabilities | Where-Object status -ne 'requested').Count -eq 0) 'external capability falsely verified'
    $offerText = $packet.localization.'dialogue.omen1.offer'
    Assert ($offerText -ceq 'The Veil Watch has detected a strange resonance above the Sky Castle. Will you hear my report?') 'offer source text drift'
    Assert ($offerText -notmatch '(?i)\bmy lord\b') 'pre-appointment title leaked into offer source'
    Assert (($packet.consequences | Group-Object id | Where-Object Count -gt 1).Count -eq 0) 'conflicting consequence trigger'
    Assert (@($packet.transitions | Where-Object { $_.from -eq 'OFFERED' }).Count -gt 0) 'unreachable start state'
    $reachable = @{ OFFERED = $true }
    do {
        $before = $reachable.Count
        foreach ($transition in $packet.transitions) {
            if ($reachable.ContainsKey($transition.from)) { $reachable[$transition.to] = $true }
        }
    } while ($reachable.Count -gt $before)
    foreach ($state in $packet.states) { Assert ($reachable.ContainsKey($state.id)) "unreachable state: $($state.id)" }
}
$packet = Get-Content -Raw -LiteralPath $PacketPath | ConvertFrom-Json
Test-Packet $packet
$negative = @(
    @{ Name='duplicate ID'; Mutate={ param($p) $p.objectives[1].id=$p.objectives[0].id } },
    @{ Name='missing dialogue target'; Mutate={ param($p) $p.dialogue[0].choices[0].target='MISSING' } },
    @{ Name='invalid state target'; Mutate={ param($p) $p.transitions[0].to='MISSING' } },
    @{ Name='missing objective'; Mutate={ param($p) $p.transitions[0].objective='MISSING' } },
    @{ Name='external classification'; Mutate={ param($p) $p.externalCapabilities[0].status='verified' } },
    @{ Name='unreachable state'; Mutate={ param($p) $p.states += [pscustomobject]@{ id='ORPHANED'; resume='none'; terminal=$false } } },
    @{ Name='conflicting consequence trigger'; Mutate={ param($p) $p.consequences[1].id=$p.consequences[0].id; $p.consequences[1].trigger='DIFFERENT_TRIGGER' } },
    @{ Name='duplicate objective source text'; Mutate={ param($p) $p.objectives[2] | Add-Member -NotePropertyName sourceText -NotePropertyValue 'Duplicate authority' } },
    @{ Name='missing required objective property'; Mutate={ param($p) $p.objectives[2].PSObject.Properties.Remove('activatesIn') } },
    @{ Name='wrong-case objective property'; Mutate={ param($p) $value=$p.objectives[0].textKey; $p.objectives[0].PSObject.Properties.Remove('textKey'); $p.objectives[0] | Add-Member -NotePropertyName TextKey -NotePropertyValue $value } },
    @{ Name='missing approval'; Mutate={ param($p) $p.approval.decisions=@('D1') } },
    @{ Name='packet version drift'; Mutate={ param($p) $p.packetVersion='omen1-a1-2026-07-29-v003' } },
    @{ Name='offer action drift'; Mutate={ param($p) $p.placement.offerAction='AUTO_OFFER' } },
    @{ Name='auto accept enabled'; Mutate={ param($p) $p.placement.autoAccept=$true } },
    @{ Name='direct Kingdom destination'; Mutate={ param($p) $p.placement.completionDestination='KINGDOM_COMMAND_VIEW' } },
    @{ Name='Kingdom capability restored'; Mutate={ param($p) $p.externalCapabilities += [pscustomobject]@{ id='KINGDOM_COMMAND_VIEW'; status='requested' } } },
    @{ Name='pre-appointment title restored'; Mutate={ param($p) $p.localization.'dialogue.omen1.offer'='My lord, the Veil Watch has detected a strange resonance above the Sky Castle. Will you hear my report?' } }
)
foreach ($case in $negative) {
    $copy = ($packet | ConvertTo-Json -Depth 20 | ConvertFrom-Json)
    & $case.Mutate $copy
    $rejected = $false
    try { Test-Packet $copy } catch { $rejected = $true }
    Assert $rejected "negative fixture accepted: $($case.Name)"
}
Write-Host "OMEN_1 A1 packet accepted; $($negative.Count) negative fixtures rejected."
