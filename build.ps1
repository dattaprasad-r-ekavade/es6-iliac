param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '.')).Path

Push-Location $root
try {
        dotnet tool restore --tool-manifest 'src\RatnaBay.Game\.config\dotnet-tools.json'
        dotnet restore 'RatnaBay.sln'
        dotnet build 'RatnaBay.sln' --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
        dotnet run --project 'tools\RatnaBay.Tools\RatnaBay.Tools.csproj' --configuration $Configuration --no-build -- doctor
        if ($LASTEXITCODE -ne 0) { throw "RatnaBay.Tools doctor failed with exit code $LASTEXITCODE" }
        dotnet test 'tests\RatnaBay.Domain.Tests\RatnaBay.Domain.Tests.csproj' --configuration $Configuration --no-build --no-restore --logger 'console;verbosity=minimal'
        if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
