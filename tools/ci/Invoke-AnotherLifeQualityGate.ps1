param(
    [ValidateSet("Classify", "Hygiene")]
    [string] $Mode,
    [string] $BaseRef = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-GitLines {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$output"
    }

    return @($output)
}

function Get-BaseRef {
    if ($BaseRef) {
        return $BaseRef
    }

    if ($env:GITHUB_BASE_REF) {
        return "origin/$env:GITHUB_BASE_REF"
    }

    return "origin/main"
}

function Get-ChangedFiles {
    $base = Get-BaseRef
    try {
        $files = @(Invoke-GitLines @("diff", "--name-only", "--diff-filter=ACMRT", "$base...HEAD"))
        if ($files.Count -eq 0 -and (-not $env:GITHUB_ACTIONS -or $BaseRef -eq "HEAD")) {
            $files = @(Invoke-GitLines @("diff", "--cached", "--name-only", "--diff-filter=ACMRT"))
        }
        return $files
    } catch {
        Write-Warning "Falling back to HEAD file list because changed-file diff was unavailable: $($_.Exception.Message)"
        return @(Invoke-GitLines @("ls-files"))
    }
}

function Read-GitHubEvent {
    if (-not $env:GITHUB_EVENT_PATH -or -not (Test-Path -LiteralPath $env:GITHUB_EVENT_PATH)) {
        return $null
    }

    return Get-Content -Raw -LiteralPath $env:GITHUB_EVENT_PATH | ConvertFrom-Json
}

function Add-Failure {
    param(
        [System.Collections.Generic.List[string]] $Failures,
        [string] $Message
    )

    $Failures.Add($Message) | Out-Null
    Write-Host "::error::$Message"
}

function Get-PolicyPath {
    $candidate = ".github/anotherlife-policy.yml"
    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    $repoCandidate = Join-Path (Join-Path $PSScriptRoot "..\..") ".github/anotherlife-policy.yml"
    if (Test-Path -LiteralPath $repoCandidate) {
        return $repoCandidate
    }

    return ""
}

function Convert-PolicyValue {
    param([string] $Value)

    $text = ""
    if ($null -ne $Value) {
        $text = $Value.Trim()
    }
    if (($text.StartsWith('"') -and $text.EndsWith('"')) -or
        ($text.StartsWith("'") -and $text.EndsWith("'"))) {
        $text = $text.Substring(1, $text.Length - 2)
    }

    return $text.Replace("\\", "\")
}

function Get-PolicyList {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [string[]] $Default = @()
    )

    $policyPath = Get-PolicyPath
    if (-not $policyPath) {
        return @($Default)
    }

    $lines = Get-Content -LiteralPath $policyPath
    $values = [System.Collections.Generic.List[string]]::new()
    $inList = $false
    foreach ($line in $lines) {
        if ($line -match "^\s*$([regex]::Escape($Name))\s*:\s*$") {
            $inList = $true
            continue
        }

        if ($inList -and $line -match "^\S[^:]*:\s*") {
            break
        }

        if ($inList -and $line -match "^\s*-\s+(.+?)\s*$") {
            $values.Add((Convert-PolicyValue $Matches[1])) | Out-Null
        }
    }

    if ($values.Count -eq 0) {
        return @($Default)
    }

    return @($values)
}

function Get-PolicyScalar {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [string] $Default = ""
    )

    $policyPath = Get-PolicyPath
    if (-not $policyPath) {
        return $Default
    }

    foreach ($line in Get-Content -LiteralPath $policyPath) {
        if ($line -match "^\s*$([regex]::Escape($Name))\s*:\s*(.+?)\s*$") {
            return Convert-PolicyValue $Matches[1]
        }
    }

    return $Default
}

function Assert-NoFailures {
    param([System.Collections.Generic.List[string]] $Failures)

    if ($Failures.Count -gt 0) {
        throw "$($Failures.Count) quality gate failure(s)."
    }
}

function Test-BodyContainsPath {
    param(
        [string] $Body,
        [string] $Path
    )

    $normalized = $Path -replace "\\", "/"
    return $Body -replace "\\", "/" -match [regex]::Escape($normalized)
}

