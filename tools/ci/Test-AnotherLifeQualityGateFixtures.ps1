param(
    [string] $WorkingRoot = "",
    [ValidateSet(
        "All",
        "DuplicateGuid",
        "TestScene",
        "MissingScene",
        "MalformedJson",
        "Utf8Json",
        "Utf8Event",
        "MutableAction",
        "DiagnosticSanitization",
        "MixedScope",
        "MixedModeRationale",
        "Coordination",
        "RetiredPrefix",
        "A2Convention",
        "A2Authority",
        "EngineeringToolClassification",
        "PolicyAuthority",
        "PushMain",
        "PullRequestRange",
        "InvalidBase",
        "StackedBase",
        "PathChanges",
        "DeletedSharedFile",
        "AndroidReleaseApplicability"
    )]
    [string] $Scenario = "All"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$powerShellExecutable = if ($PSVersionTable.PSEdition -eq "Core") {
    $coreExecutableName = if (
        [System.Environment]::OSVersion.Platform -eq
        [System.PlatformID]::Win32NT
    ) {
        "pwsh.exe"
    } else {
        "pwsh"
    }
    Join-Path $PSHOME $coreExecutableName
} else {
    Join-Path $PSHOME "powershell.exe"
}
if (-not (Test-Path -LiteralPath $powerShellExecutable)) {
    throw "Current PowerShell host executable was not found at '$powerShellExecutable'."
}
if (-not $WorkingRoot) {
    $WorkingRoot = Join-Path ([System.IO.Path]::GetTempPath()) "AnotherLife-QualityGateFixtures-$([Guid]::NewGuid().ToString('N'))"
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string] $FilePath,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [string] $WorkingDirectory = $PWD.Path,
        [hashtable] $Environment = @{},
        [switch] $ExpectFailure
    )

    $previousValues = @{}
    foreach ($key in $Environment.Keys) {
        $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, "Process")
        [Environment]::SetEnvironmentVariable($key, [string]$Environment[$key], "Process")
    }

    Push-Location $WorkingDirectory
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Pop-Location
        foreach ($key in $Environment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $previousValues[$key], "Process")
        }
    }

    $text = ($output | Out-String -Width 4096)
    if ($ExpectFailure) {
        if ($exitCode -eq 0) {
            throw "Expected failure from $FilePath $($Arguments -join ' '), but it succeeded.`n$text"
        }
    } elseif ($exitCode -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')`n$text"
    }

    return $text
}

function Get-FixturePowerShellExecutables {
    $executables = [System.Collections.Generic.List[string]]::new()
    $executables.Add($powerShellExecutable) | Out-Null
    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        $windowsPowerShell = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
        if ((Test-Path -LiteralPath $windowsPowerShell) -and
            -not $windowsPowerShell.Equals($powerShellExecutable, [System.StringComparison]::OrdinalIgnoreCase)) {
            $executables.Add($windowsPowerShell) | Out-Null
        }
    }

    return @($executables)
}

function New-FixtureRepo {
    param([Parameter(Mandatory = $true)][string] $Name)

    $path = Join-Path $WorkingRoot $Name
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    Invoke-Checked git @("init", "-q") $path | Out-Null
    Invoke-Checked git @("config", "user.name", "AnotherLife CI Fixture") $path | Out-Null
    Invoke-Checked git @("config", "user.email", "fixture@example.invalid") $path | Out-Null
    Invoke-Checked git @("config", "core.autocrlf", "false") $path | Out-Null

    New-Item -ItemType Directory -Force -Path (Join-Path $path "tools/ci") | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot "tools/ci/Invoke-AnotherLifeQualityGate.ps1") -Destination (Join-Path $path "tools/ci/Invoke-AnotherLifeQualityGate.ps1")

    Push-Location $path
    try {
        Invoke-Checked git @("add", "tools/ci/Invoke-AnotherLifeQualityGate.ps1") $path | Out-Null
        Invoke-Checked git @("commit", "-q", "-m", "base") $path | Out-Null
    } finally {
        Pop-Location
    }

    return $path
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)][string] $Text,
        [Parameter(Mandatory = $true)][string] $Needle
    )

    if ($Text -notmatch [regex]::Escape($Needle)) {
        throw "Expected output to contain '$Needle'. Actual output:`n$Text"
    }
}

function Assert-NotContains {
    param(
        [Parameter(Mandatory = $true)][string] $Text,
        [Parameter(Mandatory = $true)][string] $Needle
    )

    if ($Text -match [regex]::Escape($Needle)) {
        throw "Expected output not to contain '$Needle'. Actual output:`n$Text"
    }
}

function ConvertTo-MixedSeparators {
    param([Parameter(Mandatory = $true)][string] $Path)

    $characters = $Path.ToCharArray()
    $separatorIndex = 0
    for ($index = 0; $index -lt $characters.Length; $index++) {
        if ($characters[$index] -eq [char]"/" -or
            $characters[$index] -eq [char]"\") {
            $characters[$index] = if (($separatorIndex % 2) -eq 0) {
                [char]"/"
            } else {
                [char]"\"
            }
            $separatorIndex++
        }
    }

    return -join $characters
}

function Invoke-DiagnosticSanitizationProbe {
    param(
        [Parameter(Mandatory = $true)][string] $FixtureRepo,
        [Parameter(Mandatory = $true)][string] $Mode,
        [Parameter(Mandatory = $true)][string] $BaseRef,
        [hashtable] $Environment = @{}
    )

    return Invoke-Checked $powerShellExecutable @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1",
        "-Mode",
        $Mode,
        "-BaseRef",
        $BaseRef
    ) $FixtureRepo $Environment -ExpectFailure
}

