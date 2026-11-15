# Register (but do not start) the order-gate launch task: joins 192.168.1.136:2856.
# The game only starts when Start-ScheduledTask is called, so running this while the client is
# busy is safe.
$ErrorActionPreference = "Stop"
$name = "HusnisseOrdersLaunch"
$exe = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim.exe"
$arguments = "-console -joinserverwithcharacter 192.168.1.136:2856 nissetester -password mtest"
$action = New-ScheduledTaskAction -Execute $exe -Argument $arguments -WorkingDirectory (Split-Path $exe)
$principal = New-ScheduledTaskPrincipal -UserId "Hermes" -LogonType Interactive -RunLevel Highest
Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Force | Out-Null
Write-Host ("registered " + $name)
(Get-ScheduledTask -TaskName $name).Actions | ForEach-Object { Write-Host ("  " + $_.Execute + " " + $_.Arguments) }
