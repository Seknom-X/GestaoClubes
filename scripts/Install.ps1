#Requires -Version 7.3
param([switch]$Seed)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
. "$PSScriptRoot/Configure.ps1"
docker compose up -d --wait
if($LASTEXITCODE){throw 'MySQL não iniciou.'}
dotnet restore GestaoClubes.sln
if($LASTEXITCODE){throw 'Falha no restore.'}
dotnet build GestaoClubes.sln --no-restore
if($LASTEXITCODE){throw 'Falha na compilação.'}
if($Seed){dotnet run --project src/GestaoClubes.Api --no-build -- --seed}else{dotnet run --project src/GestaoClubes.Api --no-build -- --migrate}
if($LASTEXITCODE){throw 'Falha na migration/seed. Seed só é aceito em banco sem contas.'}
Write-Host 'Pronto. Execute Start.ps1 -Service Api e Start.ps1 -Service Web em terminais separados.'
