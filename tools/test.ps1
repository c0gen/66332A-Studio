. "$PSScriptRoot\environment.ps1"
& $script:Dotnet test Agilent66332A.sln --configuration Release -m:1 -nr:false -p:UseSharedCompilation=false --logger 'trx' --results-directory artifacts/test-results
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
