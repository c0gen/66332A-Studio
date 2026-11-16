. "$PSScriptRoot\environment.ps1"
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot 'artifacts/66332A Studio Preview'))
$expectedDirectory = [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot 'artifacts/66332A Studio Preview'))
if (Test-Path -LiteralPath $outputDirectory) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputDirectory).Path
    if ($resolvedOutput -ne $expectedDirectory -or -not $resolvedOutput.StartsWith($script:RepoRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Publish output path is outside the intended workspace directory.' }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
& $script:Dotnet publish src/Agilent66332A.Desktop --configuration Release --runtime win-x64 --self-contained true -m:1 -nr:false -p:RestoreLockedMode=true -p:UseSharedCompilation=false -p:PublishSingleFile=false -p:PublishTrimmed=false -o 'artifacts/66332A Studio Preview'
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath README.md,LICENSE,NOTICE,THIRD-PARTY-NOTICES.md -Destination 'artifacts/66332A Studio Preview'
Copy-Item -LiteralPath examples -Destination 'artifacts/66332A Studio Preview' -Recurse -Force
Copy-Item -LiteralPath plans,docs -Destination 'artifacts/66332A Studio Preview' -Recurse -Force
& "$PSScriptRoot\collect-notices.ps1" -Destination (Join-Path $script:RepoRoot 'artifacts/66332A Studio Preview')
$appPath = Join-Path $script:RepoRoot 'artifacts/66332A Studio Preview/Agilent66332A.Desktop.exe'
Get-FileHash -LiteralPath $appPath -Algorithm SHA256 | Format-List | Out-File 'artifacts/66332A Studio Preview/SHA256.txt'
$buildInfo = @{ appVersion = '0.1.0-preview'; sdk = (& $script:Dotnet --version); target = 'win-x64'; selfContained = $true; hardwareAccepted = $false; developmentOS = [System.Environment]::OSVersion.VersionString }
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'build.json') -Encoding utf8
$checksums = Get-ChildItem -LiteralPath $outputDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relativePath = $_.FullName.Substring($outputDirectory.Length + 1).Replace('\','/')
    "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $relativePath"
}
$checksums | Set-Content -LiteralPath (Join-Path $outputDirectory 'SHA256SUMS.txt') -Encoding ascii
