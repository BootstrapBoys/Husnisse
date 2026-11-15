# Print the last N lines of the client BepInEx log (default 30).
param([int]$Lines = 30)
$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
Get-Content $log -Tail $Lines
