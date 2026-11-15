$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
Select-String -Path $log -Pattern "Husnisse 0.3|content registered|rule RPC registered|localisation loaded|core ready|bowl registered|Failed|Exception" |
    ForEach-Object { Write-Output $_.Line } | Select-Object -First 20
