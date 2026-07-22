param([string]$PacketPath = "$PSScriptRoot/../../unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json")
$ErrorActionPreference = 'Stop'
function Assert($condition, $message) { if (-not $condition) { throw $message } }
function Test-Packet($packet) {
    Assert ($packet.schemaVersion -eq 1) 'unsupported schema'
    Assert (($packet.approval.decisions | Sort-Object -Unique).Count -eq 16) 'D1-D16 approval missing'
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
    foreach ($node in $packet.dialogue) {
        Assert ($packet.localization.PSObject.Properties.Name -contains $node.textKey) 'missing dialogue localization'
        foreach ($choice in $node.choices) {
            Assert ($packet.localization.PSObject.Properties.Name -contains $choice.key) 'missing choice localization'
            if ($choice.target -and $choice.target -ne 'end') { Assert ($dialogue.ContainsKey($choice.target)) 'missing dialogue target' }
        }
    }
    Assert (($packet.externalCapabilities | Where-Object status -ne 'requested').Count -eq 0) 'external capability falsely verified'
    Assert (($packet.consequences | Group-Object id | Where-Object Count -gt 1).Count -eq 0) 'conflicting consequence trigger'
    Assert (($packet.transitions | Where-Object from -eq 'OFFERED').Count -gt 0) 'unreachable start state'
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
    @{ Name='missing approval'; Mutate={ param($p) $p.approval.decisions=@('D1') } }
)
foreach ($case in $negative) {
    $copy = ($packet | ConvertTo-Json -Depth 20 | ConvertFrom-Json)
    & $case.Mutate $copy
    $rejected = $false
    try { Test-Packet $copy } catch { $rejected = $true }
    Assert $rejected "negative fixture accepted: $($case.Name)"
}
Write-Host "OMEN_1 A1 packet accepted; $($negative.Count) negative fixtures rejected."