function Test-AnyPathPrefix {
    param(
        [string] $Path,
        [string[]] $Prefixes
    )

    $normalizedPath = ""
    if ($null -ne $Path) {
        $normalizedPath = $Path -replace "\\", "/"
    }
    foreach ($prefix in $Prefixes) {
        $normalizedPrefix = ""
        if ($null -ne $prefix) {
            $normalizedPrefix = $prefix -replace "\\", "/"
        }
        if ($normalizedPrefix -and $normalizedPath.StartsWith($normalizedPrefix, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Invoke-Classify {
    $failures = [System.Collections.Generic.List[string]]::new()
    $event = Read-GitHubEvent
    $body = ""
    $draft = $false
    $baseBranch = $env:GITHUB_BASE_REF
    $headBranch = $env:GITHUB_HEAD_REF
    $branchPrefixes = Get-PolicyList "branch_prefixes" @("codex/")
    $primaryModes = Get-PolicyList "primary_modes" @(
        "Codex coordination/review",
        "Codex narrative/content",
        "Codex terrestrial design",
        "Codex engineering"
    )
    $retiredAgentsAndPrefixes = Get-PolicyList "retired_agents_and_prefixes" @("GPT", "Android Studio", "gpt/", "android-studio/", "gemini/")

    if ($null -ne $event -and $event.PSObject.Properties.Name -contains "pull_request") {
        $body = [string]$event.pull_request.body
        $draft = [bool]$event.pull_request.draft
        $baseBranch = [string]$event.pull_request.base.ref
        $headBranch = [string]$event.pull_request.head.ref
    } else {
        $headBranch = (Invoke-GitLines @("branch", "--show-current") | Select-Object -First 1)
    }

    $changedFiles = Get-ChangedFiles
    Write-Host "Changed files:"
    $changedFiles | ForEach-Object { Write-Host "  $_" }

    if ($headBranch -and -not ($branchPrefixes | Where-Object { $headBranch.StartsWith($_, [System.StringComparison]::Ordinal) })) {
        Add-Failure $failures "Branch '$headBranch' does not use an allowed Codex-only AnotherLife prefix: $($branchPrefixes -join ', ')."
    }

    if ($baseBranch -and $baseBranch -ne "main" -and $body -notmatch "(?i)(depends on|prerequisite|stacked|base branch)") {
        Add-Failure $failures "Base branch '$baseBranch' is not main and no stacked/dependency declaration was found."
    }

    if ($body) {
        $modePattern = ($primaryModes | ForEach-Object { [regex]::Escape($_) }) -join "|"
        $modeMatches = [regex]::Matches($body, "- \[[xX]\] ($modePattern)")

        if ($modeMatches.Count -ne 1) {
            Add-Failure $failures "Exactly one primary Codex mode must be selected in the PR body."
        }

        $retiredModeNames = @($retiredAgentsAndPrefixes | Where-Object { $_ -notmatch "/$" })
        if ($retiredModeNames.Count -gt 0) {
            $retiredModePattern = ($retiredModeNames | ForEach-Object { [regex]::Escape($_) }) -join "|"
            if ($body -match "- \[[xX]\] ($retiredModePattern)") {
                Add-Failure $failures "Retired AnotherLife ownership modes are selected in the PR body."
            }
        }

        if ($body -notmatch "(?i)(fixes|refs|closes|related|upstream|dependency).{0,80}(#\d+|https://github\.com/.+/issues/\d+)") {
            Add-Failure $failures "PR body must link an upstream issue or artifact."
        }

        if ($draft -and $body -match "READY TO MERGE") {
            Add-Failure $failures "Draft PR body must not claim READY TO MERGE."
        }
    } elseif ($env:GITHUB_EVENT_NAME -eq "pull_request") {
        Add-Failure $failures "Pull request body is unavailable or empty."
    }

    $sharedFiles = Get-PolicyList "shared_files" @(
        "unity/Assets/AL/Scripts/Core/Bootloader.cs",
        "unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs",
        "unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs",
        "unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs"
    )

    foreach ($sharedFile in $sharedFiles) {
        if ($changedFiles -contains $sharedFile -and $body -and -not (Test-BodyContainsPath $body $sharedFile)) {
            Add-Failure $failures "Shared file '$sharedFile' changed but was not declared in the PR body."
        }
    }

    $narrativePrefixes = Get-PolicyList "narrative_source_paths" @("unity/Docs/NVS_01_A1", "unity/Docs/Narrative", "app/src/main/java/com/example/anotherlife/narrative/")
    $terrestrialPrefixes = Get-PolicyList "terrestrial_design_paths" @("unity/Assets/AL/Art/Terrestrials/", "unity/Assets/AL/Art/Designs/Terrestrial", "unity/Docs/Terrestrials/", "unity/Docs/Terrestrial")
    $engineeringPrefixes = @(
        Get-PolicyList "runtime_paths" @("app/", "unity/Assets/AL/Scripts/", "unity/Assets/AL/Tests/")
    ) + @(
        Get-PolicyList "workflow_paths" @(".github/workflows/", ".github/anotherlife-policy.yml", "tools/ci/")
    )

    $narrativeChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $narrativePrefixes })
    $terrestrialChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $terrestrialPrefixes })
    $engineeringChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $engineeringPrefixes })

    if (($narrativeChanged -or $terrestrialChanged) -and $engineeringChanged -and $body -notmatch "(?i)mixed-mode|separate PRs are impractical") {
        Add-Failure $failures "Source-mode and engineering paths are mixed without an explicit mixed-mode justification."
    }

    Write-Host "Narrative paths changed: $($narrativeChanged.Count)"
    Write-Host "Terrestrial design paths changed: $($terrestrialChanged.Count)"
    Write-Host "Engineering/workflow paths changed: $($engineeringChanged.Count)"
    Assert-NoFailures $failures
}

