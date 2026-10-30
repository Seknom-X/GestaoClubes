#Requires -Version 7.3
param([ValidateSet('Api','Web')] [string]$Service='Web')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
. "$PSScriptRoot/Configure.ps1" -LoadOnly
$port=if($Service -eq 'Api'){5281}else{5282}
dotnet run --project "src/GestaoClubes.$Service" --no-launch-profile --urls "http://localhost:$port"
if($LASTEXITCODE){throw 'Serviço encerrou com erro.'}
