# Report the client's current state: Valheim running, task definition, plugin folders, stage dir.
$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"

Write-Host "== procs =="
Get-CimInstance Win32_Process -Filter "name like '%valheim%'" |
    Select-Object ProcessId, Name, SessionId, CommandLine | Format-List

Write-Host "== task =="
schtasks /query /tn "HusnisseLegsLaunch" /v /fo LIST 2>&1 | Select-String -Pattern "TaskName|Task To Run|Start In|Status|Last Run|Next Run"

Write-Host "== plugins =="
Get-ChildItem (Join-Path $g "BepInEx\plugins") -Directory | Select-Object -ExpandProperty Name

Write-Host "== stage =="
Get-ChildItem "C:\Users\Hermes\stage" -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime

Write-Host "== cmd file =="
Get-Content (Join-Path $env:USERPROFILE "husnisse-probe.cmd") -ErrorAction SilentlyContinue

Write-Host "== client log tail =="
Get-Content (Join-Path $g "BepInEx\LogOutput.log") -Tail 5 -ErrorAction SilentlyContinue
