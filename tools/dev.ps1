param([switch]$Simulator)
. "$PSScriptRoot\environment.ps1"
$appArgs = @()
if ($Simulator) { $appArgs += '--simulator' }
& $script:Dotnet run --project src/Agilent66332A.Desktop -- @appArgs
if ($LASTEXITCODE -ne 0) { throw "App exited with code $LASTEXITCODE" }
