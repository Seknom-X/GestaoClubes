#Requires -Version 7.3
param([string]$Nome = 'pixel_7_-_api_35', [switch]$Verificar)
$ErrorActionPreference = 'Stop'
$candidatos = @(
    $env:ANDROID_HOME
    $env:ANDROID_SDK_ROOT
    "$env:LOCALAPPDATA\Android\Sdk"
    "${env:ProgramFiles(x86)}\Android\android-sdk"
    "$env:ProgramFiles\Android\android-sdk"
) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'emulator\emulator.exe')) }
$sdk = $candidatos | Select-Object -First 1
if (!$sdk) { throw 'SDK Android não encontrado. Configure ANDROID_HOME com a pasta do SDK.' }
$emulador = Join-Path $sdk 'emulator\emulator.exe'
$nomes = @(& $emulador -list-avds)
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar os emuladores instalados.' }
if ($Nome -notin $nomes) { throw "Emulador '$Nome' não encontrado. Disponíveis: $($nomes -join ', '). Use -Nome para escolher outro." }
if ($Verificar) { Write-Host "Emulador disponível: $Nome"; return }
$adb = Join-Path $sdk 'platform-tools\adb.exe'
if (Test-Path -LiteralPath $adb) {
    foreach ($linha in @(& $adb devices)) {
        if ($linha -match '^(emulator-\d+)\s+device\s*$') {
            $serial = $Matches[1]
            $avd = @(& $adb -s $serial emu avd name)
            if ($Nome -in $avd) { Write-Host 'Seu emulador já está aberto. Aguarde a tela inicial do Android e execute o app.'; return }
        }
    }
}
# Janela visível: esta tarefa abre o celular virtual solicitado pelo usuário.
Start-Process -FilePath $emulador -ArgumentList @('-avd', $Nome) -WindowStyle Normal
Write-Host 'Abrindo o celular virtual. Aguarde a tela inicial do Android antes de executar o app.'
