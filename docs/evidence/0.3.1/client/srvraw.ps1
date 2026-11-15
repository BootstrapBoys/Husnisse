$log = 'V:\Gate031\BepInEx\LogOutput.log'
$lines = Get-Content $log
Write-Output ('lines ' + $lines.Count)
$lines | Select-Object -Last 60 | ForEach-Object { Write-Output $_ }
