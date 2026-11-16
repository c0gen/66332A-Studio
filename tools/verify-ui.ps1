. "$PSScriptRoot\environment.ps1"
& $script:Dotnet build src/Agilent66332A.Desktop -m:1 -nr:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$evidence = Join-Path $script:RepoRoot 'artifacts/ui-evidence'
$dataPath = Join-Path $script:RepoRoot 'artifacts/ui-data'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$binary = Join-Path $script:RepoRoot 'src/Agilent66332A.Desktop/bin/Debug/net10.0-windows/Agilent66332A.Desktop.exe'
$arguments = "--data-root `"$dataPath`" --capture-artifacts `"$evidence`" --smoke-report `"$evidence/error.txt`""
$process = Start-Process -FilePath $binary -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'WPF integration check timed out.' }
if ($process.ExitCode -ne 0) { throw "WPF integration failed. See $evidence/error.txt" }
Get-Content -LiteralPath (Join-Path $evidence 'verification.txt')
