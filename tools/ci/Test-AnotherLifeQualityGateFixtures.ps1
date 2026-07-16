param(
    [string] $WorkingRoot = "",
    [ValidateSet("All", "DuplicateGuid", "TestScene", "MissingScene", "MalformedJson", "MutableAction", "MixedScope", "Coordination", "RetiredPrefix")]
    [string] $Scenario = "All"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
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

    $text = ($output | Out-String)
    if ($ExpectFailure) {
        if ($exitCode -eq 0) {
            throw "Expected failure from $FilePath $($Arguments -join ' '), but it succeeded.`n$text"
        }
    } elseif ($exitCode -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')`n$text"
    }

    return $text
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
        $output = Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Hygiene", "-BaseRef", "HEAD") $fixtureRepo -ExpectFailure
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

function Test-MutableActionFixture {
    Invoke-HygieneFailureFixture "mutable-action" {
        param($fixtureRepo)
        New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo ".github/workflows") | Out-Null
        Set-Content -LiteralPath (Join-Path $fixtureRepo ".github/workflows/bad.yml") -Value @"
name: Bad
on: [pull_request]
jobs:
  bad:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
"@
    } "uses a mutable major-version action tag"
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
    "body": "- [x] Codex engineering\n\nFixes #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "codex/quality-gate-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        $output = Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
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
        Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
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

function Test-RetiredPrefixFixture {
    $fixtureRepo = New-FixtureRepo "retired-gpt"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRepo "unity/Docs") | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRepo "unity/Docs/Retired.md") -Value "# Retired fixture"
    $eventPath = Join-Path $fixtureRepo "event.json"
    Set-Content -LiteralPath $eventPath -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex coordination/review\n\nRefs #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "gpt/retired-fixture" }
  }
}
"@

    Push-Location $fixtureRepo
    try {
        Invoke-Checked git @("add", ".") $fixtureRepo | Out-Null
        $output = Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $fixtureRepo @{
            GITHUB_ACTIONS = ""
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "gpt/retired-fixture"
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $output "Codex-only AnotherLife prefix"
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
        "MutableAction" { Test-MutableActionFixture }
        "MixedScope" { Test-MixedScopeFixture }
        "Coordination" { Test-CoordinationFixture }
        "RetiredPrefix" { Test-RetiredPrefixFixture }
        "All" {
            Test-DuplicateGuidFixture
            Test-TestSceneFixture
            Test-MissingSceneFixture
            Test-MalformedJsonFixture
            Test-MutableActionFixture
            Test-MixedScopeFixture
            Test-CoordinationFixture
            Test-RetiredPrefixFixture
        }
    }

    Write-Host "Quality gate fixture scenario '$Scenario' passed."
} finally {
    if (Test-Path -LiteralPath $WorkingRoot) {
        Remove-Item -LiteralPath $WorkingRoot -Recurse -Force
    }
}