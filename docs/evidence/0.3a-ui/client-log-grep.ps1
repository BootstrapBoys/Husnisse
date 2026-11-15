# Print the client gate/probe lines that matter for this session.
$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
if (-not (Test-Path $log)) { Write-Output "no log"; exit }
Select-String -Path $log -Pattern "gc loaded|gc ready|gc aborting|rule RPC registered|content registered|Husnisse 0.3" |
    ForEach-Object { Write-Output $_.Line }
Write-Output "== tail =="
Get-Content $log -Tail 4