function Write-PullRequestEvent {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $BaseSha,
        [Parameter(Mandatory = $true)][string] $HeadSha,
        [Parameter(Mandatory = $true)][string] $Body,
        [string] $BaseBranch = "main",
        [string] $HeadBranch = "codex/quality-gate-fixture"
    )

    $event = @{
        pull_request = @{
            draft = $false
            body = $Body
            base = @{
                ref = $BaseBranch
                sha = $BaseSha
            }
            head = @{
                ref = $HeadBranch
                sha = $HeadSha
            }
        }
    }
    $event |
        ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Add-FixtureCommit {
    param(
        [Parameter(Mandatory = $true)][string] $FixtureRepo,
        [Parameter(Mandatory = $true)][string] $Message
    )

    Invoke-Checked git @("add", "-A") $FixtureRepo | Out-Null
    Invoke-Checked git @("commit", "-q", "-m", $Message) $FixtureRepo | Out-Null
    return (
        Invoke-Checked git @("rev-parse", "HEAD") $FixtureRepo
    ).Trim()
}

function Invoke-PullRequestGate {
    param(
        [Parameter(Mandatory = $true)][string] $FixtureRepo,
        [Parameter(Mandatory = $true)][string] $EventPath,
        [ValidateSet("Classify", "Hygiene", "AndroidReleaseApplicability")]
        [string] $Mode = "Classify",
        [string] $GitHubOutput = "",
        [switch] $ExpectFailure
    )

    $environment = @{
        GITHUB_ACTIONS = "true"
        GITHUB_EVENT_NAME = "pull_request"
        GITHUB_EVENT_PATH = $EventPath
        GITHUB_BASE_REF = ""
        GITHUB_HEAD_REF = ""
        GITHUB_OUTPUT = $GitHubOutput
    }
    $invoke = @{
        FilePath = $powerShellExecutable
        Arguments = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1",
            "-Mode",
            $Mode
        )
        WorkingDirectory = $FixtureRepo
        Environment = $environment
    }
    if ($ExpectFailure) {
        return Invoke-Checked @invoke -ExpectFailure
    }

    return Invoke-Checked @invoke
}

function Invoke-HygieneFailureFixture {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][scriptblock] $Arrange,
        [Parameter(Mandatory = $true)][string] $ExpectedMessage
    )

    $fixtureRepo = New-FixtureRepo $Name
    & $Arrange $fixtureRepo

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        $output = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Hygiene", "-BaseRef", "HEAD") $fixtureRepo -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $output $ExpectedMessage
}

function Test-DuplicateGuidFixture {
    Invoke-HygieneFailureFixture "duplicate-guid" {
        param($fixtureRepo)
        New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Assets") | Out-Null
        $guid = "0123456789abcdef0123456789abcdef"
        Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Assets/A.meta") -Value "guid: $guid"
        Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Assets/B.meta") -Value "guid: $guid"
    } "Duplicate Unity meta GUID"
}

function Test-TestSceneFixture {
    Invoke-HygieneFailureFixture "test-scene" {
        param($fixtureRepo)
        New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Assets") | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/ProjectSettings") | Out-Null
        Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Assets/Test.unity") -Value "%YAML 1.1"
        Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/ProjectSettings/EditorBuildSettings.asset") -Value @"
EditorBuildSettings:
  m_Scenes:
  - enabled: 1
    path: Assets/Test.unity
"@
    } "Assets/Test.unity must not be enabled"
}

function Test-MissingSceneFixture {
    Invoke-HygieneFailureFixture "missing-scene" {
        param($fixtureRepo)
        New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/ProjectSettings") | Out-Null
        Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/ProjectSettings/EditorBuildSettings.asset") -Value @"
EditorBuildSettings:
  m_Scenes:
  - enabled: 1
    path: Assets/Missing.unity
"@
    } "Enabled Build Settings scene is missing"
}

function Test-MalformedJsonFixture {
    Invoke-HygieneFailureFixture "malformed-json" {
        param($fixtureRepo)
        Set-Content -LiteralPath (Join-Path $fixtureRepo "bad.json") -Value "{ malformed"
    } "Malformed JSON file"
}

function Test-Utf8JsonFixture {
    $fixtureRepo = New-FixtureRepo "utf8-json"
    $jsonPath = Join-Path $fixtureRepo "multibyte.json"
    $koreanValue = "equipment_unicode_" + [char]0xD55C
    $json = '{"value":"' + $koreanValue + '"}'
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    [System.IO.File]::WriteAllText($jsonPath, $json, $strictUtf8)

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", "multibyte.json") $fixtureRepo | Out-Null
        foreach ($executable in (Get-FixturePowerShellExecutables)) {
            $output = Invoke-Checked $executable @(
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1",
                "-Mode",
                "Hygiene",
                "-BaseRef",
                "HEAD"
            ) $fixtureRepo
            Assert-Contains $output "Tracked files checked: 2"
        }
    } finally {
        Pop-Location
    }
}

function Test-Utf8EventFixture {
    $fixtureRepo = New-FixtureRepo "utf8-event"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Governance.md") -Value "# Governance fixture" -NoNewline
    Invoke-Checked git @("add", "unity/Docs/Governance.md") $fixtureRepo | Out-Null

    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    $koreanSuffix = [string]([char]0xD55C)
    $body = "- [x] Codex coordination/review`n`nRefs #155`n`n## Shared-file lock`n`nNone.`n`n$koreanSuffix"
    $eventPath = Join-Path $fixtureRepo "event.json"
    Write-PullRequestEvent $eventPath $base $base $body "main" "codex/coordination-utf8-event"

    foreach ($executable in (Get-FixturePowerShellExecutables)) {
        $output = Invoke-Checked $executable @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1",
            "-Mode",
            "Classify",
            "-BaseRef",
            "HEAD"
        ) $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "codex/coordination-utf8-event"
        }
        Assert-Contains $output "Event: pull_request; PR metadata checks: True"
        Assert-Contains $output "Narrative paths changed: 0"
    }
}

