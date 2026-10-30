#Requires -Version 7.3
param([switch]$LoadOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$envFile=Join-Path $root '.env'
if(!(Test-Path $envFile)) {
 if($LoadOnly){throw 'Execute Configure.ps1 primeiro.'}
 function New-Key { [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
 @("CLUBS_DB_PASSWORD=$(New-Key)","CLUBS_ROOT_PASSWORD=$(New-Key)","Jwt__Key=$(New-Key)","Security__CpfKey=$(New-Key)","Seed__Password=$(New-Key)") | Set-Content -LiteralPath $envFile -Encoding utf8
 Write-Host 'Configuração local criada. Senhas estão em .env (não versionar).'
}
foreach($line in Get-Content -LiteralPath $envFile){if($line -match '^([^#=]+)=(.*)$'){[Environment]::SetEnvironmentVariable($Matches[1],$Matches[2],'Process')}}
$env:ConnectionStrings__Clubs="Server=127.0.0.1;Port=33316;Database=gestaoclubes;User=gestaoclubes_app;Password=$env:CLUBS_DB_PASSWORD;SslMode=None"
$env:Security__KeyPath=Join-Path $root 'secrets/keys'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ApiUrl='http://localhost:5281'
