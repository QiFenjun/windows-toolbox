param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = [IO.Path]::GetFullPath(
    (Join-Path $projectRoot "..\..")
)
$portableDotnet = [IO.Path]::GetFullPath(
    (Join-Path $workspaceRoot "work\dotnet-sdk\dotnet.exe")
)
$dotnetCommand = if (Test-Path -LiteralPath $portableDotnet) {
    $portableDotnet
}
else {
    "dotnet"
}

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot ".dotnet"
$env:NUGET_PACKAGES = Join-Path $workspaceRoot ".packages"
New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
New-Item -ItemType Directory -Path $env:NUGET_PACKAGES -Force | Out-Null

Push-Location $projectRoot
try {
    & $dotnetCommand restore WindowsToolbox.sln
    & $dotnetCommand build WindowsToolbox.sln --configuration $Configuration --no-restore
    & $dotnetCommand test WindowsToolbox.sln --configuration $Configuration --no-build
}
finally {
    Pop-Location
}