function Test-MutableActionFixture {
    $fixtures = @(
        [pscustomobject]@{
            Name = "mutable-action-major"
            Reference = "actions/checkout@v4"
        },
        [pscustomobject]@{
            Name = "mutable-action-branch"
            Reference = "actions/checkout@main"
        },
        [pscustomobject]@{
            Name = "mutable-action-short-sha"
            Reference = "actions/checkout@34e1148"
        }
    )

    foreach ($fixture in $fixtures) {
        $reference = $fixture.Reference
        Invoke-HygieneFailureFixture $fixture.Name {
            param($fixtureRepo)
            New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github/workflows") | Out-Null
            Set-Content -LiteralPath (Join-Path $fixtureRepo ".github/workflows/bad.yml") -Value @"
name: Bad
on: [pull_request]
jobs:
  bad:
    runs-on: windows-latest
    steps:
      - uses: $reference
"@
        } "is not pinned to a full 40-hex commit SHA"
    }
}

function Test-DiagnosticSanitizationFixture {
    $fixtureRepo = New-FixtureRepo "diagnostic-sanitization"
    $fakeToken = "github_pat_$("A" * 48)"

    $tokenOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo "Hygiene" $fakeToken
    Assert-Contains $tokenOutput "Running git diff --check against <redacted-token>...HEAD"
    Assert-Contains $tokenOutput "::error::git diff --check failed:"
    Assert-Contains $tokenOutput "AnotherLife quality gate failed: 1 quality gate failure(s)."
    Assert-Contains $tokenOutput "<redacted-token>"
    Assert-NotContains $tokenOutput $fakeToken

    $invalidModeOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo $fakeToken "HEAD"
    Assert-Contains $invalidModeOutput "Unsupported quality-gate mode '<redacted-token>'."
    Assert-NotContains $invalidModeOutput $fakeToken

    $authorizationSecret = "opaque-secret-value-1234567890"
    $quotedAuthorization = "`"Authorization`": `"token $authorizationSecret`""
    $authorizationOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo "Hygiene" $quotedAuthorization
    Assert-Contains $authorizationOutput "<redacted-token>"
    Assert-NotContains $authorizationOutput $authorizationSecret

    $nativeRoot = $fixtureRepo
    $forwardRoot = $fixtureRepo.Replace("\", "/")
    $reverseRoot = $forwardRoot.Replace("/", "\")
    $mixedRoot = ConvertTo-MixedSeparators $fixtureRepo
    $rootVariants = @(
        $nativeRoot,
        $forwardRoot,
        $reverseRoot,
        $mixedRoot
    ) | Sort-Object -Unique

    foreach ($rootVariant in $rootVariants) {
        # Classify fails while discovering changed files, so this exercises
        # the top-level catch rather than only Add-Failure.
        $rootOutput = Invoke-DiagnosticSanitizationProbe `
            $fixtureRepo "Classify" $rootVariant
        Assert-Contains $rootOutput "<repo>"
        foreach ($unredactedVariant in $rootVariants) {
            Assert-NotContains $rootOutput $unredactedVariant
        }
    }

    $siblingRoots = @(
        "$fixtureRepo-archive",
        "$fixtureRepo+archive",
        "$fixtureRepo archive"
    )
    foreach ($siblingRoot in $siblingRoots) {
        $siblingOutput = Invoke-DiagnosticSanitizationProbe `
            $fixtureRepo "Classify" $siblingRoot
        Assert-Contains $siblingOutput $siblingRoot
        Assert-NotContains $siblingOutput "<repo>"
    }

    $sentenceMode = "Failure at ${fixtureRepo}."
    $sentenceOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo $sentenceMode "HEAD"
    Assert-Contains $sentenceOutput "Failure at <repo>."
    Assert-NotContains $sentenceOutput $fixtureRepo

    $unixRoot = "/home/runner/work/AnotherLife/AnotherLife"
    $unixReverseRoot = $unixRoot.Replace("/", "\")
    $unixOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo "Classify" $unixReverseRoot @{
            GITHUB_WORKSPACE = $unixRoot
        }
    Assert-Contains $unixOutput "<repo>"
    Assert-NotContains $unixOutput $unixRoot
    Assert-NotContains $unixOutput $unixReverseRoot

    $unixCaseDistinctRoot = "/home/runner/work/anotherlife/anotherlife"
    $unixCaseOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo "Classify" $unixCaseDistinctRoot @{
            GITHUB_WORKSPACE = $unixRoot
        }
    Assert-Contains $unixCaseOutput $unixCaseDistinctRoot
    Assert-NotContains $unixCaseOutput "<repo>"
}

function Test-MixedScopeFixture {
    $fixtureRepo = New-FixtureRepo "mixed-scope"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs/Terrestrials") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Assets/AL/Scripts") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Terrestrials/Design.md") -Value "# Fixture"
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Assets/AL/Scripts/RuntimeFixture.cs") -Value "public class RuntimeFixture {}"

    $eventPath = Join-Path $fixtureRepo "event.json"
    Set-Content -LiteralPath $eventPath -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex engineering\n\nA mixed-mode PR requires a written Codex coordination/review justification explaining why separate PRs are impractical.\n\nThis is not a mixed-mode PR.\n\nFixes #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "codex/quality-gate-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        $output = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "codex/quality-gate-fixture"
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $output "Terrestrial design paths changed: 1"
    Assert-Contains $output "Engineering/workflow paths changed: 1"
    Assert-Contains $output "Source-mode and engineering paths are mixed"
}

function Invoke-MixedModeRationaleFixture {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $RationaleBlock,
        [switch] $ExpectFailure
    )

    $fixtureRepo = New-FixtureRepo $Name
    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs/Terrestrials") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Assets/AL/Scripts") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Terrestrials/Design.md") -Value "# Fixture" -NoNewline
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Assets/AL/Scripts/RuntimeFixture.cs") -Value "public class RuntimeFixture {}" -NoNewline
    $head = Add-FixtureCommit $fixtureRepo "mixed-mode rationale fixture"

    $body = "- [x] Codex engineering`n`nFixes #155`n`n$RationaleBlock`n`n## Shared-file lock`n`nNone."
    $eventPath = Join-Path $fixtureRepo "event.json"
    Write-PullRequestEvent $eventPath $base $head $body
    $output = if ($ExpectFailure) {
        Invoke-PullRequestGate $fixtureRepo $eventPath -ExpectFailure
    } else {
        Invoke-PullRequestGate $fixtureRepo $eventPath
    }

    if ($ExpectFailure) {
        Assert-Contains $output "Source-mode and engineering paths are mixed without an explicit mixed-mode justification."
    } else {
        Assert-Contains $output "Terrestrial design paths changed: 1"
        Assert-Contains $output "Engineering/workflow paths changed: 1"
    }
}

