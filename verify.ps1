<#
.SYNOPSIS
    One command that proves a Ratna Bay change is safe to commit.

.DESCRIPTION
    Runs the Release build, tool doctor, domain tests, content validation, and the
    deterministic simulation. This is the check AGENTS.md asks for.

    Packaging (publish.ps1 and the packaged self-test) is opt-in with -Pack, because it
    needs a Windows machine and writes into .\build.

.PARAMETER Pack
    Also run publish.ps1 -SkipTests, then drive the packaged build through the smoke script.
    That last step is the only gate that asserts on a running client, so it needs the exe.

.PARAMETER Configuration
    Build configuration. Release is the default, matching the publish gate.
#>
param(
    [switch] $Pack,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $PSScriptRoot).Path

function Assert-LastExitCode([string] $what) {
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

Push-Location $root
try {
    & (Join-Path $root 'build.ps1') -Configuration $Configuration
    Assert-LastExitCode 'build.ps1'

    Write-Host ''
    Write-Host '==> Validating content manifests' -ForegroundColor Cyan
    dotnet run --project 'tools\RatnaBay.Tools\RatnaBay.Tools.csproj' --configuration $Configuration --no-build -- validate
    Assert-LastExitCode 'RatnaBay.Tools validate'

    Write-Host ''
    Write-Host '==> Running the deterministic simulation' -ForegroundColor Cyan
    dotnet run --project 'tools\RatnaBay.Tools\RatnaBay.Tools.csproj' --configuration $Configuration --no-build -- sim
    Assert-LastExitCode 'RatnaBay.Tools sim'

    if ($Pack) {
        Write-Host ''
        Write-Host '==> Packaging' -ForegroundColor Cyan
        & (Join-Path $root 'publish.ps1') -Configuration $Configuration -SkipTests
        Assert-LastExitCode 'publish.ps1'

        # The only gate that asserts on a running client. Everything above this proves the
        # rules; this proves the yard has a floor, the shaft is a hole you can see down, and a
        # descent still fills a room -- none of which a domain test can see.
        Write-Host ''
        Write-Host '==> Driving the build through the smoke script' -ForegroundColor Cyan
        $exe = Join-Path $root 'build\RatnaBay.exe'
        if (-not (Test-Path $exe)) { throw "No packaged build at $exe" }

        & $exe --script (Join-Path $root 'Docs\scripts\smoke.rbs')
        Assert-LastExitCode 'smoke.rbs'
    }

    Write-Host ''
    Write-Host 'verify.ps1 passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
