$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$sdkVersion = (Get-Content -LiteralPath (Join-Path $repoPath 'global.json') -Raw | ConvertFrom-Json).sdk.version
$downloadDirectory = Join-Path $repoPath 'artifacts/bootstrap'
New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null
$installerPath = Join-Path $downloadDirectory 'dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerPath
& $installerPath -Version $sdkVersion -InstallDir (Join-Path $repoPath '.dotnet') -NoPath
if (-not (Test-Path -LiteralPath (Join-Path $repoPath '.dotnet/dotnet.exe'))) { throw 'SDK setup failed.' }
