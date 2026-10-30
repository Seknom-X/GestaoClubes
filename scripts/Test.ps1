#Requires -Version 7.3
param([switch]$Integration)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if($Integration){. "$PSScriptRoot/Configure.ps1" -LoadOnly;$env:CLUBS_TEST_URL='http://localhost:5281'}else{Remove-Item Env:CLUBS_TEST_URL -ErrorAction SilentlyContinue}
dotnet test GestaoClubes.sln --logger 'trx;LogFileName=GestaoClubes.trx' --results-directory artifacts/tests
if($LASTEXITCODE){throw 'Testes falharam.'}
