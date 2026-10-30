#Requires -Version 7.3
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Configure.ps1" -LoadOnly
$base='http://localhost:5282'
$clubId=((Invoke-RestMethod 'http://localhost:5281/api/public/clubs') | Where-Object name -eq 'Clube Foz' | Select-Object -First 1).id
if(!$clubId){throw 'Execute o seed acadêmico antes deste teste.'}
function Get-Token($page){$match=[regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"');if(!$match.Success){throw 'Token antifalsificação ausente.'};return $match.Groups[1].Value}
$login=Invoke-WebRequest "$base/Portal/Login" -SessionVariable browser
$token=Get-Token $login
$result=Invoke-WebRequest "$base/Portal/Login" -Method Post -WebSession $browser -Body @{Email='superadmin@example.invalid';Password=$env:Seed__Password;__RequestVerificationToken=$token}
if($result.Content -notmatch 'Administração global'){throw 'Login MVC não chegou ao painel.'}
foreach($module in @('members','events','reservations','finance','tickets','audit','club')){
 $page=Invoke-WebRequest "$base/Portal/Panel?clubId=$clubId&module=$module" -WebSession $browser
 if($page.Content -match 'Acesso recusado'){throw "Módulo $module recusado para superadmin."}
 if($page.StatusCode -ne 200){throw "Módulo $module falhou."}
}
$eventPage=Invoke-WebRequest "$base/Portal/Panel?clubId=$clubId&module=events" -WebSession $browser
$start=[DateTime]::UtcNow.AddDays(20).ToString('yyyy-MM-ddTHH:mm')
$end=[DateTime]::UtcNow.AddDays(20).AddHours(2).ToString('yyyy-MM-ddTHH:mm')
$title='Evento teste MVC '+[Guid]::NewGuid().ToString('N')
$saved=Invoke-WebRequest "$base/Portal/Event" -Method Post -WebSession $browser -Body @{clubId=$clubId;Title=$title;StartUtc=$start;EndUtc=$end;Public='true';Cancelled='false';__RequestVerificationToken=(Get-Token $eventPage)}
if($saved.Content -notmatch [regex]::Escape($title)){throw 'Evento não persistiu através do MVC.'}
$blocked=Invoke-WebRequest "$base/Portal/Login" -Method Post -Body @{Email='superadmin@example.invalid';Password='invalida'} -SkipHttpErrorCheck
if($blocked.StatusCode -ne 400){throw 'POST sem antifalsificação deveria ser recusado.'}
Write-Host 'PASSOU: login MVC, 7 módulos, criação de evento via formulário e proteção antifalsificação.'