function Test-MixedModeRationaleFixture {
    Invoke-MixedModeRationaleFixture `
        "mixed-mode-valid-heading" `
        "## Mixed-mode justification`n`nThis is a Codex engineering delivery that necessarily includes generated terrestrial asset paths and runtime validation. Separate PRs are impractical because one reproducible builder must generate and validate the exact approved source outputs."

    Invoke-MixedModeRationaleFixture `
        "mixed-mode-valid-label" `
        "Mixed-mode justification: This exact engineering fixture must validate its generated terrestrial source and runtime consumer together."

    Invoke-MixedModeRationaleFixture `
        "mixed-mode-bare-heading" `
        "## Mixed-mode justification`n`n## Validation`n`nNo rationale was supplied before this next heading." `
        -ExpectFailure

    Invoke-MixedModeRationaleFixture `
        "mixed-mode-empty-label" `
        "Mixed-mode justification:`n`nThis following line cannot backfill an empty same-line label." `
        -ExpectFailure
}

function Test-CoordinationFixture {
    $fixtureRepo = New-FixtureRepo "coordination"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Governance.md") -Value "# Governance fixture"
    $eventPath = Join-Path $fixtureRepo "event.json"
    Set-Content -LiteralPath $eventPath -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex coordination/review\n\nRefs #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "codex/coordination-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "codex/coordination-fixture"
        } | Out-Null
    } finally {
        Pop-Location
    }
}

function Test-PushMainFixture {
    $fixtureRepo = New-FixtureRepo "push-main"

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("branch", "-M", "main") $fixtureRepo | Out-Null
        $before = (
            Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
        ).Trim()

        $runtimePath = Join-Path $fixtureRepo "unity/Assets/AL/Scripts"
        New-Item -ItemType Directory -Force -Path $runtimePath | Out-Null
        Set-Content -LiteralPath (Join-Path $runtimePath "PushFixture.cs") -Value "public sealed class PushFixture {}" -NoNewline
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        Invoke-Checked git @("commit", "-q", "-m", "push change") $fixtureRepo | Out-Null
        $after = (
            Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
        ).Trim()

        $eventPath = Join-Path $fixtureRepo "event.json"
        Set-Content -LiteralPath $eventPath -Value @"
{
  "ref": "refs/heads/main",
  "before": "$before",
  "after": "$after"
}
"@

        $output = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify") $fixtureRepo @{
            GITHUB_ACTIONS = "true"
            GITHUB_EVENT_NAME = "push"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = ""
            GITHUB_HEAD_REF = ""
        }
        $hygieneOutput = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Hygiene") $fixtureRepo @{
            GITHUB_ACTIONS = "true"
            GITHUB_EVENT_NAME = "push"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = ""
            GITHUB_HEAD_REF = ""
        }

        $mismatchEventPath = Join-Path $fixtureRepo "event-mismatch.json"
        Set-Content -LiteralPath $mismatchEventPath -Value @"
{
  "ref": "refs/heads/main",
  "before": "$before",
  "after": "$before"
}
"@
        $mismatchOutput = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify") $fixtureRepo @{
            GITHUB_ACTIONS = "true"
            GITHUB_EVENT_NAME = "push"
            GITHUB_EVENT_PATH = $mismatchEventPath
            GITHUB_BASE_REF = ""
            GITHUB_HEAD_REF = ""
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $output "Event: push; PR metadata checks: False"
    Assert-Contains $output "Engineering/workflow paths changed: 1"
    Assert-Contains $hygieneOutput "Running git diff --check against $before..$after"
    Assert-Contains $mismatchOutput "does not match event after SHA"
}

function Test-RetiredPrefixFixture {
    $fixtureRepo = New-FixtureRepo "retired-terrestrial"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github") | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot ".github/anotherlife-policy.yml") -Destination (Join-Path $fixtureRepo ".github/anotherlife-policy.yml")
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Retired.md") -Value "# Retired fixture"
    $eventPath = Join-Path $fixtureRepo "event.json"
    Set-Content -LiteralPath $eventPath -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex coordination/review\n\nRefs #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "codex/terrestrial-retired-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        $output = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "codex/terrestrial-retired-fixture"
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $output "retired AnotherLife ownership prefix"
}

function Test-A2ConventionFixture {
    $fixtureRepo = New-FixtureRepo "a2-convention"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github") | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot ".github/anotherlife-policy.yml") -Destination (Join-Path $fixtureRepo ".github/anotherlife-policy.yml")
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs/Terrestrials") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Terrestrials/A2.md") -Value "# A2 fixture"
    $eventPath = Join-Path $fixtureRepo "event.json"
    Set-Content -LiteralPath $eventPath -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] A2 terrestrial design\n\nA mixed-mode PR requires a written Codex coordination/review justification explaining why separate PRs are impractical.\n\nThis is not a mixed-mode PR.\n\nRefs #259\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "a2/terrestrial-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".github/anotherlife-policy.yml", "tools/ci/Invoke-AnotherLifeQualityGate.ps1") $fixtureRepo | Out-Null
        Invoke-Checked git @("commit", "-q", "-m", "policy") $fixtureRepo | Out-Null
        Invoke-Checked git @("add", "unity/Docs/Terrestrials/A2.md") $fixtureRepo | Out-Null
        $output = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "a2/terrestrial-fixture"
        }
    } finally {
        Pop-Location
    }

    Assert-Contains $output "Terrestrial design paths changed: 1"
}

