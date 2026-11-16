$ErrorActionPreference = 'Stop'
$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $script:RepoRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $script:RepoRoot '.packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$script:Dotnet = Join-Path $script:RepoRoot '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $script:Dotnet)) { $script:Dotnet = 'dotnet' }
Set-Location -LiteralPath $script:RepoRoot
