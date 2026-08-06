param(
    [string] $Mode,
    [string] $BaseRef = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$script:StrictUtf8Encoding = New-Object System.Text.UTF8Encoding($false, $true)

function Invoke-GitLines {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$output"
    }

    return @($output)
}

function Get-PushEventCommitRange {
    if ($env:GITHUB_EVENT_NAME -ne "push") {
        return $null
    }

    $event = Read-GitHubEvent
    if ($null -eq $event -or
        -not ($event.PSObject.Properties.Name -contains "before") -or
        -not ($event.PSObject.Properties.Name -contains "after")) {
        throw "Push event payload is unavailable or does not contain before/after commit SHAs."
    }

    $before = [string]$event.before
    $after = [string]$event.after
    $shaPattern = "^[0-9a-fA-F]{40}$"
    if ($before -notmatch $shaPattern -or
        $after -notmatch $shaPattern -or
        $before -eq ("0" * 40)) {
        throw "Push event before/after commit range is invalid for protected-main verification."
    }

    $checkedOutHead = (
        Invoke-GitLines @("rev-parse", "HEAD") |
            Select-Object -First 1
    ).Trim()
    if (-not $checkedOutHead.Equals(
            $after,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Push checkout HEAD '$checkedOutHead' does not match event after SHA '$after'."
    }

    return [pscustomobject]@{
        Before = $before
        After = $after
    }
}

function Assert-GitCommitObject {
    param(
        [Parameter(Mandatory = $true)][string] $Sha,
        [Parameter(Mandatory = $true)][string] $EventField
    )

    try {
        Invoke-GitLines @("cat-file", "-e", "${Sha}^{commit}") | Out-Null
    } catch {
        throw "GitHub event $EventField SHA '$Sha' is not an available commit object."
    }
}

function Get-PullRequestEventCommitRange {
    if ($env:GITHUB_EVENT_NAME -ne "pull_request") {
        return $null
    }

    $event = Read-GitHubEvent
    if ($null -eq $event -or
        -not ($event.PSObject.Properties.Name -contains "pull_request") -or
        $null -eq $event.pull_request -or
        $null -eq $event.pull_request.base -or
        $null -eq $event.pull_request.head -or
        -not ($event.pull_request.base.PSObject.Properties.Name -contains "sha") -or
        -not ($event.pull_request.head.PSObject.Properties.Name -contains "sha")) {
        throw "Pull request event payload is unavailable or does not contain base/head metadata."
    }

    $baseSha = [string]$event.pull_request.base.sha
    $headSha = [string]$event.pull_request.head.sha
    $shaPattern = "^[0-9a-fA-F]{40}$"
    if ($baseSha -notmatch $shaPattern -or
        $headSha -notmatch $shaPattern -or
        $baseSha -eq ("0" * 40) -or
        $headSha -eq ("0" * 40)) {
        throw "Pull request event base/head commit SHAs are invalid."
    }

    Assert-GitCommitObject $baseSha "pull_request.base.sha"
    Assert-GitCommitObject $headSha "pull_request.head.sha"

    $checkedOutHead = (
        Invoke-GitLines @("rev-parse", "HEAD") |
            Select-Object -First 1
    ).Trim()
    if (-not $checkedOutHead.Equals(
            $headSha,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Pull request checkout HEAD '$checkedOutHead' does not match event head SHA '$headSha'."
    }

    return [pscustomobject]@{
        Base = $baseSha
        Head = $headSha
    }
}

function Get-DiffRange {
    if ($BaseRef) {
        return "$BaseRef...HEAD"
    }

    $pushRange = Get-PushEventCommitRange
    if ($null -ne $pushRange) {
        return "$($pushRange.Before)..$($pushRange.After)"
    }

    $pullRequestRange = Get-PullRequestEventCommitRange
    if ($null -ne $pullRequestRange) {
        return "$($pullRequestRange.Base)...$($pullRequestRange.Head)"
    }

    return "origin/main...HEAD"
}

function Get-ChangedFiles {
    $diffRange = Get-DiffRange

    try {
        $files = @(
            Invoke-GitLines @(
                "diff",
                "--name-only",
                "--no-renames",
                "--diff-filter=ACDMT",
                $diffRange
            )
        )
        if ($files.Count -eq 0 -and (-not $env:GITHUB_ACTIONS -or $BaseRef -eq "HEAD")) {
            $files = @(
                Invoke-GitLines @(
                    "diff",
                    "--cached",
                    "--name-only",
                    "--no-renames",
                    "--diff-filter=ACDMT"
                )
            )
        }
        return @(
            $files |
                Where-Object { $_ } |
                Sort-Object -Unique
        )
    } catch {
        throw "Changed-file diff failed closed for '$diffRange': $($_.Exception.Message)"
    }
}

function Read-GitHubEvent {
    if (-not $env:GITHUB_EVENT_PATH -or -not (Test-Path -LiteralPath $env:GITHUB_EVENT_PATH)) {
        return $null
    }

    return Read-StrictUtf8Text $env:GITHUB_EVENT_PATH | ConvertFrom-Json
}

function Read-StrictUtf8Text {
    param([Parameter(Mandatory = $true)][string] $Path)

    # PowerShell 5.1 defaults Get-Content to the active ANSI code page. A
    # multibyte UTF-8 sequence can therefore consume a following JSON quote
    # as a DBCS trail byte and make valid repository data look malformed.
    return [System.IO.File]::ReadAllText($Path, $script:StrictUtf8Encoding)
}

function ConvertTo-SafeDiagnosticText {
    param([AllowNull()][string] $Text)

    if ($null -eq $Text) {
        return ""
    }

    try {
        $safe = $Text
        $safe = [regex]::Replace(
            $safe,
            "(?i)\bgithub_pat_[A-Za-z0-9_]{20,}\b",
            "<redacted-token>"
        )
        $safe = [regex]::Replace(
            $safe,
            "(?i)\bgh[pousr]_[A-Za-z0-9_]{20,}\b",
            "<redacted-token>"
        )
        $safe = [regex]::Replace(
            $safe,
            "(?i)([""']?Authorization[""']?\s*[:=]\s*[""']?)(?:(?:Bearer|token|Basic)\s+)?[^""'\s,;]+",
            '$1<redacted-token>'
        )
        $safe = [regex]::Replace(
            $safe,
            "(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]{20,}",
            '$1<redacted-token>'
        )

        $roots = [System.Collections.Generic.List[string]]::new()
        if ($env:GITHUB_WORKSPACE) {
            $roots.Add([string]$env:GITHUB_WORKSPACE) | Out-Null
        }

        try {
            $scriptRepoRoot = [System.IO.Path]::GetFullPath(
                (Join-Path $PSScriptRoot "..\..")
            )
            $roots.Add($scriptRepoRoot) | Out-Null
        } catch {
            # Continue with token redaction and any other available root.
        }

        foreach ($root in ($roots | Where-Object { $_ } | Sort-Object -Unique)) {
            $normalizedRoot = ([string]$root).Replace("\", "/").TrimEnd("/")
            $isUncRoot = $normalizedRoot.StartsWith(
                "//",
                [System.StringComparison]::Ordinal
            )
            $isUnixRoot = -not $isUncRoot -and $normalizedRoot.StartsWith(
                "/",
                [System.StringComparison]::Ordinal
            )
            $isDriveRoot = $normalizedRoot -match "^[A-Za-z]:/"
            if (-not $normalizedRoot -or
                (-not $isUncRoot -and -not $isUnixRoot -and -not $isDriveRoot)) {
                continue
            }

            $prefixPattern = ""
            $rootRemainder = $normalizedRoot
            if ($isUncRoot) {
                $prefixPattern = "[\\/]{2}"
                $rootRemainder = $normalizedRoot.Substring(2)
            } elseif ($isUnixRoot) {
                $prefixPattern = "[\\/]"
                $rootRemainder = $normalizedRoot.Substring(1)
            } elseif ($isDriveRoot) {
                $prefixPattern = [regex]::Escape($normalizedRoot.Substring(0, 2)) + "[\\/]"
                $rootRemainder = $normalizedRoot.Substring(3)
            }

            $segments = @(
                $rootRemainder.Split(
                    @("/"),
                    [System.StringSplitOptions]::RemoveEmptyEntries
                )
            )
            if ($segments.Count -eq 0) {
                continue
            }

            $rootPattern = $prefixPattern + (
                ($segments | ForEach-Object { [regex]::Escape($_) }) -join "[\\/]"
            )
            # Redact only at a path separator or an explicit diagnostic
            # terminator. Spaces, plus signs, and ordinary filename
            # characters can legally continue a sibling path and must not be
            # treated as a root boundary. Three dots admit BaseRef...HEAD.
            $rootPattern += '(?=$|[\\/]|\.{3}|[''"]|[.,;!?](?:\s|[''"]|$)|[\)\]\}](?:\s|$))'
            $regexOptions = if ($isUnixRoot) {
                [System.Text.RegularExpressions.RegexOptions]::None
            } else {
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
            }
            $safe = [regex]::Replace(
                $safe,
                $rootPattern,
                "<repo>",
                $regexOptions
            )
        }

        return $safe
    } catch {
        # A sanitizer failure must never fall back to the unsanitized text.
        return "<diagnostic-redaction-failed>"
    }
}

function Write-SafeHost {
    param([AllowNull()][string] $Message)

    Write-Host (ConvertTo-SafeDiagnosticText $Message)
}

function Add-Failure {
    param(
        [System.Collections.Generic.List[string]] $Failures,
        [string] $Message
    )

    $safeMessage = ConvertTo-SafeDiagnosticText $Message
    $Failures.Add($safeMessage) | Out-Null
    Write-SafeHost "::error::$safeMessage"
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

function Test-IsSubstantiveMixedModeRationale {
    param([AllowNull()][string] $Text)

    if (-not $Text) {
        return $false
    }

    $candidate = $Text.Trim()
    $candidate = [regex]::Replace($candidate, "^(?:[-*+]\s+|>\s*)+", "").Trim()
    if (-not $candidate -or
        $candidate -match "(?i)^(?:n/?a|none|not applicable|no|tbd|todo|pending|-)\.?$" -or
        $candidate -match "(?i)^(?:(?:this|it)\s+is\s+)?not\s+(?:a\s+)?mixed-mode\s+pr\.?$" -or
        $candidate -match "(?i)^a\s+mixed-mode\s+pr\s+requires\s+a\s+written\s+codex\s+coordination/review\s+justification\s+explaining\s+why\s+separate\s+prs\s+are\s+impractical\.?$" -or
        $candidate -match "(?i)^(?:\*\*|__)?mixed-mode\s+(?:justification(?:/exception)?|exception)(?:\*\*|__)?\s*:?$") {
        return $false
    }

    return $true
}

function Test-HasMixedModeJustification {
    param([AllowNull()][string] $Body)

    if (-not $Body) {
        return $false
    }

    # Only an affirmative marker plus a written explanation is authoritative.
    # A plain label must explain itself on the same line. A Markdown heading may
    # introduce one or more rationale lines, but cannot borrow text from the next
    # section. Template guidance, denials, placeholders, and empty markers fail.
    $labelPattern = "(?i)^\s*(?:[-*+]\s+)?(?:\*\*|__)?mixed-mode\s+(?:justification(?:/exception)?|exception)(?:\*\*|__)?\s*:\s*(?<rationale>.*?)\s*$"
    $headingPattern = "(?i)^\s*#{1,6}\s+(?:\*\*|__)?mixed-mode\s+(?:justification(?:/exception)?|exception)(?:\*\*|__)?\s*:?\s*$"
    $anyHeadingPattern = "^\s*#{1,6}(?:\s+|$)"
    $lines = @([regex]::Split($Body, "\r?\n"))

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = [string]$lines[$index]
        if ($line -match $labelPattern) {
            if (Test-IsSubstantiveMixedModeRationale $Matches["rationale"]) {
                return $true
            }
            continue
        }

        if ($line -notmatch $headingPattern) {
            continue
        }

        for ($rationaleIndex = $index + 1; $rationaleIndex -lt $lines.Count; $rationaleIndex++) {
            $rationaleLine = [string]$lines[$rationaleIndex]
            if ($rationaleLine -match $anyHeadingPattern) {
                break
            }
            if (Test-IsSubstantiveMixedModeRationale $rationaleLine) {
                return $true
            }
        }
    }

    return $false
}

function Invoke-Classify {
    $failures = [System.Collections.Generic.List[string]]::new()
    $event = Read-GitHubEvent
    $isPullRequest = $env:GITHUB_EVENT_NAME -eq "pull_request"
    $body = ""
    $draft = $false
    $baseBranch = $env:GITHUB_BASE_REF
    $headBranch = $env:GITHUB_HEAD_REF
    $branchPrefixes = Get-PolicyList "branch_prefixes" @("codex/")
    $a2BranchPrefix = Get-PolicyScalar "a2_branch_prefix" "a2/terrestrial-"
    $a2PrimaryMode = Get-PolicyScalar "a2_primary_mode" "A2 terrestrial design"
    $primaryModes = Get-PolicyList "primary_modes" @(
        "Codex coordination/review",
        "Codex narrative/content",
        "A2 terrestrial design",
        "Codex engineering"
    )
    $retiredAgentsAndPrefixes = Get-PolicyList "retired_agents_and_prefixes" @("GPT", "Android Studio", "gpt/", "android-studio/", "gemini/")
    $selectedMode = ""

    if ($null -ne $event -and $event.PSObject.Properties.Name -contains "pull_request") {
        $isPullRequest = $true
        $body = [string]$event.pull_request.body
        $draft = [bool]$event.pull_request.draft
        $baseBranch = [string]$event.pull_request.base.ref
        $headBranch = [string]$event.pull_request.head.ref
    } elseif (-not $isPullRequest) {
        $headBranch = (Invoke-GitLines @("branch", "--show-current") | Select-Object -First 1)
    }

    $changedFiles = Get-ChangedFiles
    Write-SafeHost "Event: $($env:GITHUB_EVENT_NAME); PR metadata checks: $isPullRequest"
    Write-SafeHost "Changed files:"
    $changedFiles | ForEach-Object { Write-SafeHost "  $_" }

    if ($isPullRequest -and
        $headBranch -and
        -not ($branchPrefixes | Where-Object { $headBranch.StartsWith($_, [System.StringComparison]::Ordinal) })) {
        Add-Failure $failures "Branch '$headBranch' does not use an allowed AnotherLife owner prefix: $($branchPrefixes -join ', ')."
    }

    $retiredBranchPrefixes = @($retiredAgentsAndPrefixes | Where-Object { $_ -match "/" })
    if ($isPullRequest -and
        $headBranch -and
        ($retiredBranchPrefixes | Where-Object { $headBranch.StartsWith($_, [System.StringComparison]::Ordinal) })) {
        Add-Failure $failures "Branch '$headBranch' uses a retired AnotherLife ownership prefix."
    }

    if ($isPullRequest -and
        $baseBranch -and
        $baseBranch -ne "main" -and
        $body -notmatch "(?i)(depends on|prerequisite|stacked|base branch)") {
        Add-Failure $failures "Base branch '$baseBranch' is not main and no stacked/dependency declaration was found."
    }

    if ($body) {
        $modePattern = ($primaryModes | ForEach-Object { [regex]::Escape($_) }) -join "|"
        $modeMatches = [regex]::Matches($body, "- \[[xX]\] ($modePattern)")

        if ($modeMatches.Count -ne 1) {
            Add-Failure $failures "Exactly one primary delivery mode must be selected in the PR body."
        } else {
            $selectedMode = $modeMatches[0].Groups[1].Value
        }

        $retiredModeNames = @($retiredAgentsAndPrefixes | Where-Object { $_ -notmatch "/" })
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
    } elseif ($isPullRequest) {
        Add-Failure $failures "Pull request body is unavailable or empty."
    }

    $isA2Branch = $isPullRequest -and
        $headBranch -and
        $headBranch.StartsWith($a2BranchPrefix, [System.StringComparison]::Ordinal)
    if ($isA2Branch -and $selectedMode -cne $a2PrimaryMode) {
        Add-Failure $failures "A2 branch '$headBranch' must select exactly '$a2PrimaryMode'."
    }
    if ($isPullRequest -and
        $selectedMode -ceq $a2PrimaryMode -and
        -not $isA2Branch) {
        Add-Failure $failures "Primary mode '$a2PrimaryMode' requires an '$a2BranchPrefix' branch."
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
    $runtimePrefixes = @(Get-PolicyList "runtime_paths" @("app/", "unity/Assets/AL/Scripts/", "unity/Assets/AL/Tests/"))
    $workflowPrefixes = @(Get-PolicyList "workflow_paths" @(".github/workflows/", ".github/anotherlife-policy.yml", "tools/ci/"))
    $engineeringPrefixes = @($runtimePrefixes + $workflowPrefixes)

    $narrativeChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $narrativePrefixes })
    $terrestrialChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $terrestrialPrefixes })
    $runtimeChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $runtimePrefixes })
    $workflowChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $workflowPrefixes })
    $engineeringChanged = @($changedFiles | Where-Object { Test-AnyPathPrefix $_ $engineeringPrefixes })
    $nonTerrestrialChanged = @($changedFiles | Where-Object { -not (Test-AnyPathPrefix $_ $terrestrialPrefixes) })

    if ($isA2Branch) {
        if ($narrativeChanged.Count -gt 0) {
            Add-Failure $failures "A2 branches cannot change narrative paths."
        }
        if ($runtimeChanged.Count -gt 0) {
            Add-Failure $failures "A2 branches cannot change runtime paths."
        }
        if ($workflowChanged.Count -gt 0) {
            Add-Failure $failures "A2 branches cannot change workflow paths."
        }
        if ($nonTerrestrialChanged.Count -gt 0) {
            Add-Failure $failures "A2 branches may change only configured terrestrial-design source paths. Out-of-bound paths: $($nonTerrestrialChanged -join ', ')."
        }
        if (Test-HasMixedModeJustification $body) {
            Add-Failure $failures "A2 branches cannot use a mixed-mode justification to escape their source-only boundary."
        }
    }

    if ($isPullRequest -and
        ($narrativeChanged -or $terrestrialChanged) -and
        $engineeringChanged -and
        -not (Test-HasMixedModeJustification $body)) {
        Add-Failure $failures "Source-mode and engineering paths are mixed without an explicit mixed-mode justification."
    }

    Write-SafeHost "Narrative paths changed: $($narrativeChanged.Count)"
    Write-SafeHost "Terrestrial design paths changed: $($terrestrialChanged.Count)"
    Write-SafeHost "Engineering/workflow paths changed: $($engineeringChanged.Count)"
    Assert-NoFailures $failures
}