function Invoke-A2AuthorityFailureFixture {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $ChangedPath,
        [Parameter(Mandatory = $true)][string] $Body,
        [Parameter(Mandatory = $true)][string] $HeadBranch,
        [Parameter(Mandatory = $true)][string[]] $ExpectedMessages
    )

    $fixtureRepo = New-FixtureRepo $Name
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github") | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot ".github/anotherlife-policy.yml") -Destination (Join-Path $fixtureRepo ".github/anotherlife-policy.yml")
    Invoke-Checked git @("add", ".github/anotherlife-policy.yml") $fixtureRepo | Out-Null
    Invoke-Checked git @("commit", "-q", "-m", "policy") $fixtureRepo | Out-Null

    $absoluteChangedPath = Join-Path $fixtureRepo $ChangedPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $absoluteChangedPath) | Out-Null
    Set-Content -LiteralPath $absoluteChangedPath -Value "# A2 authority fixture" -NoNewline
    Invoke-Checked git @("add", $ChangedPath) $fixtureRepo | Out-Null

    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    $eventPath = Join-Path $fixtureRepo "event.json"
    Write-PullRequestEvent $eventPath $base $base $Body "main" $HeadBranch

    $output = Invoke-Checked $powerShellExecutable @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1",
        "-Mode",
        "Classify",
        "-BaseRef",
        "HEAD"
    ) $fixtureRepo @{
        GITHUB_ACTIONS = ""
        GITHUB_EVENT_NAME = "pull_request"
        GITHUB_EVENT_PATH = $eventPath
        GITHUB_BASE_REF = "main"
        GITHUB_HEAD_REF = $HeadBranch
    } -ExpectFailure

    foreach ($expectedMessage in $ExpectedMessages) {
        Assert-Contains $output $expectedMessage
    }
}

function Test-A2AuthorityFixture {
    $a2Body = "- [x] A2 terrestrial design`n`nRefs #259`n`n## Shared-file lock`n`nNone."

    Invoke-A2AuthorityFailureFixture `
        "a2-wrong-mode" `
        "unity/Docs/Terrestrials/WrongMode.md" `
        "- [x] Codex engineering`n`nRefs #259`n`n## Shared-file lock`n`nNone." `
        "a2/terrestrial-wrong-mode" `
        @("A2 branch 'a2/terrestrial-wrong-mode' must select exactly 'A2 terrestrial design'.")

    Invoke-A2AuthorityFailureFixture `
        "codex-a2-mode" `
        "unity/Docs/Terrestrials/WrongBranch.md" `
        $a2Body `
        "codex/wrong-a2-mode" `
        @("Primary mode 'A2 terrestrial design' requires an 'a2/terrestrial-' branch.")

    Invoke-A2AuthorityFailureFixture `
        "a2-narrative-path" `
        "unity/Docs/Narrative/A2Narrative.md" `
        $a2Body `
        "a2/terrestrial-narrative-path" `
        @("A2 branches cannot change narrative paths.", "A2 branches may change only configured terrestrial-design source paths.")

    Invoke-A2AuthorityFailureFixture `
        "a2-runtime-path" `
        "unity/Assets/AL/Scripts/A2Runtime.cs" `
        $a2Body `
        "a2/terrestrial-runtime-path" `
        @("A2 branches cannot change runtime paths.", "A2 branches may change only configured terrestrial-design source paths.")

    Invoke-A2AuthorityFailureFixture `
        "a2-workflow-path" `
        ".github/workflows/a2-workflow.yml" `
        $a2Body `
        "a2/terrestrial-workflow-path" `
        @("A2 branches cannot change workflow paths.", "A2 branches may change only configured terrestrial-design source paths.")

    Invoke-A2AuthorityFailureFixture `
        "a2-engineering-tool-path" `
        "tools/game-data/a2-tool.py" `
        $a2Body `
        "a2/terrestrial-engineering-tool-path" `
        @("A2 branches cannot change engineering tool paths.", "A2 branches may change only configured terrestrial-design source paths.")

    Invoke-A2AuthorityFailureFixture `
        "a2-unclassified-path" `
        "unity/Docs/A2OutsideTerrestrialSource.md" `
        $a2Body `
        "a2/terrestrial-unclassified-path" `
        @("A2 branches may change only configured terrestrial-design source paths.")

    Invoke-A2AuthorityFailureFixture `
        "a2-mixed-mode-escape" `
        "unity/Docs/Terrestrials/MixedModeEscape.md" `
        "$a2Body`n`nMixed-mode exception: separate PRs are impractical for this change." `
        "a2/terrestrial-mixed-mode-escape" `
        @("A2 branches cannot use a mixed-mode justification to escape their source-only boundary.")
}

