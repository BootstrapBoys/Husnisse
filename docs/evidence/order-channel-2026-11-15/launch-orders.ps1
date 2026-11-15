$ProgressPreference='SilentlyContinue'
$name = "HusnisseOrdersLaunch"
$exe = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim.exe"
$arguments = "-console -joinserverwithcharacter 192.168.1.136:2856 nissetester -password mtest"
$action = New-ScheduledTaskAction -Execute $exe -Argument $arguments -WorkingDirectory (Split-Path $exe)
$principal = New-ScheduledTaskPrincipal -UserId "Hermes" -LogonType Interactive -RunLevel Highest
Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Force | Out-Null
Write-Host ("registered " + $name)
$t = Get-ScheduledTask -TaskName $name
$t.Actions | ForEach-Object { Write-Host ("  " + $_.Execute + " " + $_.Arguments) }
Start-ScheduledTask -TaskName $name
Start-Sleep -Seconds 3
Get-Process valheim -ErrorAction SilentlyContinue | Select-Object Id,StartTime | Format-Table -AutoSize
