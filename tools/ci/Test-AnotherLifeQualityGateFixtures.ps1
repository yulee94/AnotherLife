param(
    [string] $WorkingRoot = ""
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

if (Test-Path -LiteralPath $WorkingRoot) {
    Remove-Item -LiteralPath $WorkingRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $WorkingRoot | Out-Null

try {
    $hygieneRepo = New-FixtureRepo "hygiene"
    New-Item -ItemType Directory -Force -Path (Join-Path $hygieneRepo ".github/workflows") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $hygieneRepo "unity/Assets") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $hygieneRepo "unity/ProjectSettings") | Out-Null

    Set-Content -LiteralPath (Join-Path $hygieneRepo ".github/workflows/bad.yml") -Value @"
name: Bad
on: [pull_request]
jobs:
  bad:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
"@

    $guid = "0123456789abcdef0123456789abcdef"
    Set-Content -LiteralPath (Join-Path $hygieneRepo "unity/Assets/A.meta") -Value "guid: $guid"
    Set-Content -LiteralPath (Join-Path $hygieneRepo "unity/Assets/B.meta") -Value "guid: $guid"
    Set-Content -LiteralPath (Join-Path $hygieneRepo "bad.json") -Value "{ malformed"
    Set-Content -LiteralPath (Join-Path $hygieneRepo "unity/ProjectSettings/EditorBuildSettings.asset") -Value @"
EditorBuildSettings:
  m_Scenes:
  - enabled: 1
    path: Assets/Test.unity
  - enabled: 1
    path: Assets/Missing.unity
"@

    Push-Location $hygieneRepo
    try {
        Invoke-Checked git @("add", ".") $hygieneRepo | Out-Null
        $hygieneOutput = Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Hygiene", "-BaseRef", "HEAD") $hygieneRepo -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $hygieneOutput "Duplicate Unity meta GUID"
    Assert-Contains $hygieneOutput "Assets/Test.unity must not be enabled"
    Assert-Contains $hygieneOutput "Enabled Build Settings scene is missing"
    Assert-Contains $hygieneOutput "Malformed JSON file"
    Assert-Contains $hygieneOutput "uses a mutable major-version action tag"

    $classifyRepo = New-FixtureRepo "classify"
    New-Item -ItemType Directory -Force -Path (Join-Path $classifyRepo "unity/Docs/Terrestrials") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $classifyRepo "unity/Assets/AL/Scripts") | Out-Null
    Set-Content -LiteralPath (Join-Path $classifyRepo "unity/Docs/Terrestrials/Design.md") -Value "# Fixture"
    Set-Content -LiteralPath (Join-Path $classifyRepo "unity/Assets/AL/Scripts/RuntimeFixture.cs") -Value "public class RuntimeFixture {}"

    $eventPath = Join-Path $classifyRepo "event.json"
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

    Push-Location $classifyRepo
    try {
        Invoke-Checked git @("add", ".") $classifyRepo | Out-Null
        $classifyOutput = Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $classifyRepo @{
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $eventPath
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "codex/quality-gate-fixture"
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $classifyOutput "Terrestrial design paths changed: 1"
    Assert-Contains $classifyOutput "Engineering/workflow paths changed: 1"
    Assert-Contains $classifyOutput "Source-mode and engineering paths are mixed"

    $coordinationRepo = New-FixtureRepo "coordination"
    New-Item -ItemType Directory -Force -Path (Join-Path $coordinationRepo "unity/Docs") | Out-Null
    Set-Content -LiteralPath (Join-Path $coordinationRepo "unity/Docs/Governance.md") -Value "# Governance fixture"
    $coordinationEvent = Join-Path $coordinationRepo "event.json"
    Set-Content -LiteralPath $coordinationEvent -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex coordination/review\n\nRefs #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "codex/coordination-fixture" }
  }
}
"@

    Push-Location $coordinationRepo
    try {
        Invoke-Checked git @("add", ".") $coordinationRepo | Out-Null
        Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $coordinationRepo @{
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $coordinationEvent
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "codex/coordination-fixture"
        } | Out-Null
    } finally {
        Pop-Location
    }

    $retiredRepo = New-FixtureRepo "retired-gpt"
    New-Item -ItemType Directory -Force -Path (Join-Path $retiredRepo "unity/Docs") | Out-Null
    Set-Content -LiteralPath (Join-Path $retiredRepo "unity/Docs/Retired.md") -Value "# Retired fixture"
    $retiredEvent = Join-Path $retiredRepo "event.json"
    Set-Content -LiteralPath $retiredEvent -Value @"
{
  "pull_request": {
    "draft": false,
    "body": "- [x] Codex coordination/review\n\nRefs #155\n\n## Shared-file lock\n\nNone.",
    "base": { "ref": "main" },
    "head": { "ref": "gpt/retired-fixture" }
  }
}
"@

    Push-Location $retiredRepo
    try {
        Invoke-Checked git @("add", ".") $retiredRepo | Out-Null
        $retiredOutput = Invoke-Checked powershell @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\tools\ci\Invoke-AnotherLifeQualityGate.ps1", "-Mode", "Classify", "-BaseRef", "HEAD") $retiredRepo @{
            GITHUB_EVENT_NAME = "pull_request"
            GITHUB_EVENT_PATH = $retiredEvent
            GITHUB_BASE_REF = "main"
            GITHUB_HEAD_REF = "gpt/retired-fixture"
        } -ExpectFailure
    } finally {
        Pop-Location
    }

    Assert-Contains $retiredOutput "Codex-only AnotherLife prefix"

    Write-Host "Quality gate fixture self-tests passed."
} finally {
    if (Test-Path -LiteralPath $WorkingRoot) {
        Remove-Item -LiteralPath $WorkingRoot -Recurse -Force
    }
}