function Test-EngineeringToolClassificationFixture {
    $fixtureRepo = New-FixtureRepo "engineering-tool-classification"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github") | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot ".github/anotherlife-policy.yml") -Destination (Join-Path $fixtureRepo ".github/anotherlife-policy.yml")
    Invoke-Checked git @("add", ".github/anotherlife-policy.yml") $fixtureRepo | Out-Null
    Invoke-Checked git @("commit", "-q", "-m", "policy") $fixtureRepo | Out-Null

    $toolDirectory = Join-Path $fixtureRepo "tools/game-data"
    New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
    Set-Content -LiteralPath (Join-Path $toolDirectory "validator.py") -Value "print('fixture')" -NoNewline
    Invoke-Checked git @("add", "tools/game-data/validator.py") $fixtureRepo | Out-Null

    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    $eventPath = Join-Path $fixtureRepo "event.json"
    $body = "- [x] Codex engineering`n`nRefs #155`n`n## Shared-file lock`n`nNone."
    Write-PullRequestEvent $eventPath $base $base $body "main" "codex/engineering-tool-classification"

    $output = Invoke-Checked $powerShellExecutable @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1",
        "-Mode",
        "Classify",
        "-BaseRef",
        "HEAD"
    ) $fixtureRepo @{
        GITHUB_ACTIONS = ""
        GITHUB_EVENT_NAME = "pull_request"
        GITHUB_EVENT_PATH = $eventPath
        GITHUB_BASE_REF = "main"
        GITHUB_HEAD_REF = "codex/engineering-tool-classification"
    }

    Assert-Contains $output "Engineering tool paths changed: 1"
    Assert-Contains $output "Engineering/workflow paths changed: 1"
}

function Test-PolicyAuthorityFixture {
    $fixtureRepo = New-FixtureRepo "policy-authority"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "Runtime") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo ".github/anotherlife-policy.yml") -Value @"
branch_prefixes:
  - quality/
primary_modes:
  - Codex engineering
retired_agents_and_prefixes:
  - GPT
shared_files:
  - Shared.cs
narrative_source_paths:
  - Narrative/
terrestrial_design_paths:
  - Terrestrial/
runtime_paths:
  - Runtime/
workflow_paths:
  - .github/
forbidden_tracked_path_patterns:
  - "^Forbidden/"
production_test_scene_path: unity/Assets/Test.unity
"@
    Set-Content -LiteralPath (Join-Path $fixtureRepo "Runtime/Fixture.cs") -Value "public class Fixture {}"

    $eventPath = Join-Path $fixtureRepo "event.json"
    Set-Content -LiteralPath $eventPath -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex engineering\n\nRefs #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "quality/policy-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        $output = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "quality/policy-fixture"
        }
    } finally {
        Pop-Location
    }

    Assert-Contains $output "Engineering/workflow paths changed: 2"
}

function Test-PullRequestRangeFixture {
    $fixtureRepo = New-FixtureRepo "pull-request-range"
    Invoke-Checked git @("branch", "-M", "main") $fixtureRepo | Out-Null
    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()

    $runtimePath = Join-Path $fixtureRepo "app/src/main"
    New-Item -ItemType Directory -Force -Path $runtimePath | Out-Null
    Set-Content -LiteralPath (Join-Path $runtimePath "PullRequestRangeFixture.kt") -Value "class PullRequestRangeFixture" -NoNewline
    $head = Add-FixtureCommit $fixtureRepo "pull request range head"

    Invoke-Checked git @("update-ref", "refs/remotes/origin/main", $head) $fixtureRepo | Out-Null

    $body = "- [x] Codex engineering`n`nRefs #155`n`n## Shared-file lock`n`nNone."
    $eventPath = Join-Path $fixtureRepo "event.json"
    Write-PullRequestEvent $eventPath $base $head $body
    $output = Invoke-PullRequestGate $fixtureRepo $eventPath

    Assert-Contains $output "app/src/main/PullRequestRangeFixture.kt"
    Assert-Contains $output "Engineering/workflow paths changed: 1"

    Invoke-Checked git @("checkout", "--detach", $base) $fixtureRepo | Out-Null
    try {
        $mismatchOutput = Invoke-PullRequestGate $fixtureRepo $eventPath -ExpectFailure
    } finally {
        Invoke-Checked git @("checkout", "--detach", $head) $fixtureRepo | Out-Null
    }
    Assert-Contains $mismatchOutput "does not match event head SHA"

    $zeroEventPath = Join-Path $fixtureRepo "event-zero-base.json"
    Write-PullRequestEvent $zeroEventPath ("0" * 40) $head $body
    $zeroOutput = Invoke-PullRequestGate $fixtureRepo $zeroEventPath -ExpectFailure
    Assert-Contains $zeroOutput "base/head commit SHAs are invalid"

    $malformedEventPath = Join-Path $fixtureRepo "event-malformed-head.json"
    Write-PullRequestEvent $malformedEventPath $base "not-a-commit-sha" $body
    $malformedOutput = Invoke-PullRequestGate $fixtureRepo $malformedEventPath -ExpectFailure
    Assert-Contains $malformedOutput "base/head commit SHAs are invalid"

    $missingEventPath = Join-Path $fixtureRepo "event-missing-base.json"
    Write-PullRequestEvent $missingEventPath ("1" * 40) $head $body
    $missingOutput = Invoke-PullRequestGate $fixtureRepo $missingEventPath -ExpectFailure
    Assert-Contains $missingOutput ("1" * 40)
    Assert-Contains $missingOutput "not an available"
}

function Test-InvalidBaseFixture {
    $fixtureRepo = New-FixtureRepo "invalid-base"
    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    $runtimePath = Join-Path $fixtureRepo "app/src/main"
    New-Item -ItemType Directory -Force -Path $runtimePath | Out-Null
    Set-Content -LiteralPath (Join-Path $runtimePath "InvalidBaseFixture.kt") -Value "class InvalidBaseFixture" -NoNewline
    $head = Add-FixtureCommit $fixtureRepo "invalid base head"

    $eventPath = Join-Path $fixtureRepo "event.json"
    $body = "- [x] Codex engineering`n`nRefs #155`n`n## Shared-file lock`n`nNone."
    Write-PullRequestEvent $eventPath $base $head $body "feature/unapproved-base"
    $output = Invoke-PullRequestGate $fixtureRepo $eventPath -ExpectFailure

    Assert-Contains $output "Base branch 'feature/unapproved-base' is not main and no stacked/dependency declaration was found."
}

