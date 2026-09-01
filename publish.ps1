<#
.SYNOPSIS
    Builds a runnable Ratna Bay into .\build and optionally launches it.

.DESCRIPTION
    One command, one folder, one executable to double-click. The output is self-contained
    by default, so it runs on a machine with no .NET installed — which is what makes it
    something you can hand to a playtester rather than only run yourself.

    The build is gated: the domain tests and the headless save round-trip both have to pass
    before the folder is published. A build that ships a broken save is worse than no build.

.PARAMETER Run
    Launch the game as soon as it is built.

.PARAMETER SkipTests
    Skip the domain tests and the simulation. For quick iteration, and for callers that have
    just run them themselves -- verify.ps1 does exactly that before it packages.

    It used to skip the packaged self-test as well, which conflated two different things: the
    domain tests prove the rules, and the self-test proves the artifact. verify.ps1 -Pack
    passes this flag, so the one gate that runs a playthrough was also the one that skipped
    the 219 checks on the folder people download.

.PARAMETER SkipSelfTest
    Skip the packaged build's own self-test. Never for a build you hand to someone else --
    this is the only check that runs the real executable out of the real folder.

.PARAMETER Framework
    Produce a smaller framework-dependent build (needs the .NET 9 desktop runtime installed).

.PARAMETER Clean
    Delete .\build before publishing, so nothing stale survives.

.EXAMPLE
    .\publish.ps1 -Run
    Build everything and start playing.

.EXAMPLE
    .\publish.ps1 -Clean
    A cold, fully verified build in .\build.
#>
param(
    [switch] $Run,
    [switch] $SkipTests,
    [switch] $SkipSelfTest,
    [switch] $Framework,
    [switch] $Clean,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $PSScriptRoot).Path
$buildDir = Join-Path $root 'build'
$gameProject = Join-Path $root 'src\RatnaBay.Game\RatnaBay.Game.csproj'
$testProject = Join-Path $root 'tests\RatnaBay.Domain.Tests\RatnaBay.Domain.Tests.csproj'
$exePath = Join-Path $buildDir 'RatnaBay.exe'
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Write-Step([string] $message) {
    Write-Host ''
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Assert-LastExitCode([string] $what) {
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

Push-Location $root
try {
    if ($Clean -and (Test-Path $buildDir)) {
        Write-Step 'Clearing the previous build'
        Remove-Item $buildDir -Recurse -Force
    }

    Write-Step 'Restoring tools and packages'
    dotnet tool restore --tool-manifest 'src\RatnaBay.Game\.config\dotnet-tools.json'
    Assert-LastExitCode 'dotnet tool restore'
    dotnet restore 'RatnaBay.sln'
    Assert-LastExitCode 'dotnet restore'

    if (-not $SkipTests) {
        Write-Step 'Running the domain tests'
        dotnet test $testProject --configuration $Configuration --nologo `
            --logger 'console;verbosity=minimal'
        Assert-LastExitCode 'dotnet test'

        Write-Step 'Running the deterministic playthrough simulation'
        dotnet run --project 'tools\RatnaBay.Tools\RatnaBay.Tools.csproj' `
            --configuration $Configuration --no-restore -- sim
        Assert-LastExitCode 'playthrough simulation'
    }

    Write-Step "Publishing to .\build ($Configuration, $(if ($Framework) { 'framework-dependent' } else { 'self-contained' }))"
    $selfContained = if ($Framework) { 'false' } else { 'true' }
    dotnet publish $gameProject `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained $selfContained `
        --output $buildDir `
        --nologo
    Assert-LastExitCode 'dotnet publish'

    if (-not (Test-Path $exePath)) { throw "Publish finished but $exePath is missing." }

    # The two things most likely to be silently absent, and both make the game unplayable
    # rather than merely wrong.
    Write-Step 'Verifying the published folder'
    $checks = @(
        @{ Name = 'compiled content'; Path = Join-Path $buildDir 'Content\Feasibility' },
        @{ Name = 'bundled fonts'; Path = Join-Path $buildDir 'Content\Feasibility\Fonts\Cinzel\Cinzel-wght.ttf' },
        @{ Name = 'carving font'; Path = Join-Path $buildDir 'Content\Feasibility\Fonts\NotoSansBrahmi\NotoSansBrahmi-Regular.ttf' },
        @{ Name = 'world manifest'; Path = Join-Path $buildDir 'Content\World\northwatch.json' },
        @{ Name = 'dialogue manifest'; Path = Join-Path $buildDir 'Content\Dialogue\northwatch.json' },
        @{ Name = 'quest manifest'; Path = Join-Path $buildDir 'Content\Quests\northwatch.json' },
        @{ Name = 'shop manifest'; Path = Join-Path $buildDir 'Content\Shops\northwatch.json' }
    )
    foreach ($check in $checks) {
        if (-not (Test-Path $check.Path)) { throw "The build is missing $($check.Name): $($check.Path)" }
        Write-Host "    [ok] $($check.Name)"
    }

    # A self-contained publish without PublishSingleFile drops 277 loose runtime DLLs into the
    # root of the folder a player just extracted. It runs, and it looks like something went
    # wrong -- next to a SmartScreen warning that is two reasons to close the folder.
    #
    # Checked rather than trusted, because the way it comes back is silent: any csproj change
    # that stops the single-file properties applying still produces a working build.
    $rootEntries = @(Get-ChildItem -Path $buildDir)
    if ($rootEntries.Count -gt 4) {
        throw ("The build folder has $($rootEntries.Count) entries at its root. It should have " +
            "two: RatnaBay.exe and Content. PublishSingleFile is not taking effect.")
    }

    Write-Host "    [ok] $($rootEntries.Count) entries at the root"

    if (-not $SkipSelfTest) {
        Write-Step 'Smoke-testing the published build'
        # Runs the real executable from the real folder: if content, fonts or the save path
        # are wrong in the published layout, this is where it surfaces.
        #
        # Start-Process -Wait, not `& $exePath`. The game is a WinExe, so PowerShell does not
        # block on it — calling it directly returns instantly and the exit-code check below
        # would be reading a stale value from the previous command.
        $logPath = Join-Path $env:TEMP 'ratnabay-selftest.log'
        $selfTest = Start-Process -FilePath $exePath -ArgumentList '--selftest' `
            -WorkingDirectory $buildDir -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $logPath

        if (Test-Path $logPath) {
            Get-Content $logPath | ForEach-Object { Write-Host "    $_" }
            Remove-Item $logPath -Force
        }

        if ($selfTest.ExitCode -ne 0) {
            throw "The published build failed its self-test (exit code $($selfTest.ExitCode)). The folder is not fit to hand to anyone."
        }
    }

    $stopwatch.Stop()
    $sizeMb = [math]::Round((Get-ChildItem $buildDir -Recurse -File |
        Measure-Object -Property Length -Sum).Sum / 1MB, 1)

    Write-Host ''
    Write-Host '  Build complete.' -ForegroundColor Green
    Write-Host "    Folder   $buildDir"
    Write-Host "    Play     $exePath"
    Write-Host "    Size     $sizeMb MB"
    Write-Host "    Took     $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1))s"
    Write-Host ''
    Write-Host '  Double-click RatnaBay.exe in the build folder to play.' -ForegroundColor Gray

    if ($Run) {
        Write-Step 'Launching'
        Start-Process -FilePath $exePath -WorkingDirectory $buildDir
    }
}
finally {
    Pop-Location
}
