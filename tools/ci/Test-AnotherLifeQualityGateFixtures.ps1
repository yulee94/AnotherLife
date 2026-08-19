param(
    [string] $WorkingRoot = "",
    [ValidateSet(
        "All",
        "DuplicateGuid",
        "TestScene",
        "MissingScene",
        "MalformedJson",
        "Utf8Json",
        "MutableAction",
        "DiagnosticSanitization",
        "PushMain",
        "PullRequestRange",
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
        [string] $HeadBranch = "quality-gate-fixture"
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
        [ValidateSet("Hygiene", "AndroidReleaseApplicability")]
        [string] $Mode = "Hygiene",
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
        # AndroidReleaseApplicability fails while discovering changed files,
        # so this exercises the top-level catch rather than only Add-Failure.
        $rootOutput = Invoke-DiagnosticSanitizationProbe `
            $fixtureRepo "AndroidReleaseApplicability" $rootVariant
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
            $fixtureRepo "AndroidReleaseApplicability" $siblingRoot
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
        $fixtureRepo "AndroidReleaseApplicability" $unixReverseRoot @{
            GITHUB_WORKSPACE = $unixRoot
        }
    Assert-Contains $unixOutput "<repo>"
    Assert-NotContains $unixOutput $unixRoot
    Assert-NotContains $unixOutput $unixReverseRoot

    $unixCaseDistinctRoot = "/home/runner/work/anotherlife/anotherlife"
    $unixCaseOutput = Invoke-DiagnosticSanitizationProbe `
        $fixtureRepo "AndroidReleaseApplicability" $unixCaseDistinctRoot @{
            GITHUB_WORKSPACE = $unixRoot
        }
    Assert-Contains $unixCaseOutput $unixCaseDistinctRoot
    Assert-NotContains $unixCaseOutput "<repo>"
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
        $mismatchOutput = Invoke-Checked $powerShellExecutable @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Hygiene") $fixtureRepo @{
            GITHUB_ACTIONS = "true"
            GITHUB_EVENT_NAME = "push"
            GITHUB_EVENT_PATH = $mismatchEventPath
            GITHUB_BASE_REF = ""
            GITHUB_HEAD_REF = ""
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $hygieneOutput "Running git diff --check against $before..$after"
    Assert-Contains $mismatchOutput "does not match event after SHA"
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

    $body = "Range fixture"
    $eventPath = Join-Path $fixtureRepo "event.json"
    Write-PullRequestEvent $eventPath $base $head $body
    $output = Invoke-PullRequestGate $fixtureRepo $eventPath -Mode AndroidReleaseApplicability

    Assert-Contains $output "app/src/main/PullRequestRangeFixture.kt"
    Assert-Contains $output "Android release applicable: true"

    Invoke-Checked git @("checkout", "--detach", $base) $fixtureRepo | Out-Null
    try {
        $mismatchOutput = Invoke-PullRequestGate $fixtureRepo $eventPath -Mode AndroidReleaseApplicability -ExpectFailure
    } finally {
        Invoke-Checked git @("checkout", "--detach", $head) $fixtureRepo | Out-Null
    }
    Assert-Contains $mismatchOutput "does not match event head SHA"

    $zeroEventPath = Join-Path $fixtureRepo "event-zero-base.json"
    Write-PullRequestEvent $zeroEventPath ("0" * 40) $head $body
    $zeroOutput = Invoke-PullRequestGate $fixtureRepo $zeroEventPath -Mode AndroidReleaseApplicability -ExpectFailure
    Assert-Contains $zeroOutput "base/head commit SHAs are invalid"

    $malformedEventPath = Join-Path $fixtureRepo "event-malformed-head.json"
    Write-PullRequestEvent $malformedEventPath $base "not-a-commit-sha" $body
    $malformedOutput = Invoke-PullRequestGate $fixtureRepo $malformedEventPath -Mode AndroidReleaseApplicability -ExpectFailure
    Assert-Contains $malformedOutput "base/head commit SHAs are invalid"

    $missingEventPath = Join-Path $fixtureRepo "event-missing-base.json"
    Write-PullRequestEvent $missingEventPath ("1" * 40) $head $body
    $missingOutput = Invoke-PullRequestGate $fixtureRepo $missingEventPath -Mode AndroidReleaseApplicability -ExpectFailure
    Assert-Contains $missingOutput ("1" * 40)
    Assert-Contains $missingOutput "not an available"
}

function Test-AndroidReleaseApplicabilityFixture {
    $fixtureRepo = New-FixtureRepo "android-release-applicability"
    $body = "Android release applicability fixture"

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
        "MutableAction" { Test-MutableActionFixture }
        "DiagnosticSanitization" { Test-DiagnosticSanitizationFixture }
        "PushMain" { Test-PushMainFixture }
        "PullRequestRange" { Test-PullRequestRangeFixture }
        "AndroidReleaseApplicability" { Test-AndroidReleaseApplicabilityFixture }
        "All" {
            Test-DuplicateGuidFixture
            Test-TestSceneFixture
            Test-MissingSceneFixture
            Test-MalformedJsonFixture
            Test-Utf8JsonFixture
            Test-MutableActionFixture
            Test-DiagnosticSanitizationFixture
            Test-PushMainFixture
            Test-PullRequestRangeFixture
            Test-AndroidReleaseApplicabilityFixture
        }
    }

    Write-Host "Quality gate fixture scenario '$Scenario' passed."
} finally {
    if (Test-Path -LiteralPath $WorkingRoot) {
        Remove-Item -LiteralPath $WorkingRoot -Recurse -Force
    }
}