function Test-StackedBaseFixture {
    $fixtureRepo = New-FixtureRepo "stacked-base"
    $base = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    $runtimePath = Join-Path $fixtureRepo "app/src/main"
    New-Item -ItemType Directory -Force -Path $runtimePath | Out-Null
    Set-Content -LiteralPath (Join-Path $runtimePath "StackedBaseFixture.kt") -Value "class StackedBaseFixture" -NoNewline
    $head = Add-FixtureCommit $fixtureRepo "stacked base head"

    $eventPath = Join-Path $fixtureRepo "event.json"
    $body = "- [x] Codex engineering`n`nRefs #155`n`nDepends on #154.`n`n## Shared-file lock`n`nNone."
    Write-PullRequestEvent $eventPath $base $head $body "feature/declared-stack"
    $output = Invoke-PullRequestGate $fixtureRepo $eventPath

    Assert-Contains $output "Engineering/workflow paths changed: 1"
}

function Test-PathChangesFixture {
    $fixtureRepo = New-FixtureRepo "path-changes"
    $narrativePath = Join-Path $fixtureRepo "unity/Docs/Narrative"
    $terrestrialPath = Join-Path $fixtureRepo "unity/Docs/Terrestrials"
    New-Item -ItemType Directory -Force -Path $narrativePath | Out-Null
    New-Item -ItemType Directory -Force -Path $terrestrialPath | Out-Null
    Set-Content -LiteralPath (Join-Path $narrativePath "Deleted.md") -Value "# Delete me" -NoNewline
    Set-Content -LiteralPath (Join-Path $terrestrialPath "Moved.cs") -Value "public class MovedFixture {}" -NoNewline
    $base = Add-FixtureCommit $fixtureRepo "path changes base"

    $androidPath = Join-Path $fixtureRepo "app/src/main"
    $runtimePath = Join-Path $fixtureRepo "unity/Assets/AL/Scripts"
    New-Item -ItemType Directory -Force -Path $androidPath | Out-Null
    New-Item -ItemType Directory -Force -Path $runtimePath | Out-Null
    Set-Content -LiteralPath (Join-Path $androidPath "Added.kt") -Value "class AddedFixture" -NoNewline
    Remove-Item -LiteralPath (Join-Path $narrativePath "Deleted.md")
    Invoke-Checked git @(
        "mv",
        "unity/Docs/Terrestrials/Moved.cs",
        "unity/Assets/AL/Scripts/Moved.cs"
    ) $fixtureRepo | Out-Null
    $head = Add-FixtureCommit $fixtureRepo "path changes head"

    $eventPath = Join-Path $fixtureRepo "event.json"
    $body = "- [x] Codex engineering`n`nRefs #155`n`nMixed-mode justification: separate PRs are impractical for this exact path-classification proof.`n`n## Shared-file lock`n`nNone."
    Write-PullRequestEvent $eventPath $base $head $body
    $output = Invoke-PullRequestGate $fixtureRepo $eventPath

    Assert-Contains $output "app/src/main/Added.kt"
    Assert-Contains $output "unity/Docs/Narrative/Deleted.md"
    Assert-Contains $output "unity/Docs/Terrestrials/Moved.cs"
    Assert-Contains $output "unity/Assets/AL/Scripts/Moved.cs"
    Assert-Contains $output "Narrative paths changed: 1"
    Assert-Contains $output "Terrestrial design paths changed: 1"
    Assert-Contains $output "Engineering/workflow paths changed: 2"
}

function Test-DeletedSharedFileFixture {
    $fixtureRepo = New-FixtureRepo "deleted-shared-file"
    $sharedDirectory = Join-Path $fixtureRepo "unity/Assets/AL/Scripts/Core"
    New-Item -ItemType Directory -Force -Path $sharedDirectory | Out-Null
    $sharedPath = Join-Path $sharedDirectory "Bootloader.cs"
    Set-Content -LiteralPath $sharedPath -Value "public class Bootloader {}" -NoNewline
    $base = Add-FixtureCommit $fixtureRepo "shared file base"

    Remove-Item -LiteralPath $sharedPath
    $head = Add-FixtureCommit $fixtureRepo "delete shared file"
    $eventPath = Join-Path $fixtureRepo "event.json"
    $body = "- [x] Codex engineering`n`nRefs #155`n`n## Shared-file lock`n`nNone."
    Write-PullRequestEvent $eventPath $base $head $body
    $output = Invoke-PullRequestGate $fixtureRepo $eventPath -ExpectFailure

    Assert-Contains $output "Shared file 'unity/Assets/AL/Scripts/Core/Bootloader.cs' changed but was not declared in the PR body."
}