function Invoke-Hygiene {
    $failures = [System.Collections.Generic.List[string]]::new()
    $diffRange = Get-DiffRange

    Write-SafeHost "Running git diff --check against $diffRange"
    $diffCheck = & git diff --check $diffRange 2>&1
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
            Read-StrictUtf8Text $jsonFile | ConvertFrom-Json | Out-Null
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

        $usesMatches = [regex]::Matches(
            $text,
            '(?m)^\s*(?:-\s*)?uses:\s*[''"]?([^''"\s#]+)[''"]?\s*(?:#.*)?$'
        )
        foreach ($usesMatch in $usesMatches) {
            $actionReference = $usesMatch.Groups[1].Value
            if ($actionReference.StartsWith("./", [System.StringComparison]::Ordinal) -or
                $actionReference.StartsWith("docker://", [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $separatorIndex = $actionReference.LastIndexOf("@", [System.StringComparison]::Ordinal)
            $actionRef = if ($separatorIndex -ge 0) {
                $actionReference.Substring($separatorIndex + 1)
            } else {
                ""
            }
            if ($separatorIndex -le 0 -or $actionRef -notmatch "^[0-9a-fA-F]{40}$") {
                Add-Failure $failures "Workflow '$workflow' action '$actionReference' is not pinned to a full 40-hex commit SHA."
            }
        }
    }

    Write-SafeHost "Tracked files checked: $($trackedFiles.Count)"
    Write-SafeHost "Unity meta GUIDs checked: $($guidToFiles.Count)"
    Assert-NoFailures $failures
}

function Invoke-AndroidReleaseApplicability {
    $changedFiles = Get-ChangedFiles
    $releaseSensitivePaths = Get-PolicyList "android_release_sensitive_paths" @(
        "app/",
        "build.gradle.kts",
        "settings.gradle.kts",
        "gradle.properties",
        "gradle/",
        "gradlew",
        "gradlew.bat",
        ".github/workflows/quality-gates.yml",
        ".github/anotherlife-policy.yml",
        "tools/ci/Invoke-AnotherLifeQualityGate.ps1",
        "tools/ci/Test-AnotherLifeQualityGateFixtures.ps1"
    )
    $matchedPaths = @(
        $changedFiles |
            Where-Object { Test-AnyPathPrefix $_ $releaseSensitivePaths } |
            Sort-Object -Unique
    )
    $applicable = $matchedPaths.Count -gt 0
    $applicableText = $applicable.ToString().ToLowerInvariant()

    Write-SafeHost "Android release applicable: $applicableText"
    Write-SafeHost "Release-sensitive changed paths:"
    if ($matchedPaths.Count -eq 0) {
        Write-SafeHost "  (none)"
    } else {
        $matchedPaths | ForEach-Object { Write-SafeHost "  $_" }
    }

    if ($env:GITHUB_OUTPUT) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "applicable=$applicableText"
    }
}

try {
    $supportedModes = @(
        "Classify",
        "Hygiene",
        "AndroidReleaseApplicability"
    )
    if ($supportedModes -notcontains $Mode) {
        throw "Unsupported quality-gate mode '$Mode'."
    }

    switch ($Mode) {
        "Classify" { Invoke-Classify }
        "Hygiene" { Invoke-Hygiene }
        "AndroidReleaseApplicability" { Invoke-AndroidReleaseApplicability }
    }
} catch {
    $safeFailure = ConvertTo-SafeDiagnosticText $_.Exception.Message
    [Console]::Error.WriteLine(
        "AnotherLife quality gate failed: $safeFailure"
    )
    exit 1
}
