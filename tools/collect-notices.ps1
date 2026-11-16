param([Parameter(Mandatory=$true)][string]$Destination)
. "$PSScriptRoot\environment.ps1"
$licenseRoot = Join-Path $Destination 'Licenses'
New-Item -ItemType Directory -Force -Path $licenseRoot | Out-Null
$assetsPath = Join-Path $script:RepoRoot 'src/Agilent66332A.Desktop/obj/project.assets.json'
$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
$index = [System.Collections.Generic.List[string]]::new()
$index.Add('NuGet dependency licensing inventory (including transitive/runtime packages).')
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $packageDirectory = Join-Path $env:NUGET_PACKAGES $library.Value.path
    $targetDirectory = Join-Path $licenseRoot ($library.Name.Replace('/', '-'))
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
    $metadata = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -File | Select-Object -First 1
    if ($metadata) {
        Copy-Item -LiteralPath $metadata.FullName -Destination $targetDirectory
        [xml]$spec = Get-Content -LiteralPath $metadata.FullName -Raw
        $index.Add("$($library.Name): $($spec.package.metadata.license.InnerText) $($spec.package.metadata.licenseUrl)")
    }
    Get-ChildItem -LiteralPath $packageDirectory -File -Recurse | Where-Object { $_.Name -match '(?i)license|notice|copying' } | ForEach-Object {
        $relativeName = $_.FullName.Substring($packageDirectory.Length).TrimStart('\','/').Replace('\','_').Replace('/','_')
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetDirectory $relativeName)
    }
}
$runtimeSource = Join-Path $script:RepoRoot '.dotnet'
if (-not (Test-Path -LiteralPath (Join-Path $runtimeSource 'LICENSE.txt'))) { $runtimeSource = Split-Path -Parent (Get-Command dotnet).Source }
foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
    $source = Join-Path $runtimeSource $notice
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $licenseRoot "dotnet-$notice") }
}
$index | Set-Content -LiteralPath (Join-Path $licenseRoot 'DEPENDENCIES.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $script:RepoRoot 'third-party-licenses') -Destination $licenseRoot -Recurse -Force