function Test-AndroidReleaseApplicabilityFixture {
    $fixtureRepo = New-FixtureRepo "android-release-applicability"
    $body = "- [x] Codex engineering`n`nRefs #155`n`n## Shared-file lock`n`nNone."

    $addBase = (
        Invoke-Checked git @("rev-parse", "HEAD") $fixtureRepo
    ).Trim()
    $androidDirectory = Join-Path $fixtureRepo "app/src/main"
    New-Item -ItemType Directory -Force -Path $androidDirectory | Out-Null
    $androidFile = Join-Path $androidDirectory "ReleaseFixture.kt"
    Set-Content -LiteralPath $androidFile -Value "class ReleaseFixture" -NoNewline
    $addHead = Add-FixtureCommit $fixtureRepo "android add"
    $addEvent = Join-Path $WorkingRoot "android-release-add-event.json"
    $addOutputPath = Join-Path $WorkingRoot "android-release-add-output.txt"
    Write-PullRequestEvent $addEvent $addBase $addHead $body
    $addOutput = Invoke-PullRequestGate $fixtureRepo $addEvent -Mode AndroidReleaseApplicability -GitHubOutput $addOutputPath
    Assert-Contains $addOutput "Android release applicable: true"
    Assert-Contains (Get-Content -Raw -LiteralPath $addOutputPath) "applicable=true"

    $deleteBase = $addHead
    Remove-Item -LiteralPath $androidFile
    $deleteHead = Add-FixtureCommit $fixtureRepo "android delete"
    $deleteEvent = Join-Path $WorkingRoot "android-release-delete-event.json"
    $deleteOutputPath = Join-Path $WorkingRoot "android-release-delete-output.txt"
    Write-PullRequestEvent $deleteEvent $deleteBase $deleteHead $body
    $deleteOutput = Invoke-PullRequestGate $fixtureRepo $deleteEvent -Mode AndroidReleaseApplicability -GitHubOutput $deleteOutputPath
    Assert-Contains $deleteOutput "Android release applicable: true"
    Assert-Contains $deleteOutput "app/src/main/ReleaseFixture.kt"
    Assert-Contains (Get-Content -Raw -LiteralPath $deleteOutputPath) "applicable=true"

    $renameSource = Join-Path $androidDirectory "Renamed.kt"
    Set-Content -LiteralPath $renameSource -Value "class RenamedFixture" -NoNewline
    $renameBase = Add-FixtureCommit $fixtureRepo "android rename base"
    $documentationDirectory = Join-Path $fixtureRepo "unity/Docs"
    New-Item -ItemType Directory -Force -Path $documentationDirectory | Out-Null
    Invoke-Checked git @(
        "mv",
        "app/src/main/Renamed.kt",
        "unity/Docs/Renamed.md"
    ) $fixtureRepo | Out-Null
    $renameHead = Add-FixtureCommit $fixtureRepo "android rename to documentation"
    $renameEvent = Join-Path $WorkingRoot "android-release-rename-event.json"
    $renameOutputPath = Join-Path $WorkingRoot "android-release-rename-output.txt"
    Write-PullRequestEvent $renameEvent $renameBase $renameHead $body
    $renameOutput = Invoke-PullRequestGate $fixtureRepo $renameEvent -Mode AndroidReleaseApplicability -GitHubOutput $renameOutputPath
    Assert-Contains $renameOutput "Android release applicable: true"
    Assert-Contains $renameOutput "app/src/main/Renamed.kt"
    Assert-Contains (Get-Content -Raw -LiteralPath $renameOutputPath) "applicable=true"

    $documentationBase = $renameHead
    Set-Content -LiteralPath (Join-Path $documentationDirectory "Governance.md") -Value "# Governance only" -NoNewline
    $documentationHead = Add-FixtureCommit $fixtureRepo "governance documentation only"
    $documentationEvent = Join-Path $WorkingRoot "android-release-documentation-event.json"
    $documentationOutputPath = Join-Path $WorkingRoot "android-release-documentation-output.txt"
    Write-PullRequestEvent $documentationEvent $documentationBase $documentationHead $body
    $documentationOutput = Invoke-PullRequestGate $fixtureRepo $documentationEvent -Mode AndroidReleaseApplicability -GitHubOutput $documentationOutputPath
    Assert-Contains $documentationOutput "Android release applicable: false"
    Assert-Contains (Get-Content -Raw -LiteralPath $documentationOutputPath) "applicable=false"
}

if (Test-Path -LiteralPath $WorkingRoot) {
    Remove-Item -LiteralPath $WorkingRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $WorkingRoot | Out-Null

try {
    switch ($Scenario) {
        "DuplicateGuid" { Test-DuplicateGuidFixture }
        "TestScene" { Test-TestSceneFixture }
        "MissingScene" { Test-MissingSceneFixture }
        "MalformedJson" { Test-MalformedJsonFixture }
        "Utf8Json" { Test-Utf8JsonFixture }
        "Utf8Event" { Test-Utf8EventFixture }
        "MutableAction" { Test-MutableActionFixture }
        "DiagnosticSanitization" { Test-DiagnosticSanitizationFixture }
        "MixedScope" { Test-MixedScopeFixture }
        "MixedModeRationale" { Test-MixedModeRationaleFixture }
        "Coordination" { Test-CoordinationFixture }
        "PushMain" { Test-PushMainFixture }
        "RetiredPrefix" { Test-RetiredPrefixFixture }
        "A2Convention" { Test-A2ConventionFixture }
        "A2Authority" { Test-A2AuthorityFixture }
        "EngineeringToolClassification" { Test-EngineeringToolClassificationFixture }
        "PolicyAuthority" { Test-PolicyAuthorityFixture }
        "PullRequestRange" { Test-PullRequestRangeFixture }
        "InvalidBase" { Test-InvalidBaseFixture }
        "StackedBase" { Test-StackedBaseFixture }
        "PathChanges" { Test-PathChangesFixture }
        "DeletedSharedFile" { Test-DeletedSharedFileFixture }
        "AndroidReleaseApplicability" { Test-AndroidReleaseApplicabilityFixture }
        "All" {
            Test-DuplicateGuidFixture
            Test-TestSceneFixture
            Test-MissingSceneFixture
            Test-MalformedJsonFixture
            Test-Utf8JsonFixture
            Test-Utf8EventFixture
            Test-MutableActionFixture
            Test-DiagnosticSanitizationFixture
            Test-MixedScopeFixture
            Test-MixedModeRationaleFixture
            Test-CoordinationFixture
            Test-PushMainFixture
            Test-RetiredPrefixFixture
            Test-A2ConventionFixture
            Test-A2AuthorityFixture
            Test-EngineeringToolClassificationFixture
            Test-PolicyAuthorityFixture
            Test-PullRequestRangeFixture
            Test-InvalidBaseFixture
            Test-StackedBaseFixture
            Test-PathChangesFixture
            Test-DeletedSharedFileFixture
            Test-AndroidReleaseApplicabilityFixture
        }
    }

    Write-Host "Quality gate fixture scenario '$Scenario' passed."
} finally {
    if (Test-Path -LiteralPath $WorkingRoot) {
        Remove-Item -LiteralPath $WorkingRoot -Recurse -Force
    }
}