function Invoke-Hygiene {
    $failures = [System.Collections.Generic.List[string]]::new()
    $base = Get-BaseRef

    Write-Host "Running git diff --check against $base...HEAD"
    $diffCheck = & git diff --check "$base...HEAD" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Add-Failure $failures "git diff --check failed:`n$diffCheck"
    }

    $trackedFiles = @(Invoke-GitLines @("ls-files"))
    $forbiddenPatterns = Get-PolicyList "forbidden_tracked_path_patterns" @(
        "^unity/(Library|Temp|Logs|Build|Builds|UserSettings)/",
        "^unity/Assets/StreamingAssets/aa/",
        "(^|/)(build|obj|bin|\.gradle)/",
        "\.(apk|aab|ipa|exe|dll|pdb|keystore|jks|p12|mobileprovision)$",
        "(?i)(unity_lic|signing\.properties|local\.properties|\.env)"
    )
    $productionTestScenePath = Get-PolicyScalar "production_test_scene_path" "unity/Assets/Test.unity"
    $productionTestSceneProjectPath = $productionTestScenePath -replace "^unity/", ""

    foreach ($file in $trackedFiles) {
        foreach ($pattern in $forbiddenPatterns) {
            if ($file -match $pattern) {
                Add-Failure $failures "Forbidden tracked artifact or sensitive file: $file"
            }
        }
    }

    $guidToFiles = @{}
    foreach ($metaFile in ($trackedFiles | Where-Object { $_ -like "*.meta" })) {
        $match = Select-String -LiteralPath $metaFile -Pattern "^guid:\s*([0-9a-fA-F]{32})" | Select-Object -First 1
        if ($null -eq $match) {
            continue
        }

        $guid = $match.Matches[0].Groups[1].Value.ToLowerInvariant()
        if (-not $guidToFiles.ContainsKey($guid)) {
            $guidToFiles[$guid] = [System.Collections.Generic.List[string]]::new()
        }

        $guidToFiles[$guid].Add($metaFile) | Out-Null
    }

    foreach ($entry in $guidToFiles.GetEnumerator()) {
        if ($entry.Value.Count -gt 1) {
            Add-Failure $failures "Duplicate Unity meta GUID '$($entry.Key)' in: $($entry.Value -join ', ')"
        }
    }

    $buildSettings = "unity/ProjectSettings/EditorBuildSettings.asset"
    if (Test-Path -LiteralPath $buildSettings) {
        $enabledScenes = [System.Collections.Generic.List[string]]::new()
        $currentEnabled = $false
        foreach ($line in Get-Content -LiteralPath $buildSettings) {
            if ($line -match "enabled:\s*1") {
                $currentEnabled = $true
            }
            if ($line -match "path:\s*(.+)$") {
                $path = $Matches[1].Trim()
                if ($currentEnabled -and $path) {
                    $enabledScenes.Add($path) | Out-Null
                }
                $currentEnabled = $false
            }
        }

        $sceneNames = @{}
        foreach ($scene in $enabledScenes) {
            if ($scene -eq $productionTestSceneProjectPath) {
                Add-Failure $failures "$productionTestSceneProjectPath must not be enabled in production Build Settings."
            }

            $repoScenePath = "unity/$scene"
            if (-not (Test-Path -LiteralPath $repoScenePath)) {
                Add-Failure $failures "Enabled Build Settings scene is missing: $scene"
            }

            $name = [System.IO.Path]::GetFileNameWithoutExtension($scene)
            if ($sceneNames.ContainsKey($name)) {
                Add-Failure $failures "Duplicate enabled scene name '$name' from '$scene' and '$($sceneNames[$name])'."
            } else {
                $sceneNames[$name] = $scene
            }
        }
    }

    foreach ($jsonFile in ($trackedFiles | Where-Object { $_ -like "*.json" })) {
        try {
            Get-Content -Raw -LiteralPath $jsonFile | ConvertFrom-Json | Out-Null
        } catch {
            Add-Failure $failures "Malformed JSON file '$jsonFile': $($_.Exception.Message)"
        }
    }

    foreach ($workflow in ($trackedFiles | Where-Object { $_ -like ".github/workflows/*.yml" -or $_ -like ".github/workflows/*.yaml" })) {
        $text = Get-Content -Raw -LiteralPath $workflow
        if ($text -match "pull_request_target") {
            Add-Failure $failures "Workflow '$workflow' uses pull_request_target, which is prohibited for untrusted code."
        }
        if ($text -match "(?m)^\s*permissions:\s*write-all\s*$") {
            Add-Failure $failures "Workflow '$workflow' requests overbroad write-all token permissions."
        }
        if ($text -match "uses:\s*[^@\r\n]+@v\d+(\s|$)") {
            Add-Failure $failures "Workflow '$workflow' uses a mutable major-version action tag instead of an immutable SHA."
        }
    }

    Write-Host "Tracked files checked: $($trackedFiles.Count)"
    Write-Host "Unity meta GUIDs checked: $($guidToFiles.Count)"
    Assert-NoFailures $failures
}

switch ($Mode) {
    "Classify" { Invoke-Classify }
    "Hygiene" { Invoke-Hygiene }
}